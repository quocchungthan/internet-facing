import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import { ArrowDownToLine, ArrowLeft, ArrowRight, ArrowUpRight, BookOpen, ChevronLeft, ChevronRight, ShieldAlert, X } from 'lucide-react';
import type { Part, Vehicle } from '../domain/catalog';
import { engineLessons } from '../content';
import { lessonFor } from '../domain/engineLessons';
import { itemLabel, type Item, type ItemTree } from '../features/exploration/domain/items';
import { IconButton } from './IconButton';
import { ReferencePhoto } from './ReferencePhoto';

type Props = {
  vehicle: Vehicle; part?: Part; onSelect: (p?: Part) => void; tour?: boolean;
  onTourStep?: (delta: number) => void; onEndTour?: () => void;
  openBook: boolean; onBookOpened: () => void;
  subject?: { item: Item; tree: ItemTree; onSelectItem: (id: string) => void };
};
const fourStrokeSteps = [
  { name: 'Intake', description: 'Intake open for entry; piston down.' },
  { name: 'Compression', description: 'Both valves closed; piston up.' },
  { name: 'Power', description: 'Expanding gas drives the piston down.' },
  { name: 'Exhaust', description: 'Exhaust outlet open; piston up.' },
];

export function DetailPanel(props: Props) {
  if (props.subject) return <SubjectLessonPanel {...props} subject={props.subject} />;
  if (!props.part) return null;
  return <PartDetailPanel {...props} part={props.part} />;
}

