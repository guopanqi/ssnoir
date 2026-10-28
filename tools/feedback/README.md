# 游戏反馈

游戏右上角的「反馈」打开居中纸张弹窗，标题「意见反馈」，输入提示「请输入反馈内容」。
输入 1–2000 个 UTF-16 单元的文字后提交，自动附上版本、显示中的地点 / 导航路径、是否Scenario和世界天数。
不收集设备标识、联系方式、完整存档或截图。关闭窗口保留本次会话的草稿；退出游戏不保留。
提交成功以服务端入库回执为准，失败保留文字与提交 ID，原样重试不会重复记录。

## 线上位置与查看

- Worker：`ssnoir-feedback`
- 接收地址：`https://ssnoir-feedback.guopanqi.workers.dev/feedback`
- D1：`ssnoir-feedback`，表 `feedback`
- Unity 配置：[FeedbackConfig.json](../../UnityClient/Assets/Resources/FeedbackConfig.json)

登录 Cloudflare 控制台 → Storage & databases → D1 → `ssnoir-feedback` → Console，运行：

```sql
SELECT created_at, text, version, location, in_scenario, world_day
FROM feedback ORDER BY created_at DESC LIMIT 100;
```

也可从本目录查阅或导出 JSON（账号登录后）：

```bash
npx wrangler d1 execute ssnoir-feedback --remote --json \
  --command 'SELECT * FROM feedback ORDER BY created_at DESC' > feedback-export.json
```

记录时间为 UTC。接口匿名公开写入，不提供读取接口；读库需要 Cloudflare 账号权限。
游戏内不放任何 Cloudflare 凭据。接口限制单次请求 16 KiB，同 IP 每分钟约 30 次提交
（Cloudflare 节点内的近似限流，多个玩家共用网络时共享额度），并校验全部字段。
这用于小规模试玩，不是严格的身份认证或抗分布式滥用系统。

## 开发、验证和发布

```bash
npm ci
npm run check
npm test
# 本地手动检查
npx wrangler d1 migrations apply ssnoir-feedback --local
npm run dev
# 修改后发布
npx wrangler deploy --dry-run
npx wrangler d1 migrations apply ssnoir-feedback --remote
npm run deploy
```

`npm test` 自动启动本地 Worker，使用真实本地 D1 检查中文入库、CORS、输入约束、
重试去重、冲突和限流，并清理自己的测试记录。线上 D1 使用独立数据库 ID，不能把本地通过当作线上通过。

Unity 侧编译与交互验收遵循 [VERIFYING.md](../VERIFYING.md)。人工检查：
打开弹窗后卡片和镜头不接输入；空白内容不能提交；中文、换行、长文字可编辑；
关闭再开保留草稿；失败保留文字；成功提示收到且 D1 中只有一条；英文启动语言显示英文界面。
TapTap 真机另需确认 SDK 键盘输入、键盘收起和联网提交，并在宿主后台允许该反馈域名的请求。

发布时 Regular 字体保留源字体支持的完整字库，避免自由输入汉字变成缺字；
SemiBold 标题字体继续裁剪。只修改 staging 字体，不改主工程源资源。
正文发布字体约 13.5 MiB（压缩前），因此包体会比只按内容裁剪时增大。
