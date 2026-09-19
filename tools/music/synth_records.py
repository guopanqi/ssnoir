"""合成家里唱片机的三张唱片（占位配乐）：纯加法合成 + 唱片底噪，出 22050 Hz 单声道 16-bit WAV 循环。

不是正式音乐，是让「换唱片 → 城里换曲」这条机制先能听见。将来换真曲子只要同名替换
UnityClient/Assets/Resources/Music/唱片-1.wav … 唱片-3.wav（Unity 按 全局键 音乐 的值取同名 clip）。

跑法（用 Blender 自带的 Python，因为它带 numpy）：
    blender -b --python tools/music/synth_records.py
"""
import math
import os
import wave
import numpy as np

SR = 22050
ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "UnityClient", "Assets", "Resources", "Music")


def note_hz(name):
    """'A3' → 220.0。支持 # 与 b。"""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    n = names[name[0]]
    rest = name[1:]
    if rest.startswith("#"): n += 1; rest = rest[1:]
    elif rest.startswith("b"): n -= 1; rest = rest[1:]
    octave = int(rest)
    return 440.0 * 2 ** ((n + (octave - 4) * 12) / 12)


def env(n, a, d, s, r, sustain_len):
    """ADSR，长度 n 样本。"""
    a, d, r = int(a * SR), int(d * SR), int(r * SR)
    out = np.zeros(n)
    i = 0
    out[i:i + a] = np.linspace(0, 1, a)[:max(0, min(a, n - i))]; i += a
    seg = min(d, n - i)
    if seg > 0: out[i:i + seg] = np.linspace(1, s, d)[:seg]
    i += d
    seg = min(sustain_len, n - i)
    if seg > 0: out[i:i + seg] = s
    i += seg
    seg = min(r, n - i)
    if seg > 0: out[i:i + seg] = np.linspace(s, 0, r)[:seg]
    return out


