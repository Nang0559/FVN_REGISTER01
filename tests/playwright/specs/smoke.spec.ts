import { expect, test } from '@playwright/test';

test('application root responds and renders a page', async ({ page }) => {
  const response = await page.goto('/', { waitUntil: 'domcontentloaded' });

  expect(response, 'Expected the application root to return an HTTP response').not.toBeNull();
  expect(response!.status(), 'Application root should not return a server error').toBeLessThan(500);
  await expect(page.locator('body')).toBeVisible();
});
