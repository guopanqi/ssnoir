# 可复用的 TapTap 转换与启动适配层

本目录不引用 SSNoir 内容、计数器或固定 AppID。微信兼容宿主的运行时接口由 `../wechat` 提供。

## 转换准备好的微信目录

```sh
node adapters/taptap/convert.mjs --source /path/to/prepared-wx-game --target /path/to/taptap-output
```

或导入 `convertTapTap({source, target, binDir})`。默认从调用工程 `node_modules/.bin` 找 Babel/terser。
需要 Python 3 和 `package.json` 声明的 Babel/terser 同版本依赖。
目标目录是生成物目录，转换前会清空；输入和输出必须独立，不能互相包含。

输入至少包含 `game.js`、`game.json`、`project.config.json`；`project.config.json.appid` 必须是调用项目的 TapTap ID。
VS Code 调试插件读取 `game.json.appId`，调用项目应使两处 ID 一致。
输出仍是原版转换器生成的 `game/game.js`、资源及 `game.zip`，不由本工具另行重打包。

## 可复用启动入口

```js
import { createTapTapEntry } from './adapters/taptap/startup.mjs';
const entry = createTapTapEntry('./application.js', {title: '应用启动失败', tag: 'App startup'});
// 将 entry 写为 game.js，将应用 bundle 写为 application.js，再转换。
```

生成 ES5 入口，先注册 `wx.onError`，再 require 应用；同步加载错误和模块解析错误显示名称、消息和栈。
它无法捕获入口执行前的平台拒绝、原生宿主崩溃或操作系统终止。

## 原版与修改记录

原版 Python 转换器为用户提供的 2.0.5；SHA-256、来源和支持文件差异见
[vendor/converter/README.md](vendor/converter/README.md)。Python 源码未修改，调用前校验哈希。

本地增加通用 CLI/API、输入校验、隔离工具目录、启动入口生成器。
在生成工具目录中将 Babel preset 写成安装位置的绝对路径，使输出可以位于调用项目以外；
使用同一版本和语义的 preset，保留原始 `.babelrc` 不变。这是调用层配置处理，不是 Python 转换器补丁。
工具目录还连接调用工程的 `node_modules`，并禁止 npx 在线安装，确保原版 `npx babel` 调用
实际使用声明的本地版本，避免在外部输出目录中意外解析到旧 Babel。

只复制当前无插件路径所需的支持文件；未包含原始压缩包的 Unity cached plugins。
使用微信插件或 Unity 时必须另行审阅插件载荷；当前通用入口不声称支持这两类输入。
基础库、字体、GPU、权限和真机交互仍需实际验收，Babel 和 ZIP 成功不能代替。
