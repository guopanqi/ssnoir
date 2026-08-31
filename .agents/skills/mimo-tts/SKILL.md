---
name: mimo-tts
description: 为 SSNoir 生成、验收并接入 MiMo 角色配音。用于把游戏字幕、Banter 或角色对白转为可播放音频；已有角色样音走 VoiceClone，无样音角色走 VoiceDesign。
---

# SSNoir MiMo 配音

使用项目工具 [`tools/mimo-tts/generate.py`](../../../tools/mimo-tts/generate.py) 生成候选音频；完整命令、角色库和命名约定见其同目录 [`README.md`](../../../tools/mimo-tts/README.md)。

密钥由工具从当前 macOS 用户的 Keychain 读取，service 为 `com.ssnoir.mimo-tts.api-key`、account 为 `SSNoir MiMo TTS`。不得查找、输出、写入或要求保存 API key 到仓库、`.env`、shell 配置或任何内容清单；只有 Keychain 缺项时，才请用户在其终端完成 README 中的一次性设置。

先把待生成台词写入与剧情对应的 `角色库/配音/清单/*.json`。语音 ID 是稳定剧情坐标，而不是台词文本：`模块/地点/路径或事件/节拍/说话人`；文字与演出方向改变时保留同一 ID。角色库里已有样音的说话人不写 `style`；没有明确演出需求时可以不写 `direction`，工具只发送样音与台词，让 Voice Clone 以样音为准；需要控制情绪时只写简短、准确的 `direction`。没有样音的临时角色或环境叙述必须以一句 `style` 描述声线与当下情绪。

默认不要用“停顿、留白、拖尾、放慢、句尾收住”等时间性指令；它们会让 TTS 产生刻意拖慢的腔调。只有剧情明确需要停顿时才写，且限定在一句具体台词。

清单中的 `text` 必须逐字等于游戏 `line` 的玩家可见台词。先删除「（字幕）」等编辑标记，再生成；不要把“不要念出括号”之类的要求交给模型处理。

生成的文件只是一份候选。试听或获得用户验收后，复制到 `UnityClient/Assets/Resources/Voices/<语音ID>.wav`，带上 Unity `.meta`，并在对应 `(line ...)` 的第三个参数绑定同一 ID。不要把未验收的候选直接当发布资产。Banter 有绑定语音时会等待播放结束；显式 dwell 只用于额外停顿。旁白机制另走 `Resources/Narrations/` 与 `(play-narration! ...)`，不要混用。

生成后检查音频时长是否明显不符合文本长度；短句若异常拉长，先试听确认是否发生复诵。复诵是生成失败：在该句 `direction` 明确要求“只说一次，不重复文字，不添加内容”后重生成，不在游戏端重复播放或截断音频。

改动 `.scm` 时遵守 `$write-scheme`；改动运行时代码或可执行工具时遵守 `$verify`。每次完成接入后，至少检查路径/ID 一致性、`git diff --check`，并按改动范围做内容或编译验证。

---

## 经验参考：对 MiMo 模型的机制理解

- **极强的字面遵从性与极简介入**：模型对提示词有极强的字面遵从性（例如说“松弛”整句就会通篇过度松弛，说“缓慢”整句就会通篇机械变慢）。因此 `direction` 默认保持为空，让样音自然驱动；仅在试听后发现具体情绪偏离时，才用 4～8 个字的极简锚点进行微调纠偏（如「语气平静轻柔，沉稳克制」、「低沉从容，平淡反问」）。
