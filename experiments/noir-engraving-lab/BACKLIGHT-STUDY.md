# 逆光 BACKLIGHT (by opencode) — 城市视角价值结构实验

> 独立实验，入口 `backlight.html`，源码 `src/backlight/`，独立构建 `dist-backlight/`。
> 不描线、不 raymarch、不反射 RT；判断只看五个固定机位 × 四个阶段。

| 命令 | 作用 |
|---|---|
| `npm run dev` | 起服务，访问 `/backlight.html` |
| `npm run build:backlight` | 独立构建 → `dist-backlight/` |
| `npm run capture:backlight` | 五机位 × 四阶段出图 + 联络表 + 亮度统计 |
| `npm run export:backlight` | Blender 无头重新烘城市快照（一次性，见下） |

操作：`1`–`5` 切机位 · `Q`/`E` 切阶段 · 空格暂停时间 · `H` 隐藏 HUD · `R` 回到当前机位。
截图冻结在 `?capture=1` + `setTime(8.0)`，每次 capture 完全可复现。

---

## 一句话论点

**城市是一叠黑色剪影，贴在一条发光的地平线上；只有一座自建的 Art Deco 主角塔被从下面打亮。**

本实验回答的问题：城市视角的黑色电影情绪，能不能只靠三件事成立——

1. **发光地平线的价值结构**（远楼永远比它背后那块天暗一档）；
2. **真实 shadow map 的低角度长影**（城在逆光里，不是一堆自发光贴片）；
3. **一束从下面打亮的立面**（眼睛只落在一个地方）。

刻意不做的事：屏幕描线（那是主实验与剪影夜城的路线）、体积雾 raymarch 与镜像反射 RT（那是黑水的路线）、全前向解析光（那是灯岸的路线）。本实验的后处理只有 bloom + god rays + 一次 grade。

与同目录实验的差别不是"更酷"，而是**问题不同**：本实验只验证"价值结构"这一个假设，所有参数集中在 `src/backlight/config.js`。

---

## 数据与工程边界

- 城市几何来自 `city-box/build/city/city_build.blend`（构建产物），由 `tools/export-backlight.py` 一次性烘成
  `public/backlight/city.json`（0.2 MB）+ `city.bin`（25.4 MB）。**运行时只读这份冻结快照，不修改任何正式产物。**
- 这是与黑水 / 灯岸 / 黑水快照相同的**显式例外**：本实验的研究问题就是"真实城市的城市视角"，
  程序化几何替代不了它（见 AGENTS.md 边界的例外口径）。重新烘焙才需要 CityBox，日常开发与 capture 都不需要。
- 烘焙原则：拆顶点 + 平面法线（卡通分带需要硬终止线，面与面必须断开）；城外片场布景（z < -800）剔除；
  市中心 98 m 填充方块让位给主角塔；连续形体合并成大网格，重复形体走 `InstancedMesh`；坐标 Z-up → Y-up。
  stats：地点外壳保留 563 / 剔除 294，填充楼 1274、屋顶件 877、树 119、窗 3430、主场地清空 1 处。
- 尺度依据（导出后统计）：填充楼最高 81.5 m、p99 = 25.6 m；地点外壳最高 85.6 m（货运公司）；
  城市范围 x[-660, 470]、z[-492, 649]。主角塔定 **118 m**（tier 顶 103 / 冠 110 / 尖 118），一眼是全城最高，但不是童话巴别塔。
- `src/backlight/` 十个文件 1837 行：`main.js` 只装配；参数只在 `config.js`；机位只在 `shots.js`；
  `glsl.js` 放天空与雾的共享 GLSL（天空与雾同源是这个画法的结构保证）。

---

## 四个阶段（一次只回答一个问题）

| 阶段 | 键 | 加了什么 | 检验什么 |
|---|---|---|---|
| 1 体块 shape | `Q`/`E` | 天 + 低角度冷键 + 三档分带 + 真实阴影 | 剪影的价值结构在没有一盏灯时是否成立 |
| 2 空气 air | | + 空气透视 + 屏幕空间 god rays | 远楼是否精确收敛到"它背后那块天" |
| 3 灯 light | | + 窗光 / 路面暖 / 仰射灯 / 光锥 / 钠灯珠 / 信标 / 色标 / bloom | 层级是否被灯加强而不是被灯抹平 |
| 4 成片 final | | + 印刷层（split tone、暗角、颗粒、色散、黑位） | 成片是否还在同一张纸上 |

