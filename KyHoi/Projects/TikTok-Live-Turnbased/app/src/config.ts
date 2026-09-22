import dotenv from 'dotenv';
import { resolve } from 'node:path';
dotenv.config({ path:resolve('../.env'), quiet:true });
dotenv.config({ path:resolve('.env'), quiet:true });
function positive(name: string, fallback: number) {
  const value = Number(process.env[name] ?? fallback);
  if (!Number.isFinite(value) || value <= 0) throw new Error(`${name} must be positive`);
  return value;
}
export function loadConfig() {
  const source = process.env.COMMENT_SOURCE ?? 'mock';
  const host = process.env.HOST ?? '127.0.0.1';
  if (!['mock','tiktok'].includes(source)) throw new Error('COMMENT_SOURCE must be mock or tiktok');
  if (source === 'mock' && !['127.0.0.1','::1','localhost'].includes(host)) throw new Error('Mock input is local-only; bind HOST to loopback');
  const username = process.env.TIKTOK_USERNAME ?? ''; const key = process.env.EULER_API_KEY ?? '';
  if (source === 'tiktok' && (!username || !key)) throw new Error('TIKTOK_USERNAME and EULER_API_KEY are required');
  return { source,host,username,key, port:positive('PORT',3000),game:process.env.GAME ?? 'xiangqi',
    database:process.env.DATABASE_PATH ?? './data/game.sqlite',
    engine:{turnMs:positive('TURN_SECONDS',30)*1000,revealMs:positive('MOVE_REVEAL_SECONDS',3)*1000,intermissionMs:positive('INTERMISSION_SECONDS',15)*1000} };
}
