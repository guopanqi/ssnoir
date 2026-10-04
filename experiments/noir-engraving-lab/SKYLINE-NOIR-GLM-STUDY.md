# SKYLINE NOIR (by GLM) — 城市视角剪影实验

> 独立实验，入口 `skyline-noir-glm.html`，源码 `src/skyline-noir-glm/`。
> 不依赖主实验的版画管线，不依赖 heishui 的真实城市几何；程序化城市，seed 固定（20261004）。

---

## 一句话论点

**夜城是一张海报：建筑是黑纸剪影，天空是被城市呼吸染亮的底色，光只落在少数有叙事理由的地方。**

与同目录其他实验的关系：
- 主实验（Noir Engraving）走"街道级选择性刻线 + 印刷量化"——本实验刻意不描线、不量化、不 dither 成片。
- heishui 走"真实几何 + 体积雾 + 镜像反射"的摄影路线——本实验用程序化城市、指数雾、无反射 RT。
- 本实验回答的问题是：**城市视角的可读性能不能只靠"剪影的分组与节律"建立**。这恰好是设计目标里"有辨识度的建筑轮廓、清晰的空间层次、视觉焦点"的直接检验。

---

## 视觉语法（按优先级）

1. **轮廓先于细节**。每栋楼必须靠剪影被认出来：退台三段（装饰艺术）、水塔/烟囱/山墙/天线（屋顶件语法）、或归入几乎无细节的远景带。地标负责锚点：主角塔（退台+冠部竖棱+尖顶+探照灯）、钟塔（亮面圆钟）、气柜（带微光环的圆柱）、教堂尖塔、双烟囱。
2. **四段明度分层**，由指数雾一次完成：前景屋顶（<100 m，<10% 雾）→ 中景楼海（100–250 m，10–30%）→ 市中心（~450 m，~50%）→ 远景带（620–840 m，75–90%，几乎化进天色）。雾色 = 地平线辉光色（0x22303d），天际线因此"沉进天里"而不是"贴在天上"。
3. **一个焦点**。主角塔是全城唯一的活动光（旋转探照灯，加性光锥、无雾），加上全城唯一允许的红霓虹（塔基 marquee）。月亮方位角专门设计成只进入 04 剪影机位、并贴着塔冠。
4. **窗是神经系统**。4 色温档（钠暖 66% / 白炽 20% / 冷青 10% / 亮芯 4%），按分区控制亮灯率与亮度（老城 40% 亮但暗、市中心 26% 亮但更亮），约 5–6k 扇窗一次 draw call。少数窗做量化时间的呼吸闪烁。
5. **成片只做胶片的事**：曝光 → 线性域压黑 → 冷影/暖高乘性分离 → 暗角 → sRGB → 显示域乘性颗粒（IGN + 亮度遮罩，黑位不被颗粒抬起）。不量化——印刷感是主实验的路线。

## 布局决定

- x=0 整列街块让位成**南北大道**：城市需要一条真正的大街，02 机位站在它里面，主角塔在它尽头。
- z=110 整行让位给**高架铁路走廊**：第二地平线，桥上有作业灯点。
- 分区：市中心（退台塔群）、中环、外环、老城（暖窗密、砖色）、工业区（棚+气柜+烟囱+烟柱）、远景带（绕城一圈的黑板墙）。
- 干道灯池 + 市中心环形灯 + 真 PointLight 只给大道近景四处（建筑反照率近黑，更多真光没有意义）。

## 固定机位（判断以这四格为准）

| # | 名称 | 站位 | 检验什么 |
|---|---|---|---|
| 01 | 海报全景 | (-300, 60, 300) 近水平视 | 四段明度、天际线带、焦点塔可读 |
| 02 | 大道 | (0, 9, 172) 人眼 | 灯池导视、高架横切、红线霓虹、街道纵深 |
| 03 | 高架 | (170, 13.5, 110) 桥面 | 桥面引导线、楼海剪影、市中心收束 |
| 04 | 逆光剪影 | (269, 31, 79) 低角 | 月亮贴塔冠、烟囱剪影、轮廓辨识度 |

截图冻结在 t=19.6 s（探照灯光束指向西北，01/04 可见）。

## 本轮观察（2026-10-04，7 轮迭代）

保留：
- 指数雾分带 + 雾色=辉光色，是四段明度成立的唯一关键；雾一弱，全城糊成黑贴黑。
- 月光强度 0.95 + 分区反照率 0.16–0.28：中景楼有"体"可读，同时仍是剪影。最初 0.5 + 0.07 是全黑。
- 大道（整列让位 40 m）替代 8 m 街缝：街缝机位读成"贴脸窗墙"，大道才有街道纵深。
- 月亮"小盘 + 紧晕"（角半径 1.3°、晕指数 320）：晕一松整片天就亮。
- 探照灯作为唯一活动光 + 无雾 ShaderMaterial：在 50% 雾距上依然是焦点。

放弃 / 改掉：
- UnrealBloomPass 在 headless 环境把 readBuffer 就地写黑（to-screen 路径正常、in-place 回写失效）→ 自写 NoirBloomPass（同样架构：高通→3 级降分辨率模糊→加权合成，但全部走标准 writeBuffer 契约）。
- 屋顶前景机位（03 旧概念）：中景窗海没有层级，换成高架桥面机位。
- 气柜光环 MeshBasic 0x8fa4b2 → 0x2a343c：白天使般的亮椭圆。
- 天空 8bit 渐变色带 → 天空 shader 里加 IGN 抖动。

