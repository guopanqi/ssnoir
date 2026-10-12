#!/usr/bin/env python3
"""按 Claude 室内求助的 WebAudio 频率与包络离线近似音效（固定噪声种子）。"""
import math, random, struct, wave, re, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'UnityClient/Assets/Resources/StageSounds/室内求助'
RATE=22050

def render(name, seconds, tones=(), bursts=()):
    data=[0.0]*int(seconds*RATE)
    for freq,duration,kind,volume,onset,attack in tones:
        for i in range(int(duration*RATE)):
            t=i/RATE; phase=(t*freq)%1
            v=math.sin(2*math.pi*phase) if kind=='sine' else (1-4*abs(phase-.5) if kind=='triangle' else (1 if phase < .5 else -1) if kind=='square' else 2*phase-1)
            env=t/attack if t<attack else math.exp(math.log(.0001/volume)*(t-attack)/max(.001,duration-attack))
            j=int(onset*RATE)+i
            if j<len(data): data[j]+=v*volume*env
    rng=random.Random(44)
    for duration,volume,freq,onset in bursts:
        value=0.; alpha=1-math.exp(-2*math.pi*freq/RATE)
        for i in range(int((duration+.05)*RATE)):
            value+=alpha*(rng.uniform(-1,1)-value)
            j=int(onset*RATE)+i
            if j<len(data): data[j]+=value*volume*math.exp(math.log(.0001/volume)*(i/RATE)/duration)
    OUT.mkdir(parents=True,exist_ok=True)
    with wave.open(str(OUT/(name+'.wav')),'wb') as w:
        w.setparams((1,2,RATE,0,'NONE','not compressed'))
        w.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,v))*32767)) for v in data))

render('点烟',.16,[(900,.1,'square',.03,0,.01)],[(.06,.3,4000,0)])
render('钟表',1.7,[(1250,.03,'square',.025,0,.01),(1500,.03,'square',.025,.8,.01),(1250,.03,'square',.025,1.6,.01)])
render('首饰',.42,[(f,.12,'triangle',.05,i*.08,.01) for i,f in enumerate([1500,1900,2300,1700])])
render('叹息',.75,[(190,.7,'sine',.02,0,.01)],[(.5,.07,700,0)])
render('收束低弦',5.1,[(f,4.5,'sine',.07,i*.18,1.4) for i,f in enumerate([220,277,330,440])])
template=(ROOT/'UnityClient/Assets/Resources/StageSounds/勒索信/纸响.wav.meta').read_text()
for asset in OUT.glob('*.wav'):
    meta=asset.with_suffix('.wav.meta')
    if not meta.exists():meta.write_text(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template).replace('normalize: 1','normalize: 0'))
folder=OUT.with_suffix('.meta')
if not folder.exists():folder.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\n')
print(OUT)
