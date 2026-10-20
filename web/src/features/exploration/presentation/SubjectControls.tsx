import type { Item, ItemTree } from '../domain/items';
import { itemLabel } from '../domain/items';

type Props = {
  tree: ItemTree; item: Item; isolated: boolean;
  onFocus: () => void; onIsolate: () => void; onRestore: () => void; onSelect: (id: string) => void;
};

export function SubjectControls({ tree, item, isolated, onFocus, onIsolate, onRestore, onSelect }: Props) {
  const children = tree.items.filter(candidate => candidate.parentId === item.id);
  return <section className="subject-controls glass" aria-label="Selected subject controls">
    <div className="subject-heading"><span className="eyebrow">{isolated ? 'ISOLATED SUBJECT' : 'SELECTED SUBJECT'}</span>
      <strong>{itemLabel(tree, item)}</strong></div>
    <div className="subject-actions">
      <button onClick={onFocus}>Focus selection</button>
      <button onClick={onIsolate} disabled={item.id === 'vehicle'} aria-pressed={isolated}>Isolate selection</button>
      <button onClick={onRestore}>Show whole car</button>
    </div>
    <div className="subject-children" aria-label="Subject hierarchy">
      {item.parentId && <button onClick={() => onSelect(item.parentId!)}>Up to {itemLabel(tree, tree.items.find(candidate => candidate.id === item.parentId)!)}</button>}
      {children.map(child => <button key={child.id} onClick={() => onSelect(child.id)}>{itemLabel(tree, child)}</button>)}
    </div>
  </section>;
}
