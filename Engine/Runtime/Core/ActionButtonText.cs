#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    /// <summary>内容按钮的受控动词；可用性不改变动词，执行与判定进度由客户端表现。</summary>
    public static class ActionButtonText
    {
        private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
        {
            ["执行"] = "Act", ["查看"] = "Inspect", ["交谈"] = "Talk",
            ["接受"] = "Accept", ["拒绝"] = "Decline", ["交付"] = "Deliver",
            // 人员安排与已有风险确认分支的专用词，不扩充常规动词。
            ["委派"] = "Assign", ["解除"] = "Release", ["仍然开始"] = "Begin anyway"
        };

        public static string For(GameNode node)
            => node.ButtonText ?? (node.Resolve?.Type == ResolveType.Observe ? "查看" : "执行");

        public static void Validate(string text)
        {
            if (!Labels.ContainsKey(text))
                throw new InvalidOperationException("未知动作按钮文字：" + text + "。可用：" + string.Join("、", Labels.Keys));
        }

        public static string Display(string text)
        {
            Validate(text);
            return GameLanguage.Tr(text, Labels[text]);
        }
    }
}
