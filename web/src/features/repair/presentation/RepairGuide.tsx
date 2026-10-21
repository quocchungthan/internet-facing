import { useLayoutEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { IconButton } from '../../../components/IconButton';
import type { Vehicle } from '../../../domain/catalog';
import {
  advanceWiperGuide, compareWiperObservations, outcomeLabels, wiperOutcome, wiperPatterns, wiperReference, wiperSteps,
  type WiperObservation, type WiperStepId, type WiperTrailEntry,
} from '../domain/wiperGuide';
import { officialRecallLookups, resolveApplicability } from '../domain/guideReference';
import { observationStore } from '../../observations/application/observationStore';
import { addCheck } from '../../observations/domain/observationRecord';

function ObservationForm({ label, value, onChange }: {
  label: string; value: WiperObservation | null; onChange: (value: WiperObservation) => void;
}) {
  const current = value ?? { patterns: [], side: 'unknown', parkedWetGlass: false };
  return <fieldset className="wiper-observation">
    <legend>{label}</legend>
    {wiperPatterns.map(pattern => <label key={pattern} className="observation-check"><input type="checkbox" checked={current.patterns.includes(pattern)}
      onChange={event => onChange({
        ...current, patterns: event.target.checked ? [...current.patterns, pattern] : current.patterns.filter(item => item !== pattern),
      })} />{pattern}</label>)}
    <label>Side<select value={current.side} onChange={event => onChange({ ...current, side: event.target.value as WiperObservation['side'] })}>
      <option value="unknown">Unknown</option><option value="driver">Driver side</option>
      <option value="passenger">Passenger side</option><option value="both">Both sides</option>
    </select></label>
    <label className="observation-check"><input type="checkbox" checked={current.parkedWetGlass}
      onChange={event => onChange({ ...current, parkedWetGlass: event.target.checked })} />Seen while parked, on wet glass, with the washer test</label>
  </fieldset>;
}

const stepAfter = (trail: readonly WiperTrailEntry[]): WiperStepId =>
  trail.length ? advanceWiperGuide(trail[trail.length - 1].step, trail[trail.length - 1].choice) : 'prepare';

export function RepairGuide({ vehicle, onLocate, onClose }: {
  vehicle: Vehicle; onLocate?: (itemId: string, isolate?: boolean) => void; onClose: () => void;
}) {
  const [trail, setTrail] = useState<WiperTrailEntry[]>([]);
  const [initial, setInitial] = useState<WiperObservation | null>(null);
  const [result, setResult] = useState<WiperObservation | null>(null);
  const [logStatus, setLogStatus] = useState('');
  const heading = useRef<HTMLHeadingElement>(null);
  const stepId = stepAfter(trail);
  const step = wiperSteps[stepId];
  const outcome = wiperOutcome(trail);
  const comparison = compareWiperObservations(initial, result);
  const applicability = resolveApplicability(wiperReference, vehicle, new Date());
  const isVerification = stepId === 'verifyClean' || stepId === 'verifyReplace';
  useLayoutEffect(() => { heading.current?.focus(); }, [stepId, trail.length]);
  const advance = (choice: number) => {
    const next = advanceWiperGuide(stepId, choice);
    setTrail(previous => [...previous, { step: stepId, choice }]);
    setLogStatus('');
    onLocate?.(wiperSteps[next].target);
  };
  const back = () => {
    const previous = trail.slice(0, -1);
    setTrail(previous);
    setLogStatus('');
    onLocate?.(wiperSteps[stepAfter(previous)].target);
  };
  const addToLog = () => {
    if (!outcome) return;
    try {
      observationStore.update(record => addCheck(record, {
        description: `Wiper streak check: ${trail.map(entry => wiperSteps[entry.step].choices[entry.choice].label).join(' → ')}`,
        measurement: 'none',
        conditions: comparison.comparable ? `Parked, wet glass. ${comparison.summary}` : 'unknown',
        action: trail.some(entry => entry.step === 'replace' && entry.choice === 0) ? 'Blades replaced per instructions'
          : trail.some(entry => entry.step === 'clean' && entry.choice === 0) ? 'Cleaned glass and blades' : 'none',
        outcome,
      }, new Date()));
      setLogStatus('Added to the observation log.');
    } catch (failure) {
      setLogStatus(`Not added: ${failure instanceof Error ? failure.message : String(failure)}`);
    }
  };
  return <aside className="repair-guide glass" aria-label="Wiper troubleshooting guide">
    <div className="panel-heading repair-heading">
      <span className="eyebrow">SYMPTOM TO ACTION</span>
      <IconButton label="Close troubleshooting" onClick={onClose}><X /></IconButton>
    </div>
    <h2>Wipers leave streaks</h2>
    <p className="repair-applicability">{vehicle.year} {vehicle.name} · {vehicle.market}</p>
    <p className="guide-label" data-applicability={applicability.kind}>{applicability.label}{applicability.stale ? ' · source review overdue' : ''}</p>
    <p><strong>General possible failure, not a documented model-specific defect.</strong> This guided check applies to conventional windshield wipers. Blade fitment and access must be confirmed for your actual car.</p>
    <div className="repair-safety">Do not drive with impaired visibility. Inspect only when parked with the system off. Never infer attachment details from the 3D model.</div>
    <div className="repair-step">
      <p className="eyebrow">CHECK → REMEDY → VERIFY · {trail.length} observations recorded</p>
      <h3 ref={heading} tabIndex={-1}>{step.title}</h3>
      <p>{step.instruction}</p>
      {onLocate && <>
        <button className="repair-locate" onClick={() => onLocate(step.target)}>Locate this check in 3D</button>
        <button className="repair-locate" onClick={() => onLocate('body', false)}>Show windshield location on car</button>
      </>}
      {stepId === 'prepare' && <ObservationForm label="Initial observation (optional, before any check)" value={initial} onChange={setInitial} />}
      {isVerification && <ObservationForm label="Result observation (same parked, wet-glass test)" value={result} onChange={setResult} />}
      <div className="repair-choices" aria-label="Choose your observation">
        {step.choices.map((choice, index) =>
          <button key={choice.label} onClick={() => advance(index)}>{choice.label}</button>)}
      </div>
      {outcome && step.choices.length === 0 && <div className="wiper-outcome" aria-label="Check outcome" role="group">
        <p><strong>Outcome:</strong> {outcomeLabels[outcome]}</p>
        <p>{comparison.comparable ? comparison.summary : `Before and after are not comparable. ${comparison.reason}`}</p>
        <button className="repair-locate" onClick={addToLog}>Add this check to the observation log</button>
        <p role="status">{logStatus}</p>
      </div>}
    </div>
    <div className="repair-navigation">
      {trail.length > 0 && <button onClick={back}>Previous observation</button>}
      <button onClick={() => { setTrail([]); setResult(null); setLogStatus(''); onLocate?.('wipers'); }}>Restart check</button>
    </div>
    <details className="repair-evidence">
      <summary>Your observation trail ({trail.length})</summary>
      <ol>{trail.map((entry, index) => <li key={`${index}-${entry.step}`}>{wiperSteps[entry.step].choices[entry.choice].label}</li>)}</ol>
    </details>
    <section className="guide-reference" aria-label="Guide source and applicability">
      <h3>Source and applicability</h3>
      <dl>
        <dt>Source</dt><dd><a href={wiperReference.source.url} target="_blank" rel="noreferrer">{wiperReference.source.title}</a></dd>
        <dt>Scope</dt><dd>{wiperReference.scope}</dd>
        <dt>Reviewed</dt><dd>{wiperReference.reviewed}</dd>
        <dt>Applies to</dt><dd>{applicability.detail}</dd>
      </dl>
      <p>No recurring defect is claimed for the {vehicle.name}; none is inferred from brand reputation or complaints.</p>
      <h4>Official recall lookup (external)</h4>
      <p>30s does not check recalls and has no recall result for your vehicle. These links open official services in a new tab; use the one for the country where the vehicle is registered.</p>
      <ul>{officialRecallLookups.map(lookup => <li key={lookup.url}>
        <a href={lookup.url} target="_blank" rel="noreferrer">{lookup.title}</a> ({lookup.jurisdiction}, external site)
      </li>)}</ul>
    </section>
    <details>
      <summary>Understand the illustration</summary>
      {onLocate && <p>Silver: arm. Amber: blade carrier. Dark strip: rubber contact edge. Separate left/right occurrences can be inspected using the component index. The connector and proportions are generic, not verified Raize or Impala service geometry.</p>}
      <p>Possible contributors: dirty glass/blades, worn rubber, incorrect fit or an arm/system problem. This guide does not establish a diagnosis or a model-specific fault rate.</p>
      <p>Exact access and replacement instructions remain with the vehicle manual and blade supplier.</p>
    </details>
  </aside>;
}
