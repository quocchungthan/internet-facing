import { useMemo, useRef, useState, type ReactNode, type RefObject } from 'react';
import { useFrame, type ThreeEvent } from '@react-three/fiber';
import { Html, useCursor } from '@react-three/drei';
import { CatmullRomCurve3, Group, MathUtils, Shape, Vector3 } from 'three';
import type { Part, Vehicle, ViewMode } from '../domain/catalog';

type Vec3 = [number, number, number];
type Props = {
  vehicle: Vehicle;
  selected?: Part;
  mode: ViewMode;
  open: boolean;
  labels: boolean;
  reducedMotion: boolean;
  onSelect: (part: Part) => void;
  portal: RefObject<HTMLElement>;
};

function Box({ position = [0, 0, 0], size, color, metalness = 0.5, opacity = 1 }: {
  position?: Vec3; size: Vec3; color: string; metalness?: number; opacity?: number;
}) {
  return <mesh position={position} castShadow receiveShadow>
    <boxGeometry args={size} />
    <meshStandardMaterial color={color} metalness={metalness} roughness={0.35}
      transparent={opacity < 1} opacity={opacity} depthWrite={opacity === 1} />
  </mesh>;
}

function Pipe({ points, radius = 0.035, color = '#8d999a' }: { points: Vec3[]; radius?: number; color?: string }) {
  const curve = useMemo(() => new CatmullRomCurve3(points.map(p => new Vector3(...p))), [points]);
  return <mesh castShadow><tubeGeometry args={[curve, 24, radius, 8, false]} />
    <meshStandardMaterial color={color} metalness={0.75} roughness={0.35} /></mesh>;
}

function Polygon({ points, width, color, opacity = 1 }: { points: [number, number][]; width: number; color: string; opacity?: number }) {
  const shape = useMemo(() => {
    const s = new Shape();
    points.forEach(([x, y], i) => i === 0 ? s.moveTo(x, y) : s.lineTo(x, y));
    s.closePath();
    return s;
  }, [points]);
  return <mesh position={[0, 0, -width / 2]} castShadow receiveShadow>
    <extrudeGeometry args={[shape, { depth: width, bevelEnabled: true, bevelSize: 0.025, bevelThickness: 0.025, bevelSegments: 2, steps: 1 }]} />
    <meshPhysicalMaterial color={color} metalness={0.48} roughness={0.3}
      clearcoat={0.7} transparent={opacity < 1} opacity={opacity} depthWrite={opacity === 1} />
  </mesh>;
}

function Hinge({ position, angle, axis, reduced, children }: {
  position: Vec3; angle: number; axis: 'y' | 'z'; reduced: boolean; children: ReactNode;
}) {
  const ref = useRef<Group>(null);
  useFrame((_, dt) => {
    if (ref.current) ref.current.rotation[axis] = reduced ? angle : MathUtils.damp(ref.current.rotation[axis], angle, 8, dt);
  });
  return <group ref={ref} position={position}>{children}</group>;
}

function MechanicalPart({ part, position, selected, exploded, reduced, onSelect, children }: {
  part: Part; position: Vec3; selected: boolean; exploded: boolean; reduced: boolean;
  onSelect: (p: Part) => void; children: ReactNode;
}) {
  const ref = useRef<Group>(null);
  const [hovered, setHovered] = useState(false);
  useCursor(hovered);
  const target = new Vector3(...position);
  if (exploded) {
    target.y += part.system === 'chassis' ? 0.15 : 1.4;
    target.z += position[2] >= 0 ? 0.5 : -0.5;
    if (part.id === 'lubrication') target.y -= 0.9;
    if (part.id === 'intake') target.y += 0.65;
    if (part.id === 'electrical') target.z -= 0.6;
  }
  useFrame((_, dt) => {
    if (!ref.current) return;
    if (reduced) ref.current.position.copy(target);
    else ref.current.position.lerp(target, 1 - Math.exp(-9 * dt));
  });
  const enter = (event: ThreeEvent<PointerEvent>) => { event.stopPropagation(); setHovered(true); };
  return <group ref={ref} position={position}
    onClick={e => { e.stopPropagation(); onSelect(part); }}
    onPointerOver={enter} onPointerOut={() => setHovered(false)}>
    {children}
    {(selected || hovered) && <mesh position={[0, 0.18, 0]}>
      <sphereGeometry args={[0.22, 16, 12]} />
      <meshBasicMaterial color="#d9f690" wireframe transparent opacity={0.45} depthTest={false} />
    </mesh>}
    {hovered && <Html position={[0, 0.5, 0]} center zIndexRange={[30, 20]} style={{ pointerEvents: 'none' }}>
      <div className="part-tooltip"><strong>{part.label}</strong><span>{part.summary}</span><small>Click to explore</small></div>
    </Html>}
  </group>;
}

