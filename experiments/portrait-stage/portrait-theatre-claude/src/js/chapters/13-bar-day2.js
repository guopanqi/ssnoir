{g:'S 系列',n:'第二日酒馆',sub:'酒馆 · 白天 · 和昨天不一样',sh:'再访',pos:[300,800],bg:'#0b0a0c',set:'#set3',lx:900,i:['40%','60%'],mk:()=>SETS.barDay(),
ini(){$('#front').innerHTML=BDC;$('#front').style.opacity=0;P('bm',{d:0,show:0,x:1050,y:790,s:.6,f:1,face:'n'});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'hard'})},
/* 复用第一日的吧台和擦杯开场，但今天：光更冷、人更少、取景更近（整场固定，不运镜）；尼尔走得快，不碰那杯水，一进门就开口 */
async run(k,{w,n,bm,at,go,murmur}){
$('#front').style.opacity=1;hum([[1480,790,.7,-1]]);mp(.05);const mu=murmur(.12);
dolly(1.3,50,58,0);
await w(700);const wp=wipeGlass(w,2);
/* 尼尔大步进来；酒保照旧推过来一杯水，他没碰 */
P('nl',{show:1,d:0});pose('nl','stand',{face:'hard'});go('nl',540,1700);await w(1000);
pose('bm','push',{f:1,face:'n'});gl('gl',930,660,0,0);gl('gw',933,660,1,0);await w(150);gl('gw',690,660,1,700,0,'ease-out');await w(900);
pose('bm','idleDown',{f:1,face:'n',h:2});gl('gl',923,592,1,0);
await bm('来了，今天还是——');
/* 尼尔不等他说完 */
pose('nl','point',{face:'hard',h:0});
await n('我接了个委托。');
/* 酒保停住手里的活，杯子停在半空 */
pose('bm','wipe',{f:1,face:'n',h:2});await w(700);
await bm('又是哪个婚外情 ？');
pose('nl','stand',{face:'hard',h:0});
await n('勒索案。');
/* 酒保把杯子放下，凑过来 */
gl('gl',923,660,1,300);mv('bm',900,790,800,'ease-out');pose('bm','idleDown',{f:1,face:'n',h:-2,b:10});await w(900);
await bm('大案子。你准备怎么做？');
pose('nl','stand',{face:'sad',h:6});
await n('你这儿有挣得多一点的活吗？我需要一笔钱。');
/* （调酒师洗杯子）——水声，他不看人；尼尔在旁边等，镜头一直停在尼尔脸上 */
mv('bm',1050,790,700,'ease-out');pose('bm','idleDown',{f:1,face:'n',h:18,b:6});gl('gl',1010,652,1,0);
for(let i=0;i<5;i++){bs(.45,.14,2600);gl('gl',1004+(i%2)*10,652,1,260,i%2?5:-5);await w(560)}
pose('nl','stand',{face:'sad',h:12});await w(400);
pose('bm','idleDown',{f:1,face:'smile',h:12,b:4});
await bm('没有。有的话，我早就自己干了。');
await w(900);
pose('bm','shrug',{f:1,face:'n',h:-2});pose('nl','stand',{face:'n',h:4});
await bm('要不去码头看看？来钱快的零工就那么几样，不是脏就是累。');
/* 最后这句压低声音：他凑近，往客人那边瞟一眼；镜头压向尼尔，看他听进去 */
mu.set(.03);mv('bm',900,790,800,'ease-out');pose('bm','idleDown',{f:1,face:'n',h:-4,b:10});
at(3300,()=>pose('nl','stand',{face:'hard',h:2}));
await bm('最近几天晚上也有一些小道消息。你看场子的时候多听听，说不定会有些委托，也能赚上一笔。');
/* 他慢慢点头，这才拿起那杯一直没碰的水，一口喝完 */
pose('nl','take',{face:'hard',h:4});gl('gw',663,606,1,500);await w(900);
pose('nl','take',{face:'hard',h:-14});await w(800);tn(2300,.1,'sine',.04);
gl('gw',672,660,1,400);pose('nl','stand',{face:'hard',h:0});await w(3800);mu.stop()}},
