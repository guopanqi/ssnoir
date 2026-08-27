# 网页生成阶段

| 职责 | 入口 | 产物 |
|---|---|---|
| 参考图 | [Gemini Images](https://gemini.google.com/images) | 完整、无裁切的单体参考图 |
| 原始模型 | [Hunyuan 3D Studio Geo](https://3d.hunyuan.tencent.com/studio/creation/geo) | 可下载的原始模型 |
| 低模 | [Hunyuan 3D Studio Poly](https://3d.hunyuan.tencent.com/studio/creation/poly) | 与用途相称的低模拓扑 |

优先使用 Codex in-app Browser 并复用已有登录标签页。不要通过搜索引擎猜入口；服务未登录或不可用时报告阻塞，不擅自换服务。

## 执行规则

- 参考图默认使用 Gemini Images，并保留 Images 模式直到当前候选全部生成完成。
- AI Studio 是带 API key 的可选入口，不是默认入口。若页面同时显示 `No API key selected`，并在生成后提示 `permission denied` / `An internal error has occurred`，视为缺少可用 key 上下文，直接回到 Gemini Images；不要反复更换模型或点击方式，也不要替用户创建、选择或接入付费 key。
- 只有用户明确要求 AI Studio 且已有可用 key 上下文时，`Run` / `Rerun this turn` 才使用截图定位后的真实坐标点击；Playwright/DOM 只负责填写和读取状态。
- 参考图必须整体完整、四周留边、没有人物、可读文字、Logo、现代物件或依赖纹理才能成立的细节。
- Geo 显式选择当前最低面数档，并在生成前复核选中态。
- Poly 从最低档开始，按资产用途选择四边、三角或可解释的混合拓扑。预算造成主要轮廓失败时只升一级并直接重试。
- Hunyuan 下载前先 hover `下载` 打开格式菜单。若必须从已加载资源定位文件，用生成时间、顶点/面数和当前资产统计交叉核对，避免把 Geo 高模误当 Poly 结果。

## 审批输出

参考图、Geo、Poly 和 Blender 可见加工各有一个确认关口。每次必须同时给用户：

1. 原图或清晰预览图；
2. Codex 对轮廓、入口、圆弧、薄片、遮挡、年代和拓扑风险的判断；
3. 当前阶段的面数或关键技术统计。

优先 3/4 视角；必要时补侧视或俯视。灰色 clay/wireframe 只能用于拓扑诊断，不能代替最终加工审批。

中间下载和预览保留在制作目录。正式源目录只放用户确认后的资产。
