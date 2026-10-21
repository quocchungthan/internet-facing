import type { WebGLRenderer, Scene, Camera } from 'three';
import type { Part, Vehicle } from '../../../domain/catalog';
import { vehicleThemeStyle } from '../presentation/vehicleTheme';

export function captureSchematic(gl: WebGLRenderer, scene: Scene, camera: Camera, vehicle: Vehicle, part?: Part): Promise<void> {
  gl.render(scene, camera);
  const capture = document.createElement('canvas');
  const scale = Math.min(1, 1024 / gl.domElement.width);
  capture.width = Math.round(gl.domElement.width * scale);
  capture.height = Math.round(gl.domElement.height * scale);
  const context = capture.getContext('2d');
  if (!context) throw new Error('PNG compositing is unavailable.');
  context.drawImage(gl.domElement, 0, 0, capture.width, capture.height);
  const textScale = Math.max(1, capture.width / 1440);
  context.fillStyle = '#101716';
  context.fillRect(0, capture.height - 65 * textScale, capture.width, 65 * textScale);
  context.fillStyle = vehicleThemeStyle(vehicle.color)['--accent'];
  context.font = `${16 * textScale}px sans-serif`;
  context.fillText(`30s / ${vehicle.year} ${vehicle.name}${part ? ` / ${part.label}` : ''}`, 22 * textScale, capture.height - 37 * textScale);
  context.fillStyle = '#c4ceba';
  context.font = `${11 * textScale}px sans-serif`;
  context.fillText('Educational schematic - approximate geometry, not an OEM blueprint or repair reference.', 22 * textScale, capture.height - 16 * textScale);
  return new Promise((resolve, reject) => capture.toBlob(blob => {
    if (!blob) {
      reject(new Error('PNG encoding failed.'));
      return;
    }
    try {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.download = `30s-${vehicle.id}${part ? `-${part.id}` : ''}-schematic.png`;
      a.href = url;
      a.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      resolve();
    } catch (error) {
      reject(error);
    }
  }, 'image/png'));
}
