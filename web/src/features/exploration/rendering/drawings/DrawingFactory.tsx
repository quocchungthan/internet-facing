import type { ComponentType } from 'react';
import type { DrawingKind } from '../../domain/items';
import { BodyDrawing } from './BodyDrawing';
import { EngineBlock, EngineCover, EngineCrankshaft, EngineCylinderWall, EngineHead,
  EnginePiston, EngineRod, EngineSparkPlug, EngineValve } from './EngineDrawing';
import { BrakeDrawing, CoolingDrawing, ElectricalDrawing, ExhaustDrawing, IntakeDrawing,
  SuspensionDrawing, TransmissionDrawing, WheelDrawing, type DrawingProps } from './MechanicalDrawings';
import { Box } from './primitives';
import { WiperArm, WiperBlade, WiperRubber } from './WiperDrawing';

type Props = DrawingProps & { axle: number; exhaust?: boolean };
const drawings: Record<DrawingKind, ComponentType<Props>> = {
  assembly: () => null,
  body: BodyDrawing,
  'engine-block': EngineBlock,
  'engine-head': EngineHead,
  'engine-cover': EngineCover,
  'engine-cylinder-wall': EngineCylinderWall,
  'engine-piston': EnginePiston,
  'engine-rod': EngineRod,
  'engine-crankshaft': EngineCrankshaft,
  'engine-valve': EngineValve,
  'engine-spark-plug': EngineSparkPlug,
  lubrication: () => <Box size={[0.55, 0.16, 0.42]} color="#b19b65" />,
  intake: IntakeDrawing,
  cooling: CoolingDrawing,
  transmission: TransmissionDrawing,
  wheel: WheelDrawing,
  brake: BrakeDrawing,
  suspension: SuspensionDrawing,
  electrical: ElectricalDrawing,
  fuel: ({ width }) => <Box size={[0.65, 0.23, width * 0.54]} color="#6a7970" />,
  exhaust: ExhaustDrawing,
  'wiper-arm': WiperArm,
  'wiper-blade': WiperBlade,
  'wiper-rubber': WiperRubber,
};

export function DrawingFactory({ kind, ...props }: Props & { kind: DrawingKind }) {
  const Drawing = drawings[kind];
  if (!Drawing) throw new Error(`No drawing registered for ${kind}.`);
  return <Drawing {...props} />;
}
