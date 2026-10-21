import { expect, test } from './fixtures';

test('car-first workshop supports selection, real photos, books and history', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Know your machine.' })).toBeVisible();
  await expect(page.locator('canvas')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Explore Cooling system', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('workshop-entry.png') });
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

test('view controls, guided slides and schematic PNG capture work', async ({ page }, testInfo) => {
  test.setTimeout(90_000);
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
  await page.screenshot({ path: testInfo.outputPath('workshop-desktop.png') });
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`viewport stays fixed at ${viewport.width}x${viewport.height} with reduced motion`, async ({ page }, testInfo) => {
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
    await page.screenshot({ path: testInfo.outputPath(`workshop-${viewport.width}.png`) });
  });
}

test('malformed shared state produces an explicit notice rather than a blank scene', async ({ page }) => {
  await page.goto('/?car=missing&part=unknown');
  await expect(page.locator('.notification')).toContainText('not found');
  await expect(page.getByRole('combobox', { name: 'Choose car model' })).toHaveValue('raize');
});

test('exact engine subject survives reload, connected navigation, history and car switching', async ({ page }) => {
  const subjectUrl = '/?car=raize&part=engine&item=engine-exhaust-valve-study&isolate=1';
  await page.goto(subjectUrl);
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect(canvas).toHaveAttribute('data-focusable', 'true');
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await expect(page).toHaveURL(/item=engine-exhaust-valve-study/);
  await page.reload();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');

  const controls = page.getByRole('region', { name: 'Selected subject controls' });
  const ancestry = controls.getByRole('group', { name: 'Subject ancestry' });
  await expect(ancestry.getByRole('button', { name: 'Up to Powertrain system' })).toBeVisible();
  await expect(ancestry.getByRole('button', { name: 'Up to Engine' })).toBeVisible();
  const connected = controls.getByRole('group', { name: 'Connected subjects' }).getByRole('button').first();
  await connected.click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-cylinder-study');
  await expect(page).toHaveURL(/item=engine-cylinder-study/);
  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');

  await controls.getByRole('group', { name: 'Subject ancestry' }).getByRole('button', { name: 'Up to Engine' }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine');
  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await page.goForward();
  await expect(canvas).toHaveAttribute('data-subject', 'engine');
  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-exhaust-valve-study');
  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('impala');
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await expect(page).toHaveURL(/car=impala$/);
});

test('keyboard search keeps the selected lesson through mobile collapse, Back, and a car change', async ({ page }, testInfo) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByRole('button', { name: 'Component index', exact: true }).press('Enter');
  const index = page.getByRole('complementary', { name: 'Component index' });
  const search = index.getByRole('textbox', { name: 'Search components' });
  await search.fill('piston rod');
  await search.press('Enter');

  const canvas = page.locator('canvas');
  const lesson = page.getByRole('complementary', { name: 'Connecting rod learning' });
  await expect(lesson.getByRole('heading', { name: 'Connecting rod', exact: true })).toBeFocused();
  await expect(lesson.getByText('Connects the piston motion to a crank throw;', { exact: false })).toBeVisible();
  await expect(page).toHaveURL(/item=engine-rod-study/);
  await page.getByRole('button', { name: 'Isolate selection', exact: true }).press('Enter');
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await expect(canvas).toHaveAttribute('data-subject', 'engine-rod-study');
  await page.screenshot({ path: testInfo.outputPath('keyboard-selected-rod-desktop.png') });

  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole('button', { name: 'Collapse selected subject lesson' }).press('Enter');
  const expand = lesson.getByRole('button', { name: 'Expand selected subject lesson' });
  await expect(expand).toBeFocused();
  await expand.press('Enter');
  await expect(lesson.getByRole('button', { name: 'Collapse selected subject lesson' })).toBeFocused();
  await expect(canvas).toHaveAttribute('data-isolated', 'true');
  await expect(page).toHaveURL(/item=engine-rod-study.*isolate=1/);
  await page.screenshot({ path: testInfo.outputPath('keyboard-selected-rod-mobile-expanded.png') });

  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-rod-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await page.getByRole('button', { name: 'Component index', exact: true }).press('Enter');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('complementary', { name: 'Component index' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Component index', exact: true })).toBeFocused();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-rod-study');

  await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('impala');
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(page).toHaveURL(/car=impala$/);
  await page.goBack();
  await expect(canvas).toHaveAttribute('data-subject', 'engine-rod-study');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await expect(lesson).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('keyboard-selected-rod-history-restored.png') });

  await lesson.getByRole('button', { name: 'Close part details' }).press('Enter');
  await expect(page.getByRole('button', { name: 'Component index', exact: true })).toBeFocused();
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth > window.innerWidth || document.documentElement.scrollHeight > window.innerHeight);
  expect(overflow).toBe(false);
});

test('invalid engine item links stay explicit and do not claim a focused scene', async ({ page }) => {
  await page.goto('/?car=raize&part=engine&item=unknown-study&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-focusable', 'true');
  await expect(page.locator('.notification')).toContainText('not found');
});

test('a Raize lesson link on the Impala does not resolve across vehicles', async ({ page }) => {
  await page.goto('/?car=impala&part=engine&item=engine-rod-study&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
  await expect(canvas).toHaveAttribute('data-isolated', 'false');
  await expect(page.locator('.notification')).toContainText('not found');
  await expect(page.getByRole('complementary', { name: 'Connecting rod learning' })).toHaveCount(0);
});

test('activating a child subject stops the guided tour', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Take a guided look' }).click();
  await expect(page.getByText('GUIDED WALKTHROUGH')).toBeVisible();
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const index = page.getByRole('complementary', { name: 'Component index' });
  await index.getByText('Car / assembly / part hierarchy', { exact: true }).click();
  await index.getByRole('button', { name: 'Spark plug', exact: true }).click();
  await expect(page.locator('canvas')).toHaveAttribute('data-subject', 'engine-spark-plug-study');
  await expect(page.getByText('GUIDED WALKTHROUGH')).toHaveCount(0);
});
