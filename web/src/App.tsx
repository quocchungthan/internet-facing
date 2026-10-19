import { lazy, Suspense, useCallback, useEffect, useState } from 'react';
import { ArrowLeft, ArrowRight, ArrowUpRight, BookOpen, Camera, Check, ChevronDown, CircleHelp, DoorOpen, Expand, Eye, EyeOff, Layers3, ListFilter, Maximize2, Minus, MousePointer2, Plus, RotateCcw, Share2, Sparkles, X } from 'lucide-react';
import { vehicles } from './content';
import { useWorkshop } from './hooks/useWorkshop';
import { useReducedMotion } from './hooks/useReducedMotion';
import { IconButton } from './components/IconButton';
import { PartIndex } from './components/PartIndex';
import { DetailPanel } from './components/DetailPanel';
import type { CameraAction } from './scene/WorkshopScene';
import type { ViewMode } from './domain/catalog';
import './styles.css';

const WorkshopScene = lazy(() => import('./scene/WorkshopScene'));
const modes: { id: ViewMode; label: string; icon: typeof Eye }[] = [
  { id: 'exterior', label: 'Exterior', icon: Eye },
  { id: 'xray', label: 'X-ray', icon: Layers3 },
  { id: 'exploded', label: 'Exploded', icon: Expand },
];

