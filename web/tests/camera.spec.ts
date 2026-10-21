import { expect, test, type Locator, type Page } from './fixtures';

test.use({ reducedMotion: 'no-preference', viewport: { width: 1440, height: 900 } });

async function preservesZoomWhileOrbiting(
  page: Page, canvas: Locator, zoom: 'Zoom in' | 'Zoom out', target: 'body' | 'engine',
  pendingInitialFit = false,
) {
  if (!pendingInitialFit) {
    await expect(canvas).toHaveAttribute('data-ready', 'true');
    await expect(canvas).toHaveAttribute('data-focusable', 'true');
  }
  const bounds = await canvas.boundingBox();
  if (!bounds) throw new Error('The 3D canvas must have visible bounds to exercise mouse controls.');
  const x = bounds.x + bounds.width * 0.5;
  const y = bounds.y + bounds.height * (target === 'body' ? 0.35 : 0.5);
  await page.mouse.move(x, y);
  const hoveredItem = page.locator('.part-tooltip strong');
  if (target === 'body') await expect(hoveredItem).toHaveText('Body & access');
  else await expect(hoveredItem).toHaveText(/.+/);
  const hitState = await page.evaluate(({ x: pointX, y: pointY }) => {
    const canvas = document.querySelector('canvas');
    return {
      hitCanvas: document.elementFromPoint(pointX, pointY) === canvas,
      ready: canvas?.dataset.ready,
    };
  }, { x, y });
  expect(hitState.hitCanvas).toBe(true);
  if (pendingInitialFit) expect(hitState.ready).toBe('false');

  const cameraDistance = async () => Number(await canvas.getAttribute('data-camera-distance'));
  await expect.poll(cameraDistance).toBeGreaterThan(0);
  const initialDistance = await cameraDistance();
  const initialTarget = await canvas.getAttribute('data-target');
  if (!initialTarget) throw new Error('The camera target must be available before zooming.');

  await page.mouse.wheel(0, zoom === 'Zoom in' ? -240 : 240);
  const distanceAfterZoom = expect.poll(cameraDistance);
  if (zoom === 'Zoom in') await distanceAfterZoom.toBeLessThan(initialDistance);
  else await distanceAfterZoom.toBeGreaterThan(initialDistance);
  const zoomedDistance = await cameraDistance();
  const positionBeforeDrag = await canvas.getAttribute('data-camera-position');

  await page.mouse.down();
  await page.mouse.move(x + (target === 'body' ? -80 : 120), y + 35);
  await page.mouse.up();

  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect.poll(() => canvas.getAttribute('data-camera-position')).not.toBe(positionBeforeDrag);
  await expect.poll(cameraDistance).toBeCloseTo(zoomedDistance, 2);
  await expect(canvas).toHaveAttribute('data-target', initialTarget);
  await expect(canvas).toHaveAttribute('data-subject', target === 'body' ? 'vehicle' : 'engine');
}

test('whole-vehicle zoom and orbit preserve distance during initial fit', async ({ page }) => {
  const canvas = page.locator('canvas');
  await page.goto('/?car=raize', { waitUntil: 'commit' });
  await expect(canvas).toBeVisible();
  await expect(canvas).toHaveAttribute('data-ready', 'false');
  await test.step('whole vehicle body zooms in and orbits during its initial fit', () =>
    preservesZoomWhileOrbiting(page, canvas, 'Zoom in', 'body', true));
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await expect(page.getByRole('region', { name: 'Selected vehicle' })).toContainText('Toyota Raize');
  await test.step('whole vehicle body zooms out then orbits', () =>
    preservesZoomWhileOrbiting(page, canvas, 'Zoom out', 'body'));
});

test('isolated-engine zoom and orbit preserve distance', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const canvas = page.locator('canvas');
  await page.goto('/?car=raize', { waitUntil: 'commit' });
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Explore Engine', exact: true }).click();
  await page.getByRole('button', { name: 'Isolate selection', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await expect(page.getByRole('region', { name: 'Selected subject controls' })).toContainText('Engine');
  for (const zoom of ['Zoom in', 'Zoom out'] as const) {
    await test.step(`isolated engine ${zoom.toLowerCase()} then drag`, () =>
      preservesZoomWhileOrbiting(page, canvas, zoom, 'engine'));
  }
});
