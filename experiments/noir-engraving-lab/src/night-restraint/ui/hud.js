import {mountHUD as mountReferenceHUD} from './referenceHUD.js';
import './hud.css';
export function mountHUD({onBack,onFocus,landmarks}){
 const frame=document.createElement('div');frame.className='night-hud-frame';
 const host=document.createElement('div');host.id='hud';frame.append(host);document.body.append(frame);
 mountReferenceHUD(host,{onBack,onMenu});
 const popup=document.createElement('aside');popup.className='hud-popup';popup.hidden=true;host.append(popup);
 let mode='world';
 function onMenu(key){
  const tuning=document.querySelector('.tuning');
  if(key==='设'){tuning.open=!tuning.open;popup.hidden=true;return;}
  tuning.open=false;
  if(!popup.hidden&&popup.dataset.menu===key){popup.hidden=true;return;}
  popup.dataset.menu=key;popup.replaceChildren();popup.hidden=false;
  if(key==='档'){const p=document.createElement('p');p.textContent='码头疑云：跟踪夜间运出的货物，查明夜莺的下落。';popup.append(p);return;}
  for(const rig of landmarks){const b=document.createElement('button');b.textContent=rig.name;b.disabled=mode!=='world';b.addEventListener('click',()=>{popup.hidden=true;onFocus(rig);});popup.append(b);}
 }
 return {
 resize(rect){const scale=rect.height/720;Object.assign(frame.style,{left:`${rect.left}px`,top:`${rect.top}px`,width:`${rect.width}px`,height:`${rect.height}px`});host.style.width=`${rect.width/scale}px`;host.style.transform=`scale(${scale})`;},
 setView(next,name){mode=next;ui.setView(next,name);popup.hidden=true;}
 };
}
