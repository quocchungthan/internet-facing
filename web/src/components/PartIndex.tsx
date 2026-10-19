import { useEffect, useRef, useState } from 'react';
import { ArrowUpRight, Search, X } from 'lucide-react';
import type { Part, System, Vehicle } from '../domain/catalog';
import { IconButton } from './IconButton';

const systems: { id: System | 'all'; label: string }[] = [
  { id: 'all', label: 'All' }, { id: 'powertrain', label: 'Powertrain' },
  { id: 'chassis', label: 'Chassis' }, { id: 'electrical', label: 'Electrical' }, { id: 'body', label: 'Body' },
];
export function PartIndex({ vehicle, onSelect, onClose }: { vehicle: Vehicle; onSelect: (p: Part) => void; onClose: () => void }) {
  const [query, setQuery] = useState('');
  const [system, setSystem] = useState<System | 'all'>('all');
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);
  const matches = vehicle.parts.filter(p => (system === 'all' || p.system === system)
    && `${p.label} ${p.description}`.toLowerCase().includes(query.toLowerCase()));
  return <aside className="part-index glass" aria-label="Component index">
    <div className="panel-heading"><div><span className="eyebrow">THE ANATOMY</span><h2>Find your part.</h2></div><IconButton label="Close component index" onClick={onClose}><X /></IconButton></div>
    <label className="search-field"><Search size={17} /><input ref={input} value={query} onChange={e => setQuery(e.target.value)} placeholder="Search systems, parts, symptoms…" aria-label="Search components" /></label>
    <div className="system-filter">{systems.map(s => <button key={s.id} aria-pressed={s.id === system} onClick={() => setSystem(s.id)}>{s.label}</button>)}</div>
    <div className="index-results" aria-live="polite">
      {matches.map((p, i) => <button className="part-row" key={p.id} onClick={() => onSelect(p)}>
        <span className="part-number">{String(i + 1).padStart(2, '0')}</span>
        <span><strong>{p.label}</strong><small>{p.system}</small></span><ArrowUpRight size={17} />
      </button>)}
      {!matches.length && <p className="muted">No matching parts. Try a different term or system.</p>}
    </div>
    <div className="index-footer">{vehicle.parts.length} linked components · 4 systems</div>
  </aside>;
}
