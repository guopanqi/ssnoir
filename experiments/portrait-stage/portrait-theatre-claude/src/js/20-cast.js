const CH={
ny:{w:40,coat:'#27474d',cf:'#27474d',pants:'#15191c',hair:'#150f12',skin:'#dcb99c',lip:'#a3303f',
bd:w=>`M${-w} -366 Q${-w} -392 ${-w*.5} -396 L${w*.5} -396 Q${w} -392 ${w} -366 L${w*.8} -272 Q${w*.55} -246 ${w*.95} -215 L${w*1.4} -88 L${-w*1.4} -88 L${-w*.95} -215 Q${-w*.55} -246 ${-w*.8} -272Z`,
dc:'<path d="M-22 -396 L0 -322 L22 -396" fill="none" stroke="#0f2326" stroke-width="3"/><path d="M-30 -392 Q0 -366 30 -392 L24 -362 Q0 -346 -24 -362Z" fill="#b8893a"/><path d="M-32 -262 Q0 -250 32 -262" stroke="#0f2326" stroke-width="8" fill="none"/><circle cx="-14" cy="-340" r="4" fill="#d9b66a"/>',
hb:'<path d="M-36 -470 Q-50 -430 -44 -390 Q-30 -366 -14 -388 L14 -388 Q30 -366 44 -390 Q50 -430 36 -470 Q30 -514 0 -514 Q-30 -514 -36 -470Z" fill="#150f12"/>',
hf:'<path d="M-33 -462 Q-38 -510 0 -512 Q40 -510 34 -468 Q20 -498 -6 -492 Q-24 -488 -33 -462Z" fill="#150f12"/><path d="M-12 -506 Q10 -508 26 -490" stroke="#4a3a40" stroke-width="2" fill="none"/><g class="hat" opacity="0"><path d="M-42 -486 Q-34 -532 8 -528 Q48 -522 46 -488 Q0 -500 -42 -486Z" fill="#6e1f2d"/><path d="M-42 -486 Q0 -500 46 -488" stroke="#2b0b11" stroke-width="3" fill="none"/></g>',
ex:'<g class="bag" opacity="0"><rect x="-76" y="-272" width="46" height="58" rx="8" fill="#7a4e34"/><path d="M-60 -272 L20 -386" stroke="#7a4e34" stroke-width="5"/></g><g class="umo" opacity="0"><line x1="-50" y1="-205" x2="-50" y2="-610" stroke="#222" stroke-width="5"/><path d="M-190 -590 Q-50 -700 90 -590 Q40 -620 -50 -592 Q-140 -620 -190 -590Z" fill="#2b3548"/></g><g class="umc" opacity="0"><line x1="-50" y1="-205" x2="-62" y2="-8" stroke="#1f3a48" stroke-width="7"/></g>'},
nl:{w:54,coat:'#5a5348',cf:'#d8d2c4',pants:'#1f2125',hair:'#2a2420',skin:'#c9a587',lip:'#3a2620',
bd:w=>`M${-w} -366 Q${-w} -394 ${-w*.55} -398 L${w*.55} -398 Q${w} -394 ${w} -366 L${w*1.08} -240 L${w*1.25} -70 L${-w*1.25} -70 L${-w*1.08} -240Z`,
dc:'<path d="M-30 -398 L-50 -440 L-14 -416Z" fill="#6a6358"/><path d="M30 -398 L50 -440 L14 -416Z" fill="#6a6358"/><path d="M-26 -396 L-6 -296 L-42 -300 L-50 -380Z" fill="#4a443a"/><path d="M26 -396 L6 -296 L42 -300 L50 -380Z" fill="#4a443a"/><path d="M-8 -390 L8 -390 L5 -300 L-5 -300Z" fill="#2a1a1a"/><rect x="-68" y="-262" width="136" height="12" fill="#2a2620"/><rect x="-6" y="-264" width="12" height="16" fill="#8a7a50"/>',
hb:'',
hf:'<path d="M-35 -490 Q-37 -544 0 -546 Q37 -544 35 -490Z" fill="#2c2924"/><path d="M-36 -500 L36 -500 L35 -490 L-35 -490Z" fill="#12100d"/><path d="M-14 -546 Q0 -530 14 -546" stroke="#14120f" stroke-width="3" fill="none"/><ellipse cx="0" cy="-489" rx="60" ry="10" fill="#34302a" transform="rotate(-3)"/><path d="M-30 -484 Q0 -470 30 -484 L30 -472 Q0 -462 -30 -472Z" fill="#000" opacity=".35"/>',ex:CIG},
mg:{w:74,coat:'#5b3d24',cf:'#e8e4d8',pants:'#241a12',hair:'#4a4a4a',skin:'#d8b08e',lip:'#6a3a30',
bd:w=>`M${-w} -366 Q${-w} -392 ${-w*.55} -396 L${w*.55} -396 Q${w} -392 ${w} -366 Q${w*1.15} -300 ${w*1.12} -230 L${w*1.15} -110 L${-w*1.15} -110 L${-w*1.12} -230 Q${-w*1.15} -300 ${-w} -366Z`,
dc:'<path d="M-28 -396 L0 -318 L28 -396Z" fill="#e8e4d8"/><path d="M-9 -390 L9 -390 L13 -300 L0 -276 L-13 -300Z" fill="#a82a2a"/><path d="M-30 -396 L-4 -312 L-58 -296 L-70 -376Z" fill="#46301c"/><path d="M30 -396 L4 -312 L58 -296 L70 -376Z" fill="#46301c"/><rect x="38" y="-330" width="22" height="8" fill="#e8e4d8"/>',
hb:'',
hf:'<path d="M-32 -468 Q-36 -510 0 -512 Q36 -510 32 -468 Q18 -494 0 -492 Q-18 -494 -32 -468Z" fill="#4a4a4a"/><path d="M-14 -446 Q0 -452 14 -446 Q0 -441 -14 -446Z" fill="#2a2a2a"/>',ex:''},
wk:{w:46,coat:'#3b4a3b',cf:'#3b4a3b',pants:'#2b382b',hair:'#222',skin:'#b98f6c',lip:'#4a2a20',
bd:w=>`M${-w} -366 Q${-w} -392 ${-w*.5} -396 L${w*.5} -396 Q${w} -392 ${w} -366 L${w*1.1} -100 L${-w*1.1} -100 L${-w*1.1} -250Z`,
dc:'<path d="M-30 -392 L30 -392 L34 -240 L-34 -240Z" fill="#5a4630"/>',hb:'',
hf:'<path d="M-34 -480 Q-30 -518 8 -518 Q40 -516 36 -482Z" fill="#b87326"/><path d="M10 -484 L60 -484 Q50 -473 10 -476Z" fill="#b87326"/>',ex:''}};
function mk(id,c){const w=c.w,sk=c.skin,arm=s=>{const x=s*w,hx=x+s*8,p=`M${x-17} -376 Q${x-18} -300 ${hx-12} -214 L${hx+12} -214 Q${x+18} -300 ${x+17} -376Z`;return `<g class="${s<0?'aL':'aR'}" style="transform-origin:${x}px -372px"><path d="${p}" fill="${c.coat}"/><path d="${p}" fill="url(#shd)"/><rect x="${hx-13}" y="-222" width="26" height="9" rx="3" fill="${c.cf}"/><ellipse cx="${hx}" cy="-199" rx="10.5" ry="15" fill="${sk}"/><ellipse cx="${hx}" cy="-199" rx="10.5" ry="15" fill="url(#shd)"/></g>`};
const bd=c.bd(w),face='M-30 -462 Q-30 -418 0 -413 Q30 -418 30 -462 Q30 -494 0 -494 Q-30 -494 -30 -462Z',eye=x=>`<ellipse cx="${x}" cy="-462" rx="6" ry="3.2" fill="#e9e2d6"/><circle cx="${x}" cy="-462" r="2.5" fill="#16110e"/><path d="M${x-7} -463 Q${x} -468 ${x+7} -463" stroke="#16110e" stroke-width="1.6" fill="none"/>`;
return `<g id="${id}" class="ch" style="opacity:0"><ellipse cx="0" cy="2" rx="${w+36}" ry="12" fill="#000" opacity=".4"/><g class="in"><g class="lean"><g class="wb">
<g class="lgL" style="transform-origin:${-w*.45}px -150px"><path d="M${-w*.7} -150 L${-w*.2} -150 L${-w*.22} -8 L${-w*.75} -8Z" fill="${c.pants}"/><ellipse cx="${-w*.5+6}" cy="-5" rx="${w*.45}" ry="8" fill="#0a0a0a"/></g><g class="lgR" style="transform-origin:${w*.45}px -150px"><path d="M${w*.2} -150 L${w*.7} -150 L${w*.75} -8 L${w*.22} -8Z" fill="${c.pants}"/><ellipse cx="${w*.5+6}" cy="-5" rx="${w*.45}" ry="8" fill="#0a0a0a"/></g>
<path d="${bd}" fill="${c.coat}"/><path d="${bd}" fill="url(#shd)"/><path d="${bd}" fill="none" stroke="url(#rim)" stroke-width="3"/>${c.dc}${arm(-1)}${arm(1)}
<path d="M-12 -424 L12 -424 L14 -384 L-14 -384Z" fill="${sk}"/><path d="M-12 -424 L12 -424 L14 -384 L-14 -384Z" fill="#000" opacity=".3"/>
<g class="hd" style="transform-origin:0px -410px"><g class="bob">${c.hb}<path d="${face}" fill="${sk}"/><path d="${face}" fill="url(#shd)"/>
<g class="face"><path d="M2 -458 Q7 -446 0 -443" stroke="#000" stroke-opacity=".3" stroke-width="2" fill="none"/><g class="eyes">${eye(-13)}${eye(13)}</g><g fill="none" stroke-linecap="round"><g stroke="${c.hair}" stroke-width="3.6"><path class="bl"/><path class="br"/></g><path class="mo" stroke="${c.lip}" stroke-width="3.6"/></g><g class="tr" opacity="0"><circle cx="-14" cy="-455" r="3.5" fill="#a9dcff"/><circle cx="14" cy="-455" r="3.5" fill="#a9dcff"/></g></g>
<ellipse class="bk" cx="0" cy="-456" rx="34" ry="41" fill="${c.hair}" opacity="0"/>${c.hf}</g></g>${c.ex}</g></g></g></g>`}
const G={};
for(const id of['nl','mg','wk','ny']){$('#chs').insertAdjacentHTML('beforeend',mk(id,CH[id]));const g=$('#'+id);g.st={x:-400,y:790,s:1};G[id]=g}
function P(id,o){const g=G[id],q=s=>g.querySelector(s),st=g.st;g.style.transitionDuration=(o.d??700)+'ms';
for(const k of['x','y','s'])if(k in o)st[k]=o[k];
if('x'in o||'y'in o||'s'in o)g.style.transform=`translate(${st.x}px,${st.y}px) scale(${st.s})`;
if('show'in o)g.style.opacity=o.show;
if('f'in o)q('.in').style.transform=`scaleX(${o.f?-1:1})`;
if('b'in o)q('.lean').style.transform=`rotate(${o.b}deg)`;
if('h'in o)q('.hd').style.transform=`rotate(${o.h}deg)`;
if('al'in o)q('.aL').style.transform=`rotate(${o.al}deg)`;
if('ar'in o)q('.aR').style.transform=`rotate(${o.ar}deg)`;
if('ll'in o)q('.lgL').style.transform=`rotate(${o.ll}deg)`;
if('lr'in o)q('.lgR').style.transform=`rotate(${o.lr}deg)`;
if(o.face){const f=SKY[o.face];q('.mo').setAttribute('d',f[0]);q('.bl').setAttribute('d',f[1]);q('.br').setAttribute('d',f[2])}
if('tr'in o)q('.tr').style.opacity=o.tr;
if('look'in o)q('.eyes').style.transform=`translateX(${o.look}px)`;
if('back'in o){q('.face').style.opacity=o.back?0:1;q('.bk').style.opacity=o.back?1:0}
for(const k of['hat','bag','umo','umc','cg'])if(k in o){const e=q('.'+k);if(e)e.style.opacity=o[k]}
if(o.cl)for(const c in o.cl)g.classList.toggle(c,!!o.cl[c])}


