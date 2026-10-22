import { expect, test } from './fixtures';

test.use({ viewport: { width: 1440, height: 900 }, reducedMotion: 'no-preference' });

test('bounds a decorative response without moving the camera or stealing clicks', async ({ page }, testInfo) => {
  await page.goto('/?car=raize&part=engine&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const light = page.locator('.workshop-pointer-light');
  const distance = await canvas.getAttribute('data-camera-distance');
  await page.mouse.move(700, 350);
  await expect(light).toHaveCSS('opacity', '1');
  const left = await light.evaluate(element => element.style.getPropertyValue('--pointer-light-x'));
  await page.mouse.move(950, 500);
  await expect.poll(() => light.evaluate(element => element.style.getPropertyValue('--pointer-light-x'))).not.toBe(left);
  const position = await light.evaluate(element => ['--pointer-light-x', '--pointer-light-y'].map(name => parseFloat(element.style.getPropertyValue(name))));
  for (const value of position) {
    expect(value).toBeGreaterThanOrEqual(45);
    expect(value).toBeLessThanOrEqual(55);
  }
  await expect(light).toHaveCSS('pointer-events', 'none');
  expect(await canvas.getAttribute('data-camera-distance')).toBe(distance);
  await page.screenshot({ path: testInfo.outputPath('pointer-light.png') });
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect.poll(async () => Number(await canvas.getAttribute('data-camera-distance'))).toBeLessThan(Number(distance));
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(light).toHaveCSS('display', 'none');
});

test('coarse touch input has no decorative response or hover requirement', async ({ browser, baseURL }) => {
  const context = await browser.newContext({ hasTouch: true, isMobile: true, viewport: { width: 375, height: 812 }, reducedMotion: 'no-preference' });
  await context.addInitScript(() => localStorage.setItem('30s.onboarding.v1', 'dismissed'));
  const page = await context.newPage();
  await page.goto(`${baseURL}/?car=raize&part=engine&isolate=1`);
  await expect(page.locator('.workshop-pointer-light')).toHaveCSS('display', 'none');
  await page.getByRole('button', { name: 'Show whole car', exact: true }).tap();
  await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'vehicle');
  await context.close();
});
