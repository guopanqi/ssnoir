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
        public float DisplayScale { get; private set; } = 1f;
        public string DisplayOutcomeText { get; private set; } = "";
        public Color DisplayOutcomeColor { get; private set; } = Color.white;
        public RollOutcome FinalOutcome { get; private set; }
        public int ChosenDie { get; private set; }
        public int Phase => _phase;
        public const float LocalRollSeconds = 0.43f;
        private float _localRollDuration = LocalRollSeconds;
        public float LocalRollProgress => _phase >= 2 ? 1f
            : Mathf.Clamp01((Time.unscaledTime - _phaseStartTime) / _localRollDuration);
        public string ActionNodeId { get; private set; } = "";
        public string ActionName { get; private set; } = "";
        public ActionReport? CurrentReport { get; private set; }
        public bool UsesModal =>
            CurrentReport != null
            && OutcomePresentationPolicy.ShouldUseRollModal(CurrentReport, ActionNodeId);
        public Action? OnAcknowledged;

        private float _phaseStartTime;
        private int _phase; // 0: 概率判定中, 1: 结果档位浮现, 2: 结果, 3: 已确认

        public void StartRoll(ActionReport report, string actionNodeId, string actionName, float localRollDuration = LocalRollSeconds)
        {
            if (localRollDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(localRollDuration));
            _localRollDuration = localRollDuration;
            IsPlaying = true;
            _phase = 0;
            _phaseStartTime = Time.unscaledTime;
            FinalOutcome = report.Outcome;
            CurrentReport = report;
            ChosenDie = report.ChosenDieValue;
            ActionNodeId = actionNodeId;
            ActionName = actionName;
            DisplayScale = 1f;
            DisplayOutcomeText = "";
        }

        public void Update()
        {
            if (!IsPlaying) return;

            float elapsed = Time.unscaledTime - _phaseStartTime;

            // 卡下判定只把原执行按钮填满，完成后直接交给结果抽屉；不再单独定格档位。
            if (!UsesModal)
            {
                if (elapsed >= _localRollDuration) _phase = 2;
                return;
            }

            if (_phase == 0)
            {
                // 概率已经在行动前可见；执行时不再重复表演一颗随机骰。
                if (elapsed >= 0.25f)
                {
                    _phase = 1;
                    _phaseStartTime = Time.unscaledTime;
                }
            }
            else if (_phase == 1)
            {
                const float revealDuration = 0.18f;
                float t = Mathf.Clamp01(elapsed / revealDuration);
                DisplayScale = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;
                if (elapsed >= revealDuration)
                {
                    _phase = 2;
                    _phaseStartTime = Time.unscaledTime;
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
            // 卡下进度完成后直接出完整结果；居中弹窗保留原有确认节奏。
            return _phase == 2 && (!UsesModal || (Time.unscaledTime - _phaseStartTime) >= 0.55f);
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
            var modalRect = UIScale.CenteredModal(380f, 260f);
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

            // 三档百分比保持固定；执行后只凸显最终结果，不再出现第二颗骰子。
            if (CurrentReport != null)
            {
                var strip = FateStrip.StripForPrepared(CurrentReport.PreparedValue);

                var summaryStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = IMGUIStyles.FontSize(12),
                    normal = { textColor = IMGUIStyles.PaperTextSecondary }
                };
                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 16), UiText.Odds(strip), summaryStyle);
                contentY += 18;

                RollOutcome? highlighted = _phase >= 1 ? FinalOutcome : (RollOutcome?)null;
                float pulse = _phase == 1 ? Mathf.Max(0f, DisplayScale - 1f) : 0f;
                float stripW = Mathf.Min(contentW, 300f);
                ActionNodeDrawer.DrawOddsStrip(
                    new Rect(contentX + (contentW - stripW) / 2f, contentY, stripW, 30f),
                    strip, highlighted, pulse, _phase >= 2);
                contentY += 42;
            }

            // Details
            if (_phase >= 1)
            {
                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 20), $"投入骰子值: {ChosenDie}", IMGUIStyles.ModalBody);
                contentY += 22;

                IMGUIStyles.DrawLabel(new Rect(contentX, contentY, contentW, 20),
                    $"准备值: {CurrentReport!.PreparedValue}", IMGUIStyles.ModalBody);
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
