# Schemy Fork — 工作指南（AGENTS）

适用于 `schemy-master/` 下的开发。这是 [Microsoft/schemy](https://github.com/Microsoft/schemy)
的 fork,供 SSNoir 使用。

## 这个 fork 的目的

- 为 SSNoir(及未来项目)提供一个**独立、现代、可复用、Unity 友好的 C# Scheme 库**。
- **库本身不包含 SSNoir 概念**;SSNoir 只是使用者。新增能力请保持通用,
  不要把游戏逻辑写进解释器。

## 工作约定（必须遵守）

1. **改了 `schemy-master/` 下任何源文件,必须运行 `./build-unity-plugin.sh`**
   重建 `Engine/Plugins/schemy.dll`,否则 Unity 用的还是旧版本。
2. **每一处改动(修复或新增)都要记到 [CHANGES.md](CHANGES.md)**:带日期,
   写清「改了哪个文件 / 为什么 / 行为前后差异」。
3. 遇到与标准 Scheme(R5RS)的差异或限制,在 CHANGES.md 记录;必要时同步到
   SSNoir 的 [TODO.md](../TODO.md) / [schemy.md](../schemy.md)。

## 文档分工

| 用途 | 文件 |
|---|---|
| **它是什么**(原版项目说明,尽量保持上游原样) | [README.md](README.md) |
| **我们改了什么**(按时间的改动日志 + 与上游差异) | [CHANGES.md](CHANGES.md) |
| **怎么在这里干活**(目的 + 流程 + 约定) | 本文件 |
