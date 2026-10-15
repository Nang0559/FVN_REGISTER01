import { expect, test, type ElementHandle, type Page, type Request } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';

/**
 * Authenticated, read-only UI crawler for FVN_REGISTER (Blazor Server + MudBlazor).
 *
 * Key design rules (see comments at each helper):
 *  1. NEVER call page.goto()/page.reload() after login. Blazor Server keeps the JWT in
 *     CustomAuthStateProvider.CurrentToken (per circuit); a reload creates a new circuit,
 *     AuthorizeRouteView sees an anonymous user and redirects to /login. All navigation after
 *     login goes through window.Blazor.navigateTo() or real clicks.
 *  2. Expand every collapsed MudNavGroup first, otherwise their child links are never found.
 *  3. Phase 1 clicks every navigation link (one screenshot per page).
 *     Phase 2 explores in-page tabs/buttons/links of every captured page.
 *  4. Readiness = no loader + no fetch/xhr + no SignalR render traffic + stable DOM.
 *  5. manifest.json is rewritten after every screenshot and the crawl stops by itself after
 *     PLAYWRIGHT_CRAWL_BUDGET_MINUTES so reports/artifacts are always produced.
 */

const username = process.env.PLAYWRIGHT_USERNAME?.trim() ?? '';
const password = process.env.PLAYWRIGHT_PASSWORD ?? '';
const screenshotDir = path.resolve('test-results/screenshots');
const maxStates = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_STATES ?? '180', 10) || 180);
const maxClicks = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_CLICK_TARGETS ?? '500', 10) || 500);
const maxDepth = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_DEPTH ?? '12', 10) || 12);
const budgetMinutes = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_CRAWL_BUDGET_MINUTES ?? '40', 10) || 40);
// Phase 2 fairness: every page gets a bounded number of in-page interactions and bounded time.
const maxTargetsPerPage = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_TARGETS_PER_PAGE ?? '30', 10) || 30);
const pageBudgetSeconds = Math.max(15, Number.parseInt(process.env.PLAYWRIGHT_PAGE_BUDGET_SECONDS ?? '150', 10) || 150);
// Detail pages such as /leave/detail/123: capture at most this many per route pattern.
const MAX_PER_ROUTE_PATTERN = 2;
const expandScrollContainers = (process.env.PLAYWRIGHT_EXPAND_SCROLL ?? 'true').toLowerCase() !== 'false';

// A route that does not exist: renders the lightweight NotFound view inside MainLayout.
// Used only to reset same-route state (tabs, dialogs) without creating a new circuit.
const RESET_ROUTE = '/pw-reset-blank';

// ---------------------------------------------------------------------------------------------
// Loading indicators (MudBlazor first, then generic ones).
// Indicators inside the app bar / drawer are layout chrome and are ignored in countBusy().
// ---------------------------------------------------------------------------------------------
const loadingSelector = [
  '[aria-busy="true"]',
  '[role="progressbar"]',
  '.loading',
  '.spinner',
  '.skeleton',
  '.mud-progress-circular',
  '.mud-progress-linear',
  '.mud-skeleton',
  '.mud-table-loading',
  '.e-spinner-pane',
  '.ant-spin-spinning',
  '.el-loading-mask',
  '.ngx-spinner-overlay',
].join(', ');

// ---------------------------------------------------------------------------------------------
// Read-only policy
//  - Real same-origin navigation links (<a href>) are safe to follow, except logout/download/
//    export/api/approve-style URLs.
//  - Buttons and href-less controls are only clicked if their label has NO action word.
//    Matching is by whole word (Unicode aware) so "Phê duyệt" menu groups and words like
//    "address" / "renewal" are no longer rejected by substring accident.
//  - Navigation menu items and tabs only need to avoid logout.
//  - Nothing inside a form, dialog or the app bar is clicked in phase 2.
// ---------------------------------------------------------------------------------------------
const LETTER = '[\\p{L}\\p{N}]';
const wholeWords = (list: string[]) => `(?<!${LETTER})(?:${list.join('|')})(?!${LETTER})`;
const LOGOUT_WORDS = ['logout', 'log out', 'log-out', 'signout', 'sign out', 'sign-out', 'đăng\\s*xuất', 'ログアウト'];
const ACTION_WORDS = [
  ...LOGOUT_WORDS,
  'delete', 'remove', 'approve', 'reject', 'submit', 'save', 'lưu', 'xóa', 'xoá', 'duyệt', 'từ chối', 'gửi',
  'download', 'export', 'tải xuống', 'hủy', 'huỷ', 'cancel', 'create', 'new', 'add', 'edit', 'update', 'assign',
  'grant', 'revoke', 'confirm', 'publish', 'execute', 'start', 'stop', 'reset', 'refresh', 'sync', 'import',
  'upload', 'tạo', 'thêm', 'sửa', 'cập nhật', 'phân công', 'cấp quyền', 'thu hồi', 'xác nhận', 'đồng bộ',
  'nhập', 'tải lên', 'thực hiện', 'yes', 'ok', 'có', 'đồng ý', 'accept', 'proceed', 'continue', 'tiếp tục',
  'chấp nhận', 'apply', 'áp dụng', 'send', 'clear',
];
const ACTION_SOURCE = wholeWords(ACTION_WORDS);
const LOGOUT_SOURCE = wholeWords(LOGOUT_WORDS);
const HREF_BLOCK_SOURCE =
  '(?:^|/)api/|(?:^|[/.?&=_-])(?:download|export|logout|signout|sign-out|log-out|sync|approve|reject|delete|remove|cancel|revoke|execute|confirm)(?:[/?#&=_-]|$)|\\.(?:pdf|xlsx?|docx?|csv|zip)(?:[?#]|$)';
