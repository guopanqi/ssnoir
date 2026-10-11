{n:'巷口',sub:'老街 · 听到“莱恩”这个名字',sh:'巷口',pos:[560,190],bg:'#080a0e',set:'#set3',i:['34%','62%'],mk:()=>SETS.sign(),
ini(){P('nl',{d:0,x:-150,s:.56,face:'n'});P('ny',{d:0,x:1760,s:.56,f:1,face:'n'});P('v1',{d:0,x:1430,s:.54,f:1})},
async run(k,{w,go,fl,n,y,nar}){
await w(500);sp(2,800,1.3,.1,'#9db0e0');P('nl',{show:1,d:0});go('nl',520,2600);P('ny',{show:1,d:0});go('ny',1060,2800);P('v1',{show:1,d:900});await w(3000);
await nar('（夜莺换了一身衣服，回到老街。）',1800);
P('v1',{cl:{tk:1}});whisp('……莱恩？',1300,380,0);whisp('莱恩那小子……',1380,300,.9);await w(2400);P('v1',{cl:{tk:0},f:0,back:1});
P('ny',{face:'shock',h:0});await w(1400);P('nl',{f:0,face:'wry',h:-3});
await n('莱恩？你们认识？');P('ny',{face:'sad',h:8});await w(1000);await y('……我们回去再说');
P('nl',{face:'hard',h:0});await n('他和这件事情有关？');await y('我不想在这儿说');
await n('那就这样吧');P('ny',{h:3,ar:30,al:-30});await y('尼尔……');
cc();P('nl',{f:1,face:'n',h:5});go('nl',-200,4200);await w(2400);P('ny',{f:1,h:0,face:'sad',ar:0,al:0});go('ny',-200,4600);await w(3600);sp(2,800,1.3,.03,'#9db0e0');await w(1200)}},
