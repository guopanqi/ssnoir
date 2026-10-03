# Design — Noir Graphic City

## 命题

不是“低模 + 黑白描边”，而是：

> 摄影机主导的 3D 舞台，被压成可玩的 motion-graphic noir。

城市和人物共享同一套层级语言：
1. Silhouette：大黑形决定识别。
2. Paper plane：有限亮面决定空间方向。
3. Selective line：只解释关键结构。
4. Accent gold：稀缺叙事焦点。
5. Camera composition：先设计镜头，再允许世界补全。
6. Transition-ready：未来能够让世界在剧情中压平、展开、抽象，而不是依赖写实连续性。

## 非目标

- 不追求 Genesis Noir 资产复刻。
- 不做全局 toon outline。
- 不用霓虹和 Bloom 代替构图。
- 不追求完整可漫游城市后再找好看的角度。
- 不把人物细节量等同于角色表现力。

## 工程结构

- `scene/city.js`：舞台城市、建筑 grammar、环境叙事图形。
- `scene/characters.js`：silhouette-first 人物 grammar。
- `config/visualProfile.js`：有限 palette 和全局视觉约束。
- `post/pipeline.js`：最终有限灰阶、纸色和颗粒。
- `shots.js`：研究镜头。
- `captureApi.js`：Shape / Line / Accent / Final 分层。

## 第一阶段研究顺序

A. Stage Composition
四镜头能否仅靠体块形成明确图形。

B. Character Grammar
三种角色层级：crowd / important / hero。远景 NPC 不因“重要性低”而获得同样线与细节。

C. Selective Line
建筑只在地标、空间转折和遮挡关系上出现结构线。

D. Accent Semantics
金色只代表需要玩家注意的叙事信息。先设 2.8% 像素告警预算，之后靠审图修订。

E. Motion / Transition
静态语言稳定后，再研究人物动作、空间压平、线条浮现和 investigation rendering。

## 迁回 SSNoir 的目标

最终迁移的是：
- 建筑与人物的视觉重要性 metadata；
- silhouette / line / accent 三层规则；
- 固定镜头与构图原则；
- narrative LOD；
- 可用于调查和情绪变化的图形 transition。

不是迁移 Three.js 文件本身。
