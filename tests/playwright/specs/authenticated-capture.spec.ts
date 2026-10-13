import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';

const username = process.env.PLAYWRIGHT_USERNAME?.trim() ?? '';
const password = process.env.PLAYWRIGHT_PASSWORD ?? '';
const screenshotDir = path.resolve('test-results/screenshots');
const maxPages = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_PAGES ?? '180', 10) || 180);

function safeFileName(value: string): string {
  const cleaned = value
    .replace(/^https?:\/\/[^/]+/i, '')
    .replace(/[?#].*$/, '')
    .replace(/[^a-zA-Z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 100);
  const digest = createHash('sha1').update(value).digest('hex').slice(0, 8);
  return (cleaned || 'home') + '-' + digest;
}

async function saveScreenshot(page: Page, route: string): Promise<void> {
  await page.screenshot({
    path: path.join(screenshotDir, `${safeFileName(route)}.png`),
    fullPage: true,
    animations: 'disabled',
    timeout: 30_000,
  });
}

async function findLoginControls(page: Page) {
  const passwordInput = page.locator('input[type="password"]:visible').first();
  await expect(passwordInput, 'Login page should contain a visible password field').toBeVisible({ timeout: 15_000 });

  const userSelectors = [
    'input[autocomplete="username"]:visible',
    'input[name*="user" i]:visible',
    'input[id*="user" i]:visible',
    'input[name*="employee" i]:visible',
    'input[id*="employee" i]:visible',
    'input[name*="account" i]:visible',
    'input[id*="account" i]:visible',
    'input[type="email"]:visible',
    'input[type="text"]:visible',
  ];

  let usernameInput = page.locator(userSelectors[0]).first();
  let found = false;
  for (const selector of userSelectors) {
    const candidate = page.locator(selector).first();
    if (await candidate.count() && await candidate.isVisible().catch(() => false)) {
      usernameInput = candidate;
      found = true;
      break;
    }
  }

  if (!found) {
    const visibleInputs = page.locator('input:visible').filter({ hasNot: page.locator('[type="password"]') });
    const count = await visibleInputs.count();
    if (count > 1) usernameInput = visibleInputs.first();
    else throw new Error('Could not identify the username/employee-code input on the login page.');
  }

  return { usernameInput, passwordInput };
}

async function signIn(page: Page): Promise<void> {
  if (!username || !password) {
    throw new Error(
      'Login secrets are missing. Add repository Actions secrets PLAYWRIGHT_USERNAME and PLAYWRIGHT_PASSWORD (or TEST_USERNAME/TEST_PASSWORD or USERNAME/PASSWORD).',
    );
  }

  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await saveScreenshot(page, 'login-page');

  const { usernameInput, passwordInput } = await findLoginControls(page);
  await usernameInput.fill(username);
  await passwordInput.fill(password);

  const submitCandidates = [
    page.locator('button[type="submit"]:visible').first(),
    page.locator('input[type="submit"]:visible').first(),
    page.getByRole('button', { name: /đăng nhập|login|sign in|log in/i }).first(),
  ];

  let submitted = false;
  for (const candidate of submitCandidates) {
    if (await candidate.count() && await candidate.isVisible().catch(() => false)) {
      await candidate.click();
      submitted = true;
      break;
    }
  }
  if (!submitted) {
    await passwordInput.press('Enter');
  }

  await page.waitForLoadState('domcontentloaded').catch(() => undefined);
  await page.waitForTimeout(1_500);

  // Never continue crawling if the login form is still visible.
  const stillHasPassword = await page.locator('input[type="password"]:visible').count();
  if (stillHasPassword) {
    const bodyText = (await page.locator('body').innerText().catch(() => '')).slice(0, 1500);
    const hasLoginError = /mật khẩu|đăng nhập thất bại|sai thông tin|invalid credentials|incorrect password|login failed/i.test(bodyText);
    throw new Error(
      hasLoginError
        ? 'The website rejected the test login. Check the Actions secrets and test-account status.'
        : 'The login form is still visible after submitting. The login selectors or sign-in flow may need adjustment.',
    );
  }
}

async function expandNavigation(page: Page): Promise<void> {
  const safeLabels = /^(menu|navigation|open menu|toggle menu|sidebar|side bar|expand navigation|collapse navigation|điều hướng|danh mục|mở menu|mở rộng menu|thu gọn menu)$/i;
  const controls = page.locator('button:visible, [role="button"]:visible');
  const count = Math.min(await controls.count().catch(() => 0), 100);
  for (let i = 0; i < count; i++) {
    const control = controls.nth(i);
    const label = ((await control.getAttribute('aria-label').catch(() => null))
      || (await control.getAttribute('title').catch(() => null))
      || (await control.innerText().catch(() => ''))).trim();
    if (!safeLabels.test(label)) continue;
    try {
      await control.click({ timeout: 1_500 });
      await page.waitForTimeout(150);
    } catch {
      // A hidden/disabled navigation toggle should not stop the crawl.
    }
  }
}

async function collectInternalLinks(page: Page, baseOrigin: string): Promise<string[]> {
  return page.locator('a[href]').evaluateAll((anchors, origin) => {
    const ignored = /logout|log-out|signout|sign-out|đăng-xuất|dang-xuat|delete|remove|approve|reject|submit|download|export/i;
    const urls = new Set<string>();

    for (const anchor of anchors) {
      const element = anchor as HTMLAnchorElement;
      const href = element.href;
      const text = (element.innerText || element.getAttribute('aria-label') || element.getAttribute('title') || '').trim();
      if (!href || ignored.test(text) || ignored.test(href)) continue;

      try {
        const url = new URL(href, origin);
        if (url.origin !== origin || !/^https?:$/.test(url.protocol)) continue;
        if (url.pathname.endsWith('.pdf') || /\.(csv|xlsx?|docx?|zip)$/i.test(url.pathname)) continue;
        url.hash = '';
        urls.add(url.href);
      } catch {
        // Ignore malformed or non-navigation href values.
      }
    }

    return [...urls];
  }, baseOrigin);
}

test('signs in with Actions secrets and captures accessible application pages', async ({ page, baseURL }) => {
  test.setTimeout(25 * 60 * 1000);
  await mkdir(screenshotDir, { recursive: true });

  await signIn(page);
  await expect(page.locator('body')).toBeVisible();
  await saveScreenshot(page, 'dashboard-after-login');

  const origin = new URL(baseURL!).origin;
  const queue = [page.url()];
  const visited = new Set<string>();

  while (queue.length && visited.size < Math.max(1, maxPages)) {
    const url = queue.shift()!;
    const normalized = new URL(url);
    normalized.hash = '';
    const target = normalized.href;

    if (visited.has(target) || normalized.origin !== origin) continue;
    visited.add(target);

    try {
      const response = await page.goto(target, { waitUntil: 'domcontentloaded', timeout: 25_000 });
      if (!response || response.status() >= 500) continue;
      await page.locator('body').waitFor({ state: 'visible', timeout: 10_000 });
      await page.waitForTimeout(700);

      // Keep the crawler read-only: only expand navigation controls and select semantic tabs.
      const route = normalized.pathname + normalized.search;
      await expandNavigation(page);
      await saveScreenshot(page, route);

      // Capture each tab panel separately; tabs are view switches, not business submissions.
      const tabs = page.getByRole('tab');
      const tabCount = Math.min(await tabs.count().catch(() => 0), 30);
      for (let tabIndex = 0; tabIndex < tabCount; tabIndex++) {
        const tab = tabs.nth(tabIndex);
        if (!(await tab.isVisible().catch(() => false))) continue;
        const tabName = (await tab.getAttribute('aria-label').catch(() => null))
          || (await tab.innerText().catch(() => ''))
          || `tab-${tabIndex + 1}`;
        try {
          await tab.click({ timeout: 2_000 });
          await page.waitForTimeout(200);
          await saveScreenshot(page, `${route}--tab-${tabIndex + 1}-${tabName}`);
        } catch (error) {
          console.warn(`Skipped tab on ${normalized.pathname}: ${error instanceof Error ? error.message : String(error)}`);
        }
      }

      const links = await collectInternalLinks(page, origin);
      for (const link of links) {
        if (!visited.has(link) && queue.length + visited.size < Math.max(1, maxPages) * 3) {
          queue.push(link);
        }
      }
    } catch (error) {
      console.warn(`Skipped page ${normalized.pathname}: ${error instanceof Error ? error.message : String(error)}`);
    }
  }

  await page.screenshot({
    path: path.join(screenshotDir, 'capture-index.png'),
    fullPage: true,
  }).catch(() => undefined);

  expect(visited.size, 'At least the signed-in landing page should have been visited').toBeGreaterThan(0);
  console.log(`Captured ${visited.size} internal page URLs and semantic tab states. Screenshot files are in test-results/screenshots/.`);
  if (visited.size >= maxPages) console.warn(`Reached PLAYWRIGHT_MAX_PAGES=${maxPages}; raise the workflow input/environment value to crawl more routes.`);
});
