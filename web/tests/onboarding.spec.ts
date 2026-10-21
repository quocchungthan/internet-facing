import { expect, test } from '@playwright/test';

const storageKey = '30s.onboarding.v1';

test('first visit introduction can be skipped, stays dismissed, resets with storage and replays', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  const intro = page.getByRole('dialog');
  await expect(intro).toBeVisible();
  await expect(intro.getByRole('heading', { name: 'Welcome to 30s' })).toBeFocused();
  await expect(intro.getByText('INTRODUCTION · 1 OF 4')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('intro-welcome.png') });

  await intro.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(intro.getByRole('heading', { name: 'Check a symptom safely' })).toBeFocused();
  await expect(page.locator('[data-onboarding="repair"]')).toHaveClass(/onboarding-target-active/);
  await intro.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(intro.getByRole('heading', { name: 'Welcome to 30s' })).toBeVisible();
  await expect(page.locator('.onboarding-target-active')).toHaveCount(0);

  await page.keyboard.press('Escape');
  await expect(intro).toHaveCount(0);
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe('dismissed');
  await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');

  await page.reload();
  await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
  await page.waitForTimeout(300);
  await expect(page.getByRole('dialog')).toHaveCount(0);

  await page.evaluate(() => localStorage.clear());
  await page.reload();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.getByRole('button', { name: 'Skip introduction' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);

  await page.getByRole('button', { name: 'Workshop help' }).click();
  await page.getByRole('button', { name: 'Replay introduction' }).click();
  const replay = page.getByRole('dialog');
  await expect(replay.getByRole('heading', { name: 'Welcome to 30s' })).toBeVisible();
  for (const title of ['Check a symptom safely', 'Explore the schematic', 'Learn how parts work']) {
    await replay.getByRole('button', { name: 'Next', exact: true }).click();
    await expect(replay.getByRole('heading', { name: title })).toBeVisible();
  }
  await page.screenshot({ path: testInfo.outputPath('intro-last-step.png') });
  await replay.getByRole('button', { name: 'Finish', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe('completed');
  expect(errors).toEqual([]);
});

test('blocked storage still lets the visitor close the introduction and explains it may reappear', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', { get() { throw new Error('denied'); } });
  });
  await page.goto('/');
  await page.getByRole('dialog').getByRole('button', { name: 'Skip', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('status').filter({ hasText: /introduction/i })).toBeVisible();
  await page.getByRole('button', { name: 'Dismiss introduction notice' }).click();
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`introduction fits ${viewport.width}x${viewport.height}`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await page.goto('/');
    const intro = page.getByRole('dialog');
    await expect(intro).toBeVisible();
    const box = (await intro.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(viewport.width);
    await page.screenshot({ path: testInfo.outputPath(`intro-${viewport.width}.png`) });
    await intro.getByRole('button', { name: 'Next', exact: true }).click();
    await expect(intro.getByRole('button', { name: 'Finish' }).or(intro.getByRole('button', { name: 'Next' }))).toBeVisible();
  });
}
