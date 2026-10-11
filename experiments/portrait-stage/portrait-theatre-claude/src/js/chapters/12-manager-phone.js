{n:'经理 · 电话',sub:'尼尔回到公寓 · 经理和装电话的工人已经在等他',sh:'电话',pos:[1180,805],bg:'#0b0907',set:'#set3',i:['30%','60%'],mk:()=>SETS.phone(),
ini(){P('nl',{d:0,x:-150,s:.56,face:'n'});P('mg',{d:0,x:900,s:.58,f:1,face:'n'});P('wk',{d:0,x:1440,s:.5,f:1,b:10,ar:-50})},
async run(k,{w,go,fl,n,m}){
await w(500);await fl();lamp(1);P('mg',{show:1,d:600});P('wk',{show:1,d:600});await w(900);
P('nl',{show:1,d:0});go('nl',430,2400);await w(2600);P('mg',{face:'smile',h:-3});await w(500);
go('mg',600,900);await m('尼尔！');
const pt=async()=>{P('mg',{ar:-102,d:140});await w(150);bs(.12,.6,300);tn(80,.15,'sine',.3);P('mg',{ar:-84,d:140});P('nl',{h:4,b:-3,d:140});await w(170);P('nl',{h:0,b:-1,d:200});await w(250)};
P('mg',{ar:-90,face:'smile'});await w(300);await pt();await pt();P('mg',{b:4});P('nl',{face:'wry',b:-3});await w(500);
await n('这是干嘛？');
P('mg',{al:35});await m('电话！以后出了事情，还让人通过门房留言吗？');
await m('大侦探得有个电话。');
P('nl',{ar:-40});await n('我还……');P('nl',{ar:0});
await pt();await m('早晚的事。');
P('mg',{ar:0,al:0,f:0,b:0,face:'smile'});await w(700);P('mg',{ar:-72});
P('wk',{b:0});$('#cb').style.strokeDashoffset=0;P('nl',{h:-4,b:0,face:'wry'});await m('给他装好的！最好的线！');
await w(3200)}},
