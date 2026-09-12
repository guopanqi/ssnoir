---
name: create-3d-assets
description: 为 SSNoir 设计、生成、加工、接入或修复游戏可用的 3D 资产。用于参考图、image-to-3D、低模拓扑、Blender 处理、CityBox 城市装配、Unity 发布，以及 Anchor、Camera、orbit pivot 和描线相关工作。
---

# SSNoir 3D 资产

把资产生产视为四个稳定阶段；生成服务和操作方式只是阶段内可替换的适配器，不属于主流程本身。

## 先确定交付目标

- **城市地点与场景**：正式源是 `city-box/prefabs/<名>.blend`（Prefab），布局在 `city-box/city.blend`，Unity 只消费 `Assets/Resources/Models/Environment/City.fbx`。只读取 [references/delivery/citybox.md](references/delivery/citybox.md)。
- **非城市独立资产**：按运行时职责放入 `UnityClient/Assets/Resources/Models/`。只读取 [references/delivery/unity-standalone.md](references/delivery/unity-standalone.md)。
- **候选与中间结果**：保留在制作目录或 `city-box/prefabs/review/`，不进入正式构建。

正式地点名以 Scheme `GameNode.Name` 为唯一主键。只有涉及命名、Anchor、Camera、层级、orbit pivot 或运行时描线契约时，才读取 [references/runtime-contract.md](references/runtime-contract.md)。

## 四个阶段

### 1. 视觉定义

根据用户目标、资产职责、游戏风格、年代、目标尺寸与城市语境撰写提示词，生成一批适合 image-to-3D 的参考图。先由 Agent 按轮廓、构图、年代、遮挡和可建模性淘汰不合格结果；只把合格候选与明确判断交给用户选择。

读取 [references/visual-definition.md](references/visual-definition.md)。实际生图时按用户指定或当前默认选择 provider：Gemini 读取 [references/providers/gemini-image.md](references/providers/gemini-image.md)；LibTV 读取 `libtv-image-generate` Skill；其他 API 读取它自己的适配说明。不要为了比较服务而加载所有 provider。

### 2. 网格生成

用确认的参考图获得满足用途的低模网格。阶段的输入是参考图和预算，输出是可下载、可审计的低模；服务可以直接生成低模，也可以先生成高模再拓扑。

Agent 必须在每次交给用户前先检查轮廓、主要结构、表面噪声、缺损和拓扑风险。若服务把高模生成与低模拓扑分开，可在高模通过 Agent 质量门后让用户确认是否消耗下一步用量；低模必须再次审阅。读取 [references/mesh-generation.md](references/mesh-generation.md)；确定使用 Hunyuan 后才读取 [references/providers/hunyuan.md](references/providers/hunyuan.md)。

### 3. 游戏化加工

在 Blender 中清理网格、替换为项目材质、统一米制尺度和朝向、应用 Rotation/Scale，并建立所需语义节点。先完成最终几何变换，再处理描线和镜头。读取 [references/asset-processing.md](references/asset-processing.md)；只在需要运行时语义时再读取 `runtime-contract.md`。

生成最终 3/4 预览；城市资产同时生成现行 CityBox Low/High 描线下的聚焦预览。Agent 先按游戏画面标准检查材质、轮廓、比例、描线、相机和语义契约，合格后再交给用户确认。纯 Anchor/Camera/pivot 等不可见修正可用校验结果代替重复视觉审批。

### 4. 场景接入与发布

城市资产先进入 CityBox：已有 Prefab 则替换其几何并保持 footprint；新地点在 `city.blend` 放实例，依附别的地点的场景在那个地点的 Prefab 里 instance，然后生成整城预览。Agent 先排除遮挡、尺度、道路关系和构图问题；新增或改变布局必须由用户确认整城预览。

用户确认最终单体效果以及必要的整城布局后，才提升为正式源并执行唯一发布链。最后验证 Unity 导入与实际运行画面。继续使用开头已经选定的唯一 delivery 文档，不加载另一条发布链。修改 Unity 可执行内容或发布产物后，按 `skills/verify/SKILL.md` 选择最小充分验证。

## 质量门与确认门

每个阶段都先经过 **Agent 质量门**：不达标就诊断并在合理范围内重做，不把明显失败品交给用户。重试会产生显著费用、覆盖正式产物或需要改变需求时停止并说明。

**用户确认门**只用于需要主观取舍或扩大承诺的节点：参考图选择、可选的高模用量关口、最终低模、最终加工效果、新增/改变城市布局、正式发布。用户说“下一步”只通过当前展示的确认门；已经明确授权的机械修正不重复询问。

审批输出必须包含足以判断的图片、Agent 的明确结论和当前阶段的关键统计或风险；不能只报告“已生成”。wireframe/clay 可作诊断，不能代替最终视觉预览。

## 服务适配规则

- 主流程只依赖阶段产物，不依赖 Gemini、Hunyuan、浏览器或 API。新增服务时在 `references/providers/` 增加窄适配文档，或复用已有专用 Skill；不改四阶段骨架。
- 图片服务和网格服务分别选择；不要因为一个服务失败而隐式更换另一个阶段的方案。
- 优先级是项目当前偏好，不是资产契约。服务不可用时报告具体阻塞；未经用户授权，不创建付费 key、不扩大费用，也不把失败结果冒充完成。
- 浏览器、Computer Use、CLI、项目封装或 API 的点击与鉴权细节留在对应服务适配文档或专用 Skill，不写进主流程。

## 完成标准

- 低模在目标镜头下保持正确轮廓与主要结构，并有面数、拓扑和网格健康记录。
- 正式文件、根节点、主 Anchor 和主 Camera 使用同一地点名，无 alias 或 `final2` 一类过程名。
- Blender 后台校验通过；尺度、层级、材质、描线和相机符合该资产发布链。
- 城市资产完成 `./build.sh`，并检查 `build/city/report.json`、两份 `City.fbx` 哈希和 Unity 运行画面。
- `Main.unity` 的 City 实例只保留根节点变换，不覆盖 `City.fbx` 子对象。

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

城市源模型由 CityBox 生成描线，校验时不使用 `--require-outline`。
