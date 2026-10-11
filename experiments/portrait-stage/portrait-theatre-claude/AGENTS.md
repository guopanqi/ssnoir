# AGENTS — 本实验的接手说明

先读 [README.md](README.md) 的目录与工作流。上级 `AGENTS.md` 的约定同样适用（直接在 `main`、不过度设计、不打兼容补丁）。

## 架构要点
- 无框架、无打包器；`build.mjs` 只是把 `src/js/*.js` 按文件名顺序拼进同一个全局作用域。**文件名前缀即依赖顺序**：`<50` 的是基础层，`chapters/` 目录内按文件名排序组成 `const SC=[C1, …]`（C1、C7 两个共用对象定义在 45-chapter-objects.js，由 01-rain-night.js、09-lamp-quarrel.js 引用），`>=60` 是启动层。
- 新增章节：在 `src/js/chapters/` 加 `NN-name.js`，内容是**一个以 `,` 结尾的对象字面量** `{n,sub,sh,pos,bg,set,mk,ini,run}`；章节编号文字数组在 `60-boot.js` 的 `s.num=[…][i]`，要同步加。布景加在 `40-sets.js` 的 `SETS.xxx`。
- 章节对象：`n` 名称、`sub` 副标题、`sh` 按钮短名、`pos` 地图点位(世界坐标)、`bg` 舞台底色、`set` 布景容器 id（`#hotel/#lamp/#set3`）、`mk` 生成布景 SVG、`ini()` 初始人物位置、`run(k,H)` 异步演出时间线，`H` 提供 `w/say/go/fl/n/y/mono…`。
- 人物：`CH` 里定义，`P(id,{x,y,s,f,face,b,h,al,ar,ll,lr,cl:{…},show…})` 改姿势。**`show:1` 才可见**（ini 里漏写则人物隐身——追逐章曾因此看不到尼尔）。
- 定格（立绘式）表演：给人物加 `snap` 类（`.ch.snap` 关闭过渡），直接切姿势表，不做关节补间；见 `chapters/04-chase.js` 的 `pose/cyc/RUN/PS`。其他章仍是补间动画，需要时可迁移。
- 暂停机制：`sleep(ms,k)` 轮询 `paused` 与 `gen` 令牌；任何新的异步等待必须用 `w()`，不要裸 `setTimeout`/`await` 否则暂停与切章会失效（`throw 0` 即被取消）。

## 踩过的坑
- **SVG id 必须全局唯一**：渐变 `mg` 曾与人物 id 冲突导致整页点击无反应。新增 id 前全局搜索。
- 布景线条类 `.set path` 有统一样式（描边、dash 出现动画、`.lit` 点亮）；新增形状用 `LL()/RR()` 助手以带上 `pathLength=1` 与 `--d` 延迟。
- `@keyframes shk` 曾缺失，震屏无效；现已在 style.css。
- 地图字/邮箱等布景上不要放中文字形（用户明确要求去掉）。

## 设计准则（用户已确认，勿违背）
见 [docs/DESIGN.md](docs/DESIGN.md)。要点：黑色电影、成年人物；布景极简，每地点只一个标志物；**桌上吊灯只属于尼尔的家**；第 1 章雨夜在尼尔公寓楼下，不是酒店；对白以 `docs/story-script.txt` 为准，设计备注不上台。

## 验证清单（改动后）
1. `node build.mjs --check`；2. `test_play.py … full` 无 JS 错误；3. 对改动的章节跑 `sheet`/`realtime_capture` 看图；4. 提交 `src/` 与 `dist/`；5. 如需更新在线版，用 Artifact 工具对上面 README 的 URL 重新发布 `dist` 文件。
