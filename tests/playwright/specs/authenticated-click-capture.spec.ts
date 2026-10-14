import { expect, test, type Page } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';

const username = process.env.PLAYWRIGHT_USERNAME?.trim() ?? '';
const password = process.env.PLAYWRIGHT_PASSWORD ?? '';
const screenshotDir = path.resolve('test-results/screenshots');
const maxStates = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_STATES ?? '180', 10) || 180);
const maxClicks = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_CLICK_TARGETS ?? '500', 10) || 500);
const maxDepth = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_DEPTH ?? '12', 10) || 12);

const loadingSelector = [
  '[aria-busy="true"]',
  '[role="progressbar"]',
  '.loading',
  '.spinner',
  '.skeleton',
  '.mud-progress-circular',
  '.e-spinner-pane',
  '.ant-spin-spinning',
  '.el-loading-mask',
  '.ngx-spinner-overlay',
].join(', ');

// Read-only policy: anything that normally writes, confirms, deletes, sends, downloads,
// logs out, or starts a business operation is never clicked.
const ignoredText = /logout|log-out|signout|sign-out|đăng\s*xuất|delete|remove|approve|reject|submit|save|lưu|xóa|xoá|duyệt|từ chối|gửi|download|export|tải xuống|hủy|huỷ|cancel|create|new|add|edit|update|assign|grant|revoke|confirm|publish|execute|start|stop|reset|refresh|sync|import|upload|tạo|thêm|sửa|cập nhật|phân công|cấp quyền|thu hồi|xác nhận|đồng bộ|nhập|tải lên|thực hiện/i;

