{n:'房东催租',sub:'尼尔的公寓楼下 · 深夜 · 门口的房东',sh:'房东',pos:[960,690],bg:'#070a12',set:'#hotel',i:['20%','52%'],
ini(){P('nl',{d:0,show:0,x:-160,y:790,s:.56,f:0,face:'drunk'});P('ld',{d:0,show:0,x:1060,y:790,s:.56,f:1,face:'hard'})},
async run(k,{w,l}){
Object.assign(POSES,{
 tired:{ll:-10,lr:9,ar:6,al:-8,b:7,h:12},
 reach:{ll:-4,lr:4,ar:-62,al:-6,b:8,h:10},
 crossed:{ar:56,al:-56,b:-2,h:0},
 shake:{ar:56,al:-56,b:-3,h:-9}});
await w(600);sp(2,800,1.2,.1,'#9db0e0');
pose('ld','crossed',{show:1,f:1,face:'hard'});
/* 尼尔拖着步子走到门口 */
pose('nl','tired',{show:1,f:0,face:'drunk'});bob('nl',1);mv('nl',770,790,4200,'ease-out');
for(let t=0;t<4200;t+=420){stp();await w(420)}
bob('nl',0);
/* 摸出钥匙开门；门开，里面亮起 */
pose('nl','reach',{f:0,face:'n'});await w(700);
buzz();rx('ul',true);op('#dl',.95);$('#leaf').style.transform='scaleX(.12)';sp(2,800,.9,.22,'#f4cf80');await w(900);
/* 房东在背后开口，尼尔不回头 */
pose('ld','crossed',{f:1,face:'hard',h:-4});
await l('房租还没交尼尔，尼尔，你明早最好还记得。');
pose('nl','tired',{f:0,face:'hard',h:6});await w(1100);
/* 尼尔没理他，走进公寓 */
pose('nl','tired',{f:0,face:'n',h:2});bob('nl',1);mv('nl',800,786,1900,'ease-in-out',.46);P('nl',{show:0,d:1900});
for(let t=0;t<1800;t+=450){stp();await w(450)}
bob('nl',0);await w(300);
$('#leaf').style.transform='scaleX(1)';op('#dl',0);rx('ul',false);sp(2,800,.9,0);await w(1000);
pose('ld','shake',{f:1,face:'hard'});await w(1800);
pose('ld','crossed',{f:1,face:'n'});await w(1400);P('ld',{show:0,d:700});await w(800)}},
