{n:'平静',sub:'尼尔的房间 · 事情终于过去之后的第二天晚上',sh:'平静',pos:[700,740],bg:'#0b0907',set:'#set3',i:['34%','62%'],mk:()=>SETS.room(),
ini(){$('#props').innerHTML=BOXES;P('nl',{d:0,show:1,x:1040,f:1,s:.56,face:'n',b:3,h:5});P('ny',{d:0,x:-160,s:.56,face:'n'})},
async run(k,{w,go,fl,n,y,nar}){
await w(400);await fl();lamp(.8);await nar('（一切都很平静，尼尔又回到日常的生活中，第二天晚上。）',2600);
P('ny',{show:1,d:0});go('ny',560,2600);await w(2900);
await y('我想请你吃顿饭。');P('nl',{h:0,face:'wry'});await n('今晚……可能不行，搬了一下午的箱子。');
cc();P('ny',{face:'n',h:-3});await w(2200);
await y('那你家里有什么？');cap.innerHTML='<b style="color:#f0cf8a">尼 尔</b><span style="opacity:1">……</span>';P('nl',{face:'n',h:8});await w(2800);
P('ny',{face:'smile',h:0});await y('你对这顿饭最好期待低一点。');
cc();P('ny',{f:1});go('ny',-160,2200);await w(2400);P('ny',{show:0,d:300});await w(1600);
P('ny',{f:0,x:-160,d:0});P('ny',{show:1,d:0});go('ny',560,2400);op('#bx',1);lamp(1);await w(3000);
P('nl',{h:0,face:'n',b:0});P('ny',{face:'n'});await w(1400);
await y('你为什么想当侦探？');await n('小时候书看多了。');
P('ny',{face:'n',h:-2});await y('我是认真的。');
cc();P('nl',{face:'n',h:8});await w(4200);lamp(.7);await w(1200)}},
