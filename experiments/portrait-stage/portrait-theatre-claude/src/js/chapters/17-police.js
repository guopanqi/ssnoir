{n:'老街被介入',sub:'老街 · 警察在各处搜查',sh:'介入',pos:[640,70],bg:'#080a0e',set:'#set3',i:['36%','64%'],mk:()=>SETS.alley(),
ini(){P('v1',{d:0,x:1000,s:.54,f:1});P('wk',{d:0,x:1170,s:.52,f:0});P('v2',{d:0,x:760,s:.34,f:0});P('bn',{d:0,x:1800,s:.58,f:1});P('bn2',{d:0,x:-200,s:.58,f:0,face:'hard'});P('nl',{d:0,x:-150,s:.56,face:'n'})},
async run(k,{w,go,fl,nar}){
await w(500);sp(2,800,1.3,.08,'#9db0e0');const sir=async n=>{for(let i=0;i<n;i++){sp(1,300,1.3,.2,'#ff4a5a');sp(2,1300,1.3,.2,'#4a7aff');await w(420);sp(1,300,1.3,.02,'#ff4a5a');sp(2,1300,1.3,.02,'#4a7aff');await w(420)}};P('v1',{show:1,d:700,cl:{tk:1}});P('wk',{show:1,d:700,cl:{tk:1}});P('v2',{show:1,d:700});await w(3000);
P('nl',{show:1,d:0});go('nl',560,2400);await w(2600);
await nar('（你进入老街，就看到一些警察在各处搜查，盘问，审查。）',1600);
P('bn',{show:1,d:0,face:'hard'});go('bn',1280,2600);P('bn2',{show:1,d:0});go('bn2',280,2600);await sir(3);
P('v1',{cl:{tk:0},back:1});P('wk',{cl:{tk:0},f:1,face:'sad'});P('v2',{back:1});
note('搜查',280,380,0);note('盘问',1280,380,1.2);note('审查',1000,360,2.4);
P('bn',{ar:-60});await sir(2);P('bn2',{ar:-40});await sir(2);
P('nl',{face:'wry',h:3});await sir(2);
P('bn2',{ar:145});note('不处置，转而打电话',280,300,0);await sir(4);P('bn2',{ar:0});sp(1,300,1,0);sp(2,1300,1,0);await w(800)}},
