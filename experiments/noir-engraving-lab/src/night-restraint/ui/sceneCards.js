import * as THREE from 'three';
import './sceneCards.css';

// 场景信息样本：沿用建筑锚点；仅展示和选择，不结算游戏状态。
const VENUES={
 '老街酒馆':{actions:[['打听司机','夜班司机常坐在靠门的桌边。','交际'],['翻看账本','酒保把赊账记在柜台下面。','观察']],notes:[['夜班司机','帽子还没摘。他的卡车停在后门。'],['后门','门没有锁。有人刚搬走了几箱酒。']]},
 '码头':{actions:[['搬运','扛一班货，挣一晚的钱。','力量'],['查货单','核对今晚出港的货物。','观察']],notes:[['巡警多了','两个巡警沿泊位来回走。卸货的人不再说话。'],['艾迪','右手缠着绷带。他一直看着货堆。']]},
 '警察局':{actions:[['问值班警员','问问昨夜被扣下的卡车。','交际'],['查登记簿','找出入记录里的空白。','观察']],notes:[['值班窗口','有人把一页登记表夹进了抽屉。'],['侧门','夜班的警员从这里交接。']]},
 '剧院':{actions:[['找夜莺','向后台的人问她的去向。','交际'],['查演出单','找到她最后一次登台的日期。','观察']],notes:[['后台','门口的花还没收走。今晚换了领唱。'],['海报','夜莺的名字被一张新海报盖住了。']]},
 '报社':{actions:[['问编辑','打听没登出来的那篇报道。','交际'],['翻旧报','在旧新闻里找同一个名字。','观察']],notes:[['印刷间','机器还在转。明天的头版已经排好。'],['夜班记者','他的桌上只有一杯冷咖啡。']]},
 '格兰德酒店':{actions:[['问门房','查昨晚到店的客人。','交际'],['查访客簿','找一笔被涂掉的签名。','观察']],notes:[['门房','他记得车牌，却不肯说客人的名字。'],['侧入口','送货员绕过了正门。']]},
 '公园':{actions:[['等接头人','长椅上留着一个空位。','观察'],['询问报童','问他看见了谁。','交际']],notes:[['长椅','有一张折好的报纸，日期是昨天。'],['路口','报童卖完了早报，还没有走。']]},
 '货运公司':{actions:[['查车次','核对昨夜出车的时间。','观察'],['问调度员','找出临时换班的司机。','交际']],notes:[['车库','一辆卡车的车牌刚换过。'],['调度室','灯还亮着，窗帘却已经拉上。']]},
};
const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));

export function mountSceneCards(host,rigs){
 const layer=document.createElement('section');layer.className='scene-annotations';layer.hidden=true;layer.setAttribute('aria-label','地点动作与现场标注');host.append(layer);
 const ns='http://www.w3.org/2000/svg';
 const svg=document.createElementNS(ns,'svg');svg.classList.add('scene-leaders');layer.append(svg);
 const actions=document.createElement('div');actions.className='scene-actions';layer.append(actions);
 const notes=[0,1].map(()=>{const note=document.createElement('article');note.className='scene-note';layer.append(note);return note;});
 const lines=[0,1,2].map(()=>{const group=document.createElementNS(ns,'g');const path=document.createElementNS(ns,'path');const dot=document.createElementNS(ns,'circle');dot.setAttribute('r','3');group.append(path,dot);svg.append(group);return {group,path,dot};});
 let rig=null,width=1280;
 const point=new THREE.Vector3();
 function leader(index,x,y,edgeX,edgeY){
  const {group,path,dot}=lines[index];group.style.display='';
  const elbow=edgeX+(edgeX>x?-18:18);
  path.setAttribute('d',`M ${x} ${y} L ${elbow} ${y} L ${elbow} ${edgeY} L ${edgeX} ${edgeY}`);
  dot.setAttribute('cx',x);dot.setAttribute('cy',y);
 }
 function project(camera,dx=0,dz=0){
  point.copy(rig.topCenter);point.y=Math.max(4,rig.top*.45);point.x+=dx;point.z+=dz;point.project(camera);
  return {x:(point.x+1)*width/2,y:(1-point.y)*360,visible:point.z>-1&&point.z<1&&Math.abs(point.x)<1.12&&Math.abs(point.y)<1.1};
 }
 return {
  resize(w){width=w;svg.setAttribute('viewBox',`0 0 ${width} 720`);},
  setView(mode,name){
   layer.hidden=mode!=='focused';if(mode!=='focused')return;
   rig=rigs.find(r=>r.name===name);if(!rig||!VENUES[name])throw new Error(`缺少地点 UI 样本：${name}`);
   const data=VENUES[name];actions.replaceChildren();
   data.actions.forEach(([title,description,skill],i)=>{
    const card=document.createElement('article');card.className=`scene-action${i?' scene-action-secondary':''}`;
    const type=document.createElement('span');type.className='scene-action-kind';type.textContent=i?'调查':'行动';
    const heading=document.createElement('h2');heading.textContent=title;
    const text=document.createElement('p');text.textContent=description;
    const footer=document.createElement('div');footer.className='scene-action-footer';
    const ability=document.createElement('span');ability.textContent=skill;
    const slot=document.createElement('button');slot.className='scene-die-slot';slot.title='选择行动槽';slot.setAttribute('aria-label',`${title} · 行动骰槽`);slot.setAttribute('aria-pressed','false');slot.innerHTML='<span class="slot-cross">+</span><span>行动骰</span>';
    slot.addEventListener('click',()=>{const selected=slot.getAttribute('aria-pressed')!=='true';actions.querySelectorAll('.scene-die-slot').forEach(b=>b.setAttribute('aria-pressed','false'));slot.setAttribute('aria-pressed',String(selected));});
    footer.append(ability,slot);card.append(type,heading,text,footer);actions.append(card);
   });
   notes.forEach((note,i)=>{note.replaceChildren();const heading=document.createElement('h3');heading.textContent=data.notes[i][0];const text=document.createElement('p');text.textContent=data.notes[i][1];note.append(heading,text);});
  },
  tick(camera){
   if(layer.hidden||!rig)return;
   const anchor=project(camera);layer.classList.toggle('offscreen',!anchor.visible);if(!anchor.visible)return;
   // 固定拓扑：动作在左，标注在右；只做边界约束，不逐帧换边或互相推挤。
   const left=clamp(anchor.x-420,24,width-550),top=clamp(anchor.y-205,128,172);
   actions.style.transform=`translate(${left}px,${top}px)`;
   leader(0,anchor.x,anchor.y,left+216,top+48);
   const right=clamp(Math.max(anchor.x+230,left+252),24,width-256);
   notes.forEach((note,i)=>{
    const a=project(camera,i?14:-10,i?-14:10);
    const y=clamp(anchor.y-115+i*145,128+i*120,365+i*55);
    note.style.transform=`translate(${right}px,${y}px)`;
    leader(i+1,a.x,a.y,right,y+18);
   });
  }
 };
}
