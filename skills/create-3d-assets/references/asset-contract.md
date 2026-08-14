# Blender → Unity 资产契约

本文件记录会跨越制作与运行时的稳定知识。状态为“已实现”的条目必须能指向代码消费者；没有消费者的命名只能算制作约定或提案。

## 已实现的通用契约

### 地点主键与整城装配

来源：`city-box/city/build_city.py`、`city-box/city/export_unity.py`、`SSNoirModelImporter.cs`、`SceneDirectory.cs`

- Scheme 运行时 `GameNode.Name` 是地点的唯一主键。交互建筑必须使用同一个精确名称派生正式文件与语义节点：`<地点>.blend`、整城中的建筑根节点 `<地点>`、`Anchor_<地点>`、`Camera_<地点>`。
- 不为历史命名维护 alias 表。`剧院2`、`bar`、`老街酒吧` 之类的制作过程名必须在源资产处迁移成正式地点名。
- 行动 Anchor 默认使用它的 `GameNode.Name`；节点也可用 Scheme `:anchor` 显式声明不同的空间锚点名。
  Anchor 可以不建专属 Camera；导入器会在该建筑子树内回退到主相机。
- 纯视觉地标必须被显式标记为非交互。非交互地标不导出 Anchor、Camera 或 orbit pivot，不能用一个没有对应 Scheme 节点的假名字占位。
- CityBox 的生产构建会拒绝缺少同名模型、缺少主 Anchor/Camera、重复 NodeName 或不符合上述规则的资产，而不是悄悄退回灰盒或猜测名称。
- 发布器会静态检查每个 AnchorName 是否出现于当前 Scheme 字符串字面量中。资产与内容可以不同步到达，因此缺失只打印构建警告、不阻断 `City.fbx` 发布；这不是模糊匹配。Scheme 在运行时真正引用不存在的 Anchor 时，仍由 `SceneDirectory` 的精确契约 assert/throw。

整城 `City.fbx` 是一个发布产物，不是美术源文件。重要建筑仍以独立 `.blend` 维护；CityBox 负责装配、校验并生成 FBX，Unity 只消费固定路径的整城资产与语义节点。

CityBox 的 `city_report.json` 同时记录最终导出几何的总量、程序化集合和逐重要建筑统计；性能判断以这份构建账本为准，不靠打开某次 FBX 后手工估算。

CityBox 的贴地层由 `build_city.py` 在生成源头按 `郊野 0.00m / 城市地面 0.30m / 干道与无基座公园 0.65m / 街区顶面 1.05m` 分层。Unity 中 `City` 整体缩放为 `0.1`，不能恢复为原来的厘米级源间距，否则透视全城镜头下会出现 z-fighting。建筑、窗光、Low/High 描线和语义节点必须通过宿主层级继承相同抬升，禁止在导出器或材质上单独追加深度偏移。

`CityOutlineState` 在运行时初始化唯一 `City` 根节点时，统一关闭整城子 Renderer 的 Cast Shadows，但不改 Receive Shadows。该规则不再由 ModelImporter 实现；导入的 FBX 上不挂 City 专用运行时脚本。

#### City Low / High 描线

`build_city.py` 从同一批模型和 `HERO_SLOTS` 单次生成 `city_build.blend`。每栋重要建筑的本体、Anchor、Camera 和 orbit pivot 只有一份，同时生成两个独立描线 Mesh：`描线_<地点>_Low` 按全城视野标定，`描线_<地点>_High` 按该建筑聚焦相机标定。普通填充建筑和基础设施只生成 Low 描线。

重要建筑的 Low 用**结构勾勒**生成（`tools/structure_outline.py`），不是 High 的删减版：先把建筑读成若干个「大面」（墙、屋顶、退台），窗洞和线脚这类贴在墙上的小起伏并进它所属的大面，只画大面之间的交界，再用 Douglas-Peucker 把交界拉直成几笔。所有判据按全城机位的屏幕像素标定。少数资产描述的不是一栋楼而是一片街区（`码头`、`码头居民区`），按 `LOW_STRUCTURE_BY_HERO` 用更粗的粒度；这是对象尺度不同，不是逐资产打补丁。`hard_edges` 作为对照路径保留在 `--low-method`。

填充建筑的描线按体量筛：只有在全城机位下屏幕高度达到 `FILL_OUTLINE_MIN_H_PX` 的才生成描线，低矮的那批完全没有线，靠自身明暗和雾读出来 —— 这是有意的，不是漏生成。描线预算属于全城共享：填充侧省下来的直接还给重要建筑的 Low 档。

`export_unity.py` 校验每个重要建筑恰好有一个 Low 和一个 High，然后把建筑与两档描线一起写入唯一 `City.fbx`。不再发布外置高精描线目录，也不在运行时另行加载描线资源。`CityOutlineState` 一次扫描 City 层级建立聚焦相机到 Low / High Renderer 的对应；常态开 Low 关 High，聚焦建筑时只对该建筑开 High 关 Low。Low / High 使用同一个 `M_White_Emission_Lines` 材质，不设置独立发光参数；两档只在描线几何密度和线宽上存在差异。

### Camera

来源：`UnityClient/Assets/Editor/SSNoirModelImporter.cs`、`UnityClient/Assets/Scripts/Runtime/SSNoirCameraManager.cs`

