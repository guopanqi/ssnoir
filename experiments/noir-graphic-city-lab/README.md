# Noir Graphic City Lab

SSNoir 的独立 Three.js 图形城市与人物研究工程。它与 `noir-engraving-lab` 并列，不共享视觉代码；只复用可持续实验方法和 CI 截图基础设施。

目标不是复刻 Genesis Noir 的具体资产，而是研究一种适合 SSNoir 的语言：

> 真实 3D 城市被摄影机压成动态平面设计；大黑形建立世界，选择性白线帮助阅读，人物靠轮廓与姿态成立，极少量金色承担叙事强调。

## 新会话接续

读取 `AGENTS.md → CONTINUE.md → CHECKPOINT.json → DESIGN.md → RESEARCH_LOG.md`。有本地环境时运行 `npm run status`。有 pending review 时必须先看图。

## 运行

```bash
cd experiments/noir-graphic-city-lab
npm install
npm run dev
npm run build
npm run capture
```

键盘 `1–4` 切镜头，`M` 切研究层，`H` 隐藏 HUD。

截图层：

- Shape：只看图形体块与构图。
- Line：恢复选择性结构线。
- Accent：恢复叙事金色。
- Final：加入有限灰阶、轻微颗粒与暗角。

完整截图输出四镜头 × 四层，并计算黑面积、亮纸面积和金色占比。金色 2.8% 只是第一版告警预算，不是审美定律。

GitHub Actions：`.github/workflows/noir-graphic-city-captures.yml`。
