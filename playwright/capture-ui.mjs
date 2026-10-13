import { chromium } from "playwright";
import fs from "node:fs/promises";
import path from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const baseUrl = process.env.BASE_URL;
if (!baseUrl) throw new Error("BASE_URL is required (set it to the deployed staging URL).");
const parsedBase = new URL(baseUrl);
if (!["http:", "https:"].includes(parsedBase.protocol)) throw new Error("BASE_URL must be HTTP(S).");

const artifactsDir = path.resolve("artifacts");
const outputDir = path.join(artifactsDir, "ui-screenshots");
const zipPath = path.join(artifactsDir, "UI_Screenshots.zip");
const reportPath = path.join(artifactsDir, "ui-capture-report.json");
const htmlReportPath = path.join(artifactsDir, "index.html");
const maxPages = Math.max(1, Number.parseInt(process.env.UI_MAX_PAGES || "500", 10));
const maxStates = Math.max(0, Number.parseInt(process.env.UI_MAX_STATES_PER_PAGE || "8", 10));
const timeout = Math.max(5000, Number.parseInt(process.env.UI_TIMEOUT_MS || "20000", 10));
const ignoredPath = /\/(logout|logoff|signout|sign-out|delete|remove|deactivate)(\/|$)/i;
const ignoredExtensions = /\.(?:pdf|zip|png|jpe?g|gif|svg|webp|ico|css|js|map|woff2?|ttf|eot|xlsx?|docx?|pptx?)(?:$|\?)/i;
const captured = [];
const failures = [];
const discovered = new Map();
const seenScreens = new Set();

