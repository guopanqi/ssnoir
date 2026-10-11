import re,sys
f='/mnt/user-data/outputs/portrait-theatre-story-claude-sonnet-5-5.html'
s=open(f,encoding='utf-8').read()
def rep(a,b):
    global s
    assert s.count(a)==1,(s.count(a),a[:80])
    s=s.replace(a,b)

# 1 legs -> separately rotatable groups
a=s.index('<path d="M${-w*.7} -150');b=s.index('\n',a)
old=s[a:b]
new='<g class="lgL" style="transform-origin:${-w*.45}px -150px"><path d="M${-w*.7} -150 L${-w*.2} -150 L${-w*.22} -8 L${-w*.75} -8Z" fill="${c.pants}"/><ellipse cx="${-w*.5+6}" cy="-5" rx="${w*.45}" ry="8" fill="#0a0a0a"/></g><g class="lgR" style="transform-origin:${w*.45}px -150px"><path d="M${w*.2} -150 L${w*.7} -150 L${w*.75} -8 L${w*.22} -8Z" fill="${c.pants}"/><ellipse cx="${w*.5+6}" cy="-5" rx="${w*.45}" ry="8" fill="#0a0a0a"/></g>'
assert old.count('<ellipse')==2 and old.endswith('/>'),old[-200:]
s=s[:a]+new+s[b:]

# 2 P: leg rotation
rep("if('ar'in o)q('.aR').style.transform=`rotate(${o.ar}deg)`;","if('ar'in o)q('.aR').style.transform=`rotate(${o.ar}deg)`;\nif('ll'in o)q('.lgL').style.transform=`rotate(${o.ll}deg)`;\nif('lr'in o)q('.lgR').style.transform=`rotate(${o.lr}deg)`;")

# 3 css
rep('</style></head>','.ch.snap,.ch.snap *{transition:none!important}@keyframes bst{0%{opacity:.95;transform:scale(.5)}100%{opacity:0;transform:scale(1.35)}}.bst{animation:bst .5s ease-out forwards}@keyframes shk{20%{transform:translate(-9px,4px)}40%{transform:translate(8px,-4px)}60%{transform:translate(-5px,2px)}}@media (prefers-reduced-motion:reduce){#stage.shk{animation:none}}</style></head>')

# 4 reset
rep("umc:0,cg:0,cl:{tk:0,wk:0,sob:0,run:0}});","umc:0,cg:0,ll:0,lr:0,cl:{tk:0,wk:0,sob:0,run:0,snap:0}});")

# 5 crowd stepped
rep("{duration:8000+Math.random()*4000,delay:i*650,fill:'both'}","{duration:8000+Math.random()*4000,delay:i*650,fill:'both',easing:'steps(56)'}")

# 6 mailbox: remove Chinese glyph
rep('<text x="700" y="668" text-anchor="middle" font-size="24" fill="none" stroke="#9fb4e0" font-family="serif">邮</text>','<line x1="684" y1="650" x2="716" y2="650" stroke="#9fb4e0" stroke-width="2"/><line x1="684" y1="662" x2="716" y2="662" stroke="#9fb4e0" stroke-width="2"/>')

