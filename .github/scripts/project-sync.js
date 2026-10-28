// Keeps the "MyPoGoCompanion" GitHub project (board) in step with the repository's issues and pull requests.
// Creates the project and its fields on the first run. Safe to re-run: it only changes what differs.
//
// Status rules
//   Issue:  closed → Done · an open draft PR closes it → In progress · an open PR closes it → In review
//           · otherwise Todo (a status moved by hand is kept, except "In review" without an open PR → Todo)
//   PR:     merged or closed → Done · draft → In progress · open → In review
// Fields:  Status, Hours (tasks only, from "**N h**" in the issue body). Sprint = the built-in Milestone field;
//          tier, type and area are labels; features and epics are the built-in Parent issue / Sub-issues progress.

const TITLE = 'MyPoGoCompanion';
const STATUSES = [
  { name: 'Todo', color: 'GRAY', description: 'Not started' },
  { name: 'In progress', color: 'YELLOW', description: 'Being built (draft pull request open)' },
  { name: 'In review', color: 'BLUE', description: 'Pull request ready for the owner to review and test' },
  { name: 'Done', color: 'GREEN', description: 'Merged or closed' },
];
const VIEWS = [
  { name: 'Current sprint', layout: 'board', filter: 'milestone:"Sprint 0" -label:"type: epic"' },
  { name: 'Backlog', layout: 'table', filter: 'is:issue -label:"type: epic"' },
  { name: 'Features', layout: 'table', filter: 'label:"type: feature"' },
];

