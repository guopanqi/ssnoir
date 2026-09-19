# 3D 资产运行时契约

本文件记录会跨越制作与运行时的稳定知识。状态为“已实现”的条目必须能指向代码消费者；没有消费者的命名只能算制作约定或提案。

## 已实现的通用契约

### 地点主键与整城装配

来源：`city-box/pipeline/build.py`、`city-box/pipeline/export.py`、`SSNoirModelImporter.cs`、`SceneDirectory.cs`

- Scheme 运行时 `GameNode.Name` 是地点的唯一主键。带 Anchor 的 Prefab 必须使用同一个精确名称派生正式文件与语义节点：`prefabs/<名>.blend`、整城中的子树根 `<名>`、`Anchor_<名>`、`Camera_<名>`。Prefab 可以嵌套（酒馆里放后巷），嵌套关系不需要镜像 Scheme 的树，锚点按名全局查找。
- 不为历史命名维护 alias 表。`剧院2`、`bar`、`老街酒吧` 之类的制作过程名必须在源资产处迁移成正式地点名。
- 行动 Anchor 默认使用它的 `GameNode.Name`；节点也可用 Scheme `:anchor` 显式声明不同的空间锚点名。
  Anchor 可以不建专属 Camera；导入器会在该建筑子树内回退到主相机。
- 纯视觉的 Prefab 不放 Anchor、Camera；不能用一个没有对应 Scheme 节点的假名字占位。
- CityBox 构建会拒绝缺少主 Anchor/Camera、重复锚点名或不符合上述规则的 Prefab，而不是悄悄退回灰盒或猜测名称。
- 发布器会静态检查每个 AnchorName 是否出现于当前 Scheme 字符串字面量中。资产与内容可以不同步到达，因此缺失只打印构建警告、不阻断 `City.fbx` 发布；这不是模糊匹配。Scheme 在运行时真正引用不存在的 Anchor 时，仍由 `SceneDirectory` 的精确契约 assert/throw。

正式 City 实例不得在 `Main.unity` 保存 FBX 子对象覆盖。`City.fbx` 每次完整重建，内部 fileID 不稳定；对子对象覆盖材质、名称、激活状态、Renderer、Camera 或 VCam 参数，会在下次导出后落到另一栋建筑。场景只保留 City 根节点的位置、旋转和统一缩放，子对象关系由导入器和运行时代码按名称建立。

整城 `City.fbx` 是一个发布产物，不是美术源文件。每个地点和场景以独立 Prefab `.blend` 维护；CityBox 负责装配、校验并生成 FBX，Unity 只消费固定路径的整城资产与语义节点。

CityBox 的 `build/city/report.json` 同时记录填充几何组和逐 Prefab 统计；性能判断以这份构建账本为准，不靠打开某次 FBX 后手工估算。

CityBox 的贴地层由构建在生成源头按 `郊野 0.00m / 城市地面 0.30m / 干道与无基座公园 0.65m / 街区顶面 1.05m` 分层。Unity 中 `City` 整体缩放为 `0.1`，不能恢复为原来的厘米级源间距，否则透视全城镜头下会出现 z-fighting。建筑、窗光、描线和语义节点必须通过宿主层级继承相同抬升，禁止在导出器或材质上单独追加深度偏移。

`CityOutlineState` 在运行时初始化唯一 `City` 根节点时，统一关闭整城子 Renderer 的 Cast Shadows，但不改 Receive Shadows。该规则不再由 ModelImporter 实现；导入的 FBX 上不挂 City 专用运行时脚本。

#### City 描线可见范围

城市描线只有 `focus`、`world`、`always` 三种可见范围。真实地点模型按所属地点相机标定，生成 `描线_focus_<名>`；手搭的 `outline="proxy"` 替身按世界相机标定，生成可选的 `描线_world_<名>`；填充建筑、屋顶小件和基础设施按世界相机标定，生成 `描线_always_<类>`。`tone = "bright" | "dim"` 只决定视觉层级，不参与显隐。

世界状态显示 world + always；呈现某个顶层地点内的相机时，显示该地点的 focus + always，并关闭该地点的 world 描线。顶层地点 Prefab 是唯一切换单位。Anchor 负责解析 Focus Camera，Camera 所属顶层地点决定描线状态；不根据距离、视锥或屏幕覆盖范围自动切换。

