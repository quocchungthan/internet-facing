import type { Item, ItemTree } from '../domain/items';
import { itemLabel } from '../domain/items';
import { engineLessons } from '../../../content';
import { lessonFor } from '../../../domain/engineLessons';
import type { SubjectActivation } from '../domain/subject';
import { vehicles } from '../../../content';
import { ComponentQuestion } from '../../questions/presentation/ComponentQuestion';

type Props = {
  carId: string; tree: ItemTree; item: Item; activation: SubjectActivation; isolated: boolean;
  onFocus: () => void; onIsolate: () => void; onRestore: () => void; onSelect: (id: string) => void;
};

function ancestry(tree: ItemTree, item: Item): Item[] {
  const path: Item[] = [];
  let parentId = item.parentId;
  while (parentId) {
    const parent = tree.items.find(candidate => candidate.id === parentId);
    if (!parent) throw new Error(`Missing subject ancestor ${parentId} for ${item.id}.`);
    path.unshift(parent);
    parentId = parent.parentId;
  }
  return path;
}

export function SubjectControls({ carId, tree, item, activation, isolated, onFocus, onIsolate, onRestore, onSelect }: Props) {
  const vehicle = vehicles.find(candidate => candidate.id === carId);
  if (!vehicle) throw new Error(`Unknown vehicle ${carId}.`);
  const ancestors = ancestry(tree, item);
  const children = tree.items.filter(candidate => candidate.parentId === item.id);
  const lesson = lessonFor(engineLessons, carId, item.id);
  const connections = lesson.status === 'ready'
    ? lesson.lesson.connections.filter(connection => connection.itemId !== item.id).map(connection => ({
      connection, target: tree.items.find(candidate => candidate.id === connection.itemId),
    }))
    : [];
  const canFocus = activation.kind === 'focus';
  return <section className="subject-controls glass" aria-label="Selected subject controls">
    <div className="subject-heading"><span className="eyebrow">{isolated ? 'ISOLATED SUBJECT' : 'SELECTED SUBJECT'}</span>
      <strong id="selected-subject-heading" tabIndex={-1}>{itemLabel(tree, item)}</strong></div>
    <div className="subject-actions">
      <button onClick={onFocus} disabled={!canFocus} title={!canFocus ? activation.reason : undefined}>Focus selection</button>
      <button onClick={onIsolate} disabled={!canFocus || item.id === 'vehicle'}
        title={!canFocus ? activation.reason : undefined} aria-pressed={isolated}>Isolate selection</button>
      <button onClick={onRestore}>Show whole car</button>
    </div>
    {!canFocus && <p role="status">{activation.reason}</p>}
    <ComponentQuestion vehicle={vehicle} tree={tree} itemId={item.id} />
    {ancestors.length > 0 && <div className="subject-children" role="group" aria-label="Subject ancestry">
      {ancestors.map(ancestor => <button key={ancestor.id} onClick={() => onSelect(ancestor.id)}>
        Up to {itemLabel(tree, ancestor)}
      </button>)}
    </div>}
    {children.length > 0 && <div className="subject-children" role="group" aria-label="Subject children">
      {children.map(child => <button key={child.id} onClick={() => onSelect(child.id)}>{itemLabel(tree, child)}</button>)}
    </div>}
    {connections.length > 0 && <div className="subject-children" role="group" aria-label="Connected subjects">
      {connections.map(({ connection, target }) => target
        ? <button key={connection.itemId} onClick={() => onSelect(target.id)}
          aria-label={`Explore connected subject ${itemLabel(tree, target)}: ${connection.relationship}`}>
          {itemLabel(tree, target)}
        </button>
        : <p key={connection.itemId} role="status">{connection.itemId} is not available in this vehicle.</p>)}
    </div>}
  </section>;
}
