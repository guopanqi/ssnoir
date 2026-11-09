# Parker 双色夜城对话构图实验

从零设计的 16:9 游戏对话画面，使用 Gemini Images 的 Argon 模型生成；未上传项目现有角色图或截图。

- 原图：[三人同屏.jpeg](三人同屏.jpeg)（2752×1536）
- 提示词：[三人同屏-prompt.txt](三人同屏-prompt.txt)
- Gemini 对话：[Film-Noir Game Dialogue Screen](https://gemini.google.com/app/cde3cbd173c41da2)

本图用于检验限色人物与蓝黑雨夜场景、对话框的同屏关系，不是正式立绘资源。


## CLI 对比尝试（2026-11-07）

使用同一个 `三人同屏-prompt.txt`、新账号、无参考图，显式启用 Create image。CLI 已支持 `--model Argon|Flash`，默认 Argon；后台模式无需 `--headed`。

| 网页选择 | CLI 结果 | 新图 |
| --- | --- | --- |
| Argon | 前台与后台均达到 90 秒上限，仍显示等待，未获得会话 ID | 无 |
| Flash | 后台两次返回 `I encountered an error doing what you asked.` | 无 |

本轮未获得可比较的两张新图，因此不能判断 Argon 与 Flash 的质量差异。上面的 `三人同屏.jpeg` 是此前通过普通 Chrome 成功生成的 Argon 样本，不计作本轮 CLI 成功结果。网页选择器名称不能证明实际执行生成的后台图片模型；未取得可核实的对应关系。

诊断记录位于本地 `tmp/gemini-image-web/Parker双色夜城对话/三人同屏-Argon-CLI/` 和 `三人同屏-Flash-CLI/`，包含 manifest、错误截图及 Flash 回复原文。


## 前台诊断与成功对照（2026-11-07）

原长提示词再次通过前台 CLI 提交，Flash 返回：`I'm having a hard time fulfilling your request. Can I help you with something else instead?`。同一账号、同一 CLI 的蓝色杯子对照在 17 秒生成成功；保留三个人物、色板、雨夜及对话框要求的[精简提示词](三人同屏-精简-prompt.txt)随后在两个网页模型选择下都生成成功。两个成功 manifest 中的提示词逐字一致，未上传参考图。

| 网页模型选择 | 本次耗时 | 完整尺寸下载 | 观察 |
| --- | --- | --- | --- |
| Flash | 14 秒 | [2752×1536 JPEG](三人同屏-Flash-精简-原图.jpeg) | 对话框较干净；三人脚部被裁切，Eddie 显得偏大；造型偏干净的动画插画 |
| Argon | 22 秒 | [2752×1536 JPEG](三人同屏-Argon-精简-原图.jpeg) | 三人全身完整、街道纵深较好；误把 speaker、dialogue、advance control 等提示词标签画入 UI |

两图均已达到冷蓝黑场景、纸白肤色、少量酒红的限色方向，但脸部与线条偏柔和，大片黑块和纸张质感还不足。单次样本只支持本图观察，不能据此确认模型总体优劣，也不能证明两个网页选项调用不同的后台图片模型。

诊断证据：生成接口 `processSession` 在成功对照中返回 HTTP 200；成功和失败都出现 `net::ERR_ABORTED`，因此不能把该日志单独当成失败根因。原请求的拒绝没有说明具体政策、长度上限或内部错误码；精简后成功只能说明这次请求改写有效，无法证明一定是提示词过长或风控导致。

`--debug` 已加入 CLI。原长提示词、杯子对照和两个成功样本的脱敏诊断分别保存于本地 tmp 目录下的 `Flash前台诊断`、`Flash简单对照`、`三人同屏-Flash-精简`、`三人同屏-Argon-精简`。CLI 最初保存的 PNG 为网页预览的 canvas 重编码；上表 JPEG 已通过网页 Download full size image 按钮另行下载，原始下载路径写入各 manifest 的 `original_download`。
