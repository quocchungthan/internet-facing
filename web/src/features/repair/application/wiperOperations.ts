import { z } from 'zod';
import { defineOperation, OperationError } from '../../../application/contracts/operation';
import { advanceWiperGuide, wiperSteps, type WiperStepId } from '../domain/wiperGuide';

const stepIds = Object.keys(wiperSteps) as [WiperStepId, ...WiperStepId[]];
const wiperStep = z.enum(stepIds);

/**
 * Validated contract for the existing reversible, in-memory wiper-guide transition.
 * It computes the next step only; it records no history and persists nothing.
 */
export const advanceWiper = defineOperation({
  name: 'repair.wiper.advance', kind: 'mutation', failures: ['invalid_argument'],
  input: z.object({ step: wiperStep, choice: z.number().int().nonnegative() }).strict(),
  output: z.object({ step: wiperStep }).strict(),
  execute: ({ step, choice }) => {
    if (!wiperSteps[step].choices[choice]) {
      throw new OperationError('invalid_argument', `Choice ${choice} is not available at wiper step ${step}.`);
    }
    return { step: advanceWiperGuide(step, choice) };
  },
});
