import { expect, test, type Page, type TestInfo } from "@playwright/test";

// B1 Pokédex list (E03-F01). QA runs on a copy of prod's real data, so these check behaviour (lines appear, the
// search narrows them, versions switch, filters and sort land in the address), not fixed counts.

const isDesktop = (testInfo: TestInfo) =>
  testInfo.project.name.startsWith("desktop");

const lines = (page: Page) => page.locator("[data-dex]");
const line = (page: Page, dex: number) => page.locator(`[data-dex="${dex}"]`);
const searchBox = (page: Page) =>
  page.getByRole("searchbox", { name: "Search the Pokédex" });

async function openPokedex(page: Page, query = "") {
  await page.goto(`/pokedex${query}`);
  await expect(page.locator("[data-dex], .pokedex-empty").first()).toBeVisible({
    timeout: 30_000,
  });
}

async function dexes(page: Page): Promise<number[]> {
  return lines(page).evaluateAll((items) =>
    items.map((item) => Number((item as HTMLElement).dataset.dex)),
  );
}

test("every released species has a line, in Pokédex order, with its numbers", async ({
  page,
}) => {
  await openPokedex(page);

  const shown = await dexes(page);
  expect(shown.length).toBeGreaterThan(900);
  expect(shown).toEqual([...shown].sort((a, b) => a - b));
  const bulbasaur = line(page, 1);
  await expect(
    bulbasaur.getByRole("link", { name: "Bulbasaur" }),
  ).toHaveAttribute("href", "/pokedex/bulbasaur");
  await expect(bulbasaur).toContainText("Grass");
  await expect(bulbasaur).toContainText("Poison");
  await expect(page.getByText(/^[\d,]+ Pokémon$/)).toBeVisible();
});

