# 内容校验工具

这里保留不依赖 Unity UI 的纯逻辑验证能力，不是第二个游戏客户端。它直接编译
`Engine/Runtime/**`，并读取 `UnityClient/Assets/Resources/Content` 中的唯一一份 Scheme 内容。

从仓库根目录运行：

```bash
./run --validate
./run --test-saveload
./run --test-odds
./run --playtest <入场表达式或场景名>
```
