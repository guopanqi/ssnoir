# 立绘剧场 · glmflash（Portrait Theatre — GLM）

GLM Flash 制作的独立单文件网页实验，后缀 `glmflash` 即模型名。

```bash
cd experiments/portrait-stage/portrait-theatre-lab/归档/portrait-theatre-glmflash
python3 -m http.server 8125
# 浏览器打开 http://127.0.0.1:8125/
```

单文件 `index.html`，无依赖、双击即可打开。点击「开始演出」或舞台开始，
空格暂停/继续，底部数字 chips（一～六）可直接跳幕，重播从头再演。
声音为 Web Audio 即时合成，随演出自动初始化。

## 相对原稿的修复（3 处，不改演出与对白）

1. **IIFE 提前闭合（致命）**：原稿主脚本的 `})();` 写在雨幕循环之后，
   `posterTableau()` 及其调用留在作用域外，打开即报
   `ReferenceError: setupScene is not defined`。
   已把闭合移到文件末尾，`posterTableau` 回到 IIFE 内部。
2. **`#blackout` 挡点击**：幕间黑场盖住舞台，补 `pointer-events:none`，
   黑场期间点击不再误触暂停。
3. **空格键双重触发**：按钮获焦后按空格会同时触发按钮 click 与全局
   keydown toggle，修复为焦点在按钮上时 keydown 跳过（按钮本身的空格激活保留）。

## 验证

- 主脚本 `node --check` 通过；全文件仅一处 `})();`（末尾）。
- Headless Chrome 真实渲染：海报群像三人正常出场，顶部无红条报错，
  状态栏显示"剧场已就绪"；六幕关键对白 grep 齐全。