function Engine({ classic }: { classic: boolean }) {
  return <group>
    <Box size={classic ? [0.75, 0.45, 0.58] : [0.52, 0.44, 0.7]} color={classic ? '#a74d2f' : '#6c7b7d'} />
    {Array.from({ length: classic ? 4 : 3 }, (_, i) =>
      <group key={i} position={classic ? [(i - 1.5) * 0.17, 0.26, 0] : [0, 0.24, (i - 1) * 0.21]}>
        {(classic ? [-1, 1] : [0]).map(bank => <mesh key={bank} position={[0, 0, bank * 0.2]} rotation={[bank * 0.45, 0, 0]} castShadow>
          <cylinderGeometry args={[0.08, 0.08, 0.22, 16]} />
          <meshStandardMaterial color="#c6d0cf" metalness={0.8} roughness={0.3} />
        </mesh>)}
      </group>)}
    <Box position={[0, 0.38, 0]} size={classic ? [0.72, 0.13, 0.64] : [0.56, 0.12, 0.74]} color={classic ? '#a94c30' : '#262e32'} />
    {[-1, 1].map(side => <Pipe key={side} points={[[-0.22, 0.05, side * 0.3], [-0.26, -0.13, side * 0.45], [0.1, -0.25, side * 0.4]]} color="#b49370" />)}
    {Array.from({ length: 5 }, (_, i) => <Box key={i} position={[-0.25 + i * 0.12, 0.46, 0]} size={[0.025, 0.025, 0.5]} color="#808d8e" />)}
  </group>;
}

function Wheel({ classic }: { classic: boolean }) {
  return <group>
    <mesh castShadow receiveShadow>
      <torusGeometry args={[0.33, 0.12, 12, 40]} />
      <meshStandardMaterial color="#192024" roughness={0.9} />
    </mesh>
    <mesh rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.275, 0.275, 0.19, 32]} />
      <meshStandardMaterial color={classic ? '#a9b2b4' : '#333e43'} metalness={0.85} roughness={0.2} />
    </mesh>
    {Array.from({ length: classic ? 12 : 5 }, (_, i) => {
      const angle = i * Math.PI * 2 / (classic ? 12 : 5);
      return <group key={i} rotation={[0, 0, angle]}>
        <Box position={[0, 0.14, 0.11]} size={[classic ? 0.035 : 0.05, 0.24, 0.025]} color="#b8c3c4" />
      </group>;
    })}
    <mesh position={[0, 0, 0.14]} rotation={[Math.PI / 2, 0, 0]}>
      <cylinderGeometry args={[classic ? 0.15 : 0.08, 0.08, 0.035, 24]} />
      <meshStandardMaterial color="#c1cccb" metalness={1} roughness={0.2} />
    </mesh>
    {Array.from({ length: 16 }, (_, i) => <group key={i} rotation={[0, 0, i * Math.PI / 8]}>
      <Box position={[0, 0.445, 0]} size={[0.025, 0.012, 0.14]} color="#303637" metalness={0} />
    </group>)}
  </group>;
}

