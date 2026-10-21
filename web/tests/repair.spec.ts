import { expect, test } from './fixtures';

test.use({ reducedMotion: 'reduce', viewport: { width: 1440, height: 900 } });

test('reviewed issue guidance and component detail', async ({ page }, testInfo) => {
  await page.route(/fonts\.googleapis\.com|fonts\.gstatic\.com/, route => route.abort());
  await page.goto('/');
  await page.addStyleTag({ content: '* { font-family: Arial, sans-serif !important; }' });
  await page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true }).click();
  await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
  await expect(page).toHaveScreenshot('wiper-guidance.png');
  await page.screenshot({ path: testInfo.outputPath('wiper-guidance.png') });
});

test('wiper check links observations to 3D, cleaning and verified outcome', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await page.getByRole('button', { name: 'Wipers leave streaks? Start a guided check', exact: true }).click();
  const guide = page.getByRole('complementary', { name: 'Wiper troubleshooting guide' });
  const canvas = page.locator('canvas');
  await expect(guide).toContainText('not a documented model-specific defect');
  await expect(canvas).toHaveAttribute('data-subject', 'wipers');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  expect((await canvas.getAttribute('data-items'))!.split(',')).toHaveLength(6);
  await page.screenshot({ path: testInfo.outputPath('wiper-guide.png') });
  await guide.getByRole('button', { name: 'Parked, switched off and safe to inspect', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'wiper-blade-left');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect(canvas).toHaveAttribute('data-items', 'wiper-blade-left,wiper-rubber-left');
  await expect(guide.getByRole('heading', { name: '2. Inspect the wiping edge' })).toBeFocused();
  await guide.getByRole('button', { name: 'No visible damage; dirt or residue possible', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'wiper-rubber-left');
  await page.screenshot({ path: testInfo.outputPath('wiper-contact-edge.png') });
  await guide.getByRole('button', { name: 'Glass and blades cleaned, arms lowered', exact: true }).click();
  await guide.getByRole('button', { name: 'Clear sweep, no streaks or chatter', exact: true }).click();
  await expect(guide.getByRole('heading', { name: 'Check passed for this observation' })).toBeVisible();
  await guide.getByText('Your observation trail (4)', { exact: true }).click();
  await expect(guide.locator('.repair-evidence ol')).toContainText('Glass and blades cleaned, arms lowered');
  await guide.getByRole('button', { name: 'Show windshield location on car', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await guide.getByRole('button', { name: 'Close troubleshooting', exact: true }).click();
  await expect(guide).toHaveCount(0);
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  expect(errors).toEqual([]);
});

test('damage and unresolved replacement lead to service, with reversible observations', async ({ page }) => {
  await page.goto('/?car=impala');
  await page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true }).click();
  const guide = page.getByRole('complementary', { name: 'Wiper troubleshooting guide' });
  await expect(guide).toContainText('Chevrolet Impala');
  await guide.getByRole('button', { name: 'Parked, switched off and safe to inspect', exact: true }).click();
  await guide.getByRole('button', { name: 'Rubber split, missing or blade loose', exact: true }).click();
  await expect(guide.getByRole('heading', { name: '5. Confirm fitment before replacement' })).toBeVisible();
  await guide.getByRole('button', { name: 'Correct blades fitted securely per instructions', exact: true }).click();
  await guide.getByRole('button', { name: 'Still streaking or operation abnormal', exact: true }).click();
  await expect(guide.getByRole('heading', { name: 'Further diagnosis or fitting is needed' })).toBeVisible();
  await guide.getByRole('button', { name: 'Previous observation', exact: true }).click();
  await expect(guide.getByRole('heading', { name: '6. Verify after replacement' })).toBeVisible();
  await guide.getByRole('button', { name: 'Restart check', exact: true }).click();
  await guide.getByRole('button', { name: 'Cannot safely inspect or see clearly to drive', exact: true }).click();
  await expect(guide.getByRole('heading', { name: 'Stop: safe access or visibility is not established' })).toBeVisible();
  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('raize');
  await expect(guide).toHaveCount(0);
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`guided check remains usable at ${viewport.width}x${viewport.height}`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await page.goto('/');
    await page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true }).click();
    const guide = page.getByRole('complementary', { name: 'Wiper troubleshooting guide' });
    await guide.getByRole('button', { name: 'Parked, switched off and safe to inspect', exact: true }).click();
    await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
    await guide.getByRole('button', { name: 'No visible damage; dirt or residue possible', exact: true }).click();
    await expect(guide.getByRole('heading', { name: '3. Clean before replacing' })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(viewport.width);
    await page.screenshot({ path: testInfo.outputPath(`wiper-mobile-${viewport.width}.png`) });
    await page.keyboard.press('Escape');
    await expect(guide).toHaveCount(0);
    await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'vehicle');
  });
}
