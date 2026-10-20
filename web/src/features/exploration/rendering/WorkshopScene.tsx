import { Component, Suspense, useState, type ReactNode } from 'react';
import { Canvas } from '@react-three/fiber';
import { Grid } from '@react-three/drei';
import type { Part, Vehicle, ViewMode } from '../../../domain/catalog';
import CarModel from './CarModel';
import SubjectCamera, { type CameraAction } from './SubjectCamera';

export type { CameraAction } from './SubjectCamera';
type Props = {
  vehicle: Vehicle; part?: Part; selectedId: string; isolated: boolean; mode: ViewMode;
  open: boolean; labels: boolean; reducedMotion: boolean; action?: CameraAction;
  onSelect: (id: string) => void; onError: (message: string) => void;
};

class SceneBoundary extends Component<{ children: ReactNode; onError: (message: string) => void }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error) { this.props.onError(`3D view unavailable: ${error.message}. You can still explore every part in the component index.`); }
  render() { return this.state.failed ? null : this.props.children; }
}

export default function WorkshopScene(props: Props) {
  const [portal, setPortal] = useState<HTMLDivElement | null>(null);
  return <SceneBoundary key={props.vehicle.id} onError={props.onError}>
    <Canvas shadows frameloop="demand" camera={{ position: [-5.7, 3.7, 5.7], fov: 38 }}
      dpr={[1, 2]} gl={{ antialias: true, preserveDrawingBuffer: true }}
      fallback={<div className="scene-fallback">WebGL is unavailable. Use the component index to explore the car.</div>}>
      <color attach="background" args={['#101716']} />
      <ambientLight intensity={1.05} />
      <hemisphereLight args={['#e4f5dd', '#1b2b28', 2]} />
      <directionalLight position={[-3, 7, 4]} intensity={3.5} castShadow shadow-mapSize={[1024, 1024]}
        shadow-camera-left={-6} shadow-camera-right={6} shadow-camera-top={6} shadow-camera-bottom={-6}
        shadow-bias={-0.001} />
      <directionalLight position={[4, 3, -5]} intensity={3} color="#85b5b3" />
      <directionalLight position={[-4, 2, -2]} intensity={1} color="#e8d6ac" />
      <Suspense fallback={null}>
        {portal && <CarModel key={props.vehicle.id} vehicle={props.vehicle} selectedId={props.selectedId}
          isolated={props.isolated} mode={props.mode} open={props.open} labels={props.labels}
          reducedMotion={props.reducedMotion} onSelect={props.onSelect} portal={{ current: portal }} />}
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.02, 0]} receiveShadow>
          <planeGeometry args={[40, 40]} /><shadowMaterial transparent opacity={0.3} />
        </mesh>
      </Suspense>
      <Grid position={[0, -0.01, 0]} args={[40, 40]} cellSize={0.5} cellThickness={0.5} cellColor="#293c35"
        sectionSize={2.5} sectionThickness={0.65} sectionColor="#3b5146" fadeDistance={20} fadeStrength={2} infiniteGrid />
      <SubjectCamera {...props} />
    </Canvas>
    <div className="scene-labels" ref={setPortal} />
  </SceneBoundary>;
}
