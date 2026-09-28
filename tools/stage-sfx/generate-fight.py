#!/usr/bin/env python3
"""Generate two short, deterministic nonverbal fight cues for the street stage."""

import math
import random
import struct
import wave
from pathlib import Path


RATE = 24000
OUT = Path(__file__).resolve().parents[2] / "UnityClient/Assets/Resources/StageSounds/老街酒馆"


def save(name, samples):
    peak = max(abs(value) for value in samples)
    with wave.open(str(OUT / name), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(b"".join(struct.pack("<h", round(28000 * value / peak)) for value in samples))


def whoosh():
    rng = random.Random(17)
    previous = 0.0
    result = []
    for i in range(round(RATE * 0.19)):
        t = i / RATE
        phase = t / 0.19
        noise = rng.uniform(-1, 1)
        previous += (noise - previous) * (0.035 + 0.2 * phase)
        envelope = math.sin(math.pi * phase) ** 1.5
        result.append((noise - previous) * envelope)
    return result


def punch():
    rng = random.Random(29)
    phase = 0.0
    low = 0.0
    result = []
    for i in range(round(RATE * 0.27)):
        t = i / RATE
        frequency = 125 * math.exp(-t * 13) + 48
        phase += 2 * math.pi * frequency / RATE
        low += (rng.uniform(-1, 1) - low) * 0.13
        thump = math.sin(phase) * math.exp(-t * 17)
        leather = low * math.exp(-t * 34)
        crack = rng.uniform(-1, 1) * math.exp(-t * 170)
        result.append(0.7 * thump + 0.35 * leather + 0.18 * crack)
    return result


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    save("拳风.wav", whoosh())
    save("拳击.wav", punch())
