{n:'经理 · 解雇',sub:'剧院 · 经理找尼尔谈调查进展',sh:'解雇',pos:[1100,90],bg:'#1b1015',set:'#set3',lx:690,i:['30%','60%'],mk:()=>SETS.poster(),
ini(){$('#props').innerHTML=PADP;P('mg',{d:0,x:540,s:.58,f:0,face:'smile'});P('nl',{d:0,x:1050,s:.56,f:1,face:'n',h:3})},
async run(k,{w,go,fl,n,m}){
await w(500);sp(2,690,1.3,.2,'#e0bd70');P('mg',{show:1,d:600});P('nl',{show:1,d:600});await w(1200);
go('mg',620,900);await m('尼尔，我们必须得好好谈谈了。');cc();await w(1800);
P('mg',{ar:-35,al:30});await m('到目前为止已经有几天了，事情有什么重要的进展吗？');cc();P('nl',{h:8});await w(2300);
P('mg',{ar:-30,al:30});await m('我只针对事情，不针对人。这是一份工作，我们每个人都在自己的岗位上，为这件事出一份力。');
P('mg',{ar:42,al:0});P('nl',{h:12,b:-3});await m('就拿我来说，我每天都在为赞助和演出奔走，如果我没能做得很好，我自己就不配呆在这个岗位上，我就要开除我自己。');
cc();await w(1500);P('mg',{ar:-30,al:30});go('mg',670,700);await m('如果我们不能为这件事情的解决作出贡献，那我们在这儿不是浪费时间吗？');
cc();P('nl',{h:16,b:-5,face:'sad'});await w(2000);
P('mg',{face:'smile',h:-4,ar:-30,al:30});await m('尼尔，我其实很不情愿，但是这件事情你可以先放一放了，我可能会找一个更老练的侦探。但也别那么绝情，别断了联系。');
cc();P('mg',{ar:0,al:0,h:0});P('nl',{face:'hard'});await w(2400);
P('nl',{ar:-70,b:0,h:6,face:'n',x:900});mvp(790,470,1);await w(900);P('mg',{ar:-62,x:640});await w(700);mvp(740,470,0);await w(600);P('nl',{ar:0});P('mg',{ar:0});await w(900);
P('nl',{f:0,face:'n',h:6});go('nl',1500,3600);await w(1200);op('#dl2',.9);$('#lf2').style.transform='scaleX(.1)';await w(2000);P('nl',{show:0,d:500});await w(900);
$('#lf2').style.transitionDuration='.1s';$('#lf2').style.transform='scaleX(1)';op('#dl2',0);slam();$('#stage').classList.add('shk');setTimeout(()=>$('#stage').classList.remove('shk'),450);
await w(1400);P('mg',{face:'n',h:-5});await w(1300);P('mg',{h:0,face:'smile'});await w(2000)}},
