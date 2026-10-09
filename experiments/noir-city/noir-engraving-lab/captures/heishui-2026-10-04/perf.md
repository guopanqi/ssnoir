# 黑水 · 帧时实测

时间 2026-10-04T09:59:51.174Z
GPU  ANGLE (Apple, ANGLE Metal Renderer: Apple M4, Unspecified Version)
GL   WebGL 2.0 (OpenGL ES 3.0 Chromium)
画布 1600×900（体积雾 0.5×、拉丝与 halation 0.125×）

| 阶段 | 机位 | min ms | 中位 ms | max ms | 中位 fps |
|---|---|---:|---:|---:|---:|
| final | 01-world | 14.1 | 24 | 46.6 | 41.7 |
| final | 02-across | 16.1 | 19.9 | 27.7 | 50.3 |
| final | 03-river | 17.3 | 18.9 | 21.8 | 52.9 |
| final | 04-port | 17.2 | 18.2 | 23.6 | 54.9 |
| final | 05-rain | 17.4 | 22 | 27.3 | 45.5 |
| final | 06-silhouette | 11.9 | 13.8 | 16.4 | 72.5 |
| final | 07-plan | 9.8 | 11.3 | 13 | 88.5 |

> 每格同步连渲 24 帧，每帧后 1×1 `readPixels` 强制 GPU 落地并逐帧记时。
> 包含镜像反射（0.5×）、主场景、体积雾（0.5×, 26 步）、描线、光晕（0.125×）
> 与成片的**全部**开销。
>
> **无头 Chrome 的数值方差很大**（同一配置的 max 常常是 min 的 3–5 倍），
> 因为无头合成路径要每帧回读画布。这里给 min 与中位数，**不要**把某个单值当真；
> 有真实窗口时应该以窗口里 HUD 的 rAF 读数为准。
