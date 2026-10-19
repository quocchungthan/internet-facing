import { readFileSync, statSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { vehicles } from '../content';
import { parseVehicle, readSelection } from './catalog';

describe('linked vehicle content', () => {
  it.each(vehicles)('$name has a book chapter and working part link for every selectable part', vehicle => {
    const book = readFileSync(resolve('public', vehicle.book.slice(1)), 'utf8');
    for (const part of vehicle.parts) {
      expect(book).toContain(`<a id="${part.id}"></a>`);
      expect(book).toContain(`/?car=${vehicle.id}&part=${part.id}`);
    }
    for (const photo of vehicle.photos) {
      expect(statSync(resolve('public', photo.src.slice(1))).size).toBeGreaterThan(1000);
      expect(book).toContain(photo.src);
      expect(photo.sourceUrl).toContain('commons.wikimedia.org/wiki/File:');
    }
  });

  it('rejects dangling references instead of silently dropping them', () => {
    const invalid = structuredClone(vehicles[0]);
    invalid.parts[0].relatedIds.push('missing-part');
    expect(() => parseVehicle(invalid)).toThrow('Unknown part "missing-part"');
    invalid.parts[0].relatedIds.pop();
    invalid.parts[0].sourceIds.push('not-a-source');
    expect(() => parseVehicle(invalid)).toThrow('Unknown source "not-a-source"');
  });

  it('rejects duplicate part identities', () => {
    const invalid = structuredClone(vehicles[0]);
    invalid.parts.push(invalid.parts[0]);
    expect(() => parseVehicle(invalid)).toThrow('Duplicate part ID');
  });
  it('rejects content that cannot satisfy the geometry contract', () => {
    const invalid = structuredClone(vehicles[0]);
    invalid.parts = invalid.parts.filter(p => p.id !== 'engine');
    expect(() => parseVehicle(invalid)).toThrow('Missing schematic part "engine"');
  });

  it('rejects dangling photo and fact references', () => {
    const invalid = structuredClone(vehicles[0]);
    invalid.parts[0].photoIds.push('unknown-photo');
    expect(() => parseVehicle(invalid)).toThrow('Unknown photo');
    invalid.parts[0].photoIds.pop();
    invalid.facts[0].sourceId = 'unknown-source';
    expect(() => parseVehicle(invalid)).toThrow('Unknown fact source');
  });
});

describe('root URL selection', () => {
  it('resolves a deep selection without a nested path', () => {
    const selection = readSelection('?car=impala&part=transmission', vehicles);
    expect(selection.vehicle.id).toBe('impala');
    expect(selection.part?.label).toBe('Powerglide & driveline');
    expect(selection.invalid).toBe(false);
  });
  it('reports invalid selections and keeps the accessible catalog available', () => {
    const selection = readSelection('?car=unknown&part=missing', vehicles);
    expect(selection.vehicle.id).toBe('raize');
    expect(selection.part).toBeUndefined();
    expect(selection.invalid).toBe(true);
  });
  it('treats an absent selection as a normal first visit', () => {
    expect(readSelection('', vehicles).invalid).toBe(false);
  });
});
