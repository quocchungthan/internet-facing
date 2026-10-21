import { parseGuideReference } from './guideReference';

export type CheckOutcome = 'improved' | 'unresolved' | 'worse' | 'not-tested';

export type GuideStep = {
  title: string;
  instruction: string;
  target: string;
  choices: { label: string; next: string; outcome?: CheckOutcome }[];
};

export const wiperReference = parseGuideReference({
  guideId: 'wiper-streaks',
  source: {
    title: 'AAA: Windshield Wiper Safety and Maintenance',
    url: 'https://www.acg.aaa.com/connect/blogs/4c/auto/windshield-wipers-safety-maintenance',
  },
  scope: 'Cleaning, parked wet-glass testing, fitment checks and careful handling of conventional windshield wipers. Not blade sizes, attachment types or wiper-motor repair.',
  reviewed: '2026-10-20',
  applicability: { status: 'generic', appliesTo: 'conventional front windshield wipers on any car' },
});

export const wiperSteps = {
  prepare: {
    title: '1. Make the check safe',
    instruction: 'Park safely, switch the wipers and ignition off, and keep the key away from accidental operation. Never inspect moving wipers. If blades are frozen to the glass, defrost first; do not force them. Check your exact owner manual before lifting an arm: some cars need a service position to clear the hood. Stop if access is unclear.',
    target: 'wipers',
    choices: [
      { label: 'Parked, switched off and safe to inspect', next: 'inspect' },
      { label: 'Cannot safely inspect or see clearly to drive', next: 'stop' },
    ],
  },
  inspect: {
    title: '2. Inspect the wiping edge',
    instruction: 'Look for dirt on the glass and blade, a split or missing rubber edge, and a loose blade. The dark strip in the illustration is the contact edge, not the metal arm. Do not assume streaking proves the motor or blade has failed. This illustration does not identify your attachment type.',
    target: 'wiper-blade-left',
    choices: [
      { label: 'Rubber split, missing or blade loose', next: 'replace' },
      { label: 'No visible damage; dirt or residue possible', next: 'clean' },
      { label: 'Unsure, arm damaged or wipers do not move', next: 'service' },
    ],
  },
  clean: {
    title: '3. Clean before replacing',
    instruction: 'With the system off and safe access confirmed, gently wipe the rubber with a clean, damp cloth and clean debris from the windshield, including its lower edge. Follow the manual and cleaning-product directions. Support the arm if lifted and lower it gently; never let it snap onto the glass. Tools: a clean, damp cloth and an appropriate glass-cleaning product.',
    target: 'wiper-rubber-left',
    choices: [
      { label: 'Glass and blades cleaned, arms lowered', next: 'verifyClean' },
      { label: 'Cannot reach or handle the arm safely', next: 'service' },
    ],
  },
  verifyClean: {
    title: '4. Check the result while parked',
    instruction: 'With both blades secured and lowered, everyone clear and the windshield wet using the correct washer fluid, operate the wipers from inside the parked car. Never test on dry or icy glass. Check the swept view for streaks, missed areas and chatter. Switch the system off before any further inspection.',
    target: 'wipers',
    choices: [
      { label: 'Clear sweep, no streaks or chatter', next: 'resolved', outcome: 'improved' },
      { label: 'Streaks remain after cleaning', next: 'replace', outcome: 'unresolved' },
      { label: 'No washer spray, no movement or abnormal action', next: 'service', outcome: 'unresolved' },
      { label: 'Worse than the initial observation', next: 'service', outcome: 'worse' },
      { label: 'Could not run the parked wet-glass test', next: 'notTested', outcome: 'not-tested' },
    ],
  },
  replace: {
    title: '5. Confirm fitment before replacement',
    instruction: 'A damaged or persistently ineffective blade may need replacement; this is not a confirmed diagnosis. Confirm the vehicle year, market, arm attachment and BOTH front blade sizes using its manual and a verified fitment guide. Follow the exact replacement blade instructions, or have a service provider fit it. Do not copy the schematic connector, bend the arm or guess an adapter. No sizes or removal sequence are verified here.',
    target: 'wiper-blade-left',
    choices: [
      { label: 'Correct blades fitted securely per instructions', next: 'verifyReplace' },
      { label: 'Fitment or installation uncertain', next: 'service' },
    ],
  },
  verifyReplace: {
    title: '6. Verify after replacement',
    instruction: 'Confirm both blades are securely attached and lowered. With everyone clear, test from inside the parked car on a wet windshield using the correct washer fluid. Check for a clear sweep without streaking, chatter or missed areas. Stop the wipers before inspecting again. Do not drive if visibility is impaired.',
    target: 'wipers',
    choices: [
      { label: 'Clear sweep after replacement', next: 'resolved', outcome: 'improved' },
      { label: 'Still streaking or operation abnormal', next: 'service', outcome: 'unresolved' },
      { label: 'Worse than the initial observation', next: 'service', outcome: 'worse' },
      { label: 'Could not run the parked wet-glass test', next: 'notTested', outcome: 'not-tested' },
    ],
  },
  resolved: {
    title: 'Check passed for this observation',
    instruction: 'A clear parked test is useful evidence, not a guarantee in all weather or a permanent repair. Recheck visibility before driving and monitor for recurrence. If streaking returns, do not repeatedly replace parts by guesswork: record the conditions and seek an inspection.',
    target: 'wipers',
    choices: [],
  },
  notTested: {
    title: 'Result not tested yet',
    instruction: 'Without a parked wet-glass test the result is unknown, so do not assume the streaking is fixed. Do not drive with impaired visibility. Run the parked test once it is safe, or record "not tested" when handing the check to a service provider.',
    target: 'wipers',
    choices: [],
  },
  service: {
    title: 'Further diagnosis or fitting is needed',
    instruction: 'Stop this owner-maintenance path. Record which side streaks, whether cleaning helped, any visible damage and whether spray and motion work. Ask a qualified service provider to inspect blade fit, arm contact and the washer/wiper system. These are possible areas to check, not diagnosed faults. Do not adjust arm pressure or dismantle the mechanism from this illustration.',
    target: 'wipers',
    choices: [],
  },
  stop: {
    title: 'Stop: safe access or visibility is not established',
    instruction: 'Do not continue this check or drive with an obscured view. Arrange safe assistance. Frozen blades need defrosting before operation; unsafe access needs the correct manual or professional help. Resume only when those conditions are resolved.',
    target: 'wipers',
    choices: [],
  },
} satisfies Record<string, GuideStep>;

