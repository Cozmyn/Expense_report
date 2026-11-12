import { expect, test } from '@playwright/test';

test.describe('Wave 1 accessibility and narrow viewport smoke checks', () => {
  test('login remains readable and focusable at 320px', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Autentificare' })).toBeVisible();
    const username = page.getByLabel('Utilizator', { exact: true });
    const password = page.getByLabel('Parolă', { exact: true });
    await username.focus();
    await expect(username).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(password).toBeFocused();
    const width = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(width).toBeLessThanOrEqual(320);
  });

  test('language switch remains accessible on a narrow screen', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Autentificare' })).toBeVisible();
    const language = page.getByRole('button', { name: /English|Română|RO|EN/ }).first();
    await expect(language).toBeVisible();
    await language.click();
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    const width = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(width).toBeLessThanOrEqual(390);
  });
});
