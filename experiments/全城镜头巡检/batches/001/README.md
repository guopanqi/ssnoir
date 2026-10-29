# 001：码头与独立 Stage 代表样本

## 结论

- **城市样本「世界 ↔ 码头」**：完整装配内的两条有向路线都由共享 `FocusTravelMath` 选为 `AuthoredArc`，2 秒；机身与近裁切面采样均未检出网格穿插。`Anchor_码头` 的点投影在起终与 25% 间隔样本内都留在画面中。此为 authored center 的离线几何与投影检查，不等于 Main 首帧或实际视觉验收。
- **独立 Stage「别给他们想要的」内部样本**：3 台相机产生 6 条有向候选路线，修正检查工具作用域后都由共享策略选为 `DirectApproach`，未检出机身/近裁切面穿插。脚本确实引用了「喊的人」与「前排工人」两台演出机位；受影响节点的运行时真实触发顺序、端点画面和遮挡仍待画面/交互核对。
- **Main 实际初始 pose：待确认。** 代码显示默认起始地点是 `world`，加载后通过 `Anchor_世界` 解析世界相机；本轮没有启动 Unity 或读取 Main 的运行中相机，因此没有证明 Main 初始渲染 pose 与 Blender `Camera_世界` 相等。运行时聚焦从当前渲染相机的变换起步；玩家拖动后会与 authored center 不同。
- **Stage Portal：仅核对实现，未做空间/几何运行检查。** 进出 Portal 的首尾不是内部 FocusTravel 路由；本批不能据 Stage 内部曲线推断穿门路径安全。

## 范围、来源与快照

只选一处城市顶层地点和一处独立 Stage，没有使用孤立 Prefab 伪装世界到地点转换。两组检查都从 `city-box/build/city/city_build.blend` 完整装配读取；在 Blender 内存中筛掉未选 `Camera_*`，保留其余完整场景、位置、集合实例和网格，不保存改过的 `.blend`。第一组仅保留 `Camera_世界`、`Camera_码头`；第二组仅保留 Stage 根机位、`-叫喊`、`-退让` 三台机位。详细的运行相机清单见 `execution.json`。

| 输入 | 时间（本机文件） | SHA-256 |
|---|---:|---|
| `city-box/build/city/city_build.blend` | 2026-10-29 13:07:58 | `2acf1f5d474595aca5798d3e64f1f312d489a4c5c05bf3a581f50a9113ef9d61` |
| `city-box/city.blend` | 2026-10-28 17:51:26 | `6f428e90db613a2658f9a5c97fd727bb4a5d03051bad4c8008d40c7f1fe75e77` |
| `city-box/prefabs/世界.blend` | 2026-10-23 17:54:57 | `9e8adb625a86e4176bae8984761c972508be77b6005e2179a1f8ddac694c4d41` |
| `city-box/prefabs/码头.blend` | 2026-10-23 13:14:58 | `f4863b0c1a25ea78673ee6557f89ccea7b18d6333f33c369a3fed1cc669cb137` |
| `city-box/prefabs/别给他们想要的.blend` | 2026-10-28 17:53:32 | `610386f48d1a51a337767810d8b1e67b749162625422b85aae53f71fb17169c4` |
| `city-box/build/city/City.fbx` 与 Unity `Models/Environment/City.fbx` | 2026-10-29 13:08:22 | 两文件相同：`94eb88bdc7127830d8aa22dfddfd6cab053fc4563e858b75a262979250de1f9f` |
| `build/city/places/码头.fbx` 与 Unity `City/Places/码头.fbx` | 2026-10-29 12:05:39 | 两文件相同：`87d2a4ea48946f7c046c503454a545d71d6535c166053ef9b8781129fc221c2f` |
| `build/city/places/别给他们想要的.fbx` 与 Unity `City/Places/别给他们想要的.fbx` | 2026-10-29 12:02:41 | 两文件相同：`64b6c39b415075dc41d10badfe325f8bdb00ebb0d5afa09c0341fd4a7366d94a` |

完整装配及三个发布 FBX 与当前 Unity 资源逐字节相同。Blender 5.2.2 LTS；共享数学源码及首轮工具哈希记录在 `execution.json`。首轮 review 工具当时的 SHA-256 为 `e4c281b5908eba262922c0a1207d19cb42572a42f8f1a6a5091a74bfd78356cc`。

## 世界 ↔ 码头

机位数值均为工具映射到运行时世界轴后的米制坐标；FOV 为垂直角，画面投影用 16:9。相机的 Forward 是归一化方向向量。