- 导入模型中的每个 Blender/FBX `Camera` 都会生成一个同级 Cinemachine Virtual Camera，名字为 `<原相机名>_VCam`。
- 导入器复制相机的局部位置、旋转、缩放、FOV、正交状态和正交尺寸，默认 Priority 为 5。
- Unity 直接导入 `.blend` 时，Near/Far clipping 乘 `0.01` 修正厘米尺度；CityBox 导出的 `.fbx` 已经是正确单位，不再重复除以 100。
- Orbit 相机的 Far Clip 至少为导入资产空间中“相机到所属 orbit pivot 距离”的 `1.25` 倍，保证目标不会被远裁剪面切掉。运行时若 Far Clip 仍短于实际 pivot 距离，会 assert/throw，而不是显示空背景。
- Blender/FBX 相机定义的是最终落点镜头的 Near/Far Clip；导入器不把所有镜头强制成同一个 Near Clip。远景可以使用较大的 Near Clip 保住 WebGL 深度精度，近景则可以保留较小值避免裁掉前景。
- 地点聚焦过程会暂时把目标 VCam 移到当前渲染镜头的位置，因此把 Near/Far Clip 作为完整范围，从当前渲染值一起平滑过渡到资产定义的目标值，并在抵达或中断时一起恢复；不得在远景位置提前套用近景裁剪范围。
- 原 Camera 节点会被禁用，实际游戏视图使用生成的 VCam。
- 相机名应表达用途并保持稳定，例如 `Camera_公园`；不要保留无意义的 `Camera.001`。

### orbit pivot

来源：`SSNoirModelImporter.cs`、`SSNoirVirtualCameraConfig.cs`、`SSNoirCameraManager.cs`

- 导入器先剥掉 Blender 合并同名对象时产生的末尾数字后缀（例如 `.001`），再把移除空格、下划线和连字符后等于 `orbitpivot`（忽略大小写）的 Transform 识别为旋转中心。因此 `orbit pivot`、`orbit_pivot.003`、`Orbit-Pivot` 都有效。
- 每个相机只在最近的资产子树内绑定空间距离最近的 orbit pivot；不会跨到整城另一栋建筑。单体资产只有一个 pivot 时允许全资产唯一回退。
- 找到 pivot 时，相机拖拽模式为 Orbit；没有时为 Pan。
- pivot 是稳定的镜头旋转中心，不是交互卡片的位置。它通常放在资产视觉重心附近、略高于地面。
- Orbit 模式缺少 pivot 会被强制改为 Pan；运行时配置自相矛盾时会 assert/throw。

每个独立场景资产优先只放一个 `orbit pivot`。多相机、多 pivot 时必须检查“最近者”是否真是预期绑定。

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
`--save` 才写回文件。该脚本复刻了导入器的“子树内最近 pivot”绑定规则，因此报告里的配对与 Unity 实际绑定一致。

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
- 导入器在 Anchor 所属的最近资产子树内优先绑定精确名称 `Camera_<锚点名>_VCam`；行动点找不到专属相机时回退到同一子树里的主 VCam，绝不回退到整城第一个 VCam。
- `SceneDirectory` 以空间锚点名为字典键。重复锚点名会记录错误并 assert/throw，禁止在场景中创建重复 Anchor。
- 没有下划线或下划线后为空会得到空 NodeName，不会进入有效目录。

### 描边对象与材质

来源：`UnityClient/Assets/Resources/Tools/model-outline-handpaint.py`、`model-outline.py`；船只淡出兼容见 `AmbientBoat.cs`

- 项目描边脚本生成 `OutlineLines_*` Curve，使用 `M_White_Emission_Lines`。
- 主体材质统一为 `M_Dark_Blue_Model`，颜色 `#0A142A`。
- `AmbientBoat` 通过材质名识别主体和线条；线材质至少应保留 `M_White_Emission_Lines`。对象名保留 `OutlineLines_*` 也有利于检查与其他兼容逻辑。
- 描边脚本会清空目标 Mesh 的原材质槽，并写入项目主体材质；运行前必须确认这正是所需视觉结果。

## 已实现的专用扩展

来源：`UnityClient/Assets/Scripts/Runtime/Environment/NeonSign/NeonSignFlicker.cs`

- `SparkPoint_*` Transform 会被霓虹灯故障组件发现并用作火花发射点。
- `Grp_NeonFlicker` 是老街酒馆专用的可闪烁灯管组，组件应挂到该组。

这些不是所有模型都必须具有的通用节点。只有资产使用对应运行时组件时才创建。

## Actor 标记：制作约定，尚非运行时契约

当前仓库没有发现模型导入器或运行时代码读取 `Actor_<名字>` Transform。不要声称仅靠命名就会生成角色或绑定剧情。

需要在模型里预留角色站位时，可使用 `Actor_<稳定角色ID>` Empty 作为制作约定，并遵守：

- 使用内容数据中的稳定 ID，不以可能变化的显示名替代 ID。
- Empty 的位置代表脚底落点，局部 Z 轴向上，局部 Y 轴表示建议朝向。
- 同一资产内 ID 唯一。
- 在 Unity 消费者实现前，把它当作人工参考点。

一旦实现 Unity 消费者，必须把前缀、ID 匹配、朝向、缺失/重复行为和源码路径补充到本文件，并把状态改为“已实现”。

## 新契约的准入规则

新增任何通过名字触发的行为时，同时完成：

1. 在 Unity 代码中集中实现解析，不把字符串判断散落到多个组件。
2. 对缺失、重复、格式错误给出明确错误；高风险配置按项目风格 assert。
3. 在本文件记录精确语法、大小写规则、坐标语义和消费者源码。
4. 扩展 `validate_blend_asset.py`，使制作阶段能提前发现错误。
5. 用一个真实资产完成 Unity 导入检查。
