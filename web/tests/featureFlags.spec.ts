import { execFile, spawn } from 'node:child_process';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';
import { promisify, stripVTControlCharacters } from 'node:util';
import { expect, test, type Page } from '@playwright/test';
import type { FeatureFlags } from '../src/config/featureFlags';

const run = promisify(execFile);
const vite = fileURLToPath(new URL('../node_modules/vite/bin/vite.js', import.meta.url));

function preview(directory: string) {
  const server = spawn(process.execPath, [vite, 'preview', '--host', '127.0.0.1', '--port', '0', '--outDir', directory], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const ready = new Promise<string>((resolve, reject) => {
    let log = '';
    const timeout = setTimeout(() => reject(new Error(`Feature preview timed out:\n${log}`)), 15000);
    const collect = (data: Buffer) => {
      log += data.toString();
      const url = stripVTControlCharacters(log).match(/http:\/\/127\.0\.0\.1:\d+\//)?.[0];
      if (url) { clearTimeout(timeout); resolve(url); }
    };
    server.stdout.on('data', collect);
    server.stderr.on('data', collect);
    server.once('error', error => { clearTimeout(timeout); reject(error); });
    server.once('exit', code => {
      clearTimeout(timeout);
      reject(new Error(`Feature preview exited (${code}):\n${log}`));
    });
  });
  return { server, ready };
}

async function checkExploration(page: Page, enabled: boolean) {
  const canvas = page.locator('canvas');
  if (!enabled) {
    await expect(canvas).toHaveCount(0);
    await expect(page.getByRole('group', { name: 'Camera controls' })).toHaveCount(0);
    return;
  }
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  await page.getByRole('button', { name: 'Component index', exact: true }).click();
  const index = page.getByRole('complementary', { name: 'Component index' });
  await index.getByRole('textbox', { name: 'Search components' }).fill('Engine');
  await index.getByRole('button', { name: /Engine Toyota Raize \/ Powertrain system \/ Engine/ }).click();
  await page.getByRole('button', { name: 'Isolate selection', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'engine');
  await page.getByRole('button', { name: 'Show whole car', exact: true }).click();
  await expect(canvas).toHaveAttribute('data-subject', 'vehicle');
}

async function checkRepair(page: Page, flags: FeatureFlags) {
  const entry = page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true });
  if (!flags.repair) {
    await expect(entry).toHaveCount(0);
    await expect(page.getByText('Wipers leave streaks? Start a guided check', { exact: true })).toHaveCount(0);
    return;
  }
  await entry.click();
  const guide = page.getByRole('complementary', { name: 'Wiper troubleshooting guide' });
  await expect(guide.getByRole('button', { name: 'Locate this check in 3D' }))
    .toHaveCount(flags.exploration ? 1 : 0);
  await guide.getByRole('button', { name: 'Parked, switched off and safe to inspect' }).click();
  await guide.getByRole('button', { name: 'No visible damage; dirt or residue possible' }).click();
  await guide.getByRole('button', { name: 'Glass and blades cleaned, arms lowered' }).click();
  await guide.getByRole('button', { name: 'Clear sweep, no streaks or chatter' }).click();
  await expect(guide.getByRole('heading', { name: 'Check passed for this observation' })).toBeVisible();
  await expect(guide.getByRole('group', { name: 'Check outcome' })).toContainText('Improved in this parked test');
  await expect(guide.getByText('Generic guidance · not verified for this model')).toBeVisible();
  await guide.getByRole('button', { name: 'Previous observation' }).click();
  await guide.getByRole('button', { name: 'Could not run the parked wet-glass test' }).click();
  await expect(guide.getByRole('group', { name: 'Check outcome' })).toContainText('Outcome: Not tested');
  await guide.getByRole('button', { name: 'Previous observation' }).click();
  await expect(guide.getByRole('heading', { name: '4. Check the result while parked' })).toBeVisible();
  await guide.getByRole('button', { name: 'Close troubleshooting' }).click();
  await expect(guide).toHaveCount(0);
}

async function checkLearning(page: Page, flags: FeatureFlags) {
  const book = page.getByRole('button', { name: 'Open field book', exact: true });
  if (!flags.learning) {
    await expect(book).toHaveCount(0);
    await expect(page.getByRole('group', { name: 'Part information' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Take a guided look' })).toHaveCount(0);
    await expect(page.getByText(/not OEM geometry/).first()).toBeVisible();
    return;
  }
  await book.click();
  await expect(page.getByRole('heading', { name: 'Toyota Raize — a field book' })).toBeVisible();
  await page.getByRole('button', { name: 'Close part details' }).click();
  await expect(page.getByRole('group', { name: 'Part information' })).toHaveCount(0);
  if (flags.exploration) {
    await page.getByRole('button', { name: 'Component index', exact: true }).click();
    const index = page.getByRole('complementary', { name: 'Component index' });
    await index.getByRole('textbox', { name: 'Search components' }).fill('piston rod');
    await index.getByRole('button', { name: /Connecting rod/ }).click();
  } else {
    await page.getByRole('combobox', { name: 'Choose a part' }).selectOption('engine-rod-study');
  }
  let subject = page.getByRole('complementary', { name: 'Connecting rod learning' });
  await expect(subject.getByText('Connects the piston motion to a crank throw;', { exact: false })).toBeVisible();
  await expect(subject.getByText('NASA Glenn - Four-Stroke Engine Operation', { exact: true })).toBeVisible();
  await subject.getByRole('button', { name: /Explore connected subject Piston:/ }).click();
  subject = page.getByRole('complementary', { name: 'Piston learning' });
  await expect(subject.getByText('Gas pressure acts on the crown;', { exact: false })).toBeVisible();
  await expect(page.locator('canvas')).toHaveCount(flags.exploration ? 1 : 0);
  if (!flags.exploration) {
    const missingLessonUrl = new URL(page.url());
    missingLessonUrl.search = '?car=raize&part=engine&item=engine-head';
    await page.goto(missingLessonUrl.toString());
    const missing = page.getByRole('complementary', { name: 'Engine head learning' });
    await expect(missing.getByRole('status')).toHaveText('No reviewed explanation for this subject.');
    await expect(missing.getByText('Three cylinders. A turbo.', { exact: false })).toHaveCount(0);
    await expect(page.getByText(/Schematics are representative, not OEM geometry or repair access/)).toBeVisible();
  }
}

async function checkSmallScreens(page: Page, flags: FeatureFlags) {
  for (const viewport of [{ width: 375, height: 812 }, { width: 740, height: 390 }]) {
    await page.setViewportSize(viewport);
    if (flags.repair) await page.getByRole('button', { name: 'Troubleshoot wiper streaks', exact: true }).click();
    const overflow = await page.evaluate(() => {
      const main = document.querySelector('main')!;
      return main.scrollWidth > main.clientWidth || document.documentElement.scrollWidth > innerWidth;
    });
    expect(overflow).toBe(false);
    if (flags.repair) {
      await page.keyboard.press('Escape');
      await expect(page.getByRole('complementary', { name: 'Wiper troubleshooting guide' })).toHaveCount(0);
    }
  }
}

async function checkOnboarding(page: Page, flags: FeatureFlags) {
  const intro = page.getByRole('dialog', { name: 'Welcome to 30s' });
  const anyFeature = flags.exploration || flags.repair || flags.learning;
  if (!flags.onboarding || !anyFeature) {
    await page.waitForTimeout(300);
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Replay introduction' })).toHaveCount(0);
    return;
  }
  await expect(intro).toBeVisible();
  const expected = 1 + Number(flags.repair) + Number(flags.exploration) + Number(flags.learning);
  await expect(intro.getByText(`INTRODUCTION · 1 OF ${expected}`)).toBeVisible();
  await intro.getByRole('button', { name: 'Skip', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
}

for (let mask = 0; mask < 16; mask++) {
  const flags = { onboarding: Boolean(mask & 8), exploration: Boolean(mask & 4), repair: Boolean(mask & 2), learning: Boolean(mask & 1) };
  test(`independent features O${Number(flags.onboarding)} E${Number(flags.exploration)} R${Number(flags.repair)} L${Number(flags.learning)}`, async ({ page }, testInfo) => {
    const directory = testInfo.outputPath('build');
    await run(process.execPath, [vite, 'build', '--outDir', directory], {
      env: {
        ...process.env,
        VITE_FEATURE_ONBOARDING: String(flags.onboarding),
        VITE_FEATURE_EXPLORATION: String(flags.exploration),
        VITE_FEATURE_REPAIR: String(flags.repair),
        VITE_FEATURE_LEARNING: String(flags.learning),
      },
      maxBuffer: 4 * 1024 * 1024,
    });
    const { server, ready } = preview(directory);
    try {
      const baseURL = await ready;
      const errors: string[] = [];
      const requests: string[] = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('request', request => requests.push(request.url()));
      await page.goto(baseURL);
      await expect(page.getByRole('main')).toBeVisible();
      await checkOnboarding(page, flags);
      await checkExploration(page, flags.exploration);
      await checkRepair(page, flags);
      await checkLearning(page, flags);
      if (!flags.exploration && !flags.repair && !flags.learning) {
        await expect(page.getByRole('heading', { name: 'No features are enabled in this build.' })).toBeVisible();
      } else {
        await page.getByRole('combobox', { name: 'Choose car model' }).selectOption('impala');
        await expect(page.getByRole('combobox', { name: 'Choose car model' })).toHaveValue('impala');
      }
      await checkSmallScreens(page, flags);
      await page.screenshot({ path: testInfo.outputPath('feature-combination.png') });
      if (!flags.exploration) expect(requests.some(url => /\/(?:three|WorkshopScene|ExplorationWorkshop)-/.test(url))).toBe(false);
      if (!flags.repair) expect(requests.some(url => /\/RepairGuide-/.test(url))).toBe(false);
      if (!flags.learning) expect(requests.some(url => /\/(?:DetailPanel|markdown)-/.test(url))).toBe(false);
      if (!flags.onboarding) expect(requests.some(url => /\/FirstVisitGuide-/.test(url))).toBe(false);
      expect(errors).toEqual([]);
    } finally {
      if (server.exitCode === null && server.signalCode === null) {
        const exited = once(server, 'exit');
        server.kill();
        await exited;
      }
    }
  });
}
