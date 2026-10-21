import { describe, expect, it } from 'vitest';
import lessonContent from '../content/engine-lessons.json';
import { vehicles } from '../content';
import { engineLessonItemIds, lessonFor, parseEngineLessons } from './engineLessons';

const sources = vehicles.find(vehicle => vehicle.id === 'raize')!.sources;
const parse = (input: unknown) => parseEngineLessons(input, sources);

describe('Raize engine lessons', () => {
  it('loads the seven scoped lessons with resolved sources and roster connections', () => {
    const lessons = parse(lessonContent);
    expect(lessons.map(lesson => lesson.itemId)).toEqual(engineLessonItemIds);
    expect(lessons.every(lesson => lesson.sourceRefs.length > 0 && lesson.connections.length > 0)).toBe(true);
    expect(lessonFor(lessons, 'raize', 'engine-rod-study')).toMatchObject({ status: 'ready', lesson: { label: 'Connecting rod' } });
    expect(lessonFor(lessons, 'impala', 'engine-rod-study')).toMatchObject({ status: 'missing' });
    expect(lessonFor(lessons, 'raize', 'engine')).toMatchObject({ status: 'missing' });
  });

  it('rejects duplicate IDs and incomplete roster records', () => {
    expect(() => parse([...lessonContent, lessonContent[0]])).toThrow('Duplicate engine lesson');
    expect(() => parse(lessonContent.slice(1))).toThrow('seven-subject roster');
  });

  it('rejects unknown sources and lesson connections', () => {
    const unknownSource = structuredClone(lessonContent);
    unknownSource[0].sourceRefs[0].sourceId = 'missing-source';
    expect(() => parse(unknownSource)).toThrow('Unknown source');
    const unknownConnection = structuredClone(lessonContent);
    unknownConnection[0].connections[0].itemId = 'missing-item';
    expect(() => parse(unknownConnection)).toThrow();
  });

  it('rejects blank required content, impossible dates and unknown metadata', () => {
    const blankPurpose = structuredClone(lessonContent);
    blankPurpose[0].purpose = '  ';
    expect(() => parse(blankPurpose)).toThrow();
    const blankLocator = structuredClone(lessonContent);
    blankLocator[0].sourceRefs[0].locator = ' ';
    expect(() => parse(blankLocator)).toThrow();
    const invalidDate = structuredClone(lessonContent);
    invalidDate[0].sourceRefs[0].retrievedOn = '2026-02-30';
    expect(() => parse(invalidDate)).toThrow('valid calendar date');
    const extraField = lessonContent.map((lesson, index) => index === 0
      ? { ...lesson, unreviewed: true }
      : lesson);
    expect(() => parse(extraField)).toThrow();
  });
});