阶段只改 uniform 与 pass 参数，不重建任何对象——这是 capture 可复现的前提。

## 五个固定机位

| # | 名称 | 站位 | 检验什么 |
|---|---|---|---|
| 01 | 隔河对望 | (176, 14, 58) → (-112, 58, -55) fov 30 | 发光地平线 + 塔的剪影是否第一眼成立 |
| 02 | 主角塔 | (152, 34, 96) → (-110, 70, -55) fov 34 | 仰射立面：壁柱、收分、竖窗带、冠部 |
| 03 | 高位斜俯 | (300, 400, 330) → (-150, 0, -70) fov 34 | 长影方向、街区明暗节奏、河的黑、雾的俯视方向 |
| 04 | 干道纵深 | (-110, 14, -360) → (-110, 60, -55) fov 38 | 雾的分档、钠灯珠疏密、远近层级、塔的正面比例 |
| 05 | 远景剪影 | (452, 44, -232) → (-200, 54, -90) fov 22 | 天际线整体：118 m 塔与 81.5 m 填充楼的比例、god rays 是否帮忙 |

## 后处理栈

`RenderPass`（HalfFloat + `samples:4` MSAA）→ **GodRaysPass（自写 `Pass` 子类）** → `UnrealBloomPass`（`needsSwap=false` 回写 readBuffer）→ Grade（自写 ShaderPass：曝光 → ACES → 黑位 → 冷影/暖高分离 → 去饱和 → 暗角 → sRGB → 颗粒）。
`renderer.toneMapping = NoToneMapping`（tonemap 只发生在 grade 里，一次），`shadowMap.autoUpdate = false`（静态城只烘一次）。

---

## Three.js / 开源实现的借鉴（实际采用了什么、拒了什么）

**采用了（按对画面的贡献排序）：**

| # | 概念 / 代码 | 出处 | 本实验的用法 |
|---|---|---|---|
| 1 | `EffectComposer` / `RenderPass` / `ShaderPass` / `Pass`+`FullScreenQuad` | three.js `examples/jsm/postprocessing` | 后处理骨架；GodRaysPass 按 `Pass` 契约自己实现 |
| 2 | 屏幕空间体积光（遮挡亮部遮罩 → 以光源屏幕坐标为中心的径向模糊 → 叠加） | GPU Gems 3 Ch.13 *Volumetric Light Scattering as a Post-Process*（Mitchell） | `post.js` 自写：中心 = 相机位置 + `uGlowDir`×1000 投影，相机背后强度归 0；权重 `uWeight = 1/((1-decay^samples)/(1-decay))` 归一化，否则过曝 |
| 3 | `UnrealBloomPass`（高通 + 多级降采样模糊） | three.js examples | 直接使用；阈值 1.00、strength 0.36，只收窗光/信标/冠部，不让大面积天光进 bloom |
| 4 | ACES 拟合 tonemap | Stephen Hill 的 fitted ACES | grade 里唯一一次 tonemap |
| 5 | 逐像素梯度分带（cel / gradient map 的三档终止线） | 通用 toon shading；three `MeshToonMaterial` 的 gradientMap 思路 | `materials.js` 的 `onBeforeCompile` patch：`dotNL` 三档 + 各档亮度 floor，背光面只留 3% 体积 |
| 6 | interleaved gradient noise | Jimenez（CoD:AW 后处理） | 成片颗粒与天空抖动 |
| 7 | `OrbitControls`、`mergeGeometries` | three.js examples | 环视检查（只检查，不作结论）；主角塔几何合并成少量网格 |

**明确没做，以及为什么：**

