import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { IconButton } from '../../../components/IconButton';
import { vehicles } from '../../../content';
import type { Vehicle } from '../../../domain/catalog';
import { observationStore, useObservationRecord } from '../application/observationStore';
import {
  addCheck, addFinding, addSymptom, conditions, formatHandoff, handoffSections, outcomes, removeEntry, runStates,
  updateVehicleContext, type HandoffSection, type ObservationRecord, type SymptomInput,
} from '../domain/observationRecord';

const vehicleNames = Object.fromEntries(vehicles.map(vehicle => [vehicle.id, vehicle.name]));
const runStateLabels: Record<(typeof runStates)[number], string> = {
  off: 'Parked, engine off', starting: 'While starting', idling: 'Parked, engine running',
  moving: 'While moving (recorded afterwards)', unknown: 'Unknown',
};
const sectionLabels: Record<HandoffSection, string> = {
  vehicle: 'Vehicle context', recentWork: 'Recent work', symptoms: 'Complaint and corrections', checks: 'Checks and outcomes', findings: 'Findings',
};
const orUnknown = (value: string) => value.trim() || 'unknown';
const number = (value: string) => value.trim() === '' ? undefined : Number(value);

function useCommit() {
  const [error, setError] = useState('');
  const commit = (change: (record: ObservationRecord) => ObservationRecord, onDone?: () => void) => {
    try {
      observationStore.update(change);
      setError('');
      onDone?.();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : String(failure));
    }
  };
  return { error, commit };
}

function FormError({ error }: { error: string }) {
  return error ? <p className="observation-error" role="alert">Not recorded: {error}</p> : null;
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  const id = useId();
  return <section className="observation-section" aria-labelledby={id}><h3 id={id}>{title}</h3>{children}</section>;
}

function VehicleContextForm({ vehicle }: { vehicle: Vehicle }) {
  const record = useObservationRecord();
  const saved = record.vehicle;
  const [model, setModel] = useState(saved.catalogVehicleId === 'unknown' ? vehicle.id : saved.catalogVehicleId);
  const [year, setYear] = useState(saved.modelYear === 'unknown' ? '' : String(saved.modelYear));
  const [market, setMarket] = useState(saved.market === 'unknown' ? '' : saved.market);
  const [powertrain, setPowertrain] = useState(saved.powertrain === 'unknown' ? '' : saved.powertrain);
  const [odometer, setOdometer] = useState(saved.odometer === 'unknown' ? '' : String(saved.odometer.value));
  const [unit, setUnit] = useState<'km' | 'mi'>(saved.odometer === 'unknown' ? 'km' : saved.odometer.unit);
  const [onset, setOnset] = useState(saved.onset === 'unknown' ? '' : saved.onset);
  const [recentWork, setRecentWork] = useState(saved.recentWork === 'unknown' ? '' : saved.recentWork);
  const [status, setStatus] = useState('');
  const { error, commit } = useCommit();
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const odometerValue = number(odometer);
    const yearValue = number(year);
    commit(current => updateVehicleContext(current, {
      catalogVehicleId: model as ObservationRecord['vehicle']['catalogVehicleId'],
      modelYear: yearValue ?? 'unknown',
      market: orUnknown(market), powertrain: orUnknown(powertrain),
      odometer: odometerValue === undefined ? 'unknown' : { value: odometerValue, unit },
      onset: orUnknown(onset), recentWork: orUnknown(recentWork),
    }), () => setStatus('Vehicle context updated.'));
  };
  return <form onSubmit={submit} aria-label="Vehicle context">
    <p className="observation-hint">Leave a field blank to record it as Unknown. No VIN, plate, location or photo is needed.</p>
    <label>Model<select value={model} onChange={event => setModel(event.target.value)}>
      <option value="unknown">Unknown or not listed</option>
      {vehicles.map(candidate => <option key={candidate.id} value={candidate.id}>{candidate.name}</option>)}
    </select></label>
    <div className="observation-row">
      <label>Model year<input inputMode="numeric" value={year} onChange={event => setYear(event.target.value)} /></label>
      <label>Market<input value={market} onChange={event => setMarket(event.target.value)} placeholder="e.g. Japan" /></label>
    </div>
    <label>Engine and transmission<input value={powertrain} onChange={event => setPowertrain(event.target.value)} /></label>
    <div className="observation-row">
      <label>Odometer<input inputMode="decimal" value={odometer} onChange={event => setOdometer(event.target.value)} /></label>
      <label>Unit<select value={unit} onChange={event => setUnit(event.target.value as 'km' | 'mi')}>
        <option value="km">km</option><option value="mi">miles</option>
      </select></label>
    </div>
    <label>When did it start?<input value={onset} onChange={event => setOnset(event.target.value)} placeholder="e.g. last week, after rain" /></label>
    <label>Relevant recent work<textarea value={recentWork} onChange={event => setRecentWork(event.target.value)} rows={2} /></label>
    <button type="submit">Save vehicle context</button>
    <FormError error={error} />
    <p className="sr-only" role="status">{status}</p>
  </form>;
}

