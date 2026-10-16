{n:'第一日酒馆',sub:'酒馆 · 白天 · 吧台两侧',sh:'酒馆',pos:[520,600],bg:'#0b0a0c',set:'#set3',lx:900,i:['40%','60%'],mk:()=>SETS.barDay(),
ini(){$('#front').innerHTML=BDC;$('#front').style.opacity=0;P('bm',{d:0,show:0,x:1050,y:790,s:.6,f:1,face:'n'});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'n'})},
/* 白天，客人稀疏。熟不熟全靠动作：酒保没抬头就递来一杯水，尼尔不用开口就接住 */
async run(k,{w,n,bm,at,go,murmur}){
$('#front').style.opacity=1;hum([[1480,790,.7,-1],[290,790,.7,1]]);mp(.1);sp(2,900,1.4,.08,'#f3d08a');const mu=murmur(.2);
dolly(1.1,55,58,0);
/* 酒保擦杯口，放回架子 */
await w(900);await wipeGlass(w,3);await shelveGlass(w);
/* 尼尔推门进来，站到吧台外侧；酒保头也不抬，推过来一杯水 */
P('nl',{show:1,d:0});pose('nl','stand',{face:'n'});go('nl',540,2600);await w(2300);
pose('bm','push',{f:1,face:'n'});gl('gw',933,660,1,0);await w(150);gl('gw',690,660,1,700,0,'ease-out');await w(700);pose('bm','idleDown',{f:1,face:'n'});
pose('nl','take',{face:'n'});gl('gw',663,606,1,450);await w(600);
await bm('来了，最近侦探的活儿怎么样？');
pose('nl','shrug',{face:'wry'});
await n('和之前差不多，没什么活儿可做。今天酒吧还需要人手吗？');
gl('gw',672,660,1,500);pose('nl','stand',{face:'n'});
wipeGlass(w,2);
await bm('还是老样子，帮我们看看场子，做完就结钱。');
/* 酒保这才放下杯子，抬头，认真说这一句 */
gl('gl',923,660,1,300);pose('bm','idleDown',{f:1,face:'n',h:-2,b:6});
await bm('你如果真的缺钱的话，还是得找份正经的工作，这些零工是攒不到什么钱的。');
/* 尼尔沉默：镜头给他的脸 */
dolly(1.3,36,58,3000);pose('nl','stand',{face:'sad',h:16});
await n('...');await w(1400);
await n('我再试试，再等等吧。');
/* 他自己先绷不住：抬头，带点自嘲的劲儿 */
pose('nl','shrug',{face:'wry',h:-4});
await n('说不定今天晚上就有什么重要的侦探委托，福尔摩斯那种，现在放弃不就错过了？');
/* 两个人都笑了：肩膀轻抖，镜头拉开 */
pose('nl','stand',{face:'smile',h:4});pose('bm','idleDown',{f:1,face:'smile',h:6});dolly(1.1,55,58,3800);
for(let i=0;i<4;i++){tn(300+i*20,.12,'triangle',.04);bs(.08,.18,900);P('nl',{h:i%2?-2:5,d:260});P('bm',{h:i%2?9:3,d:260});await w(420)}
await w(1800);mu.stop()}},
