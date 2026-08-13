#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Playtest
{
    /// <summary>
    /// 无头交锋驱动：起局、列出所有合法投骰、执行、结束回合、读结果。
    ///
    /// 它替调用者做掉三件每次都会写错的事：从渲染树里挑出真正可执行的卡、
    /// 把骰子拼成合法的 SlottedResource（骰位编号不是列表下标，且骰子花掉后列表会移位）、
    /// 判断这一场是否已经结算。
    ///
    /// **观测只走渲染树**——玩家看得见什么，这里就读得到什么，不去 Eval 脚本内部变量。
    /// 这条约束让驱动不跟着每一场的实现变；反过来，某个状态如果这里读不出来，
    /// 说明玩家也读不出来，那是那一场的设计缺陷，不是驱动的缺陷。
    /// </summary>
    public sealed class EncounterDriver
    {
        public SceneManager Scenes { get; }
        public GameState State { get; }
        /// <summary>交锋自己的场景名。回到别的场景就说明它已经结算了。</summary>
        public string EncounterScene { get; }
        public int Turn { get; private set; } = 1;

        private EncounterDriver(SceneManager scenes, GameState state, string encounterScene)
        {
            Scenes = scenes;
            State = state;
            EncounterScene = encounterScene;
        }

        /// <summary>
        /// 起一局。entry 是 Scheme 表达式（例如 "(dock-collapse 'debug-enter!)"）时，
        /// 从世界那头真的走一遍入场流程，交锋的返回值会经过世界模块结算；
        /// 是场景名（例如 "encounters/combat"）时直接载入，适合还没接进城市的交锋。
        /// </summary>
        public static EncounterDriver Enter(string entry, PlaytestSetup setup)
        {
            if (setup.Seed != null) GameRandom.Reseed(setup.Seed.Value);

            var state = new GameState();
            var scenes = new SceneManager(state, new LocalScriptLoader());
            setup.ApplyTo(state);

            if (entry.TrimStart().StartsWith("("))
            {
                scenes.LoadScene("world");
                setup.ApplyTo(state);   // 世界载入会重置队伍，起手条件要在它之后再落一次
                scenes.ActiveInterpreter.Eval(entry);
            }
            else
            {
                scenes.LoadScene(entry);
                setup.ApplyTo(state);
                state.Team.RollActionDice(isInEncounter: true, consumeHangover: false);
            }

            string scene = scenes.CurrentSceneName;
            if (scene.Equals("world", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"入场表达式没有把场面切进交锋：{entry}");
            return new EncounterDriver(scenes, state, scene);
        }

        public bool Finished => !Scenes.CurrentSceneName.Equals(EncounterScene, StringComparison.OrdinalIgnoreCase);

        /// <summary>交锋 end-encounter 交回来的值。</summary>
        public object? Result => Scenes.LastEncounterResult;

        /// <summary>此刻能做的每一件事：每张可执行的卡 × 每个人手上的每一颗骰。
        /// 不吃骰的卡（道具、免费动作）出现一次，SlotId 为 -1。</summary>
        public IReadOnlyList<Move> LegalMoves()
        {
            var moves = new List<Move>();
            foreach (var card in ExecutableCards())
            {
                int dieCosts = card.Requires?.Count(r => r.Type == "die") ?? 0;
                if (dieCosts == 0)
                {
                    if ((card.Requires?.Count ?? 0) == 0)
                        moves.Add(new Move(card, string.Empty, -1, 0, 0));
                    continue;   // 需要物品的卡由调用者另行处理，驱动不替它挑背包
                }
                if (dieCosts > 1) continue;   // 目前没有一张卡要两颗骰；真出现了再扩

                string skill = card.Resolve!.Type == ResolveType.Roll ? card.Resolve.SkillName : string.Empty;
                foreach (var actor in State.Team.Actors)
                {
                    if (!TeamState.IsOnStage(actor, isInEncounter: true)) continue;
                    for (int i = 0; i < actor.ActionDice.Count; i++)
                    {
                        int prepared = skill.Length == 0
                            ? actor.ActionDice[i]
                            : FateStrip.PreparedValue(actor.ActionDice[i], actor.Stats[skill], 0);
                        moves.Add(new Move(card, actor.Id, actor.ActionDiceSlotIds[i], actor.ActionDice[i], prepared));
                    }
                }
            }
            return moves;
        }

        public ActionReport Play(Move move)
        {
            var slots = new List<SlottedResource?>();
            if (move.NeedsDie)
                slots.Add(new SlottedResource
                {
                    Type = "die",
                    Value = move.DieValue,
                    ActorId = move.ActorId,
                    DieIndex = move.SlotId,
                    SourceIndex = move.SlotId
                });
            return Scenes.ExecuteAction(move.Node, slots);
        }

        /// <summary>用掉一件交锋消耗品（香烟、酒）。</summary>
        public ActionReport UseConsumable(string itemId) => Scenes.UseEncounterConsumable(itemId);

        public ActionReport EndTurn()
        {
            var report = Scenes.EndTurn();
            Turn++;
            return report;
        }

        /// <summary>玩家此刻看得见的一屏：卡名、副标题、便签、时钟、这张卡考什么技能。</summary>
        public string Observe()
        {
            if (Scenes.CurrentRootNode == null) return "（空场面）";
            var text = new StringBuilder();
            Describe(Scenes.CurrentRootNode, 0, text);
            foreach (var actor in State.Team.Actors)
            {
                if (!TeamState.IsOnStage(actor, isInEncounter: true)) continue;
                text.Append($"[{actor.Name}] 骰 {string.Join(",", actor.ActionDice)}")
                    .Append($"　力量{actor.Stats["violence"]} 见识{actor.Stats["knowledge"]}")
                    .Append($" 敏锐{actor.Stats["sharpness"]} 交际{actor.Stats["social"]}");
                if (actor.Role == "protagonist")
                    text.Append($"　冷静 {actor.Composure}/{TeamState.MaxComposure} 伤势 {State.Team.Injury.Severity}");
                text.AppendLine();
            }
            return text.ToString();
        }

        private void Describe(GameNode node, int depth, StringBuilder text)
        {
            string pad = new string(' ', depth * 2);
            text.Append(pad).Append(node.Name);
            if (node.Resolve?.Type == ResolveType.Roll)
                text.Append($" <{node.Resolve.SkillName}>");
            if (node.Tags != null && node.Tags.Count > 0)
                text.Append(" [").Append(string.Join(",", node.Tags)).Append(']');
            if (node.Disabled) text.Append(" (不可用)");
            foreach (var clock in node.Clocks)
                text.Append($" {{{clock.Label} {clock.Current}/{clock.Max}}}");
            if (node.Resolve?.Type == ResolveType.Clock && node.Resolve.Clock != null)
                text.Append($" {{{node.Resolve.Clock.Label} {node.Resolve.Clock.Current}/{node.Resolve.Clock.Max}}}");
            text.AppendLine();
            if (!string.IsNullOrEmpty(node.Subtitle))
                text.Append(pad).Append("    « ").Append(node.Subtitle).AppendLine(" »");
            foreach (var child in node.Children)
                Describe(child, depth + 1, text);
        }

        private IEnumerable<GameNode> ExecutableCards()
        {
            var found = new List<GameNode>();
            if (Scenes.CurrentRootNode != null) Collect(Scenes.CurrentRootNode, found);
            return found;
        }

        private static void Collect(GameNode node, List<GameNode> found)
        {
            if (node.Resolve != null && !node.Disabled
                && node.Resolve.Type != ResolveType.Observe && node.Resolve.Type != ResolveType.Clock)
                found.Add(node);
            foreach (var child in node.Children) Collect(child, found);
        }
    }

    /// <summary>一次投骰：把哪个人的哪一颗骰放到哪张卡上。Prepared 是准备值，供策略排序。</summary>
    public sealed record Move(GameNode Node, string ActorId, int SlotId, int DieValue, int Prepared)
    {
        public bool NeedsDie => SlotId >= 0;

        public string Describe(GameState state)
        {
            if (!NeedsDie) return $"{Node.Name}（不吃骰）";
            string who = state.Team.FindActor(ActorId)?.Name ?? ActorId;
            return $"{who} 的 {DieValue} → {Node.Name}（准备值 {Prepared}）";
        }
    }

    /// <summary>起局条件：进场时的技能、背包、随机种子。</summary>
    public sealed record PlaytestSetup
    {
        /// <summary>四项技能统一设成这个等级，代表玩家当前的成长水平。</summary>
        public int Growth { get; init; } = 1;
        public IReadOnlyDictionary<string, int> Items { get; init; } = new Dictionary<string, int>();
        public int? Seed { get; init; }

        public void ApplyTo(GameState state)
        {
            var player = state.Team.FindActor("player")
                ?? throw new InvalidOperationException("队伍里没有主角。");
            foreach (string stat in new[] { "violence", "knowledge", "sharpness", "social" })
                player.Stats[stat] = Growth;
            foreach (var item in Items)
                state.Inventory.SetCount(item.Key, item.Value);
        }
    }
}
