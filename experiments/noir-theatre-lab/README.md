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

- `shape`：统一中性材质与视空间法线明暗，不依赖场景灯光。
- `light`：硬光与阴影。
- `line`：选择性几何线 + 屏幕空间结构边缘。
- `final`：有限灰阶、局部排线、颗粒和暗角。

## 运行

```bash
npm ci
npm run dev
npm run capture
```

截图输出到 `captures/latest/`；CI 同时更新 `captures/review/contact-sheet.jpg` 作为当前可观察基准。

本地截图前运行 `npx playwright install chromium`，或用 `CHROME_PATH` 指定已有 Chrome。提交前先运行 `npm run build` 和 `npm run capture`，实际查看 contact sheet，再写研究记录。浏览器异常和 console error 会使截图失败，每张完成的截图会输出进度。

CI 固定检查触发提交的 SHA，依次构建、生成 16 张截图、上传完整 artifact，再把 contact sheet 和带源提交 SHA 的 metrics 回写实验分支。回写提交带 `[skip ci]`，且 review 目录不触发截图任务。若分支已推进，旧任务只保留 artifact，由新任务回写证据；不要用强制推送覆盖新改动。


## 本地快速循环与交接

优先在独立本地环境做调整与实际回看；阶段版本再提交到 `experiment/noir-theatre-lab`，由 Actions 做完整验收。无需 Remote Desktop Commander。

```bash
# 快速循环：四个镜头，只拍最终层
CAPTURE_MODES=final npm run capture
# 分析结构与照明
CAPTURE_MODES=shape,light npm run capture
# 阶段验收：全部 16 张
npm run build
npm run capture
```

`captures/latest/final-board.jpg` 是四个最终镜头的 2×2 总览；`contact-sheet.jpg` 是本次实际拍摄模式的诊断矩阵。仅完整验收更新 `captures/review/`，避免快速截图覆盖完整基准。

Actions 额外发布 `noir-theatre-source` 实验包，包含本实验代码、锁文件和模型。下载解压到空目录，`npm ci`、安装 Playwright 浏览器后即可截图，无需拉取整个 Unity 仓库。分发包没有 `.git` 时 manifest 的 `gitSha` 为 null；`sourceHash` 和各模型 SHA-256 始终记录实际输入，不能把本地修改冒充原提交。

目前测得本地完整截图约 24 秒，仅最终层约 13 秒；包含模型加载、浏览器启动、截图、指标和总览生成。实际耗时随设备和浏览器而变。

## 导入边界

保留 FBX 自带的坐标转换和缩放，只在世界 Y 轴追加艺术旋转。城市只保留建筑、道路与主要空间体块；导出的描线壳、外围郊野地面和小道具不参与宏观造型。后巷保留原墙、窗口、消防梯和道具，移除导出描线壳及原追逐人物，另建一个黑色电影人物。

剧院独立 FBX 目前只包含描线壳，不能当完整无纹理实体直接使用；只保留它作为资产诊断参考，画面地标使用独立程序化的退台塔楼、剧场基座、柱廊和檐口。两个场景仍共用 `PROFILE` 和同一后处理，灯光按场景定义，机位只存在于 `shots.js`。
