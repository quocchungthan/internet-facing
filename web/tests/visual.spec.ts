import { expect, test } from '@playwright/test';

test.use({ reducedMotion: 'reduce', viewport: { width: 1440, height: 900 } });

for (const car of ['raize', 'impala']) {
  for (const subject of ['vehicle', 'engine']) {
    test(`${car} ${subject} reviewed schematic`, async ({ page }, testInfo) => {
      await page.route(/fonts\.googleapis\.com|fonts\.gstatic\.com/, route => route.abort());
      const query = subject === 'engine' ? `?car=${car}&part=engine&isolate=1` : `?car=${car}`;
      await page.goto(`/${query}`);
      await page.addStyleTag({ content: '* { font-family: Arial, sans-serif !important; }' });
      const canvas = page.locator('canvas');
      await expect(canvas).toHaveAttribute('data-ready', 'true');
      const metrics = {
        triangles: Number(await canvas.getAttribute('data-triangles')),
        drawCalls: Number(await canvas.getAttribute('data-draw-calls')),
      };
      expect(metrics.triangles).toBeGreaterThan(0);
      expect(metrics.triangles).toBeLessThan(subject === 'engine' ? 20_000 : 220_000);
      expect(metrics.drawCalls).toBeLessThan(subject === 'engine' ? 40 : 320);
      await testInfo.attach('render-budget', { body: JSON.stringify(metrics), contentType: 'application/json' });
      await expect(page).toHaveScreenshot(`${car}-${subject}.png`);
      await page.screenshot({ path: testInfo.outputPath(`${car}-${subject}.png`) });
    });
  }
}

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`isolated engine remains usable at ${viewport.width}x${viewport.height}`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await page.goto('/?car=raize&part=engine&isolate=1');
    await expect(page.locator('canvas')).toHaveAttribute('data-ready', 'true');
    await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
    await page.getByRole('button', { name: 'Focus selection', exact: true }).click();
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(viewport.width);
    expect(await page.evaluate(() => document.documentElement.scrollHeight)).toBe(viewport.height);
    await page.screenshot({ path: testInfo.outputPath(`engine-${viewport.width}.png`) });
    await page.getByRole('button', { name: 'Show whole car', exact: true }).click();
    await expect(page.locator('canvas')).toHaveAttribute('data-isolated', 'false');
  });
}
