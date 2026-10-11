{n:'剧院 · 信',sub:'化妆室 · 第三封信',sh:'第三封信',pos:[1350,200],bg:'#0a0810',set:'#set3',i:['34%','62%'],mk:()=>SETS.dress(),
ini(){$('#props').innerHTML=LETTER;P('ny',{d:0,x:700,s:.56,face:'sad',h:6});P('nl',{d:0,x:1760,s:.56,f:1,face:'n'})},
async run(k,{w,go,n,y,mono,nar}){
await w(500);os('.lh',.16);P('ny',{show:1,d:600});mvl(1120,598,1);await w(1000);
await nar('（在剧院里你见到了夜莺，她在房间里反复走来走去。）',1800);
P('ny',{f:1});go('ny',520,1500);await w(1700);P('ny',{f:0});go('ny',900,1700);await w(1900);P('ny',{f:1});go('ny',560,1600);await w(1800);
P('nl',{show:1,d:0});go('nl',1300,2400);await w(2000);P('ny',{f:0});go('ny',940,1000);await w(1100);
await y('尼尔！');await n('把那封信拿给我');
go('nl',1300,10);mvl(1120,598,0);P('ny',{ar:-72,face:'sad'});await w(700);P('nl',{ar:-72});mvl(1200,560,1);await w(900);P('ny',{ar:0});P('nl',{ar:0,face:'n'});mvl(1300,640,1);await w(900);
await n('冷静点，你是什么时候收到这封信的');
await y('我在化妆室看到它的，我中午刚排练完。');
await n('还是莱恩？');
P('ny',{h:8,face:'sad'});await w(1400);
await y('还能是别人吗？但我总觉得莱恩做不出这种事来的');
P('nl',{face:'hard',h:0});await n('人是会变的。');
mbs(3);P('ny',{ar:145,h:10});await y('他就是要毁了我的一切…………');
P('ny',{ar:0,h:0});await n('你还要上台吗？如果真的有危险的话');
P('ny',{face:'n',h:-2});await y('……我不能放弃这次机会，这是我的梦想，我一直都想登上更大的舞台');
P('ny',{f:1,face:'sad'});await y('但是……');go('ny',640,1400);await w(1500);P('ny',{f:0});go('ny',900,1400);await w(1500);
await y('尼尔，你会有办法的对吗？你总是有办法的');
P('nl',{face:'n',h:3});await n('我会试试');
P('ny',{face:'smile',h:0});await y('你真好，尼尔');
P('nl',{face:'hard',h:5});await n('这是我的工作，处理麻烦是我的工作，这件事情是我没有处理好，我会去找到他的。');
cc();mbs(2);os('.lh',.08);await mono('莱恩，我会找到你的。');await w(900)}},
