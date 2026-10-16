#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    /// <summary>教程窗口打开时要圈出来的那块地方。具体矩形由 HandPanelDrawer / 渲染器提供。</summary>
    public enum TutorialHighlight
    {
        None,
        /// <summary>左下角的行动骰 + 网格里的第一张动作卡。</summary>
        ActionDiceAndCard,
        /// <summary>主角骰子上方的冷静 / 伤势读数。</summary>
        Vitals,
        /// <summary>右下角的功能键（城里是「回家」）。</summary>
        FunctionKey,
    }

    public sealed class TutorialEntry
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>正文按段落写。一段一句话，不要塞成一大坨——窗口里只讲一件事。</summary>
        public string[] Paragraphs { get; set; } = System.Array.Empty<string>();
        public TutorialHighlight Highlight { get; set; } = TutorialHighlight.None;
    }

    /// <summary>
    /// 教程文案的唯一来源。**弹窗和帮助面板读的是同一份**——分成两份，改了一处忘了另一处
    /// 是迟早的事。
    ///
    /// 这里只有内容和它该在什么时候出现（触发条件写在 <see cref="TutorialDirector"/>）。
    /// 以后换成正式版教程时，重写的是画法那一层（TutorialDrawer），这份文案原样搬过去。
    /// </summary>
    public static class TutorialLibrary
    {
        public const string ActionDice = "action-dice";
        public const string Composure = "composure";
        public const string Injury = "injury";
        public const string EndOfDay = "end-of-day";

        public static readonly TutorialEntry[] Entries =
        {
            new TutorialEntry
            {
                Id = ActionDice,
                Title = "行动骰",
                Paragraphs = new[]
                {
                    "身体完好时每天有四颗行动骰。把一颗骰子拖拽到动作中来做一件事情。",
                    "不同点数的骰子代表不同质量的行动，它会影响动作的结果好坏。",
                },
                Highlight = TutorialHighlight.ActionDiceAndCard,
            },
            new TutorialEntry
            {
                Id = Composure,
                Title = "冷静影响骰子",
                Paragraphs = new[]
                {
                    "冷静降到 3 点或更低，下次发骰时每颗点数减少 1；降到 1 点或以下，减少 2。",
                    "已经拿在手里的骰子不变。冷静耗尽后继续承受压力，会转成身体伤势。",
                    "去家里花一颗骰子静坐，或在酒馆花钱喝酒可以恢复冷静；睡觉不自动恢复。",
                },
                Highlight = TutorialHighlight.Vitals,
            },
            new TutorialEntry
            {
                Id = Injury,
                Title = "伤势影响行动次数",
                Paragraphs = new[]
                {
                    "轻伤会封锁一颗骰子，重伤会封锁两颗。已封锁的骰位会留在手牌上。",
                    "睡两晚可缓和 1 点伤势，也可以在诊所治疗、用药或主动养伤。",
                    "治疗不会返还今天已失去的骰子；下一次发骰会按新的伤势计算数量。",
                },
                Highlight = TutorialHighlight.Vitals,
            },
            new TutorialEntry
            {
                Id = EndOfDay,
                Title = "结束这一天",
                Paragraphs = new[]
                {
                    "点击「回家」或者「家」回到家里，点击「睡觉」来结束这一天。",
                    "新的一天会刷新你所有的行动骰子，推进世界时间，触发新的事件。",
                },
                Highlight = TutorialHighlight.FunctionKey,
            },
        };

        public static TutorialEntry? Find(string id)
        {
            foreach (var entry in Entries)
                if (entry.Id == id) return entry;
            return null;
        }
    }

    /// <summary>
    /// 教程开关与「看过没有」。
    ///
    /// 状态放在当前 GameState 的纯全局里，由 SaveManager 随存档保存；新开局清空，读档恢复。
    /// </summary>
    public static class TutorialState
    {
        private const string EnabledKey = "tutorial:enabled";
        private const string SeenKeyPrefix = "tutorial:seen:";
        private static GameState? _gameState;

        public static void Bind(GameState gameState)
        {
            _gameState = gameState;
        }

        private static GameState CurrentGameState =>
            _gameState ?? throw new System.InvalidOperationException("TutorialState 未绑定 GameState。");

        public static bool Enabled
        {
            get => CurrentGameState.Get(EnabledKey, false);
            set
            {
                bool was = Enabled;
                CurrentGameState.Set(EnabledKey, value);
                // 关掉再打开 = 从头再来一遍；这个变化也会随当前存档保存。
                if (value && !was) ResetSeen();
            }
        }

        public static bool HasSeen(string id) => CurrentGameState.Get(SeenKeyPrefix + id, false);

        public static void MarkSeen(string id)
        {
            CurrentGameState.Set(SeenKeyPrefix + id, true);
        }

        public static void ResetSeen()
        {
            foreach (var entry in TutorialLibrary.Entries)
                CurrentGameState.Set(SeenKeyPrefix + entry.Id, false);
        }
    }
}
