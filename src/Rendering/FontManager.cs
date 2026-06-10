using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;
using Raylib_cs;

namespace SSNoir.Rendering
{
    public static class FontManager
    {
        private static Font _font;
        private static bool _loaded = false;

        public static Font Font
        {
            get
            {
                Debug.Assert(_loaded, "FontManager: Font has not been loaded yet. Call LoadFont() first.");
                return _font;
            }
        }

        public static void LoadFont(string fontPath, int fontSize = 48)
        {
            var fullPath = Path.GetFullPath(fontPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Font file not found: {fullPath}");
            }

            // Collect codepoints
            HashSet<char> chars = CollectUniqueCharacters();
            int[] codepoints = chars.Select(c => (int)c).ToArray();

            // Load Font
            _font = Raylib.LoadFontEx(fullPath, fontSize, codepoints, codepoints.Length);
            
            if (_font.Texture.Id == 0)
            {
                throw new Exception($"Failed to load font from path: {fullPath}");
            }

            // Apply Bilinear filtering to make rendering smooth on Retina/scaled windows
            Raylib.SetTextureFilter(_font.Texture, TextureFilter.Bilinear);

            _loaded = true;
        }

        public static void UnloadFont()
        {
            if (_loaded)
            {
                Raylib.UnloadFont(_font);
                _loaded = false;
            }
        }

        public static void DrawText(string text, float x, float y, int fontSize, Color color)
        {
            DrawText(text, new Vector2(x, y), fontSize, color);
        }

        public static void DrawText(string text, Vector2 position, int fontSize, Color color)
        {
            Debug.Assert(_loaded, "FontManager: Cannot draw text before loading font.");
            
            float spacing = 1f;
            Raylib.DrawTextEx(_font, text, position, fontSize, spacing, color);
        }

        public static int MeasureTextWidth(string text, int fontSize)
        {
            Debug.Assert(_loaded, "FontManager: Cannot measure text before loading font.");
            
            float spacing = 1f;
            Vector2 size = Raylib.MeasureTextEx(_font, text, fontSize, spacing);
            return (int)size.X;
        }

        private static string FindProjectRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.GetFiles(current, "*.csproj").Length > 0)
                {
                    return current;
                }
                var parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static HashSet<char> CollectUniqueCharacters()
        {
            var chars = new HashSet<char>();

            // Include basic ASCII
            for (int i = 32; i < 128; i++)
            {
                chars.Add((char)i);
            }

            // Include common Chinese punctuation and special symbols
            string commonPunc = "，。！？；：（）「」『』〈〉《》【】“”‘’、⚔";
            foreach (char c in commonPunc)
            {
                chars.Add(c);
            }

            // Scan directories starting from project root
            var projectRoot = FindProjectRoot();
            string[] dirsToScan = { "scenes", "scripts", "src" };
            foreach (var dir in dirsToScan)
            {
                var fullDir = Path.Combine(projectRoot, dir);
                if (Directory.Exists(fullDir))
                {
                    var files = Directory.GetFiles(fullDir, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var ext = Path.GetExtension(file);
                        if (ext == ".scm" || ext == ".cs")
                        {
                            try
                            {
                                string content = File.ReadAllText(file, System.Text.Encoding.UTF8);
                                foreach (char c in content)
                                {
                                    if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
                                    {
                                        chars.Add(c);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Failed to read file {file} for character extraction: {ex.Message}");
                            }
                        }
                    }
                }
            }

            return chars;
        }
    }
}
