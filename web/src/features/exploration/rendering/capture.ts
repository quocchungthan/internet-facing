import type { WebGLRenderer, Scene, Camera } from 'three';
import type { Part, Vehicle } from '../../../domain/catalog';

export function captureSchematic(gl: WebGLRenderer, scene: Scene, camera: Camera, vehicle: Vehicle, part?: Part) {
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
}
