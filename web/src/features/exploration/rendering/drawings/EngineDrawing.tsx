import { Box, Pipe } from './primitives';

export function EngineBlock({ classic }: { classic: boolean }) {
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

export function EngineHead({ classic }: { classic: boolean }) {
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

export function EngineCover({ classic }: { classic: boolean }) {
  return <group scale={0.72}>
    <Box position={[0, 0.38, 0]} size={classic ? [0.72, 0.13, 0.64] : [0.56, 0.12, 0.74]} color={classic ? '#a94c30' : '#262e32'} />
    {Array.from({ length: 5 }, (_, i) =>
      <Box key={i} position={[-0.25 + i * 0.12, 0.46, 0]} size={[0.025, 0.025, 0.5]} color="#808d8e" />)}
  </group>;
}