CH.fr={w:70,coat:'#2d2d33',cf:'#cfcabd',pants:'#1c1c20',hair:'#1a1a1a',skin:'#c4977a',lip:'#4a2a22',bd:CH.mg.bd,
dc:'<path d="M-28 -396 L0 -318 L28 -396Z" fill="#b8b2a4"/><path d="M-30 -396 L-4 -312 L-62 -296 L-72 -376Z" fill="#202026"/><path d="M30 -396 L4 -312 L62 -296 L72 -376Z" fill="#202026"/>',hb:'',
hf:'<path d="M-32 -470 Q-34 -508 0 -510 Q34 -508 32 -470 Q16 -490 0 -488 Q-16 -490 -32 -470Z" fill="#1a1a1a"/><path d="M-20 -468L-9 -450" stroke="#7a4a40" stroke-width="2.4"/>',ex:CIG};
CH.bn={w:52,coat:'#2b3750',cf:'#cfcabd',pants:'#1b2230',hair:'#777',skin:'#d1ad8f',lip:'#5a3a30',bd:CH.nl.bd,
dc:'<path d="M-24 -396 L0 -330 L24 -396Z" fill="#cfcabd"/><rect x="-60" y="-262" width="120" height="10" fill="#1a2234"/><circle cx="-24" cy="-340" r="6" fill="#d8b252"/><path d="M-4 -396V-250" stroke="#d8b252" stroke-width="2"/>',hb:'',
hf:'<path d="M-38 -490 Q-36 -534 0 -534 Q36 -534 38 -490Z" fill="#1f2b44"/><path d="M-44 -490H44L50 -480H-50Z" fill="#12182a"/><circle cx="0" cy="-508" r="5" fill="#d8b252"/><path d="M-30 -484 Q0 -470 30 -484 L30 -472 Q0 -462 -30 -472Z" fill="#000" opacity=".3"/>',ex:''};
CH.ld={w:66,coat:'#4c4237',cf:'#d9d2bf',pants:'#2a2622',hair:'#8d8a84',skin:'#d4ae90',lip:'#6a3a30',bd:CH.mg.bd,
dc:'<path d="M-26 -396 L0 -322 L26 -396Z" fill="#d9d2bf"/><path d="M-30 -396 L-4 -312 L-56 -296 L-66 -376Z" fill="#3a3229"/><path d="M30 -396 L4 -312 L56 -296 L66 -376Z" fill="#3a3229"/><circle cx="0" cy="-340" r="5" fill="#2a241c"/><circle cx="0" cy="-306" r="5" fill="#2a241c"/>',hb:'',
hf:'<path d="M-32 -480 Q-34 -508 0 -510 Q34 -508 32 -480 Q20 -492 0 -490 Q-20 -492 -32 -480Z" fill="#8d8a84"/><path d="M-14 -444 Q0 -450 14 -444 Q0 -438 -14 -444Z" fill="#6d6a64"/>',ex:''};
CH.sk={w:56,coat:'#3a4640',cf:'#d8d2c4',pants:'#25231f',hair:'#7a7a78',skin:'#d0aa8c',lip:'#6a3a30',bd:CH.nl.bd,
dc:'<path d="M-24 -396 L0 -330 L24 -396Z" fill="#d8d2c4"/><path d="M-28 -396 L-6 -300 L-46 -300 L-54 -382Z" fill="#2c3632"/><path d="M28 -396 L6 -300 L46 -300 L54 -382Z" fill="#2c3632"/><rect x="-62" y="-330" width="8" height="40" fill="#8a2b3a"/><rect x="54" y="-330" width="8" height="40" fill="#8a2b3a"/>',hb:'',
hf:'<path d="M-33 -474 Q-36 -496 -24 -502 Q-32 -480 -33 -474Z M33 -474 Q36 -496 24 -502 Q32 -480 33 -474Z" fill="#7a7a78"/><circle cx="-13" cy="-462" r="11" fill="none" stroke="#2a2a2a" stroke-width="2.5"/><circle cx="13" cy="-462" r="11" fill="none" stroke="#2a2a2a" stroke-width="2.5"/><path d="M-2 -462H2M-24 -462L-33 -466M24 -462L33 -466" stroke="#2a2a2a" stroke-width="2.2"/>',ex:''};
const VC='<path d="M-34 -480 Q-30 -518 8 -518 Q40 -516 36 -482Z" fill="#4a4a50"/><path d="M10 -484 L60 -484 Q50 -473 10 -476Z" fill="#4a4a50"/>';
for(const[id,c]of[['fr',CH.fr],['bn',CH.bn],['bn2',{...CH.bn,coat:'#323f55'}],['ld',CH.ld],['sk',CH.sk],['v1',{...CH.wk,coat:'#3a3730',pants:'#25231f',hf:VC}],['v2',{...CH.wk,coat:'#6a5a3a',pants:'#2a221b',hf:VC}]]){$('#chs').insertAdjacentHTML('beforeend',mk(id,c));const g=$(`#${id}`);g.st={x:-400,y:790,s:1};G[id]=g}

