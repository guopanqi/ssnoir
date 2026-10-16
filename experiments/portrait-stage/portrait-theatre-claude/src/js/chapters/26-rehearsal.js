{n:'彩排',sub:'空剧院 · 梦想最后一次纯粹的样子',sh:'彩排',pos:[1500,600],bg:'#07060a',set:'#set3',i:['40%','70%'],mk:()=>SETS.stage(),
ini(){P('ny',{d:0,x:800,s:.62,face:'n',h:-3});P('nl',{d:0,x:1800,s:.56,f:1,face:'n'})},
async run(k,{w,go,fl,n,y,nar,at,sigh,ticks}){
await w(600);os('.cone',.5);os('.lh',.4);sp(2,800,1.4,.3,'#f3d08a');P('ny',{show:1,d:1200});await w(2000);
await y('再来一次。');await w(500);piano();
for(let i=0;i<6;i++){whisp('♪',700+Math.random()*200,450-Math.random()*60,i*.55);whisp('♫',900+Math.random()*120,420,i*.55+.3)}
P('ny',{face:'smile',h:-6,cl:{tk:1}});await w(3800);P('ny',{cl:{tk:0},h:0});await w(1000);
P('nl',{show:1,d:0});go('nl',1330,3000);await w(3200);P('ny',{face:'shock',f:0});await w(700);
dolly(1.04,60,66,26000);await y('你什么时候来的？');P('nl',{face:'n'});await n('第二遍的时候');
P('ny',{face:'n'});go('ny',760,900);await y('我是不是唱得很差');
await n('有一点。');P('ny',{face:'hard'});await w(1400);
P('nl',{face:'smile'});go('nl',1230,1100);await n('但是比第二遍好多了。');
P('ny',{face:'smile',h:-4});await w(900);P('ny',{f:1,face:'n'});await w(900);
await y('但我喜欢这个地方，站在这里的时候，会觉得什么事情都可以发生。');
P('ny',{f:0,face:'smile'});go('ny',640,1500);await y('我在上面的时候看起来怎么样');
P('nl',{face:'n',h:2});dolly(1.09,58,66,6000);await w(1200);await n('很漂亮');
document.querySelectorAll('.ft').forEach((e,i)=>setTimeout(()=>{e.style.opacity=.95},i*140));pad();sp(2,800,1.7,.42,'#ffd9a0');
await w(2600);P('ny',{face:'smile',tr:1,h:0});await y('这是我一直以来的梦想，尼尔，谢谢你');
cc();dolly(1,50,66,5000);await w(4200)}},
