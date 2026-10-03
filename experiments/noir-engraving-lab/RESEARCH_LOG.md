# Noir Engraving Lab — Research Log

这里记录已经验证过的视觉结论。不要把猜想写成结论。

## 记录模板

### YYYY-MM-DD — 实验名称

**问题**

一句话说明这轮只想回答什么。

**固定条件**

列出没有改变的关键条件，例如镜头、几何、灯光、后处理。

**变量**

只列这一轮改变的变量。

**观察**

分别记录 01–04 四个 benchmark shots 的结果。

**结论**

- 保留：
- 放弃：
- 尚不确定：

**原因**

说明视觉上为什么，不写“感觉更好”就结束。

**迁回 Unity**

明确对应到 CityBox / Lighting / Atmosphere / SSNoirStylize 的哪一层。

---

## Baseline 0.1

当前版本建立第一套可运行基线：

- 无纹理程序化街区；
- 选择性 EdgesGeometry 描线；
- 冷色主光 / 补光；
- 雨、指数雾、街灯光锥；
- Bloom；
- luminance → black/white normalization → finite levels → Bayer dither → cold ramp；
- 四个固定镜头。

这仍是“基线”，不是经过验证的最终画风。下一阶段先研究 Shape，不继续叠加新效果。


## 2026-10-03 — Hero / Context / Silence

**问题**

普通街屋的轮廓线是否真的帮助空间阅读，还是把背景重新拉回前景。

**固定条件**

四个 benchmark 镜头、几何、主光、空气和 Print 均保持不变。

**变量**

普通街屋主体从 Context 改为 Silence；剧院、仓库和后巷连桥继续保留 Hero，少数结构细节保留 Context。重新加入轻量 Line 诊断。

**观察**

- 01 剧院：Hero 线仍明确强调剧院，普通街屋消失的结构线没有破坏街道纵深。
- 02 仓库：仓库屋顶与主体轮廓仍可读，远处街屋更安静。
- 03 后巷：连桥仍是唯一明显结构线之一，前景黑块没有被重新勾亮。
- 04 城市压缩：街屋不再形成连续白边，建筑层级更依赖明暗面和 silhouette。
- Shape → Line 的平均亮度变化在四镜头中约为 +0.001 到 +0.002，说明线层开始从“全局滤镜”退回为稀疏的信息层。

**结论**

- 保留：Hero / Context / Silence 三级语义，其中 Silence 是正式的一等状态。
- 放弃：普通背景建筑默认描边。
- 尚不确定：Context 的 0.11 opacity 是否还可继续降低，暂不调整。

**原因**

线仍能帮助关键物件被识别，但背景不再因为几何边缘本身而获得视觉优先级。这样更符合“显示什么与不显示什么同样重要”的 Noir Engraving 原则。

**迁回 Unity**

CityBox 资产应允许显式的 line importance / outline mask，而不是统一屏幕空间 Outline。Hero、Context、Silence 应由叙事和构图层级决定。
