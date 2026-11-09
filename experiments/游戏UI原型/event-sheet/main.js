const $ = id => document.getElementById(id);
// 插画是本次排版实验的可替换墨线示意，不作为正式角色画稿。
const frame = body => `<svg viewBox="0 0 620 240" role="img" aria-label="纸上的墨线插画"><defs><pattern id="hatch" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(28)"><path d="M0 0V7" stroke="currentColor" stroke-width=".6" opacity=".35"/></pattern></defs><g fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">${body}</g></svg>`;
const rent = frame(`<path d="M42 207H580"/><path d="m127 34 277 9-7 155-277-9Z" fill="url(#hatch)"/><path d="m136 23 277 9-7 155-277-9Z" fill="#ece2c5"/><path d="m157 48 227 7M155 65l114 4m-115 20 224 7m-225 17 220 7m-220 17 105 4"/><text x="280" y="144" fill="currentColor" stroke="none" font-size="36" font-family="Georgia">$40</text><path d="m440 202 18-49 47-19 40 8 26 40-11 20M451 168l25 9 30-18m-32 17-11 25M515 150l20 31" fill="url(#hatch)"/><ellipse cx="78" cy="178" rx="23" ry="8"/><path d="M66 173v-63h23v63M64 111h27l-6-17H70Z"/><path d="m424 66 35-9 9 36-35 9Zm17 4 12-3m-10 11 12-3"/>`);
const news = frame(`<path d="M24 211H597M31 215H586M64 207V112h72v95M75 124h12v23H75Zm29 0h19v23h-19Zm-29 39h12v23H75Zm29 0h19v23h-19ZM149 207V84h99v123M149 84l49-37 50 37M165 104h14v23h-14Zm37 0h27v23h-27Zm-37 43h14v23h-14Zm37 0h27v23h-27ZM264 207V135h82v72M278 148h19v29h-19Zm30 0h24v29h-24ZM363 207V59h96v148M352 59h118M378 77h19v28h-19Zm33 0h29v28h-29Zm-33 49h19v28h-19Zm33 0h29v28h-29ZM477 207V102h75v105M488 116h16v27h-16Zm27 0h23v27h-23ZM487 163h48M393 206v-32h30v32M86 105V84m7 0H79M515 96V73m-9 0h20" fill="url(#hatch)"/><path d="M56 46q15-9 28 0m228-11q12-9 24 0m187 7q14-9 26 0" opacity=".5"/>`);
// 每个 beat 同时拥有一格图与一句话；效果属于整件事，不属于某一格。
const vignette = (label, drawing) => `<svg viewBox="0 0 200 220" role="img" aria-label="${label}"><g fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${drawing}</g></svg>`;
const dinner = vignette('两人一起吃晚饭', `<path d="M12 157h176M21 163h157M37 164l-5 44m130-44 7 44M17 29h36v62H17m18-62v62M143 25h38v66h-38m18-66v66" opacity=".5"/><path d="M31 146q-8-45 14-57l25 2 17 54M42 86l1-15m18 14-1-13M117 146q0-48 23-57l24 2q20 16 17 55"/><ellipse cx="51" cy="58" rx="18" ry="25"/><path d="M31 59q-5-34 24-31 27 8 14 43M34 44l28-2M46 60h2m12-1h2m-15 13 11 1M135 47q-9-18 15-21 30 1 27 38l-8 29-11-4M135 46q-8 31 12 35 18 0 20-28M142 56h2m14-1h2m-18 16 11 1M136 104l-22 32-27 5M160 111l-9 32-29 1M43 106l11 30 28 4"/><ellipse cx="75" cy="151" rx="22" ry="5"/><ellipse cx="136" cy="151" rx="23" ry="5"/><path d="m91 119 4 24h13l4-24Zm-38 25-8-12m2 13-8-11"/>`);
const notice = vignette('夜莺注意到袖口的血', `<path d="M17 187h166M118 34q-16-23-38-2-15 33 5 53 16 10 29-8l10-29M74 41q12-5 26-12 16 9 20 37l14 35-21 7M84 56h3m17-1h3m-16 17h13M86 88l-20 24-16 51m54-72 22 25-17 40M73 107l17 23 30 18M113 104l-9 22-21 19M19 153l56-8 42 14-8 25-52-6-36 4"/><path d="m62 151 14 22m1-23 12 24m-10-15 7 5" stroke="#854636" stroke-width="2.8"/><path d="m149 83 10-12m-5 26h16" opacity=".6"/>`);
const bag = vignette('打开小包，取出纱布', `<path d="M18 183h164M42 174l6-65h111l8 65Z M48 109l19-24h73l19 24M74 85V67q27-19 53 0v18M83 83V70q18-11 35 0v13M48 118l52 29 59-29M93 140h16v14H93Z"/><path d="M119 31l-14 30-15 20 9 11 24-19 18-35M105 62l15 9M66 24l12 40 16 15-9 12-24-22-13-38"/><path d="M132 166v-35h18v35m-15-35v-10h12v10M135 147h12M141 141v12M55 156q15-11 29 0v16H55Zm0 7h29"/><path d="m16 193 57-9 34 8 64-4m-151 20 52-9 35 8 65-5M77 185l-3 15m13-14-3 17m14-15-3 17M112 162l-12 25 10 12 14-25"/>`);
const joke = vignette('夜莺讲了个笑话', `<path d="M16 187h169M43 185v-68l34-21 45 11 25 78M80 95l-4-18m28 20 2-19"/><ellipse cx="91" cy="55" rx="24" ry="31"/><path d="M65 49q-7-33 28-32 33 7 22 41M65 40l38-7M77 56l8-3m13 0 8 3M82 69q10 12 21-1M45 132l-10 28 31 12M122 130l21 32-31 9M24 60l-7-7m8 24H13M145 52l10-8m-7 25h13"/><path d="M58 174h66m-40 0 3-23h17l5 23"/>`);
const allCare = [
 {image:'<img src="event-sheet/art/晚饭.svg" alt="共进晚饭">', text:'今晚，夜莺和你一起吃晚饭。'},
 {image:'<img src="event-sheet/art/伤口.svg" alt="发现伤口">', text:'她放下叉子，注意到你的袖口还在渗血。'},
 {image:'<img src="event-sheet/art/包扎.svg" alt="替你包扎">', text:'她打开随身的小包，取出纱布，替你重新包好了伤口。'}
];
const samples = {
 care:{beats:allCare,result:['伤势 −1'],button:'收起',note:'三幅画，讲完一件小事'},
 first:{beats:allCare,result:['伤势 −1'],button:'继续',note:'首次包扎之后，接两句关切的对白',dialogue:[['夜莺','别动。你把扣子解开，我看看。'],['夜莺','下次早点告诉我。别等到吃饭的时候。']]},
 comfort:{beats:[{image:dinner,text:'晚饭摆在面前，你却半天没有动叉子。'},{image:notice,text:'夜莺看了看你，讲起今天彩排时闹的一个笑话。'},{image:joke,text:'你终于笑出声。她也笑了，把盘子往你面前推了推。'}],result:['冷静 +1'],button:'收起',note:'相同的三拍节奏，也可以讲一个笑话'},
 rent:{beats:[{image:rent,text:'房东敲了敲门。又到了交房租的日子。'},{image:rent,text:'他数过钞票，在账本上划了一笔。'},{image:news,text:'你收好钥匙，走回楼上那间属于你的房间。'}],result:['金钱 −40','房租已付'],button:'收好',note:'熟悉的日常，也有发生的过程'},
 news:{beats:[{image:news,text:'旧街的灯，又亮起来了。'},{image:dinner,text:'居民修好电线，把几张旧桌子搬到了门廊下。'},{image:news,text:'“有人带汤，有人带椅子。”晨报上这样写道。'}],result:['街头消息'],button:'放下报纸',note:'分镜排版实验 · 报纸示例'}
};
let current='care', timer=null, typingTimer=null, dialogueIndex=0, visible=0, complete=true;
function stopTimers(){clearTimeout(timer);clearInterval(typingTimer);timer=null;typingTimer=null;}
function currentText(){return $('story-lines').children[visible-1];}
function finishText(){
 clearInterval(typingTimer);typingTimer=null;
 const beat=samples[current].beats[visible-1];
 if(beat){currentText().textContent=beat.text;currentText().classList.add('revealed');}
 complete=true;
}
function showEffects(){
 $('consequence').classList.add('revealed');$('continue').classList.add('revealed');$('continue').disabled=false;
}
function revealNext(){
 stopTimers();finishText();const s=samples[current];
 if(visible>=s.beats.length){showEffects();return;}
 const index=visible++;const text=currentText();
 $('illustration').children[index].classList.add('revealed');
 if($('typing-toggle').checked&&!matchMedia('(prefers-reduced-motion: reduce)').matches){
  let i=0;complete=false;text.textContent='';text.classList.add('revealed');
  typingTimer=setInterval(()=>{
   text.textContent=s.beats[index].text.slice(0,++i);
   if(i>=s.beats[index].text.length){finishText();timer=setTimeout(revealNext,index===s.beats.length-1?180:0);}
  },32);
 }else{
  // 插画先浮现，文字随后出现；下一拍紧接，不留长时间的空等。
  complete=false;
  timer=setTimeout(()=>{finishText();timer=setTimeout(revealNext,index===s.beats.length-1?650:1050);},220);
 }
}
function skipBeat(){
 if(!complete){stopTimers();finishText();timer=setTimeout(revealNext,0);}
 else if(!$('consequence').classList.contains('revealed'))revealNext();
}
function render(){
 stopTimers();visible=0;complete=true;dialogueIndex=0;const s=samples[current];
 document.querySelectorAll('[data-sample]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.sample===current)));
 $('paper').hidden=false;$('dialogue').hidden=true;$('closed').hidden=true;
 $('paper').classList.toggle('no-image',!$('image-toggle').checked);
 $('illustration').dataset.layout=$('panel-layout').value;
 $('illustration').innerHTML=s.beats.map((beat,i)=>`<figure class="comic-panel" aria-label="第${i+1}格">${beat.image}</figure>`).join('');
 $('story-lines').replaceChildren(...s.beats.map(()=>document.createElement('p')));
 [...$('story-lines').children].forEach((line,i)=>line.textContent=s.beats[i].text);
 $('consequence').replaceChildren(...s.result.map(label=>{const tag=document.createElement('span');tag.className='effect-tag';tag.textContent=label;return tag;}));$('consequence').classList.remove('revealed');
 $('continue').textContent=s.button;$('continue').classList.remove('revealed');$('continue').disabled=true;$('note-title').textContent=s.note;
 revealNext();
}
function showLine(){const line=samples[current].dialogue[dialogueIndex];$('speaker').textContent=line[0];$('line').textContent=line[1];$('next-line').textContent=dialogueIndex===samples[current].dialogue.length-1?'回到旅馆':'继续';}
function close(){stopTimers();$('paper').hidden=true;$('dialogue').hidden=true;$('closed').hidden=false;$('reopen').focus({preventScroll:true});}
function advance(){
 if(!$('paper').hidden){if(!complete||!$('consequence').classList.contains('revealed')){skipBeat();return;}
 if(samples[current].dialogue){$('paper').hidden=true;$('dialogue').hidden=false;showLine();$('next-line').focus({preventScroll:true});}else close();
 }else if(!$('dialogue').hidden){dialogueIndex++;if(dialogueIndex<samples[current].dialogue.length)showLine();else close();}
}
$('samples').addEventListener('click',e=>{const b=e.target.closest('[data-sample]');if(b){current=b.dataset.sample;render();}});
$('continue').onclick=advance;$('next-line').onclick=advance;
$('story-beats').onclick=skipBeat;
$('replay').onclick=render;$('reopen').onclick=render;
$('panel-layout').onchange=()=>{$('illustration').dataset.layout=$('panel-layout').value;};
$('image-toggle').onchange=render;$('typing-toggle').onchange=render;
$('sheet-game').addEventListener('keydown',e=>{if((e.key===' '||e.key==='Enter')&&!e.target.closest('button,input,select')){e.preventDefault();advance();}});
$('sheet-game').tabIndex=0;
const resize=new ResizeObserver(entries=>{const scale=entries[0].contentRect.width/1280;$('sheet-game').style.transform=`scale(${scale})`;$('sheet-viewport').style.height=`${720*scale}px`;});resize.observe($('sheet-viewport'));
render();
