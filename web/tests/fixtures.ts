import { test as base } from '@playwright/test';

export { expect, type Locator, type Page } from '@playwright/test';

/** Existing journeys start as returning visitors so the first-visit introduction does not cover them. */
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.addInitScript(() => localStorage.setItem('30s.onboarding.v1', 'dismissed'));
    await use(page);
  },
});
