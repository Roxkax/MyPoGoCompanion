# How we planned this project (and how to do it again)

This is the process we used to plan **Gardening Companion**, from a first idea to a signed-off design and a backlog ready for sprints. It is written so you can repeat it on any new project with Claude Code. Examples come from this project.

---

## The process at a glance

| # | Stage | What you get |
|---|---|---|
| 1 | Kick-off questions | The basic shape: platform, purpose, your experience |
| 2 | Working agreements | `CLAUDE.md` with the rules of the collaboration and a decision log |
| 3 | Describe the idea as a user | A plain description of what the app should do, from the user's side |
| 4 | Clarify, one question at a time | Every open point decided and logged |
| 5 | Write the plan | `PLAN.md`: features, architecture, costs, licensing |
| 6 | Feature-by-feature review | Each feature accepted, changed or cut by you |
| 7 | Technical decisions | Stack, AI models, data, sync, all confirmed by you |
| 8 | Prioritise | A spreadsheet with MVP / Good to have / Future per feature |
| 9 | Build the backlog | Epics → Features → Tasks with estimates, grouped into 100-hour sprints |
| 10 | Design before code | An app map, then a realistic image of every screen, approved one by one |
| 11 | Rebuild the backlog from the design | The backlog matches what was actually designed |
| 12 | Start sprints | Only when you say so |

Commit and push after every confirmed change, so the repository is always the source of truth.

---

## 1. Kick-off questions

Start a new session with a short goal and ask Claude to guide you:

> I want to start a new project and need help to plan its execution. Guide me to create a good plan.

Claude asks a few basic questions: platforms (web, mobile), core features, purpose (personal, commercial) and your experience. Answer briefly. You'll refine it all later.

## 2. Set the working agreements early

Create a `CLAUDE.md` in the repository. Claude reads it at the start of every session, so it keeps the context. Put two things in it.

**Working agreements**, the rules of the collaboration. The ones that worked here:

- **Ask one question at a time.** Batches of questions are hard to answer well. This was the most important rule.
- **You describe features from the user's point of view; Claude works out the backend and confirms it with you** before treating it as decided.
- **Be honest about feasibility.** For example, "AI can't measure soil pH from a photo", with realistic alternatives.
- **Scope rule:** while it's for personal use, every feature is in scope. "Premium" is only noted for a possible commercial phase.
- **Cost rule:** prefer pay-per-use and scale-to-zero cloud services.
- **Commercial readiness:** design for multiple users from day one, and flag licensing or terms-of-service issues.
- **Language:** replies in English. You may write in Portuguese.
- **Git:** commit and push planning documents after each confirmed change.

**A decision log**: a table with a date and one line per decision. Every time you confirm something, it goes in the log. This is what stops decisions being re-discussed or forgotten.

Prompt you can reuse:

> Create a CLAUDE.md for the project to keep context on the decisions I make. Record every decision I confirm in a decision log.

## 3. Describe the idea as a user

Write the idea in your own words, in any language, as a list of things you want to do with the app. Don't think about the backend. Example from this project: "map the plot from the satellite, analyse situations with the camera, know when to plant, check if a crop is ready to harvest, track my tools, get improvement suggestions, detailed guides with videos…"

Then tell Claude how you want to work:

> I'll describe what I want from the mobile app's perspective. You deduce what the backend needs, always confirming with me first.

## 4. Clarify, one question at a time

Claude asks the open questions one by one. In this project:

