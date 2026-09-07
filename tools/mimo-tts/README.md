# MiMo 配音生成

这是 SSNoir 的**候选配音生成器**。底层基于小米 MiMo V2.5，统一由三种显式配音模式（`--mode`）驱动：

| 配音模式（`--mode`） | 对应模型 | 核心参数 | 适用场景 |
| :--- | :--- | :--- | :--- |
| **`preset`** | `mimo-v2.5-tts` | `--voice <音色名>` | 主角（尼尔）、世界念白、官方内置精品音色 |
| **`clone`** | `mimo-v2.5-tts-voiceclone` | `--sample <音频文件>` | 有参考录音的角色复刻（常驻配角，支持 CLI 传入样音） |
| **`design`** | `mimo-v2.5-tts-voicedesign` | `--style <形象白描>` | 临时 NPC、无样音路人声音定制 |

全模式通用可选参数：`--direction "本句表演与情绪方向"`。

工具不会修改 Scheme 内容，也不会把候选音频直接放进 Unity。

## 角色库与密钥

角色默认模式、样音或预置音色配置定义在 [`角色库/配音/角色.json`](../../角色库/配音/角色.json)：
- 预置角色（默认）：配置 `"mode": "preset"`, `"voice": "白桦"`（如：尼尔=白桦，夜莺=冰糖，世界=苏打）
- 克隆角色：配置 `"mode": "clone"`, `"sample": "路径/样音.wav"`（如：弗兰克、经理、记者、贝恩斯等）

密钥的常驻位置是**当前 macOS 用户的 Keychain**，不写入仓库、角色库、`.env`、shell 配置或生成记录。只需在用户自己的终端执行一次下面的命令；末尾的 `-w` 会安全地提示输入，不要把 key 写到命令中：

```bash
security add-generic-password -U \
  -a "SSNoir MiMo TTS" \
  -s "com.ssnoir.mimo-tts.api-key" \
  -w
```

之后 `generate.py` 会优先从 Keychain 读取。`MIMO_API_KEY` 仍可作为非 macOS CI 或一次性调用的后备方式，但不建议在本机长期设置。Keychain 中的条目可在“钥匙串访问”中删除或更新。

---

## 常用调用方式

### 1. 日常默认调用（最常用）
对于角色库中已收录的角色，直接提供 `--speaker`、`--id`、`--text`，工具自动读取 `角色.json` 的 `mode` 与对应配置：

```bash
# 尼尔（自动使用 preset / 白桦）
python3 tools/mimo-tts/generate.py \
  --speaker 尼尔 \
  --id 示例/开场/01/尼尔 \
  --text '钱的事我来想办法。他拿不走这笔钱。'

# 弗兰克（自动使用 clone / 弗兰克voice.wav）
python3 tools/mimo-tts/generate.py \
  --speaker 弗兰克 \
  --id 三封信/老街/工会/01/弗兰克 \
  --text '勒索是下三滥。老街不替这种事护短。'
```

### 2. 预置音色测试与覆盖（Preset）
显式指定 `--mode preset`（或直接传 `--voice`），可用内置音色覆盖角色默认配置或为路人配音：
```bash
# 临时测试尼尔换用 Dean 声音
python3 tools/mimo-tts/generate.py \
  --speaker 尼尔 \
  --mode preset \
  --voice Dean \
  --id 试音/尼尔_Dean \
  --text '钱的事我来想办法。'
```

### 3. 本地样音临时克隆（VoiceClone）
显式指定 `--mode clone` 并传入 `--sample`，无需修改 `角色.json` 即可直接测试一段音频的克隆效果：
```bash
python3 tools/mimo-tts/generate.py \
  --speaker 临时角色 \
  --mode clone \
  --sample 角色库/弗兰克/声音/参考样音/弗兰克voice.wav \
  --id 试音/克隆测试 \
  --text '老街不进这牌子的烟。'
```

### 4. 文本白描全新音色（VoiceDesign）
显式指定 `--mode design` 并传入 `--style`，通过文本描述定制声线：
```bash
python3 tools/mimo-tts/generate.py \
  --speaker 楼上的女人 \
  --mode design \
  --style '四十多岁的港口居民区女人，嗓音略哑；认出旧人时意外，但不夸张。' \
  --id 三封信/居民区/东侧门廊/01/楼上的女人 \
  --text '是你吗？我还当是认错了——'
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
