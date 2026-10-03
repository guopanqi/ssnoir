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

## Iteration 01 — Shape baseline review

状态：等待自动 benchmark 截图。第一轮只根据实际 `shape` / `final` 固定镜头判断，不预设改法。


### 2026-10-03 — Iteration 01A / Baseline shape review

**问题**

当前小街区的构图和体块本身是否已经能承担黑色电影画面，而不依赖描线和最终 shader？

**固定条件**

保持材质、灯光参数、Atmosphere、Print 和描线算法不变。使用 1600×900、固定 seed 的四个 benchmark shots。

**基线观察**

- Shot 01：镜头被近处街屋大面积遮挡，剧院几乎不可见；这是空间布局/机位问题，不是“黑面积有力量”。
- Shot 02：四个镜头中体块关系最清楚，仓库具有可识别轮廓，但仓库仍侵入道路。
- Shot 03：机位正对近距离 emissive / street light；完整效果严重过曝，无法用于判断后巷。
- Shot 04：城市可读，但普通街屋、地标和背景体块的权重过平均。
- 亮度诊断：Shape 01 / 02 中约 90% 以上像素低于 luma 0.03；Final 03 约一半画面高于 luma 0.5。两端都说明当前镜头不能作为稳定 benchmark。

**Iteration 01A 变量**

只修改空间与构图：

- 剧院进入北侧街墙，并取代一块 generic building；
- 仓库移到南侧街墙并取代两块 generic building；
- 后巷成为南侧街墙的真实缺口；
- 路灯从均匀 7 盏缩成稀疏 4 盏；
- 重设四个 benchmark camera。

不修改 Line / Light 参数 / Atmosphere / Print。

**预期**

Shot 01 应第一眼读到剧院；Shot 03 应能从街道看进一个被两侧建筑夹住的黑暗缺口；Shot 04 应出现明确的 landmark hierarchy。


### 2026-10-03 — Iteration 01B / Landmark silhouette

**01A 实际结果**

- Shot 01：相比基线，剧院竖牌与主街纵深已可读，前景不再完全堵死；但剧院本体仍像普通矩形体块。
- Shot 02：仓库成为明确的工业地标，街墙关系明显优于基线。
- Shot 03：后巷现在是一个真实、清楚的黑暗缺口，Shape 层目标基本成立。
- Shot 04：整体街区层级改善，但剧院高体量仍与 generic block 过于相似。
- Shape 黑像素比例从旧基线约 90%+ 改善为：Shot01 83%、Shot02 74%、Shot03 68%、Shot04 52%。构图信息已经明显增加，不需要靠 Print 才能读空间。

**01B 变量**

仍只修改 Shape / Camera：

- 给剧院 stage house 增加三层递减的 Art-Deco 式 crown 与顶端 finial；
- 增加低矮 entrance mass，让 marquee 在几何上有支撑；
- Shot 01 更靠近剧院，减少“远处一个红牌”的感觉；
- Shot 04 稍降低并重新瞄准剧院，使 skyline identity 成为测试重点。

不修改灯光强度、Bloom、雾、Dither、描线算法。
