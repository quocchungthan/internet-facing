import { z } from 'zod';

const unknown = z.literal('unknown');
const knownText = (max: number) => z.string().trim().min(1).max(max);
const textOrUnknown = (max: number) => z.union([unknown, knownText(max)]);
const timestamp = z.string().datetime();
const recordId = z.string().regex(/^[a-z]+-\d+$/);

export const outcomes = ['improved', 'unresolved', 'worse', 'not-tested'] as const;
export const runStates = ['off', 'starting', 'idling', 'moving', 'unknown'] as const;
export const conditions = ['cold', 'hot', 'wet', 'under load'] as const;
export const warningStates = ['steady', 'flashing', 'unknown'] as const;
export const findingKinds = ['observed', 'suspected', 'confirmed'] as const;

export const vehicleContextSchema = z.object({
  catalogVehicleId: z.union([unknown, z.string().regex(/^[a-z][a-z0-9-]*$/)]),
  modelYear: z.union([unknown, z.number().int().min(1886).max(2100)]),
  market: textOrUnknown(80),
  powertrain: textOrUnknown(120),
  odometer: z.union([unknown, z.object({ value: z.number().finite().nonnegative().max(5_000_000), unit: z.enum(['km', 'mi']) }).strict()]),
  onset: textOrUnknown(200),
  recentWork: textOrUnknown(500),
}).strict();

const warningSchema = z.discriminatedUnion('shown', [
  z.object({ shown: z.literal('no') }).strict(),
  z.object({ shown: z.literal('unknown') }).strict(),
  z.object({ shown: z.literal('yes'), text: textOrUnknown(160), state: z.enum(warningStates) }).strict(),
]);

export const symptomSchema = z.object({
  id: recordId,
  recordedAt: timestamp,
  correctsId: recordId.optional(),
  description: knownText(1000),
  warning: warningSchema,
  runState: z.enum(runStates),
  conditions: z.union([unknown, z.array(z.enum(conditions)).refine(values => new Set(values).size === values.length, 'Duplicate condition')]),
}).strict();

export const checkSchema = z.object({
  id: recordId,
  recordedAt: timestamp,
  description: knownText(500),
  measurement: z.union([z.literal('none'), z.object({ value: z.number().finite(), unit: knownText(20) }).strict()]),
  conditions: textOrUnknown(200),
  action: z.union([z.literal('none'), knownText(300)]),
  outcome: z.enum(outcomes),
}).strict();

export const findingSchema = z.discriminatedUnion('kind', [
  z.object({ id: recordId, kind: z.literal('observed'), text: knownText(500) }).strict(),
  z.object({ id: recordId, kind: z.literal('suspected'), text: knownText(500) }).strict(),
  z.object({ id: recordId, kind: z.literal('confirmed'), text: knownText(500), confirmedBy: knownText(120) }).strict(),
]);

export const observationRecordSchema = z.object({
  version: z.literal(1),
  vehicle: vehicleContextSchema,
  symptoms: z.array(symptomSchema),
  checks: z.array(checkSchema),
  findings: z.array(findingSchema),
}).strict().superRefine((record, context) => {
  const ids = [...record.symptoms, ...record.checks, ...record.findings].map(entry => entry.id);
  if (new Set(ids).size !== ids.length) context.addIssue({ code: 'custom', message: 'Duplicate record id' });
  record.symptoms.forEach((symptom, index) => {
    if (index === 0 && symptom.correctsId) {
      context.addIssue({ code: 'custom', path: ['symptoms', 0], message: 'The first symptom must be the original complaint' });
    }
    if (symptom.correctsId && !record.symptoms.slice(0, index).some(earlier => earlier.id === symptom.correctsId)) {
      context.addIssue({ code: 'custom', path: ['symptoms', index, 'correctsId'], message: 'A correction must refer to an earlier symptom' });
    }
  });
});

export type VehicleContext = z.infer<typeof vehicleContextSchema>;
export type Symptom = z.infer<typeof symptomSchema>;
export type Check = z.infer<typeof checkSchema>;
export type Finding = z.infer<typeof findingSchema>;
export type ObservationRecord = z.infer<typeof observationRecordSchema>;
export type SymptomInput = Omit<Symptom, 'id' | 'recordedAt'>;
export type CheckInput = Omit<Check, 'id' | 'recordedAt'>;
export type FindingInput = Finding extends infer F ? F extends Finding ? Omit<F, 'id'> : never : never;

export const emptyVehicleContext: VehicleContext = {
  catalogVehicleId: 'unknown', modelYear: 'unknown', market: 'unknown', powertrain: 'unknown',
  odometer: 'unknown', onset: 'unknown', recentWork: 'unknown',
};

export const emptyRecord: ObservationRecord = { version: 1, vehicle: emptyVehicleContext, symptoms: [], checks: [], findings: [] };

export class RecordValidationError extends Error {
  constructor(readonly issues: z.ZodIssue[]) {
    super(issues.map(issue => `${issue.path.join('.') || 'record'}: ${issue.message}`).join('; '));
  }
}

function validated(record: ObservationRecord): ObservationRecord {
  const result = observationRecordSchema.safeParse(record);
  if (!result.success) throw new RecordValidationError(result.error.issues);
  return result.data;
}

function nextId(record: ObservationRecord, prefix: string) {
  const used = [...record.symptoms, ...record.checks, ...record.findings].map(entry => entry.id);
  let index = used.length + 1;
  while (used.includes(`${prefix}-${index}`)) index++;
  return `${prefix}-${index}`;
}