# 7 new SETS.chase
a=s.index('SETS.chase=()=>');b=s.index("const EP=")
CHASE=r'''const SILB='<circle cx="0" cy="-190" r="20" fill="#05070d" stroke="#4a5f8c" stroke-width="2"/><polygon points="-24,-166 24,-166 36,-76 24,-70 20,0 5,0 0,-56 -5,0 -20,0 -24,-70 -36,-76" fill="#05070d" stroke="#4a5f8c" stroke-width="2"/>';
SETS.chase=()=>{let f='',m='',n='',cw='';for(let i=0;i<14;i++){const h=170+rnd(i)*260,wd=70+rnd(i+9)*40;f+=`<rect x="${i*115}" y="${790-h}" width="${wd}" height="${h}" fill="#05070e" stroke="#2a3a60" stroke-width="2"/>`;if(rnd(i+4)>.5)f+=`<rect x="${i*115+14}" y="${790-h+30}" width="10" height="12" fill="#8a6a38" opacity=".55"/>`}
for(let i=0;i<4;i++){m+=`<line x1="${i*400+60}" y1="790" x2="${i*400+60}" y2="300" stroke="#3b4a70" stroke-width="5"/><circle cx="${i*400+60}" cy="296" r="9" fill="#ffe6a8" opacity=".7"/><rect x="${i*400+230}" y="680" width="120" height="110" fill="#05070e" stroke="#34466e" stroke-width="2"/>`}
for(let i=0;i<8;i++)n+=`<line x1="${i*200}" y1="850" x2="${i*200+90}" y2="850" stroke="#46608f" stroke-width="5"/>`;
for(let i=0;i<14;i++){const sc=.52+rnd(i+20)*.42,y=788+rnd(i+30)*34,x=i*128+rnd(i)*40,fl=i%2?1:-1;cw+=`<g class="cs" data-x="${x}" data-y="${y}" data-sc="${sc}" transform="translate(${x-100} ${y})"><g class="tp"><g transform="scale(${sc*fl} ${sc})">${SILB}</g></g></g>`}
const lay=(id,c)=>`<g id="${id}"><g transform="translate(-1600 0)">${c}</g>${c}</g>`;
const W=(x,y,l)=>`<rect x="${x}" y="${y}" width="50" height="64" fill="${l?'#8a6a38':'none'}" fill-opacity=".5" stroke="#6f86b8" stroke-width="2"/>`;
const ALLEY2='<rect x="-320" y="150" width="620" height="640" fill="#05070e" stroke="#cfe0ff" stroke-width="2.5"/><rect x="560" y="190" width="560" height="600" fill="#05070e" stroke="#cfe0ff" stroke-width="2.5"/>'+W(20,230)+W(120,230,1)+W(210,230)+W(20,360)+W(120,360)+W(210,360,1)+W(610,270)+W(710,270)+W(810,270,1)+W(910,270)+W(1010,270)+W(610,400,1)+W(710,400)+W(810,400)+W(910,400,1)
+'<polygon points="300,150 560,190 560,790 300,790" fill="#020308"/><circle cx="430" cy="560" r="110" fill="#f3d08a" opacity=".05"/><polygon points="300,150 404,500 404,610 300,790" fill="#070a12" stroke="#4a5f8c" stroke-width="2"/><polygon points="560,190 456,500 456,610 560,790" fill="#070a12" stroke="#4a5f8c" stroke-width="2"/><polygon points="300,790 560,790 456,610 404,610" fill="#04060b" stroke="#4a5f8c" stroke-width="2"/><rect x="404" y="500" width="52" height="110" fill="#0d0f18" stroke="#4a5f8c" stroke-width="2"/><rect id="ff" x="420" y="520" width="20" height="70" fill="#f3d08a" opacity=".85" style="transition:opacity .08s"/>'
+'<line x1="300" y1="330" x2="404" y2="540" stroke="#34466e" stroke-width="1.5"/><line x1="300" y1="500" x2="404" y2="575" stroke="#34466e" stroke-width="1.5"/><line x1="560" y1="360" x2="456" y2="540" stroke="#34466e" stroke-width="1.5"/><line x1="560" y1="540" x2="456" y2="578" stroke="#34466e" stroke-width="1.5"/><line x1="540" y1="190" x2="540" y2="790" stroke="#6f86b8" stroke-width="3"/><rect x="318" y="735" width="44" height="55" fill="#05070e" stroke="#7f95c4" stroke-width="2"/><line x1="314" y1="735" x2="366" y2="735" stroke="#cfe0ff" stroke-width="3"/>';
return '<g id="stat">'+SETS.street()+'</g><g id="scr" style="opacity:0">'+lay('L0',f)+lay('L1',m)+lay('L2',n)+`<g id="crw">${cw}</g></g><g id="alw" transform="translate(-1800 0)">${ALLEY2}</g>`};
'''
s=s[:a]+CHASE+s[b:]

