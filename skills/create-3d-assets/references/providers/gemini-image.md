# Gemini Image 适配

本文件只说明何时以及怎样用 Gemini 把提示词变成候选参考图。提示词内容与审图标准见 [../visual-definition.md](../visual-definition.md)；不要在这里复制艺术规则。

Gemini Image 是当前默认参考图服务。优先复用 Codex in-app Browser 的已有登录会话。Gemini AI Studio 也可通过 in-app Browser 使用；只有任务确实依赖用户 Chrome 中的登录状态时，才用 Computer Use 控制 Chrome。

## 统一输入与输出

无论使用哪个服务，输入都应包含完整提示词、候选数量和必要参考素材。输出必须保存原图，并记录使用的服务、提示词和候选对应关系，使后续 image-to-3D 不依赖网页历史。

Agent 下载后先按 [../visual-definition.md](../visual-definition.md) 的标准审阅。明显裁切、时代错误、轮廓混乱、依赖纹理或不适合建模的结果在内部淘汰或重生成；只把合格候选交给用户。

## Gemini 网页适配

- Gemini Images 默认入口是 [gemini.google.com/images](https://gemini.google.com/images)，保持 Images 模式直到当前候选完成。
- AI Studio 需要可用 API key 上下文。若同时出现 `No API key selected` 与 `permission denied` / `An internal error has occurred`，按缺少 key 处理，不反复点击，也不代替用户创建或选择付费 key。
- 网页自动化方式属于浏览器工具自身职责；本 Skill 只要求确认生成状态、下载原图并建立候选对应关系。
