#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    /// <summary>一道疤：倒下那次留在身上的东西。部位与日期是全部内容，不可治疗、不会淡。</summary>
    public sealed class ScarRecord
    {
        public string Part { get; }
        public string Skill { get; }
        public string SkillName { get; }
        /// <summary>留下这道疤的世界日，供档案与对白引用（「上个月码头那回」）。</summary>
        public int Day { get; }

        public ScarRecord(string part, int day)
        {
            var site = Injury.SiteOf(part);
            Part = site.Part;
            Skill = site.Skill;
            SkillName = site.SkillName;
            Day = day;
        }
    }

    /// <summary>
    /// 疤痕：倒下的永久代价。伤势是可以养好的账，疤是养不好的那一部分——
    /// 每一次被抬进诊所，当时伤在哪儿就在哪儿留一道，从此那项能力永久 −1。
    ///
    /// 会叠加：同一个部位被打倒两次就是 −2。但落点跟着伤势走，而伤势第一次是随机命中的，
    /// 所以反复倒下更可能把人磨成四处都差一点，而不是把某一项钉死到不能用。
    ///
    /// 疤不进伤势刻度，也不吃治疗——诊所把人救回来，救不回那只手。
    /// 修正走和伤势同一条**可见**修正链：投骰前看得见，绝不暗扣。
    /// </summary>
    public sealed class ScarSet
    {
        /// <summary>一道疤的分量。</summary>
        public const int PenaltyPerScar = -1;

        private readonly List<ScarRecord> _scars = new();

        public IReadOnlyList<ScarRecord> All => _scars;
        public int Count => _scars.Count;

        public ScarRecord Add(string part, int day)
        {
            var scar = new ScarRecord(part, day);
            _scars.Add(scar);
            return scar;
        }

        public void Clear() => _scars.Clear();

        public int CountAt(string part)
        {
            int n = 0;
            foreach (var scar in _scars)
                if (scar.Part == part) n++;
            return n;
        }

        /// <summary>某项能力上所有疤的合计修正（0 表示这项没被伤过）。</summary>
        public int PenaltyFor(string skillName)
        {
            int sum = 0;
            foreach (var scar in _scars)
                if (scar.Skill.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                    sum += PenaltyPerScar;
            return sum;
        }

        /// <summary>
        /// 这次判定要不要吃旧伤修正；不吃返回 null。和 <see cref="Injury.ModifierFor"/> 同形：
        /// 只压主角，只压对应能力，卡面预览与结算都从这里取，不许出现两套账。
        /// </summary>
        public DifficultyModifierInfo? ModifierFor(string actorRole, string skillName)
        {
            if (actorRole != "protagonist") return null;
            int penalty = PenaltyFor(skillName);
            if (penalty == 0) return null;
            return new DifficultyModifierInfo { Value = penalty, Reason = ReasonFor(skillName) };
        }

        /// <summary>把疤按能力聚合成客户端能直接查表的一份修正，键是能力内部键。</summary>
        public IReadOnlyDictionary<string, DifficultyModifierInfo> ModifiersBySkill()
        {
            var result = new Dictionary<string, DifficultyModifierInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var scar in _scars)
            {
                if (result.ContainsKey(scar.Skill)) continue;
                result[scar.Skill] = new DifficultyModifierInfo
                {
                    Value = PenaltyFor(scar.Skill),
                    Reason = ReasonFor(scar.Skill),
                };
            }
            return result;
        }

        // 「手上的旧伤」。同一部位多道疤不分别列出——玩家要看的是这只手现在有多废。
        private string ReasonFor(string skillName)
        {
            foreach (var scar in _scars)
                if (scar.Skill.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                    return scar.Part + "上的旧伤";
            return "旧伤";
        }

        /// <summary>面板上那一行：「旧伤 手-2 眼-1」。没有疤时为空。</summary>
        public string Describe()
        {
            if (_scars.Count == 0) return string.Empty;
            var seen = new List<string>();
            var parts = new List<string>();
            foreach (var scar in _scars)
            {
                if (seen.Contains(scar.Part)) continue;
                seen.Add(scar.Part);
                parts.Add($"{scar.Part}{CountAt(scar.Part) * PenaltyPerScar}");
            }
            return "旧伤 " + string.Join(" ", parts);
        }

        public List<ScarSaveData> Serialize()
        {
            var data = new List<ScarSaveData>();
            foreach (var scar in _scars)
                data.Add(new ScarSaveData { Part = scar.Part, Day = scar.Day });
            return data;
        }

        /// <summary>读档：只存部位与日期，能力由部位表推回，不留两份会互相矛盾的字段。</summary>
        public void Restore(IEnumerable<ScarSaveData> data)
        {
            _scars.Clear();
            foreach (var entry in data)
                _scars.Add(new ScarRecord(entry.Part, entry.Day));
        }
    }
}
