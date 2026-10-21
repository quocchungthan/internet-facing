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
  await expect(canvas).toHaveAttribute('data-focusable', 'true');
  const visible = (await canvas.getAttribute('data-items'))!.split(',').sort();
  expect(visible).toEqual([
    'engine-block', 'engine-cover', 'engine-head', 'intake', 'lubrication',
    'engine-cylinder-study', 'engine-piston-study', 'engine-rod-study', 'engine-crankshaft-study',
    'engine-intake-valve-study', 'engine-exhaust-valve-study', 'engine-spark-plug-study',
  ].sort());
  await expect(page.getByRole('button', { name: 'Explore Wheels & tires', exact: true })).toHaveCount(0);
  const before = Number(await canvas.getAttribute('data-camera-distance'));
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect.poll(async () => Number(await canvas.getAttribute('data-camera-distance'))).toBeLessThan(before);
  const zoomedDistance = Number(await canvas.getAttribute('data-camera-distance'));
  const target = await canvas.getAttribute('data-target');
  await page.getByRole('button', { name: 'Rotate camera left', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-target', target!);
  await expect.poll(async () => Number(await canvas.getAttribute('data-camera-distance'))).toBeCloseTo(zoomedDistance, 1);
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
  await expect(canvas).toHaveAttribute('data-focusable', 'true');
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

test('component hierarchy distinguishes the shared Raize valve specification by occurrence', async ({ page }) => {
  await page.goto('/?car=raize');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const index = page.getByRole('complementary', { name: 'Component index' });
  await index.getByText('Car / assembly / part hierarchy', { exact: true }).click();
  await index.getByRole('button', { name: 'Intake valve', exact: true }).click();
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-subject', 'engine-intake-valve-study');
  await expect(page).toHaveURL(/item=engine-intake-valve-study/);
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const reopenedIndex = page.getByRole('complementary', { name: 'Component index' });
  await reopenedIndex.getByText('Car / assembly / part hierarchy', { exact: true }).click();
  await reopenedIndex.getByRole('button', { name: 'Exhaust valve', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await expect(page).toHaveURL(/item=engine-exhaust-valve-study/);
});

test('component search trims aliases, preserves identity and activates with Enter', async ({ page }, testInfo) => {
  await page.goto('/?car=raize');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const index = page.getByRole('complementary', { name: 'Component index' });
  const search = index.getByRole('textbox', { name: 'Search components' });
  await expect(search).toBeFocused();
  const beforeUrl = page.url();
  const beforeCamera = await canvas.getAttribute('data-camera-distance');
  await search.fill(' PISTON ROD ');
  await expect(index.getByRole('button', { name: /Connecting rod/ })).toHaveCount(1);
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  expect(page.url()).toBe(beforeUrl);
  expect(await canvas.getAttribute('data-camera-distance')).toBe(beforeCamera);
  await page.screenshot({ path: testInfo.outputPath('component-search-rod.png') });
  await search.press('Enter');
  await expect(canvas).toHaveAttribute('data-subject', 'engine-rod-study');
  await expect(page).toHaveURL(/item=engine-rod-study/);
  await expect(page.locator('#selected-subject-heading')).toBeFocused();
  await page.screenshot({ path: testInfo.outputPath('component-search-selected-rod.png') });
});

test('component search keeps repeated wheels distinct and does not retain rows after a car switch', async ({ page }) => {
  await page.goto('/?car=impala');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  let index = page.getByRole('complementary', { name: 'Component index' });
  const search = index.getByRole('textbox', { name: 'Search components' });
  await index.getByRole('button', { name: 'Chassis', exact: true }).click();
  await search.fill('wheel');
  await expect(index.getByRole('button', { name: /Wheel and tire \(front left\)/ })).toHaveCount(1);
  await expect(index.getByRole('button', { name: /Wheel and tire \(front right\)/ })).toHaveCount(1);
  await search.fill('not a component');
  await expect(index.getByText('No matching components. Try a different term or system.')).toBeVisible();
  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('raize');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  index = page.getByRole('complementary', { name: 'Component index' });
  const raizeSearch = index.getByRole('textbox', { name: 'Search components' });
  await expect(raizeSearch).toHaveValue('');
  expect(await index.locator('.part-row').count()).toBeGreaterThan(7);
  await raizeSearch.fill('bore');
  await expect(index.getByRole('button', { name: /Cylinder wall \/ bore/ })).toHaveCount(1);
});

test('component search Enter activation works at the mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/?car=raize');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const index = page.getByRole('complementary', { name: 'Component index' });
  const search = index.getByRole('textbox', { name: 'Search components' });
  await expect(search).toBeFocused();
  await search.fill('bore');
  await search.press('Enter');
  await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'engine-cylinder-study');
  await expect(page.locator('#selected-subject-heading')).toBeFocused();
});

const engineSubjectCases = [
  ['engine-cylinder-study', 'Cylinder wall / bore', 'The cylindrical surface guides the piston', 'NASA Glenn - Engine Parts'],
  ['engine-piston-study', 'Piston', 'Gas pressure acts on the crown', 'NASA Glenn - Engine Parts'],
  ['engine-rod-study', 'Connecting rod', 'Connects the piston motion to a crank throw', 'NASA Glenn - Four-Stroke Engine Operation'],
  ['engine-crankshaft-study', 'Crankshaft', 'Converts the constrained piston/rod movement', 'NASA Glenn - Engine Parts'],
  ['engine-intake-valve-study', 'Intake valve', 'Controls the entry path', 'NASA Glenn - Engine Parts'],
  ['engine-exhaust-valve-study', 'Exhaust valve', 'Controls the exit path', 'NASA Glenn - Engine Parts'],
  ['engine-spark-plug-study', 'Spark plug', 'Produces the spark initiating combustion', 'U.S. Department of Energy AFDC - How Do Gasoline Cars Work?'],
];

test('all seven Raize child subjects show their own sourced generic lesson', async ({ page }, testInfo) => {
  for (const [itemId, label, purpose, sourceTitle] of engineSubjectCases) {
    await page.goto(`/?car=raize&part=engine&item=${itemId}&isolate=1`);
    const panel = page.getByRole('complementary', { name: `${label} learning` });
    await expect(panel).toBeVisible();
    await expect(panel.getByRole('heading', { name: label, exact: true })).toBeVisible();
    await expect(panel.getByText(purpose, { exact: false })).toBeVisible();
    await expect(panel.getByText(sourceTitle, { exact: true })).toBeVisible();
    await expect(panel.getByRole('heading', { name: 'Four strokes · generic sequence' })).toBeVisible();
    await expect(panel.getByRole('group', { name: 'Connected subjects' }).getByRole('button').first()).toBeVisible();
    await expect(panel.getByText(/Retrieved 2026-10-21/).first()).toBeVisible();
    await expect(panel.getByText('Three cylinders. A turbo.', { exact: false })).toHaveCount(0);
    await expect(page.locator('canvas')).toHaveAttribute('data-isolated', 'true');
    await page.screenshot({ path: testInfo.outputPath(`${itemId}-lesson.png`) });
  }
});

test('connected child lessons navigate by exact identity and missing lessons do not inherit engine text', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&item=engine-rod-study');
  let panel = page.getByRole('complementary', { name: 'Connecting rod learning' });
  await panel.getByRole('button', { name: /Explore connected subject Piston:/ }).click();
  await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'engine-piston-study');
  await expect(page).toHaveURL(/item=engine-piston-study/);
  await expect(page.getByRole('complementary', { name: 'Piston learning' })).toBeVisible();
  await page.goBack();
  await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'engine-rod-study');
  await expect(page.getByRole('complementary', { name: 'Connecting rod learning' })).toBeVisible();

  await page.goto('/?car=raize&part=engine&item=engine-head');
  panel = page.getByRole('complementary', { name: 'Engine head learning' });
  await expect(panel.getByRole('status')).toHaveText('No reviewed explanation for this subject.');
  await expect(panel.getByText('Three cylinders. A turbo.', { exact: false })).toHaveCount(0);
  await expect(panel.getByText(/not OEM geometry, service access or repair instructions/)).toBeVisible();
});

