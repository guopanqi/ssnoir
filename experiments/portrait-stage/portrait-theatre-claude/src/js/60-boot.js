SC.forEach((s,i)=>{s.o=s.pos[0]+'px '+s.pos[1]+'px';s.num=['一','二','三','四','五','六','七','八','九','十','十一','十二','十三','十四','十五','十六','十七','十八','十九'][i]});
/* world markers */
(function(){let h='';SC.forEach((s,i)=>{const[x,y]=s.pos;h+=`<rect x="${x-34}" y="${y-48}" width="68" height="92" fill="#05070e"/><g class="mkr" data-i="${i}"><g filter="url(#glow)"><rect x="${x-12}" y="${y-22}" width="24" height="30" fill="#0b0f1c" stroke="#ffd98a" stroke-width="2.5"/><rect x="${x-18}" y="${y+6}" width="36" height="8" fill="#0b0f1c" stroke="#ffd98a" stroke-width="2"/></g><circle class="pl" cx="${x}" cy="${y-6}" r="30"/><circle cx="${x}" cy="${y-6}" r="40" fill="rgba(0,0,0,0)" style="pointer-events:all"/><text x="${x}" y="${y-32}" text-anchor="middle" font-size="17" letter-spacing="2" fill="#ffd98a" opacity=".9" font-family="serif">${s.num} ${s.sh}</text></g>`});$('#hm')&&$('#hm').remove();$('#world').insertAdjacentHTML('beforeend',h)})();
function reset(){const W=$('#world'),stg=$('#stage');W.style.cssText='transition:none;transform:scale(1);filter:none;opacity:1';
document.querySelectorAll('.set').forEach(s=>{s.style.transition='none';s.setAttribute('class','set');s.style.opacity=0});void W.offsetWidth;W.style.cssText='';document.querySelectorAll('.set').forEach(s=>s.style.transition='');
$('#set3').innerHTML='';$('#props').innerHTML='';$('#front').innerHTML='';$('#front').style.opacity=1;fxf('none',0);pk(0,0,.01,0,0,10);gh(1000,670,.3,0);
$('#leaf').style.transform='';for(const s of['#bt','#sg','#dl'])op(s,0);$('#nw').classList.remove('lit');$('#vr').classList.remove('on');$('#fx').classList.remove('rn');
for(const id in G)P(id,{d:0,show:0,x:-400,y:790,s:.56,f:0,b:0,h:0,al:0,ar:0,face:'n',tr:0,look:0,back:0,hat:0,bag:0,umo:0,umc:0,cg:0,ll:0,lr:0,cl:{tk:0,wk:0,sob:0,run:0,snap:0}});
sp(1,800,1,0);sp(2,800,1,0);rainI=0;rainS(0);cc();$('#title').classList.remove('on');stg.style.setProperty('--ix','50%');stg.style.background='#05070e'}
const chips=()=>document.querySelectorAll('#chips button').forEach((b,i)=>b.classList.toggle('cur',i===cur));
function idle(msg){reset();$('#stage').classList.add('idl');$('#title').innerHTML=msg||'<h1>立绘剧场 · 线绘世界</h1><p>亮着的地方藏着故事 · 点击地图上的地点，或使用下方按钮</p>';$('#title').classList.add('on');chips()}
async function chapter(S,k){const stg=$('#stage'),W=$('#world'),h=H(k),w=h.w;reset();stg.classList.remove('idl');
stg.style.setProperty('--i1',S.i?S.i[0]:'20%');stg.style.setProperty('--i2',S.i?S.i[1]:'52%');
SET=$(S.set);if(S.mk)SET.innerHTML=S.mk();SET.dataset.lx=S.lx||800;S.ini();W.style.transformOrigin=S.o;
$('#title').innerHTML=`<h1>${S.num} · ${S.n}</h1><p>${S.sub}</p>`;await w(400);$('#title').classList.add('on');await w(3200);$('#title').classList.remove('on');await w(600);
whoosh();W.style.transform='scale(7.2)';W.style.filter='blur(6px) brightness(1.4)';W.style.opacity=0;
await w(2200);SET.style.opacity=1;SET.classList.add('on');stg.style.background=S.bg;await w(2700);SET.classList.add('f');
await S.run(k,h);
SET.style.opacity=0;$('#front').style.opacity=0;op('#pack',0);op('#gh',0);rainI=0;rainS(0);cc();$('#props').innerHTML='';await w(900);
W.style.transition='transform 3.8s cubic-bezier(.65,0,.25,1),filter 3.8s,opacity 1.4s';W.style.transform='scale(1)';W.style.filter='none';W.style.opacity=1;stg.style.background='#05070e';whoosh();await w(4000)}
async function play(i,seq){ai();if(AC.state==='suspended')AC.resume();const k=++gen;if(paused)tgl(true);$('#pp').disabled=$('#rp').disabled=false;
try{for(let j=i;j<SC.length;j++){cur=j;chips();await chapter(SC[j],k);if(!seq)break}
idle(seq?'<h1>终</h1><p>立绘剧场 · 线绘世界 · 全剧连播结束</p>':'<h1>'+SC[cur].num+' · '+SC[cur].n+' · 完</h1><p>选择下一个地点，或重播</p>')}catch(e){if(e!==0)throw e}}
function tgl(f){paused=f?false:!paused;if(paused){held=document.getAnimations().filter(a=>a.playState==='running');held.forEach(a=>a.pause());AC&&AC.suspend()}else{held.forEach(a=>{try{a.play()}catch(e){}});held=[];AC&&AC.resume()}$('#pp').textContent=paused?'继续':'暂停'}
SC.forEach((s,i)=>{const b=document.createElement('button');b.textContent=s.num+' '+s.sh;b.title=s.n;b.onclick=()=>play(i);$('#chips').appendChild(b)});
document.querySelectorAll('.mkr').forEach(m=>m.onclick=()=>play(+m.dataset.i));
$('#all').onclick=()=>play(0,true);$('#rp').onclick=()=>play(Math.max(cur,0));$('#pp').onclick=()=>tgl();
$('#ga').onclick=()=>{$('#chs').className.baseVal='a';$('#ga').classList.add('cur');$('#gb').classList.remove('cur')};
$('#gb').onclick=()=>{$('#chs').className.baseVal='b';$('#gb').classList.add('cur');$('#ga').classList.remove('cur')};
$('#mk').onclick=()=>{const s=$('#stage');s.classList.toggle('noiris');const on=!s.classList.contains('noiris');$('#mk').textContent='光孔遮罩 '+(on?'开':'关');$('#mk').classList.toggle('cur',on)};
$('#rx').onclick=()=>{react=!react;if(!react)SET.classList.remove('q','ul');$('#rx').textContent='背景反应 '+(react?'开':'关');$('#rx').classList.toggle('cur',react)};
$('#mu').onclick=()=>{muted=!muted;if(MG)MG.gain.value=muted?0:1;$('#mu').textContent=muted?'声音 关':'声音 开'};
addEventListener('keydown',e=>{if(e.code==='Space'&&!$('#pp').disabled){e.preventDefault();tgl()}});
idle();
