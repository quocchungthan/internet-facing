import { Box, Pipe } from './primitives';
import { engineLayout } from '../../domain/engineLayout';

export function EngineBlock({ classic, cutaway = false }: { classic: boolean; cutaway?: boolean }) {
  if (!classic && cutaway) return <group scale={0.72}>
    <Box position={[0, -0.13, 0]} size={[0.52, 0.18, 0.7]} color="#59696c" />
    <Box position={[0, 0.04, -0.25]} size={[0.52, 0.26, 0.2]} color="#6c7b7d" />
    {[-1, 1].map(side => <Box key={side} position={[side * 0.225, 0.015, 0.03]}
      size={[0.07, 0.27, 0.52]} color="#6c7b7d" />)}
    {[-1, 1].flatMap(side => Array.from({ length: 3 }, (_, i) =>
      <mesh key={`${side}-${i}`} position={[side * 0.21, -0.02, -0.2 + i * 0.17]} rotation={[Math.PI / 2, 0, 0]}>
        <cylinderGeometry args={[0.018, 0.018, 0.025, 12]} />
        <meshStandardMaterial color="#c7d0ce" metalness={0.8} roughness={0.3} />
      </mesh>))}
  </group>;
  return <group scale={0.72}>
    <Box size={classic ? [0.75, 0.45, 0.58] : [0.52, 0.44, 0.7]} color={classic ? '#a74d2f' : '#6c7b7d'} />
    {[-1, 1].map(side => <Pipe key={side} points={[[-0.22, 0.05, side * 0.3], [-0.26, -0.13, side * 0.45], [0.1, -0.25, side * 0.4]]} color="#b49370" />)}
    {[-1, 1].flatMap(side => Array.from({ length: 4 }, (_, i) =>
      <mesh key={`${side}-${i}`} position={[-0.25 + i * 0.16, 0.08, side * 0.3]} rotation={[Math.PI / 2, 0, 0]}>
        <cylinderGeometry args={[0.025, 0.025, 0.025, 12]} />
        <meshStandardMaterial color="#c7d0ce" metalness={0.8} roughness={0.3} />
      </mesh>))}
  </group>;
}

export function EngineHead({ classic, cutaway = false }: { classic: boolean; cutaway?: boolean }) {
  if (!classic && cutaway) return <group>
    {engineLayout.contextCylinderCenters.map((position, index) => <mesh key={index} position={position} castShadow>
      <cylinderGeometry args={[0.08 * 0.72, 0.08 * 0.72, 0.22 * 0.72, 40, 1, true]} />
      <meshStandardMaterial color="#c6d0cf" metalness={0.8} roughness={0.3} />
    </mesh>)}
  </group>;
  return <group scale={0.72}>
    {Array.from({ length: classic ? 4 : 3 }, (_, i) =>
      <group key={i} position={classic ? [(i - 1.5) * 0.17, 0.26, 0] : [0, 0.24, (i - 1) * 0.21]}>
        {(classic ? [-1, 1] : [0]).map(bank => <mesh key={bank} position={[0, 0, bank * 0.2]} rotation={[bank * 0.45, 0, 0]} castShadow>
          <cylinderGeometry args={[0.08, 0.08, 0.22, 40]} />
          <meshStandardMaterial color="#c6d0cf" metalness={0.8} roughness={0.3} />
        </mesh>)}
      </group>)}
  </group>;
}

export function EngineCover({ classic, cutaway = false }: { classic: boolean; cutaway?: boolean }) {
  if (!classic && cutaway) return <group scale={0.72}>
    {[-1, 1].map(side => <Box key={side} position={[side * 0.22, 0.38, 0]}
      size={[0.1, 0.12, 0.68]} color="#262e32" />)}
    <Box position={[0, 0.38, -0.3]} size={[0.36, 0.12, 0.1]} color="#262e32" />
  </group>;
  return <group scale={0.72}>
    <Box position={[0, 0.38, 0]} size={classic ? [0.72, 0.13, 0.64] : [0.56, 0.12, 0.74]} color={classic ? '#a94c30' : '#262e32'} />
    {Array.from({ length: 5 }, (_, i) =>
      <Box key={i} position={[-0.25 + i * 0.12, 0.46, 0]} size={[0.025, 0.025, 0.5]} color="#808d8e" />)}
  </group>;
}

export function EngineCylinderWall() {
  return <group>
    <mesh position={[0, 0.12, 0]} castShadow>
      <cylinderGeometry args={[0.085, 0.085, 0.36, 48, 1, true, 0, Math.PI * 1.5]} />
      <meshStandardMaterial color="#aab8b5" metalness={0.82} roughness={0.28} />
    </mesh>
    {[-0.06, 0.3].map(y => <mesh key={y} position={[0, y, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <torusGeometry args={[0.085, 0.006, 10, 48]} />
      <meshStandardMaterial color="#c1cbc6" metalness={0.84} roughness={0.24} />
    </mesh>)}
  </group>;
}

export function EnginePiston() {
  return <group>
    <mesh position={[0, 0.025, 0]} castShadow>
      <cylinderGeometry args={[0.065, 0.067, 0.085, 48]} />
      <meshStandardMaterial color="#d2d6cb" metalness={0.82} roughness={0.24} />
    </mesh>
    {[0.006, 0.022, 0.038].map(y => <mesh key={y} position={[0, y, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <torusGeometry args={[0.066, 0.004, 8, 48]} />
      <meshStandardMaterial color="#596365" metalness={0.78} roughness={0.34} />
    </mesh>)}
    <mesh position={[0, -0.055, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <cylinderGeometry args={[0.018, 0.018, 0.15, 20]} />
      <meshStandardMaterial color="#788889" metalness={0.85} roughness={0.25} />
    </mesh>
  </group>;
}

export function EngineRod() {
  return <group>
    <Pipe points={[[0, 0.08, 0], [0.018, 0.035, 0], [0.05, -0.08, 0]]} radius={0.018} color="#b9c4bd" />
    <mesh position={[0, 0.08, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <torusGeometry args={[0.028, 0.009, 12, 32]} />
      <meshStandardMaterial color="#879692" metalness={0.8} roughness={0.26} />
    </mesh>
    <mesh position={[0.05, -0.08, 0]} rotation={[Math.PI / 2, 0, 0]}>
      <torusGeometry args={[0.04, 0.012, 12, 40]} />
      <meshStandardMaterial color="#879692" metalness={0.8} roughness={0.26} />
    </mesh>
  </group>;
}

export function EngineCrankshaft() {
  return <group>
    <mesh rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.025, 0.025, 0.58, 32]} />
      <meshStandardMaterial color="#778783" metalness={0.86} roughness={0.24} />
    </mesh>
    {[-0.13, 0.13].map(z => <mesh key={z} position={[0, 0, z]} rotation={[Math.PI / 2, 0, 0]}>
      <cylinderGeometry args={[0.052, 0.052, 0.035, 32]} />
      <meshStandardMaterial color="#98a49d" metalness={0.82} roughness={0.26} />
    </mesh>)}
    <mesh position={[0.05, 0.02, 0]} rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.022, 0.022, 0.48, 28]} />
      <meshStandardMaterial color="#c0c8be" metalness={0.84} roughness={0.24} />
    </mesh>
  </group>;
}
