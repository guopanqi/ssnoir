s=open('/tmp/merge2.py',encoding='utf-8').read()
def rep(a,b):
    global s
    assert s.count(a)==1,(s.count(a),a[:70])
    s=s.replace(a,b)
rep("'十六','十七']","'十六','十七','十八','十九']")
rep("cl:{tk:0,wk:0,sob:0}","cl:{tk:0,wk:0,sob:0,run:0}")
rep(".st,.cg .sm{",".run .wb{animation:wlk .22s ease-in-out infinite alternate}.run .aL{animation:swL .24s ease-in-out infinite alternate}.run .aR{animation:swR .24s ease-in-out infinite alternate}@keyframes swL{from{transform:rotate(-50deg)}to{transform:rotate(50deg)}}@keyframes swR{from{transform:rotate(50deg)}to{transform:rotate(-50deg)}}.st,.cg .sm{")
NEW=r'''
const SIL=(x,y,s,f=1,fill='#05070d')=>`<g transform="translate(${x} ${y}) scale(${s*f} ${s})"><circle cx="0" cy="-190" r="20" fill="${fill}" stroke="#4a5f8c" stroke-width="2"/><polygon points="-24,-166 24,-166 36,-76 24,-70 20,0 5,0 0,-56 -5,0 -20,0 -24,-70 -36,-76" fill="${fill}" stroke="#4a5f8c" stroke-width="2"/></g>`;
const crowd=(n,dir=1,y0=790)=>{const g=$('#props');for(let i=0;i<n;i++){const sc=.42+Math.random()*.26,fx=dir>0?-120:1720,tx=dir>0?1720:-120;g.insertAdjacentHTML('beforeend',`<g>${SIL(0,y0+Math.random()*14,sc,i%2?1:-1)}</g>`);g.lastElementChild.animate([{transform:`translate(${fx}px,0)`},{transform:`translate(${tx}px,0)`}],{duration:8000+Math.random()*4000,delay:i*650,fill:'both'})}};
const swoosh=(y,sc,dur=520,dir=1)=>{const g=$('#front');g.insertAdjacentHTML('beforeend',`<g>${SIL(0,y,sc,dir)}</g>`);const e=g.lastElementChild;e.animate([{transform:`translate(${dir>0?-260:1860}px,0)`},{transform:`translate(${dir>0?1860:-260}px,0)`}],{duration:dur,easing:'ease-in'}).onfinish=()=>e.remove();bs(.12,.5,400)};
const rnd=i=>{const v=Math.sin(i*127.1)*43758.5453;return v-Math.floor(v)};
SETS.street=()=>{let cw='';for(let i=0;i<7;i++)cw+=LL(`M${740+i*48} 830h26`,'',1+i*.05,6,'#46608f');
return masked(LL('M0 790H1600','',0,1.5,'#5a6f9c')+LL('M0 806H1600','',.4,1,'#33466e')+cw)
+RR(670,560,60,190,'s',.8,2.5,'#e6eeff')+LL('M670 560Q700 520 730 560','s',.9,2.5,'#e6eeff')+RR(680,592,40,8,'',1.1,1.5,'#9fb4e0')+LL('M690 750V790M710 750V790','',1.2,2,'#9fb4e0')+'<text x="700" y="668" text-anchor="middle" font-size="24" fill="none" stroke="#9fb4e0" font-family="serif">邮</text><rect id="slot" x="680" y="592" width="40" height="8" fill="#ffd98a" opacity="0" style="transition:opacity .3s"/>'
+RR(1110,430,320,36,'s',1,3,'#e6eeff')+LL('M1150 466v28M1190 466v28M1230 466v28M1270 466v28M1310 466v28M1350 466v28M1390 466v28','',1.3,1.5,'#9fb4e0')+RR(1150,500,240,110,'s',1.4,2,'#9fb4e0')+LL('M1170 520h90M1170 540h90M1170 560h90M1290 520h80M1290 540h80M1290 560h80','',1.7,1.5,'#6f86b8')+RR(1130,620,280,170,'s',1.5,2.5,'#cfe0ff')+LL('M1130 640H1410','',1.8,1.5,'#7f95c4')
+'<circle class="lh" cx="1270" cy="496" r="70" fill="#f3d08a" opacity="0" style="transition:opacity .2s"/><circle class="lb" cx="1270" cy="494" r="7" fill="#ffe6a8" opacity="0" style="transition:opacity .15s"/>'};
SETS.chase=()=>{let f='',m='',n='';for(let i=0;i<14;i++){const h=170+rnd(i)*260,wd=70+rnd(i+9)*40;f+=`<rect x="${i*115}" y="${790-h}" width="${wd}" height="${h}" fill="#05070e" stroke="#2a3a60" stroke-width="2"/>`}
for(let i=0;i<4;i++)m+=`<line x1="${i*400+60}" y1="790" x2="${i*400+60}" y2="300" stroke="#3b4a70" stroke-width="5"/><circle cx="${i*400+60}" cy="296" r="9" fill="#ffe6a8" opacity=".7"/>`;
for(let i=0;i<18;i++)m+=SIL(i*90+rnd(i+3)*40,790,.5+rnd(i+5)*.28,i%2?1:-1);
for(let i=0;i<8;i++)n+=`<line x1="${i*200}" y1="850" x2="${i*200+90}" y2="850" stroke="#46608f" stroke-width="5"/>`;
const rep=c=>`<g><g transform="translate(-1600 0)">${c}</g>${c}</g>`;
return '<g id="stat" style="transition:transform 1.7s cubic-bezier(.7,0,.9,.5)">'+SETS.street()+'</g><g id="scr" style="opacity:0;transition:opacity .4s">'+rep(f)+rep(m)+rep(n)+'<line x1="-100" y1="790" x2="1700" y2="790" stroke="#5a6f9c" stroke-width="1.5"/></g>'
+'<g id="alw" style="transform:translateX(-2000px);transition:transform 3s cubic-bezier(.15,.7,.2,1)"><rect x="-100" y="190" width="980" height="600" fill="#05070e" stroke="#cfe0ff" stroke-width="2.5"/><rect x="330" y="320" width="140" height="470" fill="#000" stroke="#7f95c4" stroke-width="2"/><rect x="40" y="260" width="70" height="90" fill="none" stroke="#6f86b8" stroke-width="2"/><rect x="170" y="260" width="70" height="90" fill="#8a6a38" fill-opacity=".5" stroke="#6f86b8" stroke-width="2"/><rect x="560" y="260" width="70" height="90" fill="none" stroke="#6f86b8" stroke-width="2"/><rect x="700" y="260" width="70" height="90" fill="none" stroke="#6f86b8" stroke-width="2"/><rect x="560" y="440" width="70" height="90" fill="none" stroke="#6f86b8" stroke-width="2"/></g>'};
const EP='<g id="ep" style="opacity:0;transition:opacity .3s"><rect x="696" y="578" width="30" height="20" fill="#e8e2d2" stroke="#8a8372" transform="rotate(-14 706 588)"/></g>';
const BENCH='<g id="bch"><path d="M985 705H1135V790H985Z" fill="#07080c" stroke="#9fb4e0" stroke-width="2.5"/><path d="M985 705H1135" stroke="#f0f4ff" stroke-width="3"/></g>';
const NEWSP='<g id="np" style="transition:transform .7s ease,opacity .4s;opacity:0" transform="translate(0 130)"><rect x="1020" y="528" width="102" height="118" fill="#d8d2bc" stroke="#6a6450" stroke-width="2"/><path d="M1032 548h78M1032 562h78M1032 576h50M1032 604h78M1032 618h78M1032 632h60" stroke="#8a846e" stroke-width="3"/><circle cx="1066" cy="576" r="11" fill="#0a0c12"/><g id="ey" style="transition:transform .4s"><ellipse cx="1066" cy="576" rx="8" ry="5.5" fill="#f0ece0"/><circle cx="1066" cy="576" r="3.2" fill="#111"/></g></g>';
'''
rep("const PHOTO=",NEW+"const PHOTO=")
CH34=r'''{n:'邮箱',sub:'街角 · 赎金投递（对白为我拟写）',sh:'邮箱',pos:[1000,230],bg:'#07090e',set:'#set3',lx:1250,i:['38%','66%'],mk:()=>SETS.street(),
ini(){$('#props').innerHTML=LETTER+EP;$('#front').innerHTML=BENCH+NEWSP;mvl(600,620,0);P('ny',{d:0,x:-160,s:.56,face:'sad',ar:30,al:-30});P('nl',{d:0,x:1760,s:.56,f:1,face:'hard'})},
async run(k,{w,go,n,y,nar}){
await w(400);os('.lb',1);os('.lh',.35);sp(2,1250,1.1,.14,'#f3d08a');
P('ny',{show:1,d:0});go('ny',560,2800);P('nl',{show:1,d:0});go('nl',840,3000);crowd(3,-1);await w(3300);
mvl(612,640,1);await y('就是这里。');await n('东西都在里面了？');await y('都在。……尼尔，你确定这样能行？');
await n('我不确定。但他得自己来拿，只要他来拿钱，我有办法弄明白他是谁。');await y('那我现在就投进去？');
await n('投吧。然后别回头，直接走。');await y('你呢？');await n('我去对面买份报纸。');
cc();go('ny',575,900);mvl(616,630,1);await w(1100);
P('ny',{ar:-90,face:'sad'});mvl(676,592,1);await w(900);await w(1400);
mvl(702,592,1);tn(900,.2,'triangle',.05);await w(500);mvl(702,592,0);op('#slot',1);bs(.12,.5,500);tn(150,.3,'sine',.2);await w(400);op('#slot',0);op('#ep',1);P('ny',{ar:0});await w(900);
P('ny',{face:'sad',h:4});P('nl',{h:4,face:'n'});await w(1300);
P('ny',{f:1,h:0,face:'n'});go('ny',-200,4200);await w(2400);
P('nl',{f:0,h:0,face:'hard'});go('nl',1070,3000);await w(3200);P('nl',{f:1});await w(500);
P('nl',{y:830,b:-4,d:700});await w(900);op('#np',1);$('#np').style.transform='translate(0,0)';
await nar('（尼尔在邮箱斜对面的报摊坐下，用报纸挡住眼睛，假装在看报纸。）',2800);
$('#ey').style.transform='translateX(-3px)';crowd(8,-1);await w(2400);$('#ey').style.transform='translate(-3px,1px)';await w(2600);
$('#ey').style.transform='translateX(-3px)';await w(2400)}},
{n:'追逐',sub:'街角 · 有人碰了那个信封',sh:'追逐',pos:[1230,250],bg:'#07090e',set:'#set3',lx:1250,i:['44%','72%'],mk:()=>SETS.chase(),
ini(){$('#props').innerHTML=LETTER+EP;$('#ep').style.opacity=1;mvl(692,585,0);$('#front').innerHTML=BENCH+NEWSP;$('#np').style.opacity=1;$('#np').style.transform='translate(0,0)';$('#ey').style.transform='translateX(-3px)';
P('nl',{d:0,x:1070,y:830,s:.56,f:1,b:-4,face:'hard'});P('v1',{d:0,x:-150,s:.56,f:0,face:'n'})},
async run(k,{w,go}){
const stat=$('#stat'),scr=$('#scr'),alw=$('#alw');
const A=[...scr.children].slice(0,3).map((g,i)=>g.animate([{transform:'translateX(0)'},{transform:'translateX(1600px)'}],{duration:[9000,3800,1800][i],iterations:Infinity}));
let rt=0;const rate=v=>{rt=v;A.forEach(a=>a.playbackRate=v)};rate(0);
const ramp=async(to,ms)=>{const a=rt;for(let i=1;i<=10;i++){rate(a+(to-a)*i/10);await w(ms/10)}};
const rsteps=ms=>{for(let i=0;i<ms/230;i++)setTimeout(stp,i*230)};
await w(500);os('.lb',1);os('.lh',.3);sp(2,1250,1.1,.14,'#f3d08a');sp(1,700,.7,.1,'#9db0e0');
crowd(7,1);await w(2400);
P('v1',{show:1,d:0});go('v1',600,3200);await w(3400);
P('v1',{ar:-85});await w(900);op('#ep',0);mvl(700,590,1);tn(1500,.08,'triangle',.05);await w(800);
P('nl',{face:'shock'});$('#ey').style.transform='translate(0,0)';await w(1000);
$('#np').style.transform='translate(0,220px)';$('#np').style.opacity=0;P('nl',{y:790,b:6,face:'hard',d:500});tn(220,.5,'sawtooth',.06);tn(233,.5,'sawtooth',.05);sp(1,1070,.8,.3,'#ffd9a0');
await w(500);mvl(700,590,0);P('v1',{ar:0,face:'shock',h:-5});await w(900);
$('#bch').style.opacity=0;
P('v1',{f:1,h:0,b:12,cl:{run:1}});P('nl',{b:12,cl:{run:1}});rsteps(2400);stat.style.transform='translateX(1900px)';scr.style.opacity=1;op('#ep',0);
go('v1',560,700,{b:12,cl:{run:1}});P('nl',{x:1000,d:900});await ramp(1.4,1400);
P('nl',{x:820,d:2400});P('v1',{x:520,d:2400});ramp(2.2,1800);swoosh(850,1.15,600,1);await w(1800);
swoosh(850,1.3,520,-1);P('nl',{b:-4,x:880,d:260});await w(500);P('nl',{b:12,x:760,d:1400});rsteps(2400);await ramp(3,1000);
swoosh(860,1.2,480,1);await w(700);P('v1',{h:-18,face:'shock'});swoosh(850,1.35,430,-1);await w(900);
P('nl',{y:762,d:260});await w(300);P('nl',{y:790,d:300});swoosh(850,1.2,500,1);await w(700);P('v1',{h:0});
rsteps(3200);alw.style.transform='translateX(0)';await ramp(.02,2800);
P('v1',{x:400,d:1100,cl:{run:1}});await w(1100);P('v1',{x:400,y:770,s:.32,show:0,d:900,f:1,cl:{run:0}});P('nl',{x:640,d:1200});await w(1300);
P('nl',{cl:{run:0},b:6,h:6});await w(2200);
P('nl',{b:0,h:0,face:'hard'});go('nl',400,1600);await w(900);P('nl',{x:400,y:770,s:.32,show:0,d:900});await w(2000)}},
'''
rep("{n:'烟盒',sub:'赎金交付之后（辨别目标 · 追逐，见游戏）· 桥边'",CH34+"{n:'烟盒',sub:'追丢之后 · 桥边'")
open('/tmp/merge2.py','w',encoding='utf-8').write(s)