| 技术 | 原因 |
|---|---|
| 屏幕描线（depth/normal Sobel、inverted-hull 墨线壳） | 本实验的论点就是"价值关系自己给出轮廓"；描线是另一条实验线 |
| 体积雾 raymarch / froxel | 雾只做一件事：把远处几何推向该方向的天空色，解析式够用 |
| 镜像反射 RT | 河面只要"黑水 + 一条向热点的碎光"，`reflect()` + 解析天空就够 |
| pmndrs `GodRaysEffect` 一类成品 | 不读 3D 光源、不做遮挡，灯被挡住照样有柱；自写只为能按 `uGlowDir` 定中心 |
| 每扇窗一个点光 | 城市尺度灯数爆炸；窗是自发光面片，路是自发光路面，钠灯是 Points + bloom |

---

## 走过弯路

| 现象 | 真因 | 处理 |
|---|---|---|
| 填充楼整片纯白、城市像雪 | 实例反照率没走 `instanceColor`，材质本体是白色 | `dress()` 里 `setColorAt(片区色 × 0.86 + 抖动)`，树另配 `TREE_TINT` |
| 上半片天被热点染亮，像白天 | 天空热点只按 `dot` 衰减，不随仰角收 | `hot *= exp(-|h|×glowFall)`，热点只活在地平线附近；宽晕 `haloFall` 6 → 8.5 |
| 中景全成暖奶，剪影只剩天际线 | 雾密度 0.00205 / 0.00095 太高 | 降到 0.00045（600 m 处只褪约 24%） |
| 俯视机位整张被冲成暖褐纸 | 雾色 = `skyBase(dir)`，往下的方向也在推向地平线亮带 | 雾色改 `mix(暗霾, skyBase(dir), smoothstep(-0.14, 0.05, dir.y))`——往上看是天，往下看是地面的霾 |
| 成片阶段 03-plan 整张压成黑 | 黑位 0.020 是在 ACES 之后的显示线性域减，暗部（~0.008）直接被减成 0 | 0.020 → 0.008 |
| 河面像织出来的绸缎 | 三组正弦波有一个不随距离衰减的恒定微幅，远处反射梯度造成规律摩尔纹 | 振幅完全随 `exp(-dist×0.018)` 衰减；`waterRefl` 0.85 → 0.38，碎光带 ×2.0 补回"一条光路" |
| 平地上规则网格纹（疑似 acne） | 低角度键光 + 大平面，`normalBias` 按米给 1.2 不够 | `normalBias` 3.0、`bias` -0.0006、阴影正交半径 780 → 700 m（4096² 下约 0.34 m/texel） |
| 光锥是一堵白墙，把立面吃掉 | 加性光锥强度按"看得见"给，没按"不抢焦点"给 | `BEAM` 0.30 → 0.035、锥底半径系数 0.82 → 0.62；探照灯 0.16 → 0.06 |
| 编译报错：fragment 缺 varying / `1` 是 int / 字符串跨行 | 手写 GLSL 与 JS 模板串的低级错 | 补 `varying vec2 vUv`；`Number(x).toFixed(3)` 进 GLSL；模板串 |
| 屋顶件浮在地面高度 | CityBox 源里 heishui/lantern 把屋顶件 y 写死 1.05（那是街区台面高度） | 烘焙时导出每件的真实 `base`（实例世界 z），运行时用 `base` 而不是猜 |
| 04 机位半屏被前景楼面吃掉 | 机位站在街块里，视线被一栋板楼横切 | 重定为正南 `(-110, 14, -360)` 正对塔：灯珠导视、河与探照灯在左、塔居中 |
| 05 成片 86% 像素亮于 20% | 亮带 + bloom + 统一曝光叠在一起 | 按机位重调曝光（05 = 0.92，01/02 = 1.05，04 = 1.10）→ 降到 79% |
| 自由环视拖到某些角度，整屏出大块硬边黑 | `OrbitControls` 默认 `maxPolarAngle=π`、`minDistance=0`：相机可钻到地底（实测 y = −94.8 m），从下方只看得到地台底面；近黑反照率 + 黑位 0.008 让它就是纯黑 | 每帧 `updateCamera()` 加世界坐标地面约束 `camera.y ≥ 3` + `minDistance 90`。试过 `maxPolarAngle=89°` 但会破坏 01/05（相机本就低于 target，y=14 < target 58），俯仰约束不能用，只用地面约束 |

