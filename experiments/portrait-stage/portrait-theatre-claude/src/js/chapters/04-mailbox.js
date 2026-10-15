{n:'邮箱',sub:'街角 · 赎金投递（对白为我拟写）',sh:'邮箱',pos:[1000,230],bg:'#07090e',set:'#set3',lx:1250,i:['38%','66%'],mk:()=>SETS.street(),
ini(){$('#props').innerHTML=LETTER+EP;$('#front').innerHTML=BENCH+NEWSP;mvl(600,620,0);P('ny',{d:0,x:-160,s:.56,face:'sad',ar:30,al:-30});P('nl',{d:0,x:1760,s:.56,f:1,face:'hard'})},
async run(k,{w,go,n,y,nar,at,ticks}){
await w(400);os('.lb',1);os('.lh',.35);sp(2,1250,1.1,.14,'#f3d08a');
P('ny',{show:1,d:0});go('ny',560,2800);P('nl',{show:1,d:0});go('nl',840,3000);crowd(3,-1);await w(3300);
dolly(1.05,56,70,18000);mvl(612,640,1);await y('就是这里。');await n('东西都在里面了？');await y('都在。……尼尔，你确定这样能行？');
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
$('#ey').style.transform='translateX(-3px)';crowd(8,-1);dolly(1.1,70,76,9000);ticks(6,800);await w(2400);$('#ey').style.transform='translate(-3px,1px)';await w(2600);
$('#ey').style.transform='translateX(-3px)';await w(2400)}},
