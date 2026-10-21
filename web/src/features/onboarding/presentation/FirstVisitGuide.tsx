import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { IconButton } from '../../../components/IconButton';
import { features } from '../../../config/features';
import {
  browserStorage, onboardingSteps, readOnboardingStatus, writeOnboardingStatus, type OnboardingStep,
} from '../domain/onboarding';

const targetSelector = (target: string) => `[data-onboarding="${target}"]`;

function presentSteps(): OnboardingStep[] {
  return onboardingSteps(features).filter(step => !step.target || document.querySelector(targetSelector(step.target)));
}

/** Local first-visit introduction. `replay` increments to reopen it explicitly. */
export function FirstVisitGuide({ replay }: { replay: number }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const opener = useRef<Element | null>(null);
  const [steps, setSteps] = useState<OnboardingStep[]>([]);
  const [index, setIndex] = useState(0);
  const [notice, setNotice] = useState('');
  const open = steps.length > 0;

  const start = useCallback(() => {
    const available = presentSteps();
    if (!available.length) return;
    opener.current = document.activeElement;
    setIndex(0);
    setSteps(available);
  }, []);

  useEffect(() => {
    if (replay > 0) { start(); return; }
    const status = readOnboardingStatus(browserStorage());
    if (status === 'new' || status === 'unavailable') {
      const frame = window.requestAnimationFrame(start);
      return () => window.cancelAnimationFrame(frame);
    }
  }, [replay, start]);

  useLayoutEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    heading.current?.focus();
  }, [open, index]);

  const step = steps[index];
  useEffect(() => {
    if (!step?.target) return;
    const target = document.querySelector(targetSelector(step.target));
    target?.classList.add('onboarding-target-active');
    return () => target?.classList.remove('onboarding-target-active');
  }, [step]);

  const finish = (status: 'completed' | 'dismissed') => {
    dialog.current?.close();
    setSteps([]);
    const result = writeOnboardingStatus(browserStorage(), status);
    setNotice(result.ok ? '' : result.message);
    if (opener.current instanceof HTMLElement && opener.current.isConnected) opener.current.focus();
  };

  return <>
    {open && step && <dialog ref={dialog} className="onboarding-dialog" aria-labelledby="onboarding-title" aria-describedby="onboarding-body"
      onCancel={event => { event.preventDefault(); finish('dismissed'); }}
      onKeyDown={event => { if (event.key === 'Escape') event.stopPropagation(); }}>
      <div className="panel-heading">
        <span className="eyebrow">INTRODUCTION · {index + 1} OF {steps.length}</span>
        <IconButton label="Skip introduction" onClick={() => finish('dismissed')}><X /></IconButton>
      </div>
      <h2 id="onboarding-title" ref={heading} tabIndex={-1}>{step.title}</h2>
      <p id="onboarding-body">{step.body}</p>
      <div className="onboarding-actions">
        <button onClick={() => finish('dismissed')}>Skip</button>
        {index > 0 && <button onClick={() => setIndex(index - 1)}>Back</button>}
        {index < steps.length - 1
          ? <button className="primary-button" onClick={() => setIndex(index + 1)}>Next</button>
          : <button className="primary-button" onClick={() => finish('completed')}>Finish</button>}
      </div>
    </dialog>}
    {notice && <div className="onboarding-notice glass" role="status">
      <span>{notice}</span>
      <IconButton label="Dismiss introduction notice" onClick={() => setNotice('')}><X /></IconButton>
    </div>}
  </>;
}
