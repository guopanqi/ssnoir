import {scenery} from './scenery.js';
import {story,performanceAt,starts} from './story.js';
import {castStyles,sprite} from './cast.js?v=original-cast-1';
import {StoryPlayer} from './player.js';
import {StageSound} from './sound.js';
import {defaultInk,flatTreatments,mountInkEditor,applyInk} from './editor.js?v=flat-study-1';
const $=s=>document.querySelector(s);
const state={scene:'lamp',bg:'absent',person:'raw',style:'signet1',acting:'neutral',blur:28,opacity:24,lines:65,comparison:null,speed:1,ink:{...defaultInk},mask:true,reaction:true};
const modes={absent:{blur:28,opacity:0,title:'完全退场'},fog:{blur:28,opacity:24,title:'重度失焦'},trace:{blur:5,opacity:30,title:'3D 轮廓残影'}};
const flatOptions=['neonEdge','curlCoat'].flatMap(style=>flatTreatments.map(t=>({id:style+'-'+t.id,style,treatment:t,title:(style==='neonEdge'?'N2':'C1')+' · '+t.name})));
const sound=new StageSound();
let svgSequence=0,currentFrame={index:0,position:0,total:0,playing:false},lastCue=-1,lastPlaying=false,pausedMotion=[];
function syncStage(stage,bg=state.bg,style=state.style,preset=false){
 const cue=story[currentFrame.index],acting=performanceAt(currentFrame.index,currentFrame.elapsed??0);
 stage.dataset.person=state.person;stage.dataset.style=style;
 const mode=preset?modes[bg]:{blur:state.blur,opacity:bg==='absent'?0:state.opacity};
 stage.style.setProperty('--blur',`${mode.blur/16}cqw`);stage.style.setProperty('--world',mode.opacity/100);stage.style.setProperty('--line-alpha',state.lines/100);stage.style.setProperty('--lamp',acting.lamp);stage.style.setProperty('--move-ms',`${acting.moveMs/state.speed}ms`);
 const key=`${state.scene}`;
 if(stage.dataset.scenery!==key){const prefix=stage.dataset.svgPrefix??`s${++svgSequence}`;stage.dataset.svgPrefix=prefix;stage.dataset.scenery=key;stage.querySelector('.set').innerHTML=scenery(state.scene).replace(/id="([^"]+)"/g,(_,id)=>`id="${prefix}-${id}"`).replace(/url\(#([^)]*)\)/g,(_,id)=>`url(#${prefix}-${id})`)}
 stage.querySelector('.scene-label').textContent=(state.scene==='warehouse'?'码头 · 仓库檐下':'老街之外 · 路灯下')+' / '+cue.label;
 for(const actor of ['neil','night']){
  const el=stage.querySelector('.'+actor),pose=state.acting==='neutral'?'neutral':acting[actor+'Pose'],url=sprite(actor==='neil'&&!castStyles.find(s=>s.id===style).both?'curlCoat':style,actor,pose);
  if(!el.querySelector('img').getAttribute('src')||el.querySelector('img').getAttribute('src')!==url)el.querySelector('img').src=url;
  const x=acting[actor+'X'];el.style.left=`${x-8.5}%`;el.style.setProperty('--move-ms',`${acting.moveMs/state.speed}ms`);el.style.setProperty('--actor-url',`url("${url}")`);
  const flip=actor==='night'?((style==='original'||style==='originalNeon')!==(acting.nightFacing==='right')):false;
  el.style.setProperty('--flip',flip?-1:1);
  stage.querySelector('.'+actor+'-shadow').style.transform=`translate(${x*16}px,0)`;
  el.dataset.speaking=String(cue.speaker===(actor==='neil'?'尼尔':'夜莺'));
 }
 applyInk(stage,inkFor(stage));
 stage.querySelector('.caption b').textContent=cue.speaker;stage.querySelector('.caption span').textContent=cue.text;
}
function render(){
 syncStage($('#stage'));
 for(const key of ['scene','bg','person','style','acting'])document.querySelectorAll(`button[data-${key}]`).forEach(button=>button.setAttribute('aria-pressed',String(button.dataset[key]===state[key])));
 for(const key of ['blur','opacity','lines']){$('#'+key).value=state[key];$('#'+key+'-value').textContent=state[key]+(key==='blur'?'':'%')}
 $('#blur').disabled=$('#opacity').disabled=state.bg==='absent';
 $('#note').textContent=(castStyles.find(s=>s.id===state.style).both?'两人同时使用原版。':'尼尔固定为 C1，仅替换夜莺。')+castStyles.find(s=>s.id===state.style).note;
 $('#cast-compare').setAttribute('aria-pressed',String(state.comparison==='cast'));$('#bg-compare').setAttribute('aria-pressed',String(state.comparison==='bg'));
 $('#flat-compare').setAttribute('aria-pressed',String(state.comparison==='flat'));
 $('#comparison').hidden=!state.comparison;
 if(state.comparison){
  $('#comparison-note').textContent=state.comparison==='flat'?'上排 N2，下排 C1；每排依次为原图、限色与三档色阶、加细描边。仅处理夜莺，尼尔、布景和拍点相同；固定预设不随主画面编辑器变化。':state.comparison==='cast'?'原版两栏同时替换两人；其余栏尼尔固定 C1。所有栏同一拍点比较。新画稿保持夜莺礼服身份与基本站姿，C1 保留外套基线。原画栏不加额外线，C1 描线栏使用固定推荐值；编辑器调整主画面。':'人物和布景完全一致，远景使用三个固定预设。';
  const grid=$('#comparison-grid');
  if(grid.dataset.mode!==state.comparison){grid.replaceChildren();grid.dataset.mode=state.comparison;const options=state.comparison==='flat'?flatOptions:state.comparison==='cast'?[...castStyles.map(s=>({id:s.id,title:s.name})),{id:'curlCoatEdge',title:'C1 · 后处理对照'}]:Object.entries(modes).map(([id,m])=>({id,title:m.title}));for(const option of options){const card=document.createElement('article');card.dataset.option=option.id;const title=document.createElement('h3');title.textContent=option.title;const clone=$('#stage').cloneNode(true);clone.removeAttribute('id');delete clone.dataset.svgPrefix;delete clone.dataset.scenery;clone.classList.add('comparison-stage');card.append(title,clone);grid.append(card)}}
  for(const card of grid.children)syncStage(card.querySelector('.stage'),state.comparison==='bg'?card.dataset.option:state.bg,state.comparison==='flat'?flatOptions.find(o=>o.id===card.dataset.option).style:state.comparison==='cast'?(card.dataset.option==='curlCoatEdge'?'curlCoat':card.dataset.option):state.style,state.comparison==='bg');
 }
 syncPerformance();
}
function time(ms){const seconds=Math.floor(ms/1000);return `${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`}
function onFrame(frame){if(frame.playing!==lastPlaying){if(frame.playing){pausedMotion.forEach(a=>a.play());pausedMotion=[]}else{pausedMotion=[...document.querySelectorAll('.actor,.neil-shadow,.night-shadow')].flatMap(el=>el.getAnimations()).filter(a=>a.playState==='running');pausedMotion.forEach(a=>a.pause())}lastPlaying=frame.playing}currentFrame=frame;$('#play').textContent=frame.playing?'暂停演出':frame.position>=frame.total&&frame.total?'故事结束 · 再播放':'播放完整故事';$('#progress').value=frame.total?frame.position/frame.total*100:0;$('#time').textContent=`${time(frame.position)} / ${time(frame.total)}`;$('#cue-label').textContent=`${frame.index+1}/${story.length} · ${story[frame.index].label}`;if(frame.index!==lastCue){lastCue=frame.index;render()}syncPerformance();sound.sync(frame)}
for(const style of castStyles){const button=document.createElement('button');button.dataset.style=style.id;const thumb=document.createElement('img');thumb.className='cast-thumb';thumb.src=sprite(style.id,'night','neutral');thumb.alt='';const name=document.createElement('span');name.textContent=style.name;button.append(thumb,name);$('#cast-options').append(button)}
const player=new StoryPlayer(story,onFrame);
for(const key of ['scene','bg','person','style','acting'])document.querySelectorAll(`button[data-${key}]`).forEach(button=>button.addEventListener('click',()=>{state[key]=button.dataset[key];if(key==='bg'){state.blur=modes[state.bg].blur;state.opacity=modes[state.bg].opacity}render()}));
for(const key of ['blur','opacity','lines'])$('#'+key).addEventListener('input',e=>{state[key]=Number(e.target.value);render()});
$('#play').addEventListener('click',()=>{sound.unlock(player.position);player.toggle()});$('#restart').addEventListener('click',()=>{sound.unlock(0);player.restart()});$('#prev').addEventListener('click',()=>{sound.seek(starts[Math.max(0,player.index-1)]);player.step(-1)});$('#next').addEventListener('click',()=>{sound.seek(starts[Math.min(story.length-1,player.index+1)]);player.step(1)});$('#speed').addEventListener('change',e=>{state.speed=player.speed=Number(e.target.value);render()});$('#progress').addEventListener('input',e=>{const position=Number(e.target.value)/100*player.total;sound.seek(position);player.seek(position)});
for(const [id,mode] of [['flat-compare','flat'],['cast-compare','cast'],['bg-compare','bg']])$('#'+id).addEventListener('click',()=>{state.comparison=state.comparison===mode?null:mode;render()});
const inkEditor=mountInkEditor(ink=>{state.ink=ink;render()},()=>({style:state.style,scene:state.scene,background:state.bg,blur:state.blur,opacity:state.opacity,lines:state.lines,cue:currentFrame.index}));
new ResizeObserver(()=>document.querySelectorAll('.stage').forEach(stage=>applyInk(stage,inkFor(stage)))).observe($('#stage'));
// Warm the image cache so style changes do not interrupt dialogue.
for(const style of castStyles)for(const [actor,poses] of [['neil',['neutral','question']],['night',['neutral','vulnerable']]])for(const pose of poses){const image=new Image();image.src=sprite(style.id,actor,pose)}
render();

