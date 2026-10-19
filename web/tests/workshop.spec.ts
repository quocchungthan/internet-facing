import { expect, test } from '@playwright/test';

test('car-first workshop supports selection, real photos, books and history', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Know your machine.' })).toBeVisible();
  await expect(page.locator('canvas')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Explore Cooling system', exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/workshop-entry.png' });
  await expect(page.getByRole('button', { name: 'Explore Engine', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  await page.getByRole('textbox', { name: 'Search components' }).fill('cooling');
  await page.getByRole('complementary', { name: 'Component index' }).getByRole('button', { name: /Cooling system/ }).click();
  await expect(page.getByRole('heading', { name: 'Cooling system', exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\?car=raize&part=cooling/);
  await page.getByRole('button', { name: 'Next lesson slide' }).click();
  await expect(page.getByRole('heading', { name: 'A thoughtful check' })).toBeVisible();
  await page.getByRole('button', { name: 'References', exact: false }).click();
  await expect(page.getByText('2021 Indonesian GR Sport reference', { exact: false })).toBeVisible();
  const photo = page.locator('.reference-photo img').first();
  await expect(photo).toBeVisible();
  await expect.poll(() => photo.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  await page.getByRole('button', { name: 'Field book', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Toyota Raize — a field book' })).toBeVisible();
  await page.getByRole('button', { name: 'Close part details' }).click();
  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('impala');
  await expect(page.getByRole('heading', { name: /Chevrolet Impala/ })).toBeVisible();
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  await page.getByRole('textbox', { name: 'Search components' }).fill('Powerglide');
  await page.getByRole('complementary', { name: 'Component index' }).getByRole('button', { name: /Powerglide & driveline/ }).click();
  await expect(page.getByRole('heading', { name: 'Powerglide & driveline', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.locator('.detail-panel')).toHaveCount(0);
  await page.getByRole('button', { name: 'Open field book', exact: true }).click();
  await expect(page.getByRole('heading', { name: '1967 Chevrolet Impala — a field book' })).toBeVisible();
  expect(errors).toEqual([]);
});

test('view controls, guided slides and schematic PNG capture work', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Explore Engine', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'X-ray', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Explore D-CVT', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Exploded', exact: true }).click();
  await page.getByRole('button', { name: 'Open hood and doors', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open hood and doors', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Rotate camera left', exact: true }).click();
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Capture schematic image', exact: true }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('30s-raize-schematic.png');
  await page.getByRole('button', { name: 'Take a guided look', exact: false }).click();
  await expect(page.getByText('GUIDED WALKTHROUGH')).toBeVisible();
  await page.getByRole('button', { name: 'Next tour part', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Oil & lubrication', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Close part details' }).click();
  await page.screenshot({ path: 'test-results/workshop-desktop.png' });
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`viewport stays fixed at ${viewport.width}x${viewport.height} with reduced motion`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/?car=impala&part=engine');
    await expect(page.getByRole('heading', { name: 'Small-block V8', exact: true })).toBeVisible();
    await expect(page.locator('canvas')).toBeVisible();
    const bounds = await page.evaluate(() => ({
      width: document.documentElement.scrollWidth,
      height: document.documentElement.scrollHeight,
      viewportWidth: window.innerWidth,
      viewportHeight: window.innerHeight,
    }));
    expect(bounds.width).toBe(bounds.viewportWidth);
    expect(bounds.height).toBe(bounds.viewportHeight);
    await page.getByRole('button', { name: 'Next lesson slide' }).click();
    await expect(page.getByRole('heading', { name: 'A thoughtful check' })).toBeVisible();
    await page.screenshot({ path: `test-results/workshop-${viewport.width}.png` });
  });
}

test('malformed shared state produces an explicit notice rather than a blank scene', async ({ page }) => {
  await page.goto('/?car=missing&part=unknown');
  await expect(page.locator('.notification')).toContainText('not found');
  await expect(page.getByRole('combobox', { name: 'Choose car model' })).toHaveValue('raize');
});