export default function App() {
  const workshop = useWorkshop(vehicles);
  const { vehicle, part, drawer, notice } = workshop;
  const [action, setAction] = useState<CameraAction>();
  const [copied, setCopied] = useState(false);
  const [openBook, setOpenBook] = useState(false);
  const reducedMotion = useReducedMotion();
  const camera = (kind: CameraAction['kind']) => setAction(previous => ({ id: (previous?.id ?? 0) + 1, kind }));
  const reportError = useCallback((message: string) => workshop.setNotice(message), [workshop.setNotice]);
  useEffect(() => {
    const escape = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      if (drawer) workshop.setDrawer(null);
      else { workshop.selectPart(); workshop.setTour(false); }
    };
    window.addEventListener('keydown', escape);
    return () => window.removeEventListener('keydown', escape);
  });
  const share = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      reportError(`Could not copy link: ${error instanceof Error ? error.message : String(error)}. Copy the address from your browser.`);
    }
  };
  return <main className={`workshop ${part ? 'has-detail' : ''}`}>
    <div className="scene" aria-label={`${vehicle.name} interactive schematic`} role="region">
      <Suspense fallback={<div className="scene-loading" role="status"><span className="loader" />Preparing your workshop…</div>}>
        <WorkshopScene vehicle={vehicle} part={part} mode={workshop.mode} open={workshop.open}
          labels={workshop.labels} reducedMotion={reducedMotion} action={action}
          onSelect={workshop.selectPart} onError={reportError} />
      </Suspense>
    </div>
    <div className="scene-vignette" />
    <header className="top-bar">
      <a className="brand" href="/" aria-label="30s workshop home"><span className="brand-mark">30<span>s</span><i /></span><span className="brand-caption">THE EVERYDAY<br />MACHINE, EXPLAINED.</span></a>
      <div className="vehicle-picker glass">
        <span className="picker-led" />
        <div><span className="eyebrow">IN YOUR GARAGE</span><label htmlFor="vehicle-select" className="sr-only">Choose car model</label>
          <select id="vehicle-select" value={vehicle.id} onChange={e => { workshop.selectCar(e.target.value); camera('reset'); }}>
            {vehicles.map(v => <option key={v.id} value={v.id}>{v.year} {v.name}</option>)}
          </select></div><ChevronDown size={15} aria-hidden="true" />
      </div>
      <div className="top-actions glass">
        <IconButton label={copied ? 'Link copied' : 'Copy link to this view'} onClick={share}>{copied ? <Check /> : <Share2 />}</IconButton>
        <IconButton label="Workshop help" aria-expanded={drawer === 'help'} onClick={() => workshop.setDrawer(drawer === 'help' ? null : 'help')}><CircleHelp /></IconButton>
      </div>
    </header>
    <nav className="side-rail glass" aria-label="Workshop tools">
      <IconButton label="Component index" aria-expanded={drawer === 'parts'} onClick={() => workshop.setDrawer(drawer === 'parts' ? null : 'parts')}><ListFilter /></IconButton>
      <span className="rail-divider" />
      <IconButton label="Open hood and doors" aria-pressed={workshop.open} onClick={() => workshop.setOpen(!workshop.open)}><DoorOpen /></IconButton>
      <IconButton label="Show part labels" aria-pressed={workshop.labels} onClick={() => workshop.setLabels(!workshop.labels)}>{workshop.labels ? <Eye /> : <EyeOff />}</IconButton>
      <IconButton label="Capture schematic image" onClick={() => camera('capture')}><Camera /></IconButton>
      <span className="rail-divider" />
      <IconButton label="Open field book" onClick={() => { workshop.selectPart(vehicle.parts[0]); setOpenBook(true); }}><BookOpen /></IconButton>
    </nav>
    {!part && !drawer && <section className="scene-intro">
      <div className="eyebrow intro-tag"><span /> AN OPEN GARAGE FOR CURIOUS MINDS</div>
      <h1>Know your<br /><em>machine.</em></h1>
      <p>Every part has a purpose.<br />Take a closer look at yours.</p>
      <button className="primary-button" onClick={workshop.startTour}>Take a guided look <ArrowUpRight size={17} /></button>
    </section>}
    <section className="vehicle-caption" aria-label="Selected vehicle">
      <div className="eyebrow">EXPLORE / {vehicle.year}</div>
      <h2>{vehicle.name}<span>{vehicle.subtitle}</span></h2>
      <p>{vehicle.character}</p>
    </section>
    <div className="viewport-label"><span className="status-dot" /> LIVE 3D <span className="muted">/</span> SCHEMATIC STUDY</div>
    <div className="camera-controls glass" role="group" aria-label="Camera controls">
      <IconButton label="Rotate camera left" onClick={() => camera('left')}><ArrowLeft /></IconButton>
      <IconButton label="Rotate camera right" onClick={() => camera('right')}><ArrowRight /></IconButton>
      <IconButton label="Zoom in" onClick={() => camera('zoom-in')}><Plus /></IconButton>
      <IconButton label="Zoom out" onClick={() => camera('zoom-out')}><Minus /></IconButton>
      <IconButton label="Reset camera" onClick={() => camera('reset')}><RotateCcw /></IconButton>
    </div>
    <footer className="bottom-bar">
      <div className="gesture-hint"><MousePointer2 size={15} /><span>Drag to orbit <i /> Scroll to zoom <i /> Click to discover</span></div>
      <div className="view-switch glass" role="group" aria-label="View mode">{modes.map(({ id, label, icon: Icon }) =>
        <button key={id} aria-pressed={workshop.mode === id} onClick={() => workshop.setMode(id)}><Icon size={16} /><span>{label}</span></button>)}</div>
      <button className="schematic-disclosure" onClick={() => workshop.setDrawer('help')}><Sparkles size={13} /> Educational schematic <ArrowUpRight size={12} /></button>
    </footer>
    {drawer === 'parts' && <PartIndex vehicle={vehicle} onSelect={workshop.selectPart} onClose={() => workshop.setDrawer(null)} />}
    {drawer === 'help' && <aside className="help-panel glass" aria-label="About this workshop">
      <div className="panel-heading"><span className="eyebrow">A LITTLE CONTEXT</span><IconButton label="Close help" onClick={() => workshop.setDrawer(null)}><X /></IconButton></div>
      <h2>Curiosity, with care.</h2>
      <p>Orbit the car, click a marker or use the component index. X-ray reveals the systems; exploded view separates them. Open access panels and capture a PNG of any view.</p>
      <p>Camera buttons provide an alternative to dragging. Use Tab to reach controls, Enter to activate, and Escape to close panels. Browser Back restores car and part selections.</p>
      <div className="safety-note"><Maximize2 size={19} /><p>These are original, simplified 3D illustrations. Positions, proportions, brake fitment and panel hinges are approximate. They are not OEM models, blueprints, diagnostic tools or repair instructions.</p></div>
      <h3>What is this car?</h3><p>{vehicle.year} {vehicle.name} · {vehicle.subtitle}<br />{vehicle.market}</p>
      <div className="vehicle-facts">{vehicle.facts.map(fact => <a href={vehicle.sources.find(s => s.id === fact.sourceId)!.url} key={fact.label} target="_blank" rel="noreferrer"><span>{fact.label}</span><strong>{fact.value}</strong></a>)}</div>
      <p className="muted">Photo captions disclose market, trim and engine differences. Service intervals, fluids and torque values are intentionally not guessed. Verify the actual vehicle and its correct manual; safety-critical work belongs with a qualified professional.</p>
      <button className="primary-button" onClick={workshop.startTour}>Start the walkthrough <ArrowUpRight size={16} /></button>
    </aside>}
    {part && !drawer && <DetailPanel vehicle={vehicle} part={part} onSelect={workshop.selectPart}
      tour={workshop.tour} onTourStep={workshop.stepTour} onEndTour={() => workshop.setTour(false)}
      openBook={openBook} onBookOpened={() => setOpenBook(false)} />}
    {notice && <div className="notification glass" role="status"><span>{notice}</span><IconButton label="Dismiss notification" onClick={() => workshop.setNotice('')}><X /></IconButton></div>}
    <div className="sr-only" aria-live="polite">{copied ? 'Link copied to clipboard.' : ''}</div>
  </main>;
}
