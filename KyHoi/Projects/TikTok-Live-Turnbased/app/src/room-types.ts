import type {Snapshot,Team} from './types.ts';
export interface RoomSettings {game:'xiangqi'|'tictactoe';maxPeople:number;redLimit:number;blackLimit:number;allowGuests:boolean;turnSeconds:number;matchMinutes:number}
export interface RoomMember {id:string;name:string;team:Team|null;kind:'player'|'guest';active:boolean;request:Team|null}
export interface RoomMeta {code:string;title:string;hostId:string;createdAt:number;matchDeadline:number;settings:RoomSettings;members:Record<string,RoomMember>;banned:string[];closed:boolean}
export interface RoomView {
  code:string;title:string;hostId:string;isHost:boolean;settings:RoomSettings;matchDeadline:number;
  me:RoomMember;members:RoomMember[];requests:RoomMember[];bans:{id:string;name:string}[];
  game:Snapshot;myVote:string|null;canSeeVotes:boolean;
}
export type HostAction='kick'|'ban'|'unban'|'move'|'approve'|'reject'|'restart'|'close';