function PartDetailPanel({ vehicle, part, onSelect, tour, onTourStep, onEndTour, openBook, onBookOpened }: Props & { part: Part }) {
  const [tab, setTab] = useState<'learn' | 'book' | 'sources'>('learn');
  const [slide, setSlide] = useState(0);
  const [book, setBook] = useState('');
  const [bookError, setBookError] = useState('');
  const heading = useRef<HTMLHeadingElement>(null);
  const scroller = useRef<HTMLDivElement>(null);
  useEffect(() => {
    setSlide(0);
    setTab('learn');
    scroller.current?.scrollTo(0, 0);
    heading.current?.focus({ preventScroll: true });
  }, [part.id, vehicle.id]);
  useEffect(() => {
    if (openBook) { setTab('book'); onBookOpened(); }
  }, [openBook, onBookOpened]);
  useEffect(() => {
    if (tab !== 'book') return;
    const controller = new AbortController();
    setBook('');
    setBookError('');
    fetch(vehicle.book, { signal: controller.signal }).then(response => {
      if (!response.ok) throw new Error(`Book request returned ${response.status}`);
      return response.text();
    }).then(text => setBook(text)).catch(error => {
      if (error instanceof Error && error.name === 'AbortError') return;
      setBookError(error instanceof Error ? error.message : String(error));
    });
    return () => controller.abort();
  }, [tab, vehicle.book]);
  const index = vehicle.parts.findIndex(p => p.id === part.id);
  const photos = vehicle.photos.filter(p => part.photoIds.includes(p.id));
  const slides = [
    { title: 'How it works', body: <p>{part.description}</p> },
    { title: 'A thoughtful check', body: <ul className="check-list">{part.checks.map(check => <li key={check}>{check}</li>)}</ul> },
    { title: 'Listen to the signs', body: <><ul className="symptom-list">{part.symptoms.map(symptom => <li key={symptom}>{symptom}</li>)}</ul><p className="muted">A symptom is a starting point, not a diagnosis. Check the complete system before replacing a part.</p></> },
  ];
  return <aside className="detail-panel glass" aria-label={`${part.label} details`}>
    <div className="detail-top"><span className="eyebrow">{part.system} / {String(index + 1).padStart(2, '0')}</span>
      <IconButton label="Close part details" onClick={() => { onEndTour?.(); onSelect(); }}><X /></IconButton></div>
    <h2 tabIndex={-1} ref={heading}>{part.label}</h2>
    <p className="detail-summary">{part.summary}</p>
    <div className="detail-tabs" role="group" aria-label="Part information">
      <button aria-pressed={tab === 'learn'} onClick={() => setTab('learn')}>Explore</button>
      <button aria-pressed={tab === 'book'} onClick={() => setTab('book')}>Field book</button>
      <button aria-pressed={tab === 'sources'} onClick={() => setTab('sources')}>References <ArrowUpRight size={12} /></button>
    </div>
    <div className="detail-scroll" ref={scroller}>
      {tab === 'learn' && <>
        <section className="lesson">
          <div className="lesson-header"><span className="eyebrow">IN FOCUS</span><span className="mono">{slide + 1} / {slides.length}</span></div>
          <h3>{slides[slide].title}</h3>
          <div className="slide-content" aria-live="polite">{slides[slide].body}</div>
          <div className="slide-controls">
            <div className="slide-dots">{slides.map((s, i) => <button key={s.title} aria-label={`Slide ${i + 1}: ${s.title}`} aria-pressed={i === slide} onClick={() => setSlide(i)} />)}</div>
            <IconButton label="Previous lesson slide" disabled={slide === 0} onClick={() => setSlide(slide - 1)}><ChevronLeft /></IconButton>
            <IconButton label="Next lesson slide" disabled={slide === slides.length - 1} onClick={() => setSlide(slide + 1)}><ChevronRight /></IconButton>
          </div>
        </section>
        <div className="safety-note"><ShieldAlert size={18} /><div><strong>Know your limits</strong><p>{part.caution}</p></div></div>
        {photos.map(photo => <ReferencePhoto key={photo.id} photo={photo} />)}
        {!photos.length && <p className="image-disclosure">No verified part-specific photograph is included for this component. The 3D view is schematic, not an OEM blueprint.</p>}
        <div className="related-parts"><span className="eyebrow">CONNECTED TO</span><div>{part.relatedIds.map(id => {
          const related = vehicle.parts.find(p => p.id === id)!;
          return <button key={id} onClick={() => onSelect(related)}>{related.label}<ArrowUpRight size={13} /></button>;
        })}</div></div>
        <button className="text-action" onClick={() => setTab('book')}><BookOpen size={16} /> Read the complete field book <ArrowRight size={16} /></button>
      </>}
      {tab === 'book' && <div className="book-content">
        <a className="text-action" href={vehicle.book} download><ArrowDownToLine size={16} /> Download original Markdown</a>
        {bookError && <p role="alert" className="error-message">{bookError}</p>}
        {!book && !bookError && <p role="status">Opening field book…</p>}
        <ReactMarkdown components={{
          a: ({ href, children }) => {
            const url = href ? new URL(href, window.location.origin) : null;
            const id = url?.searchParams.get('part') ?? (href?.startsWith('#') ? href.slice(1) : undefined);
            const target = vehicle.parts.find(p => p.id === id);
            return target ? <button className="inline-link" onClick={() => onSelect(target)}>{children}</button>
              : <a href={href} target="_blank" rel="noreferrer">{children}</a>;
          },
          h2: ({ children }) => <h2 id={vehicle.parts.find(p => p.label === String(children))?.id}>{children}</h2>,
        }}>{book}</ReactMarkdown>
      </div>}
      {tab === 'sources' && <>
        <p className="muted">Model facts and general maintenance principles are distinct. Exact service specifications must come from the manual for the actual vehicle.</p>
        {vehicle.sources.filter(s => part.sourceIds.includes(s.id)).map(s => <a key={s.id} className="source-card" href={s.url} target="_blank" rel="noreferrer">
          <span><strong>{s.title}</strong><small>{s.scope}</small></span><ArrowUpRight size={16} />
        </a>)}
        <h3>Vehicle reference photographs</h3>
        {vehicle.photos.map(photo => <ReferencePhoto key={photo.id} photo={photo} />)}
        <p className="image-disclosure">Original educational text and procedural 3D illustrations by 30s. No manufacturer affiliation. All photographs retain their individual licenses; credits and differences are shown beside each image.</p>
      </>}
    </div>
    {tour && <div className="tour-footer">
      <IconButton label="Previous tour part" disabled={index === 0} onClick={() => onTourStep?.(-1)}><ArrowLeft /></IconButton>
      <span>GUIDED WALKTHROUGH <strong>{index + 1} / {vehicle.parts.length}</strong></span>
      {index === vehicle.parts.length - 1 ? <button className="finish-tour" onClick={onEndTour}>Finish</button>
        : <IconButton label="Next tour part" onClick={() => onTourStep?.(1)}><ArrowRight /></IconButton>}
    </div>}
  </aside>;
}