中间帧截图已被最后一次 capture 覆盖（`captures/backlight/latest/` 每次整目录重写），
弯路的证据是参数 diff 与 `captures/backlight/2026-10-04/report.md` 的分阶段亮度统计。

---

## 本轮观察

**保留：**

1. **价值结构先于一切**。剪影在 shape 阶段（零盏灯）就已经成立——`shape/01`、`shape/05` 的
   黑剪影贴暖带是全实验最像黑色电影的两张；后面三个阶段只是往这个骨架上加灯，没有推翻它。
2. **雾色必须等于该方向的天空色，且俯视方向另用暗霾**。这是"远楼比天暗一档"和
   "俯视不被冲成亮天"能同时成立的唯一修正，一条 4 行的 GLSL。
3. **天空与雾共用一个 `skyBase()`**（`glsl.js`）：任何阶段、任何材质，远处收敛目标只有一个。
4. **真实 shadow map 低角度长影**：03-plan 的路网与街区明暗节奏靠它；`normalBias` 必须按 texel 物理尺寸给。
5. **四阶段只改 uniform**：同一份场景出 20 张图，联络表是可复现证据而不是手工截图。
6. **加性光柱的量级**：0.035 是"柱"，0.10 起就是"墙"；探照灯同理（0.06）。

**放弃 / 改掉：** 雾密度两档旧值、天空宽晕 6、黑位 0.020、水面恒定微幅与 0.85 反射、
光锥 0.10、统一曝光、04 旧机位（现象见上表）。

**尚不确定（要用户审图）：**

- 01 / 02 / 05 的 light 与 final 中景仍偏"奶"（`bright>20%` 在 54%–79%）：
  这是"城市光害"的正确表现，还是 bloom/雾仍需再压一档？
- 02-hero 近景的竖窗带读成白色梳齿，是否过亮、是否该降低点亮比例（现在 `strip.lit = 0.55`）。
- 03-plan 成片 77.9% 像素低于 2%：作为诊断镜头正好，作为成片是否太黑。
- 真机性能（4096² 阴影 + HalfFloat MSAA + bloom + rays）完全未测。

---

## 迁回 Unity 的是判断，不是代码

1. **雾色按方向取**：URP 自定义 fog（或按视角方向插值的 gradient）里，往上看取天空该方向的颜色，
   往下取地面霾色。这条决定远楼的暗度层级，比调雾密度重要一个数量级。
2. **近黑反照率 + 硬分带给层级**：城市面片反照率 0.02–0.04，层级交给三档 `dotNL` 与灯，
   不交给"给每栋楼涂个能看见的灰"。
3. **低角度键光 + 大范围 shadow map 是"逆光"的证据**，不是可选项；bias 按 shadow texel 米数调。
4. **加性光柱只做一件事：指向焦点**；强度量级与 bloom 阈值的比例关系可搬，绝对数字不可搬（线性管线不同）。
5. **一束仰射立面 + 全城唯一暖焦点**：主角塔以外不给任何立面"从下往上"的光。

不可搬：`config.js` 里所有绝对数字（灯强、曝光、雾密度）是 three 线性管线 + ACES 一次 tonemap 的产物，
Unity 侧要用原生灯光分层重调，只带走比例与上述判断。

---

## 验证范围

- `npm run build:backlight` 通过（有 bundle > 500 kB 的体积提示）。
- `npm run capture:backlight` 实跑通过：五机位 × 四阶段 = 20 张 + 联络表 + 分阶段亮度统计
  （`captures/backlight/2026-10-04/`，另有 `latest/`）。脚本内建 `pageerror` / `console.error` /
  HTTP ≥ 400 检查，本次未触发。
- 联络表与逐张图本轮共人工查看 6 次（含全尺寸裁切检查水面纹理与地面），参数改动均基于实际画面。
- 未验证：手动 GodRaysPass 与 4096² 阴影在真机/移动 GPU 的帧时（未跑 perf）、
  真实浏览器窗口下的人眼观感（无头截图 ≠ 屏幕观感）、Unity 侧任何接入。
- 导出脚本 `npm run export:backlight` 在 Blender 5.2.2 无头跑通并留有 stats（见"数据与工程边界"）。

---

实验与文档由 **opencode** 完成。