export default function CarModel({ vehicle, selected, mode, open, labels, reducedMotion, onSelect, portal }: Props) {
  const classic = vehicle.geometry === 'classic-sedan';
  const length = vehicle.dimensions.length;
  const width = vehicle.dimensions.width;
  const axle = vehicle.dimensions.wheelbase / 2;
  const roof = classic ? 1.5 : 1.77;
  const ghost = mode !== 'exterior';
  const opacity = ghost ? 0.12 : 1;
  const color = vehicle.color;
  const part = (id: string) => vehicle.parts.find(p => p.id === id)!;
  const mech = (id: string, children: ReactNode, position?: Vec3) =>
    <MechanicalPart key={id} part={part(id)} position={position ?? part(id).position}
      selected={selected?.id === id} exploded={mode === 'exploded'} reduced={reducedMotion} onSelect={onSelect}>
      {children}
    </MechanicalPart>;
  const bodyPart = part('body');
  const access = open || mode === 'exploded';
  const bodyPoints: [number, number][] = [
    [-length / 2, 0.7], [-length / 2, 0.97], [-length / 2 + 0.16, 1.1],
    [-0.57, 1.13], [0.9, 1.1], [length / 2 - 0.12, 1.07], [length / 2, 0.94],
    [length / 2, 0.68], [axle + 0.51, 0.68], [axle + 0.45, 0.9], [axle + 0.28, 1.0],
    [axle - 0.28, 1.0], [axle - 0.45, 0.9], [axle - 0.51, 0.68],
    [-axle + 0.51, 0.68], [-axle + 0.45, 0.9], [-axle + 0.28, 1.0],
    [-axle - 0.28, 1.0], [-axle - 0.45, 0.9], [-axle - 0.51, 0.68],
  ];
  return <group position={[0, 0, 0]}>
    {[-1, 1].map(side => <Box key={side} position={[0, 0.38, side * width * 0.32]} size={[length - 0.45, 0.12, 0.1]} color="#394447" />)}
    <Box position={[0, 0.58, 0]} size={[length * 0.65, 0.1, width * 0.8]} color="#283237" opacity={ghost ? 0.22 : 1} />
    {mech('engine', <group scale={0.72}><Engine classic={classic} /></group>)}
    {mech('lubrication', <Box size={[0.55, 0.16, 0.42]} color="#b19b65" />)}
    {mech('cooling', <group>
      <Box size={[0.12, 0.55, width * 0.65]} color="#677779" />
      {Array.from({ length: 15 }, (_, i) => <Box key={i} position={[-0.08, 0, -width * 0.3 + i * width * 0.043]} size={[0.025, 0.5, 0.018]} color="#afc0bb" />)}
      <Pipe points={[[0, 0.25, 0.3], [0.25, 0.3, 0.3], [0.5, 0.1, 0.1]]} color="#303d3b" radius={0.045} />
    </group>)}
    {mech('intake', classic ? <group>
      <mesh position={[0, 0.08, 0]} castShadow><cylinderGeometry args={[0.3, 0.3, 0.13, 40]} /><meshStandardMaterial color="#9ca9a8" metalness={0.95} roughness={0.25} /></mesh>
      <Box size={[0.18, 0.18, 0.18]} color="#b9a078" />
    </group> : <group>
      <mesh rotation={[Math.PI / 2, 0, 0]}><torusGeometry args={[0.13, 0.065, 12, 24]} /><meshStandardMaterial color="#b9a688" metalness={0.8} roughness={0.35} /></mesh>
      <Pipe points={[[0, 0, 0], [0.15, 0.18, 0.18], [-0.25, 0.18, 0.4]]} color="#414f53" radius={0.06} />
    </group>)}
    {mech('transmission', <group rotation={classic ? [0, 0, 0] : [0, Math.PI / 2, 0]}>
      <Box size={[0.65, 0.32, 0.35]} color="#97a4a5" />
      <Box position={[-0.36, 0, 0]} size={[0.18, 0.42, 0.45]} color="#77898b" />
      {Array.from({ length: 5 }, (_, i) => <Box key={i} position={[-0.22 + i * 0.12, 0.18, 0]} size={[0.035, 0.04, 0.33]} color="#c3cdca" />)}
    </group>)}
    {classic && <Pipe points={[[-0.3, 0.42, 0], [0.8, 0.42, 0], [axle, 0.42, 0]]} radius={0.06} />}
    {mech('fuel', <Box size={[0.65, 0.23, width * 0.54]} color="#6a7970" />)}
    {mech('electrical', <group>
      <Box size={[0.32, 0.3, 0.25]} color="#242e30" />
      <Box position={[0, 0.16, 0]} size={[0.33, 0.025, 0.26]} color="#66776c" />
      <Box position={[-0.1, 0.19, 0]} size={[0.05, 0.04, 0.06]} color="#b75148" />
      <Box position={[0.1, 0.19, 0]} size={[0.05, 0.04, 0.06]} color="#a5b9b5" />
    </group>)}
    {mech('exhaust', <group>
      <Pipe points={[[-length * 0.3, 0, 0], [-0.4, 0, 0], [length * 0.32, 0, 0], [length * 0.38, 0, -0.15]]} radius={0.045} />
      <Box position={[length * 0.25, 0, 0]} size={[0.45, 0.15, 0.25]} color="#909a93" />
    </group>)}
    {[-1, 1].flatMap(front => [-1, 1].map(side => {
      const wheelPosition: Vec3 = [front * axle, 0.48, side * width * 0.49];
      const wheelOffset = mode === 'exploded' ? side * 0.55 : 0;
      return <group key={`${front}-${side}`}>
        <group position={[wheelPosition[0], wheelPosition[1], wheelPosition[2] + wheelOffset]} rotation={[0, side < 0 ? Math.PI : 0, 0]}
          onClick={e => { e.stopPropagation(); onSelect(part('wheels')); }}>
          <Wheel classic={classic} />
        </group>
        {mech('brakes', <group>
          <mesh rotation={[Math.PI / 2, 0, 0]}><cylinderGeometry args={[0.2, 0.2, 0.04, 32]} /><meshStandardMaterial color="#8e9b9c" metalness={0.9} roughness={0.4} /></mesh>
          <Box position={[0.15, 0.03, 0]} size={[0.1, 0.21, 0.08]} color="#bc654f" />
        </group>, [front * axle, 0.48, side * width * 0.39])}
        {mech('suspension', <group>
          <mesh><cylinderGeometry args={[0.04, 0.04, 0.55, 12]} /><meshStandardMaterial color="#939d91" metalness={0.6} roughness={0.4} /></mesh>
          {Array.from({ length: 5 }, (_, i) => <mesh key={i} position={[0, -0.2 + i * 0.085, 0]} rotation={[Math.PI / 2, 0, 0]}>
            <torusGeometry args={[0.075, 0.015, 6, 16]} /><meshStandardMaterial color="#bc9f58" metalness={0.65} roughness={0.4} />
          </mesh>)}
        </group>, [front * axle, 0.8, side * width * 0.32])}
      </group>;
    }))}
    <group onClick={e => { e.stopPropagation(); onSelect(bodyPart); }}>
      {[-1, 1].map(side => <group key={side} position={[0, 0, side * (width / 2 - 0.055)]}>
        <Polygon points={bodyPoints} width={0.1} color={color} opacity={opacity} />
        <Box position={[0.15, 0.67, side * 0.015]} size={[axle * 1.3, 0.13, 0.08]} color={classic ? '#aeb7b7' : '#222d31'} opacity={opacity} />
        <Box position={[0, 1.085, side * 0.02]} size={[length * 0.88, 0.018, 0.025]} color="#a9bab8" opacity={opacity} />
      </group>)}
      <Box position={[0.13, roof, 0]} size={[classic ? 1.65 : 1.53, 0.08, width * 0.78]} color={classic ? color : '#263537'} opacity={opacity} />
      <Polygon points={[[-0.77, 1.12], [-0.52, roof - 0.04], [-0.43, roof - 0.04], [-0.68, 1.12]]} width={width * 0.83} color="#284248" opacity={ghost ? 0.08 : 0.82} />
      <Polygon points={[[0.86, 1.11], [0.85, roof - 0.04], [0.92, roof - 0.04], [1.2, 1.1]]} width={width * 0.83} color="#284248" opacity={ghost ? 0.08 : 0.82} />
      {[-1, 1].flatMap(side => [0, 1].map(door => {
        const hingeX = door === 0 ? -0.65 : 0.17;
        const doorWidth = door === 0 ? 0.79 : 0.69;
        return <Hinge key={`${side}-${door}`} position={[hingeX, 0.83, side * width * 0.49]}
          angle={access ? side * (door === 0 ? -0.95 : -0.75) : 0} axis="y" reduced={reducedMotion}>
          <Box position={[doorWidth / 2, 0.1, 0]} size={[doorWidth - 0.025, 0.35, 0.05]} color={color} opacity={opacity} />
          <Polygon points={[[0.02, 0.32], [door === 0 ? 0.19 : 0.02, roof - 0.88], [doorWidth - 0.06, roof - 0.88], [doorWidth - 0.02, 0.32]]}
            width={0.025} color="#29474a" opacity={ghost ? 0.06 : 0.78} />
          <Box position={[doorWidth - 0.15, 0.22, side * 0.035]} size={[0.12, 0.025, 0.025]} color="#b3c1bd" opacity={opacity} />
        </Hinge>;
      }))}
      <Hinge position={[-0.66, 1.12, 0]} axis="z" angle={access ? -1.12 : 0} reduced={reducedMotion}>
        <Box position={[-(length / 2 - 0.82) / 2, 0, 0]} size={[length / 2 - 0.82, 0.065, width * 0.88]} color={color} opacity={opacity} />
      </Hinge>
      <Hinge position={[1.08, 1.1, 0]} axis="z" angle={access ? 0.65 : 0} reduced={reducedMotion}>
        <Box position={[(length / 2 - 1.2) / 2, 0, 0]} size={[length / 2 - 1.2, 0.06, width * 0.87]} color={color} opacity={opacity} />
      </Hinge>
      {[-1, 1].map(end => <group key={end} position={[end * (length / 2 - 0.03), 0.86, 0]}>
        <Box size={[0.1, 0.12, width * 0.9]} color={classic ? '#aab7b7' : '#293639'} opacity={opacity} />
        <Box position={[end * 0.035, 0.17, 0]} size={[0.03, 0.17, width * 0.4]} color="#17232a" opacity={opacity} />
        {[-1, 1].map(side => <Box key={side} position={[end * 0.055, 0.17, side * width * 0.33]}
          size={[0.035, 0.1, width * 0.2]} color={end < 0 ? '#f0e5b8' : '#ad493d'} opacity={opacity} />)}
        <Box position={[end * 0.065, -0.04, 0]} size={[0.02, 0.09, 0.32]} color="#d9dfcf" opacity={opacity} />
      </group>)}
      {[-1, 1].map(side => <Box key={side} position={[-0.55, 1.25, side * width * 0.57]} size={[0.18, 0.1, 0.15]} color={color} opacity={opacity} />)}
    </group>
    {labels && vehicle.parts.filter(p => mode !== 'exterior' || ['engine', 'wheels', 'body', 'cooling'].includes(p.id)).map(p =>
      <Html key={`label-${p.id}`} portal={portal} position={[p.position[0], p.position[1] + (mode === 'exploded' && p.system !== 'chassis' ? 1.6 : 0.3), p.position[2]]}
        center zIndexRange={[10, 1]}>
        <button className={`hotspot ${selected?.id === p.id ? 'selected' : ''}`}
          onClick={() => onSelect(p)} aria-label={`Explore ${p.label}`} title={p.summary}>
          <span className="hotspot-dot" /><span className="hotspot-label">{p.label}</span>
        </button>
      </Html>)}
  </group>;
}
