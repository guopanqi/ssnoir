{n:'追逐',sub:'街角 · 有人碰了那个信封',sh:'追逐',pos:[1230,250],bg:'#07090e',set:'#set3',lx:1250,i:['44%','72%'],mk:()=>SETS.chase(),
ini(){$('#props').innerHTML='<g id="sl"></g>'+LETTER+EP+'<g id="fxg"></g><g id="crt" style="opacity:0"><rect x="-30" y="-48" width="60" height="48" fill="#07080c" stroke="#cfe0ff" stroke-width="2.5"/><line x1="-30" y1="-48" x2="30" y2="0" stroke="#7f95c4" stroke-width="2"/><line x1="30" y1="-48" x2="-30" y2="0" stroke="#7f95c4" stroke-width="2"/></g>';
$('#ep').style.opacity=1;mvl(692,585,0);$('#front').innerHTML=BENCH+NEWSP;$('#np').style.opacity=1;$('#np').style.transform='translate(0,0)';$('#ey').style.transform='translateX(-3px)';
G.nl.classList.add('snap');G.v1.classList.add('snap');P('nl',{d:0,show:1,x:1070,y:830,s:.56,f:1,b:-4,face:'hard'});P('v1',{d:0,x:-150,s:.56,f:0,face:'n'})},
async run(k,{w}){
const stat=$('#stat'),scr=$('#scr'),alw=$('#alw'),sl=$('#sl'),fxg=$('#fxg'),crt=$('#crt'),ey=$('#ey'),stg=$('#stage');
const Ls=[$('#L0'),$('#L1'),$('#L2')],off=[0,0,0],base=[10,26,60];
const cs=[...$('#crw').children].map(el=>({el,tp:el.firstElementChild,x:+el.dataset.x,y:+el.dataset.y,sc:+el.dataset.sc}));
let spd=0,tgt=0,rms=1,last=performance.now(),sk=0,raf=0,going=true;
/* 立绘姿势表：每个姿势是一张整体立绘（肢体角度、脸、身体前倾的固定组合），换姿势时整张切换；
   位移（走位、镜头、背景滚动）仍然是连续流畅的。以后生成真正的立绘图时，用图片替换这张表即可。 */
const POSE=POSES;Object.assign(POSE,{
 walk:{ll:-12,lr:10,ar:9,al:-11,b:2},
 run:{ll:-40,lr:34,ar:-62,al:58,b:16,h:-6},
 shove:{ll:-28,lr:36,ar:-80,al:-84,b:21,h:-7},
 swipe:{ll:-30,lr:30,ar:-96,al:42,b:9,h:-8},
 vault:{ll:-74,lr:-58,ar:-72,al:-52,b:11,h:-6},
 stagger:{ll:-18,lr:30,ar:-100,al:62,b:23,h:12},
 corner:{ll:-56,lr:42,ar:-28,al:72,b:27,h:-10},
 brake:{ll:-48,lr:-20,ar:-46,al:-36,b:-13,h:6},
 pant:{ll:-6,lr:5,ar:14,al:18,b:8,h:9}});
const ramp=(v,ms)=>{tgt=v;rms=ms};
const shake=()=>{stg.classList.remove('shk');void stg.offsetWidth;stg.classList.add('shk');setTimeout(()=>stg.classList.remove('shk'),420)};
const flash=()=>{fxf('brightness(1.9)',30);setTimeout(()=>fxf('none',160),90)};
const burst=(x,y)=>{let h='';for(let i=0;i<12;i++){const a=i/12*6.283,r1=46+Math.random()*10,r2=r1+60+Math.random()*50;h+=`<line x1="${x+Math.cos(a)*r1}" y1="${y+Math.sin(a)*r1}" x2="${x+Math.cos(a)*r2}" y2="${y+Math.sin(a)*r2}" stroke="#f0f4ff" stroke-width="3"/>`}fxg.insertAdjacentHTML('beforeend',`<g class="bst" style="transform-origin:${x}px ${y}px">${h}</g>`);if(fxg.children.length>8)fxg.firstChild.remove()};
const dust=(x,y,dir)=>{let h='';for(let i=0;i<5;i++)h+=`<circle cx="${x+dir*i*22}" cy="${y-i*4-Math.random()*10}" r="${10+i*5}" fill="none" stroke="#9fb4e0" stroke-width="2"/>`;fxg.insertAdjacentHTML('beforeend',`<g class="bst" style="transform-origin:${x}px ${y}px">${h}</g>`);if(fxg.children.length>8)fxg.firstChild.remove()};
const streaks=n=>{let h='';for(let i=0;i<n;i++){const y=250+Math.random()*540,x=Math.random()*1500,l=140+Math.random()*260;h+=`<line x1="${x}" y1="${y}" x2="${x+l}" y2="${y}" stroke="#cfe0ff" stroke-width="${1+Math.random()*2}" opacity="${.12+Math.random()*.28}"/>`}sl.innerHTML=h};
const tip=(nx,dir)=>cs.forEach((c,i)=>{if(Math.abs(c.x-100-nx)<190&&c.sc>.5)c.tp.style.transform=`rotate(${dir*(46+(i%3)*14)}deg)`});
const tick=t=>{if(!going||k!==gen)return;const dt=Math.min(50,t-last);last=t;
 if(!paused){spd+=(tgt-spd)*(1-Math.exp(-dt/(rms/3)));
  for(let i=0;i<3;i++){off[i]=(off[i]+base[i]*spd*dt/165)%1600;Ls[i].setAttribute('transform',`translate(${off[i]} 0)`)}
  cs.forEach(c=>{const o=c.x;c.x=(c.x+70*spd*dt/165)%1800;if(c.x<o)c.tp.style.transform='';c.el.setAttribute('transform',`translate(${c.x-100} ${c.y})`)});
  sk+=dt;if(sk>110&&spd>.2){sk=0;streaks(Math.round(4+spd*3))}}
 raf=requestAnimationFrame(tick)};
const foot=async(ms,gap)=>{for(let t=0;t<ms&&k===gen;t+=gap){stp();if(spd>.3)tn(56,.2,'sine',.15);await w(gap)}};
const thud=(sv)=>{shake();bs(.18,.7,320);tn(70,.2,'sine',.35)};
raf=requestAnimationFrame(tick);
/* 1 蹲守：夜莺走来，把信封塞进邮箱 */
await w(500);os('.lb',1);os('.lh',.3);sp(2,1250,1.1,.14,'#f3d08a');sp(1,700,.7,.1,'#9db0e0');
crowd(7,1);await w(1600);
pose('v1','walk',{x:-150,y:790,f:0,show:1});bob('v1',1);mv('v1',622,790,3800);foot(3800,330);await w(3800);bob('v1',0);
pose('v1','walk',{ll:0,lr:0,al:0,ar:-70,b:3});await w(380);
pose('v1','walk',{ll:0,lr:0,al:0,ar:-88,b:4});await w(420);
op('#ep',0);tn(1500,.08,'triangle',.05);bs(.1,.3,1800);
pose('v1','walk',{ll:0,lr:0,al:0,ar:-70,b:5});await w(300);
pose('v1','walk',{ll:0,lr:0,al:0,ar:-26,b:6,h:-3});await w(700);
/* 2 看见：报纸放下，惊讶——换一整套表情和姿势 */
ey.style.transform='translate(0,0)';await w(900);
const nps=$('#np');nps.style.transition='none';nps.style.opacity=0;$('#bch').style.opacity=0;
pose('nl','walk',{ll:0,lr:0,al:-12,ar:-34,b:5,h:-4,x:1070,y:790,f:1,face:'shock'});tn(220,.5,'sawtooth',.07);tn(233,.5,'sawtooth',.06);burst(1070,545);flash();shake();sp(1,1070,.8,.3,'#ffd9a0');
await w(800);
pose('v1','walk',{ll:0,lr:0,al:0,ar:-22,b:2,h:-5,face:'shock'});await w(1500);
/* 3 开跑：两人各换成奔跑立绘，整体平移，背景连续卷动 */
sp(1,700,.6,.05);sp(2,1250,1.1,0);
stat.style.transition='transform .9s cubic-bezier(.5,0,.9,.5)';stat.style.transform='translateX(1900px)';scr.style.opacity=1;ramp(1,1200);
pose('v1','run',{f:1,face:'shock',look:-6});pose('nl','run',{f:1,face:'hard'});bob('v1',1);bob('nl',1);
tn(110,.3,'sawtooth',.06);bs(.15,.5,500);
mv('v1',510,790,1800,'cubic-bezier(.2,.6,.4,1)');mv('nl',930,790,2200,'cubic-bezier(.3,.2,.5,1)');foot(2200,200);await w(2200);
/* 4 冲撞人群：尼尔换成撞肩立绘，一拍后回到奔跑立绘 */
ramp(1.5,600);mv('nl',900,790,1700);
for(const t of[500,1000]){await w(t==500?500:500);tip(900,-1);thud();pose('nl','shove',{f:1,face:'hard'});await w(260);pose('nl','run',{f:1,face:'hard'})}
await w(500);ramp(1.7,500);
/* 5 踢翻木箱，尼尔腾跃 */
pose('v1','swipe',{f:1,face:'shock',look:-6});
crt.style.transition='none';crt.style.transform='translate(570px,790px) rotate(0deg)';crt.style.opacity=1;void crt.getBoundingClientRect();
bs(.12,.6,600);await w(220);
crt.style.transition='transform .55s cubic-bezier(.2,.7,.5,1)';crt.style.transform='translate(820px,700px) rotate(-170deg)';
pose('v1','run',{f:1,face:'shock',look:-6});mv('nl',860,790,500);await w(480);
crt.style.transition='transform .4s cubic-bezier(.5,0,.8,.6)';crt.style.transform='translate(1000px,790px) rotate(-300deg)';
pose('nl','vault',{f:1,face:'shock'});mv('nl',800,700,320,'cubic-bezier(.2,.7,.4,1)');await w(330);
mv('nl',790,790,260,'cubic-bezier(.5,0,.9,.6)');await w(260);
pose('nl','stagger',{f:1,face:'shock'});dust(790,792,1);shake();bs(.3,.8,260);tn(60,.3,'sine',.4);flash();await w(450);
crt.style.opacity=0;
/* 6 冲刺，拉近距离 */
pose('nl','run',{f:1,face:'hard'});ramp(2.2,900);stg.style.setProperty('--i2','58%');
mv('v1',480,790,2600);mv('nl',770,790,2600);foot(2600,170);
await w(1100);tip(780,-1);thud();pose('nl','shove',{f:1,face:'hard'});await w(260);pose('nl','run',{f:1,face:'hard'});
await w(1200);ramp(2.4,400);
/* 7 巷口滑入，镜头定住 */
alw.style.transition='transform 2.7s cubic-bezier(.2,.7,.2,1)';alw.style.transform='translate(0px,0px)';
mv('v1',476,790,2700,'ease-out');mv('nl',600,790,2700,'ease-out');ramp(0,2300);foot(1800,230);await w(2700);
spd=0;tgt=0;sl.innerHTML='';stg.style.setProperty('--i2','72%');
/* 8 逃犯拐进巷子：转角立绘 → 背影立绘，沿纵深平滑远去 */
bob('v1',0);pose('v1','corner',{f:1,face:'shock'});stp();bs(.4,.4,2600);dust(484,792,1);await w(380);
pose('v1','run',{f:1,back:1,b:6,h:0,face:'n'});mv('v1',431,668,1300,'cubic-bezier(.45,0,.9,.6)',.23);foot(1200,260);await w(1300);
G.v1.style.transition='opacity .25s';P('v1',{show:0});$('#ff').style.opacity=.15;await w(120);$('#ff').style.opacity=.85;
/* 9 尼尔刹住，喘息，也拐进去 */
await w(450);
mv('nl',560,790,700,'ease-out');await w(700);bob('nl',0);
pose('nl','brake',{f:1,face:'hard'});bs(.5,.45,2600);dust(570,792,1);await w(800);
pose('nl','pant',{f:1,face:'hard'});await w(1400);
pose('nl','pant',{f:1,face:'hard',look:6});await w(900);
pose('nl','corner',{f:1,face:'hard'});stp();await w(380);
pose('nl','run',{f:1,back:1,b:6,h:0,face:'n'});mv('nl',432,668,1400,'cubic-bezier(.45,0,.9,.6)',.23);foot(1300,260);await w(1400);
P('nl',{show:0});$('#ff').style.opacity=.15;await w(150);$('#ff').style.opacity=.85;
await w(2200);going=false;cancelAnimationFrame(raf)}},
