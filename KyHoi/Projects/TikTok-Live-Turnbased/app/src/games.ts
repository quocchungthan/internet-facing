import { createRequire } from 'node:module';
import type { GameAdapter, Outcome, Team } from './types.ts';
const require = createRequire(import.meta.url);
const { Xiangqi } = require('../vendor/xiangqi.cjs');
const other = (team: Team): Team => team === 'red' ? 'black' : 'red';

export class XiangqiAdapter implements GameAdapter {
  readonly id = 'xiangqi';
  private game = new Xiangqi();
  private baseFen = this.game.fen() as string;
  private moves: string[] = [];
  createInitialState() { this.game = new Xiangqi(); this.baseFen = this.game.fen(); this.moves = []; }
  getActiveTeam(): Team { return this.game.turn() === 'r' ? 'red' : 'black'; }
  getLegalMoves(): string[] { return this.game.moves(); }
  applyMove(move: string) {
    if (!this.getLegalMoves().includes(move) || !this.game.move(move)) throw new Error('Illegal move');
    this.moves.push(move);
  }
  getOutcome(): Outcome | null {
    // Xiangqi stalemate is a loss for the side with no legal move.
    if (this.game.in_checkmate()) return { winner: other(this.getActiveTeam()), reason: 'Chiếu bí' };
    if (this.game.in_stalemate()) return { winner: other(this.getActiveTeam()), reason: 'Hết nước đi hợp lệ' };
    if (this.game.in_threefold_repetition()) return { winner: null, reason: 'Lặp thế 3 lần (luật MVP)' };
    if (this.game.in_draw()) return { winner: null, reason: 'Hòa theo luật thư viện MVP' };
    if (this.game.game_over()) return { winner: other(this.getActiveTeam()), reason: 'Kết thúc ván' };
    return null;
  }
  serialize() { return JSON.stringify({ baseFen: this.baseFen, moves: this.moves }); }
  restore(data: string) {
    const parsed = JSON.parse(data);
    const game = new Xiangqi();
    if (!game.load(parsed.baseFen)) throw new Error('Invalid stored FEN');
    for (const move of parsed.moves) if (!game.move(move)) throw new Error('Invalid stored move history');
    this.game = game; this.baseFen = parsed.baseFen; this.moves = [...parsed.moves];
  }
  getView() { return { kind: this.id, fen: this.game.fen() as string }; }
}

export class TicTacToeAdapter implements GameAdapter {
  readonly id = 'tictactoe';
  private cells: (Team | null)[] = Array(9).fill(null);
  private active: Team = 'red';
  createInitialState() { this.cells = Array(9).fill(null); this.active = 'red'; }
  getActiveTeam() { return this.active; }
  getLegalMoves() { return this.getOutcome() ? [] : this.cells.flatMap((v, i) => v === null ? [String(i + 1)] : []); }
  applyMove(move: string) {
    if (!this.getLegalMoves().includes(move)) throw new Error('Illegal move');
    this.cells[Number(move) - 1] = this.active; this.active = other(this.active);
  }
  getOutcome(): Outcome | null {
    for (const [a,b,c] of [[0,1,2],[3,4,5],[6,7,8],[0,3,6],[1,4,7],[2,5,8],[0,4,8],[2,4,6]]) {
      if (this.cells[a] && this.cells[a] === this.cells[b] && this.cells[a] === this.cells[c]) return { winner: this.cells[a], reason: 'Ba quân thẳng hàng' };
    }
    return this.cells.every(Boolean) ? { winner: null, reason: 'Hết ô trống' } : null;
  }
  serialize() { return JSON.stringify({ cells: this.cells, active: this.active }); }
  restore(data: string) {
    const parsed = JSON.parse(data);
    if (!Array.isArray(parsed.cells) || parsed.cells.length !== 9 || !['red','black'].includes(parsed.active) || parsed.cells.some((v: unknown) => v !== null && v !== 'red' && v !== 'black')) throw new Error('Invalid grid state');
    this.cells = parsed.cells; this.active = parsed.active;
  }
  getView() { return { kind: this.id, cells: [...this.cells] }; }
}
export function createGame(id: string): GameAdapter {
  if (id === 'xiangqi') return new XiangqiAdapter();
  if (id === 'tictactoe') return new TicTacToeAdapter();
  throw new Error('GAME must be xiangqi or tictactoe');
}
