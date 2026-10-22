#!/usr/bin/env python3
"""移植酒馆最后一夜 HTML 的三段 swing 旋律，离线合成30秒舞台音频。
没有歌词或真人配音；固定随机种子，结尾自然淡出，与 Scheme 的30秒演唱拍同步。
"""
import math
import random
import struct
import wave
from pathlib import Path

SR = 22050
DURATION = 30
PHRASES = [
    [(392,1),(440,1),(493.9,1.5),(440,.5),(392,1),(329.6,2),(392,1),(440,1),(523.3,2),(493.9,1),(440,1),(392,3)],
    [(523.3,1),(587.3,1),(659.3,2),(587.3,1),(523.3,1),(493.9,2),(440,1),(523.3,1),(587.3,1.5),(659.3,.5),(784,3)],
    [(659.3,1),(698.5,1),(784,2.5),(698.5,.5),(659.3,1.5),(587.3,.5),(523.3,3.5)],
]
BASS = [55,65.4,73.4,82.4,87.3,82.4,73.4,65.4]
CHORDS = [[220,261.6,329.6,392],[174.6,220,261.6,349.2],[196,246.9,293.7,392],[164.8,207.7,246.9,329.6]]

def generate():
    data = [0.] * (SR * DURATION)
    def tone(start, hz, duration, amplitude, vocal=False):
        begin = round(start * SR)
        for i in range(min(round(duration * SR), len(data)-begin)):
            t = i / SR
            envelope = min(1, t / (.08 if vocal else .012), max(0, (duration-t)/(.24 if vocal else .12)))
            phase = 2 * math.pi * hz * t + (.015 * hz / 5.6 * math.sin(2*math.pi*5.6*t) if vocal else 0)
            value = math.sin(phase) + (.22 if vocal else .12) * math.sin(phase*2)
            data[begin+i] += amplitude * value * envelope
    beat = 60/118
    for i in range(math.ceil(DURATION/beat)):
        start = i*beat
        tone(start, BASS[i%8], .4, .075)
        if i%8 == 0:
            for hz in CHORDS[(i//8)%4]: tone(start,hz,1.8,.018)
    cursor = 1.
    for phrase in PHRASES:
        for hz, beats in phrase:
            duration = beats*.5
            tone(cursor,hz,duration*.94,.17,True)
            cursor += duration
        cursor += .65
    rng = random.Random(7)
    for i in range(len(data)):
        t=i/SR
        # 轻柔刷镲，不用持续白噪覆盖旋律。
        local=t%beat
        data[i] += rng.uniform(-1,1)*.016*math.exp(-local*70)
        data[i] *= min(1,t/.15,max(0,(DURATION-t)/3))
    peak=max(abs(v) for v in data)
    output=Path(__file__).resolve().parents[2]/'UnityClient/Assets/Resources/StageSounds/酒馆告别/最后一首.wav'
    output.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(output),'wb') as audio:
        audio.setnchannels(1);audio.setsampwidth(2);audio.setframerate(SR)
        audio.writeframes(b''.join(struct.pack('<h',round(v/peak*.78*32767)) for v in data))
    print(f'{output}: {DURATION}s; melody ends {cursor:.2f}s')

if __name__ == '__main__': generate()
