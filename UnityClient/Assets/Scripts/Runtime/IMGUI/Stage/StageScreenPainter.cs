#nullable enable
using SSNoir.Rendering;
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 舞台的画面层：人物背后的环境和人物前面的闪光。对象是整个屏幕，不是某个人。
    public static class StageScreenPainter
    {
        private const float StageWashAlpha = 0.26f;

        // 环境。黑夜里是一层深色遮罩压暗世界，人物那一圈由灯自己的暗晕理干净；
        // 负片时先让后处理把世界那幅画翻过来（黑底白线变白底黑线，城市的轮廓还在），
        // 再盖一层薄一些的纸白——翻白的城市要看得见，纸只是把它压远。
        public static void PaintBackdrop(float negative)
        {
            SSNoirStylizeMaterial.StageInvert = negative;
            var full = new Rect(0f, 0f, UIScale.VW, UIScale.VH);
            GUI.color = new Color(0.004f, 0.009f, 0.020f, 0.78f * (1f - negative));
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            if (negative > 0f)
            {
                GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.45f * negative);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // 舞台不在的时候要把世界翻回来。
        public static void Release()
        {
            SSNoirStylizeMaterial.StageInvert = 0f;
        }

        // 满台色光：从人物脚下往外的一大团径向色，把对面的黑也染上一点。
        // 纸上没有色光：翻涌是一团灰从这个人脚下渗到对面去。
        public static void PaintWash(Rect rect, Color color, float strength, float negative)
        {
            if (strength <= 0.001f) return;
            var wash = new Rect(
                rect.center.x - rect.width * 2.6f,
                rect.center.y - rect.height * 1.1f,
                rect.width * 5.2f,
                rect.height * 2.2f);
            if (negative < 1f)
            {
                GUI.color = new Color(color.r, color.g, color.b, StageWashAlpha * strength * (1f - negative));
                GUI.DrawTexture(wash, NeonPortraitLibrary.RadialFalloff());
            }
            if (negative > 0f)
            {
                var ink = IMGUIStyles.Ink;
                GUI.color = new Color(ink.r, ink.g, ink.b, StageWashAlpha * 0.45f * strength * negative);
                GUI.DrawTexture(wash, NeonPortraitLibrary.RadialFalloff());
            }
            GUI.color = Color.white;
        }

        // 白闪：一帧全白，然后在四分之一秒里散掉。负片里闪的是黑。
        public static void PaintFlash(float alpha, float negative)
        {
            if (alpha <= 0f) return;
            float v = 1f - negative;
            GUI.color = new Color(v, v, v, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
