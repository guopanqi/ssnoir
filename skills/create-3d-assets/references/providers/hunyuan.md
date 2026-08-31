# Hunyuan Geo / Poly 适配

Hunyuan 属于“高模再拓扑”服务：Geo 从参考图生成高模，Poly 把通过形体检查的高模转换为低模。通用网格质量标准见 [../mesh-generation.md](../mesh-generation.md)。

- Geo 入口：[Hunyuan 3D Studio Geo](https://3d.hunyuan.tencent.com/studio/creation/geo)。
- Poly 入口：[Hunyuan 3D Studio Poly](https://3d.hunyuan.tencent.com/studio/creation/poly)。
- 优先复用 Codex in-app Browser 的已有登录会话。登录或服务不可用时报告阻塞，不搜索同名替代站点。
- Geo 使用满足形体判断的最低合理生成档。Agent 先检查轮廓、主要硬表面、入口、圆弧、薄片、缺损和遮挡；不合格则重做，不进入 Poly。
- Poly 会消耗额外时间或用量。Geo 通过 Agent 质量门后，把 3/4 预览和判断交给用户确认，再进入 Poly。
- Poly 从最低合理预算开始，按用途选择四边、三角或可解释的混合拓扑。主要轮廓或硬表面因预算失败时可直接升一档重试；持续失败或成本显著增加时停止说明。
- 下载前核对资产、阶段、生成时间和顶点/面数，避免把 Geo 高模当成 Poly 结果。网页下载菜单的具体操作由浏览器工具处理。

鉴权、请求、轮询或下载若改为 API，应由新的 provider 文档或脚本负责；不要把 API 协议写回主流程。
