#!/usr/bin/env python3
"""离线复现 portrait-theatre 第三、四场的 WebAudio 音色（固定噪声种子）。"""
import math, random, struct, wave
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'UnityClient/Assets/Resources/StageSounds/勒索信'
RATE=22050

def render(name, seconds, tones=(), bursts=()):
    data=[0.0]*int(seconds*RATE)
    for freq,duration,kind,volume,onset,attack in tones:
        for i in range(int(duration*RATE)):
            t=i/RATE; phase=(t*freq)%1
            v=math.sin(2*math.pi*phase) if kind=='sine' else (1-4*abs(phase-.5) if kind=='triangle' else 2*phase-1)
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

render('投信',.52,[(900,.2,'triangle',.05,0,.01),(150,.3,'sine',.2,.16,.01)],[(.12,.5,500,.16)])
render('起身',.55,[(220,.5,'saw',.06,0,.01),(233,.5,'saw',.05,0,.01)])
render('脚步',.14,bursts=[(.05,.2,700,0)])
render('擦身',.3,bursts=[(.12,.5,400,0)])
render('纸响',.24,bursts=[(.14,.25,2000,0)])
print(OUT)
