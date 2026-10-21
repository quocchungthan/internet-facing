import { describe, expect, it } from 'vitest';
import { vehicleThemeStyle } from './vehicleTheme';

function luminance(color: string) {
  const channels = color.slice(1).match(/.{2}/g)!.map(channel => Number.parseInt(channel, 16) / 255);
  const [red, green, blue] = channels.map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
  return red * 0.2126 + green * 0.7152 + blue * 0.0722;
}

function contrast(first: string, second: string) {
  const [lighter, darker] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

describe('vehicleThemeStyle', () => {
  it('keeps the Raize red and derives a readable, repeatable accent from its paint', () => {
    const theme = vehicleThemeStyle('#c83f38');
    expect(theme['--vehicle-paint']).toBe('#c83f38');
    expect(theme['--accent']).toMatch(/^#([d-f][0-9a-f])([0-9a-f]{2})([0-9a-f]{2})$/i);
    expect(contrast(theme['--accent'], '#101716')).toBeGreaterThanOrEqual(4.5);
    expect(contrast(theme['--accent'], theme['--accent-ink'])).toBeGreaterThanOrEqual(4.5);
    expect(vehicleThemeStyle('#c83f38')).toEqual(theme);
  });

  it('retains the dark Impala paint hue while lifting its interface accent for contrast', () => {
    const theme = vehicleThemeStyle('#383e48');
    expect(theme['--vehicle-paint']).toBe('#383e48');
    const channels = theme['--accent'].slice(1).match(/.{2}/g)!.map(channel => Number.parseInt(channel, 16));
    expect(Math.max(...channels) - Math.min(...channels)).toBeLessThan(20);
    expect(contrast(theme['--accent'], '#101716')).toBeGreaterThanOrEqual(4.5);
    expect(contrast(theme['--accent'], theme['--accent-ink'])).toBeGreaterThanOrEqual(4.5);
  });

  it('rejects invalid paint values instead of silently applying an unrelated theme', () => {
    expect(() => vehicleThemeStyle('red')).toThrow('Expected a six-digit hex color.');
  });
});
