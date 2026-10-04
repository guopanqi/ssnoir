#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 世界卡片与它锚点之间的那根引线，连同锚点上的圆环。
    ///
    /// <b>这是一层，不是每张卡自己的装饰。</b>卡片按远近排序绘制，谁后画谁盖住先画的东西；
    /// 引线原本由每张卡在自己之前画一次，于是一条线经常被一张毫不相干的邻卡拦腰切断——
    /// 「看不清哪张卡指哪儿」大半来自这里。收成一层、在所有卡片之前一次画完之后，
    /// 能盖住一条线的只剩它自己那张卡。
    ///
    /// 两条规则：
    /// - <b>接入点只有四个</b>：面向锚点那条边的中点。任意点接入看着就是随机的，
    ///   宁可线多绕一点，也要每次都能预期它从哪儿进来。
    /// - <b>一个锚点只画一个圆环</b>：同一处长出好几张卡时（一个 anchor 挂多个动作），
    ///   N 个重叠的圆环只会糊成一团。
    /// </summary>
    public static class CardLeaderLineDrawer
    {
        /// <summary>
        /// 引线的三档轻重。由调用方按"谁在被指着"决定，绘制层不猜。
        /// 顺序即轻重：同一锚点上取最重的一档画圆环，绘制也按这个顺序分趟。
        /// </summary>
        public enum Emphasis
        {
            /// <summary>别人正被指着，这条让开；也用于已结算的残影卡。</summary>
            Muted,
            /// <summary>常态。</summary>
            Normal,
            /// <summary>指针停在这张卡上，或它是当前聚焦的节点。</summary>
            Highlighted,
        }

        public readonly struct Tether
        {
            public readonly Vector2 AnchorPos;
            public readonly Rect CardRect;
            public readonly Emphasis Weight;

            public Tether(Vector2 anchorPos, Rect cardRect, Emphasis weight)
            {
                AnchorPos = anchorPos;
                CardRect = cardRect;
                Weight = weight;
            }
        }

        // 锚点圆环画多大（虚拟像素）。小到不抢戏，又大到在亮着的窗户上也看得见。
        private const float RingSize = 10f;
        // 引线从圆环外缘起步，不从圆心起步：压着环画会把环糊成一个点。
        private const float RingClearance = RingSize / 2f + 1f;
        // 同一处的锚点只画一个环：投影相差这么点像素以内视作同一处。
        private const float RingMergeEpsilon = 2f;

        private static readonly List<Vector2> _drawnRings = new List<Vector2>();

        public static void Draw(List<Tether> tethers)
        {
            if (tethers.Count == 0)
                return;

            // 高亮的最后画，压在同批次的其它线之上——被指着的那一条不该被邻线切断。
            DrawPass(tethers, Emphasis.Muted);
            DrawPass(tethers, Emphasis.Normal);
            DrawPass(tethers, Emphasis.Highlighted);

            _drawnRings.Clear();
            foreach (var tether in tethers)
            {
                if (AlreadyRinged(tether.AnchorPos))
                    continue;
                _drawnRings.Add(tether.AnchorPos);
                DrawRing(tether.AnchorPos, RingColor(RingWeightAt(tethers, tether.AnchorPos)));
            }
        }

        private static void DrawPass(List<Tether> tethers, Emphasis weight)
        {
            foreach (var tether in tethers)
            {
                if (tether.Weight != weight)
                    continue;
                DrawLeader(tether);
            }
        }

        /// <summary>同一个锚点上挂着好几张卡时，圆环取其中最重的那一档。</summary>
        private static Emphasis RingWeightAt(List<Tether> tethers, Vector2 anchorPos)
        {
            var weight = Emphasis.Muted;
            foreach (var tether in tethers)
            {
                if ((tether.AnchorPos - anchorPos).sqrMagnitude > RingMergeEpsilon * RingMergeEpsilon)
                    continue;
                if (tether.Weight > weight)
                    weight = tether.Weight;
            }
            return weight;
        }

        private static bool AlreadyRinged(Vector2 anchorPos)
        {
            foreach (var drawn in _drawnRings)
            {
                if ((drawn - anchorPos).sqrMagnitude <= RingMergeEpsilon * RingMergeEpsilon)
                    return true;
            }
            return false;
        }

        private static void DrawLeader(in Tether tether)
        {
            Rect card = tether.CardRect;
            Vector2 anchor = tether.AnchorPos;

            // 锚点落在卡片自己身上：线整段都在卡底下，画了也看不见，只留圆环。
            if (card.Contains(anchor))
                return;

            var attach = AttachPoint(card, anchor);
            // 斜段绕开建筑焦点，末端用短直段接入牌子；不沿地标拉一条长竖线。
            const float stub = 18f;
            Vector2 elbow = attach.OnVerticalEdge
                ? attach.Point + Vector2.right * (anchor.x < card.center.x ? -stub : stub)
                : attach.Point + Vector2.up * (anchor.y < card.center.y ? -stub : stub);

            Vector2 start = StepOffRing(anchor, elbow);
            Color color = LineColor(tether.Weight);
            float thickness = LineThickness(tether.Weight);

            // 城市是黑墙配亮黄窗户，单一颜色的细线飘到窗户上就没了。先描一道暗边，
            // 线不管压在什么上头都能读出来。
            Color halo = new Color(0f, 0f, 0f, color.a * 0.30f);
            IMGUIStyles.DrawLine(start, elbow, halo, thickness + 1f);
            IMGUIStyles.DrawLine(elbow, attach.Point, halo, thickness + 1f);
            IMGUIStyles.DrawLine(start, elbow, color, thickness);
            IMGUIStyles.DrawLine(elbow, attach.Point, color, thickness);
        }

        private readonly struct Attachment
        {
            public readonly Vector2 Point;
            /// <summary>接在左右边（竖边）上：折线先走竖段，再横着插进这条边。</summary>
            public readonly bool OnVerticalEdge;

            public Attachment(Vector2 point, bool onVerticalEdge)
            {
                Point = point;
                OnVerticalEdge = onVerticalEdge;
            }
        }

        /// <summary>
        /// 四条边的中点里选一个：锚点在哪一侧就接哪一侧。
        /// 斜着的时候比"超出去多少个半宽/半高"，谁超得多算谁那一侧——这样卡片长条形时
        /// 也不会为了几个像素的差距去接短边。
        /// </summary>
        private static Attachment AttachPoint(Rect card, Vector2 anchor)
        {
            Vector2 center = card.center;
            float halfW = Mathf.Max(1f, card.width / 2f);
            float halfH = Mathf.Max(1f, card.height / 2f);
            float overshootX = Mathf.Abs(anchor.x - center.x) / halfW;
            float overshootY = Mathf.Abs(anchor.y - center.y) / halfH;

            if (overshootX > overshootY)
            {
                float x = anchor.x < center.x ? card.xMin : card.xMax;
                return new Attachment(new Vector2(x, center.y), onVerticalEdge: true);
            }

            float y = anchor.y < center.y ? card.yMin : card.yMax;
            return new Attachment(new Vector2(center.x, y), onVerticalEdge: false);
        }

        /// <summary>让线从圆环外缘起步。两点太近就原地起步，免得出现反向的一小段。</summary>
        private static Vector2 StepOffRing(Vector2 anchor, Vector2 towards)
        {
            Vector2 delta = towards - anchor;
            float distance = delta.magnitude;
            return distance <= RingClearance ? anchor : anchor + delta / distance * RingClearance;
        }

        private static Color LineColor(Emphasis weight) => weight switch
        {
            Emphasis.Highlighted => IMGUIStyles.Gold,
            Emphasis.Muted => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.22f),
            _ => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.42f),
        };

        private static Color RingColor(Emphasis weight) => weight switch
        {
            Emphasis.Highlighted => IMGUIStyles.Gold,
            Emphasis.Muted => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f),
            _ => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.75f),
        };

        private static float LineThickness(Emphasis weight) =>
            weight == Emphasis.Highlighted ? 1.5f : 1f;

        private static void DrawRing(Vector2 center, Color color)
        {
            var rect = new Rect(center.x - RingSize / 2f, center.y - RingSize / 2f, RingSize, RingSize);
            var oldColor = GUI.color;
            // 环本身也要能压在亮窗户上，先垫一圈暗底。
            IMGUIStyles.SetColor(new Color(0f, 0f, 0f, color.a * 0.30f));
            GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f),
                IMGUIStyles.AnchorRingTexture);
            IMGUIStyles.SetColor(color);
            GUI.DrawTexture(rect, IMGUIStyles.AnchorRingTexture);
            GUI.color = oldColor;
        }
    }
}
