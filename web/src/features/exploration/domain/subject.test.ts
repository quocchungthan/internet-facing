import { expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { readSubject, subjectSearch } from './subject';

it('preserves old part links and shares a specific isolated occurrence', () => {
  expect(readSubject('?part=engine', vehicles[0]).item.id).toBe('engine');
  const search = subjectSearch('?other=keep', vehicles[0], 'wheel-front-left', true);
  const result = readSubject(search, vehicles[0]);
  expect(result.item.id).toBe('wheel-front-left');
  expect(result.isolated).toBe(true);
  expect(search).toContain('other=keep');
  expect(search).toContain('part=wheels');
});
it('rejects unknown subjects and resets root isolation', () => {
  expect(readSubject('?item=missing&isolate=1', vehicles[0])).toMatchObject({ invalid: true, isolated: false, item: { id: 'vehicle' } });
  expect(subjectSearch('?part=engine&item=engine&isolate=1', vehicles[1], 'vehicle', true)).toBe('car=impala');
  expect(() => subjectSearch('', vehicles[0], 'missing', false)).toThrow('Unknown selected item');
});
