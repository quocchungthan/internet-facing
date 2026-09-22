import type { Team } from './types.ts';
export type Command = { kind: 'team'; team: Team } | { kind: 'vote'; windowId: number; move: string };
export interface InputInterpreter { parse(text: string): Command | null }
export class CommandParser implements InputInterpreter {
  parse(text: string): Command | null {
    const normalized = text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[đĐ]/g, 'd').trim().toLowerCase();
    const team = /^!team\s+(do|den)$/.exec(normalized);
    if (team) return { kind: 'team', team: team[1] === 'do' ? 'red' : 'black' };
    const vote = /^!vote\s+w([1-9]\d*)\s+([a-z0-9]{1,20})$/.exec(normalized);
    if (vote && Number.isSafeInteger(Number(vote[1]))) return { kind: 'vote', windowId: Number(vote[1]), move: vote[2] };
    return null;
  }
}