function syncPerformance(){
 const cue=story[currentFrame.index],elapsed=currentFrame.elapsed??0,a=performanceAt(currentFrame.index,elapsed);
 for(const stage of document.querySelectorAll('.stage')){
  stage.style.setProperty('--lamp',a.lamp);stage.style.setProperty('--halo',a.halo);stage.style.setProperty('--react',state.reaction&&a.quiet?.38:1);stage.dataset.mask=String(state.mask);
  stage.style.setProperty('--focus',`${50+((cue.speaker==='尼尔'?a.neilX:cue.speaker==='夜莺'?a.nightX:50)-50)*.5}%`);
  stage.style.setProperty('--grain-x',`${Math.floor(currentFrame.position/125)%4*.6}%`);
  for(const actor of ['neil','night']){const el=stage.querySelector('.'+actor),x=a[actor+'X'];el.style.left=`${x-8.5}%`;el.style.setProperty('--bob',`${a[actor+'Walking']?Math.sin(currentFrame.position/420*Math.PI)*.5:0}cqw`);stage.querySelector('.'+actor+'-shadow').style.transform=`translate(${x*16}px,0)`}
  stage.querySelector('.caption span').textContent=cue.speaker?[...cue.text].slice(0,Math.floor(elapsed/48)+1).join(''):cue.text;
  stage.querySelector('.caption b').style.color=cue.speaker==='夜莺'?'#8fd9d0':'#f0cf8a';
  stage.querySelector('.speech-glow').style.left=`${(cue.speaker==='尼尔'?a.neilX:a.nightX)-14}%`;stage.querySelector('.speech-glow').style.opacity=cue.speaker?'.13':'0';
  
 }
}
$('#sound').addEventListener('click',()=>{$('#sound').textContent=sound.toggle()?'声音 开':'声音 关';sound.unlock(player.position)});
for(const [id,key] of [['mask','mask'],['reaction','reaction']])$('#'+id).addEventListener('click',()=>{state[key]=!state[key];$('#'+id).textContent=(key==='mask'?'光孔遮罩 ':'背景反应 ')+(state[key]?'开':'关');syncPerformance()});
$('#style-treatment').addEventListener('click',()=>inkEditor.set({...defaultInk,enabled:state.style==='curlCoat',target:'night',detail:100,width:1.5}));
syncPerformance();

function inkFor(stage){const option=stage.closest('article')?.dataset.option;if(state.comparison==='flat'&&option)return flatOptions.find(o=>o.id===option).treatment.value;if(state.comparison==='cast'&&option)return option==='curlCoatEdge'?{...defaultInk,enabled:true,target:'night',detail:100,width:1.5}:defaultInk;return state.ink}
