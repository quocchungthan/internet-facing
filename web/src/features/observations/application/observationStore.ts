import { useSyncExternalStore } from 'react';
import { emptyRecord, type ObservationRecord } from '../domain/observationRecord';

/** Tab-scoped, in-memory only: nothing is persisted or uploaded, and a reload discards the log. */
let current: ObservationRecord = emptyRecord;
const listeners = new Set<() => void>();

export const observationStore = {
  get: () => current,
  update(change: (record: ObservationRecord) => ObservationRecord) {
    current = change(current);
    listeners.forEach(listener => listener());
  },
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  },
};

export function useObservationRecord() {
  return useSyncExternalStore(observationStore.subscribe, observationStore.get);
}
