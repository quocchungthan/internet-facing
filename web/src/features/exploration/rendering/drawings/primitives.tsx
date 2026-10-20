import { useMemo } from 'react';
import { RoundedBox } from '@react-three/drei';
import { CatmullRomCurve3, Shape, Vector3 } from 'three';
import type { Vector } from '../../domain/items';

export function Box({ position = [0, 0, 0], size, color, metalness = 0.5, opacity = 1 }: {
  position?: Vector; size: Vector; color: string; metalness?: number; opacity?: number;
}) {
  const radius = Math.min(...size) * 0.22;
  return <RoundedBox position={position} args={size} radius={radius} smoothness={2} castShadow receiveShadow>
    <meshStandardMaterial color={color} metalness={metalness} roughness={0.35}
      transparent={opacity < 1} opacity={opacity} depthWrite={opacity === 1} />
  </RoundedBox>;
}

export function Pipe({ points, radius = 0.035, color = '#8d999a' }: { points: Vector[]; radius?: number; color?: string }) {
  const curve = useMemo(() => new CatmullRomCurve3(points.map(p => new Vector3(...p))), [points]);
  return <mesh castShadow><tubeGeometry args={[curve, 48, radius, 16, false]} />
    <meshStandardMaterial color={color} metalness={0.75} roughness={0.35} /></mesh>;
}

export function Polygon({ points, width, color, opacity = 1 }: {
  points: [number, number][]; width: number; color: string; opacity?: number;
}) {
  const shape = useMemo(() => {
    const s = new Shape();
    points.forEach(([x, y], i) => i === 0 ? s.moveTo(x, y) : s.lineTo(x, y));
    s.closePath();
    return s;
  }, [points]);
  return <mesh position={[0, 0, -width / 2]} castShadow receiveShadow>
    <extrudeGeometry args={[shape, { depth: width, bevelEnabled: true, bevelSize: 0.025, bevelThickness: 0.025, bevelSegments: 6, steps: 1 }]} />
    <meshPhysicalMaterial color={color} metalness={0.48} roughness={0.3}
      clearcoat={0.7} transparent={opacity < 1} opacity={opacity} depthWrite={opacity === 1} />
  </mesh>;
}