function SubjectLessonPanel({ vehicle, onSelect, subject }: Props & {
  subject: { item: Item; tree: ItemTree; onSelectItem: (id: string) => void };
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    heading.current?.focus({ preventScroll: true });
  }, [subject.item.id, vehicle.id]);
  const lookup = lessonFor(engineLessons, vehicle.id, subject.item.id);
  return <aside className="detail-panel glass" aria-label={`${itemLabel(subject.tree, subject.item)} learning`}>
    <div className="detail-top"><span className="eyebrow">SELECTED COMPONENT / SOURCES</span>
      <IconButton label="Close part details" onClick={() => onSelect()}><X /></IconButton></div>
    <h2 tabIndex={-1} ref={heading}>{itemLabel(subject.tree, subject.item)}</h2>
    <div className="detail-scroll">
      {lookup.status === 'ready' ? <>
        <p className="detail-summary">{lookup.lesson.purpose}</p>
        {subject.item.id === 'engine-cylinder-study' && <p className="muted">The chamber is the region above the piston, not a separate manufactured component.</p>}
        <section className="subject-strokes" aria-label="Generic four-stroke sequence">
          <h3>Four strokes · generic sequence</h3>
          <ol>{fourStrokeSteps.map(step => <li key={step.name}><strong>{step.name}</strong> — {step.description}</li>)}</ol>
          <p className="muted">Static teaching states only; exact timing and configuration are not specified.</p>
        </section>
        <section className="related-parts" role="group" aria-label="Connected subjects">
          <span className="eyebrow">CONNECTED TO</span>
          <div>{lookup.lesson.connections.map(connection => {
            const target = subject.tree.items.find(candidate => candidate.id === connection.itemId);
            return target
              ? <button key={connection.itemId} aria-label={`Explore connected subject ${itemLabel(subject.tree, target)}: ${connection.relationship}`}
                onClick={() => subject.onSelectItem(target.id)}>
                <span>{itemLabel(subject.tree, target)}</span><small>{connection.relationship}</small>
              </button>
              : <p key={connection.itemId} role="status">{connection.itemId} is not available in this vehicle.</p>;
          })}</div>
        </section>
        <section className="subject-sources" aria-label="Sources and limitations">
          <h3>Sources and limits</h3>
          <ul>{lookup.lesson.sourceRefs.map(reference => {
            const source = vehicle.sources.find(candidate => candidate.id === reference.sourceId);
            return source
              ? <li key={`${reference.sourceId}-${reference.locator}`}>
                <a href={source.url} target="_blank" rel="noreferrer">{source.title}</a>
                <p>{reference.locator}</p>
                <small>Retrieved {reference.retrievedOn}. {source.scope}</small>
              </li>
              : <li key={reference.sourceId} role="status">Source record {reference.sourceId} is unavailable for this vehicle.</li>;
          })}</ul>
          <p>{lookup.lesson.limitations}</p>
          <p className="image-disclosure">Original teaching text and independently authored representative geometry; no OEM geometry or source artwork.</p>
        </section>
      </> : <p role="status">{lookup.reason}</p>}
      <div className="safety-note"><ShieldAlert size={18} /><div><strong>Educational use only</strong>
        <p>Representative cutaway, not OEM geometry, service access or repair instructions. Verify the actual vehicle and its correct manual; safety-critical work belongs with a qualified professional.</p>
      </div></div>
    </div>
  </aside>;
}
