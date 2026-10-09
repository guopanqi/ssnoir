# 黑水 · 开源实现调研

> 配套 [HEISHUI-STUDY.md](HEISHUI-STUDY.md)。这份记录只回答两件事：
> **别人怎么做的**，以及**黑水实际抄了什么、拒了什么**。
> 所有链接都经过实际抓取确认；凡未证实的，都写明"未证实"。
> 结论先行的三句话在最下面「本工程的最小实现清单」。

---

## 0 先说结论

1. **没有现成的"显式灯表 + 解析 in-scattering"的 WebGL 体积雾库。**
   最接近的 [three-good-godrays](https://github.com/Ameobea/three-good-godrays) 是**一盏灯一个 pass**，
   走 shadow map 求遮挡 + 线性幂衰减；[three-volumetric-pass](https://github.com/Ameobea/three-volumetric-pass)
   的 fbm 密度场很漂亮，但 `#define DO_LIGHTING 0`，打开后用的是硬编码灯位和固定 `normal = vec3(0,1,0)`——
   它是个"漂亮的自发光雾"，不是"被灯照亮的雾"。
   **多灯解析散射这条路要自己写**，而它恰好是最划算的：每步只做几次 ALU，无贴图采样。
2. **three.js 官方的 godrays 示例用的是第三方 `goodrays` 包**；
   `examples/jsm/shaders/GodRaysShader.js` **不存在（404）**。
   pmndrs 的 `GodRaysEffect` 是纯屏幕空间径向模糊，**不读 3D 灯、不做遮挡**——
   灯被柱子挡住时它照样有 rays，穿帮。
3. **noir 的味道 90% 来自后期，不是来自几何。**
   关键是三条：逐环染色（紧的芯冷、大的晕暖）、`max(c*gain - crush, 0)` 求黑、
   量化前抖动。这三条比体积雾更早让画面"变成 noir"。

---

## 1 体积雾 / 光柱

### 1.1 three-good-godrays —— 本主题最值得读的一个

- 仓库：[Ameobea/three-good-godrays](https://github.com/Ameobea/three-good-godrays)（npm 0.12.1）
- 核心文件：[`src/godrays.frag`](https://github.com/Ameobea/three-good-godrays/blob/main/src/godrays.frag)、
  [`src/illumPass.ts`](https://github.com/Ameobea/three-good-godrays/blob/main/src/illumPass.ts)、
  [`src/compositor.frag`](https://github.com/Ameobea/three-good-godrays/blob/main/src/compositor.frag)
- 上游：[n8python/goodGodRays](https://github.com/n8python/goodGodRays)

**技术（每像素）**

1. `sceneDepth` → `WorldPosFromDepth()`；`dir = normalize(worldPos - cameraPos)`，`dist = distance(...)`。
2. **ray × 凸体 slab method 求交**：把光源作用域写成 6 个平面（点光用 AABB），
   `t = -sdPlane(cameraPos, n, h) / dot(n, dir)`，按 `denom` 符号分 entry/exit，
   取 `[max(entries), min(exits)]` 得到 march 区间。
   **这是把"整条视锥"压成"真正有雾的那一段"的关键**，直接省掉一半以上步数。
3. 步数自适应：`baseSteps = clamp(rayLength / max(raymarchStepSize, shadowTexelWorldSize*0.5), minSteps, maxSteps)`。
4. 抖动用 IGN：`fract(52.9829189 * fract(0.06711056*x + 0.00583715*y))`。
5. 提前退出：`earlyOutThreshold = -log(1-maxDensity)*samples`（Beer–Lambert 已饱和）。
6. 每步一次 shadow map 采样取遮挡，累加 `illum`。
7. 输出 `clamp(1 - exp(-illum), 0, maxDensity)` 进 rgb，**线性深度进 alpha**。
8. 合成走 **joint bilateral upsampling**（`exp(-d²/2σ_s²) * exp(-Δdepth²/2σ_d²)`）——
   这是能跑半分辨率又不糊边的原因。

**抄什么**：slab method 裁 ray、IGN 抖动、`earlyOutThreshold` 的推导、线性深度塞 alpha、JBU 上采样。
**抄但改**：它的 `distanceAttenuation` 是**线性幂衰减、不是 inverse-square**——
正合 noir「几个大灯、衰减可控、不想调物理单位」的需求，别改回物理正确。
**避**：一盏灯一个全屏 pass，灯一多就线性爆炸；它不做面光。

> 黑水采纳：IGN 抖动、半分辨率、`s` 的累加结构。
> 黑水未采纳：slab method 裁 ray（本工程是全局高度雾，没有单灯的凸体作用域）、
> shadow map（城市尺度下自阴影对氛围贡献很小，成本不划算）、JBU（用双线性 + 足够步数代替）。

### 1.2 pmndrs GodRaysEffect —— 同名但完全不同

- 文档：[GodRaysEffect](https://pmndrs.github.io/postprocessing/public/docs/class/src/effects/GodRaysEffect.js~GodRaysEffect.html)
- GLSL：[`convolution.god-rays.frag`](https://github.com/pmndrs/postprocessing/blob/main/src/materials/glsl/convolution.god-rays.frag)
- 理论出处：GPU Gems 3 Ch.13 "Volumetric Light Scattering as a Post-Process"

纯屏幕空间 radial blur：`delta = (lightPos - coord) * density / SAMPLES`，
循环 60 次 `coord += delta; texel *= illuminationDecay * weight; illuminationDecay *= decay`。
默认 `samples 60 / density .96 / decay .9 / weight .4 / exposure .6 / clampMax 1 / resolutionScale .5`。

**避**：它读的是屏幕上已经画好的高亮像素，**完全不读 3D 灯、不做任何遮挡计算**。
拿它做"雨雾里的灯柱"必然穿帮。黑水只借它的形状参数（decay/weight/clampMax）的概念，
实际光柱来自体积雾里的解析散射。

### 1.3 把解析点光/聚光注入全屏 raymarch

这是**没有现成 WebGL 库**的一块。可实现的路径（工业出处是 Lagarde & Zanuttini 的
*Local Image-based Lighting* 与 Wronski 的 Frostbite volumetric fog talk，
[Wronski 2014 SIGGRAPH 幻灯片](https://www.realtimerendering.com/advances/s2014/wronski/bwronski_volumetric_fog_siggraph2014.pdf)）：

```glsl
// 点/球光近似：对光球做线段积分，闭式解
vec3  L  = uLightPos[i] - p;
float tc = dot(L, v);                    // 沿视线的最近点
float d2 = dot(L, L) - tc*tc;            // 到视线的垂直距离²
float R2 = uLightRadius[i]*uLightRadius[i];
float seg = 2.0 * sqrt(max(R2 - d2, 0.0));
float T   = exp(-sigma * seg);           // 该段 Beer–Lambert
inscatter += uLightColor[i] * uLightIntensity[i] * (1.0 - T) * phase(dot(v, normalize(L)));
```

`phase()` 用 Henyey–Greenstein，`g ≈ 0.3~0.6` 偏前向——雨雾的强前向散射正合 noir 的灯柱。

**froxel / light-list texture 这条路明确不要走**：需要 3D texture + 两个额外 pass +
CPU 端 cell↔light 的 min/max 深度剔除；而且**没能找到任何一个可验证的、
确实做了 froxel light-list 的开源 WebGL 实现**。收益只在灯数 > 16 时才显现。

> 黑水采纳：显式灯表（`uniform vec4 uPPos[24]` 等）+ 每步解析贡献 + HG 相位。
> 黑水额外做的：把灯表分成"点光"和"城区底光"两组，
> 前者用 HG 相位（有方向感），后者各向同性（只负责给雾塑形）。
> 一个坑：**沿一条直线 ray，方向光的散射项是常量**，所以积分结果是
> `s × 光学厚度`——系数稍大就整片天爆掉。黑水把它压到 0.10。

---

## 2 描线

### 2.1 three.js `OutlineEffect`（inverted hull）

- 文件：[`examples/jsm/effects/OutlineEffect.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/effects/OutlineEffect.js)
- 顶点核心：`vec4 pos2 = projectionMatrix * modelViewMatrix * vec4(position + normal, 1.0);`
  `vec4 norm = normalize(pos - pos2);`
  `return pos + norm * outlineThickness * pos.w * ratio;`

**抄**：`* pos.w` 这个乘法就是"屏幕空间恒定线宽"的答案（透视除法会除以 w，提前乘 w 正好抵消）。
**避**：inverted hull 对所有背面外扩 → **凹面/折边也会被描**，
而且描边是平涂色、不随光照变化。它是 toon 用途，不是 noir 的"轮廓被灯点亮"。

### 2.2 pmndrs `OutlineEffect`（depth-only 4 tap）

- [`glsl/outline.frag`](https://github.com/pmndrs/postprocessing/blob/main/src/materials/glsl/outline.frag)
- 输入 buffer 必须把 viewZ 打包进 R、mask 写进 G；顶点阶段预计算 4 个邻居 UV。
- 片元是 Roberts cross：`d0 = (c0.x - c1.x)*0.5; d1 = (c2.x-c3.x)*0.5; d = length(vec2(d0,d1));`
  再用 mask 的可见性把边分成 visible/hidden 两路，各给一个颜色。

**抄**：4 tap 够用（vs Sobel 8 tap）；顶点阶段算邻居 UV；visible/hidden 双色。
**避**：只看 depth → **内部折边和轮廓无法区分**；线宽固定 1 texel。

### 2.3 pmndrs `EdgeDetectionMaterial` —— normal+depth 的正式实现

- [`src/materials/EdgeDetectionMaterial.js`](https://github.com/pmndrs/postprocessing/blob/main/src/materials/EdgeDetectionMaterial.js)
- [`glsl/edge-detection.frag`](https://github.com/pmndrs/postprocessing/blob/main/src/materials/glsl/edge-detection.frag)
- `EDGE_DETECTION_MODE == 1` 是 depth + 法线；顶点阶段预计算 8 个邻居 UV 后跑 Sobel；
  depth 项线性化后除以中心深度以免近处过敏感；`PREDICATION_MODE == 1` 让边只在谓词 buffer 命中处出现。

**这是本工程该用的模式。** 黑水采纳它的三个点：
① 深度边除以中心深度（相对量，远处一样锐利）；
② predication 做选择性描边（黑水用 mesh 的 `role` 写进颜色 alpha 当谓词，只给主角画折面线）；
③ **厚度 = `uThickness * (1/width, 1/height)`，绝不乘距离。**

### 2.4 只描轮廓 + 被光照调制

```
e_d = sobel(linearDepth) / centerLinearDepth;
n_c = normalize(normalAtCenter);
e_n = 1 - min(min(dot(n_c,n_l), dot(n_c,n_r)), min(dot(n_c,n_u), dot(n_c,n_d)));
silhouette = step(edgeThreshold, e_d) * (1 - smoothstep(normalThreshold, normalThreshold*2, e_n));
```

直觉：**深度跳变 + 法线几乎不变 = 两个物体的轮廓；深度跳变 + 法线剧烈变化 = 同一个物体的折边。**

描边亮度的调制有两种做法：
- **廉价**：在描边像素采一次 lit buffer 的 luma/hue，`outlineColor = lerp(baseNoir, hue, saturate(luma*k))`。
- **解析**：用 prepass 法线 + 灯表算 `sum(max(0,dot(n,L_i)) * color_i * atten_i)`。

> 黑水采纳：廉价那一路 + 一条"极弱冷底线"。
> 关键判断：**纯靠廉价那一路会得到"全黑画面里一根线都没有"**——
> 因为城市的背景亮度普遍低于任何合理的阈值。底线是必要的。

### 2.5 WebGL2 下按面 flat shading 与 per-face 随机

- `vec3 n = normalize(cross(dFdx(P), dFdy(P))); if (!gl_FrontFacing) n = -n;`
  GLSL ES 3.00（WebGL2 默认）下 `dFdx/dFdy` 是 core，`gl_FrontFacing` 存在。
  约束：只在单个三角形内正确；helper invocation 的边缘像素可能有 1px 噪声。
- **`gl_PrimitiveID` 在 GLSL ES 3.00 里不存在**（规范全文 0 次），
  WebGL 扩展注册表里也没有提供 primitive id 的扩展。
  `flat` 插值限定符存在，取 provoking vertex 的值；整型 varying 规范强制要求 `flat`。
- 可靠的 per-face 随机有三条：① 非索引化几何 + `flat out/in float vFaceId`；
  ② `gl_VertexID / 3`（规范说"value is not always defined"，需实测）；
  ③ **对某个每顶点唯一的 varying 求导**：`dFdx/dFdy` 在同一三角形内是常量，
  所以 `hash(round(vec2(dFdx(v), dFdy(v))))` 是真的 per-face，且不需要非索引化。
- **有坑的近似**：`hash(round(worldPos * k) + normal*17)` ——
  同一面内不同像素 `round` 后可能落到不同 bucket，面内出杂色。

> 黑水采纳：`facetNormal` + 方案③的变体。
> 黑水的 per-face 随机是 `h31(N * 37.13 + vec3(dot(N, P) * 0.113))` ——
> `N` 和 `dot(N,P)`（平面距离）在三角形内都是常量，所以这个哈希是真的 per-face，
> 不需要非索引化几何，也不需要 `flat`。

---

## 3 湿地面与反射

### 3.1 three.js `Reflector`

- [`examples/jsm/objects/Reflector.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/objects/Reflector.js)
- 镜像相机：`view.reflect(normal).negate().add(reflectorWorldPosition)`；
  `target` 同法镜像；`virtualCamera.up` 也要 `reflect(normal)`；再 `lookAt(target)`。
- `textureMatrix = bias(0.5) * P_virtual * V_virtual * matrixWorld`，
  bias 就是那个 `set(0.5,0,0,0.5, 0,0.5,0,0.5, 0,0,0.5,0.5, 0,0,0,1)`。
- `renderer.clippingPlanes = globalPlanes` 裁掉地面以下几何。
- **已知坑**：源码里 `scope.visible` 那两行是被注释掉的——它没有自动隐藏自己，会自反射。

> 黑水采纳：整个镜像相机的构造 + texture matrix 的 bias 拼法 + 全局裁剪面。
> 黑水额外做的：**反射里必须隐藏地面与水面本身，也要隐藏雨**，
> 否则地面会挡住自己的反射、雨丝会被当成实体反射进去。

### 3.2 SSR —— 明确不做

- 官方 [`postprocessing/SSRPass.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/postprocessing/SSRPass.js) 要先渲一张 `ReflectorForSSRPass` 当近场填充，
  再叠屏幕空间 ray march；`MAX_STEP = sqrt(w²+h²)`；需要 3 张 G-buffer + 线性深度 RT + 两遍模糊。
- 关于 0beqz：[realism-effects](https://github.com/0beqz/realism-effects) 仓库存在，
  但 **`src/SSRPass.js` 在主分支上已经不存在（404）**——它现在只做 SSGI / TRAA / 运动模糊 / AO。
  **"0beqz 的 SSRPass"这个说法已经失效，别按老资料引。**
- **为什么不做**：远处港口 + 大面积湿地是 SSR 最差的组合——屏幕外没有信息、
  掠射角步长爆炸、闪烁严重。这是**结论**，不是偷懒。

### 3.3 drei `MeshReflectorMaterial` —— 粗糙度驱动的平面反射

- [`MeshReflectorMaterial.tsx`](https://github.com/pmndrs/drei/blob/master/src/materials/MeshReflectorMaterial.tsx)
- 全部答案就在一行：

  ```glsl
  float blurFactor = min(1.0, mixBlur * reflectorRoughnessFactor);
  merge = mix(merge, blur, blurFactor);   // 粗糙度决定"锐利镜面 vs 模糊"
  ```

  不需要 mip-chain，不需要 cone tracing。

> 黑水采纳：不做 mip-chain，直接在采样时沿屏幕纵向做 5–6 tap 的 tent 加权模糊，
> 半径由粗糙度和距离驱动。**"湿地面光带"与"模糊反射"的唯一区别就是只在纵向 blur。**

### 3.4 一体化范本：ektogamat/threejs-conference

- 仓库：[ektogamat/threejs-conference](https://github.com/ektogamat/threejs-conference)（Threejs-Punk，WebGPU/TSL）
- 文档：[`docs/techniques/wet-ground.md`](https://github.com/ektogamat/threejs-conference/blob/main/docs/techniques/wet-ground.md)、
  [`docs/techniques/collision-rain.md`](https://github.com/ektogamat/threejs-conference/blob/main/docs/techniques/collision-rain.md)
- 源码：[`createGround.js`](https://github.com/ektogamat/threejs-conference/blob/main/src/world/ground/createGround.js)、
  [`rainRipples.js`](https://github.com/ektogamat/threejs-conference/blob/main/src/tsl/rainRipples.js)

它明确记录了**放弃 SSR** 的结论，改用半分辨率 planar reflection + oblique clip。
可抄的六条：

1. **把反射乘进 emissive，不要当 envMap**：`emissive = refl.rgb * roughness.oneMinus() * uReflectionStrength`。
2. `roughness.oneMinus()` 作为 wetness。
3. 反射 UV = screenUV + 波纹扰动 + 法线扰动。
4. **0.5 分辨率 + 隔帧 + 强度只有 0.08**——"一点霓虹味，不是铬地板"。
5. **世界空间雨波纹**（不要用 mesh UV，否则大地面上有接缝且随平面缩放变化）：
   3×3 cell（`MAX_RADIUS=1`），hash 出中心偏移 → `t = fract(0.3*time + hash)` →
   `d = length(v) - 2t` → 中心差分近似 `sin(31d)` 环的导数 → 组装法线。
   **固定 9 cell/像素，与雨滴数无关。**
6. 镜像相机上 disable `RAIN_LAYER`。

**"Do not fold the mirror pass back into the post graph"** —— 用独立的 `renderer.render`
到自己的 RT，别塞进后处理图。

> 黑水采纳：全部六条 + 那一句关于独立 pass 的告诫。
> 黑水的波纹是简化版（一个格点一个环，不做中心差分），
> 因为它只用来推开反射 UV，不参与法线。

### 3.5 廉价解析光带（如果连 planar RT 都不想开）

把灯的发光位置沿镜面镜像得到虚拟像点，在屏幕空间**只沿 Y 方向**拉长：

```glsl
vec2 duv = vec2(0.0, uSmearLength * (1.0 - roughness));   // ★ 只有 y 分量
```

`TAPS = 4~6` + IGN 抖动 tap 位置即可，不要 Poisson disk。成本 ≈ 5 次采样。
物理依据可见 Langer & Bourque 关于湿路面高光沿视觉竖直方向拉长的论文。

> 黑水用 planar RT，没用这条；但它说明了"为什么光带一定沿屏幕纵向"。

---

## 4 后期

### 4.1 UnrealBloomPass 的逐 mip 染色

- [`UnrealBloomPass.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/postprocessing/UnrealBloomPass.js)
- 5 级 mip，合成时每一级一个 tint color 和一个 weight：

  ```glsl
  float lerpBloomFactor(const in float f){ float m = 1.2 - f; return mix(f, m, bloomRadius); }
  vec3 bloom = 3.0 * bloomStrength * (
      lerpBloomFactor(bloomFactors[0]) * bloomTintColors[0] * texture2D(blurTexture1, vUv).rgb + ... );
  ```

**`bloomTintColors[i]` 是 noir 的核心武器**：让低 mip 偏冷青（高光硬芯）、
高 mip 偏暖钠黄（大范围 veiling glare），一次就得到冷暖分层。

> 黑水采纳了这个思想，但用更省的方式实现：只有一条 bloom 链，
> 在合成时按 `smoothstep(0, 0.22, luma(bloom))` 在冷青与暖钠之间插值。
> 效果接近，成本为零。

### 4.2 anamorphic 横向拉丝

- 官方 WebGPU 示例 [webgpu_postprocessing_anamorphic.html](https://github.com/mrdoob/three.js/blob/dev/examples/webgpu_postprocessing_anamorphic.html)
  （**WebGL 侧没有官方示例**；可验证的 WebGL 复刻是
  [heygen-com/hyperframes `vfx-anamorphic-flare.html`](https://github.com/heygen-com/hyperframes/blob/main/registry/blocks/vfx-anamorphic-flare/vfx-anamorphic-flare.html)）
- 逐行可读的算法：

  ```js
  bloomPass.setResolutionScale(0.25);
  Loop({ start: halfSamples.negate(), end: halfSamples }, ({ i }) => {
    let softness = float(i).abs().div(halfSamples).oneMinus();
    softness = softness.pow(2.0);                                        // (1-|i|/half)²
    const shiftedUV = vec2(uv().x.add(invSize.x.mul(i).mul(4.0)), uv().y); // ★ 只有 x 偏移
    total.addAssign(brightPass.sample(shiftedUV).mul(softness));
  });
  return total.div(samples.div(3.0));   // ★ 除以 samples/3，是 gain>1 不是平均
  const anamorphicPass = bloomPass.mul(tintColor);   // tint 默认 0x7a8aff
  ```

- 官方 GUI 实测默认：`tint 0x7a8aff / threshold 0.3 / intensity 5 / samples 80 / resolutionScale 0.25`。
- 两个可复用技巧：
  - **闭式替换 80-tap 循环**：对小而亮的源，tent 权重的水平 blur 的解析结果
    就是源点处的一个 tent，直接画渐变即可。
  - **在 1/4 分辨率 buffer 上做 streak 再双线性放大**——1/4 分辨率本身就是
    竖直方向的低通，免费得到 anamorphic 的纵向柔化。

> 黑水采纳：只沿 x 偏移、`(1-|i|/half)²` 权重、冷色 tint、1/8 分辨率。
> 黑水未采纳 `div(samples/3)` 那个 gain——先用平均，增益交给 `uStreakAmt` 控制，
> 更好调。

### 4.3 halation ≠ bloom

- **Bloom** = 镜头/光学 veiling glare：偏中性偏冷、半径较小、围绕所有高亮。
- **Halation** = 光穿过乳剂层后在片基反射回来二次曝光：**严重偏红/橙、半径大而软、
  只在极高反差的强高光周围出现**。它是"胶片的"，不是"镜头的"。

**实现上必须是两条独立通道**：halation 用更高 threshold + 更大半径 + 强偏红 tint。
**不要用同一条 bloom 换 tint**——threshold 和半径必须分开，
否则普通高光也泛红，立刻廉价。

> 黑水采纳：独立的 `halBright`（阈值 0.85）+ 1/8 分辨率 + 3 轮 blur，
> 回加时 `vec3(1.0, 0.30, 0.14)`。

### 4.4 色散 / 暗角 / 颗粒 / 片门抖动

- **色散**：three.js [`RGBShiftShader.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/shaders/RGBShiftShader.js) 是均匀 RGB shift；
  pmndrs [`chromatic-aberration.frag`](https://github.com/pmndrs/postprocessing/blob/main/src/effects/glsl/chromatic-aberration.frag)
  的 `RADIAL_MODULATION` 让**只有画面边缘有色散**，比均匀 shift 更像真镜头。**黑水用后者。**
- **暗角**：three.js [`VignetteShader.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/shaders/VignetteShader.js)
  是往**白**混（`1-darkness`），darkness=1 时才往黑。黑水不用它的公式，直接乘。
- **颗粒**：three.js [`FilmShader.js`](https://github.com/mrdoob/three.js/blob/dev/examples/jsm/shaders/FilmShader.js)
  是**乘性**的（`base + base*clamp(0.1+noise,0,1)`），黑位安全；
  pmndrs `noise.frag` 的非 PREMULTIPLY 分支是**加性**的，会在纯黑上撒灰。
  **黑水用乘性颗粒。**
- **片门抖动**：最好的 primary source 是
  [Frame jitter and gate weave](https://artifacts.gostrobrod.dev/effects/film-frame-jitter/)（George Ostrobrod）。
  `k(t) = floor(t * f_r)`（**按帧率量化时间**），`u_s = u_d + δ/size`（**反向映射**），
  再加一个**静止的片门暗边**当参照物：

  ```glsl
  vec2 edgePixels = min(uv, 1.0 - uv) * size;
  float distanceToEdge = min(edgePixels.x, edgePixels.y);
  float clearGate = smoothstep(0.0, 12.0, distanceToEdge);
  return mix(1.0 - shadowAmount, 1.0, clearGate);
  ```

  **"门静止、画面在其下移动"是让 weave 被看见的关键**——只抖 UV 不画片门暗边，
  观众只会觉得画面在晃。

> 黑水采纳：径向色散、乘性颗粒、片门抖动（水平 1.1 px / 垂直 1.1 px / 8 次每秒 / 边缘阴影 16%）。

### 4.5 受限色阶：保持黑位真黑的三条硬规则

1. **不要在 log 空间量化**。log 会把 0 附近拉成大片低数值，量化后成为"奶灰"底噪。
2. **lift 用加法、crush 用减法**：`pow()` 类曲线在 0 处导数为 0/∞，抬起来就压不下去；
   `max(c*gain - crush, 0)` 才能真正把一段暗部钉死在 0。
3. **grain 也必须被黑吃掉**：用乘性，或把 noise 乘上 `smoothstep(0, 0.1, luma)`。

**量化前的抖动用 IGN**（`fract(52.9829189 * fract(0.06711056x + 0.00583715y))`），
比 Bayer 更不容易看到规则图案。

**不要用 `HalftoneShader` / `DotScreenShader`** —— 那是印刷网点，会把画面变成漫画，
跟 noir 的"打印级色阶"不是一回事。

> 黑水在这里**多踩了一个坑**，记录在案：一开始把密度控制与量化放在**线性空间**，
> 14 档时第 1 档是 0.071 linear（≈ sRGB 0.31），整幅夜景被压成纯黑；
> 改到**显示域**才成立。另外逐通道 `floor` 会把暗部的色相撕成青块/红块，
> 必须**只量化亮度再按比例缩回**。

---

## 5 雨

- 一体化范本仍是 [ektogamat/threejs-conference](https://github.com/ektogamat/threejs-conference)：
  用**正交俯视相机渲一张高度场**做碰撞（不是 CPU raycast），
  雨的 `position += velocity`、`floorY = texture(rt, uv(position.xz)).y`。
  代价是高度 pass 期间必须隐藏雨丝、天空穹、粒子等一切会变成"地面"的东西。
- **WebGL2 的等价做法更省**：把位置做成 `InstancedBufferAttribute`，
  在**顶点着色器里解析求位置**（`p += slant * time * speed; p = mod(p, box) - box/2; p += camPos`），
  **无状态、不需要 compute、天然可暂停/回放**。
- 屏幕空间镜头水滴（[NordicBeaver/rain-shader](https://github.com/NordicBeaver/rain-shader)）
  用"随机位置的水滴扰动背景 UV"而不是画水滴本体。
  **明确不用**：城市远景视角下整屏 UV 扭曲很假。

> 黑水采纳：顶点着色器解析落体 + `mod` 环绕跟随相机 + 三层不同速度/长度/宽度 +
> 加性混合。**一条 `uRainAmount` 同时驱动雨丝、涟漪强度和（通过 shot 参数）雾浓度。**
> 未采纳：高度场碰撞（城市尺度下 7000 滴雨落到哪个屋顶根本看不见）。

---

## 6 程序化窗光

**没有找到专门做"three.js 程序化窗光"的权威开源参考。** 可靠的基础设施是官方 instancing 示例：
[instancing](https://github.com/mrdoob/three.js/blob/dev/examples/webgl_buffergeometry_instancing.html)、
[instancing_billboards](https://github.com/mrdoob/three.js/blob/dev/examples/webgl_buffergeometry_instancing_billboards.html)、
[instancing_interleaved](https://github.com/mrdoob/three.js/blob/dev/examples/webgl_buffergeometry_instancing_interleaved.html)。

以下是从这些基础设施推出的方案（**是综合，不是 repo 转述**）：

1. **instanced quad + per-instance attribute**：一次 draw call 渲完整城的窗。
2. **窗图案图集**：8×8 纹理，每 tile 一种"窗户亮暗网格"，换图案不用换几何。
3. **per-instance hash 决定亮/灭 + 闪烁**，**关键是把时间量化**：

   ```glsl
   float flicker = hash21(vec2(windowId, floor(uTime * 0.25)));   // ★ 量化时间
   ```

   不量化的话每盏灯每帧都在闪，看起来像噪点而不是灯火。
4. **色温用 2~3 个聚类色加权选择**，不要均匀随机 hue。
5. **纯 emissive + additive + depthWrite = false，绝不给每扇窗加 PointLight**——
   forward 渲染下灯数直接乘进所有材质 shader，几十盏就编译爆炸。

> 黑水采纳：3、4、5。窗光是合并好的四边面片（`aTier` 定色温档、`aRand` 定闪烁），
> 没有走 instanced quad，因为数据本来就已在 Blender 里合并。
> 抖动量化用的是 `floor(uTime * uLife.y)`，与这条建议一致。

---

## 本工程的最小实现清单

**采纳了**（按对画面的贡献排序）

| # | 技术 | 来源判断 |
|---|---|---|
| 1 | 显式灯表 + 每步解析 in-scattering + HG 相位，半分辨率 + IGN 抖动 | 自建（无现成库） |
| 2 | 冷 bloom / 红 halation 两条独立通道 + 逐环冷温染色 | UnrealBloomPass 的 `bloomTintColors` 思想 |
| 3 | `max(c*gain - crush, 0)` + 显示域量化 + 只量化亮度 + IGN 抖动 | 4.5 节三条硬规则 |
| 4 | 半分辨率 planar reflection + 纵向拉丝 + 世界空间雨滴涟漪，只给贴地与水面 | Reflector + MeshReflectorMaterial + threejs-conference |
| 5 | depth+normal 边缘 → 逆光线，亮度由背景光决定，厚度不乘距离 | pmndrs EdgeDetectionMaterial 的模式 |
| 6 | `cross(dFdx, dFdy)` 面法线 + 平面距离哈希做 per-face 随机 | GLSL ES 3.00 规范 |
| 7 | 顶点着色器解析落体 + `mod` 环绕的无状态雨 | threejs-conference 的 WebGL2 等价做法 |
| 8 | 乘性颗粒 + 径向色散 + 片门抖动 + 静止片门暗边 | FilmShader / pmndrs / Ostrobrod |

**明确没做，以及为什么**

| 技术 | 原因 |
|---|---|
| SSRPass / 屏幕空间反射 | 远港 + 大面积湿地是 SSR 最差组合；官方方案要 6 张 RT |
| froxel / light-list 体积雾 | 没有可验证的 WebGL 实现可抄；灯数 ≤ 16 时收益为负 |
| TAA / 时间重投影 | 固定机位，抖动需求用 IGN + 固定 seed 就够了 |
| inverted-hull 描边 | 对所有背面外扩，折边也被描；平涂色、不受光调制 |
| pmndrs `GodRaysEffect` | 不读 3D 灯、不做遮挡，灯被挡住照样有 rays |
| `HalftoneShader` / `DotScreenShader` | 印刷网点，会把画面变成漫画 |
| 屏幕空间镜头水滴 | 城市远景下整屏 UV 扭曲很假 |
| 每扇窗一个 `PointLight` | forward 渲染下灯数乘进所有材质，编译爆炸 + 掉帧 |
| slab method 裁 ray / shadow map | 全局高度雾没有单灯凸体作用域；城市尺度自阴影收益小 |
| 高度场雨碰撞 | 7000 滴雨落到哪个屋顶，在城市视角下看不见 |

**未能证实、请勿当成已存在**

1. 任何提供真正 froxel / light-list texture 的开源 WebGL volumetruc fog 实现。
2. `three.js/examples/jsm/shaders/GodRaysShader.js` —— 不存在（404）。
3. 0beqz 的 `SSRPass` —— realism-effects 主分支已无此文件。
4. `roombawulf/outline-effect` 的 npm 包 —— 仓库存在，npm 未正常发版。
5. three.js 官方的 **WebGL** anamorphic 示例 —— 只有 WebGPU 版。
6. 专讲"noir 打印级 posterize"的 three.js 实现。
7. 专讲"程序化窗光"的权威 three.js 开源参考。
8. 任何提供 primitive id 的 WebGL2 扩展。