function normalize(href, currentUrl) {
  try {
    const url = new URL(href, currentUrl);
    if (!["http:", "https:"].includes(url.protocol) || url.origin !== parsedBase.origin) return null;
    if (ignoredPath.test(url.pathname) || ignoredExtensions.test(url.pathname)) return null;
    if (url.searchParams.has("returnUrl") || url.searchParams.has("ReturnUrl")) return null;
    url.hash = "";
    return url.href;
  } catch { return null; }
}
function filePart(value) {
  return String(value || "screen").normalize("NFKD").replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9._-]+/g, "_").replace(/^_+|_+$/g, "").slice(0, 110) || "screen";
}
function filenameFor(url, state) {
  const u = new URL(url);
  const route = u.pathname === "/" ? "home" : u.pathname.split("/").filter(Boolean).join("_");
  const query = u.search ? "_" + Buffer.from(u.search).toString("base64url").slice(0, 10) : "";
  return filePart(route + query + (state ? "_" + state : "")) + ".png";
}
async function waitForUi(page) {
  await page.locator("body").waitFor({ state: "visible", timeout: 10000 });
  await page.waitForLoadState("networkidle", { timeout: 2500 }).catch(() => {});
  await page.waitForTimeout(350);
}
async function capture(page, url, label, responseStatus, extra = {}) {
  const key = url + "|" + label;
  if (seenScreens.has(key)) return;
  seenScreens.add(key);
  const filename = filenameFor(url, label === "default" ? "" : label);
  const screenshotPath = path.join(outputDir, filename);
  try {
    await page.screenshot({ path: screenshotPath, fullPage: true, animations: "disabled", timeout: 30000 });
    captured.push({
      url, title: (await page.title().catch(() => "")) || url,
      state: label, file: path.relative(artifactsDir, screenshotPath),
      status: responseStatus ?? null, ...extra
    });
  } catch (error) {
    failures.push({ url, state: label, error: String(error?.message || error).slice(0, 800) });
  }
}
async function readManifest() {
  try {
    const raw = JSON.parse(await fs.readFile(path.resolve("playwright/ui-routes.json"), "utf8"));
    return Array.isArray(raw.routes) ? raw.routes : [];
  } catch { return []; }
}
async function discoverRazorRoutes() {
  const roots = ["FVN_REGISTER.Web", "FVN_REGISTER.Shared"];
  const routes = [];
  async function walk(dir) {
    let entries;
    try { entries = await fs.readdir(dir, { withFileTypes: true }); } catch { return; }
    for (const entry of entries) {
      if (["bin", "obj", "node_modules", ".git"].includes(entry.name)) continue;
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) await walk(full);
      else if (entry.isFile() && entry.name.endsWith(".razor")) {
        let content;
        try { content = await fs.readFile(full, "utf8"); } catch { continue; }
        const re = /^\s*@page\s+"([^"]+)"/gm;
        let match;
        while ((match = re.exec(content)) !== null) {
          const route = match[1].trim();
          if (!route || route.includes("{") || route.includes("*")) continue;
          routes.push({ path: route.startsWith("/") ? route : "/" + route, source: path.relative(process.cwd(), full) });
        }
      }
    }
  }
  for (const root of roots) await walk(root);
  return routes;
}
async function attemptLogin(page) {
  const username = process.env.UI_LOGIN_USERNAME;
  const password = process.env.UI_LOGIN_PASSWORD;
  if (!username || !password) {
    const error = "UI_LOGIN_USERNAME/UI_LOGIN_PASSWORD are not configured; protected UI capture was skipped.";
    failures.push({ url: new URL("/login", baseUrl).href, error });
    return { configured: false, success: false, reason: "credentials-missing" };
  }

  const loginUrl = new URL("/login", baseUrl).href;
  const userSelector = process.env.UI_USERNAME_SELECTOR
    || 'input[name="UserName"], input[name="username"], input[autocomplete="username"], input[type="email"], input[type="text"]';
  const passSelector = process.env.UI_PASSWORD_SELECTOR
    || 'input[name="Password"], input[name="password"], input[autocomplete="current-password"], input[type="password"]';
  const submitSelector = process.env.UI_SUBMIT_SELECTOR
    || 'form button[type="submit"], form input[type="submit"], button[type="submit"], input[type="submit"]';

  try {
    await page.goto(loginUrl, { waitUntil: "domcontentloaded", timeout: 45000 });

    // Blazor/MudBlazor renders its controls after the initial document response.
    await page.locator(passSelector).first().waitFor({ state: "visible", timeout: 20000 });
    const user = page.locator(userSelector).first();
    const pass = page.locator(passSelector).first();
    const submit = page.locator(submitSelector).first();

    if (!(await user.count()) || !(await user.isVisible()) ||
        !(await pass.count()) || !(await pass.isVisible()) ||
        !(await submit.count()) || !(await submit.isVisible())) {
      throw new Error("Login form rendered, but the username, password, or submit control could not be located. Configure UI_USERNAME_SELECTOR, UI_PASSWORD_SELECTOR, and UI_SUBMIT_SELECTOR.");
    }

    await user.fill(username);
    await pass.fill(password);
    await submit.click();

    if (process.env.UI_POST_LOGIN_SELECTOR) {
      await page.locator(process.env.UI_POST_LOGIN_SELECTOR).first().waitFor({ state: "visible", timeout: 30000 });
      return { configured: true, success: true, method: "post-login-selector" };
    }

    // A valid login either navigates away from /login or presents the two-factor step.
    const deadline = Date.now() + 20000;
    while (Date.now() < deadline) {
      const current = new URL(page.url());
      const twoFactorVisible = await page.locator('input[maxlength="6"], input[inputmode="numeric"]').first().isVisible().catch(() => false);
      if (twoFactorVisible) {
        const error = "Login reached the two-factor verification step. UI capture requires a test account that can complete 2FA or a configured UI_POST_LOGIN_SELECTOR after completing the flow.";
        failures.push({ url: loginUrl, error });
        return { configured: true, success: false, requiresTwoFactor: true, reason: "two-factor-required" };
      }
      if (current.pathname.replace(/\/$/, "") !== "/login") {
        return { configured: true, success: true, method: "navigated-away-from-login" };
      }
      await page.waitForTimeout(250);
    }

    const error = "Login did not leave /login within 20 seconds; credentials may be invalid or the app may have displayed a validation error.";
    failures.push({ url: loginUrl, error });
    return { configured: true, success: false, reason: "login-not-confirmed" };
  } catch (error) {
    const message = String(error?.message || error).slice(0, 1000);
    failures.push({ url: loginUrl, error: message });
    return { configured: true, success: false, reason: "login-error", error: message };
  }
}
async function safeInteractiveStates(page, url, status) {
  // Only activate non-submitting navigation controls and tabs; never click arbitrary business-action buttons.
  const candidates = [
    { selector: '[role="tab"]', kind: "tab" },
    { selector: 'button[aria-haspopup="true"], [role="button"][aria-haspopup="true"]', kind: "menu" },
    { selector: 'button[data-bs-toggle="dropdown"], a[data-bs-toggle="dropdown"]', kind: "menu" },
    { selector: 'button[data-bs-toggle="tab"], a[data-bs-toggle="tab"]', kind: "tab" },
    { selector: 'button.navbar-toggler, button[aria-label*="menu" i], button[aria-label*="navigation" i]', kind: "navigation" }
  ];
  let stateCount = 0;
  for (const candidate of candidates) {
    if (stateCount >= maxStates) break;
    const loc = page.locator(candidate.selector);
    const count = Math.min(await loc.count().catch(() => 0), 20);
    for (let i = 0; i < count && stateCount < maxStates; i++) {
      const item = loc.nth(i);
      try {
        if (!(await item.isVisible()) || !(await item.isEnabled())) continue;
        const label = filePart((await item.innerText().catch(() => "")) || (await item.getAttribute("aria-label")) || candidate.kind + "-" + i);
        await item.click({ timeout: 2500 });
        await page.waitForTimeout(250);
        const stateName = filePart(candidate.kind + "-" + label + "-" + i);
        await capture(page, url, stateName, status, { interaction: candidate.kind });
        stateCount++;
      } catch { /* non-essential interaction; keep the default screenshot */ }
    }
  }
}
async function htmlReport() {
  const esc = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
  const rows = captured.map(item => '<tr><td><a href="' + esc(item.file) + '">' + esc(item.file) + '</a></td><td>' + esc(item.title) + '</td><td><a href="' + esc(item.url) + '">' + esc(item.url) + '</a></td><td>' + esc(item.state) + '</td><td>' + esc(item.status) + '</td></tr>').join("");
  const failureRows = failures.map(item => '<li><code>' + esc(item.url) + '</code> — ' + esc(item.error) + '</li>').join("");
  const html = '<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>FVN_REGISTER UI Capture Report</title><style>body{font:15px system-ui;margin:2rem;color:#222}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ddd;padding:.5rem;text-align:left;overflow-wrap:anywhere}th{background:#f4f4f4}code{overflow-wrap:anywhere}.summary{display:flex;gap:1rem;flex-wrap:wrap}</style><h1>FVN_REGISTER — Báo cáo chụp giao diện</h1><p>URL gốc: <a href="' + esc(baseUrl) + '">' + esc(baseUrl) + '</a></p><div class="summary"><p>Ảnh chụp: <b>' + captured.length + '</b></p><p>Lỗi: <b>' + failures.length + '</b></p><p>Giới hạn route: <b>' + maxPages + '</b></p></div><h2>Ảnh đã tạo</h2><table><thead><tr><th>Ảnh</th><th>Tiêu đề</th><th>URL</th><th>Trạng thái UI</th><th>HTTP</th></tr></thead><tbody>' + rows + '</tbody></table><h2>Lỗi / cần kiểm tra</h2><ul>' + (failureRows || "<li>Không ghi nhận lỗi điều hướng.</li>") + '</ul><p>Ảnh là ảnh tài liệu, không phải bằng chứng kiểm thử nghiệp vụ. Hãy kiểm tra route bị bảo vệ, route cần dữ liệu mẫu và các màn hình theo vai trò.</p></html>';
  await fs.writeFile(htmlReportPath, html, "utf8");
}

