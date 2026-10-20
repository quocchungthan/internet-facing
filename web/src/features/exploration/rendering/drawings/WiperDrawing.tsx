import { Box } from './primitives';

export function WiperArm() {
  return <group>
    <mesh rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.032, 0.04, 0.032, 24]} />
      <meshStandardMaterial color="#aebcbe" metalness={0.8} roughness={0.3} />
    </mesh>
    <group rotation={[0, 0.35, 0]}>
      <Box position={[0, 0.025, 0.12]} size={[0.025, 0.025, 0.24]} color="#aebcbe" />
    </group>
    <Box position={[0.08, 0.027, 0.34]} size={[0.022, 0.022, 0.23]} color="#aebcbe" />
  </group>;
}

export function WiperBlade() {
  return <group>
    <Box size={[0.04, 0.025, 0.54]} color="#c79a51" />
    <Box position={[0, 0.025, 0]} size={[0.065, 0.035, 0.075]} color="#c79a51" />
    {[-0.18, 0.18].map(z => <Box key={z} position={[0, -0.012, z]} size={[0.05, 0.02, 0.025]} color="#8b795c" />)}
  </group>;
}

export function WiperRubber() {
  return <group>
    <Box size={[0.024, 0.02, 0.55]} color="#293132" metalness={0} />
    <Box position={[0, -0.013, 0]} size={[0.009, 0.013, 0.55]} color="#111716" metalness={0} />
  </group>;
}
