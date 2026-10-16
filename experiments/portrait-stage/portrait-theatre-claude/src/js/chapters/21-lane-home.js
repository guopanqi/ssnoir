{n:'莱恩的家',sub:'莱恩的住处 · 很多碎片',sh:'莱恩家',pos:[120,200],bg:'#090a0d',set:'#set3',i:['34%','62%'],mk:()=>SETS.home(),
ini(){P('nl',{d:0,x:-150,s:.56,face:'hard'});P('wk',{d:0,x:1320,s:.52,f:1,face:'n'})},
async run(k,{w,go,fl,nar}){
await w(500);mp(1);P('wk',{show:1,d:700});P('nl',{show:1,d:0});go('nl',420,2600);await w(2800);
await nar('（你去到莱恩的家，这里很多碎片。）',1800);P('wk',{ar:-60});await w(900);P('wk',{ar:0});
go('nl',560,1200,{f:0});await w(1300);P('nl',{b:8,h:12});tn(1400,.06,'triangle',.05);await w(1500);
go('nl',700,1200);await w(1300);P('nl',{b:8,h:12});tn(1300,.06,'triangle',.05);await w(1300);
await nar('（你会进行探索，分析。）',1600);
go('nl',900,1800);await w(1900);P('nl',{b:10,h:14,face:'wry'});await w(900);
op('#ob',1);tn(1800,.12,'triangle',.06);await w(1300);P('nl',{ar:-30,b:12});await w(900);op('#ob',0);P('nl',{ar:-70,b:0,h:4});sp(1,900,.5,.3,'#ffd98a');await w(1200);
await nar('（其中一个东西，你拿了起来。）',3200);mp(.4);await w(1000)}},