await fs.rm(outputDir, { recursive: true, force: true });
await fs.mkdir(outputDir, { recursive: true });
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: Number(process.env.UI_VIEWPORT_WIDTH || 1440), height: Number(process.env.UI_VIEWPORT_HEIGHT || 1000) },
  deviceScaleFactor: 1,
  ignoreHTTPSErrors: process.env.UI_IGNORE_HTTPS_ERRORS === "true"
});
const page = await context.newPage();
page.setDefaultTimeout(timeout);
// Discover routes independently of authentication so a login failure does not misleadingly
// report zero routes. Protected pages are never counted as captured unless accessible.
const manifest = await readManifest();
const razorRoutes = await discoverRazorRoutes();
const routeQueue = [];
const addRoute = (href, source, label) => {
  const url = normalize(href, baseUrl);
  if (!url || discovered.has(url) || new URL(url).pathname.replace(/\/$/, "") === "/login") return;
  discovered.set(url, { url, source, label: label || "" });
  routeQueue.push(url);
};
addRoute(baseUrl, "base-url", "Trang chủ");
for (const item of razorRoutes) addRoute(item.path, "razor:" + item.source, item.path);
for (const item of manifest) {
  if (typeof item === "string") addRoute(item, "manifest", item);
  else if (item && typeof item.path === "string") addRoute(item.path, "manifest", item.name || item.path);
}

