import { useCallback, useEffect, useMemo, useState } from 'react';
import { readSelection, type Part, type Vehicle, type ViewMode } from '../../../domain/catalog';
import { createVehicleItems } from '../domain/items';
import { activateSubject, readSubject, subjectSearch } from '../domain/subject';

export function useWorkshop(vehicles: Vehicle[]) {
  const initial = readSelection(window.location.search, vehicles);
  const initialSubject = readSubject(window.location.search, initial.vehicle);
  const [carId, setCarId] = useState(initial.vehicle.id);
  const [itemId, setItemId] = useState(initialSubject.item.id);
  const [isolated, setIsolated] = useState(initialSubject.isolated);
  const [notice, setNotice] = useState(initial.invalid || initialSubject.invalid
    ? 'That vehicle or part link was not found. Showing the available catalog.'
    : initialSubject.activation.kind === 'information-only' ? initialSubject.activation.reason : '');
  const [mode, setMode] = useState<ViewMode>('exterior');
  const [open, setOpen] = useState(false);
  const [labels, setLabels] = useState(true);
  const [tour, setTour] = useState(false);
  const [drawer, setDrawer] = useState<'parts' | 'help' | null>(null);
  const vehicle = vehicles.find(v => v.id === carId)!;
  const tree = useMemo(() => createVehicleItems(vehicle), [vehicle]);
  const item = tree.items.find(candidate => candidate.id === itemId)!;
  const activation = activateSubject(tree, itemId);
  const part = vehicle.parts.find(p => p.id === item.partId);

  const navigate = useCallback((nextVehicle: Vehicle, nextItem = 'vehicle', isolate = false) => {
    const nextTree = nextVehicle.id === vehicle.id ? tree : createVehicleItems(nextVehicle);
    const result = activateSubject(nextTree, nextItem);
    if (result.kind === 'unavailable') {
      setNotice(result.reason);
      return result;
    }
    const nextIsolation = result.kind === 'focus' && isolate && nextItem !== 'vehicle';
    const url = new URL(window.location.href);
    url.search = subjectSearch(url.search, nextVehicle, nextItem, nextIsolation);
    window.history.pushState(null, '', url);
    setCarId(nextVehicle.id);
    setItemId(nextItem);
    setIsolated(nextIsolation);
    setNotice(result.kind === 'information-only' ? result.reason : '');
    return result;
  }, [tree, vehicle]);

  useEffect(() => {
    const onPop = () => {
      const selection = readSelection(window.location.search, vehicles);
      const subject = readSubject(window.location.search, selection.vehicle);
      setCarId(selection.vehicle.id);
      setItemId(subject.item.id);
      setIsolated(subject.isolated);
      setTour(false);
      setNotice(selection.invalid || subject.invalid
        ? 'That vehicle or part link was not found.'
        : subject.activation.kind === 'information-only' ? subject.activation.reason : '');
    };
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, [vehicles]);

  const selectPart = (next?: Part, keepIsolation = isolated) => {
    const result = navigate(vehicle, next?.id, keepIsolation);
    if (result.kind !== 'unavailable') setDrawer(null);
    return result;
  };
  const selectItem = (id: string, keepIsolation = isolated) => {
    const result = activateSubject(tree, id);
    if (result.kind === 'unavailable') {
      setNotice(result.reason);
      return result;
    }
    const navigated = navigate(vehicle, id, keepIsolation);
    if (navigated.kind === 'unavailable') return navigated;
    setTour(false);
    setDrawer(null);
    return navigated;
  };
  const isolate = () => {
    if (activation.kind !== 'focus') {
      setNotice(activation.reason);
      return activation;
    }
    return navigate(vehicle, itemId, true);
  };
  const restore = () => { navigate(vehicle); setTour(false); };
  const selectCar = (next: string) => {
    const nextVehicle = vehicles.find(candidate => candidate.id === next);
    if (!nextVehicle) throw new Error(`Unknown vehicle ${next}.`);
    navigate(nextVehicle);
    setOpen(false);
    setMode('exterior');
    setTour(false);
    setDrawer(null);
  };
  const startTour = () => {
    setTour(true);
    setMode('xray');
    setOpen(true);
    selectPart(vehicle.parts[0], false);
  };
  const stepTour = (delta: number) => {
    const index = vehicle.parts.findIndex(p => p.id === part?.id);
    const next = Math.max(0, Math.min(vehicle.parts.length - 1, index + delta));
    selectPart(vehicle.parts[next]);
  };
  return {
    vehicle, part, tree, item, activation, isolated, selectItem, isolate, restore,
    mode, setMode, open, setOpen, labels, setLabels, tour, setTour,
    drawer, setDrawer, selectPart, selectCar, startTour, stepTour, notice, setNotice,
  };
}
