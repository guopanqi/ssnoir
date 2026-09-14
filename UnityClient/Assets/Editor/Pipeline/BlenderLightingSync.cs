#if UNITY_EDITOR
#nullable enable
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SSNoir.Editor
{
    /// <summary>
    /// 把 city-box/city/city_greybox.blend 的光照搬进当前场景。
    ///
    /// 数值是拿 Blender 跑脚本读出来的，不是照着截图凑的：
    ///   World Background = (0.012, 0.018, 0.032) linear，Strength 1
    ///   SUN   energy 1.15  color (0.72, 0.80, 1.00)  euler (54, 0, -128)
    ///   FILL  energy 0.42  color (0.44, 0.62, 1.00)  euler (64, 0,   58)
    ///
    /// 角度不能照搬。Blender 是 Z-up 右手系、灯默认朝 -Z，Unity 是 Y-up 左手系、灯朝 +Z。
    /// 把两边的方向向量都解出来对齐之后，在 Blender 没有绕 Y 转的前提下规律很干净：
    ///     Unity pitch = 90 - Blender X
    ///     Unity yaw   = -Blender Z
    /// 于是 SUN 落在 (36, 128, 0)、FILL 落在 (26, -58, 0)。
    ///
    /// 颜色用 .gamma 转一道。Blender 面板里的 RGB 是线性值，而 Unity 的 C# 颜色 API 收的是
    /// sRGB 显示值（Inspector 里显示的那个），差一次编码——直接搬会明显偏暗。
    ///
    /// 环境光必须从 Skybox 切成 Flat。留着 Skybox 模式的话，那张内置亮天空会继续从四面八方
    /// 补光，暗部永远被抬起来，建筑怎么调都是灰的——这正是导入后"变白天"的主因。
    ///
    /// 强度要除一个 π。Blender 的 sun energy 是辐照度，漫反射出射 = albedo/π × E × cosθ；
    /// URP 的 Lambert 没有这个 1/π，就是 albedo × 强度 × cosθ。energy 直接抄成 intensity，
    /// 全城会亮 3.14 倍——墙面从深蓝变中灰，这就是"导进 Unity 就发白"的真正大头
    /// （不是 AgX：把 Blender 的 view transform 从 AgX 换成 Standard 实测几乎没差别，
    /// 只有窗光的饱和度动了一点）。所以 SUN 落在 1.15/π、FILL 落在 0.42/π。
    ///
    /// 环境光不用除。恒定环境的辐照度是 πL，漫反射出射 = albedo/π × πL = albedo × L，
    /// π 自己抵掉了，Blender 的 world 颜色可以原样搬。
    ///
    /// AgX 依然没有等价物，但实测它在这个亮度区间几乎不起作用，剩下的差异不在这儿。
    /// Global Volume 里的 Tonemapping（Neutral）是美术决策，这里不碰。
    /// </summary>
    internal static class BlenderLightingSync
    {
        private const string SunName = "SUN";
        private const string FillName = "FILL";

        // 以下全部是 Blender 里的线性值，赋值时再统一 .gamma 转成 Unity 的显示值。
        private static readonly Color WorldBackgroundLinear = new Color(0.012f, 0.018f, 0.032f, 1f);
        private static readonly Color SunColorLinear = new Color(0.72f, 0.80f, 1.00f, 1f);
        private static readonly Color FillColorLinear = new Color(0.44f, 0.62f, 1.00f, 1f);

        //  Blender 的 sun energy（辐照度），赋值时除以 π 换成 URP 的灯强度。
        private const float SunEnergy = 1.15f;
        private const float FillEnergy = 0.42f;

        private static readonly Vector3 SunEuler = new Vector3(36f, 128f, 0f);
        private static readonly Vector3 FillEuler = new Vector3(26f, -58f, 0f);

        // 距离雾 = Blender 的 Mist（city-box/pipeline/config.py 的 MIST_START / MIST_DEPTH，线性衰减）。
        // Blender 以米计，City 在 Unity 里缩放 0.1，所以除以 10。颜色就是 world 背景色。
        private const float FogStart = 800f * 0.1f;
        private const float FogEnd = (800f + 2200f) * 0.1f;

        [MenuItem("SSNoir/City/对齐 Blender 光照")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("不能在 Play Mode 中改场景光照");

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("没有已加载的活动场景");

            Undo.SetCurrentGroupName("Sync Blender Lighting");
            int undoGroup = Undo.GetCurrentGroup();

            // 环境：Blender 的 world 就是一个恒定颜色，对应 Unity 的 Flat。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = WorldBackgroundLinear.gamma;
            RenderSettings.ambientIntensity = 1f;

            // 天空盒必须一起撤掉。只把环境光切成 Flat 的话，那张内置亮天空不再供光了，
            // 但它还挂在那儿——Scene 视图自己就会画它（它不看相机的 clearFlags），
            // 任何还用 Skybox 清屏的相机也照画不误。Blender 的 world 是一个纯色，
            // 没有天空盒这层东西，撤掉才是真的对齐。
            RenderSettings.skybox = null;

            // 环境改完要刷一次，否则已经烘进去的环境光探针还是旧天空的颜色。
            DynamicGI.UpdateEnvironment();

            // 距离雾：远处往背景色里融。用 Linear 才能和 Blender 的 Mist 起止对上。
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = WorldBackgroundLinear.gamma;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            var sun = ResolveSun();
            ConfigureLight(sun, SunColorLinear, SunEnergy, SunEuler, LightShadows.Soft);

            var fill = ResolveFill();
            // 补光不投影。两盏灯各投一套阴影会在同一面墙上交叉出两组影子，Blender 那边
            // EEVEE 的软阴影糊得看不出来，URP 这边会很明显。
            ConfigureLight(fill, FillColorLinear, FillEnergy, FillEuler, LightShadows.None);

            ApplyCameraBackground();

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "[SSNoir] 已按 city_greybox.blend 对齐光照：环境改为 Flat 暗蓝、天空盒撤掉、"
                + "SUN/FILL 两盏冷色平行光、线性距离雾、相机背景改为纯色。"
                + "主光阴影还要 URP Asset 里 Main Light > Cast Shadows 打开才生效。AgX 的色调映射没有搬"
                + "（URP 无等价物），要补请在 Global Volume 里加 Tonemapping = Neutral。");
        }

        /// <summary>
        /// 主光：优先认名字，其次接管场景里已有的那盏平行光——场景本来就只有一盏"Directional
        /// Light"，把它改造成 SUN 比再加一盏干净，否则两盏主光叠在一起亮度直接翻倍。
        /// </summary>
        private static Light ResolveSun()
        {
            var byName = GameObject.Find(SunName);
            if (byName != null)
            {
                var existing = byName.GetComponent<Light>();
                if (existing != null)
                    return existing;
            }

            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>(true))
            {
                if (light.type != LightType.Directional || light.name == FillName)
                    continue;

                Undo.RecordObject(light.gameObject, "Rename to SUN");
                light.gameObject.name = SunName;
                return light;
            }

            return CreateLight(SunName);
        }

        private static Light ResolveFill()
        {
            var byName = GameObject.Find(FillName);
            if (byName != null)
            {
                var existing = byName.GetComponent<Light>();
                if (existing != null)
                    return existing;
            }

            return CreateLight(FillName);
        }

        private static Light CreateLight(string name)
        {
            var go = new GameObject(name, typeof(Light));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go.GetComponent<Light>();
        }

        private static void ConfigureLight(
            Light light, Color linearColor, float energy, Vector3 euler, LightShadows shadows)
        {
            Undo.RecordObject(light, "Configure Light");
            Undo.RecordObject(light.transform, "Orient Light");

            light.type = LightType.Directional;
            light.color = linearColor.gamma;
            //  energy 是 Blender 的辐照度，URP 的 intensity 少一个 1/π —— 见类注释。
            light.intensity = energy / Mathf.PI;
            light.shadows = shadows;
            light.transform.rotation = Quaternion.Euler(euler);
        }

        private static void ApplyCameraBackground()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[SSNoir] 没找到 MainCamera，背景色没改。");
                return;
            }

            Undo.RecordObject(camera, "Set Camera Background");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorldBackgroundLinear.gamma;
        }
    }
}
#endif