`City.fbx` 保存世界层、world/always 描线和语义对象；`Resources/City/Places/<名>.fbx` 保存每个顶层地点的 focus 描线与内部。`CityPlaces` 在启动时挂接地点细节，`CityOutlineState` 校验命名并执行状态表。

填充建筑按全城机位下的屏幕高度筛选，低于 `pipeline/outline.py` 中 `FILL_MIN_H_PX` 的建筑不生成 always 描线。背景线以全城共享的视觉和几何预算控制密度。

### Camera

来源：`UnityClient/Assets/Editor/SSNoirModelImporter.cs`、`UnityClient/Assets/Scripts/Runtime/SSNoirCameraManager.cs`

- 导入模型中的每个 Blender/FBX `Camera` 都会生成一个同级 Cinemachine Virtual Camera，名字为 `<原相机名>_VCam`。
- 导入器复制相机的局部位置、旋转、缩放、FOV、正交状态和正交尺寸，默认 Priority 为 5。
- CityBox 的正式交互地点相机统一使用透视投影（`PERSP`）；生产构建遇到 `ORTHO` 直接失败。
  正交相机只允许用于制作预览或整城审阅，不得随 `Camera_<地点>` 进入运行时。地点聚焦会从全局
  透视镜头连续推进到近景，混入正交投影会在运镜首帧造成不可插值的投影跳变。
- 正式交互地点相机统一使用 `50mm` 镜头（导入 Unity 后垂直 FOV 约 `27°`）。构图大小通过调整
  相机到 `orbit pivot` 的距离完成，不允许用不同焦距补构图；生产构建对焦距执行 `±0.01mm` 断言。
- Prefab 相机的 Near/Far 是米（资产单位）。导入器原样复制；`SSNoirVirtualCameraConfig.Awake` 按 `modelRoot.lossyScale` 缩到世界单位（City 实例 0.1）。
- Orbit 相机的 Far Clip 至少为导入资产空间中“相机到所属 orbit pivot 距离”的 `1.25` 倍，保证目标不会被远裁剪面切掉。运行时若 Far Clip 仍短于实际 pivot 距离，会 assert/throw，而不是显示空背景。
- Blender/FBX 相机定义的是最终落点镜头的 Near/Far Clip；导入器不把所有镜头强制成同一个 Near Clip。远景可以使用较大的 Near Clip 保住 WebGL 深度精度，近景则可以保留较小值避免裁掉前景。
- 地点聚焦过程会暂时把目标 VCam 移到当前渲染镜头的位置，因此把 Near/Far Clip 作为完整范围，从当前渲染值一起平滑过渡到资产定义的目标值，并在抵达或中断时一起恢复；不得在远景位置提前套用近景裁剪范围。
- 原 Camera 节点会被禁用，实际游戏视图使用生成的 VCam。
- 相机名应表达用途并保持稳定，例如 `Camera_公园`；不要保留无意义的 `Camera.001`。

### orbit pivot

来源：`SSNoirModelImporter.cs`、`SSNoirVirtualCameraConfig.cs`、`SSNoirCameraManager.cs`

- 规范名是 `OrbitPivot_<名>`（与 Anchor_/Camera_/PanBounds_ 同一套，整城唯一，不会被 Blender 加 `.001`）。导入器剥掉数字后缀、去掉空格/下划线/连字符后按“以 `orbitpivot` 开头”识别，所以独立资产里的 `orbit pivot` 也认。
- 机位类型由同根的对象决定，没有默认分支：有 `OrbitPivot_<名>` → Orbit；有 `PanBounds_<名>` → Pan；都没有 → Static；都有 → 导入失败。CityBox 侧要求相机显式声明 `drag = "orbit" | "pan" | "static"` 并在构建时校验与对象一致。
- pivot 是稳定的镜头旋转中心，不是交互卡片的位置。它通常放在资产视觉重心附近、略高于地面。
- 运行时配置自相矛盾时会 assert/throw。Pan 机位可带平移边界：Prefab 里一块贴地的框（`pan_bounds_for = "Camera_<名>"`，`preview_only`），构建时换成同根的 `PanBounds_<名>` Empty 进 FBX，角点是它的两个子 Empty `_min` / `_max`（用位置而不是 scale：FBX 导出把单位换算烘进 Prefab 根的 scale，子物体位置随之缩小、scale 不会）；导入器按角点位置算出模型空间 XZ 边界写进 `SSNoirVirtualCameraConfig.panBounds*`，运行时经 `modelRoot` 换算成世界值。

