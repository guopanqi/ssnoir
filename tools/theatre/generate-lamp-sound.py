#!/usr/bin/env python3
"""Generate the deterministic crackle WAV used by the lamp theatre sample."""
import argparse
import math
import random
import struct
import wave
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2] / "UnityClient/Assets/Resources/Theatre/路灯电流.wav")
    args = parser.parse_args()
    noise = random.Random(42)
    rate = 22050
    frames = bytearray()
    for i in range(int(rate * 0.66)):
        time = i / rate
        value = 0.0
        for onset in (0.04, 0.13, 0.24, 0.35, 0.46, 0.57):
            delta = time - onset
            if 0 <= delta < 0.045:
                value += (0.15 * math.sin(2 * math.pi * 110 * delta) + 0.17 * (noise.random() * 2 - 1)) * math.exp(-delta * 80)
        frames.extend(struct.pack("<h", int(max(-1, min(1, value)) * 32767)))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(args.output), "wb") as sound:
        sound.setnchannels(1)
        sound.setsampwidth(2)
        sound.setframerate(rate)
        sound.writeframes(frames)
    print(args.output)


if __name__ == "__main__":
    main()
