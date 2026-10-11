{n:'酒馆出门',sub:'老街尽头 · 酒馆门口 · 尼尔烂醉',sh:'酒醉',pos:[380,430],bg:'#0a0709',set:'#set3',lx:620,i:['34%','62%'],mk:()=>SETS.pubdoor(),
ini(){P('nl',{d:0,show:0,x:600,y:790,s:.5,f:0,face:'drunk'})},
async run(k,{w}){
/* 醉酒姿势：整张立绘切换（左摇/右晃/扶墙/绊倒/垂头），人物位移是连续的 */
Object.assign(POSES,{
 dA:{ll:-16,lr:14,ar:34,al:-48,b:-10,h:12},
 dB:{ll:18,lr:-14,ar:-44,al:30,b:11,h:-13},
 dLean:{ll:-6,lr:8,ar:-86,al:12,b:18,h:16},
 dTrip:{ll:-30,lr:26,ar:-70,al:-60,b:26,h:-6},
 dHang:{ll:0,lr:4,ar:10,al:12,b:-6,h:22}});
const sway=async(ms,i0=0)=>{for(let t=0,i=i0;t<ms;t+=480,i++){pose('nl',i%2?'dB':'dA',{f:0,face:'drunk'});stp();await w(480)}};
const hic=()=>{tn(190,.1,'square',.05);tn(140,.14,'square',.04,.08)};
await w(600);sp(2,620,1.2,.1,'#ff8a96');
cap.innerHTML='<em>（尼尔从酒馆里出来，已经烂醉。）</em>';
op('#pdl',.95);piano();await w(900);
pose('nl','dHang',{show:1,f:0,face:'drunk'});await w(900);
/* 出门：晃出门口，下台阶 */
mv('nl',700,790,1300,'ease-in-out',.56);await sway(1300);
/* 扶墙停住，打嗝 */
op('#pdl',0);pose('nl','dLean',{f:0,face:'drunk'});await w(500);hic();await w(1200);
pose('nl','dHang',{f:0,face:'drunk'});await w(900);
/* 推开墙，往回倒退一步 */
mv('nl',670,790,700,'ease-in-out');await sway(700,1);
/* 踉跄冲出几步，差点摔倒 */
mv('nl',930,790,1500,'cubic-bezier(.3,0,.7,1)');await sway(1000);
pose('nl','dTrip',{f:0,face:'shock'});stp();bs(.2,.5,260);tn(60,.2,'sine',.3);await w(700);
pose('nl','dB',{f:0,face:'drunk'});await w(500);hic();await w(600);
cc();
/* 一步一晃，走远 */
mv('nl',1750,790,6400,'ease-in-out');await sway(6400);
await w(600)}},