module.exports = async ({ github, context, core }) => {
  const { owner, repo } = context.repo;
  const full = !['issues', 'pull_request'].includes(context.eventName);
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

  // GraphQL with retries on rate limits; mutations are paced during a full sync.
  async function gql(query, vars = {}, mutation = false) {
    for (let attempt = 0; ; attempt++) {
      try {
        const r = await github.graphql(query, vars);
        if (mutation && full) await sleep(1000);
        return r;
      } catch (e) {
        const limited = /rate limit|abuse|secondary/i.test(e.message) || [403, 429, 502].includes(e.status);
        if (limited && attempt < 10) {
          const wait = Number(e.response?.headers?.['retry-after'] || 60 * (attempt + 1));
          core.info(`rate limited, waiting ${wait}s`);
          await sleep(wait * 1000);
          continue;
        }
        throw e;
      }
    }
  }

  // --- project -------------------------------------------------------------------------------------------
  const { repository } = await gql(
    `query($owner: String!, $repo: String!) { repository(owner: $owner, name: $repo) { id owner { login } } }`,
    { owner, repo },
  );
  const { repositoryOwner: projectOwner } = await gql(
    `query($login: String!) { repositoryOwner(login: $login) { ... on ProjectV2Owner { id
       projectsV2(first: 50) { nodes { id number title url closed } } } } }`,
    { login: repository.owner.login },
  );
  let project = projectOwner.projectsV2.nodes.find((p) => p.title === TITLE && !p.closed);
  let created = false;
  if (!project) {
    if (!full) return core.info(`No "${TITLE}" project yet; run the workflow by hand to create it.`);
    ({ createProjectV2: { projectV2: project } } = await gql(
      `mutation($owner: ID!, $repo: ID!, $title: String!) {
         createProjectV2(input: { ownerId: $owner, repositoryId: $repo, title: $title }) { projectV2 { id number url } } }`,
      { owner: projectOwner.id, repo: repository.id, title: TITLE }, true,
    ));
    await gql(
      `mutation($id: ID!, $d: String!) { updateProjectV2(input: { projectId: $id, shortDescription: $d }) { projectV2 { id } } }`,
      { id: project.id, d: 'Sprints, features and tasks of MyPoGoCompanion. Kept in sync by the Project sync workflow.' }, true,
    );
    created = true;
    core.info(`Created project ${project.url}`);
  }

  // --- fields --------------------------------------------------------------------------------------------
  async function loadFields() {
    const { node } = await gql(
      `query($id: ID!) { node(id: $id) { ... on ProjectV2 { fields(first: 50) { nodes {
         ... on ProjectV2FieldCommon { id name dataType }
         ... on ProjectV2SingleSelectField { options { id name } } } } } } }`,
      { id: project.id },
    );
    return Object.fromEntries(node.fields.nodes.map((f) => [f.name, f]));
  }
  let fields = await loadFields();
  const status = fields.Status;
  if (!status.options.some((o) => o.name === 'In review')) {
    // Replacing the options clears existing values, so only do it while no item has a status yet.
    await gql(
      `mutation($f: ID!, $o: [ProjectV2SingleSelectFieldOptionInput!]!) {
         updateProjectV2Field(input: { fieldId: $f, singleSelectOptions: $o }) { projectV2Field { ... on ProjectV2SingleSelectField { id } } } }`,
      { f: status.id, o: STATUSES }, true,
    );
  }
  if (!fields.Hours) {
    await gql(
      `mutation($p: ID!) { createProjectV2Field(input: { projectId: $p, dataType: NUMBER, name: "Hours" }) {
         projectV2Field { ... on ProjectV2Field { id } } } }`,
      { p: project.id }, true,
    );
  }
  fields = await loadFields();
  const option = (name) => fields.Status.options.find((o) => o.name.toLowerCase() === name.toLowerCase())?.id;

  // --- what is already on the board ----------------------------------------------------------------------
  const items = {}; // content node id → { id, status, hours }
  for (let after = null; ; ) {
    const { node } = await gql(
      `query($id: ID!, $after: String) { node(id: $id) { ... on ProjectV2 { items(first: 100, after: $after) {
         pageInfo { hasNextPage endCursor }
         nodes { id content { ... on Issue { id } ... on PullRequest { id } }
           fieldValues(first: 20) { nodes {
             ... on ProjectV2ItemFieldSingleSelectValue { optionId field { ... on ProjectV2FieldCommon { name } } }
             ... on ProjectV2ItemFieldNumberValue { number field { ... on ProjectV2FieldCommon { name } } } } } } } } } }`,
      { id: project.id, after },
    );
    for (const it of node.items.nodes) {
      if (!it.content?.id) continue;
      const v = Object.fromEntries(it.fieldValues.nodes.filter((f) => f.field).map((f) => [f.field.name, f.optionId ?? f.number]));
      items[it.content.id] = { id: it.id, status: v.Status ?? null, hours: v.Hours ?? null };
    }
    if (!node.items.pageInfo.hasNextPage) break;
    after = node.items.pageInfo.endCursor;
  }

  // --- pull requests and the issues they close -----------------------------------------------------------
  const prQuery = `id number state isDraft closingIssuesReferences(first: 50) { nodes { id number } }`;
  const openPrs = [];
  for (let after = null; ; ) {
    const { repository: r } = await gql(
      `query($owner: String!, $repo: String!, $after: String) { repository(owner: $owner, name: $repo) {
         pullRequests(first: 50, after: $after, states: OPEN) { pageInfo { hasNextPage endCursor } nodes { ${prQuery} } } } }`,
      { owner, repo, after },
    );
    openPrs.push(...r.pullRequests.nodes);
    if (!r.pullRequests.pageInfo.hasNextPage) break;
    after = r.pullRequests.pageInfo.endCursor;
  }
  const prFor = {}; // issue node id → 'In progress' | 'In review'
  for (const pr of openPrs) {
    for (const i of pr.closingIssuesReferences.nodes) {
      if (prFor[i.id] !== 'In review') prFor[i.id] = pr.isDraft ? 'In progress' : 'In review';
    }
  }

  // --- apply ---------------------------------------------------------------------------------------------
  let added = 0, changed = 0;
  async function setValue(itemId, field, value) {
    await gql(
      `mutation($p: ID!, $i: ID!, $f: ID!, $v: ProjectV2FieldValue!) {
         updateProjectV2ItemFieldValue(input: { projectId: $p, itemId: $i, fieldId: $f, value: $v }) { projectV2Item { id } } }`,
      { p: project.id, i: itemId, f: fields[field].id, v: value }, true,
    );
    changed++;
  }
  async function sync(content, wanted, hours) {
    let item = items[content.id];
    if (!item) {
      const r = await gql(
        `mutation($p: ID!, $c: ID!) { addProjectV2ItemById(input: { projectId: $p, contentId: $c }) { item { id } } }`,
        { p: project.id, c: content.id }, true,
      );
      item = items[content.id] = { id: r.addProjectV2ItemById.item.id, status: null, hours: null };
      added++;
    }
    const target = option(wanted(item.status));
    if (target && target !== item.status) await setValue(item.id, 'Status', { singleSelectOptionId: target });
    if (hours != null && hours !== item.hours) await setValue(item.id, 'Hours', { number: hours });
  }
  const nameOf = (optionId) => fields.Status.options.find((o) => o.id === optionId)?.name ?? null;

  function issueStatus(issue) {
    return (current) => {
      if (issue.state === 'CLOSED') return 'Done';
      if (prFor[issue.id]) return prFor[issue.id];
      const name = nameOf(current);
      return !name || name === 'Done' || name === 'In review' ? 'Todo' : name;
    };
  }
  const prStatus = (pr) => () => (pr.state !== 'OPEN' ? 'Done' : pr.isDraft ? 'In progress' : 'In review');
  const hoursOf = (issue) =>
    issue.labels.nodes.some((l) => l.name === 'type: task')
      ? Number((issue.body.match(/\*\*(\d+(?:\.\d+)?) h\*\*/) || [])[1]) || null
      : null;

  const issueFields = `id number state body labels(first: 20) { nodes { name } }`;
  if (full) {
    for (let after = null; ; ) {
      const { repository: r } = await gql(
        `query($owner: String!, $repo: String!, $after: String) { repository(owner: $owner, name: $repo) {
           issues(first: 100, after: $after) { pageInfo { hasNextPage endCursor } nodes { ${issueFields} } } } }`,
        { owner, repo, after },
      );
      for (const issue of r.issues.nodes) await sync(issue, issueStatus(issue), hoursOf(issue));
      core.info(`… ${Object.keys(items).length} items, ${added} added, ${changed} values set`);
      if (!r.issues.pageInfo.hasNextPage) break;
      after = r.issues.pageInfo.endCursor;
    }
    for (const pr of openPrs) await sync(pr, prStatus(pr));
  } else if (context.eventName === 'issues') {
    const { node: issue } = await gql(`query($id: ID!) { node(id: $id) { ... on Issue { ${issueFields} } } }`, {
      id: context.payload.issue.node_id,
    });
    await sync(issue, issueStatus(issue), hoursOf(issue));
  } else {
    const { node: pr } = await gql(`query($id: ID!) { node(id: $id) { ... on PullRequest { ${prQuery} } } }`, {
      id: context.payload.pull_request.node_id,
    });
    await sync(pr, prStatus(pr));
    for (const ref of pr.closingIssuesReferences.nodes) {
      const { node: issue } = await gql(`query($id: ID!) { node(id: $id) { ... on Issue { ${issueFields} } } }`, { id: ref.id });
      await sync(issue, issueStatus(issue), hoursOf(issue));
    }
  }

  // --- views (first run only; the API for views may not be available to every account) ------------------
  if (created) {
    const login = repository.owner.login;
    for (const v of VIEWS) {
      try {
        await github.request('POST /users/{login}/projectsV2/{number}/views', { login, number: project.number, ...v });
      } catch (e) {
        core.warning(`Could not create the "${v.name}" view (${e.status ?? e.message}); create it by hand.`);
      }
    }
  }

  core.info(`Project ${project.url}: ${added} items added, ${changed} values set.`);
  await core.summary
    .addHeading('Project sync')
    .addRaw(`<a href="${project.url}">${TITLE} board</a>: ${added} items added, ${changed} values set.`)
    .write();
};
