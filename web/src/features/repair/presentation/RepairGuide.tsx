import { useLayoutEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { IconButton } from '../../../components/IconButton';
import type { Vehicle } from '../../../domain/catalog';
import { advanceWiperGuide, wiperSource, wiperSteps, type WiperStepId } from '../domain/wiperGuide';

export function RepairGuide({ vehicle, onLocate, onClose }: {
  vehicle: Vehicle; onLocate?: (itemId: string, isolate?: boolean) => void; onClose: () => void;
}) {
  const [history, setHistory] = useState<WiperStepId[]>(['prepare']);
  const heading = useRef<HTMLHeadingElement>(null);
  const stepId = history[history.length - 1];
  const step = wiperSteps[stepId];
  useLayoutEffect(() => { heading.current?.focus(); }, [stepId]);
  const advance = (choice: number) => {
    const next = advanceWiperGuide(stepId, choice);
    setHistory(previous => [...previous, next]);
    onLocate?.(wiperSteps[next].target);
  };
  const back = () => {
    const previous = history.slice(0, -1);
    setHistory(previous);
    onLocate?.(wiperSteps[previous[previous.length - 1]].target);
  };
  return <aside className="repair-guide glass" aria-label="Wiper troubleshooting guide">
    <div className="panel-heading repair-heading">
      <span className="eyebrow">SYMPTOM TO ACTION</span>
      <IconButton label="Close troubleshooting" onClick={onClose}><X /></IconButton>
    </div>
    <h2>Wipers leave streaks</h2>
    <p className="repair-applicability">{vehicle.year} {vehicle.name} · {vehicle.market}</p>
    <p><strong>General possible failure, not a documented model-specific defect.</strong> This guided check applies to conventional windshield wipers. Blade fitment and access must be confirmed for your actual car.</p>
    <div className="repair-safety">Do not drive with impaired visibility. Inspect only when parked with the system off. Never infer attachment details from the 3D model.</div>
    <div className="repair-step">
      <p className="eyebrow">CHECK → REMEDY → VERIFY · {history.length - 1} observations recorded</p>
      <h3 ref={heading} tabIndex={-1}>{step.title}</h3>
      <p>{step.instruction}</p>
      {onLocate && <>
        <button className="repair-locate" onClick={() => onLocate(step.target)}>Locate this check in 3D</button>
        <button className="repair-locate" onClick={() => onLocate('body', false)}>Show windshield location on car</button>
      </>}
      <div className="repair-choices" aria-label="Choose your observation">
        {step.choices.map((choice, index) =>
          <button key={choice.label} onClick={() => advance(index)}>{choice.label}</button>)}
      </div>
    </div>
    <div className="repair-navigation">
      {history.length > 1 && <button onClick={back}>Previous observation</button>}
      <button onClick={() => { setHistory(['prepare']); onLocate?.('wipers'); }}>Restart check</button>
    </div>
    <details className="repair-evidence">
      <summary>Your observation trail ({history.length - 1})</summary>
      <ol>{history.slice(0, -1).map((id, index) => {
        const next = history[index + 1];
        const choice = wiperSteps[id].choices.find(candidate => candidate.next === next);
        return <li key={`${index}-${id}`}>{choice?.label}</li>;
      })}</ol>
    </details>
    <details>
      <summary>Understand the illustration and sources</summary>
      {onLocate && <p>Silver: arm. Amber: blade carrier. Dark strip: rubber contact edge. Separate left/right occurrences can be inspected using the component index. The connector and proportions are generic, not verified Raize or Impala service geometry.</p>}
      <p>Possible contributors: dirty glass/blades, worn rubber, incorrect fit or an arm/system problem. This guide does not establish a diagnosis or a model-specific fault rate.</p>
      <p><a href={wiperSource.url} target="_blank" rel="noreferrer">{wiperSource.title}</a> supports cleaning, wet-glass testing, fitment checks and careful handling. Reviewed {wiperSource.reviewed}. Exact access and replacement instructions remain with the vehicle manual and blade supplier.</p>
    </details>
  </aside>;
}
