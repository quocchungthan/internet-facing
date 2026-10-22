import { useId, useLayoutEffect, useRef, useState } from 'react';
import type { Vehicle } from '../../../domain/catalog';
import type { ItemTree } from '../../exploration/domain/items';
import { chatGptQuestionUrl, componentQuestion } from '../domain/componentQuestion';

export function ComponentQuestion({ vehicle, tree, itemId }: { vehicle: Vehicle; tree: ItemTree; itemId: string }) {
  return <QuestionEditor key={`${vehicle.id}:${itemId}`} initialQuestion={componentQuestion(vehicle, tree, itemId)} />;
}

function QuestionEditor({ initialQuestion }: { initialQuestion: string }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const opener = useRef<HTMLButtonElement>(null);
  const id = useId();
  const [open, setOpen] = useState(false);
  const [question, setQuestion] = useState(initialQuestion);
  const [notice, setNotice] = useState('');
  useLayoutEffect(() => {
    if (open) dialog.current?.showModal();
  }, [open]);
  const close = () => {
    dialog.current?.close();
    setOpen(false);
    opener.current?.focus();
  };
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(question);
      setNotice('Question copied.');
    } catch (error) {
      setNotice(`Could not copy question: ${error instanceof Error ? error.message : String(error)}. Select the question text and copy it manually.`);
    }
  };
  return <div className="component-question">
    <button className="question-trigger secondary-button" ref={opener} onClick={() => { setNotice(''); setOpen(true); }}>Ask ChatGPT about this component</button>
    {open && <dialog ref={dialog} className="question-dialog onboarding-dialog" aria-labelledby={`${id}-title`}
      aria-describedby={`${id}-privacy`}
      onCancel={event => { event.preventDefault(); close(); }}
      onKeyDown={event => { if (event.key === 'Escape') event.stopPropagation(); }}>
      <h2 id={`${id}-title`}>Ask about this component</h2>
      <p id={`${id}-privacy`}>ChatGPT is an external service with its own account and privacy terms. Only public catalog context is included. Do not add VINs, registration, private observations, notes or other personal information. Your question may remain in browser and service history.</p>
      <label htmlFor={`${id}-question`}>Preview or edit question</label>
      <textarea id={`${id}-question`} value={question} rows={8} maxLength={4000}
        onChange={event => { setQuestion(event.target.value); setNotice(''); }} />
      <p>AI answers can be inaccurate. This is educational, not validated repair guidance or permission to drive. Opening the link may prefill the question; login or browser behavior can change. Copy the question if it is not preserved. Nothing is sent until you choose Open ChatGPT.</p>
      <div className="onboarding-actions">
        <button onClick={close}>Close</button>
        <button disabled={!question.trim()} onClick={copy}>Copy question</button>
        {question.trim() && <a className="primary-button" href={chatGptQuestionUrl(question)} target="_blank" rel="noreferrer">Open ChatGPT (external)</a>}
      </div>
      {notice && <p role="status">{notice}</p>}
    </dialog>}
  </div>;
}