| 机位 | 模式 | 位置 (x,y,z) | Forward | FOV | Near |
|---|---|---|---|---:|---:|
| `Camera_世界` | Pan | `(1040, 960, 110)` | `(-0.678, -0.732, -0.069)` | 26.99° | 10.0 |
| `Camera_码头` | Pan | `(-34.629, 80.650, 138.783)` | `(0.657, -0.751, 0.058)` | 26.99° | 0.5 |

两台机位都没有显式 `FocusTarget`；共享解析把 Pan 视线与 y=0 地面求交，起点/终点兴趣点相同，为 `(35.927, 0, 144.981)`。这只是推断点，不作为主体真实位置。实际 `Anchor_码头` 在映射坐标 `(29.359, 3.650, 144.918)`，距推断兴趣点约 7.5m。

`Camera_世界 → Camera_码头` 的采样位置如下；Anchor 投影为归一化画面坐标（画面中心 `(0,0)`、边缘约 `±1`）：

| 进度 | 相机位置 | Anchor 画面坐标 | 到相机前向深度 |
|---:|---|---|---:|
| 0% | `(1040, 960, 110)` | `(-0.23, 0.25)` | 1383m |
| 25% | `(777.88, 833.26, -266.70)` | `(-0.19, 0.22)` | 1185m |
| 50% | `(49.60, 540.25, -372.85)` | `(-0.09, 0.13)` | 745m |
| 75% | `(-138.83, 228.39, 35.57)` | `(-0.01, 0.01)` | 301m |
| 100% | `(-34.629, 80.650, 138.783)` | `(0.01, -0.10)` | 100m |

反向路线的位置采样与此表时间反转一致。共享路线是 `AuthoredArc`，主体锚点点位在抽样的五个位置都位于视锥内。完整装配评估 6658 个 mesh；两条路由均为 0 条中心线命中、0 条近裁切面命中。该判断覆盖采样到的几何，不是连续碰撞证明；Anchor 点投影不代表建筑轮廓、人物或遮挡已通过视觉审片。

## Stage 内部机位

| 机位 | 模式 | 位置 (x,y,z) | Forward | FOV | Near |
|---|---|---|---|---:|---:|
| `Camera_别给他们想要的` | Pan | `(-609.700, 13.5, -930.500)` | `(-0.004, -0.492, -0.870)` | 26.99° | 0.3 |
| `Camera_别给他们想要的-叫喊` | Static | `(-609.600, 3.2, -944.300)` | `(0.316, -0.331, -0.889)` | 26.99° | 0.1 |
| `Camera_别给他们想要的-退让` | Static | `(-609.200, 4.0, -942.800)` | `(0.408, -0.383, -0.829)` | 26.99° | 0.1 |

三台相机都没有显式焦点目标；共享解析用地面 y=0 作为兴趣点。建模对象点位表明「喊的人」身体中心约 `(-607.6, 0.89, -950.2)`，「前排工人」身体中心约 `(-606.6, 0.89, -948.6)`。在 `-叫喊` 终点，两者投影约 `(-0.03,-0.07)`、`(0.59,-0.38)`；在 `-退让` 终点，分别约 `(-0.54,-0.03)`、`(-0.08,-0.26)`。点位数值均在画面内，但没有审阅对应渲染帧，也没有计算完整人物轮廓、群众遮挡或动画姿势中的变化。

通过共享策略的无通行盒基线，六条有向候选路径均为 `DirectApproach`、0.9 秒；中心线和近裁切面穿插均为 0。当前可在 `stage_internal_no_travel_space/` 查看完整数据。六条是三台相机的全有向组合，不宣称都由内容实际触发。Scheme 的 `on-scenario-enter` 确实依次调用 `-叫喊` 与 `-退让` 两台机位，后续状态还会调用对应机位；每种运行时状态、路径与主体最终画面仍待实机/渲染逐条确认。

### 检查工具误报与修复复核

首轮默认输出 `stage_internal/report.json` 的六条 Stage 路由曾全部标为 `crossfade`。原因是当时的 `review.py` 在相机所在的全局 `Scene Collection` 里找任意 `CameraTravelBounds_*`，误把 `CameraTravelBounds_巷子里在打人`（完整装配中心 Blender 坐标约 `(210,950,0)`）套给相隔约 400m 的本 Stage 相机。Stage 同父节点下没有自己的 `CameraTravelBounds_*`，Unity importer 也只会查相机同父级。首轮因此没有将默认 crossfade 当成运行时行为；使用项目工具 `--without-travel-space` 的诊断基线重新采样。

