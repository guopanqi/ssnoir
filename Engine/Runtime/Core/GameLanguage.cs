#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public static class GameLanguage
    {
        public const string Chinese = "zh-CN";
        public const string English = "en";
        private static string _current = Chinese;
        private static readonly HashSet<string> _warnedMissingSpeakers = new(StringComparer.Ordinal);

        /// <summary>
        /// Display language. Switched only on the title screen, never in-game and never saved.
        /// Content convention lives in skills/write-scheme/SKILL.md ("中英双语").
        /// Optional sink for i18n warnings (Unity wires Debug.LogWarning).
        /// </summary>
        public static Action<string>? Warn { get; set; }

        public static string Current
        {
            get => _current;
            set
            {
                if (value != Chinese && value != English)
                    throw new ArgumentException($"Unsupported game language: {value}");
                if (_current != value)
                    _warnedMissingSpeakers.Clear();
                _current = value;
            }
        }

        public static string Tr(string chinese, string english)
        {
            if (string.IsNullOrWhiteSpace(chinese) || string.IsNullOrWhiteSpace(english))
                throw new ArgumentException("tr requires nonempty Chinese and English text");
            return _current == English ? english : chinese;
        }

        public static void WarnMissingSpeaker(string speaker)
        {
            if (_current != English)
                return;
            if (!_warnedMissingSpeakers.Add(speaker))
                return;
            string message = $"[i18n] unregistered speaker (showing zh): {speaker}";
            if (Warn != null)
                Warn(message);
            else
                Console.Error.WriteLine(message);
        }
    }
}
