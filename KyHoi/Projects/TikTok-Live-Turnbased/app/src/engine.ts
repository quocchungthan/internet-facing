import { randomInt } from 'node:crypto';
import type { CommentEvent, EngineConfig, EngineState, GameAdapter, Snapshot, Team } from './types.ts';
import { CommandParser, type InputInterpreter } from './parser.ts';
import { Store } from './store.ts';
const own = (obj: object, key: string) => Object.prototype.hasOwnProperty.call(obj, key);
export class Engine {
  private state: EngineState;
  private lastPruned = 0;
  constructor(private game: GameAdapter, private store: Store, private config: EngineConfig,
    private now: () => number = Date.now, private choose: (n: number) => number = randomInt,
    private parser: InputInterpreter = new CommandParser(), connected = true) {
    const saved = store.load();
    if (saved && (saved.schema !== 1 || saved.gameKind !== game.id)) throw new Error('Stored game differs from GAME. Use a separate DATABASE_PATH.');
    if (saved) {
      this.state = saved; game.restore(saved.gameData); saved.connected = connected;
      if (saved.phase === 'voting' || saved.phase === 'paused') this.openWindow();
      else if(saved.phase !== 'finished') saved.deadline = now() + (saved.phase === 'revealing' ? config.revealMs : config.intermissionMs);
      this.notice('Đã khôi phục trạng thái');
    } else {
      game.createInitialState();
      this.state = { schema: 1, gameKind: game.id, gameData: game.serialize(), gameId: 1, roundId: 1,
        turnId: 1, windowId: 0, phase: 'paused', deadline: 0, openedAt: 0, connected,
        teams: {}, votes: {}, outcome: null, lastMove: null, lastTally: {}, lastTeam: null, notices: [],
        metrics: { comments: 0, acceptedVotes: 0, rejected: 0, moves: 0, windows: 0, emptyWindows: 0, games: 0 } };
      this.openWindow();
    }
    store.transaction(() => store.save(this.state));
  }
  private notice(message: string) { this.state.notices = [message, ...this.state.notices].slice(0, 6); }
  private atomic(fn: () => void) {
    const before = structuredClone(this.state);
    try { this.store.transaction(() => { fn(); this.state.gameData = this.game.serialize(); this.store.save(this.state); }); }
    catch (error) { this.state = before; this.game.restore(before.gameData); throw error; }
  }
  private openWindow() {
    const s = this.state;
    s.votes = {}; s.windowId++; s.openedAt = this.now();
    s.deadline = this.now() + this.config.turnMs;
    s.phase = s.connected ? 'voting' : 'paused';
    if (s.connected) s.metrics.windows++;
  }
  private applySwitches(all = false) {
    for (const member of Object.values(this.state.teams)) {
      if (member.pendingTeam && (all || member.effectiveRound! <= this.state.roundId)) {
        member.currentTeam = member.pendingTeam;
        delete member.pendingTeam; delete member.effectiveRound;
      }
    }
  }
  private tally() {
    const result: Record<string,number> = {};
    for (const move of Object.values(this.state.votes)) result[move] = (result[move] || 0) + 1;
    return result;
  }
  receive(event: CommentEvent): { accepted: boolean; reason: string } {
    // Deadline is authoritative even when a timer callback was delayed by other work.
    this.tick();
    if (this.store.hasEvent(event.eventId)) return { accepted: false, reason: 'duplicate' };
    let result = { accepted: false, reason: 'invalid-command' };
    this.atomic(() => {
      this.store.recordEvent(event.eventId, event.receivedAt);
      const s = this.state; s.metrics.comments++;
      const cmd = this.parser.parse(event.text);
      if (cmd?.kind === 'team') {
        if (!own(s.teams,event.userId)) {
          Object.defineProperty(s.teams,event.userId,{ value: {currentTeam: cmd.team}, enumerable:true, writable:true, configurable:true });
          result = { accepted:true, reason:'joined' };
        } else {
          const member = s.teams[event.userId];
          if (member.currentTeam === cmd.team) { delete member.pendingTeam; delete member.effectiveRound; }
          else { member.pendingTeam = cmd.team; member.effectiveRound = s.roundId + 1; }
          result = { accepted:true, reason:'team-request-updated' };
        }
        this.notice(cmd.team === 'red' ? 'Đã nhận yêu cầu đội Đỏ' : 'Đã nhận yêu cầu đội Đen');
      } else if (cmd?.kind === 'vote') {
        const member = own(s.teams,event.userId) ? s.teams[event.userId] : undefined;
        if (!s.connected || s.phase !== 'voting') result.reason = 'window-closed';
        else if (cmd.windowId !== s.windowId || event.receivedAt < s.openedAt || event.receivedAt >= s.deadline) result.reason = 'stale-window';
        else if (!member || member.currentTeam !== this.game.getActiveTeam()) result.reason = 'wrong-team';
        else if (!this.game.getLegalMoves().includes(cmd.move)) result.reason = 'illegal-move';
        else {
          Object.defineProperty(s.votes,event.userId,{value:cmd.move,enumerable:true,writable:true,configurable:true});
          s.metrics.acceptedVotes++; result = {accepted:true,reason:'vote-recorded'};
        }
      }
      if (!result.accepted) s.metrics.rejected++;
    });
    return result;
  }
  tick(): boolean {
    const s = this.state;
    if (this.now() - this.lastPruned > 3600000) { this.store.prune(this.now()); this.lastPruned = this.now(); }
    if (!s.connected || s.phase === 'paused' || s.phase === 'finished' || this.now() < s.deadline) return false;
    this.atomic(() => {
      if (s.phase === 'voting') {
        const tally = this.tally();
        if (!Object.keys(tally).length && !this.config.randomOnEmpty) {
          s.metrics.emptyWindows++; this.notice('Chưa có phiếu hợp lệ — mở lại bình chọn'); this.openWindow(); return;
        }
        const random = !Object.keys(tally).length;
        const max = random ? 0 : Math.max(...Object.values(tally));
        const tied = random ? this.game.getLegalMoves() : Object.keys(tally).filter(m => tally[m] === max).sort();
        if(random)s.metrics.emptyWindows++;
        if(!tied.length){s.outcome=this.game.getOutcome()??{winner:null,reason:'Không còn nước hợp lệ'};s.phase='finished';s.votes={};s.metrics.games++;return;}
        const move = tied[this.choose(tied.length)];
        s.lastTeam = this.game.getActiveTeam(); this.game.applyMove(move);
        s.lastMove = move; s.lastTally = tally; s.votes = {}; s.metrics.moves++;
        this.store.recordMove(s, this.now());
        s.outcome = this.game.getOutcome();
        s.phase = s.outcome ? (this.config.autoRestart===false?'finished':'intermission') : 'revealing';
        s.deadline = this.now() + (s.outcome ? this.config.intermissionMs : this.config.revealMs);
        if (s.outcome) s.metrics.games++;
        this.notice(random?`${move} · tự đi ngẫu nhiên vì hết giờ không có phiếu`:`${move} · ${max} phiếu${tied.length > 1 ? ' · bốc thăm đồng hạng' : ''}`);
      } else if (s.phase === 'revealing') {
        s.turnId++; if (s.turnId % 2 === 1) { s.roundId++; this.applySwitches(); }
        this.openWindow();
      } else if (s.phase === 'intermission') {
        this.game.createInitialState(); this.applySwitches(true);
        s.gameId++; s.turnId = 1; s.roundId = 1; s.outcome = null; s.lastMove = null; s.lastTeam = null; s.lastTally = {};
        this.notice('Ván mới — Đỏ đi trước'); this.openWindow();
      }
    });
    return true;
  }
  setConnected(value: boolean) {
    if (this.state.connected === value) return;
    this.atomic(() => {
      const s = this.state; s.connected = value;
      if (s.phase === 'voting' || s.phase === 'paused') {
        if (value) this.openWindow(); else { s.phase = 'paused'; s.votes = {}; }
      }
      this.notice(value ? 'Đã kết nối nguồn comment' : 'Mất kết nối comment — giữ bàn cờ');
    });
  }
  transaction(fn:()=>void){this.atomic(fn);}
  assignTeam(userId:string,team:Team|null){this.atomic(()=>{
    delete this.state.votes[userId];
    if(team)Object.defineProperty(this.state.teams,userId,{value:{currentTeam:team},enumerable:true,writable:true,configurable:true});
    else delete this.state.teams[userId];
  });}
  finish(reason:string){if(this.state.phase==='finished')return;this.atomic(()=>{this.state.phase='finished';this.state.votes={};this.state.outcome={winner:null,reason};this.state.deadline=this.now();this.state.metrics.games++;});}
  restart(){this.atomic(()=>{this.game.createInitialState();this.state.gameId++;this.state.turnId=1;this.state.roundId=1;this.state.outcome=null;this.state.lastMove=null;this.state.lastTally={};this.state.lastTeam=null;this.state.notices=[];this.openWindow();});}
  playerState(userId: string) {
    const member = own(this.state.teams,userId) ? this.state.teams[userId] : null;
    return { member: member ? {...member} : null,
      vote: own(this.state.votes,userId) ? this.state.votes[userId] : null,
      windowId:this.state.windowId, phase:this.state.phase };
  }
  snapshot(): Snapshot {
    const s = this.state; const memberCounts = {red:0,black:0}; let pendingSwitches = 0;
    for (const member of Object.values(s.teams)) { memberCounts[member.currentTeam]++; if (member.pendingTeam) pendingSwitches++; }
    return { gameKind:s.gameKind, gameId:s.gameId, roundId:s.roundId, turnId:s.turnId, windowId:s.windowId,
      phase:s.phase, deadline:s.deadline, serverNow:this.now(), connected:s.connected, activeTeam:this.game.getActiveTeam(),
      board:this.game.getView(), legalMoves:s.phase === 'voting' ? this.game.getLegalMoves() : [],
      tally:this.tally(), voterCount:Object.keys(s.votes).length, memberCounts, pendingSwitches,
      lastMove:s.lastMove, lastTeam:s.lastTeam, lastTally:{...s.lastTally}, outcome:s.outcome ? {...s.outcome} : null,
      notices:[...s.notices], metrics:{...s.metrics} };
  }
}
