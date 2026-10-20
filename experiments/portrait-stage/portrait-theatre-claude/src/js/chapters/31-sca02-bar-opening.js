{g:'SC-A 系列',n:'SC-A02 酒保与夜莺',sub:'酒馆 · 吧台与小舞台 · 夜莺第一次登台',sh:'酒保夜莺',pos:[800,620],bg:'#0a0709',set:'#set3',lx:700,i:['40%','60%'],mk:()=>SETS.barNight(),
ini(){$('#front').innerHTML=BNF;$('#front').style.opacity=0;P('bm',{d:0,show:0,x:600,y:790,s:.6,f:1,face:'n'});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'n'});P('ny',{d:0,show:0,x:1175,y:700,s:.54,f:0,face:'n',hat:0,bag:0,umo:0,umc:0})},
/* 有酒吧的劲儿：对白紧凑（pace .8），酒保手上一直有活，伴奏、人群、烟雾都在动；镜头固定在稍远处，只在夜莺听到掌声时慢慢推近，再拉开 */
async run(k,{w,n,bm,at,go,murmur,pace,jazz,vocal}){
Object.assign(POSES,{micAdj:{ar:-125,al:6,b:2,h:0},singP:{ar:-20,al:12,b:-2,h:-4}});
$('#front').style.opacity=1;
const crowd=hum([[930,790,.95,1],[1010,790,1.05,-1],[1100,790,.9,1],[1430,700,.7,-1],[1490,700,.72,-1]]);
$('#props').insertAdjacentHTML('beforeend','<g class="st"><path d="M1000 650q-8 -14 0 -28t0 -28"/></g><g class="st" style="animation-delay:.8s"><path d="M920 650q-8 -14 0 -28t0 -28"/></g>');
sp(2,600,1.4,.1,'#f3d08a');const mu=murmur(.55);pace(.8);
/* 酒保手上一直有活：擦杯子（杯子在手边飞快地晃） */
let wiping=false;const wipe=async()=>{wiping=true;pose('bm','wipe',{show:1,f:1,face:'n'});try{while(wiping&&k===gen){gl('gl',480,586,1,170,10);await w(200);gl('gl',466,594,1,170,-10);await w(200)}}catch(e){}};
const rest=(p,o)=>{wiping=false;gl('gl',560,650,1,220);pose('bm',p,o)};
gl('gw',395,650,1,0);wipe();
await w(500);P('nl',{show:1,d:0});pose('nl','stand',{face:'n'});go('nl',230,1700);await w(1200);
await bm('又没活儿了？');
pose('nl','shrug',{face:'wry'});
await n('有就不来问你了。');
rest('point',{f:1,face:'smile'});
await bm('码头，咖啡馆都有零工可做 。');
pose('nl','stand',{face:'n',h:4});
await n('你这儿呢？');
pose('bm','talkA',{f:1,face:'n'});
/* 夜莺从幕布后走出来，黑着灯走到话筒前；酒保这句说完，台上响起话筒的噪声 */
at(900,async()=>{pose('ny','stand',{show:1,d:500,f:0,face:'n'});go('ny',1330,1500)});
await bm('还是和以前一样，看场子，有人闹事就你来处理。至于平常的工钱，你知道。');
pose('nl','shrug',{face:'sad'});
pose('ny','micAdj',{f:1,face:'n'});bs(.18,.55,6000);tn(1800,.35,'sawtooth',.03);tn(120,.2,'square',.05);
await w(500);
pose('bm','idleDown',{f:0,face:'n',h:-6});pose('nl','stand',{f:0,face:'n',h:-2});await w(800);
/* 追光亮起；她把话筒扶正，往前站一步；伴奏起，过两小节 */
op('#spt',1);op('#spg',.4);sp(1,1290,1.2,.16,'#f3d08a');mu.set(.2);
const jz=jazz(112,.7);
await w(700);mv('ny',1285,700,500,'ease-out');pose('ny','singP',{f:1,face:'n'});
const beat=async()=>{try{for(let i=0;k===gen&&i<40;i++){P('bm',{b:i%2?7:3,d:230});await w(536)}}catch(e){}};beat();
await w(3400);
pose('ny','singP',{f:1,face:'n',h:-9});await w(700);
/* 开口唱：镜头保持稍远，台上台下都看得见 */
const M1=[[440,1],[493.9,1],[523.3,2],[493.9,1],[440,1],[392,2],[329.6,1],[392,1],[440,2],[0,1],[523.3,1],[587.3,1.5],[523.3,.5],[493.9,2],[440,3]];
const sing=vocal(M1,536,1);
at(5200,()=>pose('ny','singHi',{f:1,face:'n',ar:-34,al:-4,h:-10}));
await w(sing+200);
/* 一曲唱完：掌声、欢呼；镜头慢慢推到她侧脸，她听见掌声，笑了一下 */
cheer(crowd,2400);pose('ny','singP',{f:1,face:'smile',h:2});dolly(1.6,80,56,3200);
await w(4200);
/* 拉开，回到吧台这边 */
dolly(1,50,60,2800);jz.set(.3);pace(1);await w(900);
pose('nl','stand',{f:0,face:'n',h:-4});
await n('她是谁？');
pose('bm','idleDown',{f:0,face:'n',h:-4,b:3});
await bm('夜莺。就快去大剧院了。');
pose('bm','idleDown',{f:1,face:'n',h:0});pose('nl','stand',{f:0,face:'wry',h:2});
await n('那里会要我们这儿的人？');
pose('bm','shrug',{f:1,face:'smile'});
await bm('也许是她唱得好吧。');
/* 夜莺向乐手点头，继续下一段；酒保收走尼尔面前的空杯 */
jz.set(.7);pose('ny','singP',{f:1,face:'n',h:8});await w(500);pose('ny','singP',{f:1,face:'n',h:-6});
vocal([[440,1],[523.3,1],[587.3,2],[523.3,1],[493.9,1],[440,2],[392,1],[440,1],[329.6,3]],536,.8);
pose('bm','push',{f:1,face:'n'});gl('gw',470,640,1,420,0,'ease-out');await w(480);gl('gw',470,640,0,200);pose('bm','idleDown',{f:1,face:'n'});
pose('nl','stand',{f:0,face:'smile',h:2});
await w(4200);jz.set(.15);await w(1200);jz.stop();mu.stop()}},
