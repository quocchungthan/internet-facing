import { expect, test } from './fixtures';

for (const locale of ['en-US', 'vi-VN']) {
  test(`initial HTML declares the same provisional publisher city for ${locale}`, async ({ browser, baseURL }) => {
    const context = await browser.newContext({ locale, javaScriptEnabled: false });
    const page = await context.newPage();
    await page.goto(`${baseURL}/?car=impala&part=engine`);
    const city = page.locator('meta[name="geo.placename"]');
    await expect(city).toHaveCount(1);
    await expect(city).toHaveAttribute('content', 'Ho Chi Minh City, Vietnam');
    await expect(page.locator('script[type="application/ld+json"]')).toHaveCount(0);
    await expect(page.locator('meta[name="geo.position"], meta[name="ICBM"]')).toHaveCount(0);
    await context.close();
  });
}
