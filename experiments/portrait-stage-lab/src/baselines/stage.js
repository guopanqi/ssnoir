import {Stage as BaseStage,Sound} from '../stage.js';
import {cast as originalCast} from '../stories.js';
export {Sound};
export const cast={...originalCast,搭肩:{height:430,poses:{基础:'/assets/baselines/contact.png'}}};
const shapes={
 bell:['0 0 65 125',65,125,`<rect x="2" y="2" width="61" height="121" rx="3" fill="#757b72"/><path stroke="#283638" stroke-width="4" d="M12 15h41m-41 9h41m-41 9h41m-41 9h41"/><rect x="10" y="55" width="45" height="18" fill="#cac4ab"/><text x="32" y="68" text-anchor="middle" fill="#293137" font-size="12">301</text><circle cx="32" cy="96" r="10" fill="#c1baa0"/>`],
 table:['0 0 270 145',270,145,`<path fill="#81705c" d="M0 4h270v15H0zM22 19h12v126H22zM237 19h12v126h-12z"/>`],
 chair:['0 0 125 190',105,160,`<path fill="#786754" d="M13 0h99v100H13zM0 100h125v14H0zM10 113h12v77H10zM103 113h12v77h-12z"/><path fill="#243036" d="M24 13h77v70H24z"/>`],
 umbrella:['0 0 65 240',38,205,`<path fill="none" stroke="#b4a385" stroke-width="9" d="M18 33C18 0 52 0 52 27v194"/><path fill="#27313a" stroke="#81705c" stroke-width="3" d="m52 52-27 161 30 26 11-23z"/>`],
 phone:['0 0 140 85',112,68,`<path fill="#141b1d" stroke="#9d9580" stroke-width="2" d="m18 38-15 42h130l-15-42z"/><circle cx="70" cy="54" r="23" fill="#8e8877"/><circle cx="70" cy="54" r="10" fill="#252e30"/><path fill="none" stroke="#252d30" stroke-width="16" stroke-linecap="round" d="M18 26v-8Q70-6 122 18v8"/><path fill="none" stroke="#8e8877" stroke-width="2" d="M121 27q22 6 10 20q22 10 4 18"/>`],
 pack:['0 0 100 65',78,51,`<path fill="#953f34" d="m4 10 79-6 12 13-1 42-75 3z"/><path fill="#c9b784" d="m14 22 59-5v22l-56 4z"/><path stroke="#765c3a" stroke-width="3" d="m23 28 41-5m-39 12 36-5"/><path fill="#dfcfab" d="m4 10 79-6 7 8-73 7z"/>`],
 letter:['0 0 100 65',84,55,`<path fill="#c9ba97" d="M3 8h94v50H3z"/><path fill="none" stroke="#8d7c62" stroke-width="2" d="m3 8 47 30L97 8M3 58l32-29m62 29L65 29"/>`]
};
export class Stage extends BaseStage{
 scene(name){if(!['rain','quiet'].includes(name))throw Error(`未知场景 ${name}`);this.root.dataset.scene=name;this.background.replaceChildren();this.overlay.className='fx';this.sound?.rain(name==='rain'?.014:0);}
 enter(id,x,layer='middle'){
  if(this.people.has(id)||!cast[id])throw Error(`重复或未知演员 ${id}`);
  const el=document.createElement('div');el.className='actor';el.dataset.actor=id;el.dataset.layer=layer;el.style.height=`${cast[id].height}px`;
  const img=document.createElement('img');img.alt=id==='搭肩'?'经理搭着尼尔的肩，尼尔没有回应':id;img.src=cast[id].poses.基础;el.append(img);this.actors.append(el);this.people.set(id,{el,img,x,pose:'基础',light:1});this.place(id,x);
 }
 pose(id,pose){const a=this.get(id);if(!cast[id].poses[pose])throw Error(`缺少姿势 ${id}/${pose}`);a.pose=pose;a.img.src=cast[id].poses[pose];}
 light(id,value){const a=this.get(id);a.light=value;a.el.style.opacity=value===0?'0':'1';}
 prop(id,kind,x,y){if(this.props.has(id)||!shapes[kind])throw Error(`重复或未知道具 ${id}/${kind}`);const [box,w,h,shape]=shapes[kind];const el=document.createElement('div');el.className=`stage-prop ${kind}`;el.dataset.prop=id;el.style.cssText=`left:${x}px;top:${y}px;width:${w}px;height:${h}px`;el.innerHTML=`<svg viewBox="${box}" role="img" aria-label="${id}">${shape}</svg>`;this.actors.append(el);this.props.set(id,{el,x,y});}
 speaking(id,remote,inner){for(const[name,a]of this.people){a.el.classList.toggle('speaking',name===id||name==='搭肩'&&['经理','尼尔'].includes(id));}this.props.get('电铃')?.el.classList.toggle('responding',remote);}
 effect(name,actor,instant=false){if(name==='bell'||name==='unlock'){if(!instant)this.sound?.tone(name);const el=this.props.get('电铃')?.el;if(el){el.classList.toggle('unlocked',name==='unlock');if(!instant)this.animate(el,[{filter:'brightness(1)'},{filter:'brightness(1.6)'},{filter:'brightness(1)'}],500);}return;}super.effect(name,actor,instant);}
}
