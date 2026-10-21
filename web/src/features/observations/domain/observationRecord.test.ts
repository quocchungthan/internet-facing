import { describe, expect, it } from 'vitest';
import {
  addCheck, addFinding, addSymptom, emptyRecord, formatHandoff, handoffSections, observationRecordSchema,
  RecordValidationError, removeEntry, updateVehicleContext, type SymptomInput,
} from './observationRecord';

const now = new Date('2026-10-21T09:30:00Z');
const later = new Date('2026-10-21T10:15:00Z');
const complaint: SymptomInput = {
  description: 'Streaks across the driver side of the windshield.',
  warning: { shown: 'no' }, runState: 'off', conditions: ['wet'],
};
const all = new Set(handoffSections);
const names = { raize: '2019 Toyota Raize' };

describe('vehicle context and symptom intake', () => {
  it('supports explicit unknowns and units, and rejects invalid combinations', () => {
    expect(emptyRecord.vehicle.odometer).toBe('unknown');
    const record = updateVehicleContext(emptyRecord, {
      ...emptyRecord.vehicle, catalogVehicleId: 'raize', modelYear: 2020, odometer: { value: 42000, unit: 'km' },
    });
    expect(record.vehicle.odometer).toEqual({ value: 42000, unit: 'km' });
    for (const odometer of [{ value: -1, unit: 'km' }, { value: 10, unit: 'miles' }, { value: 10 }, 42000]) {
      expect(() => updateVehicleContext(emptyRecord, { ...emptyRecord.vehicle, odometer } as never)).toThrow(RecordValidationError);
    }
    expect(() => updateVehicleContext(emptyRecord, { ...emptyRecord.vehicle, modelYear: 1700 })).toThrow(/modelYear/);
    expect(() => updateVehicleContext(emptyRecord, { ...emptyRecord.vehicle, market: '  ' })).toThrow(/market/);
    expect(() => updateVehicleContext(emptyRecord, { ...emptyRecord.vehicle, vin: 'ABC' } as never)).toThrow(/Unrecognized key/);
  });

  it('captures warning text and state without diagnosing, and rejects inconsistent warnings', () => {
    const record = addSymptom(emptyRecord, {
      ...complaint, warning: { shown: 'yes', text: 'Check engine', state: 'flashing' }, runState: 'idling', conditions: ['cold', 'under load'],
    }, now);
    expect(record.symptoms[0]).toMatchObject({ id: 'symptom-1', recordedAt: now.toISOString(), warning: { state: 'flashing' } });
    expect(() => addSymptom(emptyRecord, { ...complaint, warning: { shown: 'no', text: 'x' } } as never, now)).toThrow(RecordValidationError);
    expect(() => addSymptom(emptyRecord, { ...complaint, conditions: ['wet', 'wet'] }, now)).toThrow(/Duplicate condition/);
    expect(() => addSymptom(emptyRecord, { ...complaint, description: '' }, now)).toThrow(RecordValidationError);
  });

  it('keeps the original complaint and later corrections as distinct entries', () => {
    const original = addSymptom(emptyRecord, complaint, now);
    const corrected = addSymptom(original, { ...complaint, correctsId: 'symptom-1', description: 'Actually both sides.' }, later);
    expect(corrected.symptoms.map(symptom => [symptom.id, symptom.correctsId, symptom.description])).toEqual([
      ['symptom-1', undefined, complaint.description],
      ['symptom-2', 'symptom-1', 'Actually both sides.'],
    ]);
    expect(() => addSymptom(emptyRecord, { ...complaint, correctsId: 'symptom-9' }, now)).toThrow(/first symptom|earlier symptom/);
    expect(() => removeEntry(corrected, 'symptom-1')).toThrow(/corrections/);
    expect(removeEntry(removeEntry(corrected, 'symptom-2'), 'symptom-1').symptoms).toEqual([]);
  });
});

describe('observation log handoff', () => {
  const record = addFinding(addFinding(addCheck(addSymptom(updateVehicleContext(emptyRecord, {
    ...emptyRecord.vehicle, catalogVehicleId: 'raize', odometer: { value: 26000, unit: 'mi' }, recentWork: 'New blades fitted last month',
  }), complaint, now), {
    description: 'Parked wet-glass wiper test', measurement: 'none', conditions: 'parked, wet glass', action: 'Cleaned blades', outcome: 'improved',
  }, later), { kind: 'suspected', text: 'Blade wear' }), { kind: 'confirmed', text: 'Driver blade split', confirmedBy: 'Service provider' });

  it('exports context, dated checks and outcomes while keeping unknowns and finding types distinct', () => {
    const text = formatHandoff(record, { include: all, vehicleNames: names, generatedAt: later });
    expect(text).toContain('Model: 2019 Toyota Raize');
    expect(text).toContain('Model year: Unknown');
    expect(text).toContain('Odometer: 26000 mi');
    expect(text).toContain('[Original complaint] symptom-1, 2026-10-21 09:30 UTC');
    expect(text).toContain('Outcome: improved.');
    expect(text).toContain('[Suspected, not confirmed] Blade wear');
    expect(text).toContain('[Confirmed by Service provider] Driver blade split');
    expect(text).toContain('does not prove that a part has failed');
  });

  it('omits sections the owner removes, including recent work', () => {
    const text = formatHandoff(record, { include: new Set(['symptoms', 'checks'] as const), vehicleNames: names, generatedAt: later });
    expect(text).not.toContain('VEHICLE CONTEXT');
    expect(text).not.toContain('New blades');
    expect(text).not.toContain('FINDINGS');
    expect(text).toContain('CHECKS AND OUTCOMES');
  });

  it('requires a named confirmer for confirmed findings and validates whole records', () => {
    expect(() => addFinding(emptyRecord, { kind: 'confirmed', text: 'x' } as never)).toThrow(RecordValidationError);
    expect(observationRecordSchema.safeParse({ ...record, version: 2 }).success).toBe(false);
    expect(observationRecordSchema.safeParse(record).success).toBe(true);
  });
});
