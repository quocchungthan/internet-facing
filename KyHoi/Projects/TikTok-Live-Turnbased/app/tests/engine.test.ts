import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Engine} from '../src/engine.ts';
import {Store} from '../src/store.ts';
import {TicTacToeAdapter,XiangqiAdapter} from '../src/games.ts';
import {CommandParser} from '../src/parser.ts';
import {normalizeChat} from '../src/sources.ts';

function harness(kind='tictactoe') {
  let now=1000;let seq=0;const store=new Store(':memory:');
  const config={turnMs:30000,revealMs:3000,intermissionMs:15000};
  const make=()=>kind==='xiangqi'?new XiangqiAdapter():new TicTacToeAdapter();
  let engine=new Engine(make(),store,config,()=>now,()=>0);
  const send=(userId:string,text:string,eventId=String(++seq))=>engine.receive({eventId,userId,text,receivedAt:now});
  const advance=(ms:number)=>{now+=ms;engine.tick();};
  const vote=(user:string,move:string)=>send(user,`!vote W${engine.snapshot().windowId} ${move}`);
  const play=(user:string,move:string)=>{assert.equal(vote(user,move).accepted,true);advance(30000);advance(3000);};
  return {get engine(){return engine;},store,send,advance,vote,play, restart(){engine=new Engine(make(),store,config,()=>now,()=>0);}};
}
test('parser: Vietnamese accents and strict command boundaries',()=>{
  const p=new CommandParser();assert.deepEqual(p.parse(' !TEAM ĐỎ '),{kind:'team',team:'red'});
  assert.deepEqual(p.parse('!vote w12 A3A4'),{kind:'vote',windowId:12,move:'a3a4'});
  for(const s of ['!vote W0 a3a4','!vote W1 a3a4 spam','xin đi pháo','!team xanh'])assert.equal(p.parse(s),null);
});
test('teams, one voter one latest legal vote, wrong side, duplicate',()=>{
  const h=harness();h.send('r','!team do');h.send('b','!team den');
  assert.equal(h.vote('b','1').reason,'wrong-team');assert.equal(h.vote('unknown','1').reason,'wrong-team');
  h.vote('r','1');h.vote('r','2');assert.equal(h.vote('r','10').reason,'illegal-move');
  assert.deepEqual(h.engine.snapshot().tally,{'2':1});
  const text=`!vote W${h.engine.snapshot().windowId} 3`;
  h.send('r',text,'same');assert.equal(h.send('r',text,'same').reason,'duplicate');
  assert.equal(h.engine.snapshot().voterCount,1);h.advance(30000);
  assert.equal(h.engine.snapshot().board.cells?.[2],'red');assert.equal(h.engine.snapshot().phase,'revealing');
  h.advance(3000);assert.equal(h.engine.snapshot().activeTeam,'black');h.store.close();
});
test('switch activates at next full round; last request wins and can cancel',()=>{
  const h=harness();h.send('a','!team do');h.send('b','!team den');
  h.send('a','!team den');assert.equal(h.engine.snapshot().pendingSwitches,1);
  h.play('a','1');assert.equal(h.vote('a','2').reason,'wrong-team');h.play('b','2');
  assert.equal(h.engine.snapshot().roundId,2);assert.deepEqual(h.engine.snapshot().memberCounts,{red:0,black:2});
  h.send('a','!team do');h.send('a','!team den');assert.equal(h.engine.snapshot().pendingSwitches,0);h.store.close();
});
test('no votes repeats window without advancing board, turn or round; expired votes rejected',()=>{
  const h=harness();h.send('r','!team do');const before=h.engine.snapshot();
  for(let i=0;i<20;i++)h.advance(30000);
  const after=h.engine.snapshot();assert.equal(after.turnId,before.turnId);assert.equal(after.roundId,1);
  assert.deepEqual(after.board,before.board);assert.equal(after.windowId,before.windowId+20);
  assert.equal(h.send('r',`!vote W${before.windowId} 1`).reason,'stale-window');h.store.close();
});
test('exact deadline closes even if interval timer has not run; stale windows cannot leak',()=>{
  const h=harness();h.send('r','!team do');const old=h.engine.snapshot();
  h.advance(30000);assert.equal(h.send('r',`!vote W${old.windowId} 1`).accepted,false);
  const current=h.engine.snapshot();
  assert.equal(h.engine.receive({eventId:'late',userId:'r',text:`!vote W${current.windowId} 1`,receivedAt:current.deadline}).accepted,false);
  h.store.close();
});
test('tie selection and tally are persisted exactly once',()=>{
  const h=harness();h.send('a','!team do');h.send('b','!team do');h.vote('a','2');h.vote('b','1');h.advance(30000);
  assert.equal(h.engine.snapshot().lastMove,'1');
  const row=h.store.db.prepare('SELECT tally FROM moves').get() as {tally:string};assert.deepEqual(JSON.parse(row.tally),{'1':1,'2':1});
  h.restart();h.advance(3000);assert.equal(h.engine.snapshot().turnId,2);
  assert.equal((h.store.db.prepare('SELECT COUNT(*) AS n FROM moves').get() as {n:number}).n,1);h.store.close();
});
test('disconnect closes vote; reconnect and restart give fresh windows and keep members',()=>{
  const h=harness();h.send('r','!team do');h.vote('r','1');const w=h.engine.snapshot().windowId;
  h.engine.setConnected(false);h.advance(60000);assert.equal(h.engine.snapshot().phase,'paused');
  h.engine.setConnected(true);assert.equal(h.engine.snapshot().voterCount,0);assert.ok(h.engine.snapshot().windowId>w);
  h.vote('r','2');const w2=h.engine.snapshot().windowId;h.restart();assert.ok(h.engine.snapshot().windowId>w2);
  assert.equal(h.engine.snapshot().voterCount,0);assert.equal(h.engine.snapshot().memberCounts.red,1);h.store.close();
});
test('full game ends, new game starts and pending team changes carry over',()=>{
  const h=harness();h.send('a','!team do');h.send('b','!team den');
  h.play('a','1');h.play('b','4');h.play('a','2');h.play('b','5');
  h.send('a','!team den');h.vote('a','3');h.advance(30000);
  assert.deepEqual(h.engine.snapshot().outcome,{winner:'red',reason:'Ba quân thẳng hàng'});
  h.restart();h.advance(15000);assert.equal(h.engine.snapshot().gameId,2);assert.equal(h.engine.snapshot().activeTeam,'red');
  assert.equal(h.engine.snapshot().memberCounts.black,2);assert.equal(h.engine.snapshot().roundId,1);h.store.close();
});
test('SQLite failure rolls back both in-memory game and persistent state',()=>{
  const h=harness();h.send('a','!team do');h.vote('a','1');
  h.store.db.exec("CREATE TRIGGER fail_move BEFORE INSERT ON moves BEGIN SELECT RAISE(ABORT, 'simulated disk error'); END;");
  assert.throws(()=>h.advance(30000),/simulated disk error/);
  assert.equal(h.engine.snapshot().board.cells?.[0],null);assert.equal(h.engine.snapshot().phase,'voting');
  assert.equal((h.store.db.prepare('SELECT COUNT(*) AS n FROM moves').get() as {n:number}).n,0);h.store.close();
});
test('prototype-like user IDs are safe and not inherited team memberships',()=>{
  const h=harness();h.send('__proto__','!team do');h.vote('__proto__','1');assert.equal(h.engine.snapshot().voterCount,1);h.restart();
  assert.equal(h.engine.snapshot().memberCounts.red,1);assert.equal(h.vote('constructor','2').reason,'wrong-team');h.store.close();
});
test('same engine supports Xiangqi without changes',()=>{
  const h=harness('xiangqi');h.send('r','!team do');h.play('r','a3a4');assert.equal(h.engine.snapshot().activeTeam,'black');h.restart();
  assert.equal(h.engine.snapshot().lastMove,'a3a4');assert.equal(h.engine.snapshot().board.kind,'xiangqi');h.store.close();
});
test('private player state isolates votes and returns a detached membership',()=>{
  const h=harness();h.send('a','!team do');h.send('b','!team do');h.vote('a','1');h.vote('b','2');
  assert.equal(h.engine.playerState('a').vote,'1');assert.equal(h.engine.playerState('b').vote,'2');assert.equal(h.engine.playerState('missing').member,null);
  h.engine.playerState('a').member!.currentTeam='black';assert.equal(h.engine.playerState('a').member?.currentTeam,'red');
  h.advance(30000);assert.equal(h.engine.playerState('a').vote,null);h.store.close();
});
test('connector 2.5.0 normalization preserves stable IDs and drops missing IDs',()=>{
  assert.deepEqual(normalizeChat({content:'!team do',user:{id:'123'},common:{msgId:'456',roomId:'789'}},100),{eventId:'tiktok:789:456',userId:'123',text:'!team do',receivedAt:100});
  assert.equal(normalizeChat({content:'hi',user:{id:'0'},common:{msgId:'123'}}),null);
});
test('eight simulated hours: empty windows, alternating players, restarts and completed matches',()=>{
  const h=harness();h.send('r','!team do');h.send('b','!team den');
  for(let second=0;second<8*3600;second++) {
    if(second>900 && second%5===0) {const s=h.engine.snapshot();if(s.phase==='voting'&&s.legalMoves.length)h.vote(s.activeTeam==='red'?'r':'b',s.legalMoves[0]);}
    if(second>0&&second%3600===0)h.restart();h.advance(1000);
  }
  assert.ok(h.engine.snapshot().metrics.emptyWindows>=30);assert.ok(h.engine.snapshot().metrics.games>20);h.store.close();
});
