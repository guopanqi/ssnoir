#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
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
        public int FinalValue { get; private set; }
        public RollOutcome FinalOutcome { get; private set; }
        public int ChosenDie { get; private set; }
        public int Phase => _phase;
        public string ActionName { get; private set; } = "";
        public List<int> RandomDice { get; private set; } = new List<int>();
        public ActionReport? CurrentReport { get; private set; }
        public bool UsesModal =>
            CurrentReport != null
            && OutcomePresentationPolicy.ShouldUseRollModal(CurrentReport, ActionName);
        public Action? OnAcknowledged;

        private float _phaseStartTime;
        private int _phase; // 0: rolling, 1: reveal, 2: outcome, 3: done
        private readonly System.Random _rand = new System.Random();

        public void StartRoll(ActionReport report, string actionName)
        {
            IsPlaying = true;
            _phase = 0;
            _phaseStartTime = Time.time;
            FinalValue = report.FinalRollValue;
            FinalOutcome = report.Outcome;
            CurrentReport = report;
            ChosenDie = report.ChosenDieValue;
            ActionName = actionName;
            RandomDice = new List<int>(report.RandomDice);
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
                // Rolling phase: 1.0s
                float t = Mathf.Clamp01(elapsed / 1.0f);
                float interval = Mathf.Lerp(0.05f, 0.22f, t);

                if (elapsed < 1.0f)
                {
                    // Update displayed value every interval
                    int frames = Mathf.FloorToInt(elapsed / interval);
                    int newVal = _rand.Next(1, 7);
                    DisplayedDieValue = newVal;
                    DisplayScale = UnityEngine.Random.Range(0.9f, 1.15f);
                }
                else
                {
                    _phase = 1;
                    _phaseStartTime = Time.time;
                    DisplayedDieValue = FinalValue;
                    DisplayScale = 1f;
                }
            }
            else if (_phase == 1)
            {
                // Reveal pulse phase: 0.25s
                float t = Mathf.Clamp01(elapsed / 0.25f);
                DisplayScale = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;

                if (elapsed >= 0.25f)
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
            else if (_phase == 2)
            {
                // Outcome display: wait for click
                if (elapsed >= 0.8f)
                {
                    // Ready to acknowledge
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
            return _phase == 2 && (Time.time - _phaseStartTime) >= 0.8f;
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
            float modalW = 380;
            float modalH = CurrentReport?.OutcomePresentation?.HasText == true ? 330 : 260;
            float modalX = (UIScale.VW - modalW) / 2f;
            float modalY = (UIScale.VH - modalH) / 2f;
            var modalRect = new Rect(modalX, modalY, modalW, modalH);

            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modalRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Outline
            IMGUIStyles.DrawOutline(modalRect, 1f, IMGUIStyles.PrimaryColor);

            // Content
            float contentX = modalX + 20;
            float contentY = modalY + 20;
            float contentW = modalW - 40;

            // Title
            string title = string.IsNullOrEmpty(ActionName) ? "[判定结果]" : $"[判定结果] {ActionName}";
            GUI.Label(new Rect(contentX, contentY, contentW, 24), title, IMGUIStyles.ModalTitle);
            contentY += 28;

            // Rolling die
            GUI.color = IMGUIStyles.ClockActive; // Burnt Amber color
            var dieStyle = new GUIStyle(IMGUIStyles.CardTitle);
            dieStyle.fontSize = 48;
            dieStyle.alignment = TextAnchor.MiddleCenter;
            dieStyle.normal.textColor = IMGUIStyles.ClockActive;
            var dieRect = new Rect(contentX, contentY, contentW, 60);
            // Save the UIScale matrix and compose the die bounce on top of it (not replace it).
            var savedMatrix = GUI.matrix;
            var dieAnimMatrix = Matrix4x4.TRS(dieRect.center, Quaternion.identity, Vector3.one * DisplayScale)
                              * Matrix4x4.TRS(-dieRect.center, Quaternion.identity, Vector3.one);
            GUI.matrix = savedMatrix * dieAnimMatrix;
            GUI.Label(dieRect, $"D{DisplayedDieValue}", dieStyle);
            GUI.matrix = savedMatrix; // restore UIScale matrix, not identity
            GUI.color = Color.white;
            contentY += 65;

            // Details
            if (_phase >= 1)
            {
                GUI.Label(new Rect(contentX, contentY, contentW, 20), $"投入骰子值: {ChosenDie}", IMGUIStyles.ModalBody);
                contentY += 22;

                string randText = RandomDice.Count > 0
                    ? "附加掷骰: " + string.Join(", ", RandomDice)
                    : "无附加掷骰 (技能等级为1)";
                GUI.Label(new Rect(contentX, contentY, contentW, 20), randText, IMGUIStyles.ModalBody);
                contentY += 22;

                GUI.Label(new Rect(contentX, contentY, contentW, 20), $"最终最大点数: {FinalValue}", IMGUIStyles.ModalBody);
                contentY += 26;
            }

            // Outcome
            if (_phase >= 2)
            {
                var outcomeStyle = new GUIStyle(IMGUIStyles.ModalTitle);
                outcomeStyle.fontSize = 16;
                outcomeStyle.normal.textColor = DisplayOutcomeColor;
                GUI.Label(new Rect(contentX, contentY, contentW, 24), $"判定结果: {DisplayOutcomeText}", outcomeStyle);
                contentY += 32;

                var presentation = CurrentReport?.OutcomePresentation;
                if (presentation != null && presentation.HasText)
                {
                    var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle);
                    titleStyle.fontSize = 15;
                    titleStyle.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(new Rect(contentX, contentY, contentW, 22), presentation.Title, titleStyle);
                    contentY += 24;

                    var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody);
                    subtitleStyle.wordWrap = true;
                    subtitleStyle.alignment = TextAnchor.UpperCenter;
                    GUI.Label(new Rect(contentX, contentY, contentW, 42), presentation.Subtitle, subtitleStyle);
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

                if (IMGUIStyles.DrawTechnicalButton(btnRect, "确定", isHovered, isClicked, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel))
                {
                    Acknowledge();
                }
            }
        }
    }
}
