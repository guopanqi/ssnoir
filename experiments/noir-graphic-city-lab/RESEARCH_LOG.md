# Research Log

## 2026-10-03 — 实验创建

问题：能否把 Genesis Noir 启发的语言拆成可生产、可验证的 SSNoir 城市与人物系统，而不是做一个黑白 toon shader？

第一版假设：
- 城市先用大黑形和有限亮面成立；
- 只有 hero/context 对象获得结构线；
- 人物先用帽檐、肩宽、外套/裙摆、站姿形成 silhouette；
- 金色必须足够少，才能保持叙事意义；
- 后处理只负责有限灰阶与媒介统一，不能救坏构图。

工具变化：
- 复用 Engraving Lab 的固定镜头、分层截图、严格浏览器错误检查、manifest 和 contact sheet 思路。
- 分层改成 Shape / Line / Accent / Final。
- 报告新增 gold share；第一版 2.8% 仅用于发现 accent 泛滥。

状态：尚未形成已审阅视觉基线。下一步是运行第一组完整 CI capture，并实际看图。
