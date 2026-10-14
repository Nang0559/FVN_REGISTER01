import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';

const username = process.env.PLAYWRIGHT_USERNAME?.trim() ?? '';
const password = process.env.PLAYWRIGHT_PASSWORD ?? '';
const screenshotDir = path.resolve('test-results/screenshots');
const maxClicks = Math.max(1, Number.parseInt(process.env.PLAYWRIGHT_MAX_CLICK_TARGETS ?? '100', 10) || 100);

const loadingSelector = [
  '[aria-busy="true"]', '[role="progressbar"]', '.loading', '.spinner', '.skeleton',
  '.mud-progress-circular', '.e-spinner-pane', '.ant-spin-spinning', '.el-loading-mask', '.ngx-spinner-overlay'
].join(', ');
const ignoredText = /logout|log-out|signout|sign-out|đăng\s*xuất|delete|remove|approve|reject|submit|save|lưu|xóa|xoá|duyệt|từ chối|gửi|download|export|tải xuống|hủy|huỷ|cancel/i;

function safeFileName(value: string): string {
  const cleaned = value.replace(/^https?:\/\/[^/]+/i, '').replace(/[?#].*$/, '')
    .replace(/[^a-zA-Z0-9._-]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 110);
  return (cleaned || 'screen') + '-' + createHash('sha1').update(value).digest('hex').slice(0, 8);
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
  if (!username || !password) throw new Error('Missing PLAYWRIGHT_USERNAME / PLAYWRIGHT_PASSWORD Actions secrets.');
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  const passwordInput = page.locator('input[type="password"]:visible').first();
  await expect(passwordInput).toBeVisible({ timeout: 20_000 });
  const userInput = page.locator([
    'input[autocomplete="username"]:visible', 'input[name*="user" i]:visible',
    'input[id*="user" i]:visible', 'input[name*="employee" i]:visible',
    'input[id*="employee" i]:visible', 'input[type="email"]:visible', 'input[type="text"]:visible'
  ].join(', ')).first();
  await expect(userInput).toBeVisible({ timeout: 10_000 });
  await userInput.fill(username);
  await passwordInput.fill(password);
  const submit = page.locator('button[type="submit"]:visible, input[type="submit"]:visible').first();
  if (await submit.count()) await submit.click(); else await passwordInput.press('Enter');
  await expect(page.locator('input[type="password"]:visible')).toHaveCount(0, { timeout: 60_000 });
}

async function waitReady(page: Page, label: string, timeoutMs = 60_000): Promise<void> {
  const started = Date.now();
  let previous = '';
  let stable = 0;
  while (Date.now() - started < timeoutMs) {
    const busy = await page.locator(loadingSelector).evaluateAll(elements => elements.filter(element => {
      const style = getComputedStyle(element); const rect = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden' && Number(style.opacity || '1') > 0 && rect.width > 0 && rect.height > 0;
    }).length).catch(() => 0);
    const signature = await page.locator('body').evaluate(element => {
      const text = (element.innerText || '').replace(/\s+/g, ' ').slice(0, 5000);
      return text + '|controls:' + element.querySelectorAll('a,button,[role="button"],[role="link"],[role="tab"]').length;
    }).catch(() => '');
    if (!busy && signature && signature === previous) stable++; else stable = 0;
    previous = signature;
    if (stable >= 3) return;
    await page.waitForTimeout(500);
  }
  console.warn(`Readiness timeout without visible loader: ${label}`);
  await page.waitForTimeout(1_000);
}

async function expandSafeMenus(page: Page): Promise<void> {
  const menuLabel = /^(menu|navigation|open menu|toggle menu|open navigation|toggle navigation|show navigation|sidebar|side bar|expand navigation|collapse navigation|điều hướng|danh mục|mở menu|mở rộng menu|thu gọn menu)$/i;
  const controls = page.locator('button:visible,[role="button"]:visible');
  const count = Math.min(await controls.count().catch(() => 0), 80);
  for (let i = 0; i < count; i++) {
    const control = controls.nth(i);
    const label = ((await control.getAttribute('aria-label').catch(() => null)) || (await control.getAttribute('title').catch(() => null)) || (await control.innerText().catch(() => ''))).trim();
    if (!menuLabel.test(label)) continue;
    await control.click({ timeout: 2_000 }).catch(() => undefined);
  }
}

async function getClickableTargets(page: Page): Promise<string[]> {
  return page.locator('a[href],button:visible,[role="button"]:visible,[role="link"]:visible,[onclick]').evaluateAll(elements => {
    const result: string[] = [];
    for (const element of elements) {
      const el = element as HTMLElement;
      if (!el.offsetParent) continue;
      if (el.closest('form')) continue;
      const text = ((el.getAttribute('aria-label') || el.getAttribute('title') || el.innerText || '')).replace(/\s+/g, ' ').trim();
      const href = el instanceof HTMLAnchorElement ? el.href : '';
      if (!text && !href) continue;
      if (/logout|log-out|signout|sign-out|đăng\s*xuất|delete|remove|approve|reject|submit|save|lưu|xóa|xoá|duyệt|từ chối|gửi|download|export|tải xuống|hủy|huỷ|cancel/i.test(text + ' ' + href)) continue;
      const rect = el.getBoundingClientRect();
      if (rect.width < 8 || rect.height < 8) continue;
      result.push(text || href);
      if (result.length >= 100) break;
    }
    return [...new Set(result)];
  });
}

async function clickTargetByLabel(page: Page, label: string): Promise<boolean> {
  const candidates = page.locator('a[href],button:visible,[role="button"]:visible,[role="link"]:visible,[onclick]');
  const count = Math.min(await candidates.count().catch(() => 0), 250);
  for (let i = 0; i < count; i++) {
    const candidate = candidates.nth(i);
    if (!(await candidate.isVisible().catch(() => false))) continue;
    if (await candidate.evaluate(el => !!el.closest('form')).catch(() => true)) continue;
    const text = ((await candidate.getAttribute('aria-label').catch(() => null)) || (await candidate.getAttribute('title').catch(() => null)) || (await candidate.innerText().catch(() => ''))).replace(/\s+/g, ' ').trim();
    const href = await candidate.getAttribute('href').catch(() => '') || '';
    if ((text || href) !== label) continue;
    if (ignoredText.test(text + ' ' + href)) continue;
    try {
      await candidate.scrollIntoViewIfNeeded({ timeout: 2_000 });
      await candidate.click({ timeout: 5_000 });
      return true;
    } catch { /* target may have moved after Blazor re-render */ }
  }
  return false;
}

test('captures read-only screens reached by clicking Home/Dashboard navigation and cards', async ({ page, baseURL }) => {
  test.setTimeout(30 * 60 * 1000);
  await mkdir(screenshotDir, { recursive: true });
  await signIn(page);
  await waitReady(page, 'dashboard-after-login', 90_000);
  const landingUrl = page.url();
  await saveScreenshot(page, 'click-capture-dashboard-home');

  const processed = new Set<string>();
  for (let round = 0; round < 3 && processed.size < maxClicks; round++) {
    await page.goto(landingUrl, { waitUntil: 'domcontentloaded', timeout: 45_000 });
    await waitReady(page, `landing-round-${round + 1}`);
    await expandSafeMenus(page);
    const targets = await getClickableTargets(page);
    console.log(`Round ${round + 1}: found ${targets.length} safe clickable navigation targets.`);

    for (const label of targets) {
      if (processed.has(label) || processed.size >= maxClicks || ignoredText.test(label)) continue;
      processed.add(label);
      await page.goto(landingUrl, { waitUntil: 'domcontentloaded', timeout: 45_000 }).catch(() => undefined);
      await waitReady(page, `before-click:${label}`, 60_000);
      await expandSafeMenus(page);
      const clicked = await clickTargetByLabel(page, label);
      if (!clicked) continue;
      await waitReady(page, `after-click:${label}`, 60_000);
      const route = new URL(page.url());
      route.hash = '';
      await saveScreenshot(page, `clicked-${label}-${route.pathname}${route.search}`);
      console.log(`Captured clicked target: ${label} -> ${route.pathname}${route.search}`);
    }
  }

  await expect(page.locator('body')).toBeVisible();
  console.log(`Captured ${processed.size} read-only navigation/card targets from the authenticated Home/Dashboard.`);
  expect(new URL(landingUrl).origin).toBe(new URL(baseURL!).origin);
});
