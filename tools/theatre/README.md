# Unity 线绘舞台预览

从真正的 Scheme 入口生成演出，再用当前项目的 `TheatreSession`、`TheatreMesh`、`TheatreSurface`、Shader、参数资产和图片导出完整序列。独立缓存工程不打开 Main，不进入 Play Mode，不占用主工程的 Library，也不操作正在打开的编辑器。

```bash
python3 tools/theatre/preview.py scripts/theatre/雨夜来访.scm '(雨夜来访-演出)'
python3 tools/theatre/preview.py scripts/theatre/路灯下.scm '(路灯下-试演!)'
```

默认输出到 `.cache/theatre-preview/<脚本名>/`：

- `frames/*.png`：1600×900 的完整舞台序列，每秒 4 帧。
- `show.gif`：整场无声动画，最长边压到 800，方便看节奏与位移。
- `index.png`：时间、阶段和对白索引，覆盖均匀采样与每次对白变化。
- `manifest.json`：每帧的时间、阶段、说话人与完整台词，方便定位原图。
- `scene.json`、`export.log`、`unity.log`、`result.json`：实际演出数据与失败诊断。

**审阅时先看完整 GIF 和索引，再打开可疑时刻的原尺寸 PNG。**不要用单个好看的瞬间代表全场。
索引上的台词是审阅标注，不是游戏字幕。GIF 不含音频，不用于听验。

## 范围

这是舞台绘制层预览：图片、线条、焦点、光效与属性动画来自真实 Unity 绘制代码，时间结构来自真实 C# 播放器。
导出器用独立 GameState 执行一次入口，不修改游戏存档；入口必须只发起一场 `play-theatre!`。
导出全部嵌套图片和声音引用，并在 Unity 中预加载。缺失资源、Shader 错误、超时直接失败，不生成假成功。

不加载城市、游戏输入或字幕 UI；舞台纹理合成在剧本底色上，入退场仅显示舞台淡入淡出，不包含城市压暗/推近。
完整字幕排版、固定四角暗角、城市透底、实际声音和玩家点击仍在 Play Mode 验收。

等待点击的对白默认报错。需要检查后续画面时显式模拟等待：

```bash
python3 tools/theatre/preview.py scripts/theatre/我的戏.scm '(我的戏-演出)' --manual-wait 2
```

模拟输入按统一舞台时钟等待指定秒数，再补全文字并推进；`manifest.json` 记录该设置。它不能证明实际点击正确。
任意时间点观察只受采样精度限制；短于采样间隔的闪烁可能漏帧，提高 `--fps`：

```bash
python3 tools/theatre/preview.py scripts/theatre/路灯下.scm '(路灯下-试演!)' --fps 12
```

尺寸只改变离屏输出与宽高比，不按设备改布局。可用 `--width 1200 --height 900` 检查 4:3，`--width 2000 --height 900` 检查宽屏。所有参数见 `--help`。

## 环境与缓存

需要项目可用的 dotnet、Unity、Python Pillow、中文字体源文件，以及已导入的 Unity Newtonsoft.Json 包。
Unity 默认路径为 `/Applications/Unity/Unity.app/Contents/MacOS/Unity`，可用 `--unity` 指定。

`.cache/theatre-preview-project/` 保留 Unity 导入缓存，后续预览复用它，但每次覆盖为当前运行时代码与当前所需资源；不维护第二份运行时实现。
工具使用进程锁，不能同时跑两场预览。若 Unity 已完成导出却卡在退出，工具只终止自己启动的那个进程；不会查找或关闭用户的 Unity。
输出目录必须为空或由此工具标记为拥有，避免覆盖其他文件。缓存均不进入 Git，需要时可删除重建。

生成的表现数据入口也可单独调用：

```bash
dotnet run --project tools/content-validator/SSNoir.ContentValidator.csproj -- \
  --theatre-export '(begin (load-file "scripts/theatre/雨夜来访.scm") (雨夜来访-演出))' /tmp/雨夜.json
```