function safeFileName(value: string): string {
  const cleaned = value
    .replace(/^https?:\/\/[^/]+/i, '')
    .replace(/[?#].*$/, '')
    .replace(/[^a-zA-Z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 110);
  return `${cleaned || 'screen'}-${createHash('sha1').update(value).digest('hex').slice(0, 8)}`;
}

async function saveScreenshot(page: Page, name: string): Promise<void> {
  await page.screenshot({
    path: path.join(screenshotDir, `${safeFileName(name)}.png`),
    fullPage: true,
    animations: 'disabled',
    timeout: 30_000,
  });
}

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
    if (passwordVisible === 0 && !/\/login(?:\/|$)/i.test(new URL(page.url()).pathname)) break;
    await page.waitForTimeout(500);
  }
  const stillOnLogin = /\/login(?:\/|$)/i.test(new URL(page.url()).pathname)
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

function installRequestTracker(page: Page) {
  const pending = new Set<import('@playwright/test').Request>();
  let lastActivity = Date.now();
  const ignoredRequest = /signalr|\/negotiate(?:\?|$)|telemetry|analytics|hotjar|clarity|\/collect(?:\?|$)/i;
  const trackable = (request: import('@playwright/test').Request) =>
    ['fetch', 'xhr'].includes(request.resourceType()) && !ignoredRequest.test(request.url());

  page.on('request', request => {
    if (trackable(request)) {
      pending.add(request);
      lastActivity = Date.now();
    }
  });
  const finish = (request: import('@playwright/test').Request) => {
    if (pending.delete(request)) lastActivity = Date.now();
  };
  page.on('requestfinished', finish);
  page.on('requestfailed', finish);

  return {
    pending,
    get lastActivity() { return lastActivity; },
  };
}

async function waitReady(
  page: Page,
  tracker: ReturnType<typeof installRequestTracker>,
  label: string,
  timeoutMs = 60_000,
): Promise<void> {
  const started = Date.now();
  let previousSignature = '';
  let stableSamples = 0;
  let lastLog = 0;
  let idleSince = 0;

  await page.waitForLoadState('domcontentloaded').catch(() => undefined);
  await page.locator('body').waitFor({ state: 'visible', timeout: 15_000 });

  while (Date.now() - started < timeoutMs) {
    const elapsed = Date.now() - started;
    const busy = await page.locator(loadingSelector).evaluateAll(elements => elements.filter(element => {
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden'
        && Number(style.opacity || '1') > 0 && rect.width > 0 && rect.height > 0;
    }).length).catch(() => 0);

    const signature = await getDomSignature(page);
    const networkQuiet = tracker.pending.size === 0 && Date.now() - tracker.lastActivity >= 1_000;

    if (!busy && tracker.pending.size === 0) {
      if (!idleSince) idleSince = Date.now();
    } else {
      idleSince = 0;
    }
    if (!busy && networkQuiet && signature && signature === previousSignature) stableSamples++;
    else stableSamples = 0;
    previousSignature = signature;

    if (stableSamples >= 3) {
      console.log(`Page ready after ${Math.round(elapsed / 1000)}s: ${label}`);
      return;
    }
    // A live Blazor page can mutate continuously despite being usable.
    if (idleSince && Date.now() - idleSince >= 5_000) {
      console.log(`Page idle; continuing capture after ${Math.round(elapsed / 1000)}s: ${label}`);
      return;
    }

    if (elapsed - lastLog >= 10_000) {
      lastLog = elapsed;
      console.log(`Waiting: ${label}; elapsed=${Math.round(elapsed / 1000)}s, loaders=${busy}, pending=${tracker.pending.size}`);
    }
    await page.waitForTimeout(400);
  }

  const busy = await page.locator(loadingSelector).evaluateAll(elements => elements.filter(element => {
    const style = getComputedStyle(element);
    const rect = element.getBoundingClientRect();
    return style.display !== 'none' && style.visibility !== 'hidden'
      && Number(style.opacity || '1') > 0 && rect.width > 0 && rect.height > 0;
  }).length).catch(() => 0);

  if (busy > 0 || tracker.pending.size > 0) {
    throw new Error(`Page did not become ready in ${Math.round(timeoutMs / 1000)}s; loaders=${busy}, pending=${tracker.pending.size}.`);
  }
  await page.waitForTimeout(1_000);
  console.warn(`Readiness timeout with no active loader/request: ${label}`);
}

async function getDomSignature(page: Page): Promise<string> {
  const raw = await page.locator('body').evaluate(element => {
    const normalize = (value: string) => value.replace(/\\s+/g, ' ').trim();
    const text = normalize(element.innerText || '').slice(0, 8000);
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
async function expandSafeMenus(page: Page): Promise<void> {
  // MudBlazor's navigation toggle commonly uses labels such as
  // "Toggle navigation menu"; matching the entire label missed it before.
  const menuLabel = /menu|navigation|sidebar|side bar|điều hướng|danh mục|mở rộng|thu gọn/i;
  const controls = page.locator(
    'button:visible,[role="button"]:visible,.mud-icon-button:visible,[aria-controls]:visible',
  );
  const count = Math.min(await controls.count().catch(() => 0), 100);
  for (let i = 0; i < count; i++) {
    const control = controls.nth(i);
    const label = ([
      await control.getAttribute('aria-label').catch(() => null),
      await control.getAttribute('title').catch(() => null),
      await control.getAttribute('data-testid').catch(() => null),
      await control.innerText().catch(() => ''),
    ].filter(Boolean).join(' ')).trim();
    if (!label || !menuLabel.test(label) || ignoredText.test(label)) continue;
    await control.click({ timeout: 2_000 }).catch(() => undefined);
    await page.waitForTimeout(150);
  }
}

type ClickTarget = {
  key: string;
  label: string;
  href: string;
  index: number;
};

async function collectSafeClickTargets(page: Page): Promise<ClickTarget[]> {
  return page.locator('a[href],.mud-nav-link:visible,.mud-tab:visible,button:visible,[role="button"]:visible,[role="link"]:visible,[role="tab"]:visible').evaluateAll(elements => {
    const result: ClickTarget[] = [];
    const occurrence = new Map<string, number>();

    for (let index = 0; index < elements.length; index++) {
      const element = elements[index] as HTMLElement;
      if (element.getClientRects().length === 0 || element.closest('form')) continue;
      if (element instanceof HTMLButtonElement && element.type.toLowerCase() === 'submit') continue;
      if (element.getAttribute('aria-disabled') === 'true' || element.hasAttribute('disabled')) continue;

      const label = (element.getAttribute('aria-label') || element.getAttribute('title') || element.innerText || element.textContent || '')
        .replace(/\s+/g, ' ').trim().slice(0, 180);
      const href = element instanceof HTMLAnchorElement ? element.href : '';
      if (!label && !href) continue;
      if (/logout|log-out|signout|sign-out|đăng\s*xuất|delete|remove|approve|reject|submit|save|lưu|xóa|xoá|duyệt|từ chối|gửi|download|export|tải xuống|hủy|huỷ|cancel|create|new|add|edit|update|assign|grant|revoke|confirm|publish|execute|start|stop|reset|refresh|sync|import|upload|tạo|thêm|sửa|cập nhật|phân công|cấp quyền|thu hồi|xác nhận|đồng bộ|nhập|tải lên|thực hiện/i.test(`${label} ${href}`)) continue;

      const rect = element.getBoundingClientRect();
      if (rect.width < 8 || rect.height < 8) continue;

      const identity = `${element.tagName}|${label}|${href}`;
      const occurrenceIndex = occurrence.get(identity) ?? 0;
      occurrence.set(identity, occurrenceIndex + 1);
      result.push({ key: `${identity}|${occurrenceIndex}`, label, href, index });
      if (result.length >= 250) break;
    }
    return result;
  });
}

async function clickTarget(page: Page, target: ClickTarget): Promise<boolean> {
  const candidates = page.locator('a[href],.mud-nav-link:visible,.mud-tab:visible,button:visible,[role="button"]:visible,[role="link"]:visible,[role="tab"]:visible');
  const count = Math.min(await candidates.count().catch(() => 0), 300);
  let occurrence = 0;

  for (let index = 0; index < count; index++) {
    const candidate = candidates.nth(index);
    if (!(await candidate.isVisible().catch(() => false))) continue;
    if (await candidate.evaluate(element => !!element.closest('form')).catch(() => true)) continue;
    if (await candidate.getAttribute('aria-disabled').catch(() => null) === 'true') continue;
    if (await candidate.getAttribute('disabled').catch(() => null) !== null) continue;
    if ((await candidate.getAttribute('type').catch(() => '') || '').toLowerCase() === 'submit') continue;

    const label = ((await candidate.getAttribute('aria-label').catch(() => null))
      || (await candidate.getAttribute('title').catch(() => null))
      || (await candidate.innerText().catch(() => ''))
      || (await candidate.textContent().catch(() => ''))).replace(/\s+/g, ' ').trim().slice(0, 180);
    const href = await candidate.evaluate(element =>
      element instanceof HTMLAnchorElement ? element.href : (element.getAttribute('href') || ''),
    ).catch(() => '');
    const identity = `${await candidate.evaluate(element => element.tagName)}|${label}|${href}`;
    if (`${identity}|${occurrence}` !== target.key) {
      if (identity === target.key.split('|').slice(0, -1).join('|')) occurrence++;
      continue;
    }

    if (ignoredText.test(`${label} ${href}`)) return false;
    await candidate.scrollIntoViewIfNeeded({ timeout: 3_000 });
    await candidate.click({ timeout: 5_000 });
    return true;
  }
  return false;
}

async function restore(page: Page, url: string, tracker: ReturnType<typeof installRequestTracker>): Promise<void> {
  const current = new URL(page.url());
  const target = new URL(url);
  current.hash = '';
  target.hash = '';
  if (current.href !== target.href) {
    // Preserve the SPA and its open navigation drawer whenever possible.
    await page.goto(target.href, { waitUntil: 'domcontentloaded', timeout: 45_000 });
    await waitReady(page, tracker, `restore:${target.pathname}`, 30_000);
  }
  // Do not reload when already on the parent route: reloading collapses
  // MudBlazor drawers and loses dynamically rendered navigation targets.
  await expandSafeMenus(page);
}

test('recursively captures authenticated read-only UI states', async ({ page, baseURL }) => {
  test.setTimeout(45 * 60 * 1000);
  await mkdir(screenshotDir, { recursive: true });

  const tracker = installRequestTracker(page);
  try {
    await signIn(page); // exactly one login for the whole crawl
  } catch (error) {
    const diagnostic = {
      baseUrl: process.env.PLAYWRIGHT_BASE_URL ?? 'configured base URL',
      generatedAt: new Date().toISOString(),
      status: 'login-failed',
      url: page.url(),
      error: error instanceof Error ? error.message : String(error),
    };
    await page.screenshot({ path: path.join(screenshotDir, 'failure-current-page.png'), fullPage: true, timeout: 15_000 }).catch(() => undefined);
    await writeFile(path.join(screenshotDir, 'manifest.json'), JSON.stringify({
      summary: { states: 0, screenshots: 1, clicks: 0 },
      diagnostics: diagnostic,
      states: [],
    }, null, 2), 'utf8').catch(() => undefined);
    throw error;
  }
  await waitReady(page, tracker, 'home-dashboard-after-login', 30_000);
  await expandSafeMenus(page);

  const origin = new URL(baseURL!).origin;
  const visitedStates = new Set<string>();
  const attemptedClicks = new Set<string>();
  const capturedStates: Array<{ number: number; depth: number; reason: string; url: string; signature: string; screenshot: string }> = [];
  let captured = 0;
  let clicks = 0;

  async function crawlCurrentScreen(depth: number, reason: string): Promise<void> {
    if (depth > maxDepth || visitedStates.size >= maxStates || clicks >= maxClicks) return;

    await waitReady(page, tracker, `depth=${depth}:${reason}`, 60_000);
    await expandSafeMenus(page);

    const currentUrl = new URL(page.url());
    currentUrl.hash = '';
    if (currentUrl.origin !== origin || /\/login(?:\/|$)/i.test(currentUrl.pathname)) return;

    const domSignature = await getDomSignature(page);
    if (!domSignature) return;
    const stateKey = `${currentUrl.href}|${domSignature}`;
    if (visitedStates.has(stateKey)) return;
    visitedStates.add(stateKey);

    captured++;
    const screenshotName = `state-${captured}-depth-${depth}-${currentUrl.pathname}${currentUrl.search}`;
    await saveScreenshot(page, screenshotName);
    capturedStates.push({
      number: captured,
      depth,
      reason,
      url: currentUrl.href,
      signature: domSignature,
      screenshot: `${safeFileName(screenshotName)}.png`,
    });
    console.log(`Captured state #${captured}: depth=${depth}, url=${currentUrl.pathname}${currentUrl.search}, signature=${domSignature}`);

    if (visitedStates.size >= maxStates || clicks >= maxClicks || depth >= maxDepth) return;

    const targets = await collectSafeClickTargets(page);
    console.log(`State #${captured}: ${targets.length} safe click targets.`);
    if (targets.length) {
      console.log(`Discovered targets: ${targets.slice(0, 80).map(target =>
        `${target.label || '(unlabelled)'}${target.href ? ` -> ${target.href}` : ''}`,
      ).join(' | ')}`);
    } else {
      const diagnostics = await page.locator('a[href],button:visible,[role="button"]:visible,[role="link"]:visible,[role="tab"]:visible')
        .evaluateAll(elements => elements.slice(0, 100).map(element => ({
          tag: element.tagName,
          text: ((element as HTMLElement).innerText || element.textContent || '').replace(/\\s+/g, ' ').trim().slice(0, 100),
          href: element instanceof HTMLAnchorElement ? element.href : '',
          ariaLabel: element.getAttribute('aria-label'),
          visible: element.getClientRects().length > 0,
        })));
      console.warn(`No safe targets on state #${captured}. Visible candidate diagnostics: ${JSON.stringify(diagnostics)}`);
    }

    for (const target of targets) {
      if (visitedStates.size >= maxStates || clicks >= maxClicks) break;
      const clickKey = `${stateKey}|${target.key}`;
      if (attemptedClicks.has(clickKey)) continue;
      attemptedClicks.add(clickKey);

      // Always restore the exact parent URL before clicking the next target.
      await restore(page, currentUrl.href, tracker);
      // The menu may have expanded since discovery. Resolve targets again by
      // semantic identity instead of rejecting the whole parent on DOM changes.
      if (ignoredText.test(`${target.label} ${target.href}`)) continue;
      const clicked = await clickTarget(page, target);
      if (!clicked) continue;
      clicks++;

      try {
        await waitReady(page, tracker, `after-click:${target.label}`, 60_000);
        const childUrl = new URL(page.url());
        childUrl.hash = '';
        if (childUrl.origin !== origin || /\/login(?:\/|$)/i.test(childUrl.pathname)) {
          console.warn(`Blocked unsafe/non-authenticated destination from ${target.label}: ${childUrl.href}`);
        } else {
          console.log(`Following read-only target: ${target.label} -> ${childUrl.pathname}${childUrl.search}`);
          await crawlCurrentScreen(depth + 1, target.label);
        }
      } catch (error) {
        console.warn(`Skipped target ${target.label}: ${error instanceof Error ? error.message : String(error)}`);
      } finally {
        // Step 8: return to the parent state before continuing with its next target.
        await restore(page, currentUrl.href, tracker).catch(error => {
          console.warn(`Could not restore parent after ${target.label}: ${error instanceof Error ? error.message : String(error)}`);
        });
      }
    }
  }

  await crawlCurrentScreen(0, 'initial-login');

  await writeFile(path.join(screenshotDir, 'manifest.json'), JSON.stringify({
    baseUrl: origin,
    generatedAt: new Date().toISOString(),
    limits: { maxStates, maxClicks, maxDepth },
    summary: { states: visitedStates.size, screenshots: captured, clicks },
    states: capturedStates,
  }, null, 2), 'utf8');
  await page.screenshot({ path: path.join(screenshotDir, 'capture-index.png'), fullPage: true }).catch(() => undefined);
  await expect(page.locator('body')).toBeVisible();
  expect(visitedStates.size, 'At least the authenticated Home/Dashboard state should be visited').toBeGreaterThan(0);
  console.log(`Recursive crawl complete: states=${visitedStates.size}, screenshots=${captured}, clicks=${clicks}.`);
});
