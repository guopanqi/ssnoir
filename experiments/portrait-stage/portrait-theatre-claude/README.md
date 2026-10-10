# 立绘剧场 · Portrait Theatre (Claude Sonnet 5.5)

最新、最完整：portrait-theatre-story-claude-sonnet-5-5.html（19个片段，线绘世界，地图点选/连播）
- portrait-theatre-claude-sonnet-5-5.html：最初的六段版本（纯色背景）
- portrait-theatre-grand-hotel-...：酒店门前原型
- portrait-theatre-street-lamp-...：路灯原型
- portrait-theatre-lines-...：酒店+路灯合并版
- script-notes.txt：用户提供的故事稿
- build-scripts/：最后一次补丁脚本（早期构建脚本已丢失，成品HTML本身是完整的，可直接编辑）
所有HTML为单文件，直接用浏览器打开。

## 工具 tools/
- test_play.py：无头浏览器测试。`full` 快进整部戏找JS报错；`sheet N 秒数列表` 给第N个片段截图拼接。
- patch_html.py：直接在成品HTML上做精确替换的补丁模板（旧文本必须唯一）。
## 开发经验
- 片段数据在 `const SC=[...]`，每段含 n/sub/sh/pos/bg/set/mk/ini/run；布景在 `SETS.xxx`，人物在 `CH` 与 `P(id,{...})`。
- 曾因 SVG 渐变 id 与人物 id 冲突导致整页点击无反应，新增 id 前先全局搜索。
- 每次改完先跑 `full`，再对改动的片段跑 `sheet`。
