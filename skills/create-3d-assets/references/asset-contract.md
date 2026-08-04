# Blender → Unity 资产契约

本文件记录会跨越制作与运行时的稳定知识。状态为“已实现”的条目必须能指向代码消费者；没有消费者的命名只能算制作约定或提案。

## 已实现的通用契约

### Camera

来源：`UnityClient/Assets/Editor/SSNoirModelImporter.cs`

- 导入模型中的每个 Blender/FBX `Camera` 都会生成一个同级 Cinemachine Virtual Camera，名字为 `<原相机名>_VCam`。
- 导入器复制相机的局部位置、旋转、缩放、FOV、正交状态和正交尺寸，默认 Priority 为 5。
- Near/Far clipping 会除以 100，以修正 Blender 导入尺度差异。
- 原 Camera 节点会被禁用，实际游戏视图使用生成的 VCam。
- 相机名应表达用途并保持稳定，例如 `Camera_公园`；不要保留无意义的 `Camera.001`。

### orbit pivot

来源：`SSNoirModelImporter.cs`、`SSNoirVirtualCameraConfig.cs`、`SSNoirCameraManager.cs`

- 导入器把移除空格、下划线和连字符后等于 `orbitpivot`（忽略大小写）的 Transform 识别为旋转中心。因此 `orbit pivot`、`orbit_pivot`、`Orbit-Pivot` 都有效。
- 每个相机绑定空间距离最近的 orbit pivot。
- 找到 pivot 时，相机拖拽模式为 Orbit；没有时为 Pan。
- pivot 是稳定的镜头旋转中心，不是交互卡片的位置。它通常放在资产视觉重心附近、略高于地面。
- Orbit 模式缺少 pivot 会被强制改为 Pan；运行时配置自相矛盾时会 assert/throw。

每个独立场景资产优先只放一个 `orbit pivot`。多相机、多 pivot 时必须检查“最近者”是否真是预期绑定。

### Anchor

来源：`SSNoirModelImporter.cs`、`NodeAnchor.cs`、`SceneDirectory.cs`、`IMGUIWorldRenderer.cs`

- 名字以 `anchor` 开头（忽略大小写）的 Transform 会自动获得 `NodeAnchor`。
- `NodeName` 是对象名第一个下划线之后的全部文字；规范形式为 `Anchor_<SCM 节点名>`。
- `NodeName` 必须与运行时 `GameNode.Name` 精确一致。中文可直接使用，例如 `Anchor_公园`。
- Unity 用 Anchor 的世界坐标投射对应卡片；Anchor 应放在画面中适合悬挂卡片的位置，而不一定是几何中心。
- 导入器优先绑定名字包含该 `NodeName` 的 VCam；找不到时回退到导入模型中的第一个 VCam并发出警告。因此推荐配对 `Anchor_公园` 与 `Camera_公园`。
- `SceneDirectory` 以 `NodeName` 为字典键。重复 NodeName 会静默覆盖，禁止在场景中创建重复 Anchor。
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
- `Grp_NeonFlicker` 是老街酒吧专用的可闪烁灯管组，组件应挂到该组。

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
