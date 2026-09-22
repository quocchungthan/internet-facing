import {randomBytes,createHmac,randomUUID,randomInt} from 'node:crypto';
import {mkdirSync,readdirSync,existsSync} from 'node:fs';
import {join} from 'node:path';
import {DatabaseSync} from 'node:sqlite';
import {isIP} from 'node:net';
import {Engine} from './engine.ts';
import {Store} from './store.ts';
import {createGame} from './games.ts';
import type {Team} from './types.ts';
import type {RoomMeta,RoomSettings,RoomView,HostAction} from './room-types.ts';
export class RoomError extends Error {constructor(public statusCode:number,message:string){super(message);}}
export function canonicalIp(ip:string){
  let value=ip.toLowerCase().split('%')[0];
  if(isIP(value)===6)value=new URL(`http://[${value}]/`).hostname.slice(1,-1);
  const mapped=/^::ffff:([a-f0-9]{1,4}):([a-f0-9]{1,4})$/.exec(value);
  if(mapped){const hi=parseInt(mapped[1],16),lo=parseInt(mapped[2],16);value=`${hi>>8}.${hi&255}.${lo>>8}.${lo&255}`;}
  if(value==='::1')value='127.0.0.1';return value;
}
export const defaults:RoomSettings={game:'xiangqi',maxPeople:20,redLimit:10,blackLimit:10,allowGuests:true,turnSeconds:30,matchMinutes:30};
export function validateSettings(input:Partial<RoomSettings>):RoomSettings{
  const s={...defaults,...input};
  if(!['xiangqi','tictactoe'].includes(s.game)||typeof s.allowGuests!=='boolean')throw new RoomError(400,'Cấu hình game không hợp lệ');
  for(const [key,min,max] of [['maxPeople',1,200],['redLimit',1,100],['blackLimit',1,100],['turnSeconds',5,300],['matchMinutes',1,240]] as const)
    if(!Number.isInteger(s[key])||s[key]<min||s[key]>max)throw new RoomError(400,`${key}: từ ${min} đến ${max}`);
  if(s.redLimit>s.maxPeople||s.blackLimit>s.maxPeople)throw new RoomError(400,'Giới hạn đội không được lớn hơn giới hạn phòng');return s;
}
const cleanName=(name:string)=>{const n=name.trim();if(!n||n.length>40)throw new RoomError(400,'Tên cần từ 1 đến 40 ký tự');return n;};
export class Room {
  readonly engine:Engine;
  constructor(readonly store:Store,public meta:RoomMeta,private now:()=>number=Date.now,choose:(n:number)=>number=randomInt){
    store.db.exec('CREATE TABLE IF NOT EXISTS room_meta(id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)');
    this.engine=new Engine(createGame(meta.settings.game),store,{turnMs:meta.settings.turnSeconds*1000,revealMs:2000,intermissionMs:0,randomOnEmpty:true,autoRestart:false},now,choose);
    this.save();this.tick();
  }
  private save(){this.store.db.prepare('INSERT INTO room_meta VALUES(1,?) ON CONFLICT(id) DO UPDATE SET json=excluded.json').run(JSON.stringify(this.meta));}
  private change(fn:()=>void){const before=structuredClone(this.meta);try{this.engine.transaction(()=>{fn();this.save();});}catch(e){this.meta=before;throw e;}}
  private member(id:string){if(this.meta.closed)throw new RoomError(410,'Phòng đã đóng');if(this.meta.banned.includes(id))throw new RoomError(403,'IP này đã bị cấm trong phòng');const m=this.meta.members[id];if(!m?.active)throw new RoomError(403,'Bạn đã rời phòng hoặc bị kick. Hãy nhập mã để vào lại.');return m;}
  private hasSeat(team:Team,id:string){return Object.values(this.meta.members).filter(m=>m.active&&m.team===team&&m.id!==id).length<(team==='red'?this.meta.settings.redLimit:this.meta.settings.blackLimit);}
  join(id:string,name:string,kind:'player'|'guest'){
    if(this.meta.closed)throw new RoomError(410,'Phòng đã đóng');if(this.meta.banned.includes(id))throw new RoomError(403,'IP này đã bị cấm trong phòng');
    const n=cleanName(name),existing=this.meta.members[id];
    if(id!==this.meta.hostId&&!existing?.active&&Object.values(this.meta.members).filter(m=>m.active||m.id===this.meta.hostId).length>=this.meta.settings.maxPeople)throw new RoomError(409,'Phòng đã đủ người');
    if(!existing&&kind==='guest'&&!this.meta.settings.allowGuests)throw new RoomError(403,'Phòng không cho phép guest');
    if(existing?.team&&!existing.active&&!this.hasSeat(existing.team,id))throw new RoomError(409,'Đội của bạn đã đầy. Host cần sắp xếp lại chỗ');
    this.change(()=>{if(existing){if(!existing.active)this.engine.assignTeam(id,existing.team);existing.name=n;existing.active=true;}else this.meta.members[id]={id,name:n,team:null,kind,active:true,request:null};});
  }
  rename(id:string,name:string){const m=this.member(id);const n=cleanName(name);this.change(()=>{m.name=n;});}
  chooseTeam(id:string,team:Team){const m=this.member(id);if(m.team===team)return;if(m.team)throw new RoomError(403,'Đội đã khóa. Gửi yêu cầu để host duyệt');if(m.kind==='guest')throw new RoomError(403,'Guest cần được host chuyển vào đội');if(!this.hasSeat(team,id))throw new RoomError(409,'Đội đã đủ người');
    this.change(()=>{m.team=team;m.request=null;this.engine.assignTeam(id,team);});
  }
  requestTeam(id:string,team:Team){const m=this.member(id);if(m.team===team)throw new RoomError(400,'Bạn đã ở đội này');this.change(()=>{m.request=team;});}
  leave(id:string){const m=this.member(id);this.change(()=>{m.active=false;m.request=null;this.engine.assignTeam(id,null);});}
  vote(id:string,windowId:number,move:string){this.tick();const m=this.member(id);if(m.kind==='guest'||!m.team)throw new RoomError(403,'Bạn cần vào đội để bỏ phiếu');
    // An inactive member retains a team lock in metadata; restore only that team on rejoin.
    const assigned=this.engine.playerState(id).member;if(assigned?.currentTeam!==m.team)this.engine.assignTeam(id,m.team);
    return this.engine.receive({eventId:randomUUID(),userId:id,text:`!vote W${windowId} ${move}`,receivedAt:this.now()});
  }
  host(id:string,action:HostAction,targetId?:string,team?:Team|'guest'){
    this.member(id);if(id!==this.meta.hostId)throw new RoomError(403,'Chỉ host được quản lý phòng');
    if(action==='restart'){this.change(()=>{this.engine.restart();this.meta.matchDeadline=this.now()+this.meta.settings.matchMinutes*60000;});return;}
    if(action==='close'){this.change(()=>{this.meta.closed=true;this.engine.finish('Host đã đóng phòng');});return;}
    const target=targetId?this.meta.members[targetId]:undefined;if(!target)throw new RoomError(404,'Không tìm thấy thành viên');
    if((action==='kick'||action==='ban')&&target.id===id)throw new RoomError(400,'Host không thể tự kick/ban');
    if(action==='unban'){this.change(()=>{this.meta.banned=this.meta.banned.filter(value=>value!==target.id);});return;}
    if(action==='kick'||action==='ban'){this.change(()=>{target.active=false;target.request=null;this.engine.assignTeam(target.id,null);if(action==='ban'&&!this.meta.banned.includes(target.id))this.meta.banned.push(target.id);});return;}
    if(action==='reject'){this.change(()=>{target.request=null;});return;}
    const destination=action==='approve'?target.request:team;
    if(destination!=='red'&&destination!=='black'&&destination!=='guest')throw new RoomError(400,'Chọn đội đích');
    if(destination!=='guest'&&target.active&&!this.hasSeat(destination,target.id))throw new RoomError(409,'Đội đích đã đủ người');
    this.change(()=>{target.team=destination==='guest'?null:destination;target.kind=destination==='guest'?'guest':'player';target.request=null;this.engine.assignTeam(target.id,target.active?target.team:null);});
  }
  tick(){if(this.meta.closed)return;if(this.now()>=this.meta.matchDeadline)this.engine.finish('Hết thời gian trận · hòa');else this.engine.tick();}
  view(id:string):RoomView{
    this.tick();const me=this.member(id),isHost=id===this.meta.hostId;const game=this.engine.snapshot();
    const canSeeVotes=isHost||(me.kind==='player'&&!!me.team&&me.team===game.activeTeam);
    if(!canSeeVotes){game.tally={};game.voterCount=0;}
    if(!isHost&&me.team!==game.lastTeam)game.lastTally={};
    if(!me.team||me.team!==game.activeTeam||me.kind==='guest')game.legalMoves=[];
    if(!isHost){game.notices=[];game.metrics.comments=0;game.metrics.acceptedVotes=0;game.metrics.rejected=0;game.metrics.emptyWindows=0;}
    const active=Object.values(this.meta.members).filter(m=>m.active);
    return {code:this.meta.code,title:this.meta.title,hostId:this.meta.hostId,isHost,settings:{...this.meta.settings},matchDeadline:this.meta.matchDeadline,
      me:{...me},members:active.map(m=>({...m,request:isHost||m.id===id?m.request:null})),requests:isHost?active.filter(m=>!!m.request).map(m=>({...m})):[],
      bans:isHost?this.meta.banned.map(id=>({id,name:this.meta.members[id]?.name??'Thành viên'})):[],game,myVote:this.engine.playerState(id).vote,canSeeVotes};
  }
  close(){this.store.close();}
}
export class RoomManager {
  private rooms=new Map<string,Room>();private key:string;private db:DatabaseSync;
  constructor(private directory:string,private now:()=>number=Date.now){
    mkdirSync(directory,{recursive:true});this.db=new DatabaseSync(join(directory,'registry.sqlite'));this.db.exec('CREATE TABLE IF NOT EXISTS config(key TEXT PRIMARY KEY,value TEXT NOT NULL)');
    let row=this.db.prepare("SELECT value FROM config WHERE key='ip-secret'").get() as {value:string}|undefined;
    if(!row){this.db.prepare('INSERT INTO config VALUES(?,?)').run('ip-secret',randomBytes(32).toString('hex'));row=this.db.prepare("SELECT value FROM config WHERE key='ip-secret'").get() as {value:string};}this.key=row.value;
    for(const file of readdirSync(directory).filter(f=>/^[A-Z2-9]{6}\.sqlite$/.test(f))){const store=new Store(join(directory,file));try{const row=store.db.prepare('SELECT json FROM room_meta WHERE id=1').get() as {json:string}|undefined;if(row){const meta=JSON.parse(row.json) as RoomMeta;this.rooms.set(meta.code,new Room(store,meta,now));}else store.close();}catch(e){store.close();throw e;}}
  }
  identity(ip:string){return createHmac('sha256',this.key).update(canonicalIp(ip)).digest('hex');}
  get(code:string){const room=this.rooms.get(code.toUpperCase());if(!room)throw new RoomError(404,'Mã phòng không tồn tại');return room;}
  create(id:string,name:string,title:string,input:Partial<RoomSettings>){
    const settings=validateSettings(input),clean=cleanName(name);let code='';const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    do{code=Array.from({length:6},()=>alphabet[randomInt(alphabet.length)]).join('');}while(this.rooms.has(code)||existsSync(join(this.directory,`${code}.sqlite`)));
    const meta:RoomMeta={code,title:title.trim().slice(0,60)||'Kỳ Hội cùng bạn bè',hostId:id,createdAt:this.now(),matchDeadline:this.now()+settings.matchMinutes*60000,settings,members:{[id]:{id,name:clean,team:null,kind:'player',active:true,request:null}},banned:[],closed:false};
    const room=new Room(new Store(join(this.directory,`${code}.sqlite`)),meta,this.now);this.rooms.set(code,room);return room;
  }
  tick(){for(const room of this.rooms.values())room.tick();}
  close(){for(const room of this.rooms.values())room.close();this.db.close();}
}