test('clicking the isolated 3D engine picks a drawable child', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await canvas.click({ position: { x: 720, y: 450 } });
  await expect.poll(() => canvas.getAttribute('data-subject')).not.toBe('engine');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const item = await canvas.getAttribute('data-subject');
  expect([
    'engine-block', 'engine-head', 'engine-cover', 'intake', 'lubrication',
    'engine-cylinder-study', 'engine-piston-study', 'engine-rod-study', 'engine-crankshaft-study',
    'engine-intake-valve-study', 'engine-exhaust-valve-study', 'engine-spark-plug-study',
  ]).toContain(item);
  await expect(canvas).toHaveAttribute('data-items', item!);
});

for (const itemId of [
  'engine-cylinder-study', 'engine-piston-study', 'engine-rod-study', 'engine-crankshaft-study',
  'engine-intake-valve-study', 'engine-exhaust-valve-study', 'engine-spark-plug-study',
]) {
  test(`${itemId} renders as a selectable isolated subject with measured bounds`, async ({ page }, testInfo) => {
    await page.goto(`/?car=raize&part=engine&item=${itemId}&isolate=1`);
    const canvas = page.locator('canvas');
    await expect(canvas).toHaveAttribute('data-ready', 'true');
    await expect(canvas).toHaveAttribute('data-subject', itemId);
    await expect(canvas).toHaveAttribute('data-isolated', 'true');
    await expect(canvas).toHaveAttribute('data-items', itemId);
    expect(Number(await canvas.getAttribute('data-radius'))).toBeGreaterThan(0);
    expect(Number(await canvas.getAttribute('data-triangles'))).toBeGreaterThan(0);
    const labels = page.getByRole('button', { name: 'Show part labels', exact: true });
    await expect(labels).toHaveAttribute('aria-pressed', 'true');
    await labels.click();
    await expect(labels).toHaveAttribute('aria-pressed', 'false');
    await page.screenshot({ path: testInfo.outputPath(`${itemId}.png`) });
    if (itemId.endsWith('valve-study') || itemId === 'engine-spark-plug-study') {
      await page.goto('/?car=raize&part=engine&isolate=1');
      await expect(canvas).toHaveAttribute('data-ready', 'true');
      const sceneLabels = page.getByRole('button', { name: 'Show part labels', exact: true });
      await sceneLabels.click();
      await expect(sceneLabels).toHaveAttribute('aria-pressed', 'false');
      await page.screenshot({ path: testInfo.outputPath(`${itemId}-whole-engine-cutaway.png`) });
    }
  });
}

test('field book exits isolation so its learning panel remains reachable', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&isolate=1');
  await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Open field book', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Toyota Raize — a field book' })).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-isolated', 'false');
});
