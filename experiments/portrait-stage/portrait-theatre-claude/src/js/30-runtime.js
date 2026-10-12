/* timing */
try{CSS.registerProperty({name:'--ix',syntax:'<percentage>',inherits:true,initialValue:'50%'})}catch(e){}
const sleep=async(ms,k)=>{let t=0;while(t<ms){if(k!==gen)throw 0;await new Promise(r=>setTimeout(r,50));if(!paused)t+=50}if(k!==gen)throw 0};
const cap=$('#cap'),cc=()=>cap.innerHTML='',op=(s,v)=>$(s).style.opacity=v;let SET=$('#hotel'),cur=-1;
const rx=(cl,on)=>{if(react)SET.classList.toggle(cl,on)};
function sp(n,x,sc,o,c='#fff',y=560){const e=$('#s'+n);e.style.transform=`translate(${x}px,${y}px) scale(${sc})`;e.style.opacity=o;e.style.fill=c}
const lamp=v=>{SET.style.setProperty('--lv',v);const q=s=>SET.querySelector(s);if(q('.lb'))q('.lb').style.opacity=v;if(q('.lh'))q('.lh').style.opacity=v*.4;if(q('.cone'))q('.cone').style.opacity=v;sp(2,+(SET.dataset.lx||800),1.1,.22*v,'#f3d08a')},zap=()=>{tn(110,.12,'square',.03);bs(.04,.15,3000)};
const os=(s,v)=>{const e=SET.querySelector(s);if(e)e.style.opacity=v};
const blinds=v=>SET.querySelectorAll('.blind').forEach((e,i)=>e.style.opacity=v*(.07+(i%2)*.05)),mp=v=>SET.querySelectorAll('.mp').forEach(e=>e.style.opacity=v),mbs=n=>SET.querySelectorAll('.mb').forEach((e,i)=>e.style.opacity=(i%5<n)?.12:1);
const slam=()=>{bs(.6,1,420);tn(52,.6,'sine',.5)},mvp=(x,y,o)=>{const p=$('#pd');p.style.transform=`translate(${x}px,${y}px)`;p.style.opacity=o},note=(t,x,y,dl=0)=>{const w=t.length*20+24;$('#props').insertAdjacentHTML('beforeend',`<g class="nt" style="animation-delay:${dl}s"><rect x="${x-w/2}" y="${y-22}" width="${w}" height="32" fill="#0b0f1c" stroke="#ffd98a"/><text x="${x}" y="${y}" text-anchor="middle" font-size="18" fill="#ffd98a" font-family="serif">${t}</text></g>`)};
const pad=()=>[220,277,330,440].forEach((f,i)=>tn(f,4.5,'sine',.07,i*.18,1.4));
const light=()=>{bs(.06,.3,4000);tn(900,.1,'square',.03);P('nl',{cg:1})};
const pk=(x,y,s,o,r=0,ms=900)=>{const p=$('#pack');p.style.transitionDuration=ms+'ms';p.style.transform=`translate(${x}px,${y}px) scale(${s}) rotate(${r}deg)`;p.style.opacity=o};
const gh=(x,y,s,o)=>{const g=$('#gh');g.style.transform=`translate(${x}px,${y}px) scale(${s}) rotate(8deg)`;g.style.opacity=o};
const mvl=(x,y,o=1)=>{const l=$('#lt');l.style.transform=`translate(${x}px,${y}px)`;l.style.opacity=o};
const fxf=(f,ms)=>{for(const s of['#bg','#fg']){$(s).style.transition=`filter ${ms}ms`;$(s).style.filter=f}};
const whisp=(t,x,y,dl=0)=>$('#props').insertAdjacentHTML('beforeend',`<text class="wsp" text-anchor="middle" x="${x}" y="${y}" style="animation-delay:${dl}s">${t}</text>`);
const piano=()=>[262,330,392,523,392,330,294,392].forEach((f,i)=>tn(f,1.1,'triangle',.08,i*.4));
const NM={mg:['经 理','#f4907c'],ny:['夜 莺','#8fd9d0'],nl:['尼 尔','#f0cf8a'],fr:['弗 兰 克','#e39a88'],bn:['贝 恩 斯','#93b4ea'],ld:['房 东','#c9b27a']},setIx=x=>$('#stage').style.setProperty('--ix',(50+(x-800)/16*.5)+'%');
/* 立绘姿势助手：姿势 = 一整张立绘（肢体+前倾+头），整张切换；位移 mv 是连续补间 */
const POSES={};
const pose=(id,p,o={})=>{G[id].classList.add('snap');P(id,{ll:0,lr:0,al:0,ar:0,b:0,h:0,look:0,d:0,...(typeof p=='string'?POSES[p]:p),...o})};
const bob=(id,on)=>G[id].classList.toggle('bnc',on);
const mv=(id,x,y,ms,ease='linear',s)=>{G[id].style.transitionTimingFunction=ease;P(id,{x,y,d:ms,...(s?{s}:{})})};
/* 舞台调度助手：镜头缓推/拉远（对 #bg/#fg 同步缩放，字幕不动） */
const dolly=(s=1,x=50,y=60,ms=4000)=>{for(const q of['#bg','#fg']){const e=$(q);e.style.transformOrigin=`${x}% ${y}%`;e.style.transition=ms?`transform ${ms}ms ease-in-out`:'none';e.style.transform=s==1?'':`scale(${s})`}};
function H(k){const w=ms=>sleep(ms,k);/* at(ms,fn)：不阻塞地在台词进行中触发动作；sigh：原地叹气（头低、微前倾、气声）；ticks：钟表滴答 */
const at=(ms,fn)=>setTimeout(()=>{if(k===gen)fn()},ms);
const sigh=async(id,ms=1500)=>{P(id,{h:14,b:6,d:900});bs(.5,.07,700);tn(190,.7,'sine',.02);await w(ms*.6);P(id,{h:3,b:0,d:900});await w(ms*.4)};
const ticks=(n,gap=900)=>{for(let i=0;i<n;i++)at(i*gap,()=>tn(i%2?1500:1250,.03,'square',.025))};

const say=async(who,text,o)=>{if(k!==gen)throw 0;const n=NM[who];cap.innerHTML=`<b style="color:${n[1]}">${n[0]}${o==1?'（电铃）':o==2?'（独白）':''}</b>`+(o==2?'<em>':'')+[...text].map((ch,i)=>`<span style="animation-delay:${i*48}ms">${ch}</span>`).join('')+(o==2?'</em>':'');const dur=450+text.length*105;
if(o==1){op('#sg',1);$('#vr').classList.add('on');$('#nw').classList.add('lit');const iv=setInterval(()=>op('#sg',Math.random()>.5?1:.55),160);try{await w(dur)}finally{clearInterval(iv);op('#sg',0);$('#vr').classList.remove('on')}}
else if(o==2){await w(dur+600)}
else{const g=G[who];sp(1,g.st.x,.55,.13,'#9db0e0');setIx(g.st.x);g.classList.add('tk');try{await w(dur)}finally{g.classList.remove('tk')}}};
const go=(id,x,ms,o={})=>{P(id,{x,d:ms,cl:{wk:1},...o});steps(ms);setTimeout(()=>{if(k===gen)P(id,{cl:{wk:0}})},ms)};
const fl=async()=>{for(const v of[.15,1,.3,.9,.5,1]){lamp(v);zap();await w(110)}};
const nar=async(t,ms)=>{cap.innerHTML='<em>'+t+'</em>';await w(ms??700+t.length*85)};
return{w,say,go,fl,nar,at,sigh,ticks,n:t=>say('nl',t),y:t=>say('ny',t),f:t=>say('fr',t),m:t=>say('mg',t),b:t=>say('bn',t),l:t=>say('ld',t),mono:t=>say('nl',t,2)}}
