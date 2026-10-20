import { Box3, Sphere, Vector3, type Object3D } from 'three';

export function subjectBounds(model: Object3D, ids: Set<string>) {
  model.updateWorldMatrix(true, true);
  const bounds = new Box3();
  model.traverse(object => {
    if (typeof object.userData.itemId === 'string' && ids.has(object.userData.itemId)) {
      bounds.expandByObject(object, true);
    }
  });
  if (bounds.isEmpty()) throw new Error('Selected subject has no drawable geometry.');
  return bounds.getBoundingSphere(new Sphere());
}

export function fitDistance(radius: number, verticalFov: number, aspect: number) {
  if (radius <= 0 || aspect <= 0 || verticalFov <= 0 || verticalFov >= 180) {
    throw new Error('Invalid subject or viewport bounds.');
  }
  const vertical = verticalFov * Math.PI / 360;
  const horizontal = Math.atan(Math.tan(vertical) * aspect);
  return radius / Math.sin(Math.min(vertical, horizontal)) * 1.3;
}

export const viewingDirection = new Vector3(-1, 0.65, 1).normalize();
