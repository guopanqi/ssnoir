# Portrait Theatre · Claude Sonnet 5.5（立绘剧场 · 线绘世界）

噪点黑色电影风的单文件网页实验：城市地图上有 25 个可点击地点，点击后镜头推进到该地点的线绘舞台，
由少量线稿道具 + 矢量立绘人物 + Web Audio 合成音效演出一段故事。可暂停、重播、连播全剧。

- 直接观看：浏览器打开 `dist/portrait-theatre-story-claude-sonnet-5-5.html`（单文件，无依赖；仅字体走 Google Fonts，离线自动回退）。
- 在线版（claude.ai 私有 Artifact）：https://claude.ai/artifact/TXkXgC7MiaP8zPVfkR2YfK
- 故事对白与设计准则都在本目录内：对白在 `src/js/chapters/`，准则在 [AGENTS.md](AGENTS.md)。

## 目录

```
src/                     源码（改这里）
  index.template.html    页面骨架（舞台层、控制条），含 /*@STYLE*/ /*@SCRIPT*/ 占位
  style.css
  js/00-core.js          $、全局状态、Web Audio 音效(tn/bs/bell/buzz…)
  js/10-world.js         城市地图 + 尼尔公寓外立面(#hotel) + 路灯街景(#lamp)
  js/20-cast.js          人物数据 CH、人物绘制 mk()、姿势函数 P()
  js/30-runtime.js       计时(sleep/H)、舞台反应助手(rx/lamp/sp/pk…)、字幕 say
  js/35-fx.js            雨、颗粒
  js/40-sets.js          线绘布景 SETS.*、公共道具(GROUND/HANG/TABLE/…)
  js/45-chapter-objects.js    第 1 章（雨夜）与路灯争执章的对象定义 C1/C7
  js/chapters/NN-*.js    25 章，一章一文件，按文件名排序即章节顺序（03-rain-night 引用 C1，13-lamp-quarrel 引用 C7，其余为内联对象）
  js/60-boot.js          reset/chapter/play/暂停/按钮绑定/启动
dist/                    构建产物（单文件 HTML，提交入库方便直接打开）
tools/                   test_play.py（无头测试）、realtime_capture.py（真实时间截帧）
archive/                 早期原型与历史补丁脚本（只读参考）
build.mjs                拼接脚本
```

## 工作流

```bash
node build.mjs            # src → dist/…html
node build.mjs --check    # 只做语法检查
pip install playwright pillow && playwright install chromium   # 首次
python3 tools/test_play.py dist/portrait-theatre-story-claude-sonnet-5-5.html full          # 快进全剧，报 JS 错误
python3 tools/test_play.py dist/portrait-theatre-story-claude-sonnet-5-5.html sheet 4 6,9,12 # 第4章在 6/9/12 秒截图拼图
```

改完 `src/` 必须重新 `node build.mjs`，并提交 `dist/`。给开发者/后续会话的详细约定见 [AGENTS.md](AGENTS.md)。
