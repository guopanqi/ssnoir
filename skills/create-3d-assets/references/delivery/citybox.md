# CityBox 城市与场景交付

CityBox 以 Blender 为唯一媒介，**Prefab 是唯一的单位**。约定与命令的权威说明是 `city-box/README.md`，本文只讲交付顺序。

## 唯一源与发布物

- Prefab：`city-box/prefabs/<名>.blend`（一个同名顶层集合 + `footprint`；净几何、米制、原点脚下）
- 程序化 Prefab 的生成脚本：`city-box/prefabs/src/<名>.py`
- 布局：`city-box/city.blend`（只有 Prefab 实例；人和 Agent 直接编辑）
- 候选与参考图：`city-box/prefabs/review/`
- 构建产物：`city-box/build/city/`（`city_build.blend`、`City.fbx`、`places/<名>.fbx`、`report.json`、预览图）
- Unity 固定发布物：`UnityClient/Assets/Resources/Models/Environment/City.fbx`（世界层）和
  `UnityClient/Assets/Resources/City/Places/<名>.fbx`（每个顶层 Prefab 的 focus 描线 + 内部）

世界层带全部语义对象和外壳，导航、镜头在任何地点细节没加载时都能工作；细节文件运行时由 `CityPlaces` 按名挂回 City 下。一个顶层 Prefab = 一个文件，嵌套子 Prefab 跟外层走。

## 接入顺序

1. 做出 `prefabs/<名>.blend`：带 Anchor 的 Prefab，名 = 主锚点的 Scheme 节点名；放 `Anchor_<名>`、`Camera_<名>`（50mm 透视，custom prop `drag = "orbit" | "pan" | "static"`）；orbit 配 `OrbitPivot_<名>`，pan 配贴地框 `PanBounds_<名>`（`pan_bounds_for`），static 什么都不配。
2. 单体预览，直到形体和机位对：`blender -b --python pipeline/preview.py -- <名> [--clay]`。
3. 放进城市：顶层地点在 `city.blend` 里加实例；依附别的地点的场景（酒馆后巷）在那个地点的 Prefab 里 instance，并把父 Prefab 的 `footprint`/`footprint_offset` 扩到能盖住它。
4. 整城构建与审阅：`./build.sh --no-publish --render --focus <名>`；Pan 机位再加 `--pan <名>` 看四角极限画面。新增或改动布局要用户确认整城预览。
5. 发布：`./build.sh`（构建 + 契约检查 + 原子覆盖 Unity `City.fbx` 与 `Places/*.fbx`，过期地点文件连 .meta 一起删）。

## 描线与外观

作者参数与组合约束的权威说明是 `city-box/docs/视觉参数.md`。先选表面预设、描线生成方式、阅读层级与装饰色，再调整地点光照、环境和机位；后端 BSDF/Shader 数值不放进每个物件的作者选项。只读检查用 CityBox `tools/visual_check.py`。

源 Prefab 声明 `surface` 材质角色、`tone` 描线主次和可选的 `line_palette`；Blender 材质供自身预览使用，不转换为 Unity 材质。源不保存描线。构建将真实模型生成 `描线_focus_<名>`，将 `outline="proxy"` 的手搭替身生成 `描线_world_<名>`，将填充建筑与基础设施生成 `描线_always_<类>`。`tone` 只决定亮暗。世界状态显示 world + always；聚焦状态显示该顶层地点的 focus + always，并显示它的 `内部_<名>`。逐建筑调参后运行 `./build.sh --no-publish --focus <名>` 检查游戏聚焦图；需要从全城机位检查近景线时运行 `./build.sh --no-publish --render game --inspect-outline <名>`。

## 发布后验证

1. `build/city/report.json` 的 `problems` 为空。
2. CityBox 与 Unity 两份 `City.fbx` SHA-256 相同，`.meta` GUID 未被替换；`Places/` 下文件集合与 `report.json` 的 `places` 一致。
3. Unity 重导入无契约错误；进入全城和受影响地点确认相机、主体和描线属于同一子树，世界视角下内部已隐藏。
4. `Main.unity` 的 City 实例只保留根节点变换，不对子对象保存覆盖。

坐标方向：Unity(x, y, z) = (−Bx, Bz, By)，实际世界单位乘 City 根的统一缩放；正式 Main 当前为 1。不要沿用旧试用工程的 0.1。

正式 Unity 截图使用 `python3 tools/citybox-unity-preview/preview.py --verify-play`；说明见工具 README。它读取正式 Main 与共享渲染资产，不读取 review 候选。Blender 个性化实验可保留，不要求两种渲染器像素一致。
