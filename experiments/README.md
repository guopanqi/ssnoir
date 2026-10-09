# experiments — 实验总览

本目录只放**与正式游戏隔离的视觉 / 表演实验**，按实验目的分为两组。
各实验内部保持独立：运行时不跨组 import，结论成熟后再迁回 Unity。

- [noir-city/](noir-city/) — 3D 城市视觉实验（Three.js，研究可迁回 Unity 的黑色电影视觉规律）
- [portrait-stage/](portrait-stage/) — 立绘舞台表演实验（2D 人物 + 舞台调度，研究对白、走位、停顿与人物画风融合）

新增实验时先判断属于哪一组，放入对应文件夹，不要直接堆在 `experiments/` 根下。
`experiments/` 根下不放实验内容，只放这两份组说明。
