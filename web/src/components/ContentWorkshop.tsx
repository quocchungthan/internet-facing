import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from 'react';
import { engineLessons, vehicles } from '../content';
import { readSelection, type Part } from '../domain/catalog';
import { features } from '../config/features';
import { readSubject, subjectSearch } from '../features/exploration/domain/subject';
import { createVehicleItems, itemLabel, validateEngineLessonsAgainstItems } from '../features/exploration/domain/items';
import { vehicleThemeStyle } from '../features/exploration/presentation/vehicleTheme';

const DetailPanel = lazy(() => import('./DetailPanel').then(module => ({ default: module.DetailPanel })));
const RepairGuide = lazy(() => import('../features/repair/presentation/RepairGuide').then(module => ({ default: module.RepairGuide })));
const ObservationLog = lazy(() => import('../features/observations/presentation/ObservationLog').then(module => ({ default: module.ObservationLog })));
const FirstVisitGuide = lazy(() => import('../features/onboarding/presentation/FirstVisitGuide').then(module => ({ default: module.FirstVisitGuide })));

function selectionFromSearch(search: string) {
  const selection = readSelection(search, vehicles);
  const subject = readSubject(search, selection.vehicle);
  return {
    ...selection,
    part: selection.vehicle.parts.find(candidate => candidate.id === subject.item.partId),
    subject,
    invalid: selection.invalid || subject.invalid,
  };
}

function useCatalogSelection() {
  const [selection, setSelection] = useState(() => selectionFromSearch(window.location.search));
  useEffect(() => {
    const onPop = () => setSelection(selectionFromSearch(window.location.search));
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);
  const navigate = useCallback((carId: string, partId?: string) => {
    const url = new URL(window.location.href);
    url.searchParams.set('car', carId);
    url.searchParams.delete('part');
    url.searchParams.delete('item');
    url.searchParams.delete('isolate');
    if (partId) url.searchParams.set('part', partId);
    window.history.pushState(null, '', url);
    setSelection(selectionFromSearch(url.search));
  }, []);
  const navigateItem = useCallback((carId: string, itemId: string) => {
    const vehicle = vehicles.find(candidate => candidate.id === carId);
    if (!vehicle) throw new Error(`Unknown vehicle ${carId}.`);
    const url = new URL(window.location.href);
    url.search = subjectSearch(url.search, vehicle, itemId, false);
    window.history.pushState(null, '', url);
    setSelection(selectionFromSearch(url.search));
  }, []);
  return { ...selection, navigate, navigateItem };
}

export default function ContentWorkshop() {
  const { vehicle, part, subject, invalid, navigate, navigateItem } = useCatalogSelection();
  const [repair, setRepair] = useState(false);
  const [log, setLog] = useState(false);
  const [replayIntro, setReplayIntro] = useState(0);
  const [openBook, setOpenBook] = useState(false);
  const tree = useMemo(() => {
    const items = createVehicleItems(vehicle);
    if (vehicle.id === 'raize') validateEngineLessonsAgainstItems(engineLessons, items.items);
    return items;
  }, [vehicle]);
  const subjectContext = subject.item.id !== 'vehicle' && subject.item.id !== part?.id
    ? { item: subject.item, tree, onSelectItem: (itemId: string) => navigateItem(vehicle.id, itemId) }
    : undefined;
  useEffect(() => {
    const closeGuide = () => { setRepair(false); setLog(false); setOpenBook(false); };
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      if (repair) setRepair(false);
      else if (log) setLog(false);
      else navigate(vehicle.id);
    };
    window.addEventListener('popstate', closeGuide);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('popstate', closeGuide);
      window.removeEventListener('keydown', onKey);
    };
  }, [repair, log, vehicle.id, navigate]);
  const selectPart = (next?: Part) => { setOpenBook(false); navigate(vehicle.id, next?.id); };
  const active = features.repair || features.learning;
  return <main className="content-workshop" data-car-theme={vehicle.id} style={vehicleThemeStyle(vehicle.color)}>
    <header>
      <a className="brand" href="/" aria-label="30s workshop home">30s</a>
      <h1>Know your machine.</h1>
      <p>Practical car checks and learning, without the 3D viewer.</p>
      <p className="content-disclosure" role="note">Explanations are source-scoped. Schematics are representative, not OEM geometry or repair access; verify service work in the manual for the actual vehicle.</p>
      {features.onboarding && active && <button className="replay-intro" onClick={() => setReplayIntro(count => count + 1)}>Replay introduction</button>}
    </header>
    {!active ? <section aria-labelledby="features-disabled">
      <h2 id="features-disabled">No features are enabled in this build.</h2>
      <p>Enable exploration, repair guidance or learning in the build configuration, then rebuild.</p>
    </section> : <>
      <label className="content-vehicle">Choose car model
        <select value={vehicle.id} onChange={event => {
          setRepair(false); setLog(false); setOpenBook(false); navigate(event.target.value);
        }}>
          {vehicles.map(car => <option key={car.id} value={car.id}>{car.year} {car.name}</option>)}
        </select>
      </label>
      {invalid && <p role="status">That vehicle or part link was not found. Showing the available catalog.</p>}
      <div className="content-features">
        {features.repair && <section aria-label="Repair guidance">
          <h2>Repair guidance</h2>
          <button className="primary-button" data-onboarding="repair" onClick={() => { setLog(false); setRepair(true); }}>Troubleshoot wiper streaks</button>
          <button className="secondary-button" aria-pressed={log} onClick={() => { setRepair(false); setLog(!log); }}>Observation log</button>
          {repair && <Suspense fallback={<p role="status">Opening repair guidance...</p>}>
            <RepairGuide key={vehicle.id} vehicle={vehicle} onClose={() => setRepair(false)} />
          </Suspense>}
          {log && <Suspense fallback={<p role="status">Opening observation log...</p>}>
            <ObservationLog vehicle={vehicle} onClose={() => setLog(false)} />
          </Suspense>}
        </section>}
        {features.learning && <section aria-label="Learning and field books">
          <h2>Learning and field books</h2>
          <label>Choose a part
            <select value={subject.item.id} onChange={event => {
              setOpenBook(false); navigateItem(vehicle.id, event.target.value || 'vehicle');
            }}>
              <option value="vehicle">Select a part</option>
              {subject.item.id !== 'vehicle' && !vehicle.parts.some(candidate => candidate.id === subject.item.id)
                && !engineLessons.some(lesson => lesson.itemId === subject.item.id)
                && <option value={subject.item.id}>{itemLabel(tree, subject.item)} — no reviewed lesson</option>}
              {vehicle.parts.map(candidate => <option key={candidate.id} value={candidate.id}>{candidate.label}</option>)}
              {vehicle.id === 'raize' && <optgroup label="Representative engine subjects">
                {engineLessons.map(lesson => <option key={lesson.itemId} value={lesson.itemId}>{lesson.label}</option>)}
              </optgroup>}
            </select>
          </label>
          <button className="primary-button" data-onboarding="learning" onClick={() => {
            navigate(vehicle.id, part?.id ?? vehicle.parts[0].id); setOpenBook(true);
          }}>Open field book</button>
          {(part || subjectContext) && <Suspense fallback={<p role="status">Opening learning...</p>}>
            <DetailPanel vehicle={vehicle} part={part} subject={subjectContext} onSelect={selectPart}
              openBook={openBook} onBookOpened={() => setOpenBook(false)} />
          </Suspense>}
        </section>}
      </div>
    </>}
    {features.onboarding && <Suspense fallback={null}><FirstVisitGuide replay={replayIntro} /></Suspense>}
  </main>;
}
