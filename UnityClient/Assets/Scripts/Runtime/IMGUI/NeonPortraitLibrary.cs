#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 霓虹立绘的共享入口：Resources/Portraits/Neon/<人物名>。
    // 贴图是纯黑底、靠 Alpha From Grayscale 拿到 alpha 的发光图，黑等于透明。
    // 对白舞台和左下角人物簇都从这里取，缓存只有一份。
    public readonly struct PortraitLayers
    {
        public readonly Texture2D? Lines;
        public readonly Texture2D? Accent;
        public readonly Stage.PortraitSkeleton? Skeleton;
        public bool HasLayers => Lines != null && Accent != null;

        public PortraitLayers(Texture2D? lines, Texture2D? accent, Stage.PortraitSkeleton? skeleton)
        {
            Lines = lines; Accent = accent; Skeleton = skeleton;
        }
    }

    public static class NeonPortraitLibrary
    {
        private const string ResourceRoot = "Portraits/Neon/";

        // 全身招牌里头到腰那一块，给 HUD 上立起来的半身像用。
        // 方画布上人物只占中间一条；换构图不同的立绘时调这里。
        public static readonly Rect BustCrop = new Rect(0.28f, 0.50f, 0.44f, 0.48f);
        // 半身像再往上取的那一小块：只剩头和肩线，给 banter 气泡里的头像用。
        // 头像很小，肩以下的东西进来只会糊成一团。
        public static readonly Rect HeadCrop = new Rect(0.33f, 0.66f, 0.30f, 0.30f);

        private static readonly Dictionary<string, Texture2D?> Cache = new();
        private static Texture2D? _radialFalloff;
        private static Texture2D? _verticalFade;

        public static Texture2D? Load(string characterName)
        {
            if (Cache.TryGetValue(characterName, out var cached))
                return cached;

            var texture = Resources.Load<Texture2D>(ResourceRoot + characterName);
            Cache[characterName] = texture;
            return texture;
        }

        // 姿势变体：Resources/Portraits/Neon/<人物>_<姿势>。同一个人的招牌换一组灯管。
        // 缺图返回 null，由调用方决定退回基础立绘并报警——内容里写了姿势就该有图。
        public static Texture2D? LoadPose(string characterName, string pose)
        {
            return Load(characterName + "_" + pose);
        }

        // 离线加工出来的分层与骨架（tools/portrait-neon/process.py）：
        //   <名>_lines   去掉点缀色的线稿，运行时染任意颜色
        //   <名>_accent  点缀色蒙版，单独上色——人物的标志色可以随剧情变
        //   <名>.neon    管子骨架折线
        // 没跑过加工的图这些都是 null，舞台退回整张图直接画。
        private static readonly Dictionary<Texture2D, PortraitLayers> Layers = new();

        public static PortraitLayers LayersOf(Texture2D portrait)
        {
            if (Layers.TryGetValue(portrait, out var cached))
                return cached;
            string name = portrait.name;
            var lines = Load(name + "_lines");
            var accent = Load(name + "_accent");
            var json = Resources.Load<TextAsset>(ResourceRoot + name + ".neon");
            var skeleton = json != null ? Stage.PortraitSkeleton.Parse(json.text) : null;
            var layers = new PortraitLayers(lines, accent, skeleton);
            Layers[portrait] = layers;
            return layers;
        }

        // 径向渐变：中心 alpha 1、边缘 0。当作「一笔画完的软圆」用，
        // 拉成任意椭圆都不会出现拿横条堆渐变时那种阶梯边。
        public static Texture2D RadialFalloff()
        {
            if (_radialFalloff != null)
                return _radialFalloff;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    // 指数略大于 1：中心一大片接近满值（平顶），到边缘再收干净。
                    // 用平方会让浓的那一圈太小，人物旁边就压不住。
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(a, 1.3f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _radialFalloff = tex;
            return tex;
        }

        // 竖直渐变：画面上端全透、下端全不透。用来把倒影或立绘的截断边一次性抹掉。
        public static Texture2D VerticalFade()
        {
            if (_verticalFade != null)
                return _verticalFade;

            const int height = 128;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                // GUI 的 y 轴朝下，纹理的 y 轴朝上：这里 y=0 对应画面下端。
                float k = y / (float)(height - 1);
                pixels[y] = new Color(1f, 1f, 1f, (1f - k) * (1f - k));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _verticalFade = tex;
            return tex;
        }
    }
}