function SymptomForm() {
  const record = useObservationRecord();
  const [corrects, setCorrects] = useState('');
  const [description, setDescription] = useState('');
  const [warning, setWarning] = useState<'no' | 'unknown' | 'yes'>('unknown');
  const [warningText, setWarningText] = useState('');
  const [warningState, setWarningState] = useState<'steady' | 'flashing' | 'unknown'>('unknown');
  const [runState, setRunState] = useState<(typeof runStates)[number]>('unknown');
  const [selected, setSelected] = useState<(typeof conditions)[number][]>([]);
  const [conditionsUnknown, setConditionsUnknown] = useState(true);
  const { error, commit } = useCommit();
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const input: SymptomInput = {
      description,
      warning: warning === 'yes' ? { shown: 'yes', text: orUnknown(warningText), state: warningState } : { shown: warning },
      runState,
      conditions: conditionsUnknown ? 'unknown' : selected,
      ...(corrects ? { correctsId: corrects } : {}),
    };
    commit(current => addSymptom(current, input, new Date()), () => { setDescription(''); setCorrects(''); });
  };
  return <form onSubmit={submit} aria-label="Symptom observation">
    {record.symptoms.length > 0 && <label>This entry is
      <select value={corrects} onChange={event => setCorrects(event.target.value)}>
        <option value="">A separate complaint</option>
        {record.symptoms.map(symptom => <option key={symptom.id} value={symptom.id}>A correction to {symptom.id}</option>)}
      </select></label>}
    <label>What did you observe?<textarea required value={description} onChange={event => setDescription(event.target.value)} rows={3} /></label>
    <label>Warning light or message shown?<select value={warning} onChange={event => setWarning(event.target.value as typeof warning)}>
      <option value="unknown">Unknown</option><option value="no">No</option><option value="yes">Yes</option>
    </select></label>
    {warning === 'yes' && <div className="observation-row">
      <label>Exact warning text<input value={warningText} onChange={event => setWarningText(event.target.value)} placeholder="Blank = Unknown" /></label>
      <label>Light state<select value={warningState} onChange={event => setWarningState(event.target.value as typeof warningState)}>
        <option value="unknown">Unknown</option><option value="steady">Steady</option><option value="flashing">Flashing</option>
      </select></label>
    </div>}
    <label>Vehicle state when it happened<select value={runState} onChange={event => setRunState(event.target.value as typeof runState)}>
      {runStates.map(state => <option key={state} value={state}>{runStateLabels[state]}</option>)}
    </select></label>
    <fieldset><legend>Conditions</legend>
      <label className="observation-check"><input type="checkbox" checked={conditionsUnknown} onChange={event => setConditionsUnknown(event.target.checked)} />Unknown</label>
      {conditions.map(condition => <label key={condition} className="observation-check"><input type="checkbox" disabled={conditionsUnknown}
        checked={selected.includes(condition)}
        onChange={event => setSelected(event.target.checked ? [...selected, condition] : selected.filter(value => value !== condition))} />{condition}</label>)}
    </fieldset>
    <button type="submit">Record observation</button>
    <FormError error={error} />
  </form>;
}

function CheckForm() {
  const [description, setDescription] = useState('');
  const [value, setValue] = useState('');
  const [unit, setUnit] = useState('');
  const [checkConditions, setCheckConditions] = useState('');
  const [action, setAction] = useState('');
  const [outcome, setOutcome] = useState<(typeof outcomes)[number]>('not-tested');
  const { error, commit } = useCommit();
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const measured = number(value);
    commit(current => addCheck(current, {
      description,
      measurement: measured === undefined ? 'none' : { value: measured, unit: unit.trim() },
      conditions: orUnknown(checkConditions), action: action.trim() || 'none', outcome,
    }, new Date()), () => { setDescription(''); setValue(''); setAction(''); });
  };
  return <form onSubmit={submit} aria-label="Check and outcome">
    <label>Check performed<input required value={description} onChange={event => setDescription(event.target.value)} /></label>
    <div className="observation-row">
      <label>Measurement (optional)<input inputMode="decimal" value={value} onChange={event => setValue(event.target.value)} /></label>
      <label>Unit<input value={unit} onChange={event => setUnit(event.target.value)} placeholder="e.g. kPa" /></label>
    </div>
    <label>Conditions<input value={checkConditions} onChange={event => setCheckConditions(event.target.value)} placeholder="Blank = Unknown" /></label>
    <label>Action attempted<input value={action} onChange={event => setAction(event.target.value)} placeholder="Blank = None" /></label>
    <label>Outcome<select value={outcome} onChange={event => setOutcome(event.target.value as typeof outcome)}>
      {outcomes.map(candidate => <option key={candidate} value={candidate}>{candidate}</option>)}
    </select></label>
    <button type="submit">Record check</button>
    <FormError error={error} />
  </form>;
}

