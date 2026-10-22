import { expect, test } from './fixtures';

test.use({ reducedMotion: 'reduce', viewport: { width: 1440, height: 900 } });

test('previews public canonical context, edits without transmission and handles denied clipboard', async ({ page }, testInfo) => {
  const requests: string[] = [];
  page.on('request', request => { if (request.url().startsWith('https://chatgpt.com')) requests.push(request.url()); });
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'clipboard', { value: {
      writeText: async () => { throw new Error('Clipboard denied by browser'); },
    } });
  });
  await page.goto('/?car=raize&part=wheels&item=wheel-front-left&isolate=1');
  const canvas = page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const url = page.url();
  const distance = await canvas.getAttribute('data-camera-distance');
  const trigger = page.getByRole('button', { name: 'Ask ChatGPT about this component' });
  await trigger.focus();
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog', { name: 'Ask about this component' });
  const editor = dialog.getByRole('textbox', { name: 'Preview or edit question' });
  await expect(editor).toHaveValue(/Wheel and tire \(front left\)/);
  await expect(dialog).toContainText('external service');
  await editor.fill('Bánh xe & tire? #front https://not-the-destination.example');
  const link = dialog.getByRole('link', { name: 'Open ChatGPT (external)' });
  const destination = new URL((await link.getAttribute('href'))!);
  expect(destination.origin).toBe('https://chatgpt.com');
  expect(destination.searchParams.get('q')).toBe(await editor.inputValue());
  await expect(link).toHaveAttribute('target', '_blank');
  await expect(link).toHaveAttribute('rel', 'noreferrer');
  await dialog.getByRole('button', { name: 'Copy question' }).click();
  await expect(dialog.getByRole('status')).toContainText('Could not copy question');
  await expect(editor).toHaveValue(/Bánh xe/);
  expect(requests).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('question-preview.png') });
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(trigger).toBeFocused();
  expect(page.url()).toBe(url);
  await expect(canvas).toHaveAttribute('data-subject', 'wheel-front-left');
  expect(await canvas.getAttribute('data-camera-distance')).toBe(distance);
});

test('updates question identity on navigation and keeps copy independent from the link', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'clipboard', { value: {
      writeText: async (text: string) => { document.documentElement.dataset.copiedQuestion = text; },
    } });
  });
  await page.goto('/?car=raize&part=engine&item=engine-piston-study&isolate=1');
  await page.getByRole('button', { name: 'Ask ChatGPT about this component' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('textbox')).toHaveValue(/Piston/);
  await dialog.getByRole('textbox').fill('A deliberately edited question');
  await dialog.getByRole('button', { name: 'Copy question' }).click();
  await expect(dialog.getByRole('status')).toHaveText('Question copied.');
  await expect(page.locator('html')).toHaveAttribute('data-copied-question', 'A deliberately edited question');
  await page.keyboard.press('Escape');
  const controls = page.getByRole('region', { name: 'Selected subject controls' });
  await controls.getByRole('button', { name: /^Explore connected subject Connecting rod:/ }).click();
  await page.getByRole('button', { name: 'Ask ChatGPT about this component' }).click();
  await expect(page.getByRole('dialog').getByRole('textbox')).toHaveValue(/Connecting rod/);
  await expect(page.getByRole('dialog').getByRole('textbox')).not.toHaveValue(/deliberately edited/);
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`question editor remains reachable at ${viewport.width}x${viewport.height}`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await page.goto('/?car=raize&part=engine&item=engine-piston-study&isolate=1');
    await page.getByRole('button', { name: 'Ask ChatGPT about this component' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('textbox')).toBeVisible();
    await dialog.getByRole('button', { name: 'Close', exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath('question-controls.png') });
  });
}
