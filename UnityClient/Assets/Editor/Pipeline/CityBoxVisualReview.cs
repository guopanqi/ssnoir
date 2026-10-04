#if UNITY_EDITOR
#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSNoir.Editor
{
    /// <summary>用 Blender 导出的实景、线性材质和机位创建独立 URP 对照，不覆盖 Main。</summary>
    internal static class CityBoxVisualReview
    {
        private const string AssetsRoot = "Assets/CityBoxReview";

        [Serializable] private sealed class MaterialSpec
        {
            public string name = "";
            public Color baseLinear, emissionLinear;
            public float roughness, metallic;
            public bool unlit;
        }
        [Serializable] private sealed class LightSpec
        {
            public string name = "", type = "";
            public Vector3 position, direction, up;
            public Color colorLinear;
            public float energy, size, spotAngle, spotBlend;
            public bool shadows;
        }
        [Serializable] private sealed class CameraSpec
        {
            public string name = "";
            public Vector3 position, direction, up;
            public float verticalFov;
        }
        [Serializable] private sealed class RendererSpec
        {
            public string name = "";
            public string[] materials = Array.Empty<string>();
        }
        [Serializable] private sealed class VisualSpec
        {
            public int version, width, height;
            public float scale, fogStart, fogEnd;
            public bool fog;
            public Color worldLinear;
            public MaterialSpec[] materials = Array.Empty<MaterialSpec>();
            public LightSpec[] lights = Array.Empty<LightSpec>();
            public CameraSpec[] cameras = Array.Empty<CameraSpec>();
            public RendererSpec[] renderers = Array.Empty<RendererSpec>();
        }

        public static void Build()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("请在独立验证工程中 batchmode 执行，避免关闭正在编辑的场景");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("退出 Play Mode 后再生成对照场景");
            var source = Environment.GetEnvironmentVariable("CITYBOX_VISUAL_SOURCE")
                ?? Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../city-box/prefabs/review/noir-study/unity"));
            var folders = Directory.GetDirectories(source).Where(d => File.Exists(Path.Combine(d, "scene.visual.json"))).ToArray();
            var selected = Environment.GetEnvironmentVariable("CITYBOX_VISUAL_SCENES");
            if (!string.IsNullOrEmpty(selected))
            {
                var names = selected.Split('|');
                folders = folders.Where(d => names.Contains(Path.GetFileName(d))).ToArray();
                if (folders.Length != names.Distinct().Count())
                    throw new InvalidOperationException("部分指定场景尚未导出");
            }
            if (folders.Length == 0) throw new InvalidOperationException("先执行 CityBox visual_export.py");
            var pipeline = UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
            pipeline.maxAdditionalLightsCount = 8;
            pipeline.msaaSampleCount = 4;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            pipelineSettings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
            pipelineSettings.FindProperty("m_ReflectionProbeBlending").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(AssetsRoot);
            var renderer = UnityEngine.Object.Instantiate(pipelineSettings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue);
            renderer = SaveAsset(renderer, AssetsRoot + "/ReviewRenderer.asset");
            pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline = SaveAsset(pipeline, AssetsRoot + "/ReviewPipeline.asset");
            var previousPipeline = QualitySettings.renderPipeline;
            try
            {
                QualitySettings.renderPipeline = pipeline;
                foreach (var folder in folders) BuildOne(folder, pipeline);
            }
            finally
            {
                QualitySettings.renderPipeline = previousPipeline;
            }
        }

        public static void RecaptureBanquet()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("只允许独立工程 batchmode");
            EditorSceneManager.OpenScene(AssetsRoot + "/晚宴/review.unity");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetsRoot + "/ReviewPipeline.asset");
            pipeline.msaaSampleCount = 4;
            EditorUtility.SetDirty(pipeline);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/晚宴/FloorAreaHighlight.mat");
            material.SetFloat("_Intensity", 0.12f);
            var floor = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/晚宴/Banquet_floor.mat");
            floor.SetFloat("_EnvironmentReflections", 0);
            floor.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(floor);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            var source = Environment.GetEnvironmentVariable("CITYBOX_VISUAL_SOURCE")!;
            Capture(camera, 1600, 900, Path.Combine(source, "晚宴/unity-Camera_晚宴.png"));
        }

        private static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            asset.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
            return existing;
        }

        private static void BuildOne(string source, RenderPipelineAsset pipeline)
        {
            var name = Path.GetFileName(source);
            var spec = JsonUtility.FromJson<VisualSpec>(File.ReadAllText(Path.Combine(source, "scene.visual.json")));
            if (spec.version != 1 || spec.scale <= 0 || spec.cameras.Length == 0 || spec.renderers.Length == 0)
                throw new InvalidOperationException("无效视觉描述：" + name);
            var assets = AssetsRoot + "/" + name;
            Directory.CreateDirectory(assets);
            File.Copy(Path.Combine(source, "scene.fbx"), assets + "/scene.fbx", true);
            File.Copy(Path.Combine(source, "scene.visual.json"), assets + "/scene.visual.json", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool bake = Environment.GetEnvironmentVariable("CITYBOX_VISUAL_BAKE") == "1";
            if (bake)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(assets + "/scene.fbx");
                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = new GameObject("视觉试用配置");
            settings.SetActive(false);
            settings.AddComponent<SSNoir.CityBoxReviewSettings>().Pipeline = pipeline;
            settings.SetActive(true);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assets + "/scene.fbx");
            if (model == null) throw new InvalidOperationException("FBX 导入失败：" + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.localScale *= spec.scale;
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            Vector3 origin = transforms.Single(t => t.name == "VisualBasis_Origin").position;
            var basis = Matrix4x4.identity;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 vector = transforms.Single(t => t.name == "VisualBasis_" + "XYZ"[axis]).position - origin;
                basis.SetColumn(axis, new Vector4(vector.x, vector.y, vector.z, 0));
            }
            basis.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            float actualScale = basis.GetColumn(0).magnitude;
            if (Mathf.Abs(actualScale - spec.scale) > 0.001f)
                throw new InvalidOperationException("FBX 单位没有对齐：" + actualScale);
            var materials = spec.materials.ToDictionary(m => m.name, m => CreateMaterial(m, assets, bake));
            var renderers = spec.renderers.ToDictionary(r => r.name);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                // FBX 为变换链导出的祖先也可能带隐藏几何；只启用 Blender 当前可见的实景。
                if (!renderers.TryGetValue(renderer.name, out var row))
                {
                    renderer.enabled = false;
                    continue;
                }
                renderer.sharedMaterials = row.materials.Select(n => materials[n]).ToArray();
                bool line = renderer.sharedMaterials.All(m => m.shader.name == "Universal Render Pipeline/Unlit");
                renderer.shadowCastingMode = line ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !line;
                bool movable = renderer.name == "钢琴" || renderer.name.Contains("卡座");
                var extent = renderer.bounds.size;
                bool smallEmitter = renderer.sharedMaterials.Any(m => m.IsKeywordEnabled("_EMISSION"))
                    && Mathf.Min(extent.x, Mathf.Min(extent.y, extent.z)) < 0.05f;
                if (bake && !line && !movable && !smallEmitter)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
                    ((MeshRenderer)renderer).receiveGI = ReceiveGI.Lightmaps;
                }
            }
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = spec.worldLinear.gamma;
            RenderSettings.ambientIntensity = 1;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = spec.fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = spec.worldLinear.gamma;
            RenderSettings.fogStartDistance = spec.fogStart * spec.scale;
            RenderSettings.fogEndDistance = spec.fogEnd * spec.scale;
            foreach (var row in spec.lights) CreateLight(row, spec.scale, basis);
            if (bake)
            {
                var probes = new GameObject("可移动物体光照探针").AddComponent<LightProbeGroup>();
                var points = new System.Collections.Generic.List<Vector3>();
                for (int x = -8; x <= 8; x += 4)
                    for (int y = -12; y <= 16; y += 4)
                        foreach (float z in new[] { 0.3f, 2f, 5f })
                            points.Add(basis.MultiplyPoint3x4(new Vector3(x, y, z)));
                probes.probePositions = points.ToArray();
            }
            if (bake)
            {
                var lighting = new LightingSettings
                {
                    bakedGI = true, realtimeGI = false,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                    lightmapResolution = 160, lightmapMaxSize = 2048,
                    directSampleCount = 512, indirectSampleCount = 128,
                    environmentSampleCount = 32, maxBounces = 2
                };
                // 原 EEVEE 候选没有烘焙间接光；避免另加色彩反弹和发光装饰染色。
                var bakeSettings = new SerializedObject(lighting);
                bakeSettings.FindProperty("m_IndirectOutputScale").floatValue = 0;
                bakeSettings.FindProperty("m_LightmapCompression").intValue = 0;
                bakeSettings.FindProperty("m_LightmapsBakeMode").intValue = (int)LightmapsMode.NonDirectional;
                // 高采样配轻量 Gaussian；避免旧版 OIDN 在本机长时间运行及涂抹。
                bakeSettings.FindProperty("m_PVRFilteringMode").intValue = 2;
                foreach (var channel in new[] { "Direct", "Indirect", "AO" })
                    bakeSettings.FindProperty("m_PVRDenoiserType" + channel).intValue = (int)LightingSettings.DenoiserType.None;
                bakeSettings.FindProperty("m_PVRFilteringGaussRadiusDirect").intValue = 2;
                bakeSettings.ApplyModifiedPropertiesWithoutUndo();
                Lightmapping.lightingSettings = SaveAsset(lighting, assets + "/Lighting.asset");
                LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), assets + "/review.unity");
                if (!Lightmapping.Bake()) throw new InvalidOperationException("Unity 烘焙失败：" + name);
                Debug.Log("[CityBoxReview] 烘焙 lightmaps=" + LightmapSettings.lightmaps.Length);
                // 面光地面由视角相关高光层负责；不再叠加房间探针。
                CreateFloorHighlight(spec, basis, assets, instance);
            }
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = spec.worldLinear.gamma;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 900;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (var view in spec.cameras)
            {
                camera.transform.SetPositionAndRotation(basis.MultiplyPoint3x4(Source(view.position)),
                    Quaternion.LookRotation(basis.MultiplyVector(Source(view.direction)), basis.MultiplyVector(Source(view.up))));
                camera.fieldOfView = view.verticalFov;
                camera.aspect = (float)spec.width / spec.height;
                Capture(camera, spec.width, spec.height, Path.Combine(source, "unity-" + view.name + ".png"));
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), assets + "/review.unity");
            Debug.Log("[CityBoxReview] " + name + " 已导入并渲染；试用配置，非正式发布。");
        }

        private static Material CreateMaterial(MaterialSpec spec, string assets, bool baked)
        {
            var shader = Shader.Find(spec.unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("找不到 URP shader");
            var material = new Material(shader) { name = spec.name };
            material.SetColor("_BaseColor", (spec.unlit ? spec.emissionLinear : spec.baseLinear).gamma);
            if (!spec.unlit)
            {
                material.SetFloat("_Smoothness", 1 - spec.roughness);
                material.SetFloat("_Metallic", spec.metallic);
                // 本轮对照只保留直接光的材质层次，环境探针不污染色块。
                if (baked)
                {
                    material.SetFloat("_EnvironmentReflections", 0);
                    material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
                material.SetColor("_EmissionColor", spec.emissionLinear);
                material.globalIlluminationFlags = spec.emissionLinear.maxColorComponent > 0
                    ? MaterialGlobalIlluminationFlags.None
                    : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                MaterialEditor.FixupEmissiveFlag(material);
                if (spec.emissionLinear.maxColorComponent > 0) material.EnableKeyword("_EMISSION");
            }
            string path = assets + "/" + spec.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) AssetDatabase.CreateAsset(material, path);
            else
            {
                EditorUtility.CopySerialized(material, existing);
                UnityEngine.Object.DestroyImmediate(material);
                material = existing;
            }
            return material;
        }

        private static void CreateFloorHighlight(VisualSpec spec, Matrix4x4 basis, string assets, GameObject instance)
        {
            var floorSpec = spec.materials.Single(m => m.name == "Banquet_floor");
            var shader = Shader.Find("SSNoir/Review/AreaFloorHighlight");
            if (shader == null) throw new InvalidOperationException("缺少地面面光 Shader");
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(assets + "/Banquet_floor.mat");
            floorMaterial.SetFloat("_EnvironmentReflections", 0);
            floorMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(floorMaterial);
            var material = new Material(shader);
            material.SetFloat("_Roughness", floorSpec.roughness);
            material.SetFloat("_Intensity", 0.12f);
            var areas = spec.lights.Where(l => l.type == "AREA").ToArray();
            if (areas.Length > 8) throw new InvalidOperationException("地面面光实验最多支持八盏灯");
            for (int i = 0; i < areas.Length; i++)
            {
                var light = areas[i];
                var pos = basis.MultiplyPoint3x4(Source(light.position));
                var normal = basis.MultiplyVector(Source(light.direction)).normalized;
                material.SetVector("_AreaPosition" + i, new Vector4(pos.x,pos.y,pos.z,light.size * spec.scale * 0.5f));
                material.SetVector("_AreaNormal" + i, new Vector4(normal.x,normal.y,normal.z,0));
                // Blender DISK：出射辐亮度 = 功率 / (面积 × π)。Shader uniform 直接使用线性值。
                var radiance = light.colorLinear * (4 * light.energy / (Mathf.PI * Mathf.PI * light.size * light.size));
                material.SetVector("_AreaRadiance" + i, radiance);
            }
            material = SaveAsset(material, assets + "/FloorAreaHighlight.mat");
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.enabled && r.sharedMaterials.Any(m => m.name == "Banquet_floor")))
            {
                var existingOverlay = renderer.transform.Find("抛光地面面光试验");
                var overlay = existingOverlay != null ? existingOverlay.gameObject
                    : new GameObject("抛光地面面光试验", typeof(MeshFilter), typeof(MeshRenderer));
                overlay.transform.SetParent(renderer.transform, false);
                overlay.GetComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var layer = overlay.GetComponent<MeshRenderer>();
                layer.sharedMaterial = material;
                layer.shadowCastingMode = ShadowCastingMode.Off;
                layer.receiveShadows = false;
            }
        }

        public static void RenderParameters()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("参数预览仅允许独立 batchmode 工程");
            string sceneName = Environment.GetEnvironmentVariable("CITYBOX_PARAMETER_SCENE")
                ?? throw new InvalidOperationException("缺少场景名");
            string input = Environment.GetEnvironmentVariable("CITYBOX_PARAMETER_INPUT")
                ?? throw new InvalidOperationException("缺少参数文件");
            string output = Environment.GetEnvironmentVariable("CITYBOX_PARAMETER_OUTPUT")
                ?? throw new InvalidOperationException("缺少输出目录");
            string assets = AssetsRoot + "/" + sceneName;
            EditorSceneManager.OpenScene(assets + "/review.unity");
            string baseline = File.ReadAllText(assets + "/scene.visual.json");
            string json = File.ReadAllText(input);
            bool needsBake = ApplyParameters(json,sceneName,baseline,
                Environment.GetEnvironmentVariable("CITYBOX_VISUAL_BAKE") != "1");
            if (needsBake)
            {
                if (!Lightmapping.Bake()) throw new InvalidOperationException("参数预览烘焙失败");
            }
            Directory.CreateDirectory(output);
            CaptureParameters(Path.Combine(output,"preview.png"));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            File.WriteAllText(assets + "/scene.visual.json",json);
            File.WriteAllText(Path.Combine(output,"result.json"), "{\"renderer\":\"Unity URP\",\"rebaked\":" + (needsBake ? "true" : "false") + "}");
            Debug.Log("[CityBoxParameters] 真实 Unity 参数预览完成：" + output);
        }

        internal static bool ApplyParameters(string json, string sceneName, string previousJson, bool requireFreshLightmaps = false)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string assets = AssetsRoot + "/" + sceneName;
            if (active.path != assets + "/review.unity")
                throw new InvalidOperationException("请打开对应的 CityBoxReview 试用场景");
            var spec = JsonUtility.FromJson<VisualSpec>(json);
            var previous = JsonUtility.FromJson<VisualSpec>(previousJson);
            if (spec.version != 1 || spec.scale != previous.scale || spec.cameras.Length != 1)
                throw new InvalidOperationException("实时参数契约无效或缩放改变，请完整重建");
            if (!spec.renderers.Select(r => JsonUtility.ToJson(r)).SequenceEqual(previous.renderers.Select(r => JsonUtility.ToJson(r))))
                throw new InvalidOperationException("材质槽或几何集合改变，请完整重建");
            if (!spec.materials.Select(m => m.name).SequenceEqual(previous.materials.Select(m => m.name)) ||
                !spec.lights.Select(l => l.name).SequenceEqual(previous.lights.Select(l => l.name)))
                throw new InvalidOperationException("新增/删除材质或灯光，请完整重建");
            if (!spec.materials.Select(m => m.unlit).SequenceEqual(previous.materials.Select(m => m.unlit)))
                throw new InvalidOperationException("材质照明模式改变，请完整重建");
            var transforms = active.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var basis = Matrix4x4.identity;
            Vector3 origin = transforms.Single(t => t.name == "VisualBasis_Origin").position;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 v = transforms.Single(t => t.name == "VisualBasis_" + "XYZ"[axis]).position - origin;
                basis.SetColumn(axis, new Vector4(v.x,v.y,v.z,0));
            }
            basis.SetColumn(3, new Vector4(origin.x,origin.y,origin.z,1));
            var lights = active.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).ToDictionary(l => l.name);
            bool baked = lights.Values.Any(l => l.lightmapBakeType == LightmapBakeType.Baked);
            bool needsBake = baked && spec.worldLinear != previous.worldLinear;
            needsBake |= spec.lights.Any(row => lights[row.name].lightmapBakeType == LightmapBakeType.Baked
                && JsonUtility.ToJson(row) != JsonUtility.ToJson(previous.lights.Single(l => l.name == row.name)));
            if (needsBake && requireFreshLightmaps)
                throw new InvalidOperationException("烘焙灯光或环境发生变化，请添加 --bake-lights；未生成过期光照预览");
            foreach (var row in spec.materials) CreateMaterial(row, assets, baked);
            foreach (var row in spec.lights)
            {
                var light = lights[row.name];
                bool isBaked = light.lightmapBakeType == LightmapBakeType.Baked;
                ConfigureLight(light,row,spec.scale,basis,isBaked);
                EditorUtility.SetDirty(light);
            }
            var camera = active.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single();
            var view = spec.cameras.Single();
            camera.transform.SetPositionAndRotation(basis.MultiplyPoint3x4(Source(view.position)),
                Quaternion.LookRotation(basis.MultiplyVector(Source(view.direction)),basis.MultiplyVector(Source(view.up))));
            camera.fieldOfView = view.verticalFov;
            camera.aspect = (float)spec.width / spec.height;
            camera.backgroundColor = spec.worldLinear.gamma;
            RenderSettings.ambientLight = spec.worldLinear.gamma;
            RenderSettings.fog = spec.fog;
            RenderSettings.fogColor = spec.worldLinear.gamma;
            RenderSettings.fogStartDistance = spec.fogStart * spec.scale;
            RenderSettings.fogEndDistance = spec.fogEnd * spec.scale;
            var highlight = AssetDatabase.LoadAssetAtPath<Material>(assets + "/FloorAreaHighlight.mat");
            if (highlight != null)
            {
                var model = transforms.Single(t => t.name == "VisualBasis_Origin").root.gameObject;
                CreateFloorHighlight(spec,basis,assets,model);
            }
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(active);
            AssetDatabase.SaveAssets();
            return needsBake;
        }

        internal static void CaptureParameters(string path)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var camera = active.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single();
            Capture(camera, 960, 540, path);
        }

        private static Vector3 Source(Vector3 exported) => new Vector3(-exported.x, exported.z, exported.y);

        private static void CreateLight(LightSpec spec, float scale, Matrix4x4 basis)
        {
            var light = new GameObject(spec.name, typeof(Light)).GetComponent<Light>();
            ConfigureLight(light, spec, scale, basis, Environment.GetEnvironmentVariable("CITYBOX_VISUAL_BAKE") == "1");
        }

        private static void ConfigureLight(Light light, LightSpec spec, float scale, Matrix4x4 basis, bool bake)
        {
            light.transform.SetPositionAndRotation(basis.MultiplyPoint3x4(Source(spec.position)),
                Quaternion.LookRotation(basis.MultiplyVector(Source(spec.direction)), basis.MultiplyVector(Source(spec.up))));
            light.color = spec.colorLinear.gamma;
            light.range = 100 * scale;
            light.shadows = spec.shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.05f;
            light.renderMode = LightRenderMode.ForcePixel;
            if (bake)
            {
                light.lightmapBakeType = LightmapBakeType.Baked;
                if (spec.type == "AREA")
                {
                    light.type = LightType.Area;
                    light.areaSize = Vector2.one * spec.size * scale;
                    light.intensity = 0.35f * spec.energy / (Mathf.PI * spec.size * spec.size);
                    return;
                }
            }
            if (spec.type == "SUN")
            {
                light.type = LightType.Directional;
                light.intensity = spec.energy / Mathf.PI;
                RenderSettings.sun = light;
            }
            else
            {
                light.type = spec.type == "POINT" ? LightType.Point : LightType.Spot;
                // URP 缺少实时 Area；先以同方向宽角 Spot 近似其轴向照度。
                float divisor = spec.type == "AREA" ? Mathf.PI * Mathf.PI : 4 * Mathf.PI * Mathf.PI;
                light.intensity = spec.energy * scale * scale / divisor;
                light.spotAngle = spec.spotAngle;
                light.innerSpotAngle = spec.spotAngle * (1 - spec.spotBlend);
            }
        }

        private static void Capture(Camera camera, int width, int height, string path)
        {
            // Standard 对照：sRGB 8-bit 输出，不能把线性 HDR 像素直接写进 PNG。
            // 以 2× 分辨率渲染后缩小，避免单相机请求的 MSAA resolve 路径产生空白读回。
            var target = RenderTexture.GetTemporary(width * 2, height * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var resolved = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                Graphics.Blit(target, resolved);
                RenderTexture.active = resolved;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                RenderTexture.ReleaseTemporary(resolved);
            }
        }
    }
}
#endif
