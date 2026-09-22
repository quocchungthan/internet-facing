import Fastify from 'fastify';
import fastifyStatic from '@fastify/static';
import websocket from '@fastify/websocket';
import {WebSocket} from 'ws';
import {resolve} from 'node:path';
import {RoomManager,RoomError} from './rooms.ts';
import type {RoomSettings,HostAction} from './room-types.ts';
import type {Team} from './types.ts';
export async function createRoomServer(options:{directory:string;now?:()=>number;trustProxy?:string;serveStatic?:boolean;logger?:boolean;timer?:boolean}){
  const manager=new RoomManager(options.directory,options.now);
  const app=Fastify({logger:options.logger??false,bodyLimit:8192,trustProxy:options.trustProxy??false});
  await app.register(websocket,{options:{maxPayload:1024}});
  const clients=new Map<WebSocket,{code:string;id:string}>();
  function publish(code?:string){for(const [socket,client]of clients){if(code&&client.code!==code)continue;if(socket.readyState!==WebSocket.OPEN)continue;if(socket.bufferedAmount>1024*1024){socket.terminate();continue;}
    try{socket.send(JSON.stringify({type:'state',room:manager.get(client.code).view(client.id)}));}
    catch(error){socket.send(JSON.stringify({type:'removed',message:error instanceof RoomError?error.message:'Không thể đọc phòng'}));socket.close(4003);}
  }}
  app.setErrorHandler((error,req,reply)=>{const validation=error instanceof Error&&'validation' in error;const status=error instanceof RoomError?error.statusCode:validation?400:500;if(status===500)app.log.error(error);reply.code(status).send({error:status===500?'Lỗi máy chủ; vui lòng thử lại':error instanceof Error?error.message:'Yêu cầu không hợp lệ'});});
  // All identity and host permissions derive from the same server-resolved address.
  // X-Forwarded-For is ignored unless the connecting proxy matches an explicit allowlist.
  app.addHook('onRequest',async(req,reply)=>{
    if(req.url.startsWith('/api/')||req.url.startsWith('/ws/')){
      const origin=req.headers.origin;
      if(origin&&origin!==`${req.protocol}://${req.headers.host}`)return reply.code(403).send({error:'Yêu cầu khác origin không được chấp nhận'});
      reply.header('Cache-Control','no-store');
    }
  });
  const nameSchema={type:'string',minLength:1,maxLength:40};
  const teamSchema={type:'string',enum:['red','black']};
  const params={type:'object',required:['code'],properties:{code:{type:'string',pattern:'^[A-Za-z2-9]{6}$'}}};
  const body=(properties:Record<string,unknown>,required:string[])=>({type:'object',additionalProperties:false,required,properties});
  app.get('/health',()=>({status:'ok',mode:'rooms',identity:'ip'}));
  app.post<{Body:{name:string;title?:string;settings?:Partial<RoomSettings>}}>('/api/rooms',{schema:{body:body({name:nameSchema,title:{type:'string',maxLength:60},settings:body({
    game:{type:'string',enum:['xiangqi','tictactoe']},maxPeople:{type:'integer',minimum:1,maximum:200},redLimit:{type:'integer',minimum:1,maximum:100},blackLimit:{type:'integer',minimum:1,maximum:100},allowGuests:{type:'boolean'},turnSeconds:{type:'integer',minimum:5,maximum:300},matchMinutes:{type:'integer',minimum:1,maximum:240}
  },[])},['name'])}},(req,reply)=>{const id=manager.identity(req.ip);const room=manager.create(id,req.body.name,req.body.title??'',req.body.settings??{});return reply.code(201).send(room.view(id));});
  app.post<{Params:{code:string};Body:{name:string;kind?:'player'|'guest'}}>('/api/rooms/:code/join',{schema:{params,body:body({name:nameSchema,kind:{type:'string',enum:['player','guest']}},['name'])}},req=>{const room=manager.get(req.params.code),id=manager.identity(req.ip);room.join(id,req.body.name,req.body.kind??'player');publish(room.meta.code);return room.view(id);});
  app.get<{Params:{code:string}}>('/api/rooms/:code',{schema:{params}},req=>manager.get(req.params.code).view(manager.identity(req.ip)));
  app.post<{Params:{code:string};Body:{name:string}}>('/api/rooms/:code/name',{schema:{params,body:body({name:nameSchema},['name'])}},req=>{const room=manager.get(req.params.code),id=manager.identity(req.ip);room.rename(id,req.body.name);publish(room.meta.code);return room.view(id);});
  for(const action of ['team','request'] as const)app.post<{Params:{code:string};Body:{team:Team}}>(`/api/rooms/:code/${action}`,{schema:{params,body:body({team:teamSchema},['team'])}},req=>{const room=manager.get(req.params.code),id=manager.identity(req.ip);if(action==='team')room.chooseTeam(id,req.body.team);else room.requestTeam(id,req.body.team);publish(room.meta.code);return room.view(id);});
  app.post<{Params:{code:string};Body:{windowId:number;move:string}}>('/api/rooms/:code/vote',{schema:{params,body:body({windowId:{type:'integer',minimum:1},move:{type:'string',pattern:'^[a-z0-9]{1,20}$'}},['windowId','move'])}},req=>{const room=manager.get(req.params.code),id=manager.identity(req.ip);const result=room.vote(id,req.body.windowId,req.body.move);publish(room.meta.code);return {...result,room:room.view(id)};});
  app.post<{Params:{code:string}}>('/api/rooms/:code/leave',{schema:{params}},req=>{const room=manager.get(req.params.code);room.leave(manager.identity(req.ip));publish(room.meta.code);return {left:true};});
  app.post<{Params:{code:string};Body:{action:HostAction;targetId?:string;team?:Team|'guest'}}>('/api/rooms/:code/host',{schema:{params,body:body({action:{type:'string',enum:['kick','ban','unban','move','approve','reject','restart','close']},targetId:{type:'string',pattern:'^[a-f0-9]{64}$'},team:{type:'string',enum:['red','black','guest']}},['action'])}},req=>{const room=manager.get(req.params.code),id=manager.identity(req.ip);room.host(id,req.body.action,req.body.targetId,req.body.team);publish(room.meta.code);return req.body.action==='close'?{closed:true}:room.view(id);});
  app.get<{Params:{code:string}}>('/ws/:code',{websocket:true,schema:{params}},(socket,req)=>{
    const client={code:req.params.code.toUpperCase(),id:manager.identity(req.ip)};socket.on('error',()=>socket.terminate());socket.on('close',()=>clients.delete(socket));
    try{socket.send(JSON.stringify({type:'state',room:manager.get(client.code).view(client.id)}));clients.set(socket,client);}
    catch(error){socket.send(JSON.stringify({type:'removed',message:error instanceof Error?error.message:'Không thể vào phòng'}));socket.close(4003);}
  });
  if(options.serveStatic!==false){await app.register(fastifyStatic,{root:resolve('dist')});app.get('/operator',(req,reply)=>reply.redirect('/'));app.get('/room/:code',(req,reply)=>reply.sendFile('index.html'));}
  const timer=options.timer===false?undefined:setInterval(()=>{try{manager.tick();publish();}catch(error){app.log.error(error);}},500);
  app.addHook('onClose',async()=>{clearInterval(timer);for(const socket of clients.keys())socket.terminate();manager.close();});
  return {app,manager,publish};
}
