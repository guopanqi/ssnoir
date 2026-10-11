{n:'酒馆出门',sub:'老街尽头 · 酒馆门口 · 尼尔烂醉',sh:'酒醉',pos:[380,430],bg:'#0a0709',set:'#set3',lx:620,i:['34%','62%'],mk:()=>SETS.pubdoor(),
ini(){P('nl',{d:0,show:0,x:600,y:790,s:.5,f:0,face:'drunk'})},
async run(k,{w}){
/* 醉酒：只用三张立绘（垂头站立 / 摇晃行走 / 扶墙），走路的左摇右晃是整个人物的缓慢摆动（swy），不切换密集姿势 */
Object.assign(POSES,{
 dHang:{ll:0,lr:4,ar:10,al:12,b:-6,h:22},
 dWalk:{ll:-8,lr:8,ar:22,al:-26,b:-4,h:10},
 dLean:{ll:-6,lr:8,ar:-86,al:12,b:18,h:16}});
const sw=(on,big)=>{const c=G.nl.classList;c.toggle('swy',on&&!big);c.toggle('swy2',on&&!!big)};
const hic=()=>{tn(190,.1,'square',.05);tn(140,.14,'square',.04,.08)};
await w(600);sp(2,620,1.2,.1,'#ff8a96');
cap.innerHTML='<em>（尼尔从酒馆里出来，已经烂醉。）</em>';
op('#pdl',.95);piano();await w(900);
pose('nl','dHang',{show:1,f:0,face:'drunk'});sw(true);await w(1400);
/* 出门：摇晃着走下台阶 */
pose('nl','dWalk',{f:0,face:'drunk'});mv('nl',700,790,2400,'ease-in-out',.56);
for(let t=0;t<2400;t+=600){stp();await w(600)}
/* 扶墙停住，打嗝 */
op('#pdl',0);cc();sw(false);pose('nl','dLean',{f:0,face:'drunk'});await w(700);hic();await w(1500);
/* 推开墙，继续走；中途猛地一晃（摆幅加大），随后稳住 */
pose('nl','dWalk',{f:0,face:'drunk'});sw(true);mv('nl',1750,790,9000,'linear');
for(let t=0;t<9000;t+=600){if(t==2400){sw(true,true);stp();bs(.2,.5,260);tn(60,.2,'sine',.3)}if(t==3600){sw(true,false);hic()}stp();await w(600)}
sw(false);await w(400)}},
