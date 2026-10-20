import { expect, test } from '@playwright/test';

test.use({ reducedMotion: 'reduce', viewport: { width: 1440, height: 900 } });

test('isolates engine descendants, controls their bounds, drills down and restores history', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/?car=raize&part=engine');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Isolate selection', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const visible = (await canvas.getAttribute('data-items'))!.split(',').sort();
  expect(visible).toEqual(['engine-block', 'engine-cover', 'engine-head', 'intake', 'lubrication']);
  await expect(page.getByRole('button', { name: 'Explore Wheels & tires', exact: true })).toHaveCount(0);
  const before = Number(await canvas.getAttribute('data-camera-distance'));
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect.poll(async () => Number(await canvas.getAttribute('data-camera-distance'))).toBeLessThan(before);
  const target = await canvas.getAttribute('data-target');
  await page.getByRole('button', { name: 'Rotate camera left', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-target', target!);
  await page.getByRole('button', { name: 'Reset camera', exact: true }).click();
  await expect.poll(async () => Number(await canvas.getAttribute('data-camera-distance'))).toBeCloseTo(before, 1);
  await page.screenshot({ path: testInfo.outputPath('engine-isolated.png') });
  const radius = Number(await canvas.getAttribute('data-radius'));
  await page.getByRole('group', { name: 'Camera controls' }).getByRole('button', { name: 'Zoom out' }).click();
  await page.getByRole('region', { name: 'Selected subject controls' }).getByRole('button', { name: 'Engine head', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-head');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  expect(Number(await canvas.getAttribute('data-radius'))).toBeLessThan(radius);
  await expect(canvas).toHaveAttribute('data-items', 'engine-head');
  await page.getByRole('button', { name: 'Show whole car', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-head');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('impala');
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  expect(errors).toEqual([]);
});

test('component hierarchy selects a system and a distinct wheel occurrence', async ({ page }, testInfo) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  await page.getByText('Car / assembly / part hierarchy', { exact: true }).click();
  await page.getByRole('complementary', { name: 'Component index' }).getByRole('button', { name: 'Chassis system', exact: true }).click();
  await page.getByRole('button', { name: 'Isolate selection', exact: true }).click();
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  expect((await canvas.getAttribute('data-items'))!.split(',')).toHaveLength(12);
  await page.goto('/?car=impala&part=wheels&item=wheel-front-left&isolate=1');
  await expect(canvas).toHaveAttribute('data-subject', 'wheel-front-left');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect(canvas).toHaveAttribute('data-items', 'wheel-front-left');
  await expect(page.getByRole('region', { name: 'Selected subject controls' })).toContainText('Wheel and tire (front left)');
  expect(Number(await canvas.getAttribute('data-camera-distance'))).toBeLessThan(3.5);
  await page.screenshot({ path: testInfo.outputPath('single-wheel.png') });
});

test('clicking the isolated 3D engine picks a drawable child', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await canvas.click({ position: { x: 720, y: 450 } });
  await expect.poll(() => canvas.getAttribute('data-subject')).not.toBe('engine');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const item = await canvas.getAttribute('data-subject');
  expect(['engine-block', 'engine-head', 'engine-cover', 'intake', 'lubrication']).toContain(item);
  await expect(canvas).toHaveAttribute('data-items', item!);
});

test('field book exits isolation so its learning panel remains reachable', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&isolate=1');
  await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Open field book', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Toyota Raize — a field book' })).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-isolated', 'false');
});
