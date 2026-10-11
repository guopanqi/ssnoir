# 可复用的微信小游戏适配层

输入是微信小游戏的 `wx` / `globalThis` 宿主，输出是 Canvas/WebGL2、2D 文本 Canvas、
所需 DOM 子集、UTF-8 编解码、RAF、触摸和前后台生命周期接口。TapTap 的微信兼容宿主复用同一层。

## 接入

宿主必须在 Three/Pixi/LIPS 的导入之前初始化：

```ts
import { wechatHost } from './adapters/wechat/src/host';
import { installPixiMiniGameAdapter } from './adapters/wechat/src/pixi';
import { startApplication } from './application';

installPixiMiniGameAdapter(wechatHost);
startApplication(wechatHost);
```

Vite 配置使用 `build/pixi-intl-guard.ts` 的 `pixiIntlGuard()`。
只有使用当前固定 LIPS 版本的 IIFE 构建才启用 `build/lips-iife-metadata.ts` 的 `lipsIifeMetadata()`。
根目录的 `vite.wechat.config.ts` 是实际接入样例，游戏入口路径、AppID 和输出目录由调用项目决定。

## 上游和本地修改记录

| 来源 | 本地处理 | 是否修改原始 vendor |
| --- | --- | --- |
| finscn/weapp-adapter，固定 Git tree，MIT | 选择性使用 Canvas、HTMLElement、EventTarget；来源和哈希见 vendor README | 否 |
| fast-text-encoding 1.0.6，Apache-2.0 | 缺少原生接口时提供 UTF-8 编解码；入口先建立 global window | 否 |
| UTF-8 宿主边界 | 孤立 UTF-16 代理字符转为 U+FFFD，使 encode 与原生接口一致 | 本地 `src/text-encoding.ts` |
| Pixi 8.20.1 | 构建时修正 `typeof Intl?.Segmenter` 为先判断 Intl 存在，再使用自带 fallback | 构建转换；不改 node_modules |
| LIPS 1.0.0-beta.23.1 | IIFE 文档元数据 helper 增加字符串类型判断 | 构建转换；不改 node_modules |
| 小游戏 DOM 子集 | 补齐缺失或不完整 document 上明确声明的方法，不以对象存在作为能力证据 | 本地 `src/document.ts` |
| Canvas / WebGL / 输入 / 生命周期 | 第一 Canvas 保留给屏幕；2D 离屏不足时用后续 Canvas；要求 WebGL2；显式触摸与前后台通知 | 本地 `src/host.ts`、`src/pixi.ts` |

不伪造完整 Intl，不引入平台 UI 尺寸分支。构建修正会在固定补丁位置变化时失败，要求审阅依赖升级。
当前版本的 text codec 仅支持 UTF-8，不承诺 TextDecoder 的全部编码和流式选项。
DOM 只用于当前渲染/解释器的接口契约，不支持实际网页布局或任意选择器查询。

## 验证

项目测试覆盖 vendor 原始字节、UTF-8 编解码、不完整 document，以及完整编译包的渲染和生命周期。
TapTap 回归组合同时删除 Intl、TextEncoder、TextDecoder 和 document 查询方法，并关闭 offscreen API。
模拟浏览器仍提供原生 WebGL2、Promise 等能力；后续真机错误必须继续补充契约和回归，不能宣称完全模拟宿主。
