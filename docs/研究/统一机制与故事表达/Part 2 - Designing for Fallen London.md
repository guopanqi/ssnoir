# Fallen London Writer Guidelines: Part II — Designing for Fallen London

来源：Failbetter Games 官方博客，2015-04-15　·　原文：<https://www.failbettergames.com/news/fallen-london-writer-guidelines-part-ii>

**术语对照**：Quality 状态变量；Quality notes 状态备注；QLD (Quality Level Description) 状态等级描述——即状态某个等级的显示名，如丑闻 1 级显示为「被怀疑」；Linked Events 链接事件（自动串联的事件链）；Perhaps Not 「还是算了」（FL 里退出事件链的兜底选项）；Must Events 必然事件（强制带着状态/场景变更走的事件）。

---

## In the previous post we shared the guidelines for the pitching and writing in-game content. This time, we're sharing best-practice guidelines for designing for Fallen London. This is an evolving process. We've learned a lot in the six years since Fallen London's inception, and no doubt we'll continue to adapt and improve these practices in future.

上一篇我们分享了游戏内容提案与撰写的指南。这一篇是《面向 Fallen London 做设计》的最佳实践指南。这是一个持续演进的过程：自 Fallen London 诞生六年来的经验教会了我们很多，未来无疑还会继续调整与改进这些做法。

## Designing for Fallen London

### Quality Parsimony

Quality parsimony means using as few unique qualities as possible while maintaining clarity and stability. Excess qualities complicate the economy, cause bugs, and clutter up the UI for us and the players.

状态精简（quality parsimony）指在保持清晰与稳定的前提下，用尽可能少的独立状态变量。多余的状态会让经济系统变得复杂、引发 bug，并把 UI 界面弄得杂乱——对我们和玩家都是。

Sometimes an extra quality makes a piece of content much less fragile or much more engaging, but where possible keep new qualities to a minimum. **Two qualities tends to be the magic minimum number for any content with an engaging interactive structure - otherwise it's a simplistic chain or a grind.**

有时多加一个状态，确实能让一段内容不那么脆弱，或更有参与感；但在可能的情况下，新状态要尽量少。**任何拥有引人入胜的交互结构的内容，两个状态往往是下限的"魔法数字"——否则它就只是条简单链，或者只是一段刷。**

### Quality notes

Ensure Quality notes are complete, succinct and coherent. Use paragraphs and urls to make make them easier to read and use.

确保状态备注完整、精炼、连贯。多用分段和链接，让它们更容易读、更容易用。

**Imagine reading quality notes as if you were coming back to them a year from now after writing tens of thousands of words of other content. Now imagine a total stranger reading them a year from now. Now imagine the stranger has a short temper, an ice pick, and your address.**

**设想一年之后，你写完了几万字的其他内容，回头再读这些状态备注——会是什么感觉。再设想一个完全的陌生人在一年后读它们。再设想这个陌生人脾气暴躁、手里握着一把凿冰锥，而且知道你家住哪儿。**

### 

Rules for which qualities are best to give as rewards are complex and change over time. Use the Economy spreadsheet to calculate rewards and discuss your proposals in the Content channel on Slack so the team can help.

用哪些状态做奖励最合适，规则复杂，且会随时间变化。用经济电子表格计算奖励，并把你的方案拿到 Slack 的 Content 频道讨论，让团队帮忙把关。

It's fine (often preferable) to hold off giving rewards for a while, then grant them in a chunk at the end of a content strand.

把奖励先扣着不发，等到一段内容线的末尾一次性发放，是完全可以的——而且往往更好。

### Sporadic Play

Remember that people play FL in spurts, and sometimes take leaves of absence. Provide reminders in content so players can reorient themselves. Use QLDs to point them where they need to go next, or reminder branches on a story's core cards to point them in the right direction. Use [advisory text in square brackets] to provide the player with emphasised, explicit guidance and warnings.

记住：玩家是一阵一阵地玩 FL 的，有时会离开一段日子。要在内容里提供记忆锚点，让玩家重新定向。用状态等级描述（QLD）提示玩家接下来该去哪，或者在故事的核心卡上挂"提醒分支"为其指路。用[方括号内的提示文本]给玩家明确的、被强化的引导与警告。

### Linked Events

Linked Events are fantastically useful! They can also break the game. Plan for players who use Perhaps Not or another method to back out of a chain of linked events. Make sure they can get back in, or ensure the game will put them back on track.

链接事件非常有用，但也能把游戏玩坏。要为那些用"还是算了"或其他方式从链接事件链中退出的玩家做规划。要么保证他们能重新进入，要么保证游戏会自动把他们送回轨道。

### Must Events

The same warning applies here. Use Must Events sparingly to ensure a quality, area or setting change happens exactly when you need it to. Use min and max requirements to prevent a player from getting trapped in a Must Event.

同样的警告在这里也适用。必然事件要少用，只用于确保某个状态、区域或场景的变更恰好在你需要的时机发生。用上下限要求（min/max）防止玩家被困在必然事件里出不来。

### Use quality changes to make non-player characters more tangible

When you can see consequence from a character while interacting with them, it immediately makes them more tangible. Menaces are good for this - like Wounds or Scandal. Be cautious - a string of quality changes in a result makes each one less important.

当你在和一个角色互动时能看见"自己的行为正在他身上留下后果"，这个角色立刻就变得真实可感。威胁状态很适合做这个——比如创伤或丑闻。但要克制：一个结算里连着一长串状态变化，每一个的分量就都会被稀释。
