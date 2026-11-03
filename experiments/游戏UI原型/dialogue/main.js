import { opening } from "./sample.js";
(() => {
 const game=document.querySelector('#game');
 const launch=document.querySelector('#dialogue-preview');
 const bottomLaunch=document.querySelector('#dialogue-bottom-preview');
 let activeLaunch=launch;
 let overlay=null,log=null,footer=null,cursor=0,phase='reading';
 function turn(item){
  log.querySelectorAll('.current').forEach(el=>el.classList.remove('current'));
  const entry=document.createElement('article');
  entry.className='dialogue-turn current'+(item.narration?' narration':'');
  if(!item.narration){const name=document.createElement('div');name.className='dialogue-speaker';name.textContent=item.who;entry.append(name);}
  const p=document.createElement('p');p.textContent=item.text;entry.append(p);log.append(entry);
  overlay.querySelectorAll('.dialogue-figure').forEach(el=>el.classList.toggle('active',el.dataset.speaker===item.who));
  log.scrollTop=log.scrollHeight;
 }
 function advance(){
  if(phase!=='reading')return;
  if(cursor<opening.length){turn(opening[cursor++]);if(cursor===opening.length)choices();}
 }
 function choices(){
  phase='choice';footer.replaceChildren();
  const list=document.createElement('div');list.className='dialogue-choices';list.setAttribute('aria-label','选择回应');
  for(const text of ['问她今晚为什么留下来。','不追问，和她一起离开。']){
   const button=document.createElement('button');button.textContent=text;
   button.onclick=()=>{
    turn({who:'尼尔',text:text.startsWith('问')?'你呢？今晚为什么还没走？':'走吧。我送你到街口。'});
    turn({who:'夜莺',text:text.startsWith('问')?'外面还在下雨。我想等雨小一点。':'好。但别又在街口站到天亮。'});
    phase='end';footer.innerHTML='<button class="dialogue-next" data-end><span>结束这段对话</span><b>返回场景</b></button>';
    footer.querySelector('[data-end]').onclick=close;
    footer.querySelector('[data-end]').focus({preventScroll:true});
    const rule=document.createElement('div');rule.className='dialogue-end-rule';log.append(rule);log.scrollTop=log.scrollHeight;
   };list.append(button);
  }footer.append(list);list.querySelector('button').focus({preventScroll:true});
 }
 function selectLayout(layout){
  overlay.classList.toggle('dialogue-bottom',layout==='bottom');
  launch.setAttribute('aria-pressed',String(layout==='side'));bottomLaunch.setAttribute('aria-pressed',String(layout==='bottom'));
  log.scrollTop=log.scrollHeight;
 }
 function close(){
  document.body.classList.remove('dialogue-mode');
  document.querySelector('#action-tab').setAttribute('aria-pressed','true');document.querySelector('#dialogue-tab').setAttribute('aria-pressed','false');
  if(!overlay)return;
  launch.setAttribute('aria-pressed','false');bottomLaunch.setAttribute('aria-pressed','false');
  overlay.remove();overlay=null;game.classList.remove('dialogue-open');
  for(const el of game.children)el.inert=false;
  document.querySelector('#action-tab').focus({preventScroll:true});
 }
 function open(layout){
  if(overlay){selectLayout(layout);return;}
  document.body.classList.add('dialogue-mode');
  document.querySelector('#action-tab').setAttribute('aria-pressed','false');document.querySelector('#dialogue-tab').setAttribute('aria-pressed','true');
  document.querySelector('#reset').click();
  game.classList.add('dialogue-open');
  for(const el of game.children)el.inert=true;
  overlay=document.createElement('section');overlay.className='dialogue-preview'+(layout==='bottom'?' dialogue-bottom':'');overlay.setAttribute('role','region');overlay.setAttribute('aria-label','夜莺与尼尔的对话');
  overlay.innerHTML='<div class="dialogue-stage"><div class="dialogue-location">老街酒馆 / 散场以后</div><div class="dialogue-figure neil" data-speaker="尼尔" aria-hidden="true"></div><div class="dialogue-figure nightingale" data-speaker="夜莺" aria-hidden="true"></div></div><section class="dialogue-panel"><header class="dialogue-panel-top"><span>散场以后</span></header><div class="dialogue-log" role="log" aria-live="polite" aria-relevant="additions" aria-label="对话记录" tabindex="0"></div><footer class="dialogue-footer"><button class="dialogue-next" data-next><span>继续对话</span><small>空格 / 点击继续</small><b>继续</b></button></footer></section>';
  game.append(overlay);log=overlay.querySelector('.dialogue-log');footer=overlay.querySelector('.dialogue-footer');cursor=0;phase='reading';
  selectLayout(layout);
  overlay.querySelector('[data-next]').onclick=advance;
  for(let i=0;i<3;i++)advance();

 }
 launch.onclick=()=>{activeLaunch=launch;open('side');};
 bottomLaunch.onclick=()=>{activeLaunch=bottomLaunch;open('bottom');};
 document.querySelector('#action-tab').onclick=close;
 document.querySelector('#dialogue-tab').onclick=()=>{activeLaunch=bottomLaunch;open('bottom');};
 if(new URLSearchParams(location.search).get('experiment')==='dialogue')open('bottom');
 document.addEventListener('keydown',event=>{
  if(!overlay)return;
  if(event.key==='Escape'){event.preventDefault();close();return;}
  if(event.code==='Space'&&!event.target.closest('button,input,select,textarea')){event.preventDefault();advance();}

 });
})();
