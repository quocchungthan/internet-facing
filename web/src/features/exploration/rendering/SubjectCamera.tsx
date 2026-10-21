import { useEffect, useMemo, useRef } from 'react';
import { useFrame, useThree } from '@react-three/fiber';
import { OrbitControls } from '@react-three/drei';
import { Object3D, PerspectiveCamera, Sphere } from 'three';
import type { OrbitControls as OrbitControlsImpl } from 'three-stdlib';
import type { Part, Vehicle, ViewMode } from '../../../domain/catalog';
import { createVehicleItems, specificationFor, subtreeIds } from '../domain/items';
import { captureSchematic } from './capture';
import { fitDistance, subjectBounds, viewingDirection } from './subjectBounds';

export type CameraAction = { id: number; kind: 'reset' | 'focus' | 'left' | 'right' | 'zoom-in' | 'zoom-out' | 'capture' };
function focusesSelection(kind: CameraAction['kind']) {
  return kind === 'reset' || kind === 'focus';
}

type Props = {
  vehicle: Vehicle; part?: Part; selectedId: string; isolated: boolean; mode: ViewMode;
  open: boolean; reducedMotion: boolean; action?: CameraAction; onError: (message: string) => void;
};

export default function SubjectCamera(props: Props) {
  const { vehicle, selectedId, isolated, mode, open, reducedMotion, action, part, onError } = props;
  const controls = useRef<OrbitControlsImpl>(null);
  const handledAction = useRef<number | undefined>(action?.id);
  const interactedBeforeFit = useRef(false);
  const initialFitScheduled = useRef(false);
  const pendingFit = useRef(2);
  const sphere = useRef(new Sphere());
  const { gl, scene, camera, size, invalidate } = useThree();
  const tree = useMemo(() => createVehicleItems(vehicle), [vehicle]);
  const selected = useMemo(() => subtreeIds(tree, selectedId), [tree, selectedId]);
  const hasGeometry = useMemo(() => tree.items.some(item => selected.has(item.id)
    && specificationFor(tree, item).drawing !== 'assembly'), [tree, selected]);

  useEffect(() => {
    pendingFit.current = initialFitScheduled.current ? 1 : reducedMotion ? 2 : 20;
    initialFitScheduled.current = true;
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

  function publishCanvasState(model: Object3D, focusable: boolean, radius: number) {
    const visible: string[] = [];
    model.traverse(object => { if (object.userData.itemId) visible.push(object.userData.itemId); });
    Object.assign(gl.domElement.dataset, {
      ready: 'true', focusable: String(focusable), subject: selectedId, isolated: String(isolated),
      items: visible.join(','), triangles: String(gl.info.render.triangles),
      drawCalls: String(gl.info.render.calls), radius: String(radius),
    });
  }

  function completePendingFit(model: Object3D) {
    if (!hasGeometry) {
      publishCanvasState(model, false, 0);
      return false;
    }
    try {
      sphere.current = subjectBounds(model, selected);
      fit();
      publishCanvasState(model, true, sphere.current.radius);
    } catch (error) {
      onError(error instanceof Error ? error.message : String(error));
    }
    return true;
  }

  function preserveCurrentView(model: Object3D) {
    if (!hasGeometry) {
      publishCanvasState(model, false, 0);
      return false;
    }
    try {
      sphere.current = subjectBounds(model, selected);
      configureCamera(true);
      publishCanvasState(model, true, sphere.current.radius);
    } catch (error) {
      onError(error instanceof Error ? error.message : String(error));
      return false;
    }
    return true;
  }

  useFrame(() => {
    const c = controls.current;
    const model = scene.getObjectByName('vehicle-model');
    if (!c || !(camera instanceof PerspectiveCamera)) return;
    if (pendingFit.current > 0 || interactedBeforeFit.current) invalidate();
    if (!model) return;
    if (interactedBeforeFit.current) {
      interactedBeforeFit.current = false;
      if (!preserveCurrentView(model)) return;
    } else if (pendingFit.current > 0 && --pendingFit.current === 0) {
      if (!completePendingFit(model)) return;
    }
    gl.domElement.dataset.cameraDistance = String(camera.position.distanceTo(c.target));
    gl.domElement.dataset.cameraPosition = camera.position.toArray().join(',');
    gl.domElement.dataset.target = c.target.toArray().join(',');
    if (pendingFit.current > 0 || !action || handledAction.current === action.id) return;
    handledAction.current = action.id;
    performAction();
    invalidate();
  });

  function preserveInteraction() {
    if (pendingFit.current === 0) return;
    const model = scene.getObjectByName('vehicle-model');
    pendingFit.current = 0;
    if (!model) {
      interactedBeforeFit.current = true;
      invalidate();
      return;
    }
    if (!preserveCurrentView(model)) return;
    if (action && handledAction.current !== action.id) {
      handledAction.current = action.id;
      performAction();
    }
    invalidate();
  }

  function configureCamera(preserveDistance = false) {
    const c = controls.current;
    if (!c || !(camera instanceof PerspectiveCamera)) return undefined;
    const subject = sphere.current;
    const distance = fitDistance(subject.radius, camera.fov, size.width / size.height);
    const currentDistance = camera.position.distanceTo(c.target);
    c.minDistance = preserveDistance ? Math.min(subject.radius * 1.15, currentDistance) : subject.radius * 1.15;
    c.maxDistance = preserveDistance ? Math.max(distance * 4, currentDistance) : distance * 4;
    camera.near = Math.max(0.005, subject.radius / 100);
    camera.far = Math.max(100, distance * 10);
    camera.updateProjectionMatrix();
    return distance;
  }

  function fit() {
    const c = controls.current;
    if (!c || !(camera instanceof PerspectiveCamera)) return;
    const subject = sphere.current;
    const distance = configureCamera();
    if (distance === undefined) return;
    c.target.copy(subject.center);
    camera.position.copy(subject.center).addScaledVector(viewingDirection, distance);
    c.update();
  }

  function performAction() {
    const c = controls.current;
    if (!c || !action) return;
    if (focusesSelection(action.kind) && !hasGeometry) {
      onError('This subject has no drawable geometry to focus. Use the component index to explore available geometry.');
      return;
    }
    const offset = camera.position.clone().sub(c.target);
    switch (action.kind) {
      case 'reset':
      case 'focus':
        fit();
        break;
      case 'left':
        offset.applyAxisAngle(camera.up, 0.3);
        camera.position.copy(c.target).add(offset);
        break;
      case 'right':
        offset.applyAxisAngle(camera.up, -0.3);
        camera.position.copy(c.target).add(offset);
        break;
      case 'zoom-in':
        offset.multiplyScalar(0.85).clampLength(c.minDistance, c.maxDistance);
        camera.position.copy(c.target).add(offset);
        break;
      case 'zoom-out':
        offset.multiplyScalar(1.15).clampLength(c.minDistance, c.maxDistance);
        camera.position.copy(c.target).add(offset);
        break;
      case 'capture':
        try {
          void captureSchematic(gl, scene, camera, vehicle, part)
            .catch(error => onError(`Could not capture the view: ${error instanceof Error ? error.message : String(error)}`));
        }
        catch (error) { onError(`Could not capture the view: ${error instanceof Error ? error.message : String(error)}`); }
        break;
    }
    c.update();
  }

  return <OrbitControls ref={controls} makeDefault onStart={preserveInteraction}
    enableDamping={!reducedMotion} dampingFactor={0.08}
    maxPolarAngle={Math.PI - 0.15} minPolarAngle={0.15} enablePan={false} />;
}
