#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>正交投影的立体骰子。贴图由旋转后的立方体求交生成，仍走 GUI 的缩放和裁剪。</summary>
    public static class DiceCubeDrawer
    {
        private const int Size = 80;
        private const int Frames = 24;
        private static readonly Dictionary<int, Texture2D> Cache = new();

        public static void DrawRange(Rect rect, int maximum)
        {
            UnityEngine.Assertions.Assert.IsTrue(maximum >= 1 && maximum <= 6);
            Draw(rect, 6, -1, maximum);
        }

        public static void DrawRolling(Rect rect, int finalValue, float progress)
        {
            int frame = Mathf.Clamp(Mathf.RoundToInt(progress * (Frames - 1)), 0, Frames - 1);
            Draw(rect, finalValue, frame, 6);
        }

        private static void Draw(Rect rect, int finalValue, int frame, int maximum)
        {
            if (Event.current.type != EventType.Repaint) return;
            int key = (HandPanelDrawer.ColorDicePipsOnly ? 10000 : 0) + maximum * 1000 + finalValue * 30 + frame + 1;
            if (!Cache.TryGetValue(key, out var texture))
            {
                texture = Bake(finalValue, frame, maximum);
                Cache.Add(key, texture);
            }
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        private static Texture2D Bake(int finalValue, int frame, int maximum)
        {
            float t = frame < 0 ? 0 : frame / (float)(Frames - 1);
            float remaining = Mathf.Pow(1f - t, 3f);
            // 固定姿态露出 +Z=6、+X=4、+Y=5；动画最后正面朝向玩家。
            Quaternion rotation = frame < 0 ? Quaternion.Euler(28f, -38f, 0f)
                : Quaternion.Euler(remaining * 550f, remaining * 740f, remaining * 100f);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 direction = inverse * Vector3.back;
            var pixels = new Color[Size * Size];
            float view = frame < 0 ? 1.8f : Mathf.Lerp(1.9f, 1.02f, Mathf.SmoothStep(0f,1f,t));
            int sideValue = finalValue == 4 || finalValue == 3 ? 6 : 4;
            int topValue = finalValue == 5 || finalValue == 2 ? (sideValue == 6 ? 4 : 6) : 5;
            int[] faces = { sideValue, 7 - sideValue, topValue, 7 - topValue, finalValue, 7 - finalValue };
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Vector3 origin = inverse * new Vector3((x + .5f - Size / 2f) / Size * view,
                        (y + .5f - Size / 2f) / Size * view, 3f);
                    float near = float.NegativeInfinity, far = float.PositiveInfinity;
                    int axis = -1;
                    for (int a = 0; a < 3; a++)
                    {
                        if (Mathf.Abs(direction[a]) < .00001f)
                        {
                            if (Mathf.Abs(origin[a]) > .5f) { far = float.NegativeInfinity; break; }
                            continue;
                        }
                        float one = (-.5f - origin[a]) / direction[a];
                        float two = (.5f - origin[a]) / direction[a];
                        float entry = Mathf.Min(one, two);
                        if (entry > near) { near = entry; axis = a; }
                        far = Mathf.Min(far, Mathf.Max(one, two));
                    }
                    if (near > far || axis < 0 || far < 0) continue;
                    Vector3 hit = origin + direction * near;
                    int face = axis * 2 + (hit[axis] > 0 ? 0 : 1);
                    int value = faces[face];
                    float u = axis == 0 ? hit.z : hit.x;
                    float v = axis == 1 ? hit.z : hit.y;
                    bool dot = HasDot(value, u, v);
                    HandPanelDrawer.GetDieColors(value, out var background, out var border, out var pip);
                    bool blocked = value > maximum;
                    if (frame < 0)
                    {
                        background = new Color(.025f, .033f, .047f).linear;
                        pip = blocked ? IMGUIStyles.OddsFail : IMGUIStyles.Paper.linear;
                        border = blocked ? IMGUIStyles.OddsFail : IMGUIStyles.Paper.linear;
                        if (blocked) background = new Color(.24f, .065f, .045f).linear;
                    }
                    bool edge = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) > .475f;
                    Vector3 normal = Vector3.zero; normal[axis] = hit[axis] > 0 ? 1 : -1;
                    float light = .70f + .30f * Mathf.Max(0, Vector3.Dot(rotation * normal, new Vector3(-.3f,.5f,1).normalized));
                    Color color = dot ? pip : edge ? border : background * light;
                    color.a = 1f;
                    pixels[y * Size + x] = color;
                }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "HudDiceCube", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        private static bool HasDot(int value, float u, float v)
        {
            for (int row = -1; row <= 1; row++)
                for (int col = -1; col <= 1; col++)
                {
                    bool dot = (value % 2 == 1 && row == 0 && col == 0)
                        || (value >= 2 && row != 0 && row == col)
                        || (value >= 4 && row != 0 && col != 0)
                        || (value == 6 && row == 0 && col != 0);
                    if (dot && new Vector2(u - col * .22f, v - row * .22f).sqrMagnitude < .07f * .07f) return true;
                }
            return false;
        }
    }
}
