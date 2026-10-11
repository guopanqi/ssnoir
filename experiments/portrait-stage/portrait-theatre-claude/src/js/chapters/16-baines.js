{n:'贝恩斯',sub:'老街 · 莱恩消失之后',sh:'质问',pos:[800,170],bg:'#0d0809',set:'#set3',i:['34%','62%'],mk:()=>SETS.tavern(),
ini(){P('bn',{d:0,x:600,s:.58,f:0,face:'hard'});P('fr',{d:0,x:1000,s:.6,f:1,face:'hard'});P('nl',{d:0,x:1800,s:.56,f:1,face:'hard'});P('v1',{d:0,x:1470,s:.54,f:1})},
async run(k,{w,go,fl,f,b,n,nar}){
await w(500);sp(2,800,1.5,.16,'#ff7a8a');P('bn',{show:1,d:600});P('fr',{show:1,d:600});await w(1200);
await b('你就什么也不知道？');await f('我很少见他');
await b('这事儿跟你脱不了关系');await f('这就是你们站在老街的理由？');
P('v1',{show:1,d:300});P('nl',{show:1,d:0});go('nl',1180,2800);await w(1300);P('v1',{ar:-70});await w(900);P('v1',{ar:0});await w(800);
await f('这件事');cc();P('fr',{f:0,face:'hard'});await w(700);
P('nl',{ar:-78,b:6});await w(500);P('fr',{x:970,b:-6,d:300});bs(.1,.5,400);await w(700);
await nar('（尼尔上来揪住衣领、逼近质问。弗兰克生气了。）',1700);
go('bn',1070,900);P('nl',{x:1220,ar:0,b:0,d:400});P('fr',{x:900,b:0});await w(1100);
await n('莱恩呢？');
await f('莱恩，莱恩，你们都要找这个莱恩');
await b('莱恩消失了，几天前，我已经派了警察在找他，没什么消息');
await f('你觉得我在藏着他？我比你们还急，这个王八蛋');
await b('这场演出没这么简单，有谁？贝城的很多人都在看着这场演出。弗兰克，要是你的人，这里的人出了事，惹恼了那些人，你也不会好受');
cc();P('fr',{f:1,face:'hard'});go('fr',-200,3400);await w(2600);
P('nl',{face:'wry',h:0});await n('你不觉得他藏着莱恩？');P('bn',{f:1,d:400});
await b('我搜过了，没有');
await b('他一直不希望我们插手老街的事情，可是前提是他管好自己的人');
await b('你去看过他家里了吗？有些线索，我助手在那儿他会告诉你');
P('nl',{face:'hard'});await n('我去看看');
cc();await w(1500)}},