let pagesAttempted = 0;
let login = { configured: false, success: null };
try {
  login = await attemptLogin(page);

  if (login.success === true) {
    let index = 0;
    while (index < routeQueue.length && index < maxPages) {
      const url = routeQueue[index++];
      pagesAttempted++;
      let responseStatus = null;
      try {
        const response = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45000 });
        responseStatus = response?.status() ?? null;
        await waitForUi(page);

        const actualUrl = new URL(page.url());
        if (actualUrl.pathname.replace(/\/$/, "") === "/login") {
          failures.push({
            url,
            error: "Route redirected to /login. It was not captured as a valid authenticated page; check session persistence or route permissions."
          });
          continue;
        }

        await capture(page, url, "default", responseStatus, { source: discovered.get(url)?.source || "link" });
        await safeInteractiveStates(page, url, responseStatus);
        const links = await page.locator("a[href]").evaluateAll(nodes => nodes.map(a => ({
          href: a.getAttribute("href"),
          text: (a.innerText || a.getAttribute("aria-label") || "").trim().slice(0, 120)
        })).filter(a => a.href));
        for (const link of links) {
          const next = normalize(link.href, url);
          if (next && !discovered.has(next) && discovered.size < maxPages * 4 &&
              new URL(next).pathname.replace(/\/$/, "") !== "/login") {
            discovered.set(next, { url: next, source: "link", label: link.text });
            routeQueue.push(next);
          }
        }
      } catch (error) {
        failures.push({ url, error: String(error?.message || error).slice(0, 1000) });
      }
    }
  } else {
    // Keep a diagnostic screenshot of the login form, but never crawl protected pages
    // or report them as captured when authentication was not confirmed.
    const loginUrl = new URL("/login", baseUrl).href;
    try {
      if (new URL(page.url()).pathname.replace(/\/$/, "") !== "/login") {
        await page.goto(loginUrl, { waitUntil: "domcontentloaded", timeout: 45000 });
      }
      await waitForUi(page);
      const responseStatus = await page.locator("body").count().catch(() => 0);
      await capture(page, loginUrl, "login-diagnostic", responseStatus ? 200 : null, { source: "login-diagnostic" });
    } catch (error) {
      failures.push({ url: loginUrl, error: "Could not capture login diagnostic: " + String(error?.message || error).slice(0, 700) });
    }
  }
} finally {
  await browser.close();
}
const report = {
  baseUrl, capturedAtUtc: new Date().toISOString(),
  login, pagesDiscovered: discovered.size, pagesAttempted,
  pagesCaptured: captured.length, limit: maxPages, truncated: discovered.size > maxPages,
  screenshotCount: captured.length, failureCount: failures.length,
  screenshots: captured, failures
};
await fs.writeFile(reportPath, JSON.stringify(report, null, 2), "utf8");
await htmlReport();
if (captured.length) {
  await fs.rm(zipPath, { force: true });
  await execFileAsync("zip", ["-qr", zipPath, "ui-screenshots", "ui-capture-report.json", "index.html"], { cwd: artifactsDir });
}
console.log("Discovered " + discovered.size + " route(s); captured " + captured.length + " screen(s); " + failures.length + " failure(s).");
console.log("ZIP: " + zipPath);
console.log("HTML report: " + htmlReportPath);
if (!captured.length || login.success !== true || failures.length > 0) process.exitCode = 1;
