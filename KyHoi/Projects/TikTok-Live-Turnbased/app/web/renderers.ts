import $ from 'jquery';
import type {Snapshot} from '../src/types.ts';
import {VoteArrows} from './vote-arrows.ts';
declare global { interface Window { jQuery:typeof $; $:typeof $; Xiangqiboard:(id:string,config:Record<string,unknown>)=>{position:(fen:string,animate?:boolean)=>void;resize:()=>void;destroy:()=>void} } }
export interface Interaction {canVote:()=>boolean;onVote:(move:string,windowId:number)=>void;onHint:(message:string)=>void}
export interface Renderer {update(s:Snapshot,ownVote?:string|null):void;clearSelection():void;destroy():void}
window.jQuery=$;window.$=$;
export async function createRenderer(kind:string,container:HTMLElement,interaction?:Interaction):Promise<Renderer>{
  if(kind==='tictactoe'){
    container.className='grid-game';let state:Snapshot|undefined;
    const cells=Array.from({length:9},(_,i)=>{const cell=document.createElement(interaction?'button':'div');cell.className='grid-cell';cell.dataset.move=String(i+1);cell.textContent=String(i+1);container.append(cell);
      if(interaction)cell.addEventListener('click',()=>{if(!state||!interaction.canVote())return;if(state.legalMoves.includes(String(i+1)))interaction.onVote(String(i+1),state.windowId);else interaction.onHint('Ô này đã có quân. Chọn một ô trống.');});return cell;});
    return{update(s,ownVote){state=s;s.board.cells?.forEach((value,i)=>{const cell=cells[i];cell.replaceChildren(document.createTextNode(value==='red'?'×':value==='black'?'○':String(i+1)));cell.dataset.team=value??'empty';cell.classList.toggle('own-cell',ownVote===String(i+1));const count=s.phase==='voting'?s.tally[String(i+1)]??0:0;if(count){const badge=document.createElement('span');badge.className='cell-votes';badge.textContent=`${count} phiếu`;cell.append(badge);}});},clearSelection(){},destroy(){container.replaceChildren();}};
  }
  await new Promise<void>((resolve,reject)=>{if('Xiangqiboard' in window)return resolve();const script=document.createElement('script');script.src='/vendor/xiangqiboard.js';script.onload=()=>resolve();script.onerror=reject;document.head.append(script);});
  container.className=`xiangqi-game${interaction?' interactive-board':''}`;
  const board=window.Xiangqiboard(container.id,{position:'start',orientation:'red',showNotation:true,draggable:false,pieceTheme:'/pieces/{piece}.svg',boardTheme:'/board.svg',moveSpeed:250});
  const arrows=new VoteArrows(container);
  let state:Snapshot|undefined,lastFen='',selected:string|null=null,ownVote:string|null=null;
  let gesture:{source:string;x:number;y:number;pointerId:number;windowId:number;dragged:boolean}|null=null;
  let ghost:HTMLImageElement|null=null;
  const squareAt=(x:number,y:number)=>document.elementFromPoint(x,y)?.closest<HTMLElement>('[data-square]');
  const isSource=(square:string)=>state?.legalMoves.some(m=>m.startsWith(square))??false;
  function highlights(){
    container.querySelectorAll('.selected-square,.legal-target').forEach(el=>el.classList.remove('selected-square','legal-target'));
    if(selected){container.querySelector(`[data-square="${selected}"]`)?.classList.add('selected-square');for(const m of state?.legalMoves??[])if(m.startsWith(selected))container.querySelector(`[data-square="${m.slice(2)}"]`)?.classList.add('legal-target');}
  }
  function clearSelection(){selected=null;gesture=null;ghost?.remove();ghost=null;container.classList.remove('is-dragging');highlights();}
  function propose(source:string,target:string,windowId:number){
    const move=source+target;
    if(state?.windowId!==windowId||state.phase!=='voting'){clearSelection();interaction?.onHint('Lượt đã đổi. Hãy chọn lại nước đi.');return;}
    if(!state.legalMoves.includes(move)){interaction?.onHint('Nước này chưa hợp lệ. Chọn một ô có dấu chấm.');return;}
    clearSelection();interaction?.onVote(move,windowId);
  }
  const down=(event:PointerEvent)=>{
    if(!interaction||event.button!==0)return;
    const cell=(event.target as Element).closest<HTMLElement>('[data-square]');if(!cell||!container.contains(cell))return;
    if(!interaction.canVote()||!state)return;
    gesture={source:cell.dataset.square!,x:event.clientX,y:event.clientY,pointerId:event.pointerId,windowId:state.windowId,dragged:false};
    container.setPointerCapture(event.pointerId);event.preventDefault();
  };
  const move=(event:PointerEvent)=>{
    if(!gesture||gesture.pointerId!==event.pointerId||!isSource(gesture.source))return;
    if(!gesture.dragged&&Math.hypot(event.clientX-gesture.x,event.clientY-gesture.y)>7){
      gesture.dragged=true;selected=gesture.source;highlights();container.classList.add('is-dragging');
      const img=container.querySelector<HTMLImageElement>(`[data-square="${gesture.source}"] img`);
      if(img){ghost=img.cloneNode() as HTMLImageElement;ghost.className='drag-ghost';ghost.style.width=`${img.getBoundingClientRect().width}px`;document.body.append(ghost);}
    }
    if(ghost){ghost.style.left=`${event.clientX}px`;ghost.style.top=`${event.clientY}px`;}event.preventDefault();
  };
  const up=(event:PointerEvent)=>{
    if(!gesture||gesture.pointerId!==event.pointerId)return;
    const g=gesture;gesture=null;ghost?.remove();ghost=null;container.classList.remove('is-dragging');
    if(container.hasPointerCapture(event.pointerId))container.releasePointerCapture(event.pointerId);
    const cell=squareAt(event.clientX,event.clientY);const target=cell&&container.contains(cell)?cell.dataset.square:null;
    if(!target){clearSelection();return;}
    if(g.dragged){if(target!==g.source)propose(g.source,target,g.windowId);else clearSelection();return;}
    if(selected===target){clearSelection();return;}
    if(selected&&state?.legalMoves.includes(selected+target)){propose(selected,target,g.windowId);return;}
    if(isSource(target)){selected=target;highlights();interaction?.onHint(`Đã chọn ${target} · bấm ô đến hoặc kéo quân.`);}
    else interaction?.onHint('Chọn quân của đội đến lượt để bình chọn.');
  };
  const preventDrag=(event:Event)=>event.preventDefault();
  if(interaction){container.addEventListener('pointerdown',down);container.addEventListener('pointermove',move);container.addEventListener('pointerup',up);container.addEventListener('pointercancel',clearSelection);container.addEventListener('dragstart',preventDrag);}
  const observer=new ResizeObserver(()=>{board.resize();highlights();if(state)arrows.update(state,ownVote);});observer.observe(container);
  return{update(s,personalVote=null){if(state&&(state.windowId!==s.windowId||s.phase!=='voting'||!s.connected))clearSelection();state=s;ownVote=personalVote;
    if(s.board.fen&&s.board.fen!==lastFen){board.position(s.board.fen,!!lastFen);lastFen=s.board.fen;}
    container.querySelectorAll('.last-square').forEach(el=>el.classList.remove('last-square'));
    if(s.lastMove&&/^[a-i]\d[a-i]\d$/.test(s.lastMove))for(const square of[s.lastMove.slice(0,2),s.lastMove.slice(2)])container.querySelector(`[data-square="${square}"]`)?.classList.add('last-square');
    highlights();arrows.update(s,ownVote);
  },clearSelection,destroy(){clearSelection();observer.disconnect();arrows.destroy();container.removeEventListener('pointerdown',down);container.removeEventListener('pointermove',move);container.removeEventListener('pointerup',up);container.removeEventListener('pointercancel',clearSelection);container.removeEventListener('dragstart',preventDrag);board.destroy();}};
}
