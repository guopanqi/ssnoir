{g:'S 系列',n:'艾迪的办法',sub:'码头 · 傍晚 · 一大垛没搬完的货',sh:'艾迪',pos:[1000,830],bg:'#0a0a0d',set:'#set3',lx:700,i:['40%','60%'],mk:()=>SETS.dock(),
ini(){$('#front').innerHTML=FRBOX;$('#front').style.opacity=0;P('ed',{d:0,show:0,x:1750,y:790,s:.6,f:1,face:'n'});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'n'})},
/* 取景固定；只有两次运镜：念头一转时慢慢推向尼尔，艾迪指向那垛货时慢慢拉开。人物一律走进走出，不凭空出现 */
async run(k,{w,n,ed,nar,at,go,murmur,sigh}){
$('#front').style.opacity=1;sp(2,700,1.3,.1,'#e0a860');const mu=murmur(.06);
/* 艾迪扛着箱子从右边走进来，放上木箱，擦汗；尼尔从左边走来，艾迪没停手 */
pose('ed','carry',{show:1,f:1,face:'n'});gl('cr',1638,585,1,0);
mv('ed',800,790,5000,'linear');gl('cr',688,585,1,5000,0,'linear');
P('nl',{show:1,d:0});pose('nl','stand',{face:'n'});
for(let t=0;t<5000;t+=520){if(t==1500)go('nl',500,3400);stp();await w(520)}
gl('cr',650,666,1,450);tn(90,.2,'sine',.3);bs(.1,.4,400);pose('ed','scratch',{f:1,face:'n'});await w(900);
mv('ed',760,790,500);pose('ed','shake',{f:1,face:'n',h:-4});await w(500);
await ed('尼尔，我从别人那儿听说，你最近是不是在找人？');
pose('nl','stand',{face:'n',h:4});
await n('对，有一些事，和一些混混有关。');
/* 一听是混混，艾迪攥起拳头，咧嘴笑 */
pose('ed','fist',{f:1,face:'smile'});tn(120,.1,'sine',.2);
await ed('用不用我帮忙，我把他们打得落花流水。');
/* 尼尔话说到一半停住：念头来了。镜头慢慢推向他 */
pose('nl','stand',{face:'n',h:2});dolly(1.3,40,58,2600);
await n('不用，他是...');
pose('nl','stand',{face:'shock',h:-2});await w(1300);
pose('ed','shake',{f:1,face:'n',h:0});pose('nl','stand',{face:'wry',h:-4});
await n('对啊，也许可以换一个种方式。艾迪，你听我说....');
/* 他凑近，艾迪也往前倾；（说了一些简单的情况）——只有低低的说话声 */
mv('nl',540,790,700,'ease-out');mv('ed',740,790,700,'ease-out');
const lines=nar('（说了一些简单的情况）',3800);
for(let i=0;i<5;i++){pose('nl',i%2?'talkB':'talkA',{face:'n',h:i%2?-2:4});tn(130+i*12,.18,'triangle',.02);if(i==1)pose('ed','shake',{f:1,face:'hard',b:8,h:2});if(i==3)pose('ed','shake',{f:1,face:'hard',b:8,h:6});await w(720)}
await lines;
/* 艾迪直起身，边说边比划：就站在这儿，他一伸手，一把按住 */
pose('nl','stand',{face:'n',h:0});mv('ed',760,790,500);pose('ed','point',{f:1,face:'hard',b:2});
at(1800,()=>{pose('ed','fist',{f:1,face:'hard',b:8});tn(70,.25,'sine',.4);bs(.12,.5,350)});
await ed('咱们在这儿埋伏着，他一伸手，就把他按住。');
/* 尼尔垂下眼——这一句艾迪说得慢，是认真的 */
pose('ed','shake',{f:1,face:'hard',b:4,h:-2});pose('nl','stand',{face:'sad',h:12});
await ed('你送给他一次，还有下一次，会没完没了的。');
await w(700);pose('ed','shake',{f:1,face:'n',b:10,h:2});
await ed('什么时候');
pose('nl','stand',{face:'n',h:0});
await n('就在信上约定的那天晚上。');
/* 艾迪挠头；转身指向右边那一大垛，镜头慢慢拉开把它带进画面 */
pose('ed','scratch',{f:1,face:'n'});await w(900);
pose('ed','point',{f:0,face:'wry',b:2});dolly(1,50,60,3200);
at(2500,()=>{pose('nl','stand',{f:0,face:'wry',h:6});sigh('nl',1500)});
await ed('那天我有班。你替我把这批活干完，我就能把那晚的班换开。');
await w(900);
/* 成交：两只手在木箱上方握住；艾迪把手套递给他 */
mv('ed',720,790,500);await w(500);pose('ed','point',{f:1,face:'smile'});pose('nl','point',{f:0,face:'n'});
await n('成交。');
tn(150,.12,'sine',.25);bs(.08,.3,500);await w(500);
gl('gv',690,688,1,0);await w(500);pose('nl','take',{face:'n'});gl('gv',647,600,1,500);await w(2800);mu.stop()}},
