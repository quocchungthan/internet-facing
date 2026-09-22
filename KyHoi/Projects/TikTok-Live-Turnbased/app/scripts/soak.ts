import {mkdirSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {createRoomServer} from '../src/room-server.ts';
import type {RoomView} from '../src/room-types.ts';
// Real wall-clock isolated engine/API soak; remoteAddress exercises IP identity.
const seconds=Number(process.env.SOAK_SECONDS??28800);
if(!Number.isFinite(seconds)||seconds<=0)throw new Error('Invalid SOAK_SECONDS');
const started=Date.now(),folder=resolve('../verification');mkdirSync(folder,{recursive:true});
const {app}=await createRoomServer({directory:resolve(`data/room-soak-${started}`),serveStatic:false});
await app.ready();
async function api(path:string,ip='127.0.0.1',body?:object){const r=await app.inject({url:path,remoteAddress:ip,method:body?'POST':'GET',payload:body});if(r.statusCode!==200&&r.statusCode!==201)throw new Error(`${r.statusCode}: ${r.body}`);return r.json();}
let polls=0,votes=0,restarts=0,stopping=false;
const report=(status:string,error?:string)=>writeFileSync(resolve(folder,'soak-status.json'),JSON.stringify({status,error,mode:'ip-rooms-inject',startedAt:new Date(started).toISOString(),updatedAt:new Date().toISOString(),plannedSeconds:seconds,elapsedSeconds:Math.floor((Date.now()-started)/1000),polls,votes,restarts,runnerPid:process.pid},null,2));
process.on('SIGINT',()=>{stopping=true;});process.on('SIGTERM',()=>{stopping=true;});
try{
 const room:RoomView=await api('/api/rooms','127.0.0.1',{name:'Soak host',settings:{turnSeconds:5,matchMinutes:240}}),base=`/api/rooms/${room.code}`;
 for(const [ip,team]of [['127.0.0.2','red'],['127.0.0.3','black']]){await api(`${base}/join`,ip,{name:team});await api(`${base}/team`,ip,{team});}
 report('running');let lastWindow=-1;
 while(!stopping&&Date.now()-started<seconds*1000){
  const state:RoomView=await api(base);polls++;
  if(state.game.phase==='finished'){await api(`${base}/host`,'127.0.0.1',{action:'restart'});restarts++;lastWindow=-1;}
  else if((Date.now()-started)/1000%3600>=900&&state.game.phase==='voting'&&state.game.windowId!==lastWindow){
   const ip=state.game.activeTeam==='red'?'127.0.0.2':'127.0.0.3',player:RoomView=await api(base,ip);
   if(player.game.legalMoves.length){const result=await api(`${base}/vote`,ip,{windowId:player.game.windowId,move:player.game.legalMoves[0]});if(!result.accepted)throw new Error('Soak vote rejected');votes++;lastWindow=player.game.windowId;}
  }
  report('running');await new Promise(r=>setTimeout(r,1000));
 }
 report(stopping?'stopped':'passed');
}catch(error){report('failed',String(error));process.exitCode=1;}finally{await app.close();}
