import { chromium } from "playwright";
import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const baseUrl = process.env.BASE_URL;
if (!baseUrl) throw new Error("BASE_URL is required (set it to the deployed staging URL).");

const outputDir = path.resolve("artifacts/ui-screenshots");
const zipPath = path.resolve("artifacts/UI_Screenshots.zip");
const reportPath = path.resolve("artifacts/ui-capture-report.json");
const maxPages = Math.max(1, Number.parseInt(process.env.UI_MAX_PAGES || "250", 10));
const ignoredPath = /\/(logout|signout|sign-out|delete|remove)(\/|$)/i;
const ignoredExtensions = /\.(?:pdf|zip|png|jpe?g|gif|svg|webp|ico|css|js|map|woff2?|ttf|eot|xlsx?|docx?|pptx?)(?:$|\?)/i;

const safeName = (url) => {
  const parsed = new URL(url);
  const raw = (parsed.pathname === "/" ? "home" : parsed.pathname)
    .replace(/^\/+|\/+$/g, "")
    .replace(/[^a-zA-Z0-9._-]+/g, "_")
    .slice(0, 100) || "page";
  const query = parsed.search ? "_" + Buffer.from(parsed.search).toString("base64url").slice(0, 18) : "";
  return raw + query + ".png";
};

const normalize = (href, origin) => {
  try {
    const url = new URL(href, origin);
    if (!["http:", "https:"].includes(url.protocol)) return null;
    if (url.origin !== new URL(origin).origin) return null;
    if (ignoredPath.test(url.pathname) || ignoredExtensions.test(url.pathname)) return null;
    url.hash = "";
    // Avoid unbounded query variants while retaining meaningful page routes.
    if (url.searchParams.has("returnUrl") || url.searchParams.has("ReturnUrl")) return null;
    return url.href;
  } catch {
    return null;
  }
};

await fs.rm(outputDir, { recursive: true, force: true });
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(path.dirname(zipPath), { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
  deviceScaleFactor: 1,
  ignoreHTTPSErrors: process.env.UI_IGNORE_HTTPS_ERRORS === "true",
});
const page = await context.newPage();
page.setDefaultTimeout(15000);
const failures = [];
const captured = [];
const queue = [new URL(baseUrl).href];
const visited = new Set();
const origin = new URL(baseUrl).origin;

try {
  // Optional login. Selectors are configurable because deployments can customize the login page.
  if (process.env.UI_LOGIN_USERNAME && process.env.UI_LOGIN_PASSWORD) {
    await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
    const userSelector = process.env.UI_USERNAME_SELECTOR || 'input[name="UserName"], input[name="username"], input[type="email"], input[autocomplete="username"]';
    const passSelector = process.env.UI_PASSWORD_SELECTOR || 'input[name="Password"], input[name="password"], input[type="password"], input[autocomplete="current-password"]';
    const submitSelector = process.env.UI_SUBMIT_SELECTOR || 'button[type="submit"], input[type="submit"]';
    const user = page.locator(userSelector).first();
    const pass = page.locator(passSelector).first();
    if (await user.count() && await pass.count()) {
      await user.fill(process.env.UI_LOGIN_USERNAME);
      await pass.fill(process.env.UI_LOGIN_PASSWORD);
      await page.locator(submitSelector).first().click();
      const postLogin = process.env.UI_POST_LOGIN_SELECTOR;
      if (postLogin) await page.locator(postLogin).first().waitFor({ state: "visible", timeout: 30000 });
      else await page.waitForLoadState("domcontentloaded").catch(() => {});
      await page.waitForTimeout(1200);
    } else {
      failures.push({ url: baseUrl, error: "Login credentials were configured, but username/password selectors did not match. Configure UI_USERNAME_SELECTOR and UI_PASSWORD_SELECTOR." });
    }
  }

  while (queue.length && visited.size < maxPages) {
    const url = queue.shift();
    if (!url || visited.has(url)) continue;
    visited.add(url);
    try {
      const response = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45000 });
      await page.waitForLoadState("networkidle", { timeout: 7000 }).catch(() => {});
      await page.waitForTimeout(700);
      // Let Blazor render after navigation/circuit initialization.
      await page.locator("body").waitFor({ state: "visible", timeout: 10000 });
      const title = (await page.title().catch(() => "")) || url;
      const filename = safeName(url);
      const screenshot = path.join(outputDir, filename);
      await page.screenshot({ path: screenshot, fullPage: true, animations: "disabled", timeout: 30000 });
      captured.push({ url, title, file: path.relative("artifacts", screenshot), status: response?.status() ?? null });

      const links = await page.locator("a[href]").evaluateAll(nodes => nodes.map(a => a.getAttribute("href")).filter(Boolean));
      for (const href of links) {
        const next = normalize(href, url);
        if (next && !visited.has(next) && !queue.includes(next) && visited.size + queue.length < maxPages) queue.push(next);
      }
    } catch (error) {
      failures.push({ url, error: String(error?.message || error).slice(0, 1000) });
    }
  }
} finally {
  await browser.close();
}

const report = {
  baseUrl,
  capturedAtUtc: new Date().toISOString(),
  pagesCaptured: captured.length,
  pagesAttempted: visited.size,
  limit: maxPages,
  truncated: queue.length > 0,
  screenshots: captured,
  failures,
};
await fs.writeFile(reportPath, JSON.stringify(report, null, 2), "utf8");
if (captured.length) {
  await fs.rm(zipPath, { force: true });
  await execFileAsync("zip", ["-qr", zipPath, "ui-screenshots", "ui-capture-report.json"], { cwd: path.resolve("artifacts") });
}
console.log(`Captured ${captured.length} page(s); ${failures.length} failure(s).`);
console.log(`Report: ${reportPath}`);
if (!captured.length) process.exitCode = 1;
