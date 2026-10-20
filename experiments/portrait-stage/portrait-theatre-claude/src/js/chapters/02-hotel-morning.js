{g:'S 系列',n:'第一日清晨',sub:'旅馆 · 尼尔的房间 · 门外的老板',sh:'清晨',pos:[540,830],bg:'#0b0907',set:'#set3',lx:830,i:['40%','60%'],mk:()=>SETS.hotelRoom(),
ini(){$('#front').innerHTML=BED;$('#front').style.opacity=0;P('nl',{d:0,show:0,x:470,y:830,s:.62,f:0,face:'drunk'});P('ho',{d:0,show:0,x:1340,y:790,s:.6,f:1,face:'n'})},
/* 门内是宿醉的尼尔，门外是来收房钱却不想逼他的老板；镜头基本不动，只在“谢谢了....”那一拍慢慢推向尼尔 */
async run(k,{w,n,ho,nar,at}){
const door=$('#rl'),stps=async(ms)=>{for(let t=0;t<ms;t+=520){stp();await w(520)}};
$('#front').style.opacity=1;
pose('nl','sitHung',{show:1,f:0,face:'drunk'});sp(2,830,1.1,.08,'#f3d08a');
await w(1800);mp(.13);await w(1600);
/* 咚！咚！咚！门跟着震，他慢慢抬头；镜头压向他的脸 */
for(let i=0;i<3;i++)at(i*560,()=>{tn(95,.18,'sine',.45);bs(.05,.5,220);door.style.transition='transform .06s';door.style.transform='scaleX(.975)';at(120,()=>door.style.transform='scaleX(1)')});
at(1700,()=>pose('nl','sitUp',{face:'drunk'}));
await nar('（咚！咚！咚！）',2200);
await ho('尼尔？');
pose('nl','sitUp',{face:'drunk',h:12});
await n('谁？');
/* 门外的人隔着门说话；他撑着床沿站起来，摇摇晃晃往门口走 */
pose('nl','stand',{b:4,h:10,face:'drunk'});P('nl',{y:790,d:600});G.nl.classList.add('swy');
mv('nl',1090,790,2800,'ease-in-out');const sp1=stps(2800);
await ho('是我，我看你昨晚喝多了就没来打扰你');
await sp1;G.nl.classList.remove('swy');
/* 开门：门扇转开，走廊的光涌进来，老板站在光里 */
tn(170,.5,'sawtooth',.04);door.style.transform='scaleX(.12)';$('#rcl').style.opacity=.9;
pose('ho','stand',{show:1,d:500,f:1,face:'n',h:-2});pose('nl','stand',{f:0,face:'drunk',h:8});await w(700);
await n('是老板啊，你有事儿吗？');
pose('ho','stand',{f:1,face:'n',b:3,h:0});
await ho('我是跟你说旅馆房钱的事情，你这间房该交钱了。');
pose('nl','stand',{f:0,face:'sad',h:12});
await n('能等我两天吗？');
/* 老板这句最长：先看着他，说到“侦探的活儿”时尼尔垂下眼，镜头切到尼尔，再回到老板 */
pose('ho','stand',{f:1,face:'sad',b:6,h:-2});
at(2800,()=>pose('nl','stand',{f:0,face:'sad',h:20}));
at(5300,()=>pose('ho','stand',{f:1,face:'smile',b:6,h:-4}));
await ho('不要紧，尼尔，主要不是钱的事儿。你最近侦探的活儿怎么样了？干不下去的话也可以和我说，我给你介绍点别的活儿');
/* 尼尔低着头，过一会儿才开口 */
dolly(1.3,68,58,2800);pose('nl','stand',{f:0,face:'sad',h:20});await w(900);
await n('谢谢了....');
await w(1000);pose('nl','stand',{f:0,face:'hard',h:0});await w(700);
await n('房钱我两天后给你');
/* 老板点点头，退回走廊；门合上 */
pose('ho','stand',{f:1,face:'n',h:6});await w(900);
P('ho',{x:1420,d:1300,show:0});await w(1200);
door.style.transform='scaleX(1)';$('#rcl').style.opacity=0;tn(70,.3,'sine',.4);bs(.1,.6,300);await w(1200);
/* 一个人站在晨光里，镜头慢慢拉开 */
pose('nl','stand',{f:1,face:'n',h:6});dolly(1,50,60,4000);await w(4200)}},
