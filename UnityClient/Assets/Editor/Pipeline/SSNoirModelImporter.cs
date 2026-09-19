#if UNITY_EDITOR
#nullable enable
using UnityEngine;
using UnityEditor;
using Cinemachine;
using System;
using System.IO;
using System.Linq;

namespace SSNoir.Editor
{
    /// <summary>
    /// Automatically processes imported Blender/FBX models.
    /// Part 1: Converts imported Camera nodes into Cinemachine Virtual Cameras and corrects Blender 100x scale offsets.
    /// Part 2: Configures camera drag mode from optional orbit pivot markers.
    /// Part 3: Identifies Anchor nodes, configures NodeAnchor components, extracts NodeNames, and links corresponding FocusVirtualCameras.
    /// </summary>
    public class SSNoirModelImporter : AssetPostprocessor
    {
        // Bump this whenever serialized importer output changes so existing model assets
        // are reprocessed instead of keeping stale generated VCams in the import cache.
        public override uint GetVersion() => 14;   // 13：地点文件按 clips.json 切道具 clip

        // CityBox 相机一律 50mm、36×24 传感器、按 16:9 标定（pipeline/export.py 强制焦距）。
        // Blender 的 FBX 导出把 FieldOfView 写成**水平**视角（39.6°），Unity 却当**竖直**视角用，
        // 结果游戏里比 Blender 预览宽一圈、东西小一圈、按像素标定的描线也细一圈。
        // 这里把水平角按 16:9 换回竖直角（26.99°）；已经是竖直角的原样保留。
        private const float BlenderHorizontalFov50mm = 39.5978f;
        private const float CalibrationAspect = 16f / 9f;

        private static float VerticalFieldOfView(float imported, string cameraName)
        {
            if (Mathf.Abs(imported - BlenderHorizontalFov50mm) > 0.5f)
            {
                Debug.Log($"[SSNoir] ModelImporter: '{cameraName}' FOV {imported:F2}° 不是 Blender 的 50mm 水平角，按竖直角原样使用。");
                return imported;
            }
            float vertical = 2f * Mathf.Atan(Mathf.Tan(imported * 0.5f * Mathf.Deg2Rad) / CalibrationAspect) * Mathf.Rad2Deg;
            Debug.Log($"[SSNoir] ModelImporter: '{cameraName}' FOV {imported:F2}°（Blender 水平角）→ 竖直 {vertical:F2}°。");
            return vertical;
        }

        /// <summary>
        /// CityBox 发布的世界层和地点细节层都使用 FBX 自带的材质描述。
        /// 这样两层遵循同一套色彩转换，不再让 Places 通过同名工程 .mat 得到另一种明暗结果。
        /// </summary>
        private void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.EndsWith("/Resources/Models/Environment/City.fbx", StringComparison.Ordinal))
            {
                ConfigureEmbeddedMaterials((ModelImporter)assetImporter);
                return;
            }