主任务已修复工具为按相机同父级检索，并在 `../001-fixed/` 复跑。本轮 001 的无通行盒 Stage 路由与修复后的 `001-fixed/stage_internal/routes.json` 完全相等，复核为 6 路由均无 dissolve、无几何命中。修的是审查器作用域误报，不是正式镜头或场景资产。

## 运行时边界与流程问题

- `SSNoirGameManager` 启动代码默认 `startingLocation = "world"`，加载后从 `Anchor_世界` 解析世界相机。它证明了预期选择路径；没有 Main 的运行中实际 pose、相机导入元数据和首帧来证明与 Blender author pose 完全一致。把本路由称作 **authored world-center 候选**。
- `SSNoirCameraManager.BeginFocusTravel` 从当前渲染相机变换开始，目标常规焦点机位使用作者 pose。这里的源点固定为 `Camera_世界` 中心位置；任意 world Pan 偏移起点不在样本内。
- 运行时 `CityPlaces` 会把地点细节作为 City 子节点加载；World 状态下的 `interior` 隐藏、Stage 聚焦后的场景显隐、Portal 黑场和活动机位未在该 Blender 几何循环中复刻。完整装配评估 mesh 不等于准确的运行时可见集合。本批没有静态渲染截图，没有视觉通过结论。
- `StageTransitionController` 使用独立 Portal 控制进出：城市侧先从当前画面行进到 `PortalIn`，门前最后 1.5m 进入黑场；Stage 侧从根相机后方 `0.35 × |Camera_Stage − Anchor_Stage|` 揭幕并落入根机位。此处只读了代码，未按 `PortalIn`、实际 Stage Anchor 和 Main 的当前世界机位采样前后半程，也没有核门洞遮挡/净空。
- 首轮共享工具通过临时内存相机筛选解决“工具入口没有相机过滤”问题，未扩大公共 CLI；总评估集合每轮保留 6658 个 mesh。当前批次的流程瓶颈是运行时可见性与 Main 当前 pose 不在离线工具输入里，另有 Portal 是独立路径，需要分别补上来源/证据，不能强行合并成一条 FocusTravel 曲线。

## 下一步建议

下一批选一个摄像机覆盖多个主体的普通城市地点，先从 Main 代码/配置确定 authored world center 的尺度映射，并补一张 Blender 聚焦终点图。另选一个独立 Stage，将 PortalIn→Stage 根机位代码中的半程参数与两个同装配端点位置对照；不用启动 Unity。之后再针对世界 Pan 起点任选一个有证据的边界偏移样本，标明它只是代表拖动状态。

本轮只建议修检查工具作用域；不建议改码头、Stage 建筑或焦点配置。若后续画面确认人物主体在途中偏移，再按“焦点解析 → 机位”顺序处理；目前没有证据要求改建筑。

## 命令与耗时

完整两组检查命令：

```sh
/opt/homebrew/bin/blender -b city-box/build/city/city_build.blend --python-exit-code 1 --python experiments/全城镜头巡检/batches/001/run_selected_reviews.py -- --batch experiments/全城镜头巡检/batches/001
```

命令墙钟约 22 秒；相机筛选约 0.345 秒；其中世界/码头 `review.py` 约 5.65 秒，Stage 全部三机位组合约 11.76 秒。首次 Blender 装载与 Stage 轮次重载包含在命令耗时内。首轮发现误报后另跑的基线命令：

```sh
/opt/homebrew/bin/blender -b city-box/build/city/city_build.blend --python-exit-code 1 --python experiments/全城镜头巡检/batches/001/run_stage_baseline.py -- --batch experiments/全城镜头巡检/batches/001
```

无通行盒基线约 11.30 秒。两次命令均使用 Blender 后台与现有共享 .NET 9 工具，没有启动 Unity、没有全城重建。随后主任务以修复后的工具运行 `001-fixed`，报告见 `../001-fixed/README.md`。

## 文件

- `run_selected_reviews.py`：完整装配中内存筛选指定相机并运行两组检查。
- `run_stage_baseline.py`：只对选定 Stage 运行工具提供的无通行盒诊断基线。
- `world_to_harbor/`、`stage_internal/`：首轮 cameras/routes/geometry 数据。Stage 默认输出保留为误报诊断证据。
- `stage_internal_no_travel_space/`：Stage 内部实际策略候选的无通行盒基线数据。
- `execution.json`、`stage_baseline_execution.json`：版本、hash、耗时和输入清单。
