---
name: create-3d-assets
description: 为 SSNoir 制作、接入或修复 3D 资产。覆盖 Gemini 参考图、Hunyuan Geo/Poly、Blender 几何与语义节点、CityBox 城市装配、Unity 导入和运行画面验证；涉及地点建筑、City.fbx、Anchor、Camera、orbit pivot、描线或资产预览时使用。
---

# SSNoir 3D 资产

资产同时包含几何、镜头和运行时语义。先判断资产属于哪条发布链，不允许把城市地点建筑直接塞进 Unity，也不把普通独立资产绕进 CityBox。

## 先路由

- **城市地点建筑**：正式源是 `city-box/models/<地点>.blend`；城市位置由 `city-box/city/build_city.py` 的 `HERO_SLOTS` 定义；Unity 只消费 `Assets/Resources/Models/Environment/City.fbx`。完整流程见 [references/citybox-delivery.md](references/citybox-delivery.md)。
- **非城市独立资产**：道具、车辆或不属于整城的独立环境，才按职责放入 `UnityClient/Assets/Resources/Models/`。见 [references/unity-delivery.md](references/unity-delivery.md)。
- **候选或废案**：放入 `city-box/models/review/`，不参与生产构建，也不由 Unity 直接引用。

涉及命名、Anchor、Camera 或层级时读取 [references/asset-contract.md](references/asset-contract.md)。进入 Blender 时读取 [references/blender-processing.md](references/blender-processing.md)。生成式网格质量不确定时读取 [references/mesh-quality-baseline.md](references/mesh-quality-baseline.md)。

## 制作链

1. 明确资产职责、正式地点名、目标尺寸、城市位置、交互节点、镜头和面数预算。城市地点名以 Scheme `GameNode.Name` 为唯一主键。
2. Codex 撰写提示词并用 Gemini Images 生成参考图；生成提示见 [references/prompt-and-era.md](references/prompt-and-era.md)，网页操作见 [references/workflow.md](references/workflow.md)。
3. 返回参考图和审阅结论。用户确认后才上传 Hunyuan Geo。
4. 返回 Geo 模型的 3/4 预览。用户确认后进入 Poly。
5. Poly 从最低档开始。轮廓或主要硬表面因预算失败时直接升一档，不为机械升档询问用户；返回低模预览和统计。
6. Blender 中清理网格、统一米制尺度与朝向、应用 Rotation/Scale，并建立正式语义节点。城市源模型不预生成最终描线；Low/High 描线由 CityBox 从同一源模型生成。
7. 可见加工完成后返回最终预览。城市模型需要逐栋微调 Low/High 时，按 [references/citybox-delivery.md](references/citybox-delivery.md) 的“逐模型描边调参”先生成聚焦预览；确认前不导出或发布整城。只有纯 Anchor/Camera/pivot 等不可见语义调整可用节点清单和校验结果代替视觉审批。
8. 用户确认后写入对应正式源目录并执行该资产类型的唯一发布链；最后验证 Unity 导入和实际运行画面。

用户说“下一步”只通过当前展示的确认关口。用户已经明确表示某类机械修正无需询问时，按其授权继续，不重复确认。

## 工具边界

- Codex 负责提示词、浏览器操作、审阅、Blender/CityBox/Unity 接入；Gemini 负责参考图；Hunyuan Geo/Poly 负责原始模型和低模。
- 优先使用 Codex in-app Browser 的已有登录会话；只有用户指定 Chrome 或内置浏览器不可用时才切换。
- 不搜索同名替代产品，也不在服务失败时擅自改用 Codex ImageGen。
- 每个审批关口必须直接返回足以判断的图片，并给出明确审阅结论；不能只说“已生成”。
- 参考图默认走 `gemini.google.com/images`。AI Studio 仅在已有可用 API key 上下文或用户明确指定时使用；不要把缺少 key 的 `permission denied` 误判成点击问题。

## 完成标准

- 正式文件、根节点、主 Anchor 和主 Camera 使用同一地点名；无历史 alias 或 `final2` 一类过程名。
- Blender 后台校验通过，面数、拓扑、尺度、层级和相机对齐有记录。
- 城市资产必须完成 CityBox build → export → publish，并检查 `city_report.json`、源/目标 `City.fbx` 哈希及 Unity 运行画面。
- 不在 `Main.unity` 中覆盖 `City.fbx` 子对象的材质、激活状态、名称或相机参数；FBX 重建会改变内部 fileID。City 实例只保留根节点变换，子对象行为由导入器和运行时代码按名称建立。
- 修改了 Unity 可执行内容或发布产物后，按 `skills/verify/SKILL.md` 选择最小充分验证。

通用 Blender 命令：

```bash
blender --background model.blend \
  --python skills/create-3d-assets/scripts/validate_blend_asset.py -- \
  --require-camera --require-outline --require-orbit-pivot

blender --background model.blend \
  --python skills/create-3d-assets/scripts/align_camera_to_pivot.py -- \
  --apply --save

blender --background model.blend \
  --python skills/create-3d-assets/scripts/frame_and_render_preview.py -- \
  --output /absolute/path/preview.png --camera-name Camera_资产名 --save-camera
```

城市源模型的校验参数按实际契约选择；`--require-outline` 不适用于由 CityBox 生成描线的源建筑。