def tone(hz, dur, kind, vel=1.0, vib=0.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    ph = 2 * math.pi * hz * t
    if vib:
        ph += vib * np.sin(2 * math.pi * 5.5 * t) * (t > 0.15)
    if kind == "piano":
        w = (np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.25 * np.sin(3 * ph) + 0.12 * np.sin(4 * ph))
        w *= np.exp(-t * 1.8)
        e = env(n, 0.004, 0.1, 0.8, 0.2, max(0, n - int(0.3 * SR)))
    elif kind == "trumpet":
        w = sum(np.sin(k * ph) / k for k in range(1, 9))
        w *= 0.35
        e = env(n, 0.06, 0.15, 0.75, 0.15, max(0, n - int(0.36 * SR)))
    elif kind == "bass":
        w = np.sin(ph) + 0.3 * np.sin(2 * ph)
        w *= np.exp(-t * 2.5)
        e = env(n, 0.005, 0.1, 0.6, 0.1, max(0, n - int(0.2 * SR)))
    elif kind == "strings":
        w = sum(np.sin(k * ph + 0.3 * k) / (k * 1.6) for k in range(1, 7))
        w += 0.2 * np.sin(ph * 1.003)      # 轻微失谐，弦群感
        e = env(n, 0.25, 0.2, 0.85, 0.35, max(0, n - int(0.8 * SR)))
    else:
        raise ValueError(kind)
    return w * e * vel


def place(buf, start, sig):
    s = int(start * SR)
    e = min(len(buf), s + len(sig))
    if e > s:
        buf[s:e] += sig[:e - s]


def lowpass(x, cutoff):
    """一阶 IIR 低通：把加法合成的锐边磨掉，像老唱片。"""
    rc = 1.0 / (2 * math.pi * cutoff)
    a = (1.0 / SR) / (rc + 1.0 / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def vinyl(n, rng):
    """底噪 + 偶发的爆点，再加一点 33 转的呼吸感。"""
    hiss = rng.normal(0, 0.006, n)
    pops = np.zeros(n)
    for _ in range(int(n / SR * 1.4)):
        i = rng.integers(0, n - 40)
        pops[i:i + 40] += rng.uniform(0.05, 0.18) * np.exp(-np.arange(40) / 6.0) * rng.choice([-1, 1])
    t = np.arange(n) / SR
    rumble = 0.004 * np.sin(2 * math.pi * (100 / 60.0) * t)
    return hiss + pops + rumble


def finish(name, buf, rng, cutoff):
    buf = lowpass(buf, cutoff)
    buf += vinyl(len(buf), rng)
    # 呼吸感：转速微微不稳 → 极慢的幅度起伏
    t = np.arange(len(buf)) / SR
    buf *= 1.0 + 0.05 * np.sin(2 * math.pi * 0.55 * t)
    peak = np.max(np.abs(buf))
    buf = buf / peak * 0.8
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    data = (buf * 32767).astype("<i2").tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data)
    print("MUSIC| %s  %.1fs" % (path, len(buf) / SR))


# ── 唱片-1 午夜列车：慢板钢琴，Dm–Bb–Gm–A，60 bpm，四小节一循环 ×2 ──────────────
def record_1(rng):
    bpm = 60; beat = 60.0 / bpm; bars = 8
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    prog = [("D3", ["D3", "F3", "A3"]), ("Bb2", ["Bb2", "D3", "F3"]),
            ("G2", ["G2", "Bb2", "D3"]), ("A2", ["A2", "C#3", "E3"])]
    melody = [["A4", "F4", "E4", "D4"], ["F4", "D4", "C4", "D4"],
              ["G4", "Bb4", "A4", "G4"], ["E4", "C#4", "E4", "A4"]]
    for bar in range(bars):
        root, chord = prog[bar % 4]
        t0 = bar * 4 * beat
        place(buf, t0, tone(note_hz(root) / 2, 4 * beat, "bass", 0.5))
        # 左手：分解和弦，每拍一个音
        for i in range(4):
            place(buf, t0 + i * beat, tone(note_hz(chord[i % 3]), beat * 1.6, "piano", 0.35))
        # 右手：偶数循环省一半音，像在想事
        for i, m in enumerate(melody[bar % 4]):
            if bar >= 4 and i % 2 == 1: continue
            place(buf, t0 + i * beat + 0.02, tone(note_hz(m), beat * 1.3, "piano", 0.55))
    finish("唱片-1", buf, rng, 3200)


# ── 唱片-2 码头灯火：闷音小号，G 小调十二小节，72 bpm，走路贝斯 ──────────────
def record_2(rng):
    bpm = 72; beat = 60.0 / bpm; bars = 12
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    blues = ["G", "G", "G", "G", "C", "C", "G", "G", "D", "C", "G", "D"]
    walk = {"G": ["G2", "Bb2", "D3", "F3"], "C": ["C3", "Eb3", "G3", "Bb3"], "D": ["D3", "F#3", "A3", "C4"]}
    horn = {"G": ["Bb4", "D5", "F5", "D5"], "C": ["Eb5", "G4", "Bb4", "C5"], "D": ["F#4", "A4", "C5", "A4"]}
    for bar in range(bars):
        ch = blues[bar]
        t0 = bar * 4 * beat
        for i in range(4):
            place(buf, t0 + i * beat, tone(note_hz(walk[ch][i]), beat * 0.95, "bass", 0.55))
        # 小号一句只在小节的前半，后半留白
        if bar % 2 == 0:
            for i, m in enumerate(horn[ch][:3]):
                place(buf, t0 + i * beat * 0.66 + 0.05,
                      tone(note_hz(m), beat * (1.6 if i == 2 else 0.7), "trumpet", 0.5, vib=0.02))
        else:
            place(buf, t0 + 0.1, tone(note_hz(horn[ch][3]), beat * 2.2, "trumpet", 0.42, vib=0.03))
        # 刷子：每拍一点点噪声
        for i in range(4):
            s = int((t0 + i * beat) * SR)
            k = int(0.05 * SR)
            if s + k < n:
                buf[s:s + k] += rng.normal(0, 0.03, k) * np.exp(-np.arange(k) / (k / 4))
    finish("唱片-2", buf, rng, 2600)


# ── 唱片-3 周六舞厅：弦乐三拍子，F 大调，126 bpm，十六小节 ──────────────────────
def record_3(rng):
    bpm = 126; beat = 60.0 / bpm; bars = 16
    n = int(bars * 3 * beat * SR)
    buf = np.zeros(n)
    prog = [("F2", ["F3", "A3", "C4"]), ("F2", ["F3", "A3", "C4"]),
            ("C2", ["E3", "G3", "C4"]), ("C2", ["E3", "G3", "Bb3"]),
            ("F2", ["F3", "A3", "C4"]), ("D2", ["F3", "A3", "D4"]),
            ("G2", ["G3", "Bb3", "D4"]), ("C2", ["E3", "G3", "C4"])]
    melody = ["A4", "C5", "A4", "F4", "G4", "A4", "G4", "E4", "C4", "D4", "E4", "F4",
              "A4", "G4", "F4", "C5"]
    for bar in range(bars):
        root, chord = prog[bar % 8]
        t0 = bar * 3 * beat
        # 咚-嚓-嚓
        place(buf, t0, tone(note_hz(root), beat * 0.9, "bass", 0.6))
        for i in (1, 2):
            for c in chord:
                place(buf, t0 + i * beat, tone(note_hz(c), beat * 0.55, "piano", 0.18))
        # 弦乐旋律：每小节一个长音，隔小节带一个经过音
        m = melody[bar]
        place(buf, t0, tone(note_hz(m), beat * 2.9, "strings", 0.5, vib=0.015))
        if bar % 2 == 1:
            place(buf, t0 + 2 * beat, tone(note_hz(melody[(bar + 1) % 16]), beat * 1.0, "strings", 0.3))
    finish("唱片-3", buf, rng, 3600)


if __name__ == "__main__":
    rng = np.random.default_rng(7)
    record_1(rng); record_2(rng); record_3(rng)
