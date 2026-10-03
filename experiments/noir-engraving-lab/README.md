# Noir Engraving Lab

SSNoir 的独立 Three.js 视觉研究工程。

它不是第二套客户端。它存在的目的，是先在一个低成本、反馈快、变量清晰的环境里把 SSNoir 的黑色电影视觉语言研究成熟，再把**结论**应用回 Unity / CityBox。

## 核心方向

暂定视觉语言：**Noir Engraving / 黑色版画**。

```
Shape
  ↓
Selective Line
  ↓
Narrative Light
  ↓
Atmosphere
  ↓
Print
```

也就是：

**简单几何 → 选择性的线 → 戏剧性光 → 空气 → 有限色阶与抖动的统一印刷层。**

详细原则、工程分层和长期路线见 [DESIGN.md](DESIGN.md)。

## 当前基线

当前版本只建立研究基础设施：

- 程序化小街区；
- 无纹理材质；
- 剧院 / 仓库 / 街屋 / 后巷 / 消防梯 / 路灯；
- hero / context 两级描线；
- 冷色 key / fill；
- 街灯与简化光束；
- 指数雾、雨、湿路面；
- Bloom；
- 与 Unity `SSNoirStylize` 同思想的 luminance / black-white point / finite levels / Bayer dither / cold ramp；
- 4 个固定 benchmark shots；
- 实时 A/B 控制。

这不是最终效果。第一轮正式研究从 **Shape / 构图与黑面积** 开始，而不是继续增加特效。

## 运行

```bash
cd experiments/noir-engraving-lab
npm install
npm run dev
```

构建：

```bash
npm run build
```

## 操作

- 鼠标：自由观察；
- `1`–`4`：切换四个 benchmark shots；
- `Space`：开关雨；
- `H`：隐藏面板。

## 文件

- `DESIGN.md`：长期视觉与工程设计；
- `RESEARCH_LOG.md`：经过实验的结论；
- `AGENTS.md`：后续协作规则；
- `src/config/visualProfile.js`：统一视觉参数；
- `src/scene/`：场景、材质、程序化原语；
- `src/systems/`：灯光、空气；
- `src/post/`：最终印刷层；
- `src/shots.js`：固定镜头；
- `src/ui.js`：研究面板。

## 迁回 Unity

不复制 Three.js 实现。

例如：

- “背景线减少更好” → 修改 CityBox 描线规则；
- “门光显著增强主体” → 新增 Unity 叙事灯模板；
- “非均匀雾有效” → 在 Unity Atmosphere 中实现对应算法；
- “6 色阶最好” → 调整现有 `SSNoirStylize`；
- “某种建筑体块密度最好” → 修改 CityBox 程序生成规则。

实验可以大胆失败，生产工程保持稳定。


## 自动视觉循环

实验目录包含固定截图管线。

本地安装 Playwright Chromium 后：

```bash
npm run capture
```

会生成：

```
captures/latest/
  final/
    01-theater-street.png
    02-warehouse-fog.png
    03-alley-mouth.png
    04-high-city.png
  shape/
    01-theater-street.png
    ...
  manifest.json
```

`final` 是当前完整视觉；`shape` 会关闭描线、雨、雾、光束、Bloom 和最终印刷层，用于单独判断体块与构图。

GitHub Actions 会在实验源码变化后重新生成这组基准图并写回仓库。截图提交只修改 `captures/latest/`，不会再次触发截图工作流，因此不会形成 CI 循环。

这组图是后续视觉讨论的共同观察对象：先看图，再改代码，再自动重拍。
