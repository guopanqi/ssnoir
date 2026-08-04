---
name: create-3d-assets
description: 使用固定职责链为 SSNoir 制作 3D 资产：参考图阶段由 Codex 撰写和迭代提示词并操作网页、Gemini AI Studio 实际生图；随后由 Codex 操作指定的 Hunyuan 3D Studio 生成模型与低模拓扑，在 Blender 加工，最终交付 Unity Resources。制作或修改参考图提示词、Hunyuan 模型、低模四边面、.blend/.fbx、模型相机、Anchor、orbit pivot、Actor 标记、描边材质、预览图或 Resources/Models 下的资产，以及维护相关艺术基调和 Unity 命名契约时使用。
---

# 制作 SSNoir 3D 资产

把模型视为同时包含几何、视觉处理和运行时语义的资产，不把 Blender 文件当作只有网格的中间产物。保留人工确认关口；未经用户确认，不跨越参考图、原始模型、低模和视觉加工四个阶段。单独新增或调整 Anchor、orbit pivot、Actor 标记等不可见语义节点不构成视觉加工阶段，可以在用户授权后直接修改并做契约验证。

严格区分“Codex 负责制作提示词”和“Gemini 负责生成图片”。除非用户明确改变工具链，不调用 Codex `$imagegen`、`image_gen` 或其他生图能力代替 Gemini，也不自行搜索或选择其他同名 3D 产品代替指定的 Hunyuan 3D Studio。

## 开始前

1. 读取 [references/asset-contract.md](references/asset-contract.md)，确认当前项目真正消费的命名、节点和材质契约。
2. 读取 [references/workflow.md](references/workflow.md)，按当前阶段继续，不重复已经确认的阶段。
3. 生成或修改参考图时，读取 [references/prompt-and-era.md](references/prompt-and-era.md)。
4. 进入 Blender 加工时，读取 [references/blender-processing.md](references/blender-processing.md)。
5. 保存到 Unity 或检查导入结果时，读取 [references/unity-delivery.md](references/unity-delivery.md)。
6. 读取目标目录内的 `AGENTS.md`，修改项目内容后按 `skills/verify/SKILL.md` 选择验证。
7. 用户反馈若形成可跨资产复用的艺术基调、年代约束、提示词模板或失败规避经验，立即更新对应 Reference；仅针对当前模型的局部修改不要写入 Skill。

## 核心工作流

1. **定义资产契约**：明确用途、时代、尺寸、分类、可交互节点、角色站位、相机行为和面数预算。区分已经有 Unity 消费者的契约与仅供制作使用的标记。
2. **撰写并提交提示词**：Codex 根据需求撰写完整提示词，通过已登录浏览器提交给 Gemini AI Studio；当前优先使用 Nano Banana Pro。图片必须由 Gemini 生成。先生成单体完整构图；当前阶段不做多图拼接。
3. **等待参考图确认**：提供 Gemini 生成的原图或清晰截图；只按反馈修改提示词或要求 Gemini 编辑图片，不提前生成模型。
4. **生成原始模型**：把用户确认的 Gemini 图片提交到指定的 Hunyuan 3D Studio Geo。优先速度和可辨识轮廓，选择尽可能低但能通过生成器校验的面数。
5. **等待原始模型确认**：用能说明体块、遮挡和圆弧质量的 3/4 视角提供预览。
6. **低模拓扑**：在指定的 Hunyuan 3D Studio Poly 中从最小档开始；优先四边面，但可按资产用途选择四边面、三角面或可解释的混合拓扑。若主要质量目标未通过，只上调一级，避免无依据地提高面数。
7. **等待低模确认**：再次提供合适角度的预览；详情页与普通截图都可以，信息清楚比界面形式重要。
8. **Blender 加工**：统一尺度和朝向，应用几何变换，运行项目描边脚本，建立相机、交互 Anchor、orbit pivot 及已定义的扩展标记。
9. **等待视觉加工确认**：几何、描边、材质、尺度、朝向或镜头构图发生可见变化时，渲染最终工作视角；用户确认前不写入正式 Unity 资源目录。仅修改不可见语义节点时跳过预览，以对象清单和契约校验代替。
10. **交付 Unity**：按资产职责选择目录和稳定名称，保存 `.blend`，让 Unity 生成 `.meta`，然后检查导入器生成的组件和相机行为。

用户说“下一步”只授权跨越当前已展示的一个确认关口，不代表一次性授权余下所有阶段。

## 工具链边界

准确入口和当前模型偏好见 [references/workflow.md](references/workflow.md)。模型版本可以写成“当前优先”，但服务职责不可由 Agent 自行替换：

- Codex/GPT：撰写、审阅和迭代提示词；操作浏览器；自身不生成参考图。
- Gemini AI Studio：生成和编辑参考图。
- Hunyuan 3D Studio Geo/Poly：分别生成模型和低模拓扑。
- Blender：尺寸、拓扑检查、描边、相机和语义节点加工。
- Unity：导入正式资源并验证运行时契约。

优先复用已登录的 in-app Browser/Chrome 会话。若没有对应标签页，直接导航到 Reference 中的准确 URL，不用搜索引擎寻找替代网站。指定服务不可访问、未登录或界面不存在时，报告阻塞并等待用户处理；不得无提示地改用 Codex ImageGen、其他图片服务或另一个“混元”产品。

只有用户明确要求更换工具链时才可以替换服务；替换后仍保留相同的阶段产物、确认关口和资产契约。用户要求快速时，优先直接截图或导出当前预览，不为“打开详情页”增加无价值步骤。

## 维护跨阶段知识

任何 Unity 代码若通过对象名、材质名、层级或文件目录触发行为，必须在同一次改动中更新 [references/asset-contract.md](references/asset-contract.md)。反过来，不能仅凭 Blender 中出现了某个名字，就宣称 Unity 已支持它；先找到或实现消费者。

把用户已确认且能够指导后续多个资产的提示词经验写入 [references/prompt-and-era.md](references/prompt-and-era.md)。直接修订现有规则，删除已经被推翻的说法，避免追加互相冲突的历史版本。不要为 Skill 另建资产状态机、提示词版本日志或聊天档案；使用 Git 查看 Reference 的修改历史。

把契约分成三种状态：

- **已实现**：能指向读取它的 Unity/Blender 源码。
- **制作约定**：帮助组织文件，但当前不自动触发运行时行为。
- **提案**：准备引入但尚无消费者；不得作为已生效功能交付。

## 自动化

用 Blender 自带 Python 运行脚本：

```bash
blender --background path/to/model.blend \
  --python skills/create-3d-assets/scripts/validate_blend_asset.py -- \
  --require-camera --require-outline --require-orbit-pivot
```

检查（默认）或修正相机与 orbit pivot 的对齐，整城资产同样适用：

```bash
blender --background path/to/model.blend \
  --python skills/create-3d-assets/scripts/align_camera_to_pivot.py -- \
  --apply --save
```

生成并可选保存统一的正交 3/4 预览相机：

```bash
blender --background path/to/model.blend \
  --python skills/create-3d-assets/scripts/frame_and_render_preview.py -- \
  --output /absolute/path/preview.png --camera-name Camera_资产名 --save-camera
```

脚本只覆盖机械检查和稳定取景，不能替代对年代、轮廓、遮挡、圆弧拓扑和游戏用途的人工判断。
