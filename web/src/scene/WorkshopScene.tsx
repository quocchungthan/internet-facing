import { Component, Suspense, useEffect, useRef, useState, type ReactNode } from 'react';
import { Canvas, useThree } from '@react-three/fiber';
import { Grid, OrbitControls } from '@react-three/drei';
import type { OrbitControls as OrbitControlsImpl } from 'three-stdlib';
import type { Part, Vehicle, ViewMode } from '../domain/catalog';
import CarModel from './CarModel';

export type CameraAction = { id: number; kind: 'reset' | 'left' | 'right' | 'zoom-in' | 'zoom-out' | 'capture' };
type Props = {
  vehicle: Vehicle; part?: Part; mode: ViewMode; open: boolean; labels: boolean;
  reducedMotion: boolean; action?: CameraAction; onSelect: (p: Part) => void;
  onError: (message: string) => void;
};

class SceneBoundary extends Component<{ children: ReactNode; onError: (message: string) => void }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error) { this.props.onError(`3D view unavailable: ${error.message}. You can still explore every part in the component index.`); }
  render() { return this.state.failed ? null : this.props.children; }
}

function Controls({ action, vehicle, part, onError }: Pick<Props, 'action' | 'vehicle' | 'part' | 'onError'>) {
  const controls = useRef<OrbitControlsImpl>(null);
  const handledAction = useRef<number | undefined>(undefined);
  const { gl, scene, camera, size } = useThree();
  useEffect(() => {
    const ratio = size.width / size.height;
    const distance = ratio < 0.8 ? 1.65 : ratio < 1.2 ? 1.15 : 1;
    camera.position.set(-5.7 * distance, 3.7 * distance, 5.7 * distance);
    controls.current?.update();
  }, [size.width, size.height, camera, vehicle.id]);
  useEffect(() => {
    const node = gl.domElement;
    const lost = (event: Event) => { event.preventDefault(); onError('The graphics context was lost. Reload to restore 3D, or use the component index.'); };
    node.addEventListener('webglcontextlost', lost);
    return () => node.removeEventListener('webglcontextlost', lost);
  }, [gl, onError]);
  useEffect(() => {
    if (!action || !controls.current || handledAction.current === action.id) return;
    handledAction.current = action.id;
    const c = controls.current;
    const offset = camera.position.clone().sub(c.target);
    if (action.kind === 'reset') {
      const distance = size.width / size.height < 0.8 ? 1.65 : size.width / size.height < 1.2 ? 1.15 : 1;
      camera.position.set(-5.7 * distance, 3.7 * distance, 5.7 * distance);
      c.target.set(0, 0.8, 0);
    } else if (action.kind === 'left' || action.kind === 'right') {
      const angle = action.kind === 'left' ? 0.3 : -0.3;
      const x = offset.x * Math.cos(angle) - offset.z * Math.sin(angle);
      const z = offset.x * Math.sin(angle) + offset.z * Math.cos(angle);
      camera.position.set(c.target.x + x, camera.position.y, c.target.z + z);
    } else if (action.kind === 'zoom-in' || action.kind === 'zoom-out') {
      const factor = action.kind === 'zoom-in' ? 0.85 : 1.15;
      offset.multiplyScalar(factor).clampLength(3.5, 14);
      camera.position.copy(c.target).add(offset);
    } else if (action.kind === 'capture') {
      try {
        gl.render(scene, camera);
        const capture = document.createElement('canvas');
        capture.width = gl.domElement.width;
        capture.height = gl.domElement.height;
        const context = capture.getContext('2d');
        if (!context) throw new Error('PNG compositing is unavailable.');
        context.drawImage(gl.domElement, 0, 0);
        const scale = Math.max(1, capture.width / 1440);
        context.fillStyle = '#101716';
        context.fillRect(0, capture.height - 65 * scale, capture.width, 65 * scale);
        context.fillStyle = '#d9f690';
        context.font = `${16 * scale}px sans-serif`;
        context.fillText(`30s / ${vehicle.year} ${vehicle.name}${part ? ` / ${part.label}` : ''}`, 22 * scale, capture.height - 37 * scale);
        context.fillStyle = '#c4ceba';
        context.font = `${11 * scale}px sans-serif`;
        context.fillText('Educational schematic - approximate geometry, not an OEM blueprint or repair reference.', 22 * scale, capture.height - 16 * scale);
        const a = document.createElement('a');
        a.download = `30s-${vehicle.id}${part ? `-${part.id}` : ''}-schematic.png`;
        a.href = capture.toDataURL('image/png');
        a.click();
      } catch (error) {
        onError(`Could not capture the view: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    c.update();
    gl.render(scene, camera);
  }, [action, camera, gl, scene, vehicle.id, part, onError, size.width, size.height]);
  return <OrbitControls ref={controls} makeDefault target={[0, 0.8, 0]} enableDamping
    dampingFactor={0.08} minDistance={3.5} maxDistance={14} maxPolarAngle={Math.PI / 2.05}
    minPolarAngle={0.15} enablePan={false} />;
}

export default function WorkshopScene(props: Props) {
  const [portal, setPortal] = useState<HTMLDivElement | null>(null);
  return <SceneBoundary onError={props.onError}>
    <>
    <Canvas shadows camera={{ position: [-5.7, 3.7, 5.7], fov: 38 }}
      dpr={[1, 1.5]} gl={{ antialias: true, preserveDrawingBuffer: true }}
      fallback={<div className="scene-fallback">WebGL is unavailable. Use the component index to explore the car.</div>}>
      <color attach="background" args={['#101716']} />
      <fog attach="fog" args={['#101716', 13, 30]} />
      <ambientLight intensity={1.05} />
      <hemisphereLight args={['#e4f5dd', '#1b2b28', 2]} />
      <directionalLight position={[-3, 7, 4]} intensity={3.5} castShadow shadow-mapSize={[1024, 1024]}
        shadow-camera-left={-6} shadow-camera-right={6} shadow-camera-top={6} shadow-camera-bottom={-6}
        shadow-bias={-0.001} />
      <directionalLight position={[4, 3, -5]} intensity={3} color="#85b5b3" />
      <directionalLight position={[-4, 2, -2]} intensity={1} color="#e8d6ac" />
      <Suspense fallback={null}>
        {portal && <CarModel key={props.vehicle.id} vehicle={props.vehicle} selected={props.part} mode={props.mode}
          open={props.open} labels={props.labels} reducedMotion={props.reducedMotion} onSelect={props.onSelect} portal={{ current: portal }} />}
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.02, 0]} receiveShadow>
          <planeGeometry args={[40, 40]} /><shadowMaterial transparent opacity={0.3} />
        </mesh>
      </Suspense>
      <Grid position={[0, -0.01, 0]} args={[40, 40]} cellSize={0.5} cellThickness={0.5} cellColor="#293c35"
        sectionSize={2.5} sectionThickness={0.65} sectionColor="#3b5146" fadeDistance={20} fadeStrength={2} infiniteGrid />
      <Controls action={props.action} vehicle={props.vehicle} part={props.part} onError={props.onError} />
    </Canvas>
    <div className="scene-labels" ref={setPortal} />
    </>
  </SceneBoundary>;
}
