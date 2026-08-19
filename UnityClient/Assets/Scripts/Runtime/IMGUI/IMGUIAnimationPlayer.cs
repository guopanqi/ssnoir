#nullable enable
using System;
using System.Collections;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public class IMGUIAnimationPlayer : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        public int DisplayedDieValue { get; private set; } = 1;
        public float DisplayScale { get; private set; } = 1f;
        public string DisplayOutcomeText { get; private set; } = "";
        public Color DisplayOutcomeColor { get; private set; } = Color.white;
        public int FateDieValue { get; private set; }
        public RollOutcome FinalOutcome { get; private set; }
        public int ChosenDie { get; private set; }
        public int Phase => _phase;
        public string ActionName { get; private set; } = "";
        public ActionReport? CurrentReport { get; private set; }
        public bool UsesModal =>
            CurrentReport != null
            && OutcomePresentationPolicy.ShouldUseRollModal(CurrentReport, ActionName);
        public Action? OnAcknowledged;

        private float _phaseStartTime;
        private int _phase; // 0: sweep, 1: pop, 2: outcome, 3: done

        public void StartRoll(ActionReport report, string actionName)
        {
            IsPlaying = true;
            _phase = 0;
            _phaseStartTime = Time.time;
            FateDieValue = report.FateDieValue;
            FinalOutcome = report.Outcome;
            CurrentReport = report;
            ChosenDie = report.ChosenDieValue;
            ActionName = actionName;
            DisplayedDieValue = 1;
            DisplayScale = 1f;
            DisplayOutcomeText = "";
        }

        public void Update()
        {
            if (!IsPlaying) return;

            float elapsed = Time.time - _phaseStartTime;

            if (_phase == 0)
            {
                // 掷骰扫掠：命运高亮沿命运条减速滑动（easeOutCubic），精确落在最终骰面。
                const float sweepDuration = 0.5f;
                float t = Mathf.Clamp01(elapsed / sweepDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                int totalSteps = 12 + (FateDieValue - 1); // 两圈扫掠后落格
                int step = Mathf.RoundToInt(eased * totalSteps);
                DisplayedDieValue = step % 6 + 1;
                DisplayScale = 1f;

                if (elapsed >= sweepDuration)
                {
                    _phase = 1;
                    _phaseStartTime = Time.time;
                    DisplayedDieValue = FateDieValue;
                    DisplayScale = 1f;
                }
            }
            else if (_phase == 1)
            {
                // 落格弹跳：命中格放大一下再收（sin 单峰）。
                const float popDuration = 0.2f;
                float t = Mathf.Clamp01(elapsed / popDuration);
                DisplayScale = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;

                if (elapsed >= popDuration)
                {
                    _phase = 2;
                    _phaseStartTime = Time.time;
                    DisplayScale = 1f;

                    DisplayOutcomeText = FinalOutcome == RollOutcome.Success ? "判定成功"
                                       : FinalOutcome == RollOutcome.Neutral ? "判定中性"
                                       : "判定失败";
                    DisplayOutcomeColor = FinalOutcome == RollOutcome.Success ? IMGUIStyles.OutcomeSuccess
                                        : FinalOutcome == RollOutcome.Neutral ? IMGUIStyles.OutcomeNeutral
                                        : IMGUIStyles.OutcomeFail;
                }
            }
        }

        public void Acknowledge()
        {
            if (_phase == 2)
            {
                IsPlaying = false;
                _phase = 3;
                CurrentReport = null;
                OnAcknowledged?.Invoke();
            }
        }

        public bool IsReadyToAcknowledge()
        {
            // 结果停留：轻结算让「好/中/坏」定格片刻，再切到 residue（命运条原地冻结 + 结果揭开）。
            return _phase == 2 && (Time.time - _phaseStartTime) >= 0.55f;
        }

        public void DrawModal()
        {
            if (!IsPlaying) return;
            if (!UsesModal) return;

            // Blocker
            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Modal panel
            var modalRect = UIScale.CenteredModal(
                380f,
                CurrentReport?.OutcomePresentation?.HasText == true ? 290f : 260f);
            float modalW = modalRect.width;
            float modalH = modalRect.height;
            float modalX = modalRect.x;
            float modalY = modalRect.y;

            // 纸物件：Paper 底 + 硬投影，无描边
            IMGUIStyles.DrawShadow(modalRect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modalRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Content
            float contentX = modalX + 20;
            float contentY = modalY + 20;
            float contentW = modalW - 40;

            // Title
            string title = string.IsNullOrEmpty(ActionName) ? "[判定结果]" : $"[判定结果] {ActionName}";
            IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 24), title, IMGUIStyles.ModalTitle);
            contentY += 28;

            // 命运条（赔率条）：掷骰扫掠 → 落格弹跳 → 定格，与卡面轻结算同一套视觉。
            if (CurrentReport != null)
            {
                var strip = FateStrip.StripForPrepared(CurrentReport.PreparedValue);

                var summaryStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = IMGUIStyles.FontSize(12),
                    normal = { textColor = IMGUIStyles.PaperTextSecondary }
                };
                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 16), FateStrip.Describe(strip), summaryStyle);
                contentY += 18;

                int highlightedFace = _phase == 0 ? DisplayedDieValue : FateDieValue;
                float pulse = _phase == 1 ? Mathf.Max(0f, DisplayScale - 1f) : 0f;
                float stripW = Mathf.Min(contentW, 300f);
                ActionNodeDrawer.DrawOddsStrip(
                    new Rect(contentX + (contentW - stripW) / 2f, contentY, stripW, 30f),
                    strip, highlightedFace, pulse, _phase >= 2);
                contentY += 42;
            }

            // Details
            if (_phase >= 1)
            {
                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 20), $"投入骰子值: {ChosenDie}", IMGUIStyles.ModalBody);
                contentY += 22;

                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 20),
                    $"准备值: {CurrentReport!.PreparedValue} · 命运骰: {CurrentReport.FateDieValue}", IMGUIStyles.ModalBody);
                contentY += 26;
            }

            // Outcome
            if (_phase >= 2)
            {
                var outcomeStyle = new GUIStyle(IMGUIStyles.ModalTitle);
                outcomeStyle.fontSize = IMGUIStyles.FontSize(16);
                outcomeStyle.normal.textColor = DisplayOutcomeColor;
                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 24), $"判定结果: {DisplayOutcomeText}", outcomeStyle);
                contentY += 32;

                var presentation = CurrentReport?.OutcomePresentation;
                if (presentation != null && presentation.HasText)
                {
                    var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle);
                    titleStyle.fontSize = IMGUIStyles.FontSize(15);
                    titleStyle.alignment = TextAnchor.MiddleCenter;
                    IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 22), presentation.Title, titleStyle);
                }
            }

            // Acknowledge button
            if (_phase >= 2)
            {
                float btnW = 100;
                float btnH = 32;
                float btnX = modalX + (modalW - btnW) / 2f;
                float btnY = modalY + modalH - 45;
                var btnRect = new Rect(btnX, btnY, btnW, btnH);
                var mousePos = Event.current.mousePosition;

                bool isHovered = btnRect.Contains(mousePos);
                bool isClicked = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;

                if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", isHovered, isClicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
                {
                    Event.current.Use();
                    Acknowledge();
                }
            }
        }
    }
}
