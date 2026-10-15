{n:'序幕夜',sub:'贝尔维尔 · 不夜城 · 酒吧、街口、旅馆',sh:'序幕',pos:[260,540],bg:'#0a0709',set:'#set3',lx:760,i:['40%','60%'],hardOut:1,mk:()=>SETS.barBooth(),
ini(){$('#front').innerHTML=BTBL+GL('gl');$('#front').style.opacity=0;P('nl',{d:0,show:0,x:760,y:812,s:.62,f:0,face:'drunk'});P('ho',{d:0,show:0,x:940,y:790,s:.56,f:1,face:'n'})},
/* 四个镜头：卡座 → 吧台 → 酒吧门外 → 旅馆大堂；镜头之间全是硬切（瞬黑 1.5 秒、瞬亮），没有任何渐变 */
async run(k,{w,n,ho,cut,scene,murmur,clink,at}){
const sw=(on,big)=>{const c=G.nl.classList;c.toggle('swy',on&&!big);c.toggle('swy2',on&&!!big)};
const hic=()=>{tn(190,.1,'square',.05);tn(140,.14,'square',.04,.08)},rt=()=>{bs(.45,.5,320);tn(120,.5,'sawtooth',.05);tn(80,.6,'sine',.2,.1)};
/* ── 镜头一：卡座。人很多，嘈杂；镜头从远处慢慢推向尼尔 ── */
$('#front').style.opacity=1;
hum([[150,790,.8,1],[270,790,.7,-1],[380,790,.9,1],[490,790,.75,-1],[1040,790,.8,-1],[1150,790,.95,1],[1270,790,.75,-1],[1390,790,.85,1],[1500,790,.7,-1]]);
let mu=murmur(1);sp(2,760,1.3,.14,'#f3d08a');
pose('nl','stand',{show:1,y:800,f:0,face:'drunk',b:6,h:10});gl('gl',830,668,1,0);
dolly(1.5,50,56,11000);
await w(2400);
/* 先一口干了，再把空杯高高举起来 */
pose('nl','toast',{face:'drunk',h:-18});gl('gl',830,472,1,0,28);tn(2100,.08,'sine',.04);await w(1100);
pose('nl','toast',{face:'smile',h:-4});gl('gl',822,488,1,0,0);mu.set(1.8);
await n('来，再来一杯！');
clink();await w(1300);
/* ── 瞬黑 1.5 秒 → 吧台 ── */
mu.stop();
await cut(()=>{scene(()=>SETS.barCounter(),BCNT+GL('gl'),'');hum([[260,790,.7,1],[1300,790,.8,-1],[1430,790,.7,1]]);dolly(1,50,60,0);
 pose('nl','slump',{show:1,x:800,y:790,s:.66,f:0,face:'drunk',ar:-20,al:10,b:22,h:30});gl('gl',930,640,1,0);sp(2,800,1.1,.1,'#ff8a96');mu=murmur(.45)});
dolly(1.5,50,57,12000);
await w(900);
await n('私家侦探？我告诉你吧，听着像那么回事，但假如没有名声，就什么也不是！');
pose('nl','slump',{face:'wry',h:38,ar:-20,al:10,b:22});await w(1700);
/* 对面不知问了句什么；他抬一下手，又垂下去 */
pose('nl','slump',{face:'drunk',h:22,ar:-20,al:10,b:20});gl('gl',930,640,1,300);await w(500);
await n('我怎么知道？我有一个朋友.......');
pose('nl','slump',{face:'sad',h:44,ar:-20,al:10,b:30});gl('gl',930,640,1,1500,14);mu.set(.3);await w(2200);
/* ── 瞬黑 1.5 秒 → 酒吧门外 ── */
mu.stop();
await cut(()=>{scene(()=>SETS.pubdoor2(),'',PUD);dolly(1,50,60,0);op('#pdl',.95);
 pose('nl','dHang',{show:0,x:612,y:790,s:.56,f:0,face:'drunk'});sp(2,640,1.2,.1,'#ff8a96')});
mu=murmur(.35);piano();
pose('nl','dHang',{show:1,f:0,face:'drunk'});sw(true);await w(1200);
/* 出门，摇摇晃晃走到墙边 */
pose('nl','dWalk',{f:0,face:'drunk'});mv('nl',700,790,2400,'ease-in-out',.56);
for(let t=0;t<2400;t+=600){stp();await w(600)}
op('#pdl',.3);mu.stop();sw(false);pose('nl','dLean',{f:0,face:'drunk'});await w(1000);hic();await w(900);
/* 推墙，再走两步 */
pose('nl','dWalk',{f:0,face:'drunk'});sw(true);mv('nl',885,790,1700,'linear');
for(let t=0;t<1700;t+=425){stp();await w(425)}
/* 扶住灯柱，吐了；镜头压近 */
sw(false);pose('nl','dLean',{f:0,face:'drunk'});dolly(1.3,56,62,6000);await w(700);
pose('nl','retch',{f:0,face:'drunk'});op('#pud',1);G.nl.classList.add('swy2');rt();await w(900);rt();await w(900);rt();await w(1000);
G.nl.classList.remove('swy2');pose('nl','dLean',{f:0,face:'drunk',h:22});await w(1800);
/* ── 瞬黑 1.5 秒 → 旅馆大堂 ── */
await cut(()=>{scene(()=>SETS.hotelLobby(),LOBF,'');dolly(1,50,60,0);
 pose('ho','sitHung',{show:1,x:940,y:790,s:.56,f:1,face:'n'});pose('nl','dWalk',{show:1,x:230,y:800,s:.6,f:0,face:'drunk'});sp(2,900,1.2,.08,'#f3d08a')});
sw(true);mv('nl',1160,800,4200,'linear');
const stps=(async()=>{for(let t=0;t<4200;t+=525){stp();await w(525)}})();
await w(2200);
/* 他经过柜台，老板抬头；尼尔不回应，也不停 */
pose('ho','stand',{f:1,face:'n',h:-2});tn(260,.15,'triangle',.03);dolly(1.3,58,56,1800);
await ho('尼尔？');cc();
pose('ho','stand',{f:0,face:'sad',h:2});
await stps;
/* 上楼：斜着往右上走，越走越远，隐进黑里 */
dolly(1.25,86,52,3600);pose('nl','dWalk',{f:0,face:'drunk'});mv('nl',1470,500,3400,'linear',.5);P('nl',{show:0,d:3400});
for(let t=0;t<3400;t+=480){stp();await w(480)}
sw(false);await w(900);
/* ── 描线渐隐 ── */
$('#front').style.transition='opacity 1.8s';$('#front').style.opacity=0;P('ho',{show:0,d:1800});SET.classList.remove('on','f');dolly(1,50,60,2400);await w(2800)}},
