import { expect, test, type Page, type TestInfo } from "@playwright/test";

// B2 Pokémon page (E03-F02). QA runs on a copy of prod's real data and local runs on the development sample, so
// these check behaviour (the page opens from the list, versions and tabs switch through the address, the IV checker
// answers) and numbers that don't change (Garchomp's base stats), not rankings.

const isDesktop = (testInfo: TestInfo) =>
  testInfo.project.name.startsWith("desktop");

const section = (page: Page, name: string) =>
  page.getByRole("region", { name, exact: true });

async function openGarchomp(page: Page, address = "/pokedex/garchomp") {
  await page.goto(address);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    "Garchomp",
    { timeout: 30_000 },
  );
}

test("opens from the Pokédex list with the species' numbers", async ({
  page,
}) => {
  await page.goto("/pokedex?q=garchomp");
  await page
    .locator('[data-dex="445"]')
    .getByRole("link", { name: "Garchomp" })
    .click();
  await expect(page).toHaveURL(/\/pokedex\/garchomp$/);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Garchomp", {
    timeout: 30_000,
  });
  await expect(page.getByText("#445")).toBeVisible();

  const stats = section(page, "Stats");
  await expect(stats).toContainText("261");
  await expect(stats).toContainText("193");
  await expect(stats).toContainText("239");
  await expect(stats).toContainText("Max CP · L50");
  await expect(
    section(page, "Rankings").getByRole("link", { name: /Great League/ }),
  ).toHaveAttribute("href", "/battle/tiers/great-league");
  await expect(section(page, "Type matchups")).toContainText("Ice ×2.56");
});

test("switches versions through the address", async ({ page }) => {
  await openGarchomp(page);
  const versions = page.getByRole("group", { name: "Versions" });

  await versions.getByRole("button", { name: "Mega", exact: true }).click();
  await expect(page).toHaveURL(/version=garchomp_mega/);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    "Mega Garchomp",
  );

  await versions.getByRole("button", { name: "Shadow", exact: true }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    "Shadow Garchomp",
  );

  await page.reload();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    "Shadow Garchomp",
    { timeout: 30_000 },
  );
});

test("the IV checker ranks any IVs in every league", async ({
  page,
}, testInfo) => {
  await openGarchomp(
    page,
    isDesktop(testInfo) ? "/pokedex/garchomp" : "/pokedex/garchomp#ivs",
  );
  const check = section(page, "Check any IVs");
  for (const [label, value] of [
    ["Attack", "15"],
    ["Defense", "15"],
    ["HP", "15"],
  ]) {
    await check.getByRole("spinbutton", { name: label }).fill(value);
  }
  await expect(check).toContainText("#1 · 100%");
  await expect(check).toContainText("CP 4,479");
  await expect(section(page, "100% CP cheat sheet")).toContainText("2,264");
});

test("phone: the tabs switch the content and Back returns to the last tab", async ({
  page,
}, testInfo) => {
  test.skip(isDesktop(testInfo), "the desktop shows everything at once");
  await openGarchomp(page);
  const tabs = page.getByRole("navigation", { name: "Pokémon page sections" });

  await tabs.getByRole("button", { name: "Moves" }).click();
  await expect(page).toHaveURL(/#moves$/);
  await expect(section(page, "Moves")).toContainText("Dragon Tail");
  await expect(section(page, "Stats")).toHaveCount(0);

  await tabs.getByRole("button", { name: "Get it" }).click();
  await expect(section(page, "How to get it")).toContainText("Right now");
  await expect(section(page, "Evolution")).toContainText("Gible");

  await page.goBack();
  await expect(page).toHaveURL(/#moves$/);
  await expect(section(page, "Moves")).toBeVisible();
});

test("desktop: everything on one page in two columns", async ({
  page,
}, testInfo) => {
  test.skip(!isDesktop(testInfo), "the phone uses tabs");
  await openGarchomp(page);
  for (const name of [
    "Stats",
    "Rankings",
    "Moves",
    "Best IVs",
    "Check any IVs",
    "Evolution",
    "How to get it",
    "Versions",
  ]) {
    await expect(section(page, name)).toBeVisible();
  }
  const [left, right] = await Promise.all([
    section(page, "Stats").boundingBox(),
    section(page, "Best IVs").boundingBox(),
  ]);
  expect(right!.x).toBeGreaterThan(left!.x + left!.width);
});

test("a name that isn't in the Pokédex says so", async ({ page }) => {
  await page.goto("/pokedex/missingno");
  await expect(
    page.getByText("There is no Pokémon called “missingno” in the Pokédex."),
  ).toBeVisible({ timeout: 30_000 });
});