export function updateVehicleContext(record: ObservationRecord, vehicle: VehicleContext): ObservationRecord {
  return validated({ ...record, vehicle });
}

/** Append-only: the original complaint is never rewritten; a correction is a separate dated entry. */
export function addSymptom(record: ObservationRecord, input: SymptomInput, now: Date): ObservationRecord {
  return validated({ ...record, symptoms: [...record.symptoms, { ...input, id: nextId(record, 'symptom'), recordedAt: now.toISOString() }] });
}

export function addCheck(record: ObservationRecord, input: CheckInput, now: Date): ObservationRecord {
  return validated({ ...record, checks: [...record.checks, { ...input, id: nextId(record, 'check'), recordedAt: now.toISOString() }] });
}

export function addFinding(record: ObservationRecord, input: FindingInput): ObservationRecord {
  return validated({ ...record, findings: [...record.findings, { ...input, id: nextId(record, 'finding') } as Finding] });
}

export function removeEntry(record: ObservationRecord, id: string): ObservationRecord {
  if (record.symptoms.some(symptom => symptom.correctsId === id)) {
    throw new Error('Remove the corrections to this complaint first.');
  }
  return validated({
    ...record,
    symptoms: record.symptoms.filter(entry => entry.id !== id),
    checks: record.checks.filter(entry => entry.id !== id),
    findings: record.findings.filter(entry => entry.id !== id),
  });
}

export const handoffSections = ['vehicle', 'recentWork', 'symptoms', 'checks', 'findings'] as const;
export type HandoffSection = (typeof handoffSections)[number];

const show = (value: string | number) => value === 'unknown' ? 'Unknown' : String(value);
const date = (iso: string) => iso.replace('T', ' ').slice(0, 16) + ' UTC';

const listSection = <T,>(heading: string, entries: readonly T[], format: (entry: T) => string[]) =>
  ['', heading, ...(entries.length ? entries.flatMap(format) : ['None recorded.'])];

function vehicleLines(vehicle: ObservationRecord['vehicle'], vehicleNames: Readonly<Record<string, string>>) {
  const odometer = vehicle.odometer === 'unknown' ? 'Unknown' : `${vehicle.odometer.value} ${vehicle.odometer.unit}`;
  const model = vehicle.catalogVehicleId === 'unknown' ? 'Unknown' : vehicleNames[vehicle.catalogVehicleId] ?? vehicle.catalogVehicleId;
  return [`Model: ${model}`, `Model year: ${show(vehicle.modelYear)}`, `Market: ${show(vehicle.market)}`,
    `Powertrain: ${show(vehicle.powertrain)}`, `Odometer: ${odometer}`, `Onset: ${show(vehicle.onset)}`];
}

function symptomLines(symptom: Symptom) {
  const { warning } = symptom;
  const warningText = warning.shown === 'yes'
    ? `Warning shown: "${show(warning.text)}", ${warning.state}`
    : `Warning shown: ${warning.shown === 'no' ? 'No' : 'Unknown'}`;
  let conditions = 'None noted';
  if (symptom.conditions === 'unknown') conditions = 'Unknown';
  else if (symptom.conditions.length) conditions = symptom.conditions.join(', ');
  const kind = symptom.correctsId ? `Correction to ${symptom.correctsId}` : 'Original complaint';
  return [`- [${kind}] ${symptom.id}, ${date(symptom.recordedAt)}`, `  ${symptom.description}`,
    `  ${warningText}. Engine/vehicle state: ${symptom.runState}. Conditions: ${conditions}.`];
}

function checkLines(check: Check) {
  const measurement = check.measurement === 'none' ? 'No measurement' : `Measured ${check.measurement.value} ${check.measurement.unit}`;
  const action = check.action === 'none' ? 'None' : check.action;
  return [`- ${check.id}, ${date(check.recordedAt)}: ${check.description}`,
    `  ${measurement}. Conditions: ${show(check.conditions)}. Action: ${action}. Outcome: ${check.outcome}.`];
}

const findingLabels = { observed: 'Observed fact', suspected: 'Suspected, not confirmed' } as const;
function findingLines(finding: Finding) {
  const label = finding.kind === 'confirmed' ? `Confirmed by ${finding.confirmedBy}` : findingLabels[finding.kind];
  return [`- [${label}] ${finding.text}`];
}

export function formatHandoff(record: ObservationRecord, options: {
  include: ReadonlySet<HandoffSection>; vehicleNames: Readonly<Record<string, string>>; generatedAt: Date;
}): string {
  const { include } = options;
  const lines = [
    '30s OWNER OBSERVATION HANDOFF',
    `Prepared ${date(options.generatedAt.toISOString())}. Owner-recorded observations, not a diagnosis.`,
    'A symptom or a diagnostic code alone does not prove that a part has failed.',
  ];
  const context = [
    ...(include.has('vehicle') ? vehicleLines(record.vehicle, options.vehicleNames) : []),
    ...(include.has('recentWork') ? [`Recent work: ${show(record.vehicle.recentWork)}`] : []),
  ];
  if (context.length) lines.push('', 'VEHICLE CONTEXT', ...context);
  if (include.has('symptoms')) lines.push(...listSection('COMPLAINT AND CORRECTIONS (observed facts)', record.symptoms, symptomLines));
  if (include.has('checks')) lines.push(...listSection('CHECKS AND OUTCOMES', record.checks, checkLines));
  if (include.has('findings')) lines.push(...listSection('FINDINGS', record.findings, findingLines));
  return lines.join('\n');
}
