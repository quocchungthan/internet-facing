import type { CSSProperties } from 'react';

type ThemeStyle = CSSProperties & {
  '--vehicle-paint': string;
  '--accent': string;
  '--accent-ink': string;
  '--accent-soft': string;
  '--accent-border': string;
  '--accent-hover': string;
};

type RGB = [number, number, number];

const background: RGB = [16, 23, 22];

function parseColor(color: string): RGB {
  const match = /^#([0-9a-f]{6})$/i.exec(color);
  if (!match) throw new Error(`Invalid vehicle paint color "${color}". Expected a six-digit hex color.`);
  return [0, 2, 4].map(offset => Number.parseInt(match[1].slice(offset, offset + 2), 16)) as RGB;
}

function luminance([red, green, blue]: RGB) {
  const linear = [red, green, blue].map(channel => {
    const value = channel / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
}

function contrast(first: RGB, second: RGB) {
  const [lighter, darker] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

function mixWithWhite(color: RGB, amount: number): RGB {
  return color.map(channel => Math.round(channel + (255 - channel) * amount)) as RGB;
}

function hex([red, green, blue]: RGB) {
  return `#${[red, green, blue].map(channel => channel.toString(16).padStart(2, '0')).join('')}`;
}

function rgba([red, green, blue]: RGB, alpha: number) {
  return `rgb(${red} ${green} ${blue} / ${alpha})`;
}

export function vehicleThemeStyle(paint: string): ThemeStyle {
  const rgb = parseColor(paint);
  let accent = rgb;
  for (let amount = 0; contrast(accent, background) < 4.5 && amount < 1; amount += 0.01) {
    accent = mixWithWhite(rgb, amount + 0.01);
  }
  const ink = contrast(background, accent) >= contrast([255, 255, 255], accent) ? hex(background) : '#ffffff';
  const hover = mixWithWhite(accent, 0.12);

  return {
    '--vehicle-paint': paint,
    '--accent': hex(accent),
    '--accent-ink': ink,
    '--accent-soft': rgba(accent, 0.1),
    '--accent-border': rgba(accent, 0.38),
    '--accent-hover': hex(hover),
  };
}
