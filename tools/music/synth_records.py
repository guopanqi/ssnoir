"""合成家里的五张唱片 + 两首城市默认曲（占位配乐）：纯加法合成 + 唱片底噪，
出 22050 Hz 单声道 16-bit WAV 循环。

不是正式音乐，是让「换唱片 → 城里换曲」这条机制先能听见。将来换真曲子只要同名替换
UnityClient/Assets/Resources/Music/ 下的 wav（Unity 按 全局键 音乐 的值取同名 clip）。
命名即归属：「唱片-*」是唱片机上架的歌，「城市-*」是城市默认声的曲库，不上架；
Unity 侧按前缀各自组池，加歌自动收录。「试听-*」是待选小样，哪个池都不进，选定后再改名转正。

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


# ── 唱片-4 慢雨蓝调：A 小调慢爵士，Am9–Dm9–Fmaj7–E7，72 bpm，四小节一循环 ×2 ──
def record_4(rng):
    bpm = 72; beat = 60.0 / bpm; bars = 8
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    prog = [("A2", ["A3", "C4", "E4", "B4"]), ("D3", ["D4", "F4", "A4", "B4"]),
            ("F2", ["F3", "A3", "C4", "E4"]), ("E2", ["E3", "G#3", "D4", "F4"])]
    walk = [0, 2, 7, 1]  # 根音上的半音偏移：根–二–五–半音趋近
    lead = {4: "A4", 5: "C5", 6: "B4", 7: "E5"}  # 后半程闷音小号一句
    for bar in range(bars):
        root, chord = prog[bar % 4]
        t0 = bar * 4 * beat
        for i, off in enumerate(walk):
            place(buf, t0 + i * beat,
                  tone(note_hz(root) * 2 ** (off / 12) / 2, beat * 1.0, "bass", 0.5))
        # 钢琴只弹 2、4 拍，还拖后一点
        for i, v in ((1, 0.40), (3, 0.50)):
            for c in chord:
                place(buf, t0 + i * beat + 0.06,
                      tone(note_hz(c), 1.4, "piano", v * rng.uniform(0.85, 1.0)))
        # 刷子在 2、4 拍，镲每拍一点
        for i in range(4):
            s = int((t0 + i * beat) * SR)
            k = int((0.30 if i in (1, 3) else 0.05) * SR)
            if s + k < n:
                buf[s:s + k] += rng.normal(0, 0.03, k) * np.exp(-np.arange(k) / (k / 4))
        if bar in lead:
            place(buf, t0 + 2 * beat, tone(note_hz(lead[bar]), 1.8, "trumpet", 0.35, vib=0.02))
    finish("唱片-4", loop_edge(buf), rng, 3200)


# ── 城市-摇摆 河滨摇摆：F 调十二小节布鲁斯，100 bpm，行走贝斯 + 钢琴切分 ──
# 城市默认声，不上唱片架。
def city_swing(rng):
    bpm = 100; beat = 60.0 / bpm; bars = 12
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    roots = [29, 29, 29, 29, 34, 34, 29, 29, 31, 30, 29, 29]  # F F F F Bb Bb F F G Gb F F
    comp = {29: ["F3", "A3", "C4", "Eb4"], 34: ["Bb3", "D4", "F4", "Ab4"],
            31: ["G3", "B3", "D4", "F4"], 30: ["Gb3", "Bb3", "Db4", "E4"]}
    walk_iv = [0, 4, 5, 7]  # 根–三–四–五
    for bar in range(bars):
        r = roots[bar]
        t0 = bar * 4 * beat
        for i, iv in enumerate(walk_iv):
            place(buf, t0 + i * beat,
                  tone(440.0 * 2 ** ((r + iv - 69) / 12) / 2, beat * 1.0, "bass", 0.55))
        for i, v in ((1, 0.42), (3, 0.50)):
            for c in comp[r]:
                place(buf, t0 + i * beat + 0.03,
                      tone(note_hz(c), 0.9, "piano", v * rng.uniform(0.85, 1.0)))
        for i in range(4):
            s = int((t0 + i * beat) * SR)
            k = int((0.30 if i in (1, 3) else 0.05) * SR)
            if s + k < n:
                buf[s:s + k] += rng.normal(0, 0.03, k) * np.exp(-np.arange(k) / (k / 4))
    finish("城市-摇摆", loop_edge(buf), rng, 3000)


# ── 城市-圆舞 午夜圆舞：A 小调三拍子，100 bpm，贝斯–和弦–和弦 + 弦乐独句 ──
# 城市默认声，不上唱片架。
def city_waltz(rng):
    beat = 0.6; bars = 12
    n = int(bars * 3 * beat * SR)
    buf = np.zeros(n)
    prog = [("A1", ["A3", "C4", "E4"]), ("A1", ["A3", "C4", "E4"]),
            ("D2", ["D4", "F4", "A4"]), ("A1", ["A3", "C4", "E4"]),
            ("E1", ["E3", "G#3", "B3"]), ("E1", ["E3", "G#3", "B3"]),
            ("A1", ["A3", "C4", "E4"]), ("A1", ["A3", "C4", "E4"]),
            ("D2", ["D4", "F4", "A4"]), ("D2", ["D4", "F4", "A4"]),
            ("E1", ["E3", "G#3", "B3"]), ("A1", ["A3", "C4", "E4"])]
    lead = {0: "E5", 4: "G#4", 8: "A4"}  # 三句弦乐，每句占四小节
    for bar in range(bars):
        root, chord = prog[bar]
        t0 = bar * 3 * beat
        place(buf, t0, tone(note_hz(root), beat * 1.1, "bass", 0.6))
        for i in (1, 2):
            for c in chord:
                place(buf, t0 + i * beat, tone(note_hz(c), 0.5, "piano", 0.20))
        if bar in lead:
            place(buf, t0, tone(note_hz(lead[bar]), beat * 11.0, "strings", 0.35, vib=0.015))
    finish("城市-圆舞", loop_edge(buf), rng, 3400)


# ── 唱片-5 影子脚步：低音八分音 ostinato + 半音摩擦，84 bpm，八小节 ────────────
def record_5(rng):
    bpm = 84; beat = 60.0 / bpm; bars = 8
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    ost = ["A1", "A1", "C2", "B1", "A1", "A1", "D2", "B1"]  # 每小节八个八分音
    pings = {0: "G#4", 2: "A4", 4: "G#4", 6: "E4"}  # 不协和钢琴点
    for bar in range(bars):
        t0 = bar * 4 * beat
        for j, b in enumerate(ost):
            place(buf, t0 + j * beat / 2, tone(note_hz(b), beat * 0.55, "bass", 0.5))
        for i in range(4):  # 秒针感的滴答
            s = int((t0 + i * beat) * SR)
            k = int(0.03 * SR)
            if s + k < n:
                f = 1200.0 if i == 0 else 3200.0
                t = np.arange(k) / SR
                buf[s:s + k] += np.sin(2 * math.pi * f * t) * np.exp(-t * 85.0) * 0.10
        if bar in pings:
            m = pings[bar]
            place(buf, t0 + 2 * beat, tone(note_hz(m), 2.2, "piano", 0.30))
            place(buf, t0 + 2 * beat + 0.02,
                  tone(note_hz(m) * 2 ** (1 / 12), 2.2, "piano", 0.22))
        if bar in (3, 7):  # 小节尾低频下潜
            place(buf, t0 + 4 * beat - beat, tone(note_hz("A0"), 1.6, "bass", 0.6))
    finish("唱片-5", loop_edge(buf), rng, 2400)


# ── 试听-C 尾随：E 小调断奏贝斯 + 滴答，92 bpm，八小节 ─────────────────────────
def demo_c(rng):
    bpm = 92; beat = 60.0 / bpm; bars = 8
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    line = ["E2", "E2", "G2", "E2", "A2", "A2", "G2", "B2"]  # 每小节一个根音走
    for bar in range(bars):
        r = note_hz(line[bar])
        t0 = bar * 4 * beat
        # 跟脚步：短–空–短短–长
        for i, d in ((0, 0.22), (2, 0.18), (2.5, 0.18), (3, 0.5)):
            place(buf, t0 + i * beat, tone(r, beat * d * 2, "bass", 0.55))
        # 每拍一记轻滴答
        for i in range(4):
            s = int((t0 + i * beat) * SR)
            k = int(0.03 * SR)
            if s + k < n:
                t = np.arange(k) / SR
                buf[s:s + k] += np.sin(2 * math.pi * 3200.0 * t) * np.exp(-t * 85.0) * 0.08
        # 偶数小节尾一记闷钢琴
        if bar % 2 == 1:
            for m in ("E3", "G3", "Bb3"):
                place(buf, t0 + 3.5 * beat, tone(note_hz(m), 0.4, "piano", 0.25))
    finish("试听-C", loop_edge(buf), rng, 2400)


# ── 试听-D 安魂：D 小调赞美诗式长和弦，四块和弦一循环 ──────────────────────────
def demo_d(rng):
    n = int(28 * SR)
    buf = np.zeros(n)
    prog = [(0.0, "D2", ["D3", "F3", "A3", "D4"]),
            (7.0, "Bb1", ["Bb2", "D3", "F3", "Bb3"]),
            (14.0, "G1", ["G2", "Bb2", "D3", "G3"]),
            (21.0, "A1", ["A2", "C#3", "E3", "A3"])]
    for st, root, chord in prog:
        place(buf, st, tone(note_hz(root), 7.5, "strings", 0.35))
        for c in chord:
            place(buf, st, tone(note_hz(c), 7.5, "strings", 0.22))
        # 每块和弦一记低钟
        place(buf, st, tone(note_hz(root) * 2, 3.0, "piano", 0.30))
    finish("试听-D", loop_edge(buf, 800), rng, 2800)


# ── 试听-E 夜莺吟：G 小调俱乐部慢歌，60 bpm，闷音小号独句 ──────────────────────
def demo_e(rng):
    bpm = 60; beat = 60.0 / bpm; bars = 8
    n = int(bars * 4 * beat * SR)
    buf = np.zeros(n)
    prog = [("G2", ["G3", "Bb3", "D4"]), ("C3", ["C4", "Eb4", "G4"]),
            ("G2", ["G3", "Bb3", "D4"]), ("D3", ["D4", "F#4", "A4"]),
            ("G2", ["G3", "Bb3", "D4"]), ("C3", ["C4", "Eb4", "G4"]),
            ("D3", ["D4", "F#4", "A4"]), ("G2", ["G3", "Bb3", "D4"])]
    lead = {0: ("Bb4", 3.0), 2: ("A4", 3.0), 4: ("G4", 2.0), 5: ("D5", 2.0), 6: ("C5", 3.0)}
    for bar in range(bars):
        root, chord = prog[bar]
        t0 = bar * 4 * beat
        place(buf, t0, tone(note_hz(root) / 2, 4 * beat, "bass", 0.45))
        for c in chord:
            place(buf, t0, tone(note_hz(c), 3.5 * beat, "piano", 0.25))
        # 极轻的刷子，每拍一点
        for i in range(4):
            s = int((t0 + i * beat) * SR)
            k = int(0.05 * SR)
            if s + k < n:
                buf[s:s + k] += rng.normal(0, 0.02, k) * np.exp(-np.arange(k) / (k / 4))
        if bar in lead:
            m, d = lead[bar]
            place(buf, t0 + beat, tone(note_hz(m), d * beat, "trumpet", 0.42, vib=0.03))
    finish("试听-E", loop_edge(buf, 600), rng, 2600)


# ── 试听-F 市集：G 大调法式三拍子，120 bpm，十六小节 ───────────────────────────
def demo_f(rng):
    beat = 0.5; bars = 16
    n = int(bars * 3 * beat * SR)
    buf = np.zeros(n)
    prog = [("G1", ["G3", "B3", "D4"]), ("G1", ["G3", "B3", "D4"]),
            ("C2", ["C4", "E4", "G4"]), ("C2", ["C4", "E4", "G4"]),
            ("G1", ["G3", "B3", "D4"]), ("D2", ["D4", "F#4", "A4"]),
            ("G1", ["G3", "B3", "D4"]), ("C2", ["C4", "E4", "G4"]),
            ("G1", ["G3", "B3", "D4"]), ("E2", ["E4", "G#4", "B4"]),
            ("A2", ["A4", "C#5", "E5"]), ("D2", ["D4", "F#4", "A4"]),
            ("G1", ["G3", "B3", "D4"]), ("C2", ["C4", "E4", "G4"]),
            ("D2", ["D4", "F#4", "A4"]), ("G1", ["G3", "B3", "D4"])]
    melody = ["D5", "B4", "G4", "A4", "B4", "C5", "D5", "E5",
              "D5", "B4", "G4", "A4", "B4", "A4", "F#4", "G4"]
    for bar in range(bars):
        root, chord = prog[bar]
        t0 = bar * 3 * beat
        place(buf, t0, tone(note_hz(root), beat, "bass", 0.55))
        for i in (1, 2):
            for c in chord:
                place(buf, t0 + i * beat, tone(note_hz(c), beat * 0.5, "piano", 0.20))
        # 手风琴感：弦乐自带轻微失谐，顶一句旋律
        place(buf, t0, tone(note_hz(melody[bar]), beat * 2.8, "strings", 0.40, vib=0.02))
        if bar % 2 == 1:
            place(buf, t0 + 2 * beat,
                  tone(note_hz(melody[(bar + 1) % 16]), beat, "strings", 0.28))
    finish("试听-F", loop_edge(buf), rng, 3600)


def loop_edge(buf, ms=120):
    """首尾各压一段线性淡入淡出，Unity loop=true 接缝不咔哒。只给新唱片用，不碰 finish。"""
    k = min(len(buf) // 4, int(ms / 1000 * SR))
    if k <= 0:
        return buf
    ramp = np.linspace(0, 1, k)
    buf[:k] *= ramp
    buf[-k:] *= ramp[::-1]
    return buf


if __name__ == "__main__":
    rng = np.random.default_rng(7)
    record_1(rng); record_2(rng); record_3(rng)
    record_4(rng); record_5(rng); city_swing(rng); city_waltz(rng)
    demo_c(rng)
    demo_d(rng); demo_e(rng); demo_f(rng)
