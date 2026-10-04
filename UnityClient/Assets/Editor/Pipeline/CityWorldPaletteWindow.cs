#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir.Editor
{
    /// <summary>编辑正式色盘与共享材质；所有机位仍消费同一套资产。</summary>
    public sealed class CityWorldPaletteWindow : EditorWindow
    {
        private CityWorldPalette _palette = null!;
        private SerializedObject _serialized = null!;
        private Snapshot? _baseline;
        private Vector2 _scroll;
        private bool _surfaces = true, _lighting = true, _lines = true, _river = true, _film;
        private string _status = "";
        private static readonly string[] BuildingRoles = {
            "M_建筑", "M_建筑_civic", "M_建筑_downtown", "M_建筑_enclave", "M_建筑_newgrid",
            "M_建筑_newport", "M_建筑_oldport", "M_建筑_oldtown", "M_建筑_outskirt", "M_建筑_village"
        };
        [Serializable] private sealed class ColorValue { public string Property = ""; public Color Value; }
        [Serializable] private sealed class FloatValue { public string Property = ""; public float Value; }
        [Serializable] private sealed class MaterialState
        {
            public string Path = "";
            public ColorValue[] Colors = Array.Empty<ColorValue>();
            public FloatValue[] Floats = Array.Empty<FloatValue>();
        }
        [Serializable] private sealed class Snapshot
        {
            public CityWorldOverview.Settings Overview = new();
            public CityWorldOverview.NightSettings Night = new();
            public CityWorldPalette.FocusSettings Focus = new();
            public MaterialState[] Materials = Array.Empty<MaterialState>();
        }

        [MenuItem("SSNoir/City/世界视觉调节")]
        public static void Open() => GetWindow<CityWorldPaletteWindow>("世界视觉调节");
        private void OnEnable()
        {
            _palette = CityWorldPalette.Load();
            _serialized = new SerializedObject(_palette);
            _baseline = Capture();
            Undo.undoRedoPerformed += OnUndo;
            minSize = new Vector2(360, 500);
        }
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
        private void OnUndo() { Apply(); Repaint(); }
        private Material Role(string name) => _palette.FindMaterial(name)
            ?? throw new InvalidOperationException("世界色盘缺少共享材质：" + name);
        private Material[] SharedMaterials() => _palette.Materials.Select(r => r.Material)
            .Append(_palette.WorldLandmarkLine).Distinct().ToArray();
        private void OnGUI()
        {
            _serialized.Update();
            EditorGUILayout.HelpBox("正式共享色盘：调色、颗粒和材质同时作用于世界与地点。世界配光与高度雾仅用于远景；地点保留局部配光与距离雾。配光修改后点击应用。", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _surfaces = EditorGUILayout.Foldout(_surfaces, "建筑与地面 · 统一色系", true);
            if (_surfaces)
            {
                EditorGUILayout.LabelField("建筑组保留各街区的相对明暗差。", EditorStyles.wordWrappedMiniLabel);
                BuildingColor();
                MaterialColor(Role("M_重要建筑"), "重要建筑表面", "_BaseColor");
                MaterialColor(Role("M_城市地面"), "城市地面", "_BaseColor");
                MaterialColor(Role("M_街区"), "街区底座", "_BaseColor");
                MaterialColor(Role("M_干道"), "道路", "_BaseColor");
                MaterialColor(Role("M_郊野"), "郊野", "_BaseColor");
                MaterialColor(Role("M_驳岸"), "驳岸", "_BaseColor");
            }
            _lighting = EditorGUILayout.Foldout(_lighting, "世界配光与层次", true);
            if (_lighting)
            {
                Field("Overview.Ambient", "环境光 · 暗面可读性");
                Field("Overview.KeyColor", "主光颜色");
                Slider("Overview.KeyIntensity", "主光强度", 0, 8);
                Slider("Overview.KeyDirection.x", "主光俯角", 5, 85);
                Slider("Overview.KeyDirection.y", "主光方位", -180, 180);
                Slider("Overview.FillIntensity", "补光强度", 0, 2);
                Slider("Overview.ShadowStrength", "阴影深度", 0, 1);
                Slider("Night.WarmLight", "暖光照亮建筑", 0, 1.5f);
                Slider("Night.LampGlow", "沿街小灯晕", 0, 1.5f);
                Slider("Night.LampPool", "地面光池", 0, 2);
                Slider("Night.AccentPoolGain", "大片光池亮度", 0, .4f);
                Slider("Night.AccentPoolSize", "大片光池范围", 20, 200);
                Slider("Night.DevelopedLampGain", "发达岸沿街灯", 0, 1);
                Slider("Night.PoorLampGain", "贫穷岸沿街灯", 0, 1);
                Slider("Night.HeadlightRange", "汽车头灯照射长度", 0, 12);
                Slider("Night.HeadlightGain", "汽车头灯亮度", 0, 1.5f);
                Field("Night.FogLow", "低处雾色");
                Field("Night.FogHigh", "高处雾色");
                Slider("Night.FogDensity", "高度雾密度", 0, .003f);
                Slider("Night.FogFalloff", "高度雾衰减", .001f, .04f);
                Slider("Night.Haze", "远处薄雾", 0, .001f);
                Slider("Night.Mist", "流动雾", 0, 1);
            }
            EditorGUILayout.LabelField("聚焦地点与切换", EditorStyles.boldLabel);
            Slider("Focus.TransitionSeconds", "配光与雾过渡（秒）", 0, 2);
            Slider("Focus.BloomMultiplier", "近景辉光 · 世界强度比例", 0, 1);
            Slider("Focus.LocalFogStrength", "地点距离雾强度", 0, 1);
            Slider("Focus.AmbientMultiplier", "近景环境光倍率", 0, 3);
            Slider("Focus.LightMultiplier", "近景配光倍率", 0, 3);
            _lines = EditorGUILayout.Foldout(_lines, "描线 · 远景地标与建筑细节分开", true);
            if (_lines)
            {
                Line(_palette.WorldLandmarkLine, "地标远景线");
                Line(Role("次线"), "普通建筑次线");
                Line(Role("主线"), "地点主线");
                Line(Role("结构线"), "建筑结构线");
                Slider("Night.Bloom", "基础辉光强度（世界）", 0, 1);
                Slider("Night.BloomScatter", "辉光扩散范围", 0, 1);
                Slider("Night.BloomThreshold", "辉光阈值", 0, 3);
            }
            _river = EditorGUILayout.Foldout(_river, "河流 · 共享水面与波纹", true);
            if (_river)
            {
                var material = Role("河面");
                MaterialColor(material, "水面底色", "_BaseColor");
                MaterialColor(material, "波纹颜色", "_LineColor", true);
                MaterialFloat(material, "波纹亮度", "_LineStrength", 0, 2);
                MaterialFloat(material, "远景波纹强度", "_FarLineStrength", 0, 1);
                MaterialFloat(material, "波纹减弱起点（米）", "_LineFadeStart", 0, 1500);
                MaterialFloat(material, "波纹减弱终点（米）", "_LineFadeEnd", 1501, 3000);
                MaterialFloat(material, "波纹宽度", "_LineWidth", .002f, .25f);
                MaterialFloat(material, "波纹间距", "_LineSpacing", .15f, 5);
                MaterialFloat(material, "交叉波纹", "_CrossingStrength", 0, 1);
                MaterialFloat(material, "流速", "_DriftSpeed", -3, 3);
            }
            _film = EditorGUILayout.Foldout(_film, "共用胶片与整体调色 · 世界／地点", true);
            if (_film)
            {
                Slider("Night.Exposure", "曝光", .3f, 2.5f);
                Slider("Night.Contrast", "对比度", .7f, 1.5f);
                Slider("Night.Saturation", "饱和度", 0, 1.5f);
                Slider("Night.Grain", "胶片颗粒", 0, .05f);
                Slider("Night.Vignette", "暗角", 0, 1);
            }
            EditorGUILayout.EndScrollView();
            _serialized.ApplyModifiedProperties();
            if (GUILayout.Button("应用到当前画面")) Apply();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存这版")) { Apply(); AssetDatabase.SaveAssets(); _status += "；已保存正式资产"; }
            if (GUILayout.Button("导出参数…")) Export();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("撤回到本次打开面板时")) Restore();
            EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedMiniLabel);
        }
        private void Field(string path, string label) => EditorGUILayout.PropertyField(_serialized.FindProperty(path), new GUIContent(label));
        private void Slider(string path, string label, float min, float max) => EditorGUILayout.Slider(_serialized.FindProperty(path), min, max, new GUIContent(label));
        private static Color Pick(string label, Color color, bool hdr) => EditorGUILayout.ColorField(new GUIContent(label), color, true, false, hdr);
        private static void SetColor(Material material, string property, Color color)
        {
            material.SetColor(property, color);
            if (property == "_BaseColor" && material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
        }
        private void MaterialColor(Material material, string label, string property, bool hdr = false)
        {
            var before = material.GetColor(property); var after = Pick(label, before, hdr);
            if (before == after) return;
            Undo.RecordObject(material, label); SetColor(material, property, after);
            SceneView.RepaintAll();
        }
        private void MaterialFloat(Material material, string label, string property, float min, float max)
        {
            float before = material.GetFloat(property), after = EditorGUILayout.Slider(label, before, min, max);
            if (before == after) return;
            Undo.RecordObject(material, label); material.SetFloat(property, after); EditorUtility.SetDirty(material);
            SceneView.RepaintAll();
        }
        private void BuildingColor()
        {
            var before = Role("M_建筑").GetColor("_BaseColor"); var after = Pick("建筑统一基色", before, false);
            Color.RGBToHSV(after, out float hue, out float saturation, out float brightness);
            float newBrightness = EditorGUILayout.Slider("建筑表面明度", brightness, 0, .5f);
            float newSaturation = EditorGUILayout.Slider("建筑颜色饱和度", saturation, 0, 1);
            if (newBrightness != brightness || newSaturation != saturation)
                after = Color.HSVToRGB(hue, newSaturation, newBrightness);
            if (before == after) return;
            var materials = BuildingRoles.Select(Role).ToArray(); Undo.RecordObjects(materials, "建筑统一色系");
            // 以母色的亮度差和色差移动整组，而不是把十种街区压成同一种颜色。
            foreach (var material in materials)
            {
                var color = material.GetColor("_BaseColor");
                float relative = color.grayscale / Mathf.Max(.00001f, before.grayscale);
                var value = after * relative; value.a = color.a;
                SetColor(material, "_BaseColor", value);
            }
            SceneView.RepaintAll();
        }
        private void Line(Material material, string label)
        {
            MaterialColor(material, label + "颜色（HDR）", "_BaseColor", true);
            var color = material.GetColor("_BaseColor"); float gain = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float value = EditorGUILayout.Slider(label + "亮度", gain, 0, 4);
            if (value == gain) return;
            Undo.RecordObject(material, label + "亮度");
            SetColor(material, "_BaseColor", gain > .00001f ? color * (value / gain) : new Color(value, value, value, 1));
            color = material.GetColor("_BaseColor"); color.a = 1; SetColor(material, "_BaseColor", color);
            SceneView.RepaintAll();
        }
        private void Apply()
        {
            int count = CityWorldVisuals.RefreshEditorPalette(); SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            _status = count > 0 ? "已刷新正式配光；当前聚焦地点保持不变" : "参数已更新；进入 Play 的世界视角即可查看";
        }
        private Snapshot Capture()
        {
            var materials = new List<MaterialState>();
            foreach (var material in SharedMaterials())
            {
                var colors = new List<ColorValue>(); var floats = new List<FloatValue>();
                for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                {
                    string name = material.shader.GetPropertyName(i); var type = material.shader.GetPropertyType(i);
                    if (type == ShaderPropertyType.Color) colors.Add(new ColorValue { Property = name, Value = material.GetColor(name) });
                    if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)
                        floats.Add(new FloatValue { Property = name, Value = material.GetFloat(name) });
                }
                materials.Add(new MaterialState { Path = AssetDatabase.GetAssetPath(material), Colors = colors.ToArray(), Floats = floats.ToArray() });
            }
            return new Snapshot {
                Overview = JsonUtility.FromJson<CityWorldOverview.Settings>(JsonUtility.ToJson(_palette.Overview)),
                Night = JsonUtility.FromJson<CityWorldOverview.NightSettings>(JsonUtility.ToJson(_palette.Night)),
                Focus = JsonUtility.FromJson<CityWorldPalette.FocusSettings>(JsonUtility.ToJson(_palette.Focus)),
                Materials = materials.ToArray()
            };
        }
        private void Restore()
        {
            var snapshot = _baseline ?? throw new InvalidOperationException("调节面板缺少打开时的快照");
            Undo.RecordObjects(SharedMaterials().Cast<UnityEngine.Object>().Append(_palette).ToArray(), "撤回本次视觉调节");
            _palette.Overview = JsonUtility.FromJson<CityWorldOverview.Settings>(JsonUtility.ToJson(snapshot.Overview));
            _palette.Night = JsonUtility.FromJson<CityWorldOverview.NightSettings>(JsonUtility.ToJson(snapshot.Night));
            _palette.Focus = JsonUtility.FromJson<CityWorldPalette.FocusSettings>(JsonUtility.ToJson(snapshot.Focus));
            EditorUtility.SetDirty(_palette);
            foreach (var row in snapshot.Materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(row.Path);
                foreach (var color in row.Colors) material.SetColor(color.Property, color.Value);
                foreach (var value in row.Floats) material.SetFloat(value.Property, value.Value);
                EditorUtility.SetDirty(material);
            }
            Apply(); _status += "；已撤回到打开面板时的参数";
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("导出世界视觉参数", "", "世界视觉参数", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(Capture(), true)); _status = "已导出：" + path;
        }
    }
}
