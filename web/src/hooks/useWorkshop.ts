import { useCallback, useEffect, useState } from 'react';
import { readSelection, type Part, type Vehicle, type ViewMode } from '../domain/catalog';

export function useWorkshop(vehicles: Vehicle[]) {
  const initial = readSelection(window.location.search, vehicles);
  const [carId, setCarId] = useState(initial.vehicle.id);
  const [partId, setPartId] = useState(initial.part?.id);
  const [notice, setNotice] = useState(initial.invalid ? 'That vehicle or part link was not found. Showing the available catalog.' : '');
  const [mode, setMode] = useState<ViewMode>('exterior');
  const [open, setOpen] = useState(false);
  const [labels, setLabels] = useState(true);
  const [tour, setTour] = useState(false);
  const [drawer, setDrawer] = useState<'parts' | 'help' | null>(null);
  const vehicle = vehicles.find(v => v.id === carId)!;
  const part = vehicle.parts.find(p => p.id === partId);

  const navigate = useCallback((nextCar: string, nextPart?: string) => {
    const url = new URL(window.location.href);
    url.searchParams.set('car', nextCar);
    if (nextPart) url.searchParams.set('part', nextPart);
    else url.searchParams.delete('part');
    window.history.pushState(null, '', url);
    setCarId(nextCar);
    setPartId(nextPart);
  }, []);

  useEffect(() => {
    const onPop = () => {
      const selection = readSelection(window.location.search, vehicles);
      setCarId(selection.vehicle.id);
      setPartId(selection.part?.id);
      setTour(false);
      setNotice(selection.invalid ? 'That vehicle or part link was not found.' : '');
    };
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, [vehicles]);

  const selectPart = (next?: Part) => {
    navigate(carId, next?.id);
    setDrawer(null);
  };
  const selectCar = (next: string) => {
    navigate(next);
    setOpen(false);
    setMode('exterior');
    setTour(false);
    setDrawer(null);
  };
  const startTour = () => {
    setTour(true);
    setMode('xray');
    setOpen(true);
    selectPart(vehicle.parts[0]);
  };
  const stepTour = (delta: number) => {
    const index = vehicle.parts.findIndex(p => p.id === partId);
    const next = Math.max(0, Math.min(vehicle.parts.length - 1, index + delta));
    selectPart(vehicle.parts[next]);
  };
  return {
    vehicle, part, mode, setMode, open, setOpen, labels, setLabels, tour, setTour,
    drawer, setDrawer, selectPart, selectCar, startTour, stepTour, notice, setNotice,
  };
}
