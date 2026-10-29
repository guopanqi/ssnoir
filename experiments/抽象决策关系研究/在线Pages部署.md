# SSNoir 抽象机制研究 · GitHub Pages

正式在线入口（Pages 启用后）：**https://guopanqi.github.io/ssnoir/**。研究源码仍以 main 分支为准，所有研究结论或试验任务更新后由工作流自动发布。

## 首次启用

1. 在仓库的 [Settings → Pages](https://github.com/guopanqi/ssnoir/settings/pages) 中，将 **Build and deployment → Source** 设为 **GitHub Actions**。
2. 打开 [研究网页部署工作流](https://github.com/guopanqi/ssnoir/actions/workflows/ssnoir-mechanism-pages.yml)。如果启用前首次部署失败，点击 **Run workflow**，从 main 分支手动触发一次。
3. 成功后访问 https://guopanqi.github.io/ssnoir/ 。后续对研究源、模板、规则和交互的提交会自动重新发布；GitHub Pages/CDN 可能有短暂传播延迟。

工作流只上传编译后的 index.html 和 .nojekyll，不会把整个游戏仓库发布为网站资源。

## Token 只需首次连接

在线页面中的「GitHub 同步」允许你输入自己的 Fine-grained Personal Access Token。仅选择 guopanqi/ssnoir 这一个仓库，权限只需要 **Contents: Read and write**（Metadata: Read 通常自动授予），不需要 Actions、Administration、Workflow 等写权限。

点击「验证并保存到此浏览器」之后，Token 在当前网页 Origin 的 localStorage 持续保存，页面更新后仍可使用。点击「清除此设备 Token」即可清除；Token 到期或在 GitHub 被撤销则必须重新输入。Token 不会提交到 GitHub 或写入反馈 JSON，且网页没有第三方脚本。

**安全权衡**：localStorage 可以被同源脚本读取。所有位于 guopanqi.github.io 域名下的 Pages 项目可能共享同一个 Origin；如果其他同源页面的脚本被攻破，该 Token 存在被读取的风险。如果想严格隔离，建议给本研究网页使用独立域名；不接受风险则继续导出 JSON 人工提交。公开仓库中的反馈文本和试玩数据也是公开可读的，避免上传个人敏感信息。

## 记录与更新

1. R54/R50 可以直接用网页简化版试玩；R37/R49 使用正式 Unity/C# 入口。
2. 写一条感受并点击「提交到 GitHub」，页面创建一份反馈文件并在下方「已提交」列表展示。
3. 后续点击「继续编辑」，载入原文件，追加更多游戏回合、修改文字或填「补充新的体验」，再次提交就更新同一文件。GitHub Contents API 使用 SHA 乐观并发校验，避免同时更新时悄悄覆盖。
4. 切换到新实验可创建独立反馈；旧反馈不会被删除。无需每次输入 Token。
5. 任何反馈都仍可手动「备份为 JSON」，不依赖 Token 或网络。

提交反馈只会自动触发 JSON 格式校验，不会自行在后台启动 AI 研究；当你下次要求 Agent 继续时，Agent 将按照研究 Skill 先检查反馈目录并读取新内容。

原始 [研究总览](研究总览.html)、[反馈协议](反馈/README.md)、[研究定位](研究定位与评价.md)。
