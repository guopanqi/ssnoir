{g:'SC-A 系列',n:'SC-A09 酒馆最后一夜',sub:'酒馆 · 夜莺唱歌的夜晚 · 一段即将过去的时光',sh:'最后一夜',pos:[1000,560],bg:'#0a0709',set:'#set3',lx:900,i:['40%','60%'],mk:()=>SETS.barNight(),
ini(){$('#front').innerHTML=BNF2;$('#front').style.opacity=0;P('ny',{d:0,show:0,x:1285,y:700,s:.54,f:1,face:'n',hat:0,bag:0,umo:0,umc:0});P('nl',{d:0,show:0,x:-160,y:790,s:.6,f:0,face:'n'});P('bm',{d:0,show:0,x:700,y:790,s:.6,f:0,face:'n'});P('lc',{d:0,show:0,x:1780,y:790,s:.6,f:1,face:'n'});P('ed',{d:0,show:0,x:1820,y:790,s:.6,f:1,face:'n'})},
/* 无台词。熟人一个个走过来坐下、靠着看；镜头慢慢聚到舞台；歌从轻到满，空间跟着拍子跳，唱到最高点后落回安静。
   运镜只有两次：开唱前缓缓推向舞台、结尾缓缓拉开。 */
async run(k,{w,at,go,murmur,jazz,vocal,sigh}){
Object.assign(POSES,{sitT:{ar:4,al:6,b:4,h:6},lean:{ar:10,al:10,b:5,h:2},singHi:{ar:-34,al:-4,b:-4,h:-10},singUp:{ar:-150,al:-20,b:-6,h:-14}});
$('#front').style.opacity=1;
const crowd=hum([[930,790,.95,1],[1010,790,1.05,-1],[1100,790,.9,1]]),band=hum([[1415,700,.7,-1],[1510,700,.72,-1]]);
$('#props').insertAdjacentHTML('beforeend','<g class="st"><path d="M1000 650q-8 -14 0 -28t0 -28"/></g>');
sp(2,900,1.3,.08,'#f3d08a');const mu=murmur(.3);
let sc=1;
const onBeat=(i,e)=>{if(e<=0)return;pulse(.004+e*.016);band.forEach(x=>x.animate([{transform:'none'},{transform:`translateY(${-3-e*5}px)`},{transform:'none'}],{duration:220}));op('#spg',.35+e*.35*(i%2?1:.6));if(e>.6&&i%2==0)spark(2);if(sc)P('ny',{b:i%2?-3:1,d:230});if(e>.5){P('ed',{h:i%2?5:-1,d:200});P('lc',{h:i%2?3:-3,d:200})}};
const jz=jazz(126,.35,{bass:[65.4,73.4,82.4,98,82.4,73.4,65.4,55],chords:[[261.6,329.6,392,493.9],[220,261.6,329.6,392],[293.7,349.2,440,523.3],[196,246.9,293.7,349.2]],chordEvery:8,beat:onBeat});
/* 舞台：夜莺已在话筒前，追光柔柔地亮着；乐手轻轻摇摆 */
op('#spt',.55);op('#spg',.3);sp(1,1290,1.2,.1,'#f3d08a');
pose('ny','singP',{show:1,d:700,f:1,face:'n'});
/* 尼尔从左边走进来，在小桌后坐下；酒保放下擦着的杯子，从吧台后绕过来坐在他旁边 */
let wiping=true;pose('bm','wipe',{show:1,f:0,face:'n'});(async()=>{try{while(wiping&&k===gen){gl('gl',827,586,1,170,10);await w(200);gl('gl',815,594,1,170,-10);await w(200)}}catch(e){}})();
await w(1800);
P('nl',{show:1,d:0});pose('nl','stand',{face:'n'});go('nl',230,3000);await w(3200);
mv('nl',230,818,600,'ease-out');pose('nl','sitT',{face:'smile'});await w(1200);
wiping=false;gl('gl',790,650,1,300);pose('bm','idleDown',{f:0,face:'n'});await w(400);
go('bm',330,2800);await w(3000);mv('bm',330,818,600,'ease-out');pose('bm','sitT',{f:0,face:'smile'});await w(900);
/* 露西、艾迪各自从右边进来，不挨着他们：露西靠在吧台后，艾迪靠在卡座隔断边 */
pose('lc','lean',{show:1,f:1,face:'smile'});go('lc',780,4200);
at(1800,()=>{pose('ed','lean',{show:1,f:1,face:'n'});go('ed',1010,4200)});
await w(4600);
pose('lc','lean',{f:0,face:'smile'});pose('ed','lean',{f:1,face:'smile'});await w(1000);
/* 镜头缓缓推向舞台；伴奏渐渐满起来 */
dolly(1.2,58,58,6000);jz.set(.55);jz.energy(.25);await w(2600);
/* 第一段：轻快里带点怀念 */
const V=[[392,1],[440,1],[523.3,1.5],[440,.5],[392,1],[329.6,1],[392,2],[349.2,1],[392,1],[440,1.5],[392,.5],[349.2,1],[293.7,1],[329.6,2]];
const C=[[523.3,1],[587.3,1],[659.3,2],[587.3,1],[523.3,1],[440,2],[523.3,1],[587.3,1],[659.3,1.5],[784,.5],[659.3,2],[523.3,2]];
let d=vocal(V,476,1);
at(d-1800,()=>{jz.energy(.55);pose('ny','singHi',{f:1,face:'smile'})});
await w(d);
/* 副歌：空间开始跟着拍子跳，光点往上飘 */
jz.set(.75);jz.energy(.8);pose('ny','singHi',{f:1,face:'smile'});pose('nl','sitT',{face:'smile',h:2});pose('bm','sitT',{f:0,face:'smile',h:2});
d=vocal(C,476,1.1);await w(d);
/* 最高点：再来一遍副歌，唱到最高音时全场炸开 */
jz.energy(1);jz.set(.9);op('#spt',1);sp(1,1290,1.4,.22,'#ffd98a');pose('ny','singUp',{f:1,face:'smile'});
d=vocal(C,476,1.2);at(d-2200,()=>{vocal([[784,4]],476,1.3);pose('ny','singUp',{f:1,face:'smile'})});
await w(d);
cheer(crowd,2800);cheer(band,2800);spark(30);pulse(.03);at(260,()=>pulse(.03));at(520,()=>pulse(.02));
for(const c of['nl','bm','lc','ed'])P(c,{h:-4,d:300});
await w(2400);
/* 回落：能量归零，灯暗下来，只剩轻轻的哼唱；尼尔安静下来，轻轻叹一口气 */
jz.energy(0);jz.set(.2);mu.set(.1);sc=0;op('#spt',.5);op('#spg',.25);sp(1,1290,1.1,.08,'#f3d08a');
pose('ny','singP',{f:1,face:'smile',h:2});pose('nl','sitT',{face:'n',h:6});pose('bm','sitT',{f:0,face:'n',h:6});pose('lc','lean',{f:0,face:'n'});pose('ed','lean',{f:1,face:'n'});
vocal([[440,1.5],[392,1.5],[329.6,3]],700,.8);
await w(1800);sigh('nl',1600);await w(1500);
dolly(1,50,60,4000);await w(3800);
tn(261.6,2.6,'triangle',.06);tn(329.6,2.6,'triangle',.05,.03);tn(392,2.6,'triangle',.05,.06);await w(2600);
op('#spt',.15);jz.stop();mu.stop();await w(1800)}},
