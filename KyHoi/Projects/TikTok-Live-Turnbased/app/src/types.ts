export type Team = 'red' | 'black';
export type Outcome = { winner: Team | null; reason: string };
export type Phase = 'voting' | 'paused' | 'revealing' | 'intermission' | 'finished';
export interface GameAdapter {
  readonly id: string;
  createInitialState(): void;
  getActiveTeam(): Team;
  getLegalMoves(): string[];
  applyMove(move: string): void;
  getOutcome(): Outcome | null;
  serialize(): string;
  restore(data: string): void;
  getView(): { kind: string; fen?: string; cells?: (Team | null)[] };
}
export interface CommentEvent { eventId: string; userId: string; text: string; receivedAt: number }
export interface Member { currentTeam: Team; pendingTeam?: Team; effectiveRound?: number }
export interface EngineConfig { turnMs: number; revealMs: number; intermissionMs: number; randomOnEmpty?:boolean; autoRestart?:boolean }
export interface EngineState {
  schema: 1; gameKind: string; gameData: string;
  gameId: number; roundId: number; turnId: number; windowId: number;
  phase: Phase; deadline: number; openedAt: number; connected: boolean;
  teams: Record<string, Member>; votes: Record<string, string>;
  outcome: Outcome | null; lastMove: string | null; lastTally: Record<string, number>;
  lastTeam: Team | null; notices: string[];
  metrics: { comments: number; acceptedVotes: number; rejected: number; moves: number; windows: number; emptyWindows: number; games: number };
}
export interface Snapshot {
  gameKind: string; gameId: number; roundId: number; turnId: number; windowId: number;
  phase: Phase; deadline: number; serverNow: number; connected: boolean; activeTeam: Team;
  board: ReturnType<GameAdapter['getView']>; legalMoves: string[];
  tally: Record<string, number>; voterCount: number; memberCounts: Record<Team, number>;
  pendingSwitches: number; lastMove: string | null; lastTally: Record<string, number>;
  lastTeam: Team | null; outcome: Outcome | null; notices: string[];
  metrics: EngineState['metrics'];
}
