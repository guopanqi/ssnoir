/* 现场音乐（Web Audio 合成）：JZ = 爵士伴奏（走动贝斯 + 踩镲 + 稀疏钢琴和弦），VOCAL = 带颤音的人声旋律。
   都是 H(k) 里的工厂：章节被打断（k!==gen）时伴奏自动停。 */
const JZ=k=>(bpm=112,lv0=.6,o={})=>{let lv=lv0,en=0,b=0;const ms=60000/bpm/2,BASS=o.bass||[55,61.7,65.4,73.4,82.4,73.4,65.4,61.7],CH=o.chords||[[220,261.6,329.6,392],[196,246.9,293.7,392],[174.6,220,261.6,329.6],[196,246.9,293.7,370]],ce=o.chordEvery||16;
/* energy(0..1)：0 只有贝斯/踩镲/和弦；越高越加底鼓、2&4 拍的拍手、八分音符琶音。o.beat(拍序, 当前能量) 每个四分音符回调一次，供画面跟着跳 */
const iv=setInterval(()=>{if(k!==gen){clearInterval(iv);return}if(paused||lv<=0)return;
const ch=CH[((b/ce)|0)%CH.length];
if(b%2==0){tn(BASS[(b/2)%BASS.length|0],.38,'triangle',.13*lv);bs(.04,.07*lv,7000);if(en>0)tn(60,.14,'sine',.28*en*lv);if(o.beat)o.beat(b/2,en)}else if(b%4==3)bs(.03,.04*lv,7000);
if(en>0){if(b%4==2)bs(.09,.18*en*lv,3200);tn(ch[b%4]*2,.14,'triangle',.05*en*lv)}
if(b%ce==0)ch.forEach((f,i)=>tn(f,1.6,'triangle',.045*lv,i*.03));
if(b%8==4&&!o.bass)tn(130+Math.random()*10,.12,'sine',.05*lv);b++},ms);
return{set:v=>{lv=v},energy:v=>{en=v},stop:()=>{lv=0;en=0;clearInterval(iv)}}};
/* 人声：notes=[[Hz,拍数],…]，每拍 beatMs 毫秒，返回总时长(ms)。基音 + 八度泛音 + 5.5Hz 颤音。 */
const VOCAL=k=>(notes,beatMs=536,lv=1)=>{if(!AC)return notes.reduce((a,n)=>a+n[1]*beatMs,0);let t=AC.currentTime+.05;
for(const[f,bt]of notes){const d=bt*beatMs/1000;if(f>0){const o=AC.createOscillator(),o2=AC.createOscillator(),g=AC.createGain(),lf=AC.createOscillator(),lg=AC.createGain();o.type='sine';o2.type='triangle';o.frequency.value=f;o2.frequency.value=f*2;lf.frequency.value=5.5;lg.gain.value=f*.012;lf.connect(lg);lg.connect(o.frequency);lg.connect(o2.frequency);
g.gain.setValueAtTime(0,t);g.gain.linearRampToValueAtTime(.09*lv,t+.08);g.gain.setValueAtTime(.09*lv,t+d*.75);g.gain.linearRampToValueAtTime(0,t+d);const g2=AC.createGain();g2.gain.value=.25;o.connect(g);o2.connect(g2);g2.connect(g);g.connect(MG);for(const x of[o,o2,lf]){x.start(t);x.stop(t+d+.05)}}t+=d}
return notes.reduce((a,n)=>a+n[1]*beatMs,0)};
