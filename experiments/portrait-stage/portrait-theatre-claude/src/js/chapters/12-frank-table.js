{n:'弗兰克的桌子',sub:'老街 · 一拳之后',sh:'弗兰克',pos:[170,640],bg:'#0c0808',set:'#set3',i:['30%','60%'],mk:()=>SETS.frank(),
ini(){$('#front').innerHTML=FRONT;$('#front').style.opacity=0;P('nl',{d:0,x:-150,s:.56,face:'hard'});P('fr',{d:0,x:800,y:880,s:.6,f:1,face:'hard',cg:1});P('wk',{d:0,x:1300,s:.52,f:1,face:'n'})},
async run(k,{w,go,fl,f,n,nar}){
await w(400);P('nl',{show:1,d:0});go('nl',500,2400);await w(2600);
bs(.4,1,300);tn(60,.4,'sine',.5);fxf('brightness(4)',60);await w(120);fxf('brightness(0)',100);await w(300);
P('nl',{x:440,b:-12,h:16,face:'n',d:0});$('#front').style.opacity=1;P('fr',{show:1,d:0});P('wk',{show:1,d:0});blinds(1);
await nar('（一个街角，突然你被一拳打晕了。……醒来的时候，你看到了弗兰克，和他的那张桌子。）',3200);
fxf('blur(14px) brightness(.35)',10);await w(60);fxf('none',2800);await w(2800);P('nl',{b:-4,h:6});
await f('你在找什么？');P('nl',{b:0,h:0,face:'hard'});await n('找你');
P('fr',{ar:-62});await w(500);bs(.06,.5,500);P('fr',{ar:-45});await w(400);bs(.06,.5,500);P('fr',{ar:-62});
await f('你想知道什么？');
await n('莱恩勒索一个女孩儿，她委托我来处理这件事');
P('fr',{face:'hard',h:3});await w(900);go('wk',1150,900,{f:1});P('wk',{h:-4});await w(1100);P('wk',{f:0});go('wk',1700,1400);await w(1500);
await f('有什么依据吗？');
await n('我拿到了他身上的烟，在我的口袋里');
P('fr',{h:5});go('wk',500,2600,{f:1,show:1});await w(2700);pk(470,640,.22,1,0,10);await w(300);pk(540,610,.22,1,0,400);await w(500);go('wk',1060,2400);pk(940,672,.3,1,0,2400);await w(2600);
await f('我也会抽这种烟');gh(1000,672,.3,.9);
await n('那么也可能是你');
P('fr',{face:'smile',cl:{tk:1}});await w(1800);P('fr',{cl:{tk:0}});
go('wk',1240,1200);await w(1400);P('wk',{cl:{tk:1}});await w(1600);P('wk',{cl:{tk:0}});
P('fr',{face:'n',h:0});await f('看来你说的有些道理，我们来谈谈这件事');
cc();await w(1600);await nar('（此后进入和莱恩的交锋：和弗兰克谈判 → 教训莱恩 · 见游戏）',3200)}},
