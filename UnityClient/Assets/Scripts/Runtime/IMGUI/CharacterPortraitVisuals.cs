#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class CharacterPortraitVisuals
    {
        private static CharacterPortraitSettings? _settings;
        public static CharacterPortraitSettings Settings => _settings != null ? _settings :
            (_settings = Resources.Load<CharacterPortraitSettings>("Portraits/CharacterVisuals")
                ?? throw new InvalidOperationException("缺少人物视觉参数 Portraits/CharacterVisuals"));
        private sealed class Entry { public string Key = ""; public Texture2D? Texture; }
        private static readonly Dictionary<Texture2D, Entry> Cache = new();

        public static Texture2D Adjust(Texture2D source)
        {
            var s = Settings;
            string key = JsonUtility.ToJson(s);
            if (!Cache.TryGetValue(source, out var entry)) Cache.Add(source, entry = new Entry());
            if (entry.Key == key && entry.Texture != null) return entry.Texture;
            var pixels = source.GetPixels();
            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                int i = y * source.width + x;
                Color c = pixels[i];
                float gray = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                c.r = ((Mathf.LerpUnclamped(gray, c.r, s.Saturation) - 0.5f) * s.Contrast + 0.5f) * s.Brightness * (1f + s.Temperature * 0.15f);
                c.g = ((Mathf.LerpUnclamped(gray, c.g, s.Saturation) - 0.5f) * s.Contrast + 0.5f) * s.Brightness;
                c.b = ((Mathf.LerpUnclamped(gray, c.b, s.Saturation) - 0.5f) * s.Contrast + 0.5f) * s.Brightness * (1f - s.Temperature * 0.15f);
                if (s.BottomFade > 0f) c.a *= Mathf.SmoothStep(0f, 1f, y / (source.height * s.BottomFade));
                pixels[i] = c;
            }
            if (entry.Texture != null) UnityEngine.Object.DestroyImmediate(entry.Texture);
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels); texture.Apply(false, true);
            entry.Texture = texture; entry.Key = key;
            return texture;
        }

        public static void Paint(Rect rect, Texture2D source, bool onLeft, float enter, float brightness)
        {
            var saved = GUI.color;
            GUI.color = new Color(brightness, brightness, brightness, enter);
            var uv = onLeft ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
            GUI.DrawTextureWithTexCoords(rect, Adjust(source), uv, true);
            GUI.color = saved;
        }
    }
}