# 8 new chase chapter
a=s.index("{n:'追逐'");b=s.index("{n:'烟盒'")
CH=r'''{n:'追逐',sub:'街角 · 有人碰了那个信封',sh:'追逐',pos:[1230,250],bg:'#07090e',set:'#set3',lx:1250,i:['44%','72%'],mk:()=>SETS.chase(),
ini(){$('#props').innerHTML='<g id="sl"></g>'+LETTER+EP+'<g id="fxg"></g><g id="crt" style="opacity:0"><rect x="-30" y="-48" width="60" height="48" fill="#07080c" stroke="#cfe0ff" stroke-width="2.5"/><line x1="-30" y1="-48" x2="30" y2="0" stroke="#7f95c4" stroke-width="2"/><line x1="30" y1="-48" x2="-30" y2="0" stroke="#7f95c4" stroke-width="2"/></g>';
$('#ep').style.opacity=1;mvl(692,585,0);$('#front').innerHTML=BENCH+NEWSP;$('#np').style.opacity=1;$('#np').style.transform='translate(0,0)';$('#ey').style.transform='translateX(-3px)';
G.nl.classList.add('snap');G.v1.classList.add('snap');P('nl',{d:0,x:1070,y:830,s:.56,f:1,b:-4,face:'hard'});P('v1',{d:0,x:-150,s:.56,f:0,face:'n'})},
async run(k,{w}){
const stat=$('#stat'),scr=$('#scr'),alw=$('#alw'),sl=$('#sl'),fxg=$('#fxg'),crt=$('#crt'),ey=$('#ey'),stg=$('#stage');
const Ls=[$('#L0'),$('#L1'),$('#L2')],off=[0,0,0],base=[10,26,60];
const cs=[...$('#crw').children].map(el=>({el,tp:el.firstElementChild,x:+el.dataset.x,y:+el.dataset.y,sc:+el.dataset.sc}));
let spd=0,alx=-1800;
/* 定格姿势：每个姿势是一张静态立绘，直接切换，不做关节补间 */
const RUN={A:{ll:-38,lr:34,ar:-62,al:58,b:15,h:-5},B:{ll:34,lr:-38,ar:58,al:-62,b:15,h:-5},C:{ll:-64,lr:48,ar:-88,al:30,b:19,h:-8}};
const WALK={A:{ll:-15,lr:13,ar:11,al:-13,b:2},B:{ll:13,lr:-15,ar:-13,al:11,b:2}};
const PS={shove:{ll:-28,lr:36,ar:-80,al:-84,b:21,h:-7},brake:{ll:-48,lr:-20,ar:-46,al:-36,b:-13,h:6},corner:{ll:-56,lr:42,ar:-28,al:72,b:27,h:-10},stagger:{ll:-18,lr:30,ar:-100,al:62,b:23,h:12},vault:{ll:-74,lr:-58,ar:-72,al:-52,b:11,h:-6},swipe:{ll:-30,lr:30,ar:-96,al:42,b:9,h:-8},pant1:{ll:-6,lr:5,ar:14,al:18,b:5,h:7},pant2:{ll:-6,lr:5,ar:10,al:12,b:9,h:10}};
const pose=(id,p,o={})=>{G[id].classList.add('snap');P(id,{ll:0,lr:0,al:0,ar:0,b:0,h:0,look:0,...p,...o})};
const shake=()=>{stg.classList.remove('shk');void stg.offsetWidth;stg.classList.add('shk');setTimeout(()=>stg.classList.remove('shk'),420)};
const flash=()=>{fxf('brightness(1.9)',30);setTimeout(()=>fxf('none',160),90)};
const burst=(x,y)=>{let h='';for(let i=0;i<12;i++){const a=i/12*6.283,r1=46+Math.random()*10,r2=r1+60+Math.random()*50;h+=`<line x1="${x+Math.cos(a)*r1}" y1="${y+Math.sin(a)*r1}" x2="${x+Math.cos(a)*r2}" y2="${y+Math.sin(a)*r2}" stroke="#f0f4ff" stroke-width="3"/>`}fxg.insertAdjacentHTML('beforeend',`<g class="bst" style="transform-origin:${x}px ${y}px">${h}</g>`);if(fxg.children.length>8)fxg.firstChild.remove()};
const dust=(x,y,dir)=>{let h='';for(let i=0;i<5;i++)h+=`<circle cx="${x+dir*i*22}" cy="${y-i*4-Math.random()*10}" r="${10+i*5}" fill="none" stroke="#9fb4e0" stroke-width="2"/>`;fxg.insertAdjacentHTML('beforeend',`<g class="bst" style="transform-origin:${x}px ${y}px">${h}</g>`);if(fxg.children.length>8)fxg.firstChild.remove()};
const streaks=n=>{let h='';for(let i=0;i<n;i++){const y=250+Math.random()*540,x=Math.random()*1500,l=140+Math.random()*260;h+=`<line x1="${x}" y1="${y}" x2="${x+l}" y2="${y}" stroke="#cfe0ff" stroke-width="${1+Math.random()*2}" opacity="${.12+Math.random()*.28}"/>`}sl.innerHTML=h};
const scroll=()=>{for(let i=0;i<3;i++){off[i]=(off[i]+base[i]*spd)%1600;Ls[i].setAttribute('transform',`translate(${off[i]} 0)`)}
cs.forEach(c=>{const o=c.x;c.x=(c.x+70*spd)%1800;if(c.x<o)c.tp.style.transform='';c.el.setAttribute('transform',`translate(${c.x-100} ${c.y})`)})};
const tip=(nx,dir)=>cs.forEach((c,i)=>{if(Math.abs(c.x-100-nx)<190&&c.sc>.5)c.tp.style.transform=`rotate(${dir*(46+(i%3)*14)}deg)`});
const HOLD=165;
async function cyc(ms,fn,sn=7){const n=Math.max(1,Math.round(ms/HOLD));for(let i=0;i<n;i++){const t=(i+1)/n,o=fn(t,i)||{},ph=i%2,fl=i%4==3;
pose('v1',o.rp||(fl?RUN.C:ph?RUN.B:RUN.A),{f:1,x:o.rx,y:ph?790:781,face:o.rf||'n',look:o.rl||0});
pose('nl',o.np||(fl?RUN.C:ph?RUN.A:RUN.B),{f:1,x:o.nx,y:ph?781:790,face:'hard'});
scroll();streaks(sn);if(ph)stp();if(i%3==0)tn(56,.2,'sine',.2);await w(HOLD)}}
/* 1 蹲守 */
await w(500);os('.lb',1);os('.lh',.3);sp(2,1250,1.1,.14,'#f3d08a');sp(1,700,.7,.1,'#9db0e0');
crowd(7,1);await w(1600);
pose('v1',WALK.A,{x:-150,y:790,f:0,show:1});
for(let i=0;i<14;i++){pose('v1',i%2?WALK.B:WALK.A,{x:-150+i*59.2,y:790,f:0});if(i%2)stp();await w(250)}
await w(300);
pose('v1',{ar:-70,b:3},{x:622,y:790,f:0});await w(380);
pose('v1',{ar:-88,b:4},{x:622,y:790,f:0});await w(420);
op('#ep',0);tn(1500,.08,'triangle',.05);bs(.1,.3,1800);
pose('v1',{ar:-70,b:5},{x:622,y:790,f:0});await w(300);
pose('v1',{ar:-26,b:6,h:-3},{x:622,y:790,f:0});await w(700);
/* 2 看见 */
ey.style.transform='translate(0,0)';await w(900);
const nps=$('#np');nps.style.transition='none';nps.style.opacity=0;$('#bch').style.opacity=0;
pose('nl',{b:5,h:-4,ar:-34,al:-12},{x:1070,y:790,f:1,face:'shock'});tn(220,.5,'sawtooth',.07);tn(233,.5,'sawtooth',.06);burst(1070,545);flash();shake();sp(1,1070,.8,.3,'#ffd9a0');
await w(800);
pose('v1',{b:2,h:-5,ar:-22},{x:622,y:790,f:0,face:'shock'});await w(1500);
/* 3 开跑 */
sp(1,700,.6,.05);sp(2,1250,1.1,0);
stat.style.transition='transform .55s steps(5,end)';stat.style.transform='translateX(1900px)';scr.style.opacity=1;spd=1;
pose('v1',RUN.A,{x:620,y:781,f:1});pose('nl',RUN.B,{x:1040,y:781,f:1});tn(110,.3,'sawtooth',.06);bs(.15,.5,500);
await cyc(2200,t=>({rx:620-110*Math.min(1,t*1.6),nx:1040-100*t}));
/* 4 冲撞人群 */
spd=1.4;
await cyc(1650,(t,i)=>{const sv=i==2||i==6;if(sv){tip(950,-1);shake();bs(.18,.7,320);tn(70,.2,'sine',.35)}return{rx:510,nx:940+30*t,np:sv?PS.shove:null}});
spd=1.6;
await cyc(1300,t=>({rx:510,nx:970-90*t}),8);
/* 5 踢翻木箱，尼尔腾跃 */
spd=1.7;
pose('v1',PS.swipe,{x:510,y:786,f:1,face:'shock',look:-6});pose('nl',RUN.A,{x:890,y:781,f:1});
crt.style.opacity=1;crt.setAttribute('transform','translate(570 790)');scroll();streaks(8);bs(.12,.6,600);await w(200);
for(let j=0;j<7;j++){const ph=j%2,x=570+j*70,y=790-Math.abs(Math.sin(j*1.1))*34,r=-j*34;
crt.setAttribute('transform',`translate(${x} ${y}) rotate(${r})`);
pose('v1',ph?RUN.B:RUN.A,{x:512-j*3,y:ph?790:781,f:1,face:'n',look:-6});
if(j<3)pose('nl',ph?RUN.A:RUN.B,{x:880-j*8,y:ph?781:790,f:1});
else if(j<5)pose('nl',PS.vault,{x:850-(j-3)*14,y:j==3?740:704,f:1,face:'shock'});
else if(j==5)pose('nl',PS.vault,{x:820,y:728,f:1,face:'shock'});
else{pose('nl',PS.stagger,{x:806,y:790,f:1,face:'shock'});dust(806,792,1);shake();bs(.3,.8,260);tn(60,.3,'sine',.4);flash()}
scroll();streaks(8);await w(j==6?330:135)}
crt.style.opacity=0;
/* 6 冲刺，拉近距离 */
spd=2.2;stg.style.setProperty('--i2','58%');
await cyc(2300,t=>({rx:496-20*t,nx:900-150*t,rf:'shock',rl:-6}),10);
spd=2.4;
await cyc(1300,(t,i)=>{const sv=i==1||i==4;if(sv){tip(760,-1);shake();bs(.18,.7,320)}return{rx:476,nx:750+25*t,np:sv?PS.shove:null}},10);
/* 7 巷口滑入，镜头定住 */
await cyc(2700,t=>{alx=-1800*Math.pow(1-t,3);alw.setAttribute('transform',`translate(${alx} 0)`);spd=2.4*Math.pow(1-t,1.5);return{rx:476-4*t,nx:775-175*t,rf:'shock',rl:-6}},8);
spd=0;scroll();sl.innerHTML='';stg.style.setProperty('--i2','72%');
/* 8 逃犯拐进巷子 */
pose('v1',PS.corner,{x:476,y:786,f:1,face:'shock'});stp();bs(.4,.4,2600);dust(484,792,1);await w(320);
for(const[x,y,s,p]of[[452,770,.5,RUN.A],[440,735,.4,RUN.B],[434,700,.31,RUN.A],[431,668,.23,RUN.B]]){pose('v1',p,{x,y,s,f:1,back:1,b:6,h:0});stp();await w(210)}
await w(210);P('v1',{show:0});$('#ff').style.opacity=.15;await w(120);$('#ff').style.opacity=.85;
/* 9 尼尔刹住，喘息，也拐进去 */
await w(450);
let ph=0;for(const x of[720,650,596]){pose('nl',ph?RUN.A:RUN.B,{x,y:ph?781:790,f:1});ph^=1;stp();await w(HOLD)}
pose('nl',PS.brake,{x:560,y:790,f:1,face:'hard'});bs(.5,.45,2600);dust(570,792,1);await w(750);
for(let i=0;i<6;i++){pose('nl',i%2?PS.pant2:PS.pant1,{x:558,y:790,f:1,face:'hard',look:i>2?6:0});await w(330)}
await w(250);
pose('nl',PS.corner,{x:524,y:788,f:1,face:'hard'});stp();await w(300);
for(const[x,y,s,p]of[[484,770,.5,RUN.A],[452,735,.4,RUN.B],[437,700,.31,RUN.A],[432,668,.23,RUN.B]]){pose('nl',p,{x,y,s,f:1,back:1,b:6,h:0});stp();await w(230)}
await w(230);P('nl',{show:0});$('#ff').style.opacity=.15;await w(150);$('#ff').style.opacity=.85;
await w(2200)}},
'''
s=s[:a]+CH+s[b:]
open(f,'w',encoding='utf-8').write(s)
print('patched')
