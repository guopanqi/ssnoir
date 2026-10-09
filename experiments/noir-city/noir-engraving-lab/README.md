# Noir Engraving Lab

SSNoir 的独立 Three.js 长期视觉研究工程。只把经过验证的视觉结论迁回 Unity / CityBox。

## 新会话接续

读取 [AGENTS.md](AGENTS.md) → [CONTINUE.md](CONTINUE.md) → [CHECKPOINT.json](CHECKPOINT.json)，再运行 `npm run status` 并打开基线图片。
状态命令检查源码指纹、四镜头四层证据和当前待办；返回 2 表示源码与旧图不匹配，需要新截图。
实验目标、失败结论、审图标准、下一问题和流程改进方法都保存在工程内，无需依赖聊天 compact。

## 当前视觉基线

简单几何 → 选择性结构线 → 局部叙事光 → 空气 → 有限色阶 Print。

- Hero / Context / Silence 三级线；普通街屋默认 Silence。
- 低角度侧后 Key：垂直面亮、道路暗。
- 后巷：黑色巷口、冷色中景、远端暖门；真实顶棚和平台切光。
- 大部分画面保持冷色，红牌与暖门使用稀疏叙事色墨。
- 雨、雾、Bloom 和 Print 都可以分层关闭；几何光锥不进入正式基准。
- 四个镜头：Theater Street / Warehouse Fog / Alley Mouth / City Compression。

最新经过实际审阅的证据：[2026-10-03 本地研究记录](captures/reviews/2026-10-03-local/README.md)。
长期设计见 [DESIGN.md](DESIGN.md)，结论与失败实验见 [RESEARCH_LOG.md](RESEARCH_LOG.md)。

## 独立子实验

主实验研究的是**近景**（一条街、一个房间）。尺度不同的研究各自独立入口，
共用这个 Vite 服务与 `node_modules`，各有自己的 HTML、构建配置和研究记录。
入口在主页 `/` 的「视觉变量」面板底部，也可以直接访问：

| 实验 | 入口 | 记录 | 独立构建 |
|---|---|---|---|
| 桥头 | `/bridge.html` | [BRIDGE-STUDY.md](BRIDGE-STUDY.md) | `dist-bridge/` |
| 住所 | `/residence.html` | [RESIDENCE-STUDY.md](RESIDENCE-STUDY.md) | `dist-residence/` |
| 黑水 · 城市视角 | `/heishui.html` | [HEISHUI-STUDY.md](HEISHUI-STUDY.md) | `dist-heishui/` |
| 灯岸 · 城市视角 — by Spark | `/lantern-spark.html` | [LANTERN-STUDY.md](LANTERN-STUDY.md) | `dist-lantern-spark/` |
| 剪影夜城 · 城市视角 — by GLM | `/skyline-noir-glm.html` | [SKYLINE-NOIR-GLM-STUDY.md](SKYLINE-NOIR-GLM-STUDY.md) | `dist-skyline-noir-glm/` |
| 逆光 · 城市视角 — by opencode | `/backlight.html` | [BACKLIGHT-STUDY.md](BACKLIGHT-STUDY.md) | `dist-backlight/` |
| 雾与霓虹 · 城市视角 — 交互 UI | `/fog-neon.html` | [FOG-NEON-STUDY.md](FOG-NEON-STUDY.md) | `dist-fog-neon/` |

黑水、灯岸与逆光**消费外部几何**：各自把 CityBox 构建产物一次性烘焙成
`public/<实验>/city.{json,bin}` 快照（逆光是自己的一份，见 `tools/export-backlight.py`），
运行时不再接触 CityBox。
它有自己的七格城市机位（`1`–`7`）与四个分层阶段，同时支持拖动自由观察
（左键环视 / 滚轮推拉 / 右键平移 / `R` 复位回当前机位）。
调研笔记见 [HEISHUI-RESEARCH.md](HEISHUI-RESEARCH.md)。

## 运行

```bash
cd experiments/noir-city/noir-engraving-lab
npm ci
npm run dev
npm run build
```

鼠标自由观察；`1`–`4` 切换镜头；`Space` 开关雨；`H` 隐藏面板。

## 本地视觉循环

```bash
npx playwright install chromium
npm run capture
```

默认使用 Playwright 配套的 headless shell。`CHROME_PATH` 可指定已有浏览器；受限 Linux 环境优先用 headless shell，完整 Chrome 可能需要不可用的进程锁 socket。

快速检验一个变量：

```bash
npm run capture -- --shot 03-alley-mouth --mode final --out captures/trial-name
```

保留完整回归：

```bash
npm run capture -- --out captures/reviews/my-review
```

`--shot` 使用文件名 ID；`--mode` 支持 `shape / line / preprint / final`；`--port` 支持独立端口。无筛选时生成全部 16 张图。
输出目录必须在实验的 `captures/` 内，而且运行时会被替换；每项重要研究请使用新的目录名。

完整输出：

- `shape/*.jpg`：关闭线、空气、Bloom、Print，检查体块和基本灯光。
- `line/*.jpg`：只恢复结构线。
- `preprint/*.jpg`：恢复空气与 Bloom，关闭最终 Print。
- `final/*.png`：1600 × 900 完整视觉。
- `contact-sheet.jpg`：按 Shape / Line / Preprint / Final 排列。
- `report.md`：亮度、黑面积、亮面积、层影响与截图耗时。
- `manifest.json`：相机、画风配置、源码 SHA-256、浏览器版本、Git SHA（可用时）、图像指标。

循环：改少数变量 → 快速局部截图 → 打开图片实际观察 → 完整四镜头回归 → 记录保留或放弃及原因。
JS 异常和浏览器 console error 都会使截图任务失败，防止把报错页面当成成功结果。
本地实测完整截图约 14–18 秒，单镜头截图约 5 秒（含启动；速度随环境变化）。

## GitHub Actions

[Noir Engraving Captures](https://github.com/guopanqi/ssnoir/actions/workflows/noir-engraving-captures.yml) 继续承担推送后的独立回归。Actions artifact 保留 30 天，本地循环无需每次等待它。

`captures/latest/` 是可覆盖缓存；需要长期保存的研究证据放在 `captures/reviews/` 并显式加入 Git。本轮代码、配置、截图和记录都在本实验目录中。

## 边界与迁移

`main.js` 只装配；视觉参数在 `config/visualProfile.js`；几何在 `scene/`；灯光与空气在 `systems/`；Print 在 `post/`；摄影机位在 `shots.js`。

Three.js Object3D Layers 不作为 Light Linking。这里用光源锥角、距离与真实遮挡控制作用域；迁回 Unity 时使用原生灯光分层，并重新调节光度单位。

迁移的是线的重要性、叙事光范围、黑面积和摄影规律，不是 Three.js 代码或灯光强度数字。生产工程保持独立。
