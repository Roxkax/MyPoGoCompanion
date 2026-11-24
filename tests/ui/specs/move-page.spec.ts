import { expect, test } from "@playwright/test";

test("Move details open from a filtered list and link back to the list and learner", async ({
  page,
}) => {
  await page.goto("/pokedex/moves?q=Earth+Power");
  await page.getByRole("link", { name: /Earth Power/i }).click();
  await expect(
    page.getByRole("heading", { name: "Earth Power", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Raids & gyms" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "PvP (trainer battles)" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Who learns it" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: /Garchomp/ }).first(),
  ).toHaveAttribute("href", "/pokedex/445?version=garchomp");
  await page.locator(".back-link").getByText("Moves", { exact: true }).click();
  await expect(page).toHaveURL(/\/pokedex\/moves\?q=Earth\+Power/);
});

test("Unknown move shows a way back", async ({ page }) => {
  await page.goto("/pokedex/moves/does-not-exist");
  await expect(
    page.getByRole("heading", { name: "Move not found" }),
  ).toBeVisible();
  await expect(
    page.locator(".back-link").getByText("Moves", { exact: true }),
  ).toBeVisible();
});

test("Phone expands learners while desktop shows the full group", async ({
  page,
  isMobile,
}) => {
  const learners = Array.from({ length: 10 }, (_, i) => ({
    dex: i + 1,
    formId: `learner-${i}`,
    nameEn: `Learner ${i + 1}`,
    namePt: null,
    types: ["ground"],
    attack: 300 - i,
    sprite: null,
    availability: "Normal",
    megaToo: i === 0,
    primalToo: false,
  }));
  await page.route("**/moves/fixture", (route) =>
    route.request().isNavigationRequest()
      ? route.continue()
      : route.fulfill({
          json: {
            move: {
              id: "FIXTURE",
              nameEn: "Fixture Move",
              namePt: null,
              type: "ground",
              kind: "Charged",
              raidPower: 100,
              raidDurationMs: 3500,
              raidEnergy: -50,
              pvpPower: 90,
              pvpEnergy: -50,
              pvpTurns: null,
              pvpBuffChance: 0.1,
              pvpSelfAttack: 0,
              pvpSelfDefense: 0,
              pvpOpponentAttack: 0,
              pvpOpponentDefense: -1,
            },
            learners,
            events: [],
          },
        }),
  );
  await page.goto("/pokedex/moves/fixture");
  const group = page.getByRole("region", { name: "Can learn it" });
  await expect(group.getByRole("link")).toHaveCount(isMobile ? 8 : 10);
  await expect(group.getByText("Mega too")).toBeVisible();
  if (isMobile) {
    await group.getByRole("button", { name: "Show all 10" }).click();
    await expect(group.getByRole("link")).toHaveCount(10);
  }
});
