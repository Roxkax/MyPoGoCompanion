// Renders design screens to PNG.  Usage: node shot.js A1 [B2 ...] [--dark]
// For each id: screens/<id>-phone.html -> ../screens/<id>-phone.png (+ -full.png when it scrolls)
//              screens/<id>-desktop.html -> ../screens/<id>-desktop.png
const { chromium } = require("playwright");
const path = require("path");
const fs = require("fs");
(async () => {
  const ids = process.argv.slice(2).filter(a => !a.startsWith("--"));
  const dark = process.argv.includes("--dark");
  const out = path.join(__dirname, "..", "screens");
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  for (const id of ids) {
    for (const kind of ["phone", "desktop"]) {
      const file = path.join(__dirname, "screens", `${id}-${kind}.html`);
      if (!fs.existsSync(file)) continue;
      const page = await browser.newPage({ viewport: { width: kind === "phone" ? 480 : 1440, height: 900 }, deviceScaleFactor: 2 });
      const errors = [];
      page.on("pageerror", e => errors.push(e.message));
      const suffix = dark ? "-dark" : "";
      const load = async (q) => {
        await page.goto("file://" + file + q);
        await page.evaluate(() => document.fonts.ready);
        await page.waitForTimeout(150);
      };
      await load(dark ? "?theme=dark" : "");
      if (kind === "phone") {
        await page.locator(".stage").screenshot({ path: path.join(out, `${id}-phone${suffix}.png`) });
        const scrolls = await page.evaluate(() => { const s = document.querySelector(".scroll"); return s && s.scrollHeight > s.clientHeight + 4; });
        if (scrolls) {
          await load(dark ? "?full=1&theme=dark" : "?full=1");
          await page.locator(".stage").screenshot({ path: path.join(out, `${id}-phone-full${suffix}.png`) });
        }
      } else {
        await page.screenshot({ path: path.join(out, `${id}-desktop${suffix}.png`), fullPage: true });
      }
      console.log(id, kind, errors.length ? errors : "ok");
      await page.close();
    }
  }
  await browser.close();
})();
