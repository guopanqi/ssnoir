/* 现场音乐（Web Audio 合成）：JZ = 爵士伴奏（走动贝斯 + 踩镲 + 稀疏钢琴和弦），VOCAL = 带颤音的人声旋律。
   都是 H(k) 里的工厂：章节被打断（k!==gen）时伴奏自动停。 */
const JZ=k=>(bpm=112,lv0=.6)=>{let lv=lv0,b=0;const ms=60000/bpm/2,BASS=[55,61.7,65.4,73.4,82.4,73.4,65.4,61.7],CH=[[220,261.6,329.6,392],[196,246.9,293.7,392],[174.6,220,261.6,329.6],[196,246.9,293.7,370]];
const iv=setInterval(()=>{if(k!==gen){clearInterval(iv);return}if(paused||lv<=0)return;
if(b%2==0){tn(BASS[(b/2)%8|0],.38,'triangle',.13*lv);bs(.04,.07*lv,7000)}else if(b%4==3)bs(.03,.04*lv,7000);
if(b%16==0)CH[(b/16)%4|0].forEach((f,i)=>tn(f,1.6,'triangle',.045*lv,i*.03));
if(b%8==4)tn(130+Math.random()*10,.12,'sine',.05*lv);b++},ms);
return{set:v=>{lv=v},stop:()=>{lv=0;clearInterval(iv)}}};
/* 人声：notes=[[Hz,拍数],…]，每拍 beatMs 毫秒，返回总时长(ms)。基音 + 八度泛音 + 5.5Hz 颤音。 */
const VOCAL=k=>(notes,beatMs=536,lv=1)=>{if(!AC)return notes.reduce((a,n)=>a+n[1]*beatMs,0);let t=AC.currentTime+.05;
for(const[f,bt]of notes){const d=bt*beatMs/1000;if(f>0){const o=AC.createOscillator(),o2=AC.createOscillator(),g=AC.createGain(),lf=AC.createOscillator(),lg=AC.createGain();o.type='sine';o2.type='triangle';o.frequency.value=f;o2.frequency.value=f*2;lf.frequency.value=5.5;lg.gain.value=f*.012;lf.connect(lg);lg.connect(o.frequency);lg.connect(o2.frequency);
g.gain.setValueAtTime(0,t);g.gain.linearRampToValueAtTime(.09*lv,t+.08);g.gain.setValueAtTime(.09*lv,t+d*.75);g.gain.linearRampToValueAtTime(0,t+d);const g2=AC.createGain();g2.gain.value=.25;o.connect(g);o2.connect(g2);g2.connect(g);g.connect(MG);for(const x of[o,o2,lf]){x.start(t);x.stop(t+d+.05)}}t+=d}
return notes.reduce((a,n)=>a+n[1]*beatMs,0)};