            if (!path.Contains("/Resources/" + CityPlaces.ResourcesFolder))
                return;
            var importer = (ModelImporter)assetImporter;
            ConfigureEmbeddedMaterials(importer);
            // 地点细节文件只有几何：相机、灯都在 City.fbx 里
            importer.importCameras = false;
            importer.importLights = false;
            ConfigurePropClips(importer, path);
        }

        /// <summary>
        /// 会动的道具（CityBox pipeline/motion.py）：整个地点的动画是一个 take，旁边的
        /// <c>&lt;名&gt;.clips.json</c> 说哪段帧是哪个 clip（<c>道具__状态</c>）、是否循环。这里按它切成
        /// Legacy clip，运行时 <see cref="PropMotion"/> 按名字播。没有 sidecar 的地点不导动画。
        /// </summary>
        private static void ConfigurePropClips(ModelImporter importer, string path)
        {
            string sidecar = Path.ChangeExtension(path, null) + ".clips.json";
            if (!File.Exists(sidecar))
            {
                importer.importAnimation = false;
                return;
            }
            var spec = PropClips.Parse(File.ReadAllText(sidecar));
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            string take = importer.importedTakeInfos.Length > 0 ? importer.importedTakeInfos[0].name : string.Empty;
            var clips = spec.clips.Select(c => new ModelImporterClipAnimation
            {
                name = c.name,
                takeName = take,
                firstFrame = c.start,
                lastFrame = c.end,
                loopTime = c.loop,
                loop = c.loop,
                wrapMode = c.loop ? WrapMode.Loop : WrapMode.ClampForever,
            }).ToArray();
            importer.clipAnimations = clips;
            var takes = string.Join(", ", importer.importedTakeInfos.Select(t => $"{t.name} {t.startTime * spec.fps:F1}-{t.stopTime * spec.fps:F1}f"));
            Debug.Log($"[SSNoir] ModelImporter: '{Path.GetFileName(path)}' take[{takes}] 切出 {clips.Length} 个道具 clip：{string.Join(", ", clips.Select(c => $"{c.name}[{c.firstFrame}-{c.lastFrame}{(c.loopTime ? " loop" : string.Empty)}]"))}");
        }

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/" + CityPlaces.ResourcesFolder))
                return;
            // 对着 clips.json 核对切出来的长度：帧数对不上就是 take 起点和 Blender 帧号没对齐
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var byPath = bindings.GroupBy(b => b.path.Substring(b.path.LastIndexOf('/') + 1))
                .Select(g => $"{g.Key}[{string.Join(",", g.Select(b => b.propertyName.Replace("m_Local", "")).Distinct())}]");
            Debug.Log($"[SSNoir] ModelImporter: clip '{clip.name}' length={clip.length * clip.frameRate:F1}f @{clip.frameRate}fps loop={clip.isLooping} curves: {string.Join(" ", byPath)}");
        }

        private static void ConfigureEmbeddedMaterials(ModelImporter importer)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnTextureName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
        }

        private void OnPostprocessModel(GameObject root)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var orbitPivots = allTransforms.Where(t => IsOrbitPivotName(t.name)).ToArray();

            // ==========================================
            // PART 1: Process Cameras & Convert to VCam
            // ==========================================
            var cameras = root.GetComponentsInChildren<Camera>(true);
            if (cameras != null && cameras.Length > 0)
            {
                foreach (var cam in cameras)
                {
                    var orbitPivot = FindClosestOrbitPivot(
                        cam.transform, root.transform, orbitPivots);

                    // Create a sibling GameObject for the Cinemachine Virtual Camera
                    GameObject vcamGo = new GameObject(cam.name + "_VCam");
                    vcamGo.transform.SetParent(cam.transform.parent, false);
                    vcamGo.transform.localPosition = cam.transform.localPosition;
                    vcamGo.transform.localRotation = cam.transform.localRotation;
                    vcamGo.transform.localScale = cam.transform.localScale;

                    // Configure Cinemachine Virtual Camera
                    var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
                    vcam.m_Lens.FieldOfView = VerticalFieldOfView(cam.fieldOfView, cam.name);
                    
                    // Unity's direct .blend importer reports camera clip planes in
                    // centimetres; an authored FBX already carries the correct units.
                    // Applying the old /100 correction to City.fbx reduced a 100-unit
                    // far plane to 1 and clipped the whole building during focus travel.
                    float clipScale = GetClipPlaneScale(assetPath);
                    float nearClip = Mathf.Max(0.01f, cam.nearClipPlane * clipScale);
                    float farClip = Mathf.Max(nearClip + 1.0f, cam.farClipPlane * clipScale);
                    if (orbitPivot != null)
                    {
                        // An orbit shot must at least reach its own subject. Use the
                        // imported asset-space distance so this remains safe even when
                        // the final scene instance is uniformly scaled.
                        float pivotDistance = Vector3.Distance(
                            cam.transform.position, orbitPivot.position);
                        farClip = Mathf.Max(farClip, pivotDistance * 1.25f);
                    }

                    vcam.m_Lens.NearClipPlane = nearClip;
                    vcam.m_Lens.FarClipPlane = farClip;
                    vcam.m_Lens.Orthographic = cam.orthographic;
                    vcam.m_Lens.OrthographicSize = cam.orthographicSize;
                    vcam.Priority = 5; // Default priority for node cameras

                    bool isPortalCamera = cam.name.StartsWith("PortalIn_", StringComparison.Ordinal);
                    // Portal Camera 只描述穿门路径，不是玩家可操纵的焦点相机。
                    var config = isPortalCamera ? null : vcamGo.AddComponent<SSNoirVirtualCameraConfig>();
                    if (config != null)
                        config.modelRoot = root.transform;
                    // 机位类型由 Prefab 里看得见的对象决定，没有默认分支：
                    //   orbit pivot → Orbit；PanBounds_<名> → Pan；都没有 → Static；都有 → 错。
                    var panBounds = isPortalCamera ? null : FindPanBounds(cam.transform);
                    if (!isPortalCamera && orbitPivot != null && panBounds != null)
                        throw new InvalidOperationException(
                            $"[SSNoir] ModelImporter: '{cam.name}' has both an orbit pivot and '{panBounds.name}'. A camera is Orbit or Pan, not both.");
                    if (!isPortalCamera)
                    {
                        // 非 Portal 分支必然创建配置；显式断言也让 nullable 分析和运行时契约一致。
                        Debug.Assert(config != null);
                        if (orbitPivot != null)
                            ConfigureOrbit(config!, orbitPivot);
                        else if (panBounds != null)
                            ConfigurePan(config!, panBounds, root.transform);
                        else
                            config!.dragMode = CameraDragMode.Static;
                    }

                    // Disable the original Camera node to prevent rendering interference
                    cam.gameObject.SetActive(false);

                    string cameraKind = isPortalCamera ? "Portal" : DescribeDragMode(config!);
                    Debug.Log($"[SSNoir] ModelImporter: Converted camera '{cam.name}' to Virtual Camera '{vcamGo.name}' ({cameraKind}; clip {nearClip:F3}..{farClip:F3}, source '{Path.GetExtension(assetPath)}').");
                }
            }

            // ==========================================
            // PART 2: Process Nodes starting with 'anchor'
            // ==========================================
            var allVcamComponents = root.GetComponentsInChildren<CinemachineVirtualCamera>(true);

            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith("anchor", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure NodeAnchor component exists
                    var anchor = t.gameObject.GetComponent<NodeAnchor>();
                    if (anchor == null)
                    {
                        anchor = t.gameObject.AddComponent<NodeAnchor>();
                    }

                    // Extract NodeName (everything after the first underscore)
                    int underscoreIndex = t.name.IndexOf('_');
                    string extractedName = (underscoreIndex != -1 && underscoreIndex < t.name.Length - 1)
                        ? t.name.Substring(underscoreIndex + 1)
                        : string.Empty;
                    anchor.NodeName = extractedName;

                    // Bind matching FocusVirtualCamera
                    if (!string.IsNullOrEmpty(extractedName) && allVcamComponents.Length > 0)
                    {
                        var scopedVcams = FindNearestScopedComponents(
                            t, root.transform, allVcamComponents,
                            v => v.transform);
                        var expectedCameraName = $"Camera_{extractedName}_VCam";
                        var matchedVcam = scopedVcams.FirstOrDefault(v =>
                            string.Equals(v.name, expectedCameraName, StringComparison.Ordinal));
                        if (matchedVcam != null)
                        {
                            anchor.FocusVirtualCamera = matchedVcam;
                        }
                        else if (scopedVcams.Length > 0)
                        {
                            // 行动点通常没有专属 Camera，应该共享所属建筑的主相机。
                            // 这里绝不能回退到整座城市的第一个 VCam，否则整城 FBX 中
                            // 一个酒馆行动点可能会聚焦到码头或诊所。
                            // 建筑里可以嵌套别的 Prefab（酒馆里有后巷，后巷带自己的相机），
                            // 所以先找"所属子树根自己的主相机" Camera_<根名>_VCam，再退回第一台。
                            var scopeRoot = t.parent;
                            var mainCameraName = scopeRoot != null ? $"Camera_{scopeRoot.name}_VCam" : string.Empty;
                            var mainVcam = scopedVcams.FirstOrDefault(v =>
                                string.Equals(v.name, mainCameraName, StringComparison.Ordinal));
                            anchor.FocusVirtualCamera = mainVcam != null ? mainVcam : scopedVcams[0];
                            if (mainVcam == null && scopedVcams.Length > 1)
                            {
                                Debug.LogWarning($"[SSNoir] ModelImporter: NodeAnchor '{anchor.NodeName}' has no exact camera '{expectedCameraName}' nor a main camera '{mainCameraName}'; its asset subtree contains {scopedVcams.Length} VCams. Using '{scopedVcams[0].name}'.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[SSNoir] ModelImporter: NodeAnchor '{anchor.NodeName}' has no VCam in its own asset subtree. FocusVirtualCamera remains unassigned.");
                        }
                    }

                    Debug.Log($"[SSNoir] ModelImporter: Configured NodeAnchor on '{t.name}' (NodeName='{anchor.NodeName}') linking VCam: {(anchor.FocusVirtualCamera != null ? anchor.FocusVirtualCamera.name : "None")}.");
                }
            }

            foreach (var config in root.GetComponentsInChildren<SSNoirVirtualCameraConfig>(true))
                EditorUtility.SetDirty(config);

            ConfigureStagePortals(root);
        }

        private static void ConfigureStagePortals(GameObject root)
        {
            var vcams = root.GetComponentsInChildren<CinemachineVirtualCamera>(true);
            var anchors = root.GetComponentsInChildren<NodeAnchor>(true);
            var ins = vcams.Where(v => v.name.StartsWith("PortalIn_", StringComparison.Ordinal)).ToArray();

            // 一台 PortalIn 就是一个 Portal：挂到同名 Stage 的 Anchor 上。Stage 内的揭幕由
            // StageTransitionController 从交锋根机位算出来，不再要求 PortalOut。
            foreach (var inCam in ins)
            {
                string context = inCam.name.Substring("PortalIn_".Length);
                if (context.EndsWith("_VCam", StringComparison.Ordinal))
                    context = context.Substring(0, context.Length - "_VCam".Length);
                var anchorMatches = anchors.Where(a => string.Equals(a.NodeName, context, StringComparison.Ordinal)).ToArray();
                if (anchorMatches.Length != 1)
                    throw new InvalidOperationException(
                        $"[SSNoir] Portal '{context}' requires exactly one Anchor; got {anchorMatches.Length}.");

                var portal = anchorMatches[0].GetComponent<StagePortalConfig>()
                    ?? anchorMatches[0].gameObject.AddComponent<StagePortalConfig>();
                portal.IntroCam = inCam;
                EditorUtility.SetDirty(portal);
                Debug.Log($"[SSNoir] ModelImporter: Configured Stage Portal '{context}' on Anchor '{anchorMatches[0].NodeName}' ({portal.IntroCam.name}).");
            }
        }

        /// <summary>
        /// 规范名是 `OrbitPivot_<名>`（与 Anchor_/Camera_/PanBounds_ 同一套，整城里唯一，不会被 Blender 加 .001）。
        /// 判定按"去掉空格、下划线、连字符和数字后缀后以 orbitpivot 开头"，所以独立资产里的 `orbit pivot` 也认。
        /// </summary>
        private static bool IsOrbitPivotName(string name)
        {
            var normalized = StripBlenderNumericSuffix(name)
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return normalized.StartsWith("orbitpivot", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetClipPlaneScale(string modelAssetPath)
        {
            return string.Equals(
                Path.GetExtension(modelAssetPath), ".blend",
                StringComparison.OrdinalIgnoreCase)
                ? 0.01f
                : 1.0f;
        }

        private static string StripBlenderNumericSuffix(string name)
        {
            var dot = name.LastIndexOf('.');
            if (dot < 0 || dot == name.Length - 1)
                return name;

            return name.Skip(dot + 1).All(char.IsDigit)
                ? name.Substring(0, dot)
                : name;
        }

        private static T[] FindNearestScopedComponents<T>(
            Transform source,
            Transform importRoot,
            T[] candidates,
            Func<T, Transform> getTransform)
        {
            // 语义对象的作用域就是它的父节点 —— 那是它所属 Prefab 的根：
            //   City / 老街酒馆 / { Camera_老街酒馆, orbit pivot, Anchor_*, 巷子里在打人 / { Camera_巷子里在打人, Anchor_* } }
            // 不向上爬。爬会让嵌套 Prefab（后巷）的相机借到外层（酒馆）的 pivot，Pan 机位被误判成 Orbit。
            // 单体模型的 Anchor/Camera 可能恰好都是导入根的直接子节点；这种情况下
            // 只有全资产唯一候选才可安全回退。
            var scope = source.parent;
            if (scope != null && scope != importRoot)
            {
                return candidates
                    .Where(candidate => IsSameOrChildOf(getTransform(candidate), scope))
                    .ToArray();
            }

            return candidates.Length == 1 ? candidates : Array.Empty<T>();
        }

        private static bool IsSameOrChildOf(Transform candidate, Transform scope)
        {
            for (var current = candidate; current != null; current = current.parent)
            {
                if (current == scope)
                    return true;
            }

            return false;
        }

        private static Transform? FindClosestOrbitPivot(
            Transform cameraTransform,
            Transform importRoot,
            Transform[] orbitPivots)
        {
            if (orbitPivots.Length == 0)
                return null;

            // 作用域内可能含嵌套 Prefab 的 pivot（酒馆作用域里有后巷的）：只取直接挂在同一根下的。
            var scopedPivots = FindNearestScopedComponents(
                    cameraTransform, importRoot, orbitPivots, pivot => pivot)
                .Where(p => p.parent == cameraTransform.parent)
                .ToArray();
            Transform? closest = null;
            float closestDistance = float.MaxValue;
            foreach (var pivot in scopedPivots)
            {
                float distance = Vector3.SqrMagnitude(cameraTransform.position - pivot.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = pivot;
                }
            }

            return closest;
        }

        private static void ConfigureOrbit(SSNoirVirtualCameraConfig config, Transform orbitPivot)
        {
            config.dragMode = CameraDragMode.Orbit;
            config.orbitPivot = orbitPivot;

            // The authored shot is always a legal orbit position. Widening the band to
            // contain it is what keeps the player's first drag from snapping the camera
            // vertically onto a limit the artist never agreed to; the default band is
            // only a floor, so a shot already inside it changes nothing.
            const float AuthoredPitchMargin = 1f;
            Vector3 offset = config.transform.position - orbitPivot.position;
            float horizontal = new Vector2(offset.x, offset.z).magnitude;
            float authoredPitch = Mathf.Atan2(offset.y, horizontal) * Mathf.Rad2Deg;
            config.minPitch = Mathf.Min(config.minPitch, authoredPitch - AuthoredPitchMargin);
            config.maxPitch = Mathf.Max(config.maxPitch, authoredPitch + AuthoredPitchMargin);
        }

        /// <summary>同根的 `PanBounds_<名>`：Blender 里是一块贴地的框，构建时换成一个 Empty，角点是它的两个子 Empty。</summary>
        private static Transform? FindPanBounds(Transform cameraTransform)
        {
            const string prefix = "Camera_";
            if (!cameraTransform.name.StartsWith(prefix, StringComparison.Ordinal) || cameraTransform.parent == null)
                return null;
            return cameraTransform.parent.Find("PanBounds_" + cameraTransform.name.Substring(prefix.Length));
        }

        /// <summary>
        /// 机身能站的范围 = 两个角点子 Empty（`_min` / `_max`）在模型空间里的 XZ 包围盒。
        /// 角点用子物体的**位置**表示，不用 Empty 的 scale：FBX 导出把单位换算烘进每个 Prefab 根的
        /// scale（=100），子物体的位置随之缩小、scale 却不会——按 scale 算出来的边界会大 100 倍，
        /// 相机永远夹不到。位置和相机自己走的是同一套换算，所以这里不手写任何轴或单位。
        /// </summary>
        private static void ConfigurePan(SSNoirVirtualCameraConfig config, Transform bounds, Transform root)
        {
            var lo = bounds.Find(bounds.name + "_min");
            var hi = bounds.Find(bounds.name + "_max");
            if (lo == null || hi == null)
                throw new InvalidOperationException(
                    $"[SSNoir] ModelImporter: '{bounds.name}' needs '{bounds.name}_min' and '{bounds.name}_max' child empties for its corners. Rebuild City.fbx with the current CityBox pipeline.");

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in new[] { lo, hi })
            {
                Vector3 p = root.InverseTransformPoint(corner.position);
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            config.dragMode = CameraDragMode.Pan;
            config.usePanBounds = true;
            config.panBoundsMinXZ = min;
            config.panBoundsMaxXZ = max;
            bounds.gameObject.SetActive(false);
        }

        private static string DescribeDragMode(SSNoirVirtualCameraConfig config)
        {
            return config.dragMode switch
            {
                CameraDragMode.Orbit => $"Orbit, pivot '{config.orbitPivot!.name}', pitch band {config.minPitch:F1}..{config.maxPitch:F1}",
                CameraDragMode.Pan => $"Pan, bounds x {config.panBoundsMinXZ.x:F1}..{config.panBoundsMaxXZ.x:F1} z {config.panBoundsMinXZ.y:F1}..{config.panBoundsMaxXZ.y:F1} (model space)",
                _ => "Static",
            };
        }
    }
}
#endif
