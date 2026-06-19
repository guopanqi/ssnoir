#nullable enable
using System;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public sealed class PresentationPlayer
    {
        private readonly IMGUIAnimationPlayer _animator;
        private Action? _onDone;
        private ActionReport? _pendingReport;
        private string _actionName = string.Empty;
        private int _stepIndex;
        private float _timer;
        private bool _isPlaying;
        private bool _acknowledgingAnimator;

        public bool IsPlaying => _isPlaying;
        public string ProgressText { get; private set; } = string.Empty;
        public float Progress01 { get; private set; }
        public string ActionName => _actionName;

        public PresentationPlayer(IMGUIAnimationPlayer animator)
        {
            _animator = animator;
        }

        public void Play(ActionReport report, string actionName, Action onDone)
        {
            _pendingReport = report;
            _actionName = actionName;
            _onDone = onDone;
            _stepIndex = 0;
            _timer = 0f;
            _isPlaying = true;
            ProgressText = string.Empty;
            Progress01 = 0f;
        }

        public void Update(float dt)
        {
            if (!_isPlaying || _pendingReport == null)
            {
                return;
            }

            if (_animator.IsPlaying)
            {
                ProgressText = string.Empty;
                Progress01 = 0f;
                if (!_animator.UsesModal && _animator.IsReadyToAcknowledge())
                {
                    OnRollAcknowledged();
                }
                return;
            }

            var hints = _pendingReport.PresentationHints;
            if (hints == null || hints.Count == 0 || _stepIndex >= hints.Count)
            {
                Finish();
                return;
            }

            var hint = hints[_stepIndex];
            if (hint.Kind == PresentationHintKind.RollDice)
            {
                ProgressText = string.Empty;
                Progress01 = 0f;
                _animator.StartRoll(_pendingReport, _actionName);
                return;
            }

            ProgressText = string.IsNullOrEmpty(hint.Text) ? "执行中..." : hint.Text;
            _timer += dt;
            Progress01 = hint.DurationSeconds <= 0f ? 1f : Math.Clamp(_timer / hint.DurationSeconds, 0f, 1f);
            if (_timer < hint.DurationSeconds)
            {
                return;
            }

            _timer = 0f;
            _stepIndex++;
            if (_stepIndex >= hints.Count)
            {
                Finish();
            }
        }

        public void OnRollAcknowledged()
        {
            if (_acknowledgingAnimator)
            {
                return;
            }

            if (_animator.IsPlaying)
            {
                _acknowledgingAnimator = true;
                _animator.Acknowledge();
                _acknowledgingAnimator = false;
            }

            if (!_isPlaying || _pendingReport == null)
            {
                return;
            }

            _stepIndex++;
            _timer = 0f;

            var hints = _pendingReport.PresentationHints;
            if (hints == null || _stepIndex >= hints.Count)
            {
                Finish();
            }
        }

        private void Finish()
        {
            _isPlaying = false;
            _pendingReport = null;
            _actionName = string.Empty;
            ProgressText = string.Empty;
            Progress01 = 0f;
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
        }
    }
}
