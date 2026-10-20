import { useEffect, useMemo, useRef } from 'react';
import { useFrame, useThree } from '@react-three/fiber';
import { OrbitControls } from '@react-three/drei';
import { PerspectiveCamera, Sphere } from 'three';
import type { OrbitControls as OrbitControlsImpl } from 'three-stdlib';
import type { Part, Vehicle, ViewMode } from '../../../domain/catalog';
import { createVehicleItems, subtreeIds } from '../domain/items';
import { captureSchematic } from './capture';
import { fitDistance, subjectBounds, viewingDirection } from './subjectBounds';

export type CameraAction = { id: number; kind: 'reset' | 'focus' | 'left' | 'right' | 'zoom-in' | 'zoom-out' | 'capture' };
type Props = {
  vehicle: Vehicle; part?: Part; selectedId: string; isolated: boolean; mode: ViewMode;
  open: boolean; reducedMotion: boolean; action?: CameraAction; onError: (message: string) => void;
};

export default function SubjectCamera(props: Props) {
  const { vehicle, selectedId, isolated, mode, open, reducedMotion, action, part, onError } = props;
  const controls = useRef<OrbitControlsImpl>(null);
  const handledAction = useRef<number | undefined>(action?.id);
  const pendingFit = useRef(2);
  const sphere = useRef(new Sphere());
  const { gl, scene, camera, size, invalidate } = useThree();
  const tree = useMemo(() => createVehicleItems(vehicle), [vehicle]);
  const selected = useMemo(() => subtreeIds(tree, selectedId), [tree, selectedId]);

  useEffect(() => {
    pendingFit.current = reducedMotion ? 2 : 20;
    gl.domElement.dataset.ready = 'false';
    invalidate();
  }, [vehicle.id, selectedId, isolated, mode, open, reducedMotion, size.width, size.height, gl, invalidate]);

  useEffect(() => { invalidate(); }, [action, invalidate]);

  useEffect(() => {
    const node = gl.domElement;
    const lost = (event: Event) => { event.preventDefault(); onError('The graphics context was lost. Reload to restore 3D, or use the component index.'); };
    node.addEventListener('webglcontextlost', lost);
    return () => node.removeEventListener('webglcontextlost', lost);
  }, [gl, onError]);

  useFrame(() => {
    const c = controls.current;
    const model = scene.getObjectByName('vehicle-model');
    if (!c || !model || !(camera instanceof PerspectiveCamera)) return;
    if (pendingFit.current > 0) invalidate();
    if (pendingFit.current > 0 && --pendingFit.current === 0) {
      try {
        sphere.current = subjectBounds(model, selected);
        fit();
        const visible: string[] = [];
        model.traverse(object => { if (object.userData.itemId) visible.push(object.userData.itemId); });
        Object.assign(gl.domElement.dataset, {
          ready: 'true', subject: selectedId, isolated: String(isolated), items: visible.join(','),
          triangles: String(gl.info.render.triangles), drawCalls: String(gl.info.render.calls),
          radius: String(sphere.current.radius),
        });
      } catch (error) {
        onError(error instanceof Error ? error.message : String(error));
      }
    }
    gl.domElement.dataset.cameraDistance = String(camera.position.distanceTo(c.target));
    gl.domElement.dataset.target = c.target.toArray().join(',');
    if (pendingFit.current > 0 || !action || handledAction.current === action.id) return;
    handledAction.current = action.id;
    performAction();
    invalidate();
  });

  function fit() {
    const c = controls.current;
    if (!c || !(camera instanceof PerspectiveCamera)) return;
    const subject = sphere.current;
    const distance = fitDistance(subject.radius, camera.fov, size.width / size.height);
    c.target.copy(subject.center);
    c.minDistance = subject.radius * 1.15;
    c.maxDistance = distance * 4;
    camera.near = Math.max(0.005, subject.radius / 100);
    camera.far = Math.max(100, distance * 10);
    camera.updateProjectionMatrix();
    camera.position.copy(subject.center).addScaledVector(viewingDirection, distance);
    c.update();
  }

  function performAction() {
    const c = controls.current;
    if (!c || !action) return;
    const offset = camera.position.clone().sub(c.target);
    if (action.kind === 'reset' || action.kind === 'focus') fit();
    if (action.kind === 'left' || action.kind === 'right') {
      offset.applyAxisAngle(camera.up, action.kind === 'left' ? 0.3 : -0.3);
      camera.position.copy(c.target).add(offset);
    }
    if (action.kind === 'zoom-in' || action.kind === 'zoom-out') {
      offset.multiplyScalar(action.kind === 'zoom-in' ? 0.85 : 1.15).clampLength(c.minDistance, c.maxDistance);
      camera.position.copy(c.target).add(offset);
    }
    if (action.kind === 'capture') {
      try { captureSchematic(gl, scene, camera, vehicle, part); }
      catch (error) { onError(`Could not capture the view: ${error instanceof Error ? error.message : String(error)}`); }
    }
    c.update();
  }

  return <OrbitControls ref={controls} makeDefault enableDamping={!reducedMotion} dampingFactor={0.08}
    maxPolarAngle={Math.PI - 0.15} minPolarAngle={0.15} enablePan={false} />;
}
