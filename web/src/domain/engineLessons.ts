import { z } from 'zod';

export const engineLessonItemIds = [
  'engine-cylinder-study',
  'engine-piston-study',
  'engine-rod-study',
  'engine-crankshaft-study',
  'engine-intake-valve-study',
  'engine-exhaust-valve-study',
  'engine-spark-plug-study',
] as const;

const sourceRefSchema = z.object({
  sourceId: z.string().min(1),
  locator: z.string().trim().min(1),
  retrievedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).refine(value => {
    const date = new Date(`${value}T00:00:00.000Z`);
    return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
  }, 'Expected a valid calendar date.'),
}).strict();

const engineLessonSchema = z.object({
  carId: z.literal('raize'),
  itemId: z.enum(engineLessonItemIds),
  label: z.string().trim().min(1),
  aliases: z.array(z.string().trim().min(1)),
  purpose: z.string().trim().min(1),
  connections: z.array(z.object({
    itemId: z.enum(engineLessonItemIds),
    relationship: z.string().trim().min(1),
  }).strict()).min(1),
  sourceRefs: z.array(sourceRefSchema).min(1),
  scope: z.literal('generic-four-stroke'),
  limitations: z.string().trim().min(1),
  authorship: z.literal('original'),
}).strict();

const engineLessonsSchema = z.array(engineLessonSchema);
export type EngineLesson = z.infer<typeof engineLessonSchema>;

export function parseEngineLessons(input: unknown, sources: readonly { id: string }[]): EngineLesson[] {
  const lessons = engineLessonsSchema.parse(input);
  const lessonIds = new Set<string>();
  const sourceIds = new Set(sources.map(source => source.id));

  for (const lesson of lessons) {
    if (lessonIds.has(lesson.itemId)) throw new Error(`Duplicate engine lesson for ${lesson.carId}/${lesson.itemId}.`);
    lessonIds.add(lesson.itemId);
    for (const reference of lesson.sourceRefs) {
      if (!sourceIds.has(reference.sourceId)) throw new Error(`Unknown source ${reference.sourceId} in ${lesson.itemId}.`);
    }
    for (const connection of lesson.connections) {
      if (!engineLessonItemIds.includes(connection.itemId)) {
        throw new Error(`Unknown lesson connection ${connection.itemId} in ${lesson.itemId}.`);
      }
    }
  }

  if (lessonIds.size !== engineLessonItemIds.length
    || engineLessonItemIds.some(itemId => !lessonIds.has(itemId))) {
    throw new Error('Engine lesson records must match the seven-subject roster.');
  }
  return lessons;
}

export type LessonLookup =
  | { status: 'ready'; lesson: EngineLesson }
  | { status: 'missing'; reason: string };

export function lessonFor(lessons: readonly EngineLesson[], carId: string, itemId: string): LessonLookup {
  const lesson = lessons.find(candidate => candidate.carId === carId && candidate.itemId === itemId);
  return lesson
    ? { status: 'ready', lesson }
    : { status: 'missing', reason: 'No reviewed explanation for this subject.' };
}
