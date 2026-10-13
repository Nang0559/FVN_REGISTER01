import { expect, test } from '@playwright/test';

test('application root responds, renders a page, and saves a screenshot', async ({ page }) => {
  const response = await page.goto('/', { waitUntil: 'domcontentloaded' });

  expect(response, 'Expected the application root to return an HTTP response').not.toBeNull();
  expect(response!.status(), 'Application root should not return a server error').toBeLessThan(500);
  await expect(page.locator('body')).toBeVisible();

  // Always save a full-page screenshot, including when the smoke test passes.
  await page.screenshot({ path: 'test-results/homepage.png', fullPage: true });
});