function FindingForm() {
  const [kind, setKind] = useState<'observed' | 'suspected' | 'confirmed'>('observed');
  const [text, setText] = useState('');
  const [confirmedBy, setConfirmedBy] = useState('');
  const { error, commit } = useCommit();
  const submit = (event: FormEvent) => {
    event.preventDefault();
    commit(current => addFinding(current, kind === 'confirmed' ? { kind, text, confirmedBy } : { kind, text }), () => setText(''));
  };
  return <form onSubmit={submit} aria-label="Finding">
    <label>Type<select value={kind} onChange={event => setKind(event.target.value as typeof kind)}>
      <option value="observed">Observed fact</option><option value="suspected">Suspected cause (unconfirmed)</option>
      <option value="confirmed">Confirmed finding</option>
    </select></label>
    <label>Finding<input required value={text} onChange={event => setText(event.target.value)} /></label>
    {kind === 'confirmed' && <label>Confirmed by<input required value={confirmedBy} onChange={event => setConfirmedBy(event.target.value)} placeholder="e.g. workshop name" /></label>}
    <button type="submit">Record finding</button>
    <FormError error={error} />
  </form>;
}

function Entries() {
  const record = useObservationRecord();
  const { error, commit } = useCommit();
  const entries = [
    ...record.symptoms.map(entry => ({ id: entry.id, text: `${entry.correctsId ? `Correction to ${entry.correctsId}` : 'Complaint'}: ${entry.description}` })),
    ...record.checks.map(entry => ({ id: entry.id, text: `Check: ${entry.description} (${entry.outcome})` })),
    ...record.findings.map(entry => ({ id: entry.id, text: `Finding (${entry.kind}): ${entry.text}` })),
  ];
  if (!entries.length) return <p className="observation-hint">Nothing recorded yet.</p>;
  return <><ul className="observation-entries">{entries.map(entry => <li key={entry.id}>
    <span><strong>{entry.id}</strong> {entry.text}</span>
    <button onClick={() => commit(current => removeEntry(current, entry.id))} aria-label={`Remove ${entry.id}`}>Remove</button>
  </li>)}</ul><FormError error={error} /></>;
}

function Handoff() {
  const record = useObservationRecord();
  const [include, setInclude] = useState<Set<HandoffSection>>(() => new Set(handoffSections));
  const [copyStatus, setCopyStatus] = useState('');
  const text = formatHandoff(record, { include, vehicleNames, generatedAt: new Date() });
  const copy = async () => {
    try {
      if (!navigator.clipboard) throw new Error('Clipboard access is not available in this browser');
      await navigator.clipboard.writeText(text);
      setCopyStatus('Handoff copied.');
    } catch (failure) {
      setCopyStatus(`Copy failed: ${failure instanceof Error ? failure.message : String(failure)}. Select the preview text and copy it manually.`);
    }
  };
  return <>
    <fieldset><legend>Include in the handoff</legend>
      {handoffSections.map(section => <label key={section} className="observation-check"><input type="checkbox" checked={include.has(section)}
        onChange={event => {
          const next = new Set(include);
          if (event.target.checked) next.add(section); else next.delete(section);
          setInclude(next);
        }} />{sectionLabels[section]}</label>)}
    </fieldset>
    <pre className="handoff-preview" aria-label="Handoff preview" tabIndex={0}>{text}</pre>
    <div className="observation-actions">
      <button onClick={copy}>Copy handoff text</button>
      <button onClick={() => window.print()}>Print handoff</button>
    </div>
    <p role="status" className="observation-hint">{copyStatus}</p>
  </>;
}

export function ObservationLog({ vehicle, onClose }: { vehicle: Vehicle; onClose: () => void }) {
  return <aside className="repair-guide observation-log glass" aria-label="Observation log and mechanic handoff">
    <div className="panel-heading repair-heading">
      <span className="eyebrow">OBSERVATION LOG</span>
      <IconButton label="Close observation log" onClick={onClose}><X /></IconButton>
    </div>
    <h2>Record what you observed</h2>
    <div className="repair-safety">Record only while parked safely. Never use this while driving.</div>
    <p className="observation-hint"><strong>Kept only in this browser tab.</strong> Nothing is saved or uploaded; reloading or closing the tab discards the log. Entries record observations, not a diagnosis.</p>
    <Section title="Vehicle context"><VehicleContextForm vehicle={vehicle} /></Section>
    <Section title="Complaint or correction"><SymptomForm /></Section>
    <Section title="Check and outcome"><CheckForm /></Section>
    <Section title="Finding"><FindingForm /></Section>
    <Section title="Recorded entries"><Entries /></Section>
    <Section title="Mechanic handoff preview"><Handoff /></Section>
  </aside>;
}
