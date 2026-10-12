# SSNoir：三个独立 GPT-6 玩法研究 Agent

这套工程是真正的 OpenAI GPT-6 Astra Agent SDK 模型会话，**不是**之前的三条确定性 Python 数学 CI Job。每条研究线独立启动、独立调用工具、独立保存报告；结果仍要经过协调者审查。

## 启动

1. 在仓库 guopanqi/ssnoir 的 Settings → Secrets and variables → Actions 中新增 Repository secret：OPENAI_API_KEY。不要把密钥贴进仓库、聊天或工作流输入。
2. 打开 [Three GPT-6 Research Agents](https://github.com/guopanqi/ssnoir/actions/workflows/ssnoir-gpt6-agents.yml)，手动选择 Run workflow。
3. 运行产物为三个独立的 ssnoir-gpt6-pressure、ssnoir-gpt6-route、ssnoir-gpt6-information Artifact，以及一个 ssnoir-gpt6-independent-verification。
4. 由协调者审阅后再更新共享研究记忆。此工作流绝不会自动改写 Unity/Scheme 原版或 Debug 试玩白名单。

运行模型固定为 gpt-6-astra。OpenAI API 与 ChatGPT 订阅**单独计费**，账户需有相应 API 模型权限和余额。工作流只接受**手动触发**，不在每次推送时花费模型调用费用。

## 三个工作者

- pressure：R106 同频率成功/失败产生问题是否改变修补选择。
- route：R107 不可逆封门后，修梯的净容量与改道时机。
- information：R108 自然线索与专门探查是否能被简单优先规则解释。

每个工作者使用独立 Runner.run 会话（最多12次工具往返），拥有 read_research_file、run_reviewed_baseline、submit_paper_experiment 三个工具。可以阅读项目研究资料、重新执行审阅过的历史基线，以及提出新的可证伪数学模型与代码。其它工作者的私人产物不在它的上下文中。

## 安全与证据

LLM 生成的 experiment.py **不在持有 API Secret 的 Job 内执行**；新代码只在另一个不含 Secret、只读 GitHub 权限的 Job 中运行，受 AST 导入白名单、CPU、内存和时长上限约束。每份代码必须输出 hypothesis、baseline、counterfactual、results、verdict 字段。代码成功运行不是数学定理、正式 C# 因果，也不是真人乐趣证据。

框架的无密钥测试在 [Agent Harness Preflight](https://github.com/guopanqi/ssnoir/actions/workflows/ssnoir-gpt6-agents-ci.yml) 中自动执行；它只验证工程，**不会产生 GPT-6 研究结论**。

完整协作约束见 [并行研究协议](../../docs/实验/机制研究/并行研究协议.md)。先让三条真实 API 研究跑通并审阅，之后再考虑持续循环、相互批评与无人值守的自动 PR；不要为追求轮数而运行无成本上限的自主循环。
