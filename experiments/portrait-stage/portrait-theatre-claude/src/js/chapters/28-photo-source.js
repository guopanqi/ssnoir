{n:'照相馆 · 查来源',sub:'照相馆柜台 · 查照片来源',sh:'查片',pos:[1370,860],bg:'#0a0809',set:'#set3',lx:900,i:['40%','66%'],mk:()=>SETS.photoshop(),
ini(){$('#props').innerHTML=PSP;$('#front').innerHTML=PCOUNTER;$('#front').style.opacity=0;P('nl',{d:0,x:-150,s:.56,face:'n'});P('sk',{d:0,show:0,x:1070,y:790,s:.58,f:1,face:'n',b:4,h:6,ar:-24})},
async run(k,{w,go,n,s,at,sigh,ticks}){
await w(500);$('#front').style.opacity=1;P('sk',{show:1,d:600});sp(2,900,1.2,.1,'#ff7a6a');
/* 1 回到柜台，把勒索照片放在店主面前 */
bell();P('nl',{show:1,d:0,ar:-30,b:3});go('nl',650,2200);await w(2400);dolly(1.04,62,70,40000);
P('nl',{ar:-70,b:5});await w(500);op('#rp',1);tn(500,.05,'triangle',.04);bs(.05,.2,900);await w(500);P('nl',{ar:0,b:0});
P('sk',{face:'smile',h:-2,ar:-24});await n('老板，我这儿有张照片想请你看看。');
P('sk',{face:'n',h:2});await s('要洗照片吗？');
/* 2 没取出胶卷，手指轻压照片边缘 */
P('nl',{ar:-72,b:6,h:6});await w(700);dolly(1.1,60,68,7000);
await n('我只是问问，这张是不是也在你们这儿洗的？');
/* 3 店主看一眼，目光移开，手停在照片旁，迟疑 */
P('sk',{ar:-50,b:8,h:8,face:'n'});await w(700);P('sk',{look:-7,face:'sad',ar:-46});ticks(4,700);lamp(.8);await w(1700);
await s('这、这我怎么知道……');
/* 4 恼火 */
P('sk',{look:0,face:'hard',b:-8,h:-4,ar:-30,al:30});go('sk',1040,600);tn(140,.3,'sawtooth',.04);sp(2,900,1.2,.2,'#ff5a4a');await w(500);
await s('你到底想问什么？');
/* 5 演出结束：不展示订单、取件人或调查结论 */
P('nl',{h:3});ticks(3,800);await w(1800);dolly(1,50,66,3500);await w(900)}},
