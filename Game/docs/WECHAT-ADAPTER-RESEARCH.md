# 微信 Three 适配实现核对（2026-10-11）

这份记录服务于第一阶段已有技术底座的验收，不代表完整游戏内容已移植。

| 开源实现 | 当前源码证据 | 对 SSNoir 的结论 |
| --- | --- | --- |
| [wechat-miniprogram/threejs-miniprogram](https://github.com/wechat-miniprogram/threejs-miniprogram) | [package.json](https://github.com/wechat-miniprogram/threejs-miniprogram/blob/master/package.json) 使用 Three 0.108.0；面向小程序 scoped Canvas | 不能据此证明当前小游戏 Three r186 + Pixi 8 的共享 WebGL2 兼容性 |
| [deepkolos/platformize](https://github.com/deepkolos/platformize/blob/main/packages/platformize-three/README.md) | 文档列举 GLTF/纹理等 Loader，明确只测试 r133；[小游戏示例](https://github.com/deepkolos/platformize/blob/main/examples/three-wechat-game/minigame/game.ts) 使用 WechatGamePlatform、WebGL1Renderer、GLTFLoader 和 RAF | 可参考宿主边界及资源加载实现，但示例使用 WebGL1，未覆盖 Pixi 共享上下文；不直接替换当前已运行的适配层 |
| [finscn/weapp-adapter](../vendor/weapp-adapter/README-SSNOIR.md) | 当前仓库按固定来源保留 MIT Canvas/HTMLElement/EventTarget 原始文件，哈希测试通过 | 继续作为选择性 DOM 基础；Three/Pixi/LIPS 所需宿主接口在 bootstrap 内明确实现 |

platformize 的资源加载与 GLB 示例比当前几何体探针覆盖更广。它的 README 同时指出内嵌纹理的 ArrayBuffer→base64 成本及多次页面进入的内存问题；这些是后续接入真实资源时必须实测的风险，不能把上游声明作为本项目通过证据。

当前本项目已有实测：微信开发者工具 WebGL2、Three/Pixi 同画布、中文文字、双 LIPS VM 隔离、原始 stdlib 与触摸计数。尚缺后台恢复证据、原生设备与发布宿主证据；远程资源/XML 在当前探针中明确不支持。完整 engine/world/native bridge 属于第二阶段。
