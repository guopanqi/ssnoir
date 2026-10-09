import {cast} from './cast.js';
import {Stage as SimpleStage,Sound} from '../baselines/stage.js';
export {Sound};
const defs=`<defs><filter id="surface"><feTurbulence baseFrequency=".6" numOctaves="2" seed="8" result="noise"/><feColorMatrix in="noise" type="saturate" values="0"/><feComponentTransfer><feFuncA type="linear" slope=".06"/></feComponentTransfer><feBlend in="SourceGraphic" mode="soft-light"/></filter></defs>`;
const shapes={
 streetlamp:[172,450,'0 0 180 470',`<path d="M87 118h9v340H87zM61 455h62v11H61z" fill="#313b46" stroke="#5c6875" stroke-width="3"/><path d="M91 120V54q0-28 29-28h23" fill="none" stroke="#5c6875" stroke-width="7"/><path d="M112 30h52l10 29h-72z" fill="#293441" stroke="#5c6875" stroke-width="3"/><path class="lamp-bulb" d="M108 60h60l-9 17h-42z" fill="#d7bd83" stroke="#9d8963" stroke-width="2"/>`],
 pack:[70,100,'-50 -70 100 140',`<path d="M-43-62 39-66 48-20 41 15 46 64-44 61-40 17-47-23Z" fill="#a53530" stroke="#552727" stroke-width="3"/><path d="m-43-62 82-4 6 31-88 5Z" fill="#822b29"/><path d="m-43-5 83-8 3 37-83 7Z" fill="#c4b278" stroke="#766440" stroke-width="2"/><text x="0" y="8" fill="#632a25" text-anchor="middle" font-family="Georgia,serif" font-size="13">GOLD</text><text x="0" y="21" fill="#632a25" text-anchor="middle" font-family="Georgia,serif" font-size="12">SEAL</text><path d="m-34-30 10 12M18-54v30M-25 38l17 14m36-18-15 22" stroke="#c77464" stroke-width="2" opacity=".6"/>`],
 cigarettes:null,
 letter:[82,55,'0 0 100 65',`<path d="M3 8h94v50H3z" fill="#bcb19a" stroke="#6b6e6d" stroke-width="3"/><path d="m3 8 47 30L97 8M3 58l32-29m62 29L65 29" fill="none" stroke="#73746d" stroke-width="2"/>`],
 table:[270,145,'0 0 270 145',`<path d="M3 4h264v15H3zM22 19h13v122H22zM235 19h13v122h-13z" fill="#625e54" stroke="#36414c" stroke-width="4"/>`],
 chair:[105,160,'0 0 125 190',`<path d="M13 3h99v97H13zM3 100h119v14H3zM10 113h13v74H10zM102 113h13v74h-13z" fill="#5e605b" stroke="#374451" stroke-width="4"/><path d="M25 16h75v69H25z" fill="#28343e" stroke="#414f5e" stroke-width="3"/>`],
 umbrella:[38,205,'0 0 65 240',`<path d="M18 33C18 0 52 0 52 27v194" fill="none" stroke="#a69d81" stroke-width="7"/><path d="m52 52-27 161 30 26 11-23Z" fill="#25303b" stroke="#526072" stroke-width="3"/>`],
 phone:[112,68,'0 0 140 85',`<path d="m18 38-15 42h130l-15-42Z" fill="#222d37" stroke="#5a6676" stroke-width="3"/><circle cx="70" cy="54" r="22" fill="#919080" stroke="#5a6676" stroke-width="3"/><circle cx="70" cy="54" r="10" fill="#25303b"/><path d="M18 26v-8Q70-6 122 18v8" fill="none" stroke="#566272" stroke-width="18" stroke-linecap="round"/><path d="M18 26v-8Q70-6 122 18v8" fill="none" stroke="#24303b" stroke-width="12" stroke-linecap="round"/>`]
};shapes.cigarettes=shapes.pack;
const doorway=`<svg viewBox="0 0 310 530" aria-label="公寓门"><path class="door-gap" d="M12 10h286v510H12z" fill="#c3a572"/><g class="door-leaf"><path d="M12 10h286v510H12z" fill="#1c2630" stroke="#39495a" stroke-width="6"/><path d="M28 26h254v478H28z" fill="#26313e" stroke="#151e28" stroke-width="10"/><path d="M55 54h200v189H55zM55 281h200v192H55z" fill="#222c37" stroke="#3c4d61" stroke-width="4"/><circle cx="249" cy="303" r="10" fill="#aa9160"/></g></svg>`;
const bell=`<svg viewBox="0 0 84 172" aria-label="公寓入户电铃"><rect x="3" y="3" width="78" height="166" rx="8" fill="#293442" stroke="#5c6c81" stroke-width="3"/><path d="M22 22h40M22 32h40M22 42h40" stroke="#64758c" stroke-width="3"/><g fill="#b1bfd0" font-size="12" font-family="sans-serif"><text x="11" y="78">301</text><text x="11" y="111">302</text><text x="11" y="144">303</text></g><g fill="#535653"><circle class="bell-button" cx="57" cy="74" r="10"/><circle cx="57" cy="107" r="10"/><circle cx="57" cy="140" r="10"/></g></svg>`;
export class Stage extends SimpleStage{
 constructor(root){super(root);this.curtain=root.querySelector('.curtain');this.pools=root.querySelector('.light-pools');this.transition=root.querySelector('.scene-transition');this.poolStates=new Map();this.curtainOpacity=.84;this.setCurtainOpacity(.84);}
 reset(){super.reset();this.root.classList.remove('prop-focus');this.hideTitle();this.pools.replaceChildren();this.poolStates.clear();}
 setCurtainOpacity(value){if(!Number.isFinite(value)||value<0||value>1)throw Error(`幕布透明度无效 ${value}`);this.curtainOpacity=value;this.curtain.style.opacity=String(value);}
 atmosphere(c){
  if(!/^#[0-9a-f]{6}$/i.test(c.color)||!Array.isArray(c.lights))throw Error('无效幕布或光池配置');
  this.curtain.style.backgroundColor=c.color;this.pools.replaceChildren();this.poolStates.clear();
  for(const p of c.lights){
   if(this.poolStates.has(p.id)||![p.x,p.y,p.width,p.height,p.strength].every(Number.isFinite)||p.strength<0||p.strength>1||!/^#[0-9a-f]{6}$/i.test(p.color))throw Error(`无效光池 ${p.id}`);
   const el=document.createElement('div');el.className='light-pool';el.dataset.pool=p.id;el.style.cssText=`left:${p.x}px;top:${p.y}px;width:${p.width}px;height:${p.height}px;background:radial-gradient(ellipse,${p.color} 0%,transparent 70%);opacity:${p.strength}`;
   this.pools.append(el);this.poolStates.set(p.id,{el,strength:p.strength});
  }
 }
 pool(id,strength,instant=false,duration=1000){const p=this.poolStates.get(id);if(!p||!Number.isFinite(strength)||strength<0||strength>1)throw Error(`无效光池变化 ${id}`);if(!instant)this.animate(p.el,[{opacity:p.strength},{opacity:strength}],duration);p.strength=strength;p.el.style.opacity=String(strength);}
 flicker(id,levels,duration){
  const light=this.poolStates.get(id),prop=this.props.get(id);
  if(!light||!prop||!Array.isArray(levels)||levels.length<3||levels[0]!==1||levels.at(-1)!==1||levels.some(v=>!Number.isFinite(v)||v<0||v>1))throw Error(`无效闪烁 ${id}`);
  const bulb=prop.el.querySelector('.lamp-bulb');if(!bulb)throw Error(`道具没有灯泡 ${id}`);
  this.animate(light.el,levels.map(v=>({opacity:light.strength*v})),duration);
  this.animate(bulb,levels.map(v=>({opacity:.18+.82*v})),duration);
 }
 cover(duration){const from=this.transition.hidden?0:Number(getComputedStyle(this.transition).opacity);this.transition.hidden=false;this.transition.querySelector('.title-content').style.visibility='hidden';this.transition.style.opacity='1';this.animate(this.transition,[{opacity:from},{opacity:1}],duration);}
 title(c){
  this.transition.hidden=false;this.transition.style.opacity='1';const content=this.transition.querySelector('.title-content');content.style.opacity='1';content.style.visibility='visible';
  content.querySelector('small').textContent=c.kicker;content.querySelector('h2').textContent=c.title;content.querySelector('p').textContent=c.description;
  this.animate(content,[{opacity:0,transform:'translateY(5px)'},{opacity:1,transform:'translateY(0)'}],c.fadeIn);
 }
 reveal(duration){this.animate(this.transition,[{opacity:1},{opacity:0}],duration);this.transition.style.opacity='0';}
 hideTitle(){this.transition.hidden=true;this.transition.style.opacity='1';}

 scene(name){if(!['room','rain','quiet','street','theatre'].includes(name))throw Error(`未知场景 ${name}`);this.root.dataset.scene=name;this.background.innerHTML=name==='rain'?`<div class="stage-door">${doorway}</div><div class="intercom">${bell}</div><div class="rain"></div>`:name==='theatre'?`<div class="stage-door theatre-door" data-state="closed">${doorway}</div>`:'';this.overlay.className='fx';this.sound?.rain(name==='rain'?.024:0);}
 enter(id,x,layer='middle'){
  const design=cast[id];if(!design||this.people.has(id))throw Error(`重复或未知演员 ${id}`);
  const el=document.createElement('div');el.className='actor';el.dataset.actor=id;el.dataset.layer=layer;el.style.height=`${design.height}px`;if(design.silhouette)el.classList.add('silhouette');
  const img=document.createElement('img');img.alt=id==='搭肩'?'经理搭肩，尼尔后倾且没有回应':id;img.src=design.poses.基础;el.append(img);this.actors.append(el);this.people.set(id,{el,img,x,pose:'基础',light:1,angle:0});this.place(id,x);
 }
 pose(id,pose){const a=this.get(id);const src=cast[id].poses[pose];if(!src)throw Error(`缺少姿势 ${id}/${pose}`);a.pose=pose;a.img.src=src;}
 say(c){super.say(c);this.dialogue.style.setProperty('--speaker-color',cast[c.actor]?.color||'#c5b899');}

 light(id,value){const a=this.get(id);a.light=value;a.el.style.opacity=value===0?'0':'1';a.el.style.setProperty('--light',value===0?1:value);}
 prop(id,kind,x,y){const shape=shapes[kind];if(!shape||this.props.has(id))throw Error(`重复或未知道具 ${id}/${kind}`);const[w,h,box,content]=shape;const el=document.createElement('div');el.className=`stage-prop ${kind}`;el.dataset.prop=id;el.style.cssText=`left:${x}px;top:${y}px;width:${w}px;height:${h}px`;el.innerHTML=`<svg viewBox="${box}" role="img" aria-label="${id}">${defs}<g filter="url(#surface)">${content}</g></svg>`;this.actors.append(el);this.props.set(id,{el,x,y,scale:1,home:null});}
 speaking(id,remote,inner){for(const[name,a]of this.people){a.el.classList.toggle('speaking',name===id&&!remote||name==='搭肩'&&['经理','尼尔'].includes(id));a.el.classList.toggle('listening',!inner&&name!==id&&name!=='搭肩');}this.background.querySelector('.intercom')?.classList.toggle('responding',remote);}
 tilt(id,angle,instant=false,duration=500){const a=this.get(id);if(!Number.isFinite(angle)||Math.abs(angle)>10)throw Error(`无效后倾角度 ${angle}`);if(!instant)this.animate(a.img,[{transform:`rotate(${a.angle}deg)`},{transform:`rotate(${angle}deg)`}],duration);a.angle=angle;a.img.style.transform=`rotate(${angle}deg)`;}
 inspect(id,show,instant=false,duration=800){const p=this.props.get(id);if(!p)throw Error(`道具未登场 ${id}`);if(show&&p.home||!show&&!p.home)throw Error(`重复或未开始查看 ${id}`);const from={x:p.x,y:p.y,scale:p.scale};if(show)p.home={...from};const target=show?{x:640,y:285,scale:3.1}:p.home;this.root.classList.toggle('prop-focus',show);p.el.classList.toggle('inspecting',show);if(!instant)this.animate(p.el,[{left:`${from.x}px`,top:`${from.y}px`,transform:`translate(-50%,-50%) scale(${from.scale})`},{left:`${target.x}px`,top:`${target.y}px`,transform:`translate(-50%,-50%) scale(${target.scale})`}],duration);this.propPlace(id,target.x,target.y);p.scale=target.scale;p.el.style.transform=`translate(-50%,-50%) scale(${target.scale})`;if(!show)p.home=null;}
 effect(name,actor,instant=false){if(name==='bell'||name==='unlock'){if(!instant)this.sound?.tone(name);const el=this.background.querySelector('.intercom');if(el&&!instant)this.animate(el,[{filter:'brightness(1)'},{filter:'brightness(1.8)'},{filter:'brightness(1)'}],450);if(name==='unlock')this.background.querySelector('.stage-door')?.classList.add('open');return;}super.effect(name,actor,instant);}
 door(action,instant=false,duration=650){
  const door=this.background.querySelector('.theatre-door');
  if(!door||!['open','slam'].includes(action))throw Error(`门动作无效 ${action}`);
  const leaf=door.querySelector('.door-leaf'),gap=door.querySelector('.door-gap');
  const from=door.dataset.state==='open'?'scaleX(.14)':'scaleX(1)';
  const to=action==='open'?'scaleX(.14)':'scaleX(1)';
  if(!instant)this.animate(leaf,[{transform:from},{transform:to}],action==='slam'?110:duration);
  leaf.style.transform=to;gap.style.opacity=action==='open'?'.35':'0';door.dataset.state=action==='open'?'open':'closed';
  if(action==='slam'&&!instant){this.sound?.cue('impact');this.animate(door,[{transform:'translateX(0)'},{transform:'translateX(-6px)'},{transform:'translateX(4px)'},{transform:'translateX(-2px)'},{transform:'translateX(0)'}],duration);}
 }
 snapshot(){return {...{actors:super.snapshot(),curtainOpacity:this.curtainOpacity,lights:[...this.poolStates].map(([id,p])=>({id,strength:p.strength}))},props:[...this.props].map(([id,p])=>({id,x:p.x,y:p.y,scale:p.scale})),angles:[...this.people].map(([id,a])=>({id,angle:a.angle}))};}
}
