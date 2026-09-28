#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // Fixed interface copy lives here. Content text and stable data IDs remain in Scheme.
    public static class UiText
    {
        private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
        {
            [" + 修正 "] = " + Modifier ",
            [" − 修正 "] = " − Modifier ",
            [" ＝ 骰 "] = " = Die ",
            ["< 返 回"] = "< Back",
            ["不可用"] = "Unavailable",
            ["交锋"] = "Encounter",
            ["从存档加载"] = "Continue",
            ["低风险"] = "Low Risk",
            ["准备值 "] = "Ready ",
            ["判定中"] = "Rolling",
            ["判定中性"] = "Mixed Result",
            ["判定失败"] = "Failure",
            ["判定成功"] = "Success",
            ["判定结果"] = "Result",
            ["卷宗"] = "Dossier",
            ["命运骰滚动..."] = "Fate die rolling...",
            ["回 家"] = "Home",
            ["帮助"] = "Help",
            ["待 命"] = "Ready",
            ["成长"] = "Growth",
            ["执 行"] = "Execute",
            ["执行中"] = "Executing",
            ["新游戏"] = "New Game",
            ["未加载"] = "Not Loaded",
            ["查 看"] = "Inspect",
            ["结束回合"] = "End Turn",
            ["结算中"] = "Resolving",
            ["行动结果"] = "Action Result",
            ["设置"] = "Settings",
            ["调试"] = "Debug",
            ["贝尔维尔的歌谣"] = "Ballad of Belleville",
            ["退出游戏"] = "Quit",
            ["高风险"] = "High Risk",
            ["必须处理"] = "Required",
            ["点击返回"] = "Tap to return",
            ["冷静"] = "Composure",
            ["见底"] = "Empty",
            ["酒"] = "Drink",
            ["烟"] = "Cig.",
            ["药"] = "Meds",
            ["准备"] = "Ready",
            ["力量"] = "Force",
            ["见识"] = "Knowledge",
            ["敏锐"] = "Insight",
            ["交际"] = "Social",
            ["游戏已存档。"] = "Game saved.",
            ["游戏已读档。"] = "Game loaded.",
            ["没有找到存档文件。"] = "Save file not found.",
            ["存档失败"] = "Save failed",
            ["读档失败"] = "Load failed",
        };

        public static string Get(string chinese)
        {
            if (!English.TryGetValue(chinese, out string? english))
                throw new ArgumentException($"Unknown UI text: {chinese}");
            return GameLanguage.Current == GameLanguage.English ? english : chinese;
        }

        public static string SkillName(string key) => Get(key switch
        {
            "violence" => "力量",
            "knowledge" => "见识",
            "sharpness" => "敏锐",
            "social" => "交际",
            _ => throw new ArgumentException($"Unknown skill: {key}")
        });

        public static string GrowthUpgrade(string actorName, string skillKey, int value) =>
            GameLanguage.Current == GameLanguage.English
                ? $"{actorName}: {SkillName(skillKey)} increased to {value}"
                : $"{actorName}的{SkillName(skillKey)}提升到 {value}";

        public static string Day(int value) => GameLanguage.Current == GameLanguage.English ? $"Day {value}" : $"第 {value} 天";

        public static string MoreEffects(int count) => GameLanguage.Current == GameLanguage.English
            ? $"+ {count} more effects..." : $"+ 还有 {count} 项影响...";

        public static string Ready(int value) => $"{Get("准备")} {value}";
    }
}
