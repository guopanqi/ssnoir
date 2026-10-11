{n:'室内求助',sub:'尼尔的房间 · 夜莺把一切摊在桌上',sh:'求助',pos:[1060,720],bg:'#0b0907',set:'#set3',i:['34%','62%'],mk:()=>SETS.room(),
ini(){$('#props').innerHTML=PHOTO;P('nl',{d:0,show:1,x:1040,f:1,s:.56,face:'n',ar:55,al:-55});P('ny',{d:0,x:-160,s:.56,bag:1,umc:1,face:'sad'})},
async run(k,{w,go,fl,n,y,mono}){
await w(500);await fl();lamp(1);await w(600);
P('ny',{show:1,d:0});go('ny',560,2800);await w(3000);
P('ny',{ar:-75});await w(700);op('#ph',1);P('ny',{ar:30,al:-30});await w(600);
P('nl',{ar:0,al:0,face:'wry'});await n('这张照片，嗯，是有些出格，但也没什么特别的，你为什么这么紧张？');
P('ny',{face:'sad',h:4});await y('他手上也许不止这一张，这种照片对普通人来说没什么，可我过些天有一场演出，这是我第一场大型的演出，这肯定会毁了我的');
light();P('nl',{face:'n'});await n('会不会是一个恶作剧，有些小混混会四处搜集照片，然后写一堆这样的信，如果恰巧有人有一些不愿意别人知道的故事的话');
P('ny',{h:9});await w(500);await y('唉，但我这次真的不能冒险，万一他真的有些什么……');P('ny',{h:3});
await n('你能想到，你身边有什么人会干这种事吗？');
cc();P('ny',{h:10,look:6,face:'sad'});await w(2400);await y('……没有，我和他们很久都不联系了');P('ny',{look:0,h:3});
await n('他们？');await y('过去的邻居和朋友什么的，不会是他们');
await n('报警呢？');await y('不行，我经理会知道的，最好也别让他知道');
P('nl',{cg:0});await w(300);light();await n('还有一个方法，他要得不算多，不是什么大钱，把钱给他也许他就不会再纠缠了。');
P('ny',{face:'wry',h:-3});await y('你不是一个侦探吗？');
await n('我很少见到要这么少的勒索，让我来办也要付给我钱，恐怕未必划算');
await n('但是话说回来，拿到钱他也未必肯停手，那我们就必须弄清楚他是谁了');
cc();P('ny',{ar:-72,face:'sad',h:6});await w(700);op('#jw',1);[1500,1900,2300,1700].forEach((f,i)=>tn(f,.12,'triangle',.05,i*.08));P('ny',{ar:0});await w(1200);
await y('说实话，我把首饰也当了，也凑不够他信里要的钱。我其实根本就没办法');
await n('那我的报酬呢？你准备怎么付我的报酬？');
P('ny',{h:8});await w(1700);await y('演出之后可以吗？经理说过些天会有一场大型的演出，应该会有些钱。虽然之前很多次小一些的演出，他也没给过我什么钱');
P('ny',{f:1,back:1,h:8,cl:{sob:1}});await y('当时他说会有些钱的，但是也没给');await y('这次机会很重要，我不愿意错过');
cc();P('nl',{face:'n'});await w(2600);
P('ny',{f:0,back:0,face:'sad',ar:145,cl:{sob:0}});await w(900);P('ny',{ar:0,h:0});await y('我明白，确实太麻烦你了');
op('#jw',0);P('ny',{ar:-72});await w(700);P('ny',{ar:145,hat:1});await w(900);P('ny',{ar:0});
await y('谢谢你，尼尔先生');
cc();P('ny',{f:1,face:'n'});go('ny',330,2200);await w(2400);
await n('演出还有多久？');P('ny',{h:-3});await y('就这两周了。');
P('ny',{f:0,d:500});P('nl',{face:'hard',ar:0});await n('我们得先想办法凑够赎金，只要他来拿钱，我有办法弄明白他是谁');
cc();await w(900);P('ny',{face:'sad',tr:1,ar:145,h:0});pad();sp(1,330,1.1,.26,'#ffd9a0');await w(3800);
lamp(.45);P('nl',{face:'n',h:4});await mono('我自己也没想明白，为什么我会接下这个赔钱的生意，但这是这几个月难得的委托。我打算从哪里凑些钱，不过，我是不会乖乖把钱送给这个杂种的。');
await w(900)}},