- location (adapt to each user's location, Alentejo first)
- languages (Portuguese and English)
- offline needs
- whether the AI may act on its own (no: it proposes, and you confirm)
- how to map the plot (draw on the map)
- guide formats
- AI provider
- database
- business model
- backend language
- which platform first.

Each answer goes into the decision log.

## 5. Write the plan

Claude writes `PLAN.md`:

- vision
- one section per feature, with what the user sees and what the backend needs
- architecture
- monthly cost estimate
- release plan
- licensing and terms-of-service risks for commercial use.

## 6. Review every feature, one by one

Go through `PLAN.md` feature by feature. For each, Claude presents what's planned plus suggestions, and you answer with "add all of those", "change X" or "remove Y". Real examples of how this shaped the product:

- **Soil analysis removed completely.** It was replaced by a site assessment that covers what you can actually see.
- **"Materials" became "Tools".** Only tools are tracked, with no stock or costs, and an opt-in "always own" setting.
- **Irrigation lost water-needs and water-quality calculations**, and kept a simple bucket flow test.
- **A new feature, "choose your crops", was added mid-review.**
- **The roadmap became dynamic.** It works next actions out from the garden's state and history instead of a fixed list.

The lesson: the review is where the product really gets designed. Don't skip it.

## 7. Confirm the technical decisions

Claude proposes the options with costs and trade-offs, and you pick one. Here:

- native apps (SwiftUI, then Kotlin)
- .NET backend
- PostgreSQL with PostGIS and pgvector
- Azure scale-to-zero services
- Claude via Microsoft Foundry behind a provider-agnostic layer
- OpenAI embeddings
- gpt-image for illustrations
- "the user resolves sync conflicts".

All of them were logged.

## 8. Prioritise in a spreadsheet

Ask for a spreadsheet you can edit on your phone:

> Create a file I can edit in Numbers on my iPhone, with each feature and sub-feature, where I can pick MVP, Good to have or Future improvement for each.

Fill in what you care about, send it back, and let Claude suggest the blanks. Here it gave 72 MVP, 68 Good to have and 45 Future items. Some notable calls: offline sync and Android went to "Good to have".

## 9. Build the backlog

Ask for a backlog in the shape you know. Here that was Azure DevOps style, on GitHub:

- **Epic → Feature (like a PBI) → Task**, using GitHub sub-issues.
- **Labels** for release tier and area (backend, iOS, Android, content, infra, design, testing).
- **Every task has an hour estimate that includes your testing time.**
- **Sprints are milestones of 100 points, where 1 point = 1 hour.**

How it was built, so it's repeatable:

- **The source of truth is JSON fragments in the repository** (`planning/backlog/*.json`). A small script (`build.py`) puts them together and fills the sprints in order.
- **A GitHub Actions workflow imports them** as issues, sub-issues, labels and milestones. Each issue carries a hidden `backlog-key` marker, so re-running only creates what's missing.
- **A second workflow resets the backlog when needed.** Deleting issues requires an admin token: a *classic* personal access token with `repo` scope worked; a fine-grained one didn't.

Result here: 21 epics, 214 features and 890 tasks. The MVP is about 1,558 h (16 sprints), and Good to have is about 976 h (10 sprints).

## 10. Design before any code

You don't need design experience. You need critical thinking.

> I have zero knowledge of app design. Looking at the planned features, create the workflow of the app with all the screens needed, then iterate them with me so I can give feedback on each one.

### 10.1 App map first
Agree the structure before drawing screens:

- the tabs (here: Today, Garden, Snap & Ask camera button, Plan, Journal)
- where secondary areas live (Learn and Me, from the profile picture)
- a numbered list of every screen (A1–G6).

### 10.2 Realistic screen images, not wireframes
> I want an actual image of what it will look like, so I can approve it or ask for changes.

How it's done:
- **Each screen is an HTML/CSS file inside an iPhone frame**, rendered to PNG with Playwright (`design/source/shot.js`).
- **Sources live in `design/source/`, images in `design/screens/`.**
- **Scrolling screens also get a "-full" version** showing the whole page.

### 10.3 A conventions file
`design/source/CONVENTIONS.md` fixes the visual rules:

- font, colours ("orange only for warnings")
- tab screens vs. detail screens: detail screens have a back button labelled with the screen they return to, and no large title
- **one shared sample dataset for every screen**: the same date, plot, zones, beds, crops, weather and tools.

### 10.4 Review one screen at a time
We tried rendering all screens at once to review together. **It didn't work: it was confusing.** We went back to one screen at a time:

1. Claude sends one image and explains each part in plain words.
2. Claude lists what it fixed before showing it, mostly inconsistencies with earlier screens.
3. You answer **"Approved, move to the next screen"**, ask a question, or ask for changes.
4. The approval, with its key details, goes into the decision log, and everything is pushed.

Questions that improved the design the most, so ask them on every screen:
- **"Where is this screen accessed from?"** This found screens with no way in, such as the bucket test, and led to adding entry points.
- **"Why is it only for X?"** The weekend planner became a week planner with a "Day off" button and calendar sync.
- **"How does the user actually select Y?"** This led to a picture catalogue of regional trees, plus identifying trees from a photo.
- **"Is it possible to do Z with the camera?"** This produced bed scanning from photos now, and an AR walk later, with an honest note on what a photo can and can't measure.

### 10.5 Keep the story consistent
Claude checks every screen against the shared sample data and the approved screens. Typical catches:

- a crop sown in the wrong bed
- drip watering shown before the drip was installed
- a harvest in an empty bed
- numbers that didn't add up (well flow vs. drip lines).

When a later screen exposes a problem in an approved one, the old screen is fixed and you're told.

## 11. Rebuild the backlog after the design

Design changes features, so the first backlog was **deleted** once it was clear the design would change it. It is rebuilt from the updated JSON fragments after sign-off. Keep a running list of backlog changes in `CLAUDE.md` under "Open decisions" while you review.

## 12. Start sprints only when you say so

`CLAUDE.md` says: *don't start application code until the owner says to start a sprint.* This keeps planning and building separate.

---

## Lessons learned

1. **One question at a time.** Short questions, quick answers, better decisions.
2. **Log every decision immediately**, with the date, in `CLAUDE.md`.
3. **Describe features as a user** and let Claude derive the technical side, but confirm it.
4. **Review features one by one before estimating anything.** Most of the real product changes happened there.
5. **Prioritise in a simple spreadsheet** with three tiers.
6. **Estimate in hours including your own testing time**, and use fixed-size sprints.
7. **Keep the backlog as code** (JSON plus scripts plus workflows), so it can be rebuilt at any time.
8. **Design with realistic images, reviewed one screen at a time.**
9. **Use one sample dataset across all screens.** Inconsistencies reveal missing features and wrong assumptions.
10. **Ask "where is this accessed from?" for every screen.**
11. **Be ready to throw away the backlog** when the design changes the product.
12. **Commit and push after every confirmed change.**

---

## Starter prompts for a new project

1. *"I want to start a new project. Guide me to create a good execution plan. Ask me one question at a time."*
2. *"Create a CLAUDE.md with our working agreements and a decision log. Record every decision I confirm."*
3. *"Here is my idea from the user's point of view: … Deduce what the backend needs and confirm with me before deciding."*
4. *"Write PLAN.md with features, architecture, costs and licensing risks."*
5. *"Let's review the plan feature by feature. Show me one feature at a time with your suggestions."*
6. *"Create a spreadsheet I can edit on my phone to set each feature as MVP, Good to have or Future improvement."*
7. *"Create the backlog as Epic → Feature → Task with hour estimates including my testing time, in 100-hour sprints, and a GitHub Actions workflow to import it."*
8. *"I have no design experience. Create the app map with all screens, then show me a realistic phone image of each screen, one at a time, for approval."*
9. For each screen: *"Approved, move to the next screen"*, or *"Where is this accessed from?"*, or your change.
10. *"Design is signed off. Update the backlog fragments and rebuild the backlog."*