const NAV_SCOPE = '.mud-navmenu, .mud-drawer, nav, [role="navigation"]';
const TARGET_SELECTOR = 'a[href], .mud-nav-link, .mud-tab, button, [role="button"], [role="link"], [role="tab"]';

class SessionLostError extends Error {}

type Target = { key: string; label: string; href: string; idx: number; kind: 'tab' | 'button' | 'link' };
type Tracker = ReturnType<typeof installTracker>;
type Scope = 'nav' | 'content';

function safeFileName(value: string): string {
  const cleaned = value
    .replace(/^https?:\/\/[^/]+/i, '')
    .replace(/[?#].*$/, '')
    .replace(/[^a-zA-Z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 110);
  return `${cleaned || 'screen'}-${createHash('sha1').update(value).digest('hex').slice(0, 8)}`;
}

const slug = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd')
  .replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase().slice(0, 40);
const patternOf = (url: string) => new URL(url).pathname.toLowerCase()
  .replace(/\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g, '/:id')
  .replace(/\/\d+(?=\/|$)/g, '/:id');

const isLoginPath = (pathname: string) => /\/login(?:\/|$)/i.test(pathname);

// ---------------------------------------------------------------------------------------------
// Screenshots
// ---------------------------------------------------------------------------------------------
const EXPAND_SCROLL_CSS = `
  .mud-table-container, .mud-data-grid .mud-table-container { max-height: none !important; height: auto !important; overflow: visible !important; }
  .mud-dialog .mud-dialog-content { max-height: none !important; overflow: visible !important; }
`;

async function settleForScreenshot(page: Page): Promise<void> {
  await page.evaluate(async () => {
    const pending = [...document.images].filter(image => !image.complete);
    const imagesLoaded = Promise.all(pending.map(image => new Promise<void>(resolve => {
      image.addEventListener('load', () => resolve(), { once: true });
      image.addEventListener('error', () => resolve(), { once: true });
    })));
    await Promise.race([imagesLoaded, new Promise<void>(resolve => setTimeout(resolve, 5_000))]);
    await (document as unknown as { fonts?: { ready: Promise<unknown> } }).fonts?.ready;
  }).catch(() => undefined);
  await page.mouse.move(0, 0).catch(() => undefined); // drop hover tooltips
  await page.waitForTimeout(300);
}

async function saveScreenshot(page: Page, name: string): Promise<void> {
  const options = {
    path: path.join(screenshotDir, `${safeFileName(name)}.png`),
    fullPage: true,
    animations: 'disabled' as const,
    timeout: 30_000,
  };
  // fullPage only grows with the document. MudTable/MudDataGrid with a fixed Height scroll
  // internally, so temporarily lift their height limit to capture every loaded row.
  let styleTag: ElementHandle | undefined;
  if (expandScrollContainers) {
    styleTag = await page.addStyleTag({ content: EXPAND_SCROLL_CSS }).catch(() => undefined);
  }
  try {
    await page.screenshot(options);
  } catch (error) {
    if (!styleTag) throw error;
    await styleTag.evaluate(element => (element as unknown as Element).remove()).catch(() => undefined);
    styleTag = undefined;
    await page.screenshot(options); // retry without the injected CSS
  } finally {
    await styleTag?.evaluate(element => (element as unknown as Element).remove()).catch(() => undefined);
  }
}

// ---------------------------------------------------------------------------------------------
// Login (unchanged behaviour)
// ---------------------------------------------------------------------------------------------
async function signIn(page: Page): Promise<void> {
  if (!username || !password) {
    throw new Error('Missing PLAYWRIGHT_USERNAME / PLAYWRIGHT_PASSWORD Actions secrets.');
  }

  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 45_000 });
  const passwordInput = page.locator('input[type="password"]:visible').first();
  await expect(passwordInput).toBeVisible({ timeout: 20_000 });

  // Prefer the accessible label from Login.razor, then fall back to editable
  // MudTextField inputs. MudSelect renders a readonly .mud-select-input and must
  // never be mistaken for the username field.
  const labelledUserInput = page.getByLabel(/username|user name|tên đăng nhập|tài khoản|mã nhân viên|ユーザー名|ログインID/i).first();
  let userInput = labelledUserInput;
  let foundEditable = await labelledUserInput.count().then(async count =>
    count > 0 && await labelledUserInput.isVisible().catch(() => false)
      && await labelledUserInput.isEditable().catch(() => false),
  ).catch(() => false);

  const userSelectors = [
    'input[autocomplete="username"]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[name*="user" i]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[id*="user" i]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[name*="employee" i]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[id*="employee" i]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    '.mud-input-control input.mud-input-slot:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[type="email"]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
    'input[type="text"]:visible:not([readonly]):not([disabled]):not(.mud-select-input)',
  ];
  if (!foundEditable) {
    for (const selector of userSelectors) {
      const candidates = page.locator(selector);
      const count = Math.min(await candidates.count().catch(() => 0), 20);
      for (let i = 0; i < count; i++) {
        const candidate = candidates.nth(i);
        if (await candidate.isEditable().catch(() => false)) {
          userInput = candidate;
          foundEditable = true;
          break;
        }
      }
      if (foundEditable) break;
    }
  }
  if (!foundEditable) {
    await saveScreenshot(page, 'login-no-editable-username');
    const visibleInputs = await page.locator('input:visible').evaluateAll(inputs => inputs.map(input => ({
      type: input.getAttribute('type'), name: input.getAttribute('name'), id: input.id,
      autocomplete: input.getAttribute('autocomplete'),
      readonly: (input as HTMLInputElement).readOnly,
      disabled: (input as HTMLInputElement).disabled,
      placeholder: input.getAttribute('placeholder'),
    })));
    throw new Error('No editable username field found. Visible inputs: ' + JSON.stringify(visibleInputs));
  }
  await userInput.fill(username, { timeout: 10_000 });
  await passwordInput.fill(password, { timeout: 10_000 });

  const submitCandidates = page.locator('button[type="submit"]:visible, input[type="submit"]:visible');
  if (await submitCandidates.count()) {
    await submitCandidates.first().click({ timeout: 10_000 });
  } else {
    const loginButton = page.getByRole('button', { name: /đăng nhập|login|sign in/i }).first();
    if (await loginButton.count()) await loginButton.click({ timeout: 10_000 });
    else await passwordInput.press('Enter');
  }
  const loginDeadline = Date.now() + 45_000;
  while (Date.now() < loginDeadline) {
    const passwordVisible = await page.locator('input[type="password"]:visible').count().catch(() => 0);
    if (passwordVisible === 0 && !isLoginPath(new URL(page.url()).pathname)) break;
    await page.waitForTimeout(500);
  }
  const stillOnLogin = isLoginPath(new URL(page.url()).pathname)
    || (await page.locator('input[type="password"]:visible').count()) > 0;
  if (stillOnLogin) {
    await saveScreenshot(page, 'login-failed');
    const twoFactorVisible = await page.getByText(/two.factor|xác thực hai lớp|xác thực 2 lớp|mã xác minh|verification code|認証コード/i).count().catch(() => 0);
    if (twoFactorVisible > 0) {
      throw new Error('Login reached a two-factor verification step. This crawler currently needs a configured TOTP secret or a test account without 2FA; inspect login-failed.png.');
    }
    throw new Error('Login did not complete within 45 seconds. Inspect login-failed.png and failure-current-page.png for validation messages or a different login flow.');
  }
}

// ---------------------------------------------------------------------------------------------
// Readiness
// ---------------------------------------------------------------------------------------------
function installTracker(page: Page) {
  const pending = new Set<Request>();
  let lastActivity = Date.now();
  let frames = 0;
  const ignoredRequest = /signalr|\/negotiate(?:\?|$)|telemetry|analytics|hotjar|clarity|\/collect(?:\?|$)/i;
  const trackable = (request: Request) =>
    ['fetch', 'xhr'].includes(request.resourceType()) && !ignoredRequest.test(request.url());

  page.on('request', request => {
    if (trackable(request)) {
      pending.add(request);
      lastActivity = Date.now();
    }
  });
  const finish = (request: Request) => {
    if (pending.delete(request)) lastActivity = Date.now();
  };
  page.on('requestfinished', finish);
  page.on('requestfailed', finish);

  // Blazor Server loads data server-side and pushes UI diffs through the SignalR WebSocket,
  // so fetch/xhr tracking alone sees nothing. Count meaningful server->browser frames as
  // activity (keep-alive pings are only a few bytes and are ignored).
  page.on('websocket', socket => {
    socket.on('framereceived', ({ payload }) => {
      if (payload.length > 24) {
        lastActivity = Date.now();
        frames++;
      }
    });
  });

  return {
    pending,
    get lastActivity() { return lastActivity; },
    get frames() { return frames; },
  };
}

async function countBusy(page: Page): Promise<number> {
  return page.locator(loadingSelector).evaluateAll(elements => elements.filter(element => {
    if (element.closest('.mud-appbar, .mud-drawer, header')) return false; // layout chrome
    const style = getComputedStyle(element);
    const rect = element.getBoundingClientRect();
    return style.display !== 'none' && style.visibility !== 'hidden'
      && Number(style.opacity || '1') > 0 && rect.width > 0 && rect.height > 0;
  }).length).catch(() => 0);
}

async function getDomSignature(page: Page): Promise<string> {
  const raw = await page.locator('body').evaluate(element => {
    const normalize = (value: string) => value.replace(/\s+/g, ' ').trim();
    const text = normalize((element as HTMLElement).innerText || '').slice(0, 8000);
    const controls = [...element.querySelectorAll('a,button,[role="button"],[role="link"],[role="tab"]')]
      .slice(0, 250)
      .map(control => normalize(
        control.getAttribute('aria-label') || control.getAttribute('title') || control.textContent || '',
      ).slice(0, 160));
    const headings = [...element.querySelectorAll('h1,h2,h3,[role="heading"]')]
      .map(heading => normalize(heading.textContent || '').slice(0, 120));
    return `${text}|headings:${headings.join('|')}|controls:${controls.join('|')}`;
  }).catch(() => '');
  return raw ? createHash('sha1').update(raw).digest('hex') : '';
}

/**
 * Returns true when the page is really settled, false when we gave up waiting
 * (the caller still captures but records `ready:false` in the manifest).
 * Settled means: no visible content loader, no pending fetch/xhr, no SignalR render frames for
 * QUIET_MS, and the same DOM signature for STABLE_SAMPLES consecutive samples.
 */
async function waitReady(page: Page, tracker: Tracker, label: string, timeoutMs = 60_000): Promise<boolean> {
  const QUIET_MS = 1_500;
  const STABLE_SAMPLES = 3;
  const POLL_MS = 400;
  const IDLE_FALLBACK_MS = 10_000;   // live page that keeps changing but has no loader / traffic
  const FROZEN_LOADER_MS = 30_000;   // loader that never disappears while nothing else happens

  const started = Date.now();
  let previousSignature = '';
  let stableSamples = 0;
  let idleSince = 0;
  let lastChange = Date.now();
  let lastLog = 0;

  await page.waitForLoadState('domcontentloaded').catch(() => undefined);
  await page.locator('body').waitFor({ state: 'visible', timeout: 15_000 }).catch(() => undefined);

  while (Date.now() - started < timeoutMs) {
    const elapsed = Date.now() - started;
    const busy = await countBusy(page);
    const signature = await getDomSignature(page);
    const quiet = tracker.pending.size === 0 && Date.now() - tracker.lastActivity >= QUIET_MS;

    if (signature !== previousSignature) lastChange = Date.now();
    if (!busy && quiet && signature && signature === previousSignature) stableSamples++;
    else stableSamples = 0;
    previousSignature = signature;

    if (stableSamples >= STABLE_SAMPLES) {
      console.log(`Page ready after ${Math.round(elapsed / 1000)}s: ${label}`);
      return true;
    }

    if (!busy && quiet) {
      if (!idleSince) idleSince = Date.now();
      if (Date.now() - idleSince >= IDLE_FALLBACK_MS) {
        console.log(`Page idle with live DOM changes; continuing after ${Math.round(elapsed / 1000)}s: ${label}`);
        return true;
      }
    } else {
      idleSince = 0;
    }

    if (busy && Date.now() - lastChange >= FROZEN_LOADER_MS && Date.now() - tracker.lastActivity >= FROZEN_LOADER_MS) {
      console.warn(`Loader visible but page frozen for ${FROZEN_LOADER_MS / 1000}s; capturing as not-ready: ${label}`);
      return false;
    }

    if (elapsed - lastLog >= 10_000) {
      lastLog = elapsed;
      console.log(`Waiting: ${label}; elapsed=${Math.round(elapsed / 1000)}s, loaders=${busy}, pending=${tracker.pending.size}, frames=${tracker.frames}`);
    }
    await page.waitForTimeout(POLL_MS);
  }

  console.warn(`Readiness timeout (${Math.round(timeoutMs / 1000)}s); capturing as not-ready: ${label}`);
  return false;
}

// ---------------------------------------------------------------------------------------------
// Navigation WITHOUT creating a new Blazor circuit
// ---------------------------------------------------------------------------------------------
async function navigateInApp(page: Page, urlOrPath: string): Promise<void> {
  const target = new URL(urlOrPath, page.url());
  const wanted = target.pathname + target.search;
  const ok = await page.evaluate(destination => {
    const blazor = (window as unknown as { Blazor?: { navigateTo?: (uri: string) => void } }).Blazor;
    if (!blazor?.navigateTo) return false;
    blazor.navigateTo(destination);
    return true;
  }, wanted);
  if (!ok) {
    throw new Error('window.Blazor.navigateTo is unavailable. Refusing to reload the page: a reload would start a new Blazor Server circuit and drop the login.');
  }
  await page.waitForFunction(
    expected => (location.pathname + location.search).toLowerCase() === expected.toLowerCase(),
    wanted,
    { timeout: 8_000 },
  ).catch(() => undefined); // the app may redirect to a different final URL
}

async function assertAuthenticated(page: Page): Promise<void> {
  if (isLoginPath(new URL(page.url()).pathname)) {
    throw new SessionLostError(`Session lost: the app redirected to ${page.url()}. Stopping the crawl instead of capturing login pages.`);
  }
}

async function closeTransientUi(page: Page): Promise<void> {
  await page.keyboard.press('Escape').catch(() => undefined);
  await page.waitForTimeout(100);
  await page.keyboard.press('Escape').catch(() => undefined);
}

let fallbackGroupExpandDone = false;

/** Opens the drawer if needed and expands every collapsed MudNavGroup (13 groups start collapsed). */
async function expandNavigation(page: Page): Promise<{ visibleLinks: number; expandedGroups: number }> {
  const visibleLinks = () => page.locator('.mud-navmenu a.mud-nav-link:visible').count().catch(() => 0);
  let expandedGroups = 0;

  if ((await visibleLinks()) === 0) {
    const toggle = page.locator('.mud-appbar button:visible').first();
    if (await toggle.count().catch(() => 0)) {
      await toggle.click({ timeout: 3_000 }).catch(() => undefined);
      await page.waitForTimeout(400);
    }
  }

  for (let round = 0; round < 6; round++) {
    const collapsed = page.locator('.mud-navmenu [aria-expanded="false"]:visible');
    const count = await collapsed.count().catch(() => 0);
    if (count === 0) break;
    for (let i = 0; i < Math.min(count, 25); i++) {
      const clicked = await collapsed.first().click({ timeout: 2_000 }).then(() => true).catch(() => false);
      if (clicked) expandedGroups++;
      await page.waitForTimeout(150);
    }
    await page.waitForTimeout(350); // MudCollapse animation
  }

  // Fallback if this MudBlazor build does not expose aria-expanded on group headers:
  // click each header once (they all start collapsed because of Expanded="false").
  if (!fallbackGroupExpandDone && expandedGroups === 0) {
    const headers = page.locator('.mud-navmenu .mud-nav-group > button:visible');
    const count = await headers.count().catch(() => 0);
    if (count > 0) {
      fallbackGroupExpandDone = true;
      for (let i = 0; i < Math.min(count, 25); i++) {
        await headers.nth(i).click({ timeout: 2_000 }).catch(() => undefined);
        await page.waitForTimeout(150);
      }
      await page.waitForTimeout(350);
    }
  }
  return { visibleLinks: await visibleLinks(), expandedGroups };
}

// ---------------------------------------------------------------------------------------------
// Target discovery. Elements are tagged with data-pw-i so the click is a single round trip.
// Tags are rebuilt by every call, and a click always re-collects first (the DOM changes after
// every navigation), then matches on the stable semantic key.
// ---------------------------------------------------------------------------------------------
async function collectTargets(page: Page, scope: Scope): Promise<Target[]> {
  return page.evaluate(args => {
    const action = new RegExp(args.actionSource, 'iu');
    const logout = new RegExp(args.logoutSource, 'iu');
    const hrefBlock = new RegExp(args.hrefBlockSource, 'i');
    const normalize = (value: string) => value.replace(/\s+/g, ' ').trim();

    document.querySelectorAll('[data-pw-i]').forEach(element => element.removeAttribute('data-pw-i'));

    const dialogOpen = [...document.querySelectorAll('.mud-dialog')]
      .some(dialog => (dialog as HTMLElement).getClientRects().length > 0);
    if (dialogOpen) return []; // background is covered by an overlay; capture the dialog only

    const isClipped = (element: HTMLElement) => {
      const rect = element.getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      for (let parent = element.parentElement; parent && parent !== document.body; parent = parent.parentElement) {
        const style = getComputedStyle(parent);
        if (style.display === 'none' || style.visibility === 'hidden') return true;
        if (!['hidden', 'clip'].includes(style.overflowY) && !['hidden', 'clip'].includes(style.overflowX)) continue;
        const box = parent.getBoundingClientRect();
        if (box.height < 1 || box.width < 1) return true; // collapsed MudCollapse
        if (cy < box.top - 1 || cy > box.bottom + 1 || cx < box.left - 1 || cx > box.right + 1) return true;
      }
      return false;
    };

    const result: Array<{ key: string; label: string; href: string; idx: number; kind: 'tab' | 'button' | 'link' }> = [];
    const occurrence = new Map<string, number>();
    const elements = [...document.querySelectorAll(args.selector)] as HTMLElement[];

    for (const element of elements) {
      if (result.length >= 250) break;

      const inNav = !!element.closest(args.navScope);
      if (args.scope === 'nav' ? !inNav : inNav) continue;
      if (args.scope === 'content' && element.closest('.mud-appbar, header')) continue;
      if (element.closest('.mud-dialog, .mud-overlay, .mud-popover')) continue;

      const rect = element.getBoundingClientRect();
      if (element.getClientRects().length === 0 || rect.width < 8 || rect.height < 8) continue;
      const style = getComputedStyle(element);
      if (style.visibility === 'hidden' || style.display === 'none') continue;
      if (isClipped(element)) continue;
      if (element.getAttribute('aria-disabled') === 'true' || element.hasAttribute('disabled')) continue;
      if (element instanceof HTMLButtonElement && element.type.toLowerCase() === 'submit') continue;

      const isTab = element.matches('.mud-tab, [role="tab"]');
      const isAnchor = element instanceof HTMLAnchorElement;
      const rawHref = element.getAttribute('href') ?? '';
      const hasRealHref = isAnchor && rawHref !== '' && !rawHref.startsWith('#') && !/^(javascript|mailto|tel):/i.test(rawHref);
      const label = normalize(
        element.getAttribute('aria-label') || element.getAttribute('title') || element.innerText || element.textContent || '',
      ).slice(0, 180);

      let href = '';
      if (hasRealHref) {
        const url = new URL((element as HTMLAnchorElement).href, location.href);
        if (url.origin !== location.origin) continue;
        if ((element as HTMLAnchorElement).target === '_blank') continue;
        if (hrefBlock.test(url.pathname + url.search)) continue;
        if (logout.test(label)) continue;
        href = url.href;
        if (href.split('#')[0] === location.href.split('#')[0]) continue; // link to the current page
      } else if (inNav) {
        if (element.hasAttribute('aria-expanded')) continue; // group header, expanded separately
        if (!label || logout.test(label)) continue;
      } else if (isTab) {
        if (!label || logout.test(label)) continue;
      } else {
        if (!label || action.test(label)) continue;
        if (element.closest('form')) continue;
      }
      if (hasRealHref && element.closest('form') && !inNav) continue;

      const identity = `${element.tagName}|${label}|${href}`;
      const index = occurrence.get(identity) ?? 0;
      occurrence.set(identity, index + 1);
      const idx = result.length;
      element.setAttribute('data-pw-i', String(idx));
      const kind = hasRealHref ? 'link' : isTab ? 'tab' : 'button';
      result.push({ key: `${identity}|${index}`, label, href, idx, kind });
    }
    return result;
  }, {
    scope,
    navScope: NAV_SCOPE,
    selector: TARGET_SELECTOR,
    actionSource: ACTION_SOURCE,
    logoutSource: LOGOUT_SOURCE,
    hrefBlockSource: HREF_BLOCK_SOURCE,
  });
}

async function clickTarget(page: Page, target: Target, scope: Scope): Promise<boolean> {
  const fresh = await collectTargets(page, scope);
  const match = fresh.find(candidate => candidate.key === target.key);
  if (!match) return false;
  const element = page.locator(`[data-pw-i="${match.idx}"]`).first();
  await element.scrollIntoViewIfNeeded({ timeout: 3_000 }).catch(() => undefined);
  await element.click({ timeout: 8_000 });
  return true;
}

async function restore(page: Page, tracker: Tracker, url: string): Promise<void> {
  await closeTransientUi(page);
  const target = new URL(url);
  const here = new URL(page.url());
  if (here.pathname === target.pathname && here.search === target.search) {
    // Same route: bounce through a blank route to reset tabs/dialogs inside the same circuit.
    await navigateInApp(page, RESET_ROUTE);
    await page.waitForTimeout(400);
  }
  await navigateInApp(page, target.pathname + target.search);
  await waitReady(page, tracker, `restore:${target.pathname}`, 60_000);
  await assertAuthenticated(page);
  await expandNavigation(page);
}

// ---------------------------------------------------------------------------------------------
// The crawl
// ---------------------------------------------------------------------------------------------
test('recursively captures authenticated read-only UI states', async ({ page, baseURL }) => {
  test.setTimeout((budgetMinutes + 8) * 60 * 1000);
  await mkdir(screenshotDir, { recursive: true });

  const origin = new URL(baseURL!).origin;
  const startedAt = Date.now();
  const deadline = startedAt + budgetMinutes * 60 * 1000;
  const visitedStates = new Set<string>();
  const visitedPaths = new Set<string>();
  const attemptedClicks = new Set<string>();
  const capturedStates: Array<{
    number: number; depth: number; reason: string; url: string; signature: string; screenshot: string; ready: boolean;
  }> = [];
  let captured = 0;
  let clicks = 0;
  let abortReason: string | undefined;
  const explored: Array<{ url: string; tried: number; captured: number; knownLinksSkipped: number; remaining: number; tabs: number; buttons: number; links: number }> = [];
  const patternCounts = new Map<string, number>();

  const pathKeyOf = (url: string) => {
    const parsed = new URL(url);
    return `${parsed.pathname.replace(/\/+$/, '') || '/'}${parsed.search}`.toLowerCase();
  };
  const outOfBudget = () => Date.now() > deadline || visitedStates.size >= maxStates || clicks >= maxClicks;

  async function writeManifest(status: string): Promise<void> {
    await writeFile(path.join(screenshotDir, 'manifest.json'), JSON.stringify({
      baseUrl: origin,
      generatedAt: new Date().toISOString(),
      status,
      abortReason,
      limits: { maxStates, maxClicks, maxDepth, budgetMinutes },
      summary: {
        states: visitedStates.size,
        screenshots: captured,
        clicks,
        notReady: capturedStates.filter(state => !state.ready).length,
        elapsedSeconds: Math.round((Date.now() - startedAt) / 1000),
      },
      explored,
      states: capturedStates,
    }, null, 2), 'utf8').catch(() => undefined);
  }

  const tracker = installTracker(page);
  try {
    await signIn(page); // exactly one login for the whole crawl
  } catch (error) {
    await page.screenshot({ path: path.join(screenshotDir, 'failure-current-page.png'), fullPage: true, timeout: 15_000 }).catch(() => undefined);
    abortReason = error instanceof Error ? error.message : String(error);
    await writeManifest('login-failed');
    throw error;
  }

  const homeReady = await waitReady(page, tracker, 'home-dashboard-after-login', 60_000);
  const navInfo = await expandNavigation(page);
  console.log(`Navigation: ${navInfo.visibleLinks} visible links, ${navInfo.expandedGroups} groups expanded.`);

  async function captureCurrent(depth: number, reason: string, ready: boolean) {
    const url = new URL(page.url());
    url.hash = '';
    if (url.origin !== origin || isLoginPath(url.pathname)) return null;
    const signature = await getDomSignature(page);
    if (!signature) return null;
    const stateKey = `${url.href}|${signature}`;
    if (visitedStates.has(stateKey)) return { url, stateKey, isNew: false };
    visitedStates.add(stateKey);

    captured++;
    await settleForScreenshot(page);
    const screenshotName = `state-${captured}-depth-${depth}-${slug(reason)}--${url.pathname}${url.search}`;
    await saveScreenshot(page, screenshotName);
    capturedStates.push({
      number: captured, depth, reason, url: url.href, signature,
      screenshot: `${safeFileName(screenshotName)}.png`, ready,
    });
    console.log(`Captured state #${captured}: depth=${depth}, ready=${ready}, reason=${reason}, url=${url.pathname}${url.search}`);
    await writeManifest('running');
    return { url, stateKey, isNew: true };
  }

  const isDialogOpen = () => page.locator('.mud-dialog:visible').count().then(count => count > 0).catch(() => false);

  // Phase 2: tabs first, then in-page buttons, then links. Same-page interactions (tabs, filters)
  // do NOT restore the page between clicks, so they are cheap; the page is only restored after a
  // navigation, a dialog that will not close, or a target that disappeared.
  async function explore(depth: number, parentUrl: string): Promise<void> {
    if (depth > maxDepth || outOfBudget()) return;

    const parentPath = pathKeyOf(parentUrl);
    const rank = { tab: 0, button: 1, link: 2 } as const;
    const seenKeys = new Set<string>();
    const queue: Target[] = [];
    const stats = { tabs: 0, buttons: 0, links: 0, knownLinksSkipped: 0, captured: 0, tried: 0 };
    const enqueue = (targets: Target[]) => {
      for (const target of targets) {
        if (seenKeys.has(target.key)) continue;
        seenKeys.add(target.key);
        if (target.kind === 'tab') stats.tabs++;
        else if (target.kind === 'button') stats.buttons++;
        else stats.links++;
        if (target.kind === 'link' && visitedPaths.has(pathKeyOf(target.href))) {
          stats.knownLinksSkipped++; // destination already has its own screenshot from phase 1
          continue;
        }
        queue.push(target);
      }
      queue.sort((left, right) => rank[left.kind] - rank[right.kind]);
    };

    enqueue(await collectTargets(page, 'content'));
    console.log(`Explore ${new URL(parentUrl).pathname}: ${queue.length} to try (tabs=${stats.tabs}, buttons=${stats.buttons}, links=${stats.links}, links to already captured pages=${stats.knownLinksSkipped}).`);
    if (queue.length) {
      console.log(`Discovered targets: ${queue.slice(0, 60).map(target =>
        `[${target.kind}] ${target.label || '(unlabelled)'}${target.href ? ` -> ${target.href}` : ''}`).join(' | ')}`);
    }

    const pageDeadline = Date.now() + pageBudgetSeconds * 1000;
    let needsRestore = false;

    while (queue.length && stats.tried < maxTargetsPerPage && Date.now() < pageDeadline && !outOfBudget()) {
      const target = queue.shift()!;
      const clickKey = `${parentPath}|${target.key}`;
      if (attemptedClicks.has(clickKey)) continue;
      attemptedClicks.add(clickKey);
      if (target.kind === 'link' && (patternCounts.get(patternOf(target.href)) ?? 0) >= MAX_PER_ROUTE_PATTERN) continue;
      stats.tried++;

      try {
        let restoredThisRound = false;
        if (needsRestore) {
          await restore(page, tracker, parentUrl);
          needsRestore = false;
          restoredThisRound = true;
        }
        let clicked = await clickTarget(page, target, 'content');
        if (!clicked && !restoredThisRound) { // the DOM changed under us: reset once and retry
          await restore(page, tracker, parentUrl);
          clicked = await clickTarget(page, target, 'content');
        }
        if (!clicked) continue;
        clicks++;

        const ready = await waitReady(page, tracker, `after-click:${target.label}`, 90_000);
        await assertAuthenticated(page);

        const childUrl = new URL(page.url());
        childUrl.hash = '';
        if (childUrl.origin !== origin) {
          console.warn(`Blocked unsafe destination from ${target.label}: ${childUrl.href}`);
          needsRestore = true;
          continue;
        }

        const reason = `${target.kind}:${target.label}`;
        if (pathKeyOf(childUrl.href) !== parentPath) {
          needsRestore = true; // we left the parent page
          const childKey = pathKeyOf(childUrl.href);
          const pattern = patternOf(childUrl.href);
          if (visitedPaths.has(childKey)) continue;
          if ((patternCounts.get(pattern) ?? 0) >= MAX_PER_ROUTE_PATTERN) continue;
          const child = await captureCurrent(depth, reason, ready);
          if (child?.isNew) {
            stats.captured++;
            visitedPaths.add(childKey);
            patternCounts.set(pattern, (patternCounts.get(pattern) ?? 0) + 1);
            await explore(depth + 1, child.url.href);
          }
        } else {
          // Same page: a tab, filter, accordion or dialog changed what is on screen.
          const dialogOpen = await isDialogOpen();
          const child = await captureCurrent(depth, reason, ready);
          if (child?.isNew) {
            stats.captured++;
            if (!dialogOpen) enqueue(await collectTargets(page, 'content')); // new tabs/buttons revealed
          }
          await closeTransientUi(page); // close popover menus / dialogs so they do not swallow the next click
          if (await isDialogOpen()) needsRestore = true;
        }
      } catch (error) {
        if (error instanceof SessionLostError) throw error;
        needsRestore = true;
        console.warn(`Skipped target ${target.label}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }

    explored.push({
      url: parentUrl, tried: stats.tried, captured: stats.captured, knownLinksSkipped: stats.knownLinksSkipped,
      remaining: queue.length, tabs: stats.tabs, buttons: stats.buttons, links: stats.links,
    });
    if (queue.length) console.log(`Explore ${new URL(parentUrl).pathname}: stopped with ${queue.length} targets left (per-page cap or time limit).`);
  }

  const routes: Array<{ url: string; label: string }> = [];

  try {
    // Home / dashboard right after login.
    const home = await captureCurrent(0, 'initial-login', homeReady);
    if (home) {
      visitedPaths.add(pathKeyOf(home.url.href));
      routes.push({ url: home.url.href, label: 'home' });
    }

    // Phase 1: click every navigation link (all groups are expanded) and capture each page.
    const navTargets = (await collectTargets(page, 'nav')).filter(target => target.href);
    const seenHref = new Set<string>();
    const navRoutes = navTargets.filter(target => {
      const key = pathKeyOf(target.href);
      if (seenHref.has(key)) return false;
      seenHref.add(key);
      return true;
    });
    console.log(`Phase 1: ${navRoutes.length} safe click targets in the navigation menu.`);
    console.log(`Navigation menu: ${navRoutes.map(route => `${route.label} -> ${new URL(route.href).pathname}`).join(' | ')}`);

    for (const route of navRoutes) {
      if (outOfBudget()) break;
      const key = pathKeyOf(route.href);
      if (visitedPaths.has(key)) continue;
      try {
        await closeTransientUi(page);
        const clicked = await clickTarget(page, route, 'nav').catch(() => false);
        if (clicked) clicks++;
        else await navigateInApp(page, route.href); // group collapsed again, or link re-rendered

        const ready = await waitReady(page, tracker, `nav:${route.label}`, 90_000);
        await assertAuthenticated(page);
        const state = await captureCurrent(1, `nav:${route.label}`, ready);
        visitedPaths.add(key);
        if (state) routes.push({ url: state.url.href, label: route.label });
        await expandNavigation(page);
      } catch (error) {
        if (error instanceof SessionLostError) throw error;
        console.warn(`Skipped navigation target ${route.label}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }

    // Phase 2: tabs, in-page buttons and in-content links of every captured page.
    console.log(`Phase 2: exploring in-page interactions on ${routes.length} pages.`);
    for (const route of routes) {
      if (outOfBudget()) break;
      try {
        await restore(page, tracker, route.url);
        await explore(2, route.url);
      } catch (error) {
        if (error instanceof SessionLostError) throw error;
        console.warn(`Could not explore ${route.label}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
  } catch (error) {
    if (error instanceof SessionLostError) abortReason = error.message;
    else {
      abortReason = error instanceof Error ? error.message : String(error);
      await writeManifest('crashed');
      throw error;
    }
  }

  const stopReason = abortReason
    ? 'aborted'
    : Date.now() > deadline ? 'time-budget-reached'
      : visitedStates.size >= maxStates ? 'max-states-reached'
        : clicks >= maxClicks ? 'max-clicks-reached' : 'completed';
  await writeManifest(stopReason);
  console.log(`Crawl ${stopReason}: states=${visitedStates.size}, screenshots=${captured}, clicks=${clicks}, elapsed=${Math.round((Date.now() - startedAt) / 1000)}s.`);

  expect(abortReason, 'The crawl must not lose its login session').toBeUndefined();
  expect(visitedStates.size, 'The crawler must capture more than the first screen').toBeGreaterThan(1);
});
