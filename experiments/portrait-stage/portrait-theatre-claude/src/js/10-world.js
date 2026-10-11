/* world map */
(function(){const R=(a,b)=>a+Math.random()*(b-a);let h='<rect width="1600" height="900" fill="#05070e"/><path d="M-100 360 Q800 250 1700 400 L1700 500 Q800 400 -100 470Z" fill="#0a1224" stroke="#26365a" stroke-width="2"/>';
for(let i=0;i<9;i++)h+=`<path d="M800 440 L${-300+i*250} ${i%2?1000:-100}" stroke="#16223d" stroke-width="3" fill="none"/>`;
for(let i=0;i<460;i++){const x=R(0,1560),y=R(0,860);if(y>290&&y<500)continue;if(Math.abs(x-900)<70&&Math.abs(y-590)<70)continue;const w=R(10,26),t=R(7,16),f=R(8,22);
h+=`<path d="M${x} ${y} l-4 -6 h${w} l4 6z" fill="#101a30" stroke="#34466e" stroke-width="1"/><rect x="${x}" y="${y}" width="${w}" height="${t+f}" fill="#070b16" stroke="#34466e" stroke-width="1"/>`;
if(Math.random()<.3)h+=`<rect x="${x+3}" y="${y+t}" width="3" height="3" fill="#f2c36e"/><rect x="${x+w-7}" y="${y+t+4}" width="3" height="3" fill="#f2c36e"/>`}
for(let i=0;i<7;i++){const x=R(80,1500),y=R(40,820);if(y>270&&y<520)continue;h+=`<rect x="${x}" y="${y}" width="22" height="30" fill="none" stroke="#f2c36e" stroke-width="2" opacity=".75"/>`}
h+='<g id="hm"><rect x="880" y="565" width="40" height="52" fill="#0b0f1c" stroke="#ffd98a" stroke-width="3"/><rect x="872" y="612" width="56" height="12" fill="#0b0f1c" stroke="#ffd98a" stroke-width="2"/></g>';
$('#world').innerHTML=h})();
/* apartment facade (Neil's building) */
(function(){let h='';const L=(d,c='',dl=0,sw=2,col)=>`<path class="${c}" d="${d}" pathLength="1" style="--d:${dl}s;stroke-width:${sw};${col?'stroke:'+col:''}"/>`,
Rc=(x,y,w,hh,c,dl,sw,col,id)=>`<path ${id?`id="${id}" `:''}class="${c}" d="M${x} ${y}h${w}v${hh}h${-w}z" pathLength="1" style="--d:${dl}s;stroke-width:${sw};${col?'stroke:'+col:''}"/>`;
h+=L('M0 790H1600','',0,1,'#2a3a60')+Rc(460,-30,680,820,'s',.2,2,'#9fb4e0')+L('M450 -30V790M1150 -30V790','',.3,1.5,'#6f86b8');
for(let r=0;r<4;r++)for(let c=0;c<5;c++){const id=(c==3&&r==2)?'nw':'';h+=Rc(500+c*118,60+r*110,56,70,'s'+((c*5+r*3)%7==1&&!id?' lit2':''),1+(c+r)*.08,1.5,'#6f86b8',id)}
h+=Rc(500,560,110,130,'s',1.6,1.5,'#6f86b8')+L('M555 560V690M500 625H610','',1.8,1,'#4f6490')+Rc(990,560,110,130,'s',1.7,1.5,'#6f86b8')+L('M1045 560V690M990 625H1100','',1.9,1,'#4f6490');
for(const y of[150,260,370,480])h+=L(`M1150 ${y}h120M1270 ${y}V${y+8}`,'',2+y/400,1.5,'#7f95c4')+L(`M1270 ${y+8}L1150 ${y+110}`,'',2.2+y/400,1.2,'#5f759f');
h+=L('M690 482L910 482L930 450L670 450Z','s',1.5,2.5,'#e6eeff')+Rc(710,774,180,16,'s',1.6,2,'#9fb4e0');
h+=`<rect id="dl" x="735" y="490" width="130" height="284" fill="#f2c36e" opacity="0" style="transition:opacity .8s"/>`+Rc(729,484,142,290,'',1.3,1.5,'#7f95c4');
h+='<g id="leaf" style="transform-origin:735px 490px;transition:transform .9s ease">'+Rc(735,490,130,284,'s door',1.4,2.5,'#e9eef8')+Rc(748,505,48,110,'door',1.7,1.5)+Rc(804,505,48,110,'door',1.7,1.5)+Rc(748,630,48,128,'door',1.9,1.5)+Rc(804,630,48,128,'door',1.9,1.5)+'</g>';
h+=Rc(900,548,44,92,'s',2,2,'#9fb0d8')+L('M908 556H936M908 563H936M908 570H936','',2.2,1.5,'#9fb0d8');
for(const y of[590,610,628])h+=`<path d="M${918} ${y}a6 6 0 1 0 12 0a6 6 0 1 0 -12 0" pathLength="1" style="--d:2.3s;stroke-width:1.5;stroke:#9fb0d8"/>`;
h+='<circle id="bt" cx="924" cy="590" r="8" fill="#ffbf55" opacity="0" style="transition:opacity .2s"/><circle id="sg" cx="922" cy="563" r="22" fill="#ffbf55" opacity="0" style="transition:opacity .25s"/>';
h+='<g class="vr" id="vr"><circle cx="882" cy="315" r="12"/><circle cx="882" cy="315" r="12"/><circle cx="882" cy="315" r="12"/></g><text id="sn" x="800" y="430" text-anchor="middle" font-size="24" letter-spacing="4" fill="none" stroke="#e0bd70" stroke-width="1.2" font-family="serif">17</text>';
$('#hotel').innerHTML=h})();
/* street: road strip + lamp */
(function(){const L=(d,c='',dl=0,sw=2,col)=>`<path class="${c}" d="${d}" pathLength="1" style="--d:${dl}s;stroke-width:${sw};${col?'stroke:'+col:''}"/>`,el=(rx,ry)=>`M${800-rx} 790a${rx} ${ry} 0 1 0 ${2*rx} 0a${rx} ${ry} 0 1 0 ${-2*rx} 0`;
let h='<g mask="url(#fade)">'+L('M0 790H1600','',0,1.5,'#5a6f9c')+L('M0 806H1600','',.4,1,'#33466e');
for(let x=60;x<1600;x+=190)h+=L(`M${x} 860h90`,'',.6+Math.abs(x-800)/700,3,'#46608f');
h+='</g>'+L(el(250,24),'gd',1.2,1.5,'#d9b86a;stroke-opacity:.55')+L(el(165,15),'gd',1.5,1.5,'#d9b86a;stroke-opacity:.4')+L(el(85,8),'gd',1.8,1.5,'#d9b86a;stroke-opacity:.3');
h+='<path class="cone" d="M792 270L808 270L1040 790L560 790Z" fill="url(#cn)" stroke="none" style="opacity:0;transition:opacity .2s"/>'+L('M792 272L560 790','gd',2,1.5,'#d9b86a;stroke-opacity:.4')+L('M808 272L1040 790','gd',2,1.5,'#d9b86a;stroke-opacity:.4');
h+=L('M794 790V282M806 790V282','',1,2,'#cfe0ff')+L('M778 790h44v-20h-44z','s',1.2,2,'#cfe0ff')+L('M768 272h64l-16 -44h-32z','s',1.6,2.5,'#e6eeff')+L('M800 228v-16','',1.8,2,'#cfe0ff');
h+='<circle class="lh" cx="800" cy="266" r="46" fill="#f3d08a" opacity="0" style="transition:opacity .2s"/><circle class="lb" cx="800" cy="264" r="11" fill="#ffe6a8" opacity="0" style="transition:opacity .15s"/>';
h+=L('M800 814V884','gd',2.4,2,'#d9b86a;stroke-opacity:.35')+L('M784 830V876M816 830V876','gd',2.5,1.5,'#d9b86a;stroke-opacity:.25');
$('#lamp').innerHTML=h;
})();

const CIG='<g class="cg" opacity="0"><rect x="8" y="-436" width="28" height="4" rx="2" fill="#e8e2d2"/><circle cx="37" cy="-434" r="3.2" fill="#ff8a3a"/><path class="sm" d="M40 -438Q34 -460 42 -478Q36 -496 46 -514"/></g>';
const SKY={n:['M-9 -430 L9 -430','M-26 -478 L-8 -478','M8 -478 L26 -478'],
smile:['M-16 -438 Q0 -420 16 -438','M-26 -481 L-8 -481','M8 -481 L26 -481'],
sad:['M-10 -427 Q0 -437 10 -427','M-26 -472 L-8 -480','M8 -480 L26 -472'],
hard:['M-11 -430 L11 -432','M-26 -482 L-8 -474','M8 -474 L26 -482'],
shock:['M-5 -428 Q0 -420 5 -428 Q0 -438 -5 -428','M-26 -484 L-8 -488','M8 -488 L26 -484'],
drunk:['M-9 -427 Q0 -419 9 -430','M-26 -470 L-8 -477','M8 -481 L26 -475'],
wry:['M-10 -430 Q0 -434 10 -427','M-26 -480 L-8 -487','M8 -476 L26 -474']};
