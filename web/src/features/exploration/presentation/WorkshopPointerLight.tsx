import { useEffect, useRef } from 'react';

export function WorkshopPointerLight({ reducedMotion }: { reducedMotion: boolean }) {
  const light = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const element = light.current;
    const scene = element?.parentElement?.querySelector('.scene');
    if (!element || !scene || reducedMotion) return;
    const pointer = window.matchMedia('(hover: hover) and (pointer: fine)');
    let frame = 0;
    const reset = () => {
      window.cancelAnimationFrame(frame);
      frame = 0;
      element.style.opacity = '0';
    };
    const move = (event: Event) => {
      if (!(event instanceof PointerEvent) || !pointer.matches || event.pointerType !== 'mouse' || event.buttons !== 0) {
        reset();
        return;
      }
      window.cancelAnimationFrame(frame);
      frame = window.requestAnimationFrame(() => {
        const bounds = scene.getBoundingClientRect();
        const x = Math.min(1, Math.max(0, (event.clientX - bounds.left) / bounds.width));
        const y = Math.min(1, Math.max(0, (event.clientY - bounds.top) / bounds.height));
        element.style.setProperty('--pointer-light-x', `${45 + x * 10}%`);
        element.style.setProperty('--pointer-light-y', `${45 + y * 10}%`);
        element.style.opacity = '1';
        frame = 0;
      });
    };
    scene.addEventListener('pointermove', move);
    scene.addEventListener('pointerdown', reset);
    scene.addEventListener('pointerleave', reset);
    pointer.addEventListener('change', reset);
    return () => {
      reset();
      scene.removeEventListener('pointermove', move);
      scene.removeEventListener('pointerdown', reset);
      scene.removeEventListener('pointerleave', reset);
      pointer.removeEventListener('change', reset);
    };
  }, [reducedMotion]);
  return <div ref={light} className="workshop-pointer-light" aria-hidden="true" />;
}
