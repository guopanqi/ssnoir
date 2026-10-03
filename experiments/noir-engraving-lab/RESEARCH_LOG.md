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
