{n:'追逐',sub:'街角 · 有人碰了那个信封',sh:'追逐',pos:[1230,250],bg:'#07090e',set:'#set3',lx:1250,i:['44%','72%'],mk:()=>SETS.chase(),
ini(){$('#props').innerHTML='<g id="sl"></g>'+LETTER+EP+'<g id="fxg"></g><g id="crt" style="opacity:0"><rect x="-30" y="-48" width="60" height="48" fill="#07080c" stroke="#cfe0ff" stroke-width="2.5"/><line x1="-30" y1="-48" x2="30" y2="0" stroke="#7f95c4" stroke-width="2"/><line x1="30" y1="-48" x2="-30" y2="0" stroke="#7f95c4" stroke-width="2"/></g>';
$('#ep').style.opacity=1;mvl(692,585,0);$('#front').innerHTML=BENCH+NEWSP;$('#np').style.opacity=1;$('#np').style.transform='translate(0,0)';$('#ey').style.transform='translateX(-3px)';
G.nl.classList.add('snap');G.v1.classList.add('snap');P('nl',{d:0,show:1,x:1070,y:830,s:.56,f:1,b:-4,face:'hard'});P('v1',{d:0,x:-150,s:.56,f:0,face:'n'})},
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
