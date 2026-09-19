#nullable enable
using System;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 地点文件旁边的 <c>&lt;名&gt;.clips.json</c>（CityBox pipeline/motion.py 写）：会动的道具的 clip 表。
    /// clip 名是 <c>道具__状态</c>；状态 clip 记 loop，过渡 clip 记 from / to。
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
            public string from = string.Empty;
            public string to = string.Empty;

            /// <summary>clip 名前半：道具名。</summary>
            public string Prop => name.Substring(0, name.IndexOf("__", StringComparison.Ordinal));
            /// <summary>clip 名后半：状态名或过渡名。</summary>
            public string Tail => name.Substring(name.IndexOf("__", StringComparison.Ordinal) + 2);
            public bool IsTransition => from.Length > 0;
        }

        [Serializable]
        public sealed class Parts
        {
            public string prop = string.Empty;
            public string[] names = Array.Empty<string>();
        }

        public int fps;
        public Clip[] clips = Array.Empty<Clip>();
        /// <summary>每件道具的部件对象名：播 clip 前复位用（导入器会丢掉 clip 内恒定的曲线）。</summary>
        public Parts[] parts = Array.Empty<Parts>();

        public static PropClips Parse(string json)
        {
            var spec = JsonUtility.FromJson<PropClips>(json);
            foreach (var c in spec.clips)
            {
                if (c.name.IndexOf("__", StringComparison.Ordinal) <= 0)
                    throw new InvalidOperationException($"[SSNoir] clips.json：clip 名 '{c.name}' 不是 道具__状态。");
            }
            return spec;
        }
    }
}
