import { lazy, Suspense, useCallback, useEffect, useState } from 'react';
import { vehicles } from '../content';
import { readSelection, type Part } from '../domain/catalog';
import { features } from '../config/features';

const DetailPanel = lazy(() => import('./DetailPanel').then(module => ({ default: module.DetailPanel })));
const RepairGuide = lazy(() => import('../features/repair/presentation/RepairGuide').then(module => ({ default: module.RepairGuide })));

function useCatalogSelection() {
  const [selection, setSelection] = useState(() => readSelection(window.location.search, vehicles));
  useEffect(() => {
    const onPop = () => setSelection(readSelection(window.location.search, vehicles));
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
    setSelection(readSelection(url.search, vehicles));
  }, []);
  return { ...selection, navigate };
}

export default function ContentWorkshop() {
  const { vehicle, part, invalid, navigate } = useCatalogSelection();
  const [repair, setRepair] = useState(false);
  const [openBook, setOpenBook] = useState(false);
  useEffect(() => {
    const closeGuide = () => { setRepair(false); setOpenBook(false); };
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      if (repair) setRepair(false);
      else navigate(vehicle.id);
    };
    window.addEventListener('popstate', closeGuide);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('popstate', closeGuide);
      window.removeEventListener('keydown', onKey);
    };
  }, [repair, vehicle.id, navigate]);
  const selectPart = (next?: Part) => { setOpenBook(false); navigate(vehicle.id, next?.id); };
  const active = features.repair || features.learning;
  return <main className="content-workshop">
    <header>
      <a className="brand" href="/" aria-label="30s workshop home">30s</a>
      <h1>Know your machine.</h1>
      <p>Practical car checks and learning, without the 3D viewer.</p>
    </header>
    {!active ? <section aria-labelledby="features-disabled">
      <h2 id="features-disabled">No features are enabled in this build.</h2>
      <p>Enable exploration, repair guidance or learning in the build configuration, then rebuild.</p>
    </section> : <>
      <label className="content-vehicle">Choose car model
        <select value={vehicle.id} onChange={event => {
          setRepair(false); setOpenBook(false); navigate(event.target.value);
        }}>
          {vehicles.map(car => <option key={car.id} value={car.id}>{car.year} {car.name}</option>)}
        </select>
      </label>
      {invalid && <p role="status">That vehicle or part link was not found. Showing the available catalog.</p>}
      <div className="content-features">
        {features.repair && <section aria-label="Repair guidance">
          <h2>Repair guidance</h2>
          <button className="primary-button" onClick={() => setRepair(true)}>Troubleshoot wiper streaks</button>
          {repair && <Suspense fallback={<p role="status">Opening repair guidance...</p>}>
            <RepairGuide key={vehicle.id} vehicle={vehicle} onClose={() => setRepair(false)} />
          </Suspense>}
        </section>}
        {features.learning && <section aria-label="Learning and field books">
          <h2>Learning and field books</h2>
          <label>Choose a part
            <select value={part?.id ?? ''} onChange={event => {
              setOpenBook(false); navigate(vehicle.id, event.target.value || undefined);
            }}>
              <option value="">Select a part</option>
              {vehicle.parts.map(candidate => <option key={candidate.id} value={candidate.id}>{candidate.label}</option>)}
            </select>
          </label>
          <button className="primary-button" onClick={() => {
            navigate(vehicle.id, part?.id ?? vehicle.parts[0].id); setOpenBook(true);
          }}>Open field book</button>
          {part && <Suspense fallback={<p role="status">Opening learning...</p>}>
            <DetailPanel vehicle={vehicle} part={part} onSelect={selectPart}
              openBook={openBook} onBookOpened={() => setOpenBook(false)} />
          </Suspense>}
        </section>}
      </div>
    </>}
  </main>;
}
