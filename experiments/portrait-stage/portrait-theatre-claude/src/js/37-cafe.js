/* SC-A04 露西初识：两个分支（玩家做过/没做过码头搬运）共用同一段演出，只有露西观察外套的那句不同。
   章节文件各写一个对象：ini:CAFE_INI, run:(k,h)=>CAFE_RUN(k,h,是否做过码头) */
const CAFE_INI=()=>{$('#front').innerHTML=CAFF;$('#front').style.opacity=0;P('lc',{d:0,show:0,x:1240,y:790,s:.6,f:1,face:'n'});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'n'})};
async function CAFE_RUN(k,{w,n,lc,at,murmur,pace,go},dock){
Object.assign(POSES,{carry2:{ar:-80,al:-76,b:-4,h:4},count:{ar:-40,al:10,b:6,h:12},catch:{ar:-75,al:8,b:10,h:6}});
$('#front').style.opacity=1;mp(.12);sp(2,900,1.3,.08,'#f3d08a');
hum([[390,790,.95,1],[650,790,.9,-1]]);
const mu=murmur(.18);pace(.85);
const clat=()=>{tn(2400+Math.random()*600,.07,'sine',.05);bs(.04,.12,3500)};
/* 背景的小动静：咖啡机嘶一声、杯碟轻碰 */
at(1500,()=>bs(.7,.09,4200));at(4200,clat);at(6800,clat);
/* 露西在收银台后数零钱（硬币叮当）；尼尔端着托盘从左边走进来，托盘跟着他走 */
pose('lc','count',{show:1,f:1,face:'n'});
let counting=true;(async()=>{try{while(counting&&k===gen){tn(3100,.05,'sine',.04);await w(330);tn(2700,.05,'sine',.04);await w(520)}}catch(e){}})();
P('nl',{show:1,d:0});pose('nl','carry2',{face:'n'});G.nl.style.transitionTimingFunction='linear';
gl('tr',-48,590,1,0);gl('c1',-70,584,1,0);gl('cp',-30,584,1,0);
const walk=4600;go('nl',880,walk);
/* 手里端着托盘，圆杯 */
gl('tr',992,590,1,walk,0,'linear');gl('c1',970,584,1,walk,0,'linear');gl('cp',1010,584,1,walk,0,'linear');
for(let t=0;t<walk;t+=460){stp();await w(460)}
/* 她看了看他的外套 */
counting=false;pose('lc','idleDown',{f:1,face:'n',h:8});await w(600);
await lc('你是新来的？');
pose('lc','idleDown',{f:1,face:'n',h:0});
await n('嗯。');
await lc('只干今天？');
pose('nl','carry2',{face:'wry'});
await n('看老板明天还要不要我。');
/* 杯子在托盘上越晃越厉害；她说完这句，伸手把它接住 */
const wob=async()=>{try{for(let i=0;i<14&&k===gen;i++){gl('cp',1010,584,1,110,i%2?9:-9);await w(120)}}catch(e){}};
wob();pose('lc','idleDown',{f:1,face:'smile',h:-2});
await lc('只要你没摔盘子就行。');
mv('lc',1110,790,350,'ease-out');pose('lc','catch',{f:1,face:'n'});await w(380);
gl('cp',1040,650,1,260,0);tn(2600,.1,'sine',.09);await w(300);
pose('lc','idleDown',{f:1,face:'n',h:4});
await lc('先学会把杯子摆平。');
/* 她笑了；尼尔有点窘 */
pose('lc','idleDown',{f:1,face:'smile',h:-4});tn(520,.12,'triangle',.05);tn(620,.12,'triangle',.04,.14);pose('nl','carry2',{face:'wry',h:8});await w(1200);
pose('lc','idleDown',{f:1,face:'n',h:6});
if(dock)await lc('你之前在码头干过活？袖子上还有煤灰。');
else await lc('你以前干什么的？看起来不像常端盘子的人。');
pose('nl','carry2',{face:'n',h:2});
await n('哪儿有活儿就去哪儿。');
pose('lc','idleDown',{f:1,face:'smile',h:2});
await lc('那你挺忙的。明天要是还来，记得从后面拿托盘，别挡住客人。');
/* 她拿起一只杯子继续擦，没有停下来等他回答；尼尔把托盘放上柜台 */
let wiping=true;gl('cp',1040,650,0,200);gl('gl',1000,592,0,0);
pose('lc','wipe',{f:1,face:'n',h:14});(async()=>{try{while(wiping&&k===gen){gl('c1',1090,590,1,170,10);await w(200);gl('c1',1078,596,1,170,-10);await w(200)}}catch(e){}})();
await w(700);gl('tr',1010,648,1,600);pose('nl','stand',{face:'n',h:4});await w(900);
await w(2600);wiping=false;mu.stop()}
