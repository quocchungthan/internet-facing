import { expect, test, type Page } from './fixtures';

async function walkToVerification(page: Page) {
  await page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true }).click();
  const guide = page.getByRole('complementary', { name: 'Wiper troubleshooting guide' });
  const initial = guide.getByRole('group', { name: /Initial observation/ });
  await initial.getByRole('checkbox', { name: 'streaks' }).check();
  await initial.getByRole('combobox', { name: 'Side' }).selectOption('driver');
  await initial.getByRole('checkbox', { name: /Seen while parked/ }).check();
  await guide.getByRole('button', { name: 'Parked, switched off and safe to inspect', exact: true }).click();
  await guide.getByRole('button', { name: 'No visible damage; dirt or residue possible', exact: true }).click();
  await guide.getByRole('button', { name: 'Glass and blades cleaned, arms lowered', exact: true }).click();
  await expect(guide.getByRole('heading', { name: '4. Check the result while parked' })).toBeVisible();
  return guide;
}

test('wiper guide is labelled generic, links official recall lookups and records each verification outcome', async ({ page }, testInfo) => {
  await page.goto('/');
  const guide = await walkToVerification(page);
  await expect(guide.locator('.guide-label')).toHaveText('Generic guidance · not verified for this model');
  const reference = guide.getByRole('region', { name: 'Guide source and applicability' });
  await expect(reference).toContainText('No recurring defect is claimed for the Toyota Raize');
  for (const name of [/GOV\.UK/, /NHTSA/]) {
    const link = reference.getByRole('link', { name });
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('href', /^https:\/\//);
  }

  const result = guide.getByRole('group', { name: /Result observation/ });
  await result.getByRole('checkbox', { name: /Seen while parked/ }).check();
  await guide.getByRole('button', { name: 'Clear sweep, no streaks or chatter', exact: true }).click();
  const outcome = guide.getByRole('group', { name: 'Check outcome' });
  await expect(outcome).toContainText('Improved in this parked test (not proof of a permanent repair)');
  await expect(outcome).toContainText('Before: streaks (driver side). After: no visible problem (side unknown).');
  await page.screenshot({ path: testInfo.outputPath('wiper-outcome-improved.png') });
  await outcome.getByRole('button', { name: 'Add this check to the observation log' }).click();
  await expect(outcome.getByRole('status')).toHaveText('Added to the observation log.');

  const cases = [
    { choice: 'Streaks remain after cleaning', heading: '5. Confirm fitment before replacement', outcome: null },
    { choice: 'Worse than the initial observation', heading: 'Further diagnosis or fitting is needed', outcome: 'Worse than before' },
    { choice: 'Could not run the parked wet-glass test', heading: 'Result not tested yet', outcome: 'Not tested' },
  ];
  for (const item of cases) {
    await guide.getByRole('button', { name: 'Previous observation', exact: true }).click();
    await guide.getByRole('button', { name: item.choice, exact: true }).click();
    await expect(guide.getByRole('heading', { name: item.heading })).toBeVisible();
    if (item.outcome) await expect(guide.getByRole('group', { name: 'Check outcome' })).toContainText(`Outcome: ${item.outcome}`);
    else await expect(guide.getByRole('group', { name: 'Check outcome' })).toHaveCount(0);
  }
  await guide.getByRole('button', { name: 'Close troubleshooting' }).click();
  await page.getByRole('button', { name: 'Observation log', exact: true }).click();
  const log = page.getByRole('complementary', { name: 'Observation log and mechanic handoff' });
  await expect(log.getByRole('list')).toContainText('Check: Wiper streak check');
  await expect(log.getByLabel('Handoff preview', { exact: true })).toContainText('Outcome: improved.');
});

test('observation log keeps unknowns, corrections and section choices in the mechanic handoff', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/?car=impala');
  await page.getByRole('button', { name: 'Observation log', exact: true }).click();
  const log = page.getByRole('complementary', { name: 'Observation log and mechanic handoff' });
  await expect(log).toContainText('Kept only in this browser tab');
  await log.getByRole('textbox', { name: 'Model year' }).fill('1967');
  await log.getByRole('button', { name: 'Save vehicle context' }).click();
  const preview = log.getByLabel('Handoff preview', { exact: true });
  await expect(preview).toContainText('Model: Chevrolet Impala');
  await expect(preview).toContainText('Model year: 1967');
  await expect(preview).toContainText('Market: Unknown');
  await expect(preview).toContainText('Odometer: Unknown');

  const symptom = log.getByRole('form', { name: 'Symptom observation' });
  await symptom.getByRole('textbox', { name: 'What did you observe?' }).fill('Squeal when braking');
  await symptom.getByRole('combobox', { name: 'Warning light or message shown?' }).selectOption('no');
  await symptom.getByRole('button', { name: 'Record observation' }).click();
  await expect(log.getByRole('list')).toContainText('symptom-1');
  await symptom.getByRole('combobox', { name: 'This entry is' }).selectOption('symptom-1');
  await symptom.getByRole('textbox', { name: 'What did you observe?' }).fill('Only on the first stop of the day');
  await symptom.getByRole('button', { name: 'Record observation' }).click();
  await expect(preview).toContainText('[Original complaint] symptom-1');
  await expect(preview).toContainText('[Correction to symptom-1] symptom-2');
  await expect(preview).toContainText('Conditions: Unknown.');
  await log.getByRole('button', { name: 'Remove symptom-1' }).click();
  await expect(log.getByRole('alert')).toContainText('Not recorded');
  await expect(log.getByRole('list')).toContainText('symptom-1');

  const finding = log.getByRole('form', { name: 'Finding' });
  await finding.getByRole('combobox', { name: 'Type' }).selectOption('suspected');
  await finding.getByRole('textbox', { name: 'Finding' }).fill('Pads may be glazed');
  await finding.getByRole('button', { name: 'Record finding' }).click();
  await expect(preview).toContainText('[Suspected, not confirmed] Pads may be glazed');

  await log.getByRole('checkbox', { name: 'Findings' }).uncheck();
  await expect(preview).not.toContainText('FINDINGS');
  await log.getByRole('checkbox', { name: 'Vehicle context' }).uncheck();
  await log.getByRole('checkbox', { name: 'Recent work' }).uncheck();
  await expect(preview).not.toContainText('VEHICLE CONTEXT');
  await expect(preview).toContainText('not a diagnosis');
  await page.screenshot({ path: testInfo.outputPath('observation-log.png'), fullPage: true });

  await page.evaluate(() => Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true }));
  await log.getByRole('button', { name: 'Copy handoff text' }).click();
  await expect(log.getByText(/Copy failed: .*copy it manually/)).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'Observation log', exact: true }).click();
  await expect(page.getByRole('complementary', { name: 'Observation log and mechanic handoff' }).getByText('Nothing recorded yet.')).toBeVisible();
  expect(errors).toEqual([]);
});

for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
  test(`observation log fits ${viewport.width}x${viewport.height}`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await page.goto('/');
    await page.getByRole('button', { name: 'Observation log', exact: true }).click();
    const log = page.getByRole('complementary', { name: 'Observation log and mechanic handoff' });
    await log.getByRole('textbox', { name: 'What did you observe?' }).fill('Streaks on the driver side');
    await log.getByRole('button', { name: 'Record observation' }).click();
    await expect(log.getByLabel('Handoff preview', { exact: true })).toContainText('Streaks on the driver side');
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(viewport.width);
    expect(await log.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`observation-log-${viewport.width}.png`) });
    await page.keyboard.press('Escape');
    await expect(log).toHaveCount(0);
  });
}
