# MiMo 配音生成

这是 SSNoir 的**候选配音生成器**。它直接调用 `mimo-v2.5-tts-voiceclone`：角色库中已有样音的角色走 VoiceClone；临时 NPC、旁白等无样音角色必须写一句 `--style`，走 VoiceDesign。工具不会修改 Scheme 内容，也不会把候选音频直接放进 Unity。

## 角色库与密钥

角色样音和可选的默认演出方向定义在 [`角色库/配音/角色.json`](../../角色库/配音/角色.json)。已有样音的角色如果不需要额外表演控制，可以留空 `direction`，工具只把样音和台词交给 VoiceClone；只有确实需要时才写一句简短方向。

密钥的常驻位置是**当前 macOS 用户的 Keychain**，不写入仓库、角色库、`.env`、shell 配置或生成记录。只需在用户自己的终端执行一次下面的命令；末尾的 `-w` 会安全地提示输入，不要把 key 写到命令中：

```bash
security add-generic-password -U \
  -a "SSNoir MiMo TTS" \
  -s "com.ssnoir.mimo-tts.api-key" \
  -w
```

之后 `generate.py` 会优先从 Keychain 读取。`MIMO_API_KEY` 仍可作为非 macOS CI 或一次性调用的后备方式，但不建议在本机长期设置。Keychain 中的条目可在“钥匙串访问”中删除或更新。

已经完成上述一次性设置后，生成角色台词不再需要传入 key：

```bash
python3 tools/mimo-tts/generate.py \
  --speaker 尼尔 \
  --id 三封信/居民区/东侧门廊/01/尼尔 \
  --text '老街不进「老金牌」香烟。我要去码头居民区一趟。'
```

无参考样音的临时角色：

```bash
python3 tools/mimo-tts/generate.py \
  --speaker 楼上的女人 \
  --id 三封信/居民区/东侧门廊/01/楼上的女人 \
  --text '是你吗？我还当是认错了——' \
  --style '四十多岁的港口居民区女人，嗓音略哑；认出旧人时意外，但不夸张。'
```

语音 ID 是剧情坐标，不是台词文本。统一写成 `模块/地点/路径或事件/节拍/说话人`；同一节拍同一角色的多句台词再加 `01`、`02`。这样台词或演出提示修改时 ID 不变，只需重生成同一条资产；`rg '三封信/居民区/东侧回廊/02/夜莺' UnityClient/Assets/Resources/Content` 能直接回到内容调用处。

复杂地点把多句配音放进 `角色库/配音/清单/` 的 JSON 清单。每一条记录都带稳定 ID、说话人、原文、演出提示和 `sourcePath`；这是 Agent 查找、批量重生成和审阅的唯一索引：

```bash
python3 tools/mimo-tts/generate.py \
  --manifest 角色库/配音/清单/三封信-居民区.json

# 只重生成用户要求调整的一句
python3 tools/mimo-tts/generate.py \
  --manifest 角色库/配音/清单/三封信-居民区.json \
  --only 三封信/居民区/东侧回廊/02/夜莺
```

默认输出是 `角色库/配音/候选/<语音ID>.wav`。先试听并验收；通过后，复制到 `UnityClient/Assets/Resources/Voices/<语音ID>.wav`，并在内容脚本中显式绑定：

```scheme
(line "夜莺" "我们赶时间。" "三封信/居民区/东侧回廊/02/夜莺")
```

旁白不使用 `line` 的语音参数。它需要放入 `Resources/Narrations/`，同时提供同 ID 的 JSON cue 文件，并由 `(play-narration! "ID")` 触发。

## 安全与边界

只上传项目授权的角色样音。生成工具输出的是待验收资产；不把它当作已经可发布的游戏资源。任何曾在聊天、终端记录或截图中明文暴露的 API key 都应在服务商控制台轮换。
