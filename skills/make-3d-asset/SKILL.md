---
name: make-3d-asset
description: 为 SSNoir 制作、加工和修复单件 3D 资产，包括代码建模、已有模型修改、生成模型与拓扑清理。整体地标、室内或街区的设计、视觉组合及 CityBox 发布由 make-scene 负责。
---

# 制作单件 3D 资产

交付的是能放入地点或被既有运行时消费的单件模型：道具、雕塑、车辆、家具、人物等。先明确它在目标镜头中的职责、主导轮廓、尺寸、朝向和运动部件，再选择制作方式。地标建筑或整个地点的表达与验收从 [make-scene](../make-scene/SKILL.md) 进入；地点流程只在需要独立资产时调用本 Skill。

## 按形体选择制作路线

- **复用或修改已有模型**：先检查项目已有资产，保留已经成立的形体、比例与来源。
- **Python / Blender 建模**：适合几何关系清楚、需要精确尺寸或可重复调整的资产；代码既可做灰盒，也可生成最终几何。
- **生成服务**：适合需要特定雕塑感、复杂有机形体的模型。需要参考图时按 [视觉定义](references/visual-definition.md) 明确可建模形体，生图使用 [make-images](../make-images/SKILL.md)。当前默认低模服务见 [Tripo 选型](references/providers/tripo.md)，实际调用与鉴权查 `tools/tripo/README.md`。用户指定其他服务时，按相关工具 README 与对应 provider 参考执行。

这些路线可以混合，不要求每个资产先生成参考图再生成网格。不要为了遵循流程重做已有合格模型，也不要把代码模型默认当成只能丢弃的占位体。

## 形体、网格与加工

先在目标尺寸下审轮廓、体块和关键特征，再判断表面、面数与拓扑。检查标准见 [网格质量](references/mesh-generation.md)，适用于代码模型、外来模型与服务生成结果。生成成功或多边形数低，不等于形体合格；只有形体值得保留而拓扑不适用时，才考虑额外重拓扑。

按 [游戏化加工](references/asset-processing.md) 清理网格、统一米制尺度和朝向、应用 Rotation/Scale，并建立消费者确实需要的语义节点。保留来源和可复现脚本；候选放在制作目录或 `city-box/prefabs/review/`。完成几何变换后再处理描线。

表面与描线使用项目已有角色和作者声明，选项查 [CityBox 视觉参数](../../city-box/docs/视觉参数.md)。材质角色如何与整个地点的光、雾及注意力结合，由 make-scene 决定；不在本 Skill 维护另一份色盘或整场景配光流程。

## 交付与审核

- **地点内的单件资产**：把模型、尺寸/朝向、来源、几何统计及 3/4 预览交回 make-scene，由地点流程装配并走 [CityBox 交付](../make-scene/references/citybox-delivery.md)。共享库与场景私有资产的归属见 `city-box/props/README.md`。
- **非城市独立资产**：按既有消费者走 [独立资产交付](references/delivery/unity-standalone.md)，不另造运行时加载或发布方式。
- **涉及地点命名、Anchor、Camera 或显隐**：按 [运行时契约](../make-scene/references/runtime-contract.md) 执行，不把普通道具强制做成带相机的地点。

交付前先自审可见效果与网格健康，保留足以判断形体和表现的图片。需要用户选择最终造型、额外高模费用或正式采纳时展示具体候选；已有明确连续制作或发布授权时，不重复索取机械步骤确认。扩大费用或替换服务前确认是否仍在授权范围内。

## 检查工具

`skills/make-3d-asset/scripts/` 提供 `audit_mesh_quality.py`、`validate_blend_asset.py`、`align_camera_to_pivot.py`、`frame_and_render_preview.py`。按资产用途选择参数，不默认所有物件都要 Anchor、Camera 或描线。城市源保存净几何，构建生成描线，因此校验不使用 `--require-outline`。

日常形体、网格和动作迭代在 Blender 完成，后台网格检查不能代替视觉审阅。普通资产交付不自动启动 Unity；只有导出/导入契约、Unity 专有渲染改变、两端出现差异或用户明确要求时，才按 [验证边界](../../tools/VERIFYING.md#unity-验证边界) 做最小核对。若 Blender 正确而 Unity 不一致，按共同管线的功能缺陷定位，不靠逐轮启动 Unity 或修改正确资产绕过。
