import type { z } from 'zod';

/**
 * Transport-neutral operation boundary (contract version 1).
 *
 * An operation declares input/output schemas, the domain failures it may report and a typed
 * `execute` function. `runOperation` validates input before execution and output before
 * returning, and converts every outcome into an explicit `OperationResult`. Adapters (React
 * hooks today; HTTP, MCP or agent tools later) call `runOperation`; business rules never see
 * transport details. Cancellation is execution context, never part of the serialized input.
 */
export type OperationKind = 'query' | 'mutation';

export const domainFailureCodes = ['invalid_argument', 'not_found', 'conflict', 'unavailable'] as const;
export type DomainFailureCode = typeof domainFailureCodes[number];
export type OperationFailureCode = DomainFailureCode | 'cancelled' | 'contract_violation' | 'internal';

export type OperationFailure = {
  code: OperationFailureCode;
  operation: string;
  message: string;
  issues?: { path: (string | number)[]; message: string }[];
  cause?: unknown;
};

export type OperationResult<O> = { ok: true; value: O } | { ok: false; error: OperationFailure };

export type OperationContext = { signal: AbortSignal };

/** Thrown by `execute` to report a declared domain failure. */
export class OperationError extends Error {
  constructor(readonly code: DomainFailureCode, message: string) {
    super(message);
    this.name = 'OperationError';
  }
}

export type Operation<I extends z.ZodTypeAny, O extends z.ZodTypeAny> = {
  name: string;
  version: 1;
  kind: OperationKind;
  input: I;
  output: O;
  failures: readonly DomainFailureCode[];
  execute: (input: z.output<I>, context: OperationContext) => z.input<O> | Promise<z.input<O>>;
};

export function defineOperation<I extends z.ZodTypeAny, O extends z.ZodTypeAny>(
  operation: Omit<Operation<I, O>, 'version'>,
): Operation<I, O> {
  if (!/^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$/.test(operation.name)) {
    throw new Error(`Operation name "${operation.name}" must be a dotted lowerCamel path such as "catalog.getVehicle".`);
  }
  return Object.freeze({ ...operation, version: 1 as const, failures: Object.freeze([...operation.failures]) });
}

function issuesOf(error: z.ZodError) {
  return error.issues.map(issue => ({ path: issue.path, message: issue.message }));
}

function fail(operation: string, code: OperationFailureCode, message: string,
  extra: Partial<OperationFailure> = {}): OperationResult<never> {
  return { ok: false, error: { code, operation, message, ...extra } };
}

function cancelled(name: string) {
  return fail(name, 'cancelled', `${name} was cancelled; any effects already applied are not rolled back.`);
}

function failureFromThrown<I extends z.ZodTypeAny, O extends z.ZodTypeAny>(
  operation: Operation<I, O>, thrown: unknown, signal: AbortSignal,
): OperationResult<never> {
  if (signal.aborted) return cancelled(operation.name);
  if (thrown instanceof OperationError) {
    if (operation.failures.includes(thrown.code)) return fail(operation.name, thrown.code, thrown.message);
    return fail(operation.name, 'contract_violation',
      `${operation.name} reported undeclared failure "${thrown.code}": ${thrown.message}`, { cause: thrown });
  }
  const detail = thrown instanceof Error ? thrown.message : String(thrown);
  return fail(operation.name, 'internal', `${operation.name} failed unexpectedly: ${detail}`, { cause: thrown });
}

export async function runOperation<I extends z.ZodTypeAny, O extends z.ZodTypeAny>(
  operation: Operation<I, O>, rawInput: unknown, context: Partial<OperationContext> = {},
): Promise<OperationResult<z.output<O>>> {
  const signal = context.signal ?? new AbortController().signal;
  if (signal.aborted) return cancelled(operation.name);
  const input = operation.input.safeParse(rawInput);
  if (!input.success) {
    return fail(operation.name, 'invalid_argument', `Invalid input for ${operation.name}.`, { issues: issuesOf(input.error) });
  }
  let value: unknown;
  try {
    value = await operation.execute(input.data, { signal });
  } catch (thrown) {
    return failureFromThrown(operation, thrown, signal);
  }
  if (signal.aborted) return cancelled(operation.name);
  const output = operation.output.safeParse(value);
  if (!output.success) {
    return fail(operation.name, 'contract_violation', `${operation.name} returned output that violates its contract.`,
      { issues: issuesOf(output.error) });
  }
  return { ok: true, value: output.data };
}
