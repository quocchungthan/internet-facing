import { Box, Pipe } from './primitives';

export type DrawingProps = { classic: boolean; width: number; length: number; open: boolean; ghost: boolean; color: string; reducedMotion: boolean; cutaway?: boolean };

export function WheelDrawing({ classic }: DrawingProps) {
  return <group>
    <mesh castShadow receiveShadow>
      <torusGeometry args={[0.33, 0.12, 24, 80]} />
      <meshStandardMaterial color="#192024" roughness={0.9} />
    </mesh>
    <mesh rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.275, 0.275, 0.19, 64]} />
      <meshStandardMaterial color={classic ? '#a9b2b4' : '#333e43'} metalness={0.85} roughness={0.2} />
    </mesh>
    {Array.from({ length: classic ? 12 : 5 }, (_, i) => <group key={i} rotation={[0, 0, i * Math.PI * 2 / (classic ? 12 : 5)]}>
      <Box position={[0, 0.14, 0.11]} size={[classic ? 0.035 : 0.05, 0.24, 0.025]} color="#b8c3c4" />
    </group>)}
    <mesh position={[0, 0, 0.14]} rotation={[Math.PI / 2, 0, 0]}>
      <cylinderGeometry args={[classic ? 0.15 : 0.08, 0.08, 0.035, 40]} />
      <meshStandardMaterial color="#c1cccb" metalness={1} roughness={0.2} />
    </mesh>
    {Array.from({ length: 24 }, (_, i) => <group key={i} rotation={[0, 0, i * Math.PI / 12]}>
      <Box position={[0, 0.445, 0]} size={[0.025, 0.012, 0.14]} color="#303637" metalness={0} />
    </group>)}
  </group>;
}

export function CoolingDrawing({ width }: DrawingProps) {
  return <group>
    <Box size={[0.12, 0.55, width * 0.65]} color="#677779" />
    {Array.from({ length: 15 }, (_, i) => <Box key={i} position={[-0.08, 0, -width * 0.3 + i * width * 0.043]} size={[0.025, 0.5, 0.018]} color="#afc0bb" />)}
    <Pipe points={[[0, 0.25, 0.3], [0.25, 0.3, 0.3], [0.5, 0.1, 0.1]]} color="#303d3b" radius={0.045} />
  </group>;
}

export function IntakeDrawing({ classic }: DrawingProps) {
  return classic ? <group>
    <mesh position={[0, 0.08, 0]} castShadow><cylinderGeometry args={[0.3, 0.3, 0.13, 64]} /><meshStandardMaterial color="#9ca9a8" metalness={0.95} roughness={0.25} /></mesh>
    <Box size={[0.18, 0.18, 0.18]} color="#b9a078" />
  </group> : <group>
    <mesh rotation={[Math.PI / 2, 0, 0]}><torusGeometry args={[0.13, 0.065, 24, 48]} /><meshStandardMaterial color="#b9a688" metalness={0.8} roughness={0.35} /></mesh>
    <Pipe points={[[0, 0, 0], [0.15, 0.18, 0.18], [-0.25, 0.18, 0.4]]} color="#414f53" radius={0.06} />
  </group>;
}

export function TransmissionDrawing({ classic }: DrawingProps) {
  return <group rotation={classic ? [0, 0, 0] : [0, Math.PI / 2, 0]}>
    <Box size={[0.65, 0.32, 0.35]} color="#97a4a5" />
    <Box position={[-0.36, 0, 0]} size={[0.18, 0.42, 0.45]} color="#77898b" />
    {Array.from({ length: 5 }, (_, i) => <Box key={i} position={[-0.22 + i * 0.12, 0.18, 0]} size={[0.035, 0.04, 0.33]} color="#c3cdca" />)}
  </group>;
}

export function BrakeDrawing() {
  return <group>
    <mesh rotation={[Math.PI / 2, 0, 0]}><cylinderGeometry args={[0.2, 0.2, 0.04, 48]} /><meshStandardMaterial color="#8e9b9c" metalness={0.9} roughness={0.4} /></mesh>
    <Box position={[0.15, 0.03, 0]} size={[0.1, 0.21, 0.08]} color="#bc654f" />
  </group>;
}

export function SuspensionDrawing() {
  return <group>
    <mesh><cylinderGeometry args={[0.04, 0.04, 0.55, 24]} /><meshStandardMaterial color="#939d91" metalness={0.6} roughness={0.4} /></mesh>
    {Array.from({ length: 5 }, (_, i) => <mesh key={i} position={[0, -0.2 + i * 0.085, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <torusGeometry args={[0.075, 0.015, 12, 32]} /><meshStandardMaterial color="#bc9f58" metalness={0.65} roughness={0.4} />
    </mesh>)}
  </group>;
}

export function ElectricalDrawing() {
  return <group>
    <Box size={[0.32, 0.3, 0.25]} color="#242e30" />
    <Box position={[0, 0.16, 0]} size={[0.33, 0.025, 0.26]} color="#66776c" />
    <Box position={[-0.1, 0.19, 0]} size={[0.05, 0.04, 0.06]} color="#b75148" />
    <Box position={[0.1, 0.19, 0]} size={[0.05, 0.04, 0.06]} color="#a5b9b5" />
  </group>;
}

export function ExhaustDrawing({ length }: DrawingProps) {
  return <group>
    <Pipe points={[[-length * 0.3, 0, 0], [-0.4, 0, 0], [length * 0.32, 0, 0], [length * 0.38, 0, -0.15]]} radius={0.045} />
    <Box position={[length * 0.25, 0, 0]} size={[0.45, 0.15, 0.25]} color="#909a93" />
  </group>;
}
