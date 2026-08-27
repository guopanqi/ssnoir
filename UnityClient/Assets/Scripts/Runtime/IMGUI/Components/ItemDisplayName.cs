#nullable enable
using System.Collections.Generic;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 物品在格子底下那一行显示成什么。
    ///
    /// 为什么要这一层：物品的**内部名**同时是内容脚本里的键（"莱恩的底片与照片"），它得
    /// 写全、写准，好让写内容的人一眼知道指的是哪件东西；但格子只有 50px 宽，写全就是一团
    /// 糊字。所以这里做一次**只影响显示**的收缩，内部名一个字都不动。
    ///
    /// 收缩的规矩：先按表；表里没有的走通用规则（去掉「」引号、去掉"谁谁的"这个前缀），
    /// 还是太长就截断加省略号。表是给那些"通用规则会砍错重点"的名字准备的——
    /// 比如"莱恩的底片与照片"，重点是底片，不是照片。
    /// </summary>
    public static class ItemDisplayName
    {
        /// <summary>格子底下那行最多几个字。再多就换行/挤成一团，不如截断。</summary>
        private const int MaxChars = 5;

        private static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>
        {
            ["半包「老金牌」"] = "老金牌",
            ["奥托的地址纸条"] = "地址纸条",
            ["莱恩的底片与照片"] = "底片",     // 通用规则会留下"底片与照片"，五个字里三个是废的
            ["莱恩的照片与信"] = "照片与信",
        };

        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        public static string Short(string? itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return string.Empty;
            if (_cache.TryGetValue(itemName!, out var hit)) return hit;

            string result = Compute(itemName!);
            _cache[itemName!] = result;
            return result;
        }

        private static string Compute(string name)
        {
            if (Overrides.TryGetValue(name, out var overridden)) return overridden;

            string s = name.Replace("「", "").Replace("」", "");

            // "奥托的地址纸条" → "地址纸条"：属主在格子里没有意义，谁的东西看卷宗去。
            int de = s.IndexOf('的');
            if (de > 0 && de < s.Length - 2 && s.Length - de - 1 <= MaxChars)
                s = s.Substring(de + 1);

            if (s.Length > MaxChars)
                s = s.Substring(0, MaxChars - 1) + "…";

            return s;
        }
    }
}
