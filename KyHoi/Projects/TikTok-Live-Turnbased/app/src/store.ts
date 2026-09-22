import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import type { EngineState } from './types.ts';
export class Store {
  private depth = 0;
  readonly db: DatabaseSync;
  constructor(path: string) {
    if (path !== ':memory:') mkdirSync(dirname(path), { recursive: true });
    this.db = new DatabaseSync(path);
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;
      CREATE TABLE IF NOT EXISTS state (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS events (id TEXT PRIMARY KEY, received_at INTEGER NOT NULL);
      CREATE INDEX IF NOT EXISTS events_time ON events(received_at);
      CREATE TABLE IF NOT EXISTS moves (game_id INTEGER, turn_id INTEGER, window_id INTEGER,
        move TEXT NOT NULL, tally TEXT NOT NULL, committed_at INTEGER, PRIMARY KEY(game_id,turn_id));`);
  }
  load(): EngineState | null {
    const row = this.db.prepare('SELECT json FROM state WHERE id=1').get() as {json: string} | undefined;
    return row ? JSON.parse(row.json) : null;
  }
  hasEvent(id: string) { return !!this.db.prepare('SELECT 1 FROM events WHERE id=?').get(id); }
  transaction(fn: () => void) {
    const level=this.depth++; const savepoint=`nested_${level}`;
    try {
      this.db.exec(level===0?'BEGIN IMMEDIATE':`SAVEPOINT ${savepoint}`);
      try { fn(); this.db.exec(level===0?'COMMIT':`RELEASE ${savepoint}`); }
      catch(e){this.db.exec(level===0?'ROLLBACK':`ROLLBACK TO ${savepoint}; RELEASE ${savepoint}`);throw e;}
    } finally {this.depth--;}
  }
  save(state: EngineState) { this.db.prepare('INSERT INTO state VALUES(1,?) ON CONFLICT(id) DO UPDATE SET json=excluded.json').run(JSON.stringify(state)); }
  recordEvent(id: string, at: number) { this.db.prepare('INSERT INTO events VALUES(?,?)').run(id, at); }
  recordMove(s: EngineState, at: number) {
    this.db.prepare('INSERT INTO moves VALUES(?,?,?,?,?,?)').run(s.gameId,s.turnId,s.windowId,s.lastMove,JSON.stringify(s.lastTally),at);
  }
  prune(now: number) { this.db.prepare('DELETE FROM events WHERE received_at < ?').run(now - 86400000); }
  close() { this.db.close(); }
}
