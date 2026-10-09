# SSNoir 抽象玩法研究：人类试玩反馈协议 v1

此目录是**人类和 Agent 的双向接口**：玩家在独立 HTML 研究工作台中选择优先实验、试玩或使用正式原型、填写感受、导出 JSON，并上传到 \`反馈/inbox/\`。研究者下一次续研必须**先阅读新反馈，再决定是否继续发展、弱化、暂停或重构模式**。

## 玩家提交路径

1. 获取 GitHub Actions 生成的 \`ssnoir-abstract-pattern-atlas\` Artifact，将 \`研究总览.html\` 下载到电脑后用浏览器打开。也可用静态服务器打开仓库中的同名文件。
2. 进入「先试玩」：浏览器内置 R54 / R50 的最小**交互示意版**，无需 Unity；R37 / R49 只有正式 C# / Scheme 或 Unity 调试入口。**浏览器版和正式引擎是两个不同执行环境**，其反馈必须分开解释。
3. 填写「体验记录」：可以只写一两句，可以标注犹豫的一手，不要求给机制打分。会话操作可自动记录，但不自动做研究结论。
4. 选择「导出 JSON」，得到单独的 \`SSNoir-feedback-R54-....json\` 文件。网页会尝试本地保存草稿，但浏览器可能不允许 file:// 持久存储；**只以导出的 JSON 文件为保存凭据**。可以「导入草稿 JSON」继续补充。
5. 在 GitHub 仓库的 \`docs/实验/机制研究/反馈/inbox/\` 中使用 **Add file → Upload files** 上传导出的文件并提交到 \`main\`。无需修改实验网页源码、无需粘贴 GitHub token。若从手机访问，先下载 JSON 再上传。
6. GitHub Actions 的反馈验证检查结构、实验身份和大小；不会编辑或覆盖玩家感受。失败时请查看校验日志，只修格式，不自行改写原意。

## 文件契约

顶层示例（省略详细行动；全部可空的自然语言字段允许空字符串）：

\`\`\`json
{
  "schema": "ssnoir.mechanism-feedback/v1",
  "studyId": "R54",
  "studyTitle": "对手追击领先目标",
  "createdAt": "2026-10-09T06:00:00.000Z",
  "environment": "browser-sketch",
  "source": {"studyConfigVersion": 1, "atlasVersion": 1, "officialEntry": "encounters/研究·追击领先目标"},
  "responses": {
    "understanding": "somewhat",
    "replayInterest": "maybe",
    "decisiveMoment": "第二手之后",
    "decisionChange": "……",
    "mechanicalFeeling": "……",
    "friction": "……",
    "ideas": ""
  },
  "runs": [],
  "notes": []
}
\`\`\`

- \`studyId\` 必须在试玩任务配置中存在（R54、R50、R37、R49）。
- \`environment\` 是 \`browser-sketch\`（网页独立示意版）或 \`official-runtime\`（真实Unity/C#）；严禁假装网页调用了正式引擎。
- \`runs\` 只有浏览器版可以自动追加（含骰面、结果、所选目标和对手反应）。会话日志只能证明**本次示意版中的玩家路径**，不用于更新 C# 数学真值或称正式引擎回放。
- \`responses\` 保留玩家原话，不要强制分数或把情绪压成一个总分。未填回答视为未知，不得推定没感受。
- \`notes\` 可留给人工补充。不要包含个人敏感信息、账户信息或 token。
- 内容最少需要一项文字反馈或一个完整网页试玩回合；上传文件体积上限 200 KiB，最多20局。
- 文件名格式推荐 \`SSNoir-feedback-R54-2026-10-09-<随机短码>.json\`，不会覆盖其他玩家文件。

## Agent 接续义务

- 开始新的抽象机制研究之前检查 \`反馈/inbox/*.json\` 有无未处理的反馈，不要求玩家把相同内容重新口述。判断 \`environment\` 和 \`source.officialEntry\`，**区分示意版与正式 Content**。
- 先引用/概括玩家的**具体犹豫时刻、选择、理解偏差和重复感**，再评估模型是否回答了正确的问题；不改写原始文件。
- 将反馈裁决、原因与待验证反例记录在相应批次目录、研究队列、当前状态，必要时更新 \`模式库.json\` 和网页推荐顺序。不要因为一次单人反馈就声称统计显著。
- 人类反馈可以要求新实验、改进UI、废除某模式，Agent 不应把数学上非零的效用增量压过人类体验裁决。
- 不要自动从 Inbox 删除原始反馈。若想标记已处理，在研究记录中链接文件路径和提交 SHA；重复读到同一文件时可以比对 SHA 去重。

入口：[研究工作台](../研究总览.html) | [试玩任务配置](../试玩任务.json) | [研究定位](../研究定位与评价.md)