每个 Prefab 的 `OrbitPivot_<名>` 只配同根的 `Camera_<名>`；相机只在同根直接子级里找 pivot。

#### 相机与 pivot 必须对齐

来源：`SSNoirVirtualCameraConfig.ApplyOrbitFromDrag`（拖拽时 `LookAt(pivot)`）、
`SSNoirModelImporter.ConfigureDragMode`（按出厂机位撑开 pitch 区间）。

聚焦落点用的是相机自己的朝向，玩家一拖拽却改用绕 pivot 的姿态，两者不一致时画面会**跳**。所以出厂的
Orbit 相机必须满足：

- **相机看向自己的 orbit pivot**，偏离角 ≈ 0（容差 0.5°）。构图想让建筑偏离画面中心时，把 pivot 一起
  挪到相机真正的注视点，而不是让相机斜着看 pivot。
- pivot 放在建筑视觉重心：它既是旋转中心，也是玩家拖拽时的画面中心。
- **不要对建筑做非等比缩放**。缩放只改位置不改朝向，非等比就会把对准好的视线拧歪，拉得越狠歪得越多。
  CityBox 装配因此只做三轴等比，并在最后一步重新对准每台建筑相机。

俯角不必迁就默认的 `[minPitch, maxPitch]`（10°/25°）：导入器会把每台 Orbit 相机的 pitch 区间**撑到
包含出厂机位**（各留 1° 余量），出厂机位因此永远是合法的 orbit 位置，第一次拖拽不会被 clamp 弹。
默认带只是下限，机位本来就在带内时什么都不变。

检查与修复用 `scripts/align_camera_to_pivot.py`：默认只报告并以退出码 1 拦截；`--apply` 配合
`--fix pivot`（默认，把 pivot 滑到相机视轴上、构图不变）或 `--fix camera`（把相机转向 pivot、构图会变），
`--save` 才写回文件。该脚本复刻了导入器的“同根（同一父级）最近 pivot”绑定规则，因此报告里的配对与 Unity 实际绑定一致；出厂俯角在默认带外只作提示，不算错误。

### Anchor

来源：`SSNoirModelImporter.cs`、`NodeAnchor.cs`、`SceneDirectory.cs`、`IMGUIWorldRenderer.cs`

- 名字以 `anchor` 开头（忽略大小写）的 Transform 会自动获得 `NodeAnchor`。
- `NodeName` 字段实际保存的是**空间锚点名**：它是对象名第一个下划线之后的全部文字；规范形式为
  `Anchor_<锚点名>`。
- 节点不写 `:anchor` 时，运行时按 `GameNode.Name` 查找同名 Anchor；找不到就进入网格布局。普通情况
  因此仍可直接使用 `Anchor_公园`。
- 节点显式写 `:anchor` 时，运行时改查该值；找不到会 assert/throw，不能静默退回网格。移动人物可用
  `Anchor_夜莺@酒馆`、`Anchor_夜莺@剧院`，两个节点实例仍共享唯一身份名 `夜莺`。
- `@` 只用于同一角色或实体在不同地点的身份消歧，格式为 `<实体>@<地点>`。场所内的功能分区用
  `<地点>-<功能>`，例如 `Anchor_老街酒馆-购买`、`Anchor_老街酒馆-工作`；同一分区内的多个行动
  共用一个 Anchor，不能为每个动作各建一个 Anchor。
