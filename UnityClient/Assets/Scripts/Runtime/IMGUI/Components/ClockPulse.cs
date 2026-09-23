#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 钟一动，画钟的地方都得看得见它动。
    //
    // 钟画在三处：卡顶徽章、标注读数、卷宗。它们各自只拿到一份 GameClock 快照，谁也不知道
    // 上一帧的值是多少，于是数字换了就换了——玩家只看见结果条里一行「+2」，抬头找钟时它
    // 已经是新的样子。这里替所有画钟的地方记住上一次的值，值一变就起一串脉冲，三处一起动。
    //
    // 节奏借冷静 / 伤势条那一套（见 SSNoirGameManager 的脉冲注释）：一格一格来、还没轮到的
    // 格子先按旧样子画、涨是软的金光、退是硬的过曝。同一种变化在人身上和钟上长一个样子。
    // 钟的身份就是它的标签：同一根钟在徽章和标注上同时出现时，两处按同一串时间表闪。
    public static class ClockPulse
    {
        public readonly struct Cell
        {
            public readonly float Strength;   // 1 → 0
            public readonly bool Loss;        // 值在往下走
            public readonly bool Pending;     // 还没轮到它：先按旧样子画

            public Cell(float strength, bool loss, bool pending)
            {
                Strength = strength;
                Loss = loss;
                Pending = pending;
            }
        }

        private sealed class State
        {
            public int Current;
            public int LoCell;                // 变化覆盖的格子区间 [LoCell, HiCell)
            public int HiCell;
            public bool FillingUp;
            public float StartTime = -1f;
            public float SeenAt;
        }

        // 比冷静条略慢：钟不在手边，眼睛要先找到它。
        private const float LossDuration = 0.6f;
        private const float GainDuration = 0.8f;
        private const float StepDelay = 0.2f;
        // 变化那一下的弹与余像。弹要短（一下子），余像要能看清那一扇是怎么没的。
        private const float PopDuration = 0.55f;
        private const float LossGhostDuration = 0.9f;

        private static readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        // 钟的身份 = 宿主 + 标签。三个打手的生命钟都叫"生命"，只按标签记会把三张卡的值
        // 当成同一根钟在来回跳。画卡 / 画标注的地方进出时声明宿主；没声明的（卷宗）各自一份。
        private static string _host = string.Empty;

        public static void BeginHost(string host) => _host = host ?? string.Empty;
        public static void EndHost() => _host = string.Empty;

        private static string Key(GameClock clock) => _host + "\u0001" + clock.Label;

        /// <summary>登记这一帧看到的值；值变了就起脉冲。画钟的地方每帧先调它，再问格子。</summary>
        public static void Note(GameClock clock)
        {
            float now = Time.unscaledTime;
            string key = Key(clock);
            if (!_states.TryGetValue(key, out var state))
            {
                // 第一次见到不算「变了」，否则进场所有钟一起闪，真正的变化反而被淹掉。
                _states[key] = new State { Current = clock.Current, SeenAt = now };
                Prune(now);
                return;
            }
            state.SeenAt = now;
            if (clock.Current == state.Current)
                return;
            bool up = clock.Current > state.Current;
            state.LoCell = Mathf.Min(state.Current, clock.Current);
            state.HiCell = Mathf.Max(state.Current, clock.Current);
            state.FillingUp = up;
            state.StartTime = now;
            state.Current = clock.Current;
        }

        /// <summary>
        /// 变化那一下整枚徽章的"弹"：0→峰值→回落，给调用方做缩放用（1 + Pop * 0.16）。
        /// 眼睛先被形状的动吸住，再去读格子——格子的脉冲接着说变了几格。
        /// </summary>
        public static float Pop(GameClock clock)
        {
            if (!_states.TryGetValue(Key(clock), out var state) || state.StartTime < 0f)
                return 0f;
            float t = Time.unscaledTime - state.StartTime;
            if (t < 0f || t >= PopDuration)
                return 0f;
            const float attack = 0.10f;
            return t < attack ? t / attack : 1f - Mathf.Pow((t - attack) / (PopDuration - attack), 0.7f);
        }

        /// <summary>
        /// 正在退的那一段还剩多少余像（0–1）和退之前的值：表盘用它把消失的那一扇先画成白的再淡掉，
        /// "少了一格"就不是数字换了，而是看得见一块东西走了。只对减少有效。
        /// </summary>
        public static bool TryGetFadingLoss(GameClock clock, out int previous, out float strength)
        {
            previous = 0;
            strength = 0f;
            if (!_states.TryGetValue(Key(clock), out var state) || state.StartTime < 0f || state.FillingUp)
                return false;
            float t = Time.unscaledTime - state.StartTime;
            if (t < 0f || t >= LossGhostDuration)
                return false;
            previous = state.HiCell;
            strength = 1f - Mathf.Pow(t / LossGhostDuration, 1.4f);
            return true;
        }

        /// <summary>整根钟此刻的余光（0–1），给徽章描边 / 标签提亮用。</summary>
        public static float Glow(GameClock clock)
        {
            if (!_states.TryGetValue(Key(clock), out var state) || state.StartTime < 0f)
                return 0f;
            float total = (state.HiCell - state.LoCell - 1) * StepDelay + (state.FillingUp ? GainDuration : LossDuration);
            float elapsed = Time.unscaledTime - state.StartTime;
            if (elapsed >= total) return 0f;
            return 1f - Mathf.Pow(elapsed / total, 0.6f);
        }

        /// <summary>第 cellIndex 格此刻的脉冲；不在变化区间里就是空。</summary>
        public static Cell CellPulse(GameClock clock, int cellIndex)
        {
            if (!_states.TryGetValue(Key(clock), out var state) || state.StartTime < 0f
                || cellIndex < state.LoCell || cellIndex >= state.HiCell)
                return default;

            // 沿着变化的方向排队：亮起来的从低位数起，熄灭的从高位数起。
            int order = state.FillingUp ? cellIndex - state.LoCell : state.HiCell - 1 - cellIndex;
            float elapsed = Time.unscaledTime - (state.StartTime + order * StepDelay);
            bool loss = !state.FillingUp;
            if (elapsed < 0f)
                return new Cell(0f, loss, pending: true);
            float duration = loss ? LossDuration : GainDuration;
            if (elapsed >= duration)
                return default;
            return new Cell(1f - elapsed / duration, loss, pending: false);
        }

        /// <summary>
        /// 一格进度的通用画法：底色按「此刻该不该亮」画（还没轮到的格子按旧样子），
        /// 再叠脉冲——退是过曝到白，涨是本色柔光。
        /// </summary>
        public static void DrawCell(Rect cell, bool filledNow, Color fill, Color empty, Cell pulse, System.Action<Rect>? drawEmptyOutline)
        {
            bool filled = pulse.Pending ? !filledNow : filledNow;
            IMGUIStyles.SetColor(filled ? fill : empty);
            GUI.DrawTexture(cell, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            if (!filled)
                drawEmptyOutline?.Invoke(cell);

            if (pulse.Strength > 0f)
            {
                Color flash = pulse.Loss ? Color.white : fill;
                float alpha = pulse.Strength * (pulse.Loss ? 1f : 0.7f);
                IMGUIStyles.SetColor(new Color(flash.r, flash.g, flash.b, alpha));
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }
        }

        private static void Prune(float now)
        {
            if (_states.Count <= 128) return;
            var stale = new List<string>();
            foreach (var pair in _states)
                if (now - pair.Value.SeenAt > 60f)
                    stale.Add(pair.Key);
            foreach (var key in stale)
                _states.Remove(key);
        }
    }
}
