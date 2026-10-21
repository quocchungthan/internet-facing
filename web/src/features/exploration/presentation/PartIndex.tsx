import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ArrowUpRight, Search, X } from 'lucide-react';
import type { System, Vehicle } from '../../../domain/catalog';
import { IconButton } from '../../../components/IconButton';
import { engineLessons } from '../../../content';
import { createVehicleItems, itemLabel, searchItemTree, type ItemTree } from '../domain/items';

const systems: { id: System | 'all'; label: string }[] = [
  { id: 'all', label: 'All' }, { id: 'powertrain', label: 'Powertrain' },
  { id: 'chassis', label: 'Chassis' }, { id: 'electrical', label: 'Electrical' }, { id: 'body', label: 'Body' },
];
function Hierarchy({ tree, parentId, onSelect }: { tree: ItemTree; parentId?: string; onSelect: (id: string) => void }) {
  return <ul className="item-children">{tree.items.filter(item => item.parentId === parentId).map(item =>
    <li key={item.id}>
      <button className="item-choice" onClick={() => onSelect(item.id)}>
        {itemLabel(tree, item)}
        {item.id.includes('-front-') || item.id.includes('-rear-') ? <small>{item.id.split('-').slice(1).join(' ')}</small> : null}
      </button>
      <Hierarchy tree={tree} parentId={item.id} onSelect={onSelect} />
    </li>)}</ul>;
}

export function PartIndex({ vehicle, onSelectItem, onClose }: {
  vehicle: Vehicle; onSelectItem: (id: string) => void; onClose: () => void;
}) {
  const [query, setQuery] = useState('');
  const [system, setSystem] = useState<System | 'all'>('all');
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);
  const tree = createVehicleItems(vehicle);
  const matches = searchItemTree(tree, vehicle, engineLessons, query, system);
  const activate = (itemId: string) => {
    onSelectItem(itemId);
    window.requestAnimationFrame(() => document.getElementById('selected-subject-heading')?.focus());
  };
  const activateFirstMatch = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter' || !query.trim() || !matches[0]) return;
    event.preventDefault();
    activate(matches[0].itemId);
  };
  return <aside className="part-index glass" aria-label="Component index">
    <div className="panel-heading"><div><span className="eyebrow">THE ANATOMY</span><h2>Find your part.</h2></div><IconButton label="Close component index" onClick={onClose}><X /></IconButton></div>
    <label className="search-field"><Search size={17} /><input ref={input} value={query} onChange={e => setQuery(e.target.value)} onKeyDown={activateFirstMatch} placeholder="Search systems and components…" aria-label="Search components" /></label>
    <div className="system-filter">{systems.map(s => <button key={s.id} aria-pressed={s.id === system} onClick={() => setSystem(s.id)}>{s.label}</button>)}</div>
    <div className="index-results" aria-live="polite">
      {!query && system === 'all' && <details className="item-hierarchy">
        <summary>Car / assembly / part hierarchy</summary>
        <Hierarchy tree={tree} onSelect={activate} />
      </details>}
      {matches.map((result, i) => <button className="part-row" key={result.itemId} onClick={() => activate(result.itemId)}>
        <span className="part-number">{String(i + 1).padStart(2, '0')}</span>
        <span><strong>{result.label}</strong><small>{result.pathLabel}</small>
          <small>{result.system}{result.drawable ? '' : ` · ${result.reason}`}</small></span><ArrowUpRight size={17} />
      </button>)}
      {!matches.length && <p className="muted">No matching components. Try a different term or system.</p>}
    </div>
    <div className="index-footer">{matches.length} matching components · 4 systems</div>
  </aside>;
}
