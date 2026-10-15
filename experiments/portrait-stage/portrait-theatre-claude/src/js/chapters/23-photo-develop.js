{n:'照相馆 · 冲印',sub:'照相馆柜台 · 冲印与发现压纹',sh:'冲印',pos:[1460,840],bg:'#0a0809',set:'#set3',lx:900,i:['40%','66%'],mk:()=>SETS.photoshop(),
ini(){$('#props').innerHTML=PSP;$('#front').innerHTML=PCOUNTER;$('#front').style.opacity=0;P('nl',{d:0,x:-150,s:.56,face:'n'});P('sk',{d:0,show:0,x:1070,y:790,s:.58,f:1,face:'n',b:4,h:6,ar:-24})},
async run(k,{w,go,n,s,mono,at,sigh,ticks}){
/* 取证胶卷数：有取证胶卷时 2（两卷），否则 1（一卷，只放家庭胶卷）。游戏接入时由外部传入 */
const ROLLS=2;
await w(500);$('#front').style.opacity=1;P('sk',{show:1,d:600});sp(2,900,1.2,.1,'#ff7a6a');
/* 1 进店、放胶卷 */
bell();P('nl',{show:1,d:0});go('nl',650,2200);await w(2400);dolly(1.04,62,70,40000);
P('sk',{h:0,ar:-24});await w(500);
P('nl',{ar:-70,b:4});op('#f1',1);tn(900,.05,'triangle',.04);
if(ROLLS==2){at(1700,()=>{op('#f2',1);tn(900,.05,'triangle',.04)});await n('这卷替我洗出来。还有这一卷。')}else await n('这卷替我洗出来。');
P('nl',{ar:0,b:0});
/* 2 店主收下，写取件单，推回 */
P('sk',{ar:-62,b:8});await w(500);op('#f1',0);op('#f2',0);tn(300,.05,'triangle',.04);await w(400);
P('sk',{ar:-30,b:12,h:16});for(let i=0;i<5;i++){bs(.04,.15,3200);await w(260)}await w(500);
P('sk',{ar:-50,b:6,h:4,al:0});op('#tk',1);await w(600);$('#tk').style.transform='translate(-190px,0)';await w(500);
await s('拿着取件单，别丢了。');
/* 3 冲印完成的画面过渡：暗下去、红灯、滴水、钟摆，再亮起 */
cc();fxf('brightness(0)',500);await w(650);
op('#tk',0);$('#tk').style.transform='';P('sk',{ar:-24,b:4,h:6});P('nl',{ar:0,b:0});
for(let i=0;i<7;i++){tn(i%2?1500:1250,.03,'square',.03);if(i==2||i==5)tn(2400,.04,'sine',.03);await w(380)}
op('#phs',1);P('nl',{ar:-72,b:4});fxf('none',900);sp(2,900,1.3,.2,'#ff5a4a');await w(1100);sp(2,900,1.2,.08,'#ff7a6a');
/* 4 接过照片，翻到背面，视线停在压纹上 */
op('#phs',0);P('nl',{ar:-30,b:6,h:10});await w(600);inset('bk',700,330,1.25,1,600);tn(660,.12,'triangle',.05);dolly(1.1,58,66,6000);await w(2200);
P('nl',{ar:0,h:4,b:2});inset('bk',700,330,1.25,.0,300);P('nl',{face:'wry'});await n('我能看看你们的相纸吗？');
P('sk',{ar:-62,face:'smile',h:-2});op('#pp',1);await s('喏，最好的纸张。');P('sk',{ar:-24,face:'n',h:6});
/* 5 摸过纸背，对比压纹 */
P('nl',{ar:-70,b:6,h:10});await w(700);inset('bk2',700,330,1.25,1,500);tn(700,.1,'triangle',.05);await w(1300);
inset('bk',560,330,1.0,1,500);inset('bk2',840,330,1.0,1,500);await w(900);
await n('这背面的压纹，是原来就有的吗？');
P('sk',{face:'n',h:8,look:-6});await s('嗯，应该是吧。');
/* 6 若有所思：夜莺那张照片的背面浮现 */
P('nl',{ar:0,h:8,b:3,face:'n'});P('sk',{look:0});lamp(.7);ticks(4,900);
inset('bk3',700,330,1.0,.5,900);at(1400,()=>inset('bk3',700,330,1.0,.2,800));
await mono('夜莺给我看的那张照片背面，好像也有这个。');
inset('bk',560,330,1,0,600);inset('bk2',840,330,1,0,600);inset('bk3',700,330,1,0,600);dolly(1,50,66,3500);await w(1500)}},
