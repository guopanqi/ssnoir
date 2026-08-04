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
        public override uint GetVersion() => 2;

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
                    vcam.m_Lens.FieldOfView = cam.fieldOfView;
                    
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

                    // Configure custom camera config for drag/orbit behavior.
                    var config = vcamGo.AddComponent<SSNoirVirtualCameraConfig>();
                    ConfigureDragMode(config, orbitPivot);

                    // Disable the original Camera node to prevent rendering interference
                    cam.gameObject.SetActive(false);

                    Debug.Log($"[SSNoir] ModelImporter: Converted camera '{cam.name}' to Virtual Camera '{vcamGo.name}' ({DescribeDragMode(config)}; clip {nearClip:F3}..{farClip:F3}, source '{Path.GetExtension(assetPath)}').");
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
                            anchor.FocusVirtualCamera = scopedVcams[0];
                            if (scopedVcams.Length > 1)
                            {
                                Debug.LogWarning($"[SSNoir] ModelImporter: NodeAnchor '{anchor.NodeName}' has no exact camera '{expectedCameraName}' and its asset subtree contains {scopedVcams.Length} VCams. Using '{scopedVcams[0].name}'. Add an exact camera name to remove ambiguity.");
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
            {
                if (config.orbitPivot == null)
                {
                    if (config.dragMode != CameraDragMode.Pan)
                        Debug.LogWarning($"[SSNoir] ModelImporter: VCam '{config.name}' had Orbit mode without an orbit pivot. Forcing Pan mode.");
                    config.dragMode = CameraDragMode.Pan;
                }
                else
                {
                    config.dragMode = CameraDragMode.Orbit;
                }

                EditorUtility.SetDirty(config);
            }
        }

        private static bool IsOrbitPivotName(string name)
        {
            var normalized = StripBlenderNumericSuffix(name)
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return string.Equals(normalized, "orbitpivot", StringComparison.OrdinalIgnoreCase);
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
            // 从节点父级向上找第一个含候选对象的资产子树，但不允许上升到整城根。
            // 单体模型的 Anchor/Camera 可能恰好都是导入根的直接子节点；这种情况下
            // 只有全资产唯一候选才可安全回退。
            for (var scope = source.parent; scope != null && scope != importRoot; scope = scope.parent)
            {
                var matches = candidates
                    .Where(candidate => IsSameOrChildOf(getTransform(candidate), scope))
                    .ToArray();
                if (matches.Length > 0)
                    return matches;
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

            var scopedPivots = FindNearestScopedComponents(
                cameraTransform, importRoot, orbitPivots, pivot => pivot);
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

        private static void ConfigureDragMode(
            SSNoirVirtualCameraConfig config,
            Transform? orbitPivot)
        {
            config.orbitPivot = orbitPivot;
            config.dragMode = orbitPivot != null ? CameraDragMode.Orbit : CameraDragMode.Pan;
            if (orbitPivot == null)
                return;

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

        private static string DescribeDragMode(SSNoirVirtualCameraConfig config)
        {
            return config.dragMode == CameraDragMode.Orbit
                ? $"Orbit, pivot '{config.orbitPivot!.name}', pitch band " +
                  $"{config.minPitch:F1}..{config.maxPitch:F1}"
                : "Pan, no orbit pivot found";
        }
    }
}
#endif