export type WiperStepId = keyof typeof wiperSteps;

export function advanceWiperGuide(step: WiperStepId, choice: number): WiperStepId {
  const option = wiperSteps[step].choices[choice];
  if (!option) throw new Error(`Invalid wiper-guide choice ${choice} at ${step}.`);
  const next = option.next;
  if (!(next in wiperSteps)) throw new Error(`Unknown wiper-guide step ${next}.`);
  return next as WiperStepId;
}

export type WiperTrailEntry = { step: WiperStepId; choice: number };

/** The latest verification decides the outcome; earlier ones are superseded. No verification means no outcome. */
export function wiperOutcome(trail: readonly WiperTrailEntry[]): CheckOutcome | null {
  for (let index = trail.length - 1; index >= 0; index--) {
    const { step, choice } = trail[index];
    const option: GuideStep['choices'][number] | undefined = wiperSteps[step].choices[choice];
    if (option?.outcome) return option.outcome;
  }
  return null;
}

export const wiperPatterns = ['streaks', 'missed areas', 'chatter or skipping', 'smearing'] as const;
export type WiperObservation = {
  patterns: (typeof wiperPatterns)[number][];
  side: 'driver' | 'passenger' | 'both' | 'unknown';
  parkedWetGlass: boolean;
};

export const outcomeLabels: Record<CheckOutcome, string> = {
  improved: 'Improved in this parked test (not proof of a permanent repair)',
  unresolved: 'Unresolved',
  worse: 'Worse than before',
  'not-tested': 'Not tested',
};

/** Before/after are comparable only when both were recorded parked on wet glass. */
export function compareWiperObservations(initial: WiperObservation | null, result: WiperObservation | null) {
  if (!initial || !result) return { comparable: false as const, reason: 'Record both the initial and the result observation.' };
  if (!initial.parkedWetGlass || !result.parkedWetGlass) {
    return { comparable: false as const, reason: 'Both observations must be made parked, on wet glass, with the same washer test.' };
  }
  const describe = (observation: WiperObservation) =>
    `${observation.patterns.length ? observation.patterns.join(', ') : 'no visible problem'} (${observation.side === 'unknown' ? 'side unknown' : `${observation.side} side`})`;
  return { comparable: true as const, summary: `Before: ${describe(initial)}. After: ${describe(result)}.` };
}
