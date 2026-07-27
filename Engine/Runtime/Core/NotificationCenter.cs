#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum NotificationKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class Notification
    {
        public string Text { get; set; } = string.Empty;
        public NotificationKind Kind { get; set; }
        public float ElapsedTime { get; set; } = 0f;
        public float Duration { get; set; } = 3.0f;
    }

    public class NotificationCenter
    {
        private readonly List<Notification> _notifications = new List<Notification>();

        public void Push(string text, NotificationKind kind)
        {
            var notif = new Notification
            {
                Text = text,
                Kind = kind,
                ElapsedTime = 0f,
                Duration = 3.0f
            };

            _notifications.Add(notif);

            // Limit to maximum of 3 items
            while (_notifications.Count > 3)
            {
                _notifications.RemoveAt(0);
            }
        }

        public void Update(float deltaTime)
        {
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                _notifications[i].ElapsedTime += deltaTime;
                if (_notifications[i].ElapsedTime >= _notifications[i].Duration)
                {
                    _notifications.RemoveAt(i);
                }
            }
        }

        public IReadOnlyList<Notification> GetVisible()
        {
            return _notifications.AsReadOnly();
        }

        public void Clear()
        {
            _notifications.Clear();
        }
    }
}
