import type {Snapshot} from '../src/types.ts';
const NS='http://www.w3.org/2000/svg';
function svgEl<K extends keyof SVGElementTagNameMap>(tag:K,attributes:Record<string,string|number>={}){const el=document.createElementNS(NS,tag);for(const [key,value] of Object.entries(attributes))el.setAttribute(key,String(value));return el;}
export class VoteArrows {
  private svg=svgEl('svg',{'class':'vote-arrows','aria-label':'Các nước đang được bình chọn',role:'img'});
  private cache='';
  constructor(private container:HTMLElement){container.append(this.svg);}
  update(s:Snapshot,ownVote:string|null=null){
    const box=this.container.getBoundingClientRect();const tally=s.phase==='voting'&&s.connected?s.tally:{};
    const key=JSON.stringify([box.width,box.height,tally,s.windowId,ownVote]);if(key===this.cache)return;this.cache=key;
    this.svg.setAttribute('viewBox',`0 0 ${box.width} ${box.height}`);this.svg.replaceChildren();
    const center=(square:string)=>{const cell=this.container.querySelector(`[data-square="${square}"]`);if(!cell)return null;const r=cell.getBoundingClientRect();return {x:r.left-box.left+r.width/2,y:r.top-box.top+r.height/2,size:r.width};};
    for(const [move,count] of Object.entries(tally).sort((a,b)=>a[1]-b[1]||a[0].localeCompare(b[0]))){
      if(!/^[a-i]\d[a-i]\d$/.test(move)||count<=0)continue;const a=center(move.slice(0,2)),b=center(move.slice(2));if(!a||!b)continue;
      const dx=b.x-a.x,dy=b.y-a.y,length=Math.hypot(dx,dy);if(!length)continue;const ux=dx/length,uy=dy/length,nx=-uy,ny=ux;
      const unit=a.size/80,weight=Math.min(30,4+count*5)*unit,bend=((move.charCodeAt(0)+move.charCodeAt(3))%3-1)*4*unit;
      const start={x:a.x+ux*12*unit,y:a.y+uy*12*unit},end={x:b.x-ux*9*unit,y:b.y-uy*9*unit};
      const middle={x:(start.x+end.x)/2+nx*bend,y:(start.y+end.y)/2+ny*bend};const path=`M${start.x},${start.y} Q${middle.x},${middle.y} ${end.x},${end.y}`;
      const head=15*unit+weight*.6,arrowhead=`M${end.x-ux*head+nx*head*.62},${end.y-uy*head+ny*head*.62} L${end.x},${end.y} L${end.x-ux*head-nx*head*.62},${end.y-uy*head-ny*head*.62}`;
      const group=svgEl('g',{'class':`vote-arrow${move===ownVote?' is-own':''}`,'data-move':move,'data-count':count});
      const title=svgEl('title');title.textContent=`${move}: ${count} phiếu${move===ownVote?' · phiếu của bạn':''}`;group.append(title);
      const color=s.activeTeam==='red'?'#bc4e31':'#216657';
      if(move===ownVote)group.append(svgEl('path',{d:path,stroke:'#fff3b3','stroke-width':weight+7*unit,'class':'arrow-own-halo'}));
      group.append(svgEl('path',{d:path,stroke:'#493923','stroke-width':weight+3*unit,'class':'arrow-shadow'}));
      group.append(svgEl('path',{d:path,stroke:color,'stroke-width':weight,'class':'arrow-ink'}));
      group.append(svgEl('path',{d:`M${start.x+nx*2*unit},${start.y+ny*2*unit} Q${middle.x+nx*2*unit},${middle.y+ny*2*unit} ${end.x},${end.y}`,stroke:'#f8dfac','stroke-width':Math.max(1,weight*.18),'class':'arrow-bristle'}));
      group.append(svgEl('path',{d:arrowhead,stroke:color,'stroke-width':Math.max(4*unit,weight*.75),'class':'arrow-head'}));
      const badge=svgEl('g',{'class':'arrow-badge',transform:`translate(${start.x+dx*.55+nx*9*unit},${start.y+dy*.55+ny*9*unit})`});
      badge.append(svgEl('circle',{r:(count>99?15:12)*unit,fill:color,stroke:'#ffedca','stroke-width':1.5*unit}));
      const label=svgEl('text',{'text-anchor':'middle','dominant-baseline':'central','font-size':12*unit,fill:'#fff8e7'});label.textContent=String(count);badge.append(label);group.append(badge);this.svg.append(group);
    }
  }
  destroy(){this.svg.remove();}
}
