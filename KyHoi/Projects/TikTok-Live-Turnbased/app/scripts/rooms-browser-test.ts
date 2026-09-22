import {chromium,type Page} from 'playwright';
import {createServer,request as httpRequest} from 'node:http';
import {connect} from 'node:net';
import {mkdtempSync,mkdirSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';import {join,resolve} from 'node:path';
import assert from 'node:assert/strict';import {WebSocket} from 'ws';
import {createRoomServer} from '../src/room-server.ts';
import type {RoomView} from '../src/room-types.ts';
const out=resolve('../verification');mkdirSync(out,{recursive:true});
let now=Date.now();const {app,manager,publish}=await createRoomServer({directory:mkdtempSync(join(tmpdir(),'kyhoi-room-ui-')),now:()=>now});await app.listen({host:'127.0.0.1',port:3320});
const url='http://127.0.0.1:3320';const browser=await chromium.launch({headless:true});const checks:string[]=[],errors:string[]=[];let passed=false;
const proxies:ReturnType<typeof createServer>[]=[];const sockets:WebSocket[]=[];
// Test-only local proxies bind distinct TCP source addresses. No production IP override exists.
async function proxy(ip:string){
 const server=createServer((req,res)=>{const target=new URL(req.url!,url);const upstream=httpRequest({hostname:'127.0.0.1',port:3320,localAddress:ip,path:target.pathname+target.search,method:req.method,headers:{...req.headers,host:'127.0.0.1:3320'}},response=>{res.writeHead(response.statusCode!,response.headers);response.pipe(res);});upstream.on('error',()=>{res.writeHead(502);res.end();});req.pipe(upstream);});
 server.on('connect',(req,socket,head)=>{const upstream=connect({host:'127.0.0.1',port:3320,localAddress:ip},()=>{socket.write('HTTP/1.1 200 Connection Established\r\n\r\n');if(head.length)upstream.write(head);socket.pipe(upstream);upstream.pipe(socket);});upstream.on('error',()=>socket.destroy());socket.on('error',()=>upstream.destroy());});
 server.on('upgrade',(req,socket,head)=>{const upstream=connect({host:'127.0.0.1',port:3320,localAddress:ip},()=>{const target=new URL(req.url!,url);upstream.write(`GET ${target.pathname+target.search} HTTP/1.1\r\n`+Object.entries({...req.headers,host:'127.0.0.1:3320'}).map(([k,v])=>`${k}: ${v}`).join('\r\n')+'\r\n\r\n');if(head.length)upstream.write(head);socket.pipe(upstream);upstream.pipe(socket);});upstream.on('error',()=>socket.destroy());socket.on('error',()=>upstream.destroy());});
 await new Promise<void>(r=>server.listen(0,'127.0.0.1',r));proxies.push(server);return `http://127.0.0.1:${(server.address() as {port:number}).port}`;
}
async function pageFor(ip?:string){const context=await browser.newContext({viewport:{width:1440,height:1200},...(ip?{proxy:{server:await proxy(ip),bypass:'<-loopback>'}}:{})});const page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));await page.goto(url);return page;}
async function joinRoom(page:Page,code:string,name:string,guest=false){await page.locator('#join-name').fill(name);await page.locator('#join-code').fill(code);if(guest)await page.locator('#as-guest').check();await page.locator('#join-room').click();await page.locator('#board img').first().waitFor();}
async function team(page:Page,name:string){await page.locator('#team-actions').getByRole('button',{name,exact:true}).click();await page.waitForTimeout(100);}
async function move(page:Page,a:string,b:string){await page.locator(`[data-square="${a}"]`).click();await page.locator(`[data-square="${b}"]`).click();}
function watch(ip:string,code:string){const ws=new WebSocket(`ws://127.0.0.1:3320/ws/${code}`,{localAddress:ip});sockets.push(ws);const seen:RoomView[]=[];ws.on('message',bytes=>{const data=JSON.parse(bytes.toString());if(data.room)seen.push(data.room);});return seen;}
try{
 const host=await pageFor();await host.screenshot({path:join(out,'rooms-lobby.png'),fullPage:true});
 await host.locator('#create-name').fill('Chủ phòng');await host.locator('#room-title').fill('Kỳ Hội · Bạn bè');await host.locator('#turn-seconds').fill('5');await host.locator('#match-minutes').fill('1');await host.locator('#create-room').click();await host.locator('#board img').first().waitFor();
 const code=host.url().split('/room/')[1];assert.match(code,/^[A-Z2-9]{6}$/);await team(host,'Vào Đỏ');checks.push('Create code, match begins, limits/timers configured');
 const same=await host.context().newPage();await same.goto(url);await joinRoom(same,code,'Cùng IP');await same.locator('#host-panel').waitFor();assert.equal(await same.locator('.member-card').count(),1);await host.waitForFunction(()=>document.querySelector('#my-name')?.textContent==='Cùng IP');checks.push('Two tabs on the same IP share one named person and host role');
 const red=await pageFor('127.0.0.2');await joinRoom(red,code,'Minh');await team(red,'Vào Đỏ');
 const black=await pageFor('127.0.0.3');await joinRoom(black,code,'Lan');await team(black,'Vào Đen');
 const guest=await pageFor('127.0.0.4');await joinRoom(guest,code,'Khách',true);
 const redWire=watch('127.0.0.2',code),blackWire=watch('127.0.0.3',code),guestWire=watch('127.0.0.4',code);await host.waitForFunction(()=>document.querySelectorAll('.member-card').length===4);
 await move(red,'b2','b5');await host.locator('.vote-arrow[data-move="b2b5"]').waitFor();await red.locator('.vote-arrow[data-move="b2b5"]').waitFor();await black.waitForTimeout(600);
 assert.equal(await black.locator('.vote-arrow').count(),0);assert.equal(await guest.locator('.vote-arrow').count(),0);assert.ok(redWire.some(v=>v.game.tally.b2b5===1));assert.ok(blackWire.length>0&&guestWire.length>0);assert.ok(blackWire.every(v=>!Object.keys(v.game.tally).length));assert.ok(guestWire.every(v=>!Object.keys(v.game.tally).length));checks.push('Different source IPs: teammate and host see vote; opponent/guest HTTP and WebSocket do not');
 await host.screenshot({path:join(out,'room-host.png'),fullPage:true});await black.screenshot({path:join(out,'room-opponent-private.png'),fullPage:true});await guest.screenshot({path:join(out,'room-guest.png'),fullPage:true});
 await red.locator('#team-actions').getByRole('button',{name:'Xin sang Đen',exact:true}).click();await host.locator('#requests').getByRole('button',{name:'Duyệt',exact:true}).waitFor();assert.equal(await red.locator('#team-actions').getByRole('button',{name:'Vào Đen',exact:true}).count(),0);
 await host.locator('#requests').getByRole('button',{name:'Duyệt',exact:true}).click();await red.waitForFunction(()=>document.querySelector('#my-team')?.textContent==='Đen');await host.waitForFunction(()=>document.querySelectorAll('.vote-arrow').length===0);checks.push('Team lock and host-approved request, old-team vote removed');
 const gid=manager.identity('127.0.0.4');await host.locator(`.member-card[data-member="${gid}"]`).dragTo(host.locator('.team-drop[data-team="red"]'));await guest.waitForFunction(()=>document.querySelector('#my-team')?.textContent==='Đỏ');checks.push('Host drags guest card into a team');
 const bid=manager.identity('127.0.0.3');await host.locator(`.member-card[data-member="${bid}"]`).getByRole('button',{name:'Kick',exact:true}).click();await black.locator('#join-code').waitFor();await joinRoom(black,code,'Lan trở lại');await black.waitForFunction(()=>document.querySelector('#my-team')?.textContent==='Đen');
 await host.locator(`.member-card[data-member="${bid}"]`).getByRole('button',{name:'Ban',exact:true}).click();await black.locator('#join-code').waitFor();await black.locator('#join-code').fill(code);await black.locator('#join-name').fill('Tên khác');await black.locator('#join-room').click();await black.waitForFunction(()=>document.querySelector('#lobby-message')?.textContent?.includes('bị cấm'));checks.push('Kick removes all access; explicit rejoin keeps team; ban blocks renamed same IP');
 now+=5000;manager.tick();publish();await host.waitForFunction(()=>document.querySelector('#last-move')?.textContent?.startsWith('Vừa đi:'));assert.equal(manager.get(code).view(manager.identity('127.0.0.1')).game.metrics.moves,1);checks.push('Empty deadline chooses random legal move');
 now+=60000;manager.tick();publish();await host.waitForFunction(()=>document.querySelector('#phase-label')?.textContent?.includes('HÒA'));checks.push('Match deadline draws and finishes without waiting');
 await guest.locator('#leave-room').click();await guest.locator('#create-room').waitFor();checks.push('Leave returns to create/join screen');
 const mobile=await browser.newPage({viewport:{width:390,height:844}});await mobile.goto(url);assert.ok(await mobile.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await mobile.screenshot({path:join(out,'rooms-mobile-lobby.png'),fullPage:true});
 const other=await pageFor();await other.locator('#create-name').fill('Caro host');await other.locator('#game').selectOption('tictactoe');await other.locator('#create-room').click();await other.locator('.grid-cell').first().waitFor();await team(other,'Vào Đỏ');await other.locator('.grid-cell[data-move="1"]').click();await other.locator('.cell-votes').waitFor();checks.push('Caro remains a supported independent room');
 assert.deepEqual(errors,[]);passed=true;
}finally{for(const ws of sockets)ws.terminate();await browser.close();for(const p of proxies){p.closeAllConnections();p.close();}await app.close();writeFileSync(join(out,'rooms-browser-results.json'),JSON.stringify({checkedAt:new Date().toISOString(),passed,checks,errors},null,2));}
console.log(JSON.stringify({passed,checks,errors},null,2));
