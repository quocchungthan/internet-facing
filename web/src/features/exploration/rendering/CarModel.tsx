import { useMemo, useState, type RefObject } from 'react';
import { Html, useCursor } from '@react-three/drei';
import type { Vehicle, ViewMode } from '../../../domain/catalog';
import { createVehicleItems, itemLabel, specificationFor, subtreeIds, worldPosition, type Item, type ItemTree, type Vector } from '../domain/items';
import { DrawingFactory } from './drawings/DrawingFactory';

type Props = {
  vehicle: Vehicle; selectedId: string; isolated: boolean; mode: ViewMode;
  open: boolean; labels: boolean; reducedMotion: boolean; onSelect: (id: string) => void;
  portal: RefObject<HTMLElement>;
};

function drawingPosition(tree: ItemTree, item: Item, mode: ViewMode): Vector {
  const position = worldPosition(tree, item);
  if (mode !== 'exploded' || item.id === 'body') return position;
  const chassis = ['wheels', 'brakes', 'suspension'].includes(item.partId ?? '');
  return [position[0], position[1] + (chassis ? 0.15 : 1.4), position[2] + (position[2] >= 0 ? 0.5 : -0.5)];
}

function labelPosition(vehicle: Vehicle, tree: ItemTree, item: Item, mode: ViewMode): Vector {
  const part = vehicle.parts.find(candidate => candidate.id === item.id);
  if (!part) return drawingPosition(tree, item, mode);
  const [x, y, z] = part.position;
  if (mode !== 'exploded' || item.id === 'body') return [x, y, z];
  return [x, y + (part.system === 'chassis' ? 0.15 : 1.4), z + (z >= 0 ? 0.5 : -0.5)];
}

function ItemDrawing({ item, tree, props }: { item: Item; tree: ItemTree; props: Props }) {
  const [hovered, setHovered] = useState(false);
  useCursor(hovered);
  const spec = specificationFor(tree, item);
  const { vehicle, mode } = props;
  return <group name={`item:${item.id}`} userData={{ itemId: item.id }}
    position={drawingPosition(tree, item, mode)}
    rotation={[0, spec.drawing === 'wheel' && item.position[2] < 0 ? Math.PI : 0, 0]}
    onClick={event => { event.stopPropagation(); props.onSelect(item.id); }}
    onPointerOver={event => { event.stopPropagation(); setHovered(true); }}
    onPointerOut={() => setHovered(false)}>
    <DrawingFactory kind={spec.drawing} classic={vehicle.geometry === 'classic-sedan'}
      length={vehicle.dimensions.length} width={vehicle.dimensions.width} axle={vehicle.dimensions.wheelbase / 2}
      color={vehicle.color} ghost={mode !== 'exterior'} open={props.open || mode === 'exploded'} reducedMotion={props.reducedMotion} />
    {hovered && <Html position={[0, 0.5, 0]} center style={{ pointerEvents: 'none' }}>
      <div className="part-tooltip"><strong>{itemLabel(tree, item)}</strong><small>Click to select this item</small></div>
    </Html>}
  </group>;
}

export default function CarModel(props: Props) {
  const { vehicle, isolated, selectedId, mode, labels, portal } = props;
  const tree = useMemo(() => createVehicleItems(vehicle), [vehicle]);
  const visible = isolated ? subtreeIds(tree, selectedId) : subtreeIds(tree, 'vehicle');
  const drawable = tree.items.filter(item => visible.has(item.id) && specificationFor(tree, item).drawing !== 'assembly');
  const labelItems = tree.items.filter(item => visible.has(item.id) && item.partId === item.id
    && (isolated || mode !== 'exterior' || ['engine', 'wheels', 'body', 'cooling'].includes(item.id)));
  return <group name="vehicle-model" userData={{ subject: isolated ? selectedId : 'vehicle' }}>
    {drawable.map(item => <ItemDrawing key={item.id} item={item} tree={tree} props={props} />)}
    {labels && labelItems.map(item => {
      const position = labelPosition(vehicle, tree, item, mode);
      const spec = specificationFor(tree, item);
      return <Html key={`label-${item.id}`} portal={portal} position={[position[0], position[1] + 0.3, position[2]]}
        center zIndexRange={[10, 1]}>
        <button className={`hotspot ${selectedId === item.id ? 'selected' : ''}`}
          onClick={() => props.onSelect(item.id)} aria-label={`Explore ${spec.label}`}>
          <span className="hotspot-dot" /><span className="hotspot-label">{spec.label}</span>
        </button>
      </Html>;
    })}
  </group>;
}
