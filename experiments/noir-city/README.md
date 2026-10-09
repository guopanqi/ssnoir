# noir-city — 3D 城市视觉实验组

共同目的：在低模 / 无纹理 / 程序化 / AI 资产都可进入的条件下，
研究能稳定迁回 Unity 的黑色电影城市视觉（形、线、光、空气、印刷）。

各实验互相独立，运行时不 import 对方代码，只复用思想。
CI 截图工作流（`.github/workflows/noir-*.yml`）的工作目录已指向本组下各实验。

| 实验 | 一句话 |
| --- | --- |
| [noir-engraving-lab/](noir-engraving-lab/) | 长期视觉研究工程：Noir Engraving（Shape → Line → Light → Atmosphere → Print → 世界生产），固定四镜头基线 |
| [noir-graphic-city-lab/](noir-graphic-city-lab/) | 重构版单幅城市 tableaux：矢量建筑线、蓝黑体块、剪影人物、极简图形光 |
| [noir-theatre-lab/](noir-theatre-lab/) | Noir Theatre Engraving：用现有 City FBX 搭舞台式框景与黑场，四镜头四层诊断 |
| [noir-world-kernel/](noir-world-kernel/) | Genesis Noir 式可漫游局部 3D 空间：FacadeGrammar + VectorStroke + SVG 人物/道具 + 源驱动反射 |
