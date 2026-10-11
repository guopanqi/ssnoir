{n:'剧院的安全',sub:'剧院 · 封堵漏洞，筹备首演',sh:'安全',pos:[1530,120],bg:'#08090d',set:'#set3',lx:1000,i:['34%','62%'],mk:()=>SETS.exitroom(),
ini(){$('#props').innerHTML=PLANK;P('nl',{d:0,x:-150,s:.56,face:'hard'})},
async run(k,{w,go,fl,nar,mono}){
await w(500);await fl();lamp(1);P('nl',{show:1,d:0});go('nl',900,2600);await w(2800);
await nar('（调查剧院的安全。封堵漏洞，筹备。）',1800);
go('nl',540,2000,{f:1});await w(2200);P('nl',{ar:-75,h:5});await w(900);
for(let i=0;i<3;i++){op('#plk',.35+i*.3);bs(.07,.6,600);await w(450)}op('#plk',1);P('nl',{ar:0,h:0,face:'n'});await w(900);
await mono('莱恩这种头脑的人，根本就不会在这里下功夫。');
cc();P('nl',{f:0});go('nl',1500,3200);await w(3000);
op('#plk',.2);$('#lk').setAttribute('fill','#e0584a');op('#gap',.9);tn(120,.5,'sawtooth',.05);await fl();lamp(.7);await w(900);
await nar('（但是后来你发现，还是出了问题。）',3200);await w(800)}},
