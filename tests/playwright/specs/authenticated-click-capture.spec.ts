import { expect, test, type Page, type Locator } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
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

  const userInput = page.locator([
    'input[autocomplete="username"]:visible',
    'input[name*="user" i]:visible',
    'input[id*="user" i]:visible',
    'input[name*="employee" i]:visible',
    'input[id*="employee" i]:visible',
    'input[type="email"]:visible',
    'input[type="text"]:visible',
  ].join(', ')).first();
  await expect(userInput).toBeVisible({ timeout: 10_000 });
  await userInput.fill(username);
  await passwordInput.fill(password);

  const submit = page.locator('button[type="submit"]:visible, input[type="submit"]:visible').first();
  if (await submit.count()) await submit.click();
  else await passwordInput.press('Enter');

  await expect(page.locator('input[type="password"]:visible')).toHaveCount(0, { timeout: 60_000 });
  if (await page.locator('input[type="password"]:visible').count()) {
    throw new Error('The login form is still visible after submitting.');
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

    if (!busy && networkQuiet && signature && signature === previousSignature) stableSamples++;
    else stableSamples = 0;
    previousSignature = signature;

    if (stableSamples >= 3) {
      console.log(`Page ready after ${Math.round(elapsed / 1000)}s: ${label}`);
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
  return page.locator('body').evaluate(element => {
    const normalize = (value: string) => value.replace(/\s+/g, ' ').trim();
    const text = normalize(element.innerText || '').slice(0, 8000);
    const controls = [...element.querySelectorAll('a,button,[role="button"],[role="link"],[role="tab"]')]
      .slice(0, 250)
      .map(control => normalize(
        control.getAttribute('aria-label') || control.getAttribute('title') || control.textContent || '',
      ).slice(0, 160));
    const headings = [...element.querySelectorAll('h1,h2,h3,[role="heading"]')]
      .map(heading => normalize(heading.textContent || '').slice(0, 120));
    return createHash('sha1')
      .update(`${text}|headings:${headings.join('|')}|controls:${controls.join('|')}`)
      .digest('hex');
  }).catch(() => '');
}

async function expandSafeMenus(page: Page): Promise<void> {
  const menuLabel = /^(menu|navigation|open menu|toggle menu|open navigation|toggle navigation|show navigation|sidebar|side bar|expand navigation|collapse navigation|điều hướng|danh mục|mở menu|mở rộng menu|thu gọn menu)$/i;
  const controls = page.locator('button:visible,[role="button"]:visible');
  const count = Math.min(await controls.count().catch(() => 0), 80);
  for (let i = 0; i < count; i++) {
    const control = controls.nth(i);
    const label = ((await control.getAttribute('aria-label').catch(() => null))
      || (await control.getAttribute('title').catch(() => null))
      || (await control.innerText().catch(() => ''))).trim();
    if (!menuLabel.test(label) || ignoredText.test(label)) continue;
    await control.click({ timeout: 2_000 }).catch(() => undefined);
  }
}

type ClickTarget = {
  key: string;
  label: string;
  href: string;
  index: number;
};

async function collectSafeClickTargets(page: Page): Promise<ClickTarget[]> {
  return page.locator('a[href],button:visible,[role="button"]:visible,[role="link"]:visible,[role="tab"]:visible').evaluateAll(elements => {
    const result: ClickTarget[] = [];
    const occurrence = new Map<string, number>();

    for (let index = 0; index < elements.length; index++) {
      const element = elements[index] as HTMLElement;
      if (!element.offsetParent || element.closest('form')) continue;
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
  const candidates = page.locator('a[href],button:visible,[role="button"]:visible,[role="link"]:visible,[role="tab"]:visible');
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
    const href = await candidate.getAttribute('href').catch(() => '') || '';
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
    await page.goto(target.href, { waitUntil: 'domcontentloaded', timeout: 45_000 });
  } else {
    // Reloading gives us a deterministic parent state after a child click.
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 45_000 });
  }
  await waitReady(page, `restore:${target.pathname}`, 60_000);
  await expandSafeMenus(page);
}

test('recursively captures authenticated read-only UI states', async ({ page, baseURL }) => {
  test.setTimeout(45 * 60 * 1000);
  await mkdir(screenshotDir, { recursive: true });

  const tracker = installRequestTracker(page);
  await signIn(page); // exactly one login for the whole crawl
  await waitReady(page, 'home-dashboard-after-login', 90_000);
  await expandSafeMenus(page);

  const origin = new URL(baseURL!).origin;
  const visitedStates = new Set<string>();
  const attemptedClicks = new Set<string>();
  let captured = 0;
  let clicks = 0;

  async function crawlCurrentScreen(depth: number, reason: string): Promise<void> {
    if (depth > maxDepth || visitedStates.size >= maxStates || clicks >= maxClicks) return;

    await waitReady(page, `depth=${depth}:${reason}`, 60_000);
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
    await saveScreenshot(page, `state-${captured}-depth-${depth}-${currentUrl.pathname}${currentUrl.search}`);
    console.log(`Captured state #${captured}: depth=${depth}, url=${currentUrl.pathname}${currentUrl.search}, signature=${domSignature}`);

    if (visitedStates.size >= maxStates || clicks >= maxClicks || depth >= maxDepth) return;

    const targets = await collectSafeClickTargets(page);
    console.log(`State #${captured}: ${targets.length} safe click targets.`);

    for (const target of targets) {
      if (visitedStates.size >= maxStates || clicks >= maxClicks) break;
      const clickKey = `${stateKey}|${target.key}`;
      if (attemptedClicks.has(clickKey)) continue;
      attemptedClicks.add(clickKey);

      // Always restore the exact parent URL before clicking the next target.
      await restore(page, currentUrl.href, tracker);
      const parentSignature = await getDomSignature(page);
      if (parentSignature !== domSignature) {
        console.warn(`Parent DOM changed during restore; skipping target ${target.label}.`);
        continue;
      }

      if (ignoredText.test(`${target.label} ${target.href}`)) continue;
      const clicked = await clickTarget(page, target);
      if (!clicked) continue;
      clicks++;

      try {
        await waitReady(page, `after-click:${target.label}`, 60_000);
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

  await page.screenshot({ path: path.join(screenshotDir, 'capture-index.png'), fullPage: true }).catch(() => undefined);
  await expect(page.locator('body')).toBeVisible();
  expect(visitedStates.size, 'At least the authenticated Home/Dashboard state should be visited').toBeGreaterThan(0);
  console.log(`Recursive crawl complete: states=${visitedStates.size}, screenshots=${captured}, clicks=${clicks}.`);
});
