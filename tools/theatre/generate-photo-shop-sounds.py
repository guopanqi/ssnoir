#!/usr/bin/env python3
"""Render Claude chapters 22/23's WebAudio cues into reproducible mono WAVs."""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'UnityClient/Assets/Resources/StageSounds/照相馆'
RATE = 22050


def render(name, seconds, tones=(), noises=()):
    rng = random.Random(22)
    filtered = [0.0] * len(noises)
    samples = bytearray()
    for i in range(int(seconds * RATE)):
        t = i / RATE
        value = 0.0
        for start, frequency, duration, kind, gain in tones:
            d = t - start
            if not 0 <= d < duration:
                continue
            phase = frequency * d
            oscillator = (math.sin(2 * math.pi * phase) if kind == 'sine' else
                          1 - 4 * abs((phase % 1) - .5) if kind == 'triangle' else
                          1 if kind == 'square' and phase % 1 < .5 else
                          -1 if kind == 'square' else 2 * (phase % 1) - 1)
            envelope = d / .01 if d < .01 else math.exp(math.log(.0001 / gain) * (d - .01) / max(.01, duration - .01))
            value += oscillator * envelope * gain
        for j, (start, duration, cutoff, gain) in enumerate(noises):
            d = t - start
            if 0 <= d < duration:
                alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
                filtered[j] += alpha * (rng.uniform(-1, 1) - filtered[j])
                value += filtered[j] * gain * math.exp(math.log(.0001 / gain) * d / duration)
        samples.extend(struct.pack('<h', round(max(-1, min(1, value)) * 32767)))
    with wave.open(str(OUTPUT / (name + '.wav')), 'wb') as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(RATE)
        audio.writeframes(samples)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    render('门铃', 2.3, [(0, 784, 1.3, 'sine', .16), (.45, 587, 1.8, 'sine', .16)])
    render('放胶卷', .12, [(0, 900, .05, 'triangle', .04)])
    render('写取件单', 1.5, noises=[(i * .26, .04, 3200, .15) for i in range(5)])
    render('冲印', 2.7, [(i * .38, 1500 if i % 2 else 1250, .03, 'square', .03) for i in range(7)] +
           [(i * .38, 2400, .04, 'sine', .03) for i in (2, 5)])
    render('翻相片', .2, [(0, 660, .12, 'triangle', .05)])
    render('低音', .4, [(0, 140, .3, 'sawtooth', .04)])


if __name__ == '__main__':
    main()