- Unity 用 Anchor 的世界坐标投射对应卡片；Anchor 应放在画面中适合悬挂卡片的位置，而不一定是几何中心。
- 语义对象的作用域就是它的父节点（所属 Prefab 的根），不向上爬：Anchor 在同根内优先绑定精确名称 `Camera_<锚点名>_VCam`，找不到时回退到同根的主相机 `Camera_<根名>_VCam`；相机只在同根直接子级里找 orbit pivot。嵌套 Prefab（酒馆里的后巷）因此各用各的相机与 pivot。
- `SceneDirectory` 以空间锚点名为字典键。重复锚点名会记录错误并 assert/throw，禁止在场景中创建重复 Anchor。
- 没有下划线或下划线后为空会得到空 NodeName，不会进入有效目录。
- **Stage 的身份是节点名，不是 `:anchor`**（`SSNoirGameManager.ResolveStageAnchor`）：导航路径里某个节点的
  `Anchor_<节点名>` 挂着 `StagePortalConfig`（由同名 `PortalIn_<名>` 导入时挂上），站在它里面就是在那个 Stage
  里，镜头取 `Anchor_<节点名>` 的根机位。`:anchor` 只决定这张门卡挂在城市里的哪儿：家的「租屋」卡挂 `门口`，
  门后的空间在郊野的 Stage 里，两者不可能同一个锚点。交锋根容器名就是场景名，对交锋这条规则没有新东西。

### 随卡显隐（`随卡_<锚点名>`）

来源：`CityOutlineState.cs`（解析与开关）、`SSNoirGameManager.cs`（每次快照落地喂当前锚点集）、
`city-box/pipeline/outline.py`（生成）、`city-box/pipeline/export.py`（校验）

买回来的家具这类"游戏状态决定在不在"的物件，不走独立状态通道，借渲染树：

- Blender 里给对象标 `presence = "<锚点名>"`。构建把同一锚点的件收进 `内部_<Prefab>/随卡_<锚点名>`
  节点，并单独出一份线 `描线_随卡_<Prefab>_<锚点名>` 挂在同一节点下；这些件不并进 `描线_focus_<名>`。
- 运行时规则只有一条：**当前渲染树里有任何节点的有效锚点（`:anchor`，缺省为节点名）等于
  `<锚点名>`，节点显示；否则隐藏。** 脚本"买了就多渲染一张挂在那个锚点上的卡"，模型就随之出现；
  读档天然对齐，存档不多一个字段。
- `随卡_` 必须直接挂在 `内部_<名>` 下（没聚焦时随内部一起隐藏），锚点必须是同一 Prefab 自己的
  `Anchor_<锚点名>`；构建校验这两条，Unity 侧再核一次父节点。
- 是 opt-in：没标 `presence` 的家具照旧常在（吧台没卡也得在）。
- 范例：`city-box/prefabs/src/租屋.py`（花盆随「看花」、唱片机随唱片那组卡）。

### 描边对象与材质

城市描边的唯一生产者是 CityBox 构建：生成 focus/world/always 描线并分别写入世界层和地点细节资产；Prefab 源 `.blend` 不保存描边。

非城市独立资产不得默认复用城市描边流程。只有找到明确运行时消费者时，才采用该消费者要求的对象名和材质名。例如 `AmbientBoat` 当前仍按材质名区分船体和线条，这只是车辆系统专用契约，不是所有资产的通用规范。

## 已实现的专用扩展

来源：`UnityClient/Assets/Scripts/Runtime/Environment/NeonSign/NeonSignFlicker.cs`

- `SparkPoint_*` Transform 会被霓虹灯故障组件发现并用作火花发射点。
- `Grp_NeonFlicker` 是老街酒馆专用的可闪烁灯管组，组件应挂到该组。

这些不是所有模型都必须具有的通用节点。只有资产使用对应运行时组件时才创建。

## 不存在的契约

当前没有运行时代码消费 `Actor_*`。不要主动创建，也不要声称它能生成角色或绑定剧情。确实需要角色站位时，应先实现并记录消费者，而不是把制作标记长期留在正式资产中。

## 新契约的准入规则

新增任何通过名字触发的行为时，同时完成：

1. 在 Unity 代码中集中实现解析，不把字符串判断散落到多个组件。
2. 对缺失、重复、格式错误给出明确错误；高风险配置按项目风格 assert。
3. 在本文件记录精确语法、大小写规则、坐标语义和消费者源码。
4. 扩展 `validate_blend_asset.py`，使制作阶段能提前发现错误。
5. 用一个真实资产完成 Unity 导入检查。