已知未解决（下一步）：
- 01 里探照灯光束源（塔冠）偶尔被前景楼切掉，读成"彗星"；需要微调机位或光束仰角。
- 03 桥面自身仍是"黑贴黑"，桥缘没有受光暗示；可以给桥面栏杆一条极弱的冷边。
- 远景带是纯黑板，没有窗点；加 2% 极暗窗会更有"远城"感。
- 湿地面/反射刻意没做（heishui 的领地）；如果做，只需要灯池的纵向拉丝，不需要 planar RT。

## Three.js 开源实现的调研与采纳

以下每一条都实际抓取源码/文档核实过。

| 采纳 | 来源 | 用法 |
|---|---|---|
| **inverted hull 墨线壳** + `* pos.w` 恒定屏幕线宽 | [three.js OutlineEffect](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/effects/OutlineEffect.js)（`examples/jsm/effects/OutlineEffect.js`） | `pos2 = P·MV·(position+normal)`，`pos + normalize(pos-pos2)·thickness·pos.w·ratio`。只给地标包 BackSide 壳（不做全场景渲染器包装），手动 FogExp2 混合。海报语言的"墨线重量" |
| **UnrealBloomPass 的架构**：亮度高通 → 逐级降分辨率 separable blur → 加权合成、逐级染色 | [UnrealBloomPass](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/postprocessing/UnrealBloomPass.js)（`bloomTintColors` 思想） | 自写 `post/bloom.js`：同样 5 级结构裁到 3 级，紧芯偏冷、大晕偏钠暖；但全部 pass 写 writeBuffer（规避上文 headless 坑），软膝高通（UE4 式）。EffectComposer/RenderPass/ShaderPass 沿用官方 |
| **加性光锥 + Fresnel 边缘衰减**做探照灯 | [Stemkoski 的 glow/volumetric light cone](https://github.com/stemkoski/stemkoski.github.com)（`pow(c + dot(V,N), p)` + AdditiveBlending） | `atmosphere.js`：锥体尖端锚在塔冠，`core = pow(abs(dot(V,N)),1.6)`，沿长度 `(1-t)^1.7` 淡出，DoubleSide |
| **软膝亮度高通**（UE4 / Call of Duty bloom 的公开算法） | 同上 UnrealBloomPass 系资料 | `soft = clamp(br-t+k,0,2k); soft²/4k` |
| **乘性颗粒 + IGN 抖动 + 颗粒被黑吃掉** | three.js FilmShader（乘性）与 IGN 通识；黑位规则与 heishui《参考研究》§4.5 一致（独立复核） | 成片最后一道，sRGB 之后、按 `smoothstep(0,0.12,luma)` 遮罩 |
| **量化时间闪烁**（`floor(t*rate)` 再 hash） | three.js instancing 系示例的通识做法 | 窗光闪烁、灯的呼吸；不量化会变成每帧噪声 |
| **拒绝**：pmndrs GodRaysEffect（屏幕空间径向模糊，不读 3D 灯、不做遮挡）；Halftone/DotScreen（印刷网点≠海报剪影）；SSR/planar 反射（本实验无湿地主张）；每扇窗一个 PointLight（forward 灯数爆炸）；屏幕空间镜头水滴 | | |

## 工程结构

```
skyline-noir-glm.html            入口（独立 HUD/面板，?capture=1 进截图模式）
vite.skyline-noir-glm.config.js  独立构建（dist-skyline-noir-glm）
src/skyline-noir-glm/
  profile.js        ★ 全部视觉参数（调画面先看这个）
  rng.js            mulberry32 可复现随机
  shots.js          四个固定机位
  scene/sky.js      渐变穹顶 + 月 + 星 + IGN 抖动
  scene/city.js     街块语法 / 分区 / 大道与高架走廊 / 远景带 / 灯位
  scene/landmarks.js 五个地标 + 主角塔窗 + 红霓虹
  scene/windows.js  合并窗光几何 + 量化闪烁
  scene/inkOutline.js  地标墨线壳（OutlineEffect 数学）
  systems/lighting.js  月光 / 半球 / 灯池 / 灯头光斑 / 近景 PointLight
  systems/atmosphere.js 雾 / 探照灯 / 烟柱
  post/bloom.js     NoirBloom（标准 read/write 契约）
  post/grade.js     成片（曝光/压黑/冷暖/暗角/sRGB/颗粒）
  main.js           只装配
  captureApi.js     window.__skylineNoirGlm
tools/capture-skyline-noir-glm.mjs   本地视觉循环（截图/联络表/亮度报告/manifest）
tools/skyline-probe.mjs              渲染管线调试探针
captures/skyline-noir-glm/latest/    最新证据（联络表 + report.md + manifest.json）
```

## 跑

```bash
cd experiments/noir-engraving-lab
npm run dev                       # http://localhost:5173/skyline-noir-glm.html
npm run capture:skyline-noir-glm  # 全部机位 × final/shape → captures/skyline-noir-glm/latest
npm run build:skyline-noir-glm
```
键位：1–4 机位 · Space 暂停 · H 隐藏面板 · 拖动环视 · 滚轮推拉。

## 迁回 Unity 的是判断，不是代码

1. **城市视角的分层靠"雾色=天际辉光色"一条就立起来**，Unity 里等价于把高度雾/距离雾颜色绑定到天穹地平线色，密度按 450 m 处 ~50% 校准。
2. **灯光高度衰减**（灯只洗到街面往上十几米）是零成本高收益项；本实验用"近黑反照率 + 少量真点光"达到同一目的。
3. **窗是城市视角的主体照明**：分档色温 + 分区亮灯率 + 量化闪烁，一张 additive 面片搞定；Unity 用一张合并 mesh + vertex color 即可。
4. **一个活动光 + 一点叙事红**足够建立焦点；不要第二盏全城方向光以外的"氛围光"。
5. 地标剪影辨识度来自屋顶件语法（退台/水塔/烟囱/尖顶），这些在 CityBox 里是可生产的 prefab 件，不是贴图细节。
