#nullable enable
using System;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 地点文件旁边的 <c>&lt;名&gt;.clips.json</c>（CityBox pipeline/motion.py 写）：会动的道具的 clip 表。
    /// clip 名是 <c>道具__状态</c>；状态 clip 记 loop，环境循环另记 ambient，过渡记 from / to，短动作记 once + from。
    /// 导入器按它切 Legacy clip，<see cref="PropMotion"/> 按它在状态之间选过渡。
    /// </summary>
    [Serializable]
    public sealed class PropClips
    {
        [Serializable]
        public sealed class Clip
        {
            public string name = string.Empty;
            public int start;
            public int end;
            public bool loop;
            public bool ambient;
            public bool once;
            public string from = string.Empty;
            public string to = string.Empty;
            // 空数组沿用道具parts；演出可额外驱动临时参演者，状态片段仍只控制本体。
            public string[] participants = Array.Empty<string>();
            public float travelSeconds;

            /// <summary>clip 名前半：道具名。</summary>
            public string Prop => name.Substring(0, name.IndexOf("__", StringComparison.Ordinal));
            /// <summary>clip 名后半：状态名或过渡名。</summary>
            public string Tail => name.Substring(name.IndexOf("__", StringComparison.Ordinal) + 2);
            public bool IsTransition => from.Length > 0 && !once;
        }

        [Serializable]
        public sealed class Parts
        {
            public string prop = string.Empty;
            public string[] names = Array.Empty<string>();
            public string anchor = string.Empty;
            public string present = string.Empty;
            public string absent = string.Empty;
        }

        public int fps;
        public Clip[] clips = Array.Empty<Clip>();
        /// <summary>每件道具的部件对象名：播 clip 前复位用（导入器会丢掉 clip 内恒定的曲线）。</summary>
        public Parts[] parts = Array.Empty<Parts>();

        public static PropClips Parse(string json)
        {
            var spec = JsonUtility.FromJson<PropClips>(json);
            var ambientProps = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var c in spec.clips)
            {
                if (c.travelSeconds < 0 || float.IsNaN(c.travelSeconds) || float.IsInfinity(c.travelSeconds))
                    throw new InvalidOperationException($"[SSNoir] '{c.name}' 的 travelSeconds 必须是非负有限秒数。");
                if (c.name.IndexOf("__", StringComparison.Ordinal) <= 0)
                    throw new InvalidOperationException($"[SSNoir] clips.json：clip 名 '{c.name}' 不是 道具__状态。");
                if (c.ambient && (!c.loop || c.once || c.IsTransition || !ambientProps.Add(c.Prop)))
                    throw new InvalidOperationException($"[SSNoir] 环境循环 '{c.name}' 必须是唯一的循环状态，不能是过渡或短动作。");
            }
            foreach (var p in spec.parts)
                if (p.anchor.Length > 0)
                    foreach (var state in new[] { p.present, p.absent })
                        if (state.Length == 0 || Array.Find(spec.clips, c => c.name == p.prop + "__" + state && !c.IsTransition && !c.once) == null)
                            throw new InvalidOperationException($"[SSNoir] '{p.prop}' 的随目标姿势缺少有效状态 '{state}'。");
            return spec;
        }
    }
}