test("the search narrows the list as you type and shows how it read the words", async ({
  page,
}) => {
  await openPokedex(page);

  await searchBox(page).fill("fire,water !legendary");
  await expect(page.locator(".pokedex-term").first()).toContainText(
    "Type: Fire",
  );
  await expect(page.getByText("not Legendary")).toBeVisible();
  await expect(page).toHaveURL(/q=fire%2Cwater/);
  const shown = await dexes(page);
  expect(shown).toContain(6); // Charizard
  expect(shown).toContain(9); // Blastoise
  expect(shown).not.toContain(146); // Moltres is Legendary
  expect(shown).not.toContain(1); // Bulbasaur is neither

  await searchBox(page).fill("xyzzy");
  await expect(page.getByText("“xyzzy”: not recognised")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "No Pokémon match" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Clear search and filters" }).click();
  await expect(lines(page).first()).toBeVisible();
  await expect(page).toHaveURL(/\/pokedex$/);
});

test("every version counts: the line shows the version that matched", async ({
  page,
}) => {
  await openPokedex(page, "?q=fire%20atk300-");

  const charizard = line(page, 6);
  await expect(charizard).toContainText("Showing Mega Charizard Y");
  await expect(charizard).toContainText("319");
  await expect(
    charizard.getByRole("link", { name: "Charizard" }),
  ).toHaveAttribute("href", "/pokedex/charizard?version=charizard_mega_y");
});

test("a marker switches the line to its version, and back", async ({
  page,
}, testInfo) => {
  await openPokedex(page);
  const charizard = line(page, 6);
  const megaY = charizard.getByRole("button", {
    name: "Show Mega Charizard Y on this line",
  });

  if (isDesktop(testInfo)) {
    await megaY.hover();
    await expect(charizard).toContainText("Showing Mega Charizard Y");
    await page.mouse.move(0, 0);
    await expect(charizard).not.toContainText("Showing");
  } else {
    await megaY.tap();
    await expect(charizard).toContainText("Showing Mega Charizard Y");
    await megaY.tap();
    await expect(charizard).not.toContainText("Showing");
  }

  await charizard.getByRole("button", { name: /^Shadow: same stats/ }).click();
  await expect(charizard).toContainText("deals 20% more damage");
});

test("filters apply, stay in the address and survive a reload", async ({
  page,
}, testInfo) => {
  await openPokedex(page);

  if (isDesktop(testInfo)) {
    // Type ▾ changes only the type, right under its chip; More filters holds the rest.
    await page.getByRole("button", { name: /^Type: Any/ }).click();
    await page
      .getByRole("dialog", { name: "Type" })
      .getByRole("button", { name: "Ground", exact: true })
      .click();
    await page.keyboard.press("Escape");
    await page.getByRole("button", { name: "More filters" }).click();
    const more = page.getByRole("dialog", { name: "More filters" });
    await expect(more.getByRole("heading", { name: "Type" })).toHaveCount(0);
    await more.getByRole("button", { name: "Legendary" }).click();
    await more.getByRole("button", { name: /^Show \d+ Pokémon$/ }).click();
    await expect(more).toBeHidden();
  } else {
    await page.getByRole("button", { name: "Filters" }).click();
    const sheet = page.getByRole("dialog", { name: "Filters" });
    await sheet.getByRole("button", { name: "Ground", exact: true }).click();
    await sheet.getByRole("button", { name: "Legendary" }).click();
    await sheet.getByRole("button", { name: /^Show \d+ Pokémon$/ }).click();
    await expect(sheet).toBeHidden();
  }

  await expect(page).toHaveURL(/type=ground/);
  await expect(page).toHaveURL(/cat=legendary/);
  await expect(line(page, 383)).toBeVisible(); // Groudon
  await expect(line(page, 1)).toHaveCount(0);

  await page.reload();
  await expect(line(page, 383)).toBeVisible();
  await expect(line(page, 1)).toHaveCount(0);
});

test("Has Mega and Has Shadow together find Pokémon that have both", async ({
  page,
}) => {
  await openPokedex(page, "?has=mega,shadow");

  // Charizard has a Mega and a Shadow (on different versions of it).
  await expect(line(page, 6)).toBeVisible();
  // Every line shown has both markers; Pokémon with only one of them are left out.
  const shown = lines(page);
  const count = await shown.count();
  expect(count).toBeGreaterThan(5);
  for (let index = 0; index < Math.min(count, 15); index++) {
    const item = shown.nth(index);
    await expect(
      item.locator('[data-kind="mega"], [data-kind="primal"]').first(),
    ).toBeVisible();
    await expect(item.locator('[data-kind="shadow"]')).toBeVisible();
  }
  await expect(line(page, 1)).toHaveCount(0); // Bulbasaur: a Shadow, no Mega
});

test("sorting by Attack puts the strongest first", async ({
  page,
}, testInfo) => {
  await openPokedex(page);

  if (isDesktop(testInfo)) {
    await page
      .getByRole("columnheader", { name: /Attack/ })
      .getByRole("button")
      .click();
  } else {
    await page.getByRole("button", { name: /^Sort: Number/ }).click();
    await page
      .getByRole("dialog", { name: "Sort by" })
      .getByRole("button", { name: "Attack" })
      .click();
  }

  await expect(page).toHaveURL(/sort=atk/);
  const attacks = await lines(page)
    .locator(
      isDesktop(testInfo)
        ? "td.pokedex-stat >> nth=0"
        : ".pokedex-abc span >> nth=0",
    )
    .allTextContents();
  const values = attacks
    .slice(0, 20)
    .map((text) => Number(text.replace(/\D/g, "")));
  expect(values).toEqual([...values].sort((a, b) => b - a));
});

test("“How search works” explains the search and tries an example", async ({
  page,
}) => {
  await openPokedex(page);

  await page.getByRole("button", { name: "How search works" }).click();
  const help = page.getByRole("dialog", { name: "How search works" });
  await expect(help.getByText("A comma means either/or")).toBeVisible();
  await help.getByRole("button", { name: "fire&flying" }).click();

  await expect(help).toBeHidden();
  await expect(searchBox(page)).toHaveValue("fire&flying");
  await expect(line(page, 6)).toBeVisible(); // Charizard is Fire/Flying
  await expect(line(page, 4)).toHaveCount(0); // Charmander is only Fire
});

test("the Pokédex answer is cached and compressed", async ({ page }) => {
  await openPokedex(page);
  const api = await page.evaluate(() =>
    performance.getEntriesByType("resource").map((entry) => entry.name),
  );
  const url = api.find((name) => name.endsWith("/pokedex"));
  expect(url).toBeTruthy();

  const response = await page.request.get(url!, {
    headers: { "Accept-Encoding": "br, gzip" },
  });
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toBe("public, max-age=3600");
  const etag = response.headers()["etag"];
  expect(etag).toMatch(/^"[0-9a-f]{32}"$/);
  const again = await page.request.get(url!, {
    headers: { "If-None-Match": etag! },
  });
  expect(again.status()).toBe(304);
});

test("on a phone the search box and filter chips stay on top while the list scrolls", async ({
  page,
}, testInfo) => {
  test.skip(isDesktop(testInfo), "phone only");
  await openPokedex(page);

  await page.mouse.wheel(0, 4000);
  await expect(line(page, 1)).not.toBeInViewport();
  await expect(searchBox(page)).toBeInViewport();
  await expect(page.getByRole("button", { name: /^Filters/ })).toBeInViewport();
  await page.getByRole("button", { name: /^Filters/ }).click();
  await expect(page.getByRole("dialog", { name: "Filters" })).toBeVisible();
});
