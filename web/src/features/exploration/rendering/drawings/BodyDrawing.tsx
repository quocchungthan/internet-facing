import { useMemo, useRef, type ReactNode } from 'react';
import { useFrame, useThree } from '@react-three/fiber';
import { Group, Shape } from 'three';
import type { Vector } from '../../domain/items';
import { Box, Polygon } from './primitives';
import type { DrawingProps } from './MechanicalDrawings';
import { advanceHingeAngle } from './hingeMotion';

function Hinge({ position, angle, axis, reduced, children }: {
  position: Vector; angle: number; axis: 'y' | 'z'; reduced: boolean; children: ReactNode;
}) {
  const ref = useRef<Group>(null);
  const invalidate = useThree(state => state.invalidate);
  useFrame((_, dt) => {
    if (!ref.current) return;
    const next = advanceHingeAngle(ref.current.rotation[axis], angle, reduced, dt);
    ref.current.rotation[axis] = next;
    if (ref.current.rotation[axis] !== angle) invalidate();
  });
  return <group ref={ref} position={position}>{children}</group>;
}

function BodyShell({ length, width, axle, color, opacity }: { length: number; width: number; axle: number; color: string; opacity: number }) {
  const shape = useMemo(() => {
    const s = new Shape();
    s.moveTo(-length / 2, 0.68);
    s.lineTo(-length / 2, 0.94);
    s.quadraticCurveTo(-length / 2, 1.12, -length / 2 + 0.2, 1.12);
    s.lineTo(length / 2 - 0.2, 1.1);
    s.quadraticCurveTo(length / 2, 1.1, length / 2, 0.92);
    s.lineTo(length / 2, 0.68);
    for (const center of [axle, -axle]) {
      s.lineTo(center + 0.51, 0.68);
      s.bezierCurveTo(center + 0.5, 1.16, center - 0.5, 1.16, center - 0.51, 0.68);
    }
    s.closePath();
    return s;
  }, [length, axle]);
  return <group>{[-1, 1].map(side => <mesh key={side} position={[0, 0, side * width / 2 - 0.05]} castShadow receiveShadow>
    <extrudeGeometry args={[shape, { depth: 0.1, bevelEnabled: true, bevelSize: 0.025, bevelThickness: 0.025, bevelSegments: 6, curveSegments: 24 }]} />
    <meshPhysicalMaterial color={color} metalness={0.48} roughness={0.3} clearcoat={0.7}
      transparent={opacity < 1} opacity={opacity} depthWrite={opacity === 1} />
  </mesh>)}</group>;
}

function Doors({ width, roof, color, opacity, open, reducedMotion }: DrawingProps & { roof: number; opacity: number }) {
  return <group>{[-1, 1].flatMap(side => [0, 1].map(door => {
    const hingeX = door === 0 ? -0.65 : 0.17;
    const doorWidth = door === 0 ? 0.79 : 0.69;
    return <Hinge key={`${side}-${door}`} position={[hingeX, 0.83, side * width * 0.49]}
      angle={open ? side * (door === 0 ? -0.95 : -0.75) : 0} axis="y" reduced={reducedMotion}>
      <Box position={[doorWidth / 2, 0.1, 0]} size={[doorWidth - 0.025, 0.35, 0.05]} color={color} opacity={opacity} />
      <Polygon points={[[0.02, 0.32], [door === 0 ? 0.19 : 0.02, roof - 0.88], [doorWidth - 0.06, roof - 0.88], [doorWidth - 0.02, 0.32]]}
        width={0.025} color="#29474a" opacity={opacity < 1 ? 0.06 : 0.78} />
      <Box position={[doorWidth - 0.15, 0.22, side * 0.035]} size={[0.12, 0.025, 0.025]} color="#b3c1bd" opacity={opacity} />
    </Hinge>;
  }))}</group>;
}

function Bumpers({ length, width, classic, opacity }: DrawingProps & { opacity: number }) {
  return <group>{[-1, 1].map(end => <group key={end} position={[end * (length / 2 - 0.03), 0.86, 0]}>
    <Box size={[0.1, 0.12, width * 0.9]} color={classic ? '#aab7b7' : '#293639'} opacity={opacity} />
    <Box position={[end * 0.035, 0.17, 0]} size={[0.03, 0.17, width * 0.4]} color="#17232a" opacity={opacity} />
    {[-1, 1].map(side => <Box key={side} position={[end * 0.055, 0.17, side * width * 0.33]}
      size={[0.035, 0.1, width * 0.2]} color={end < 0 ? '#f0e5b8' : '#ad493d'} opacity={opacity} />)}
    <Box position={[end * 0.065, -0.04, 0]} size={[0.02, 0.09, 0.32]} color="#d9dfcf" opacity={opacity} />
  </group>)}</group>;
}

export function BodyDrawing(props: DrawingProps & { axle: number }) {
  const { classic, length, width, color, open, ghost, reducedMotion, axle } = props;
  const roof = classic ? 1.5 : 1.77;
  const opacity = ghost ? 0.12 : 1;
  return <group>
    {[-1, 1].map(side => <Box key={side} position={[0, 0.38, side * width * 0.32]} size={[length - 0.45, 0.12, 0.1]} color="#394447" />)}
    <Box position={[0, 0.58, 0]} size={[length * 0.65, 0.1, width * 0.8]} color="#283237" opacity={ghost ? 0.22 : 1} />
    <BodyShell length={length} width={width} axle={axle} color={color} opacity={opacity} />
    {[-1, 1].map(side => <group key={side} position={[0, 0, side * width / 2]}>
      <Box position={[0.15, 0.67, side * 0.015]} size={[axle * 1.3, 0.13, 0.08]} color={classic ? '#aeb7b7' : '#222d31'} opacity={opacity} />
      <Box position={[0, 1.085, side * 0.02]} size={[length * 0.88, 0.018, 0.025]} color="#a9bab8" opacity={opacity} />
    </group>)}
    <Box position={[0.13, roof, 0]} size={[classic ? 1.65 : 1.53, 0.08, width * 0.78]} color={classic ? color : '#263537'} opacity={opacity} />
    <Polygon points={[[-0.77, 1.12], [-0.52, roof - 0.04], [-0.43, roof - 0.04], [-0.68, 1.12]]} width={width * 0.83} color="#284248" opacity={ghost ? 0.08 : 0.82} />
    <Polygon points={[[0.86, 1.11], [0.85, roof - 0.04], [0.92, roof - 0.04], [1.2, 1.1]]} width={width * 0.83} color="#284248" opacity={ghost ? 0.08 : 0.82} />
    <Doors {...props} roof={roof} opacity={opacity} />
    <Hinge position={[-0.66, 1.12, 0]} axis="z" angle={open ? -1.12 : 0} reduced={reducedMotion}>
      <Box position={[-(length / 2 - 0.82) / 2, 0, 0]} size={[length / 2 - 0.82, 0.065, width * 0.88]} color={color} opacity={opacity} />
    </Hinge>
    <Hinge position={[1.08, 1.1, 0]} axis="z" angle={open ? 0.65 : 0} reduced={reducedMotion}>
      <Box position={[(length / 2 - 1.2) / 2, 0, 0]} size={[length / 2 - 1.2, 0.06, width * 0.87]} color={color} opacity={opacity} />
    </Hinge>
    <Bumpers {...props} opacity={opacity} />
    {[-1, 1].map(side => <Box key={side} position={[-0.55, 1.25, side * width * 0.57]} size={[0.18, 0.1, 0.15]} color={color} opacity={opacity} />)}
  </group>;
}
