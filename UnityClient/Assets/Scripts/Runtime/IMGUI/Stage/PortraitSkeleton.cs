#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 一张霓虹立绘的管子骨架：一组 uv 折线（u 从左、v 从下，和 GUI.DrawTextureWithTexCoords 一致）。
    // 由 tools/portrait-neon/process.py 离线从图里细化出来，存在 <名>.neon.json。
    // 每条折线是一根有头有尾的管子（离线时已按切线连续性把碎线并成整管），所以效果可以按根来：
    // 电流从一根管子的电极进、另一头出，接触不良时是某几根管子死掉而不是整块招牌暗下去。
    public sealed class PortraitSkeleton
    {
        public sealed class Path
        {
            public readonly Vector2[] Points;
            public readonly float[] Cumulative;   // 每个点之前的累计长度
            public readonly float Length;

            public Path(Vector2[] points)
            {
                Points = points;
                Cumulative = new float[points.Length];
                float total = 0f;
                for (int i = 1; i < points.Length; i++)
                {
                    total += Vector2.Distance(points[i - 1], points[i]);
                    Cumulative[i] = total;
                }
                Length = total;
            }

            // 沿线走 distance 之后的位置。
            public Vector2 At(float distance)
            {
                distance = Mathf.Clamp(distance, 0f, Length);
                int i = 1;
                while (i < Points.Length - 1 && Cumulative[i] < distance) i++;
                float segment = Cumulative[i] - Cumulative[i - 1];
                float t = segment > 0f ? (distance - Cumulative[i - 1]) / segment : 0f;
                return Vector2.Lerp(Points[i - 1], Points[i], t);
            }
        }

        public readonly IReadOnlyList<Path> Paths;
        public readonly float TotalLength;
        // 图里点缀色的默认值（离线算出的平均色）；没有点缀色的人物为 null。
        public readonly Color? DefaultAccent;

        private PortraitSkeleton(List<Path> paths, Color? accent)
        {
            Paths = paths;
            float total = 0f;
            foreach (var p in paths) total += p.Length;
            TotalLength = total;
            DefaultAccent = accent;
        }

        // 沿整条巡回走 distance（自动绕圈）之后的位置。折线离线时已按空间连续排好序，
        // 所以一路走下去就像一根管子从头绕到尾。
        public Vector2 At(float distance)
        {
            if (TotalLength <= 0f) return default;
            distance %= TotalLength;
            if (distance < 0f) distance += TotalLength;
            foreach (var path in Paths)
            {
                if (distance <= path.Length) return path.At(distance);
                distance -= path.Length;
            }
            return Paths[Paths.Count - 1].Points[^1];
        }

        // 巡回上 distance 处落在哪根管子、离它的头有多远。
        public bool Locate(float distance, out int pathIndex, out float local)
        {
            pathIndex = -1; local = 0f;
            if (TotalLength <= 0f) return false;
            distance %= TotalLength;
            if (distance < 0f) distance += TotalLength;
            for (int i = 0; i < Paths.Count; i++)
            {
                if (distance <= Paths[i].Length) { pathIndex = i; local = distance; return true; }
                distance -= Paths[i].Length;
            }
            pathIndex = Paths.Count - 1; local = Paths[pathIndex].Length;
            return true;
        }

        public static PortraitSkeleton? Parse(string json)
        {
            var root = JObject.Parse(json);
            var paths = new List<Path>();
            foreach (var rawPath in root["paths"] as JArray ?? new JArray())
            {
                var arr = (JArray)rawPath;
                var pts = new Vector2[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                    pts[i] = new Vector2((float)arr[i]![0]!, (float)arr[i]![1]!);
                if (pts.Length >= 2) paths.Add(new Path(pts));
            }
            Color? accent = null;
            var hex = root["accent"]?.Type == JTokenType.String ? (string)root["accent"]! : null;
            if (hex != null && ColorUtility.TryParseHtmlString(hex, out var c)) accent = c;
            return new PortraitSkeleton(paths, accent);
        }
    }
}
