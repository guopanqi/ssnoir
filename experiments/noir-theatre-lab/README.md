# Noir Theatre Lab

SSNoir 的第二个、完全独立的 Three.js 艺术实验。方向暂称 **Noir Theatre Engraving / 黑色剧场版画**。

它直接复制 SSNoir 现有 `City.fbx`、`剧院.fbx`、`巷子.fbx` 到本实验资产目录；复制后由本实验独立维护，运行时不引用 Unity、CityBox 或 `noir-engraving-lab`。

## 核心命题

> 形体建立世界，光决定可见部分，线帮助阅读。

优先级：黑色电影的光影叙事 → 舞台式框景与黑场 → 建筑模型的清晰体块 → 选择性的版画线刻。避免全描边、霓虹、Bloom 和无目的细节堆叠。

## 四个固定镜头

1. `01-city-overlook`：整城斜俯视。
2. `02-city-landmark`：低机位观察地标与纵深。
3. `03-alley-mouth`：巷口朝尽头，检查负空间与遮挡。
4. `04-alley-figure`：人物与门光中近景。

## 四层诊断

- `shape`：原始形体与材质层级。
- `light`：硬光与阴影。
- `line`：选择性几何线 + 屏幕空间结构边缘。
- `final`：有限灰阶、局部排线、颗粒和暗角。

## 运行

```bash
npm install
npm run dev
npm run capture
```

截图输出到 `captures/latest/`；CI 同时更新 `captures/review/contact-sheet.jpg` 作为当前可观察基准。
