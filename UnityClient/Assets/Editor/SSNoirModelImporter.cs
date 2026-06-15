#if UNITY_EDITOR
#nullable enable
using UnityEngine;
using UnityEditor;
using Cinemachine;
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
                    // Create a sibling GameObject for the Cinemachine Virtual Camera
                    GameObject vcamGo = new GameObject(cam.name + "_VCam");
                    vcamGo.transform.SetParent(cam.transform.parent, false);
                    vcamGo.transform.localPosition = cam.transform.localPosition;
                    vcamGo.transform.localRotation = cam.transform.localRotation;
                    vcamGo.transform.localScale = cam.transform.localScale;

                    // Configure Cinemachine Virtual Camera
                    var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
                    vcam.m_Lens.FieldOfView = cam.fieldOfView;
                    
                    // Correct Blender 100x unit scale factor for clipping planes (e.g. Near 10 -> 0.1, Far 9999 -> 100)
                    vcam.m_Lens.NearClipPlane = Mathf.Max(0.01f, cam.nearClipPlane / 100f);
                    vcam.m_Lens.FarClipPlane = Mathf.Max(vcam.m_Lens.NearClipPlane + 1.0f, cam.farClipPlane / 100f);
                    vcam.m_Lens.Orthographic = cam.orthographic;
                    vcam.m_Lens.OrthographicSize = cam.orthographicSize;
                    vcam.Priority = 5; // Default priority for node cameras

                    // Configure custom camera config for drag/orbit behavior.
                    var config = vcamGo.AddComponent<SSNoirVirtualCameraConfig>();
                    ConfigureDragMode(config, cam.transform, orbitPivots);

                    // Disable the original Camera node to prevent rendering interference
                    cam.gameObject.SetActive(false);

                    Debug.Log($"[SSNoir] ModelImporter: Converted camera '{cam.name}' to Virtual Camera '{vcamGo.name}' ({DescribeDragMode(config)}).");
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
                    if (!string.IsNullOrEmpty(extractedName) && allVcamComponents != null && allVcamComponents.Length > 0)
                    {
                        // Match camera and anchor by suffix (e.g. "黑市商人")
                        var matchedVcam = allVcamComponents.FirstOrDefault(v => v.name.Contains(extractedName));
                        if (matchedVcam != null)
                        {
                            anchor.FocusVirtualCamera = matchedVcam;
                        }
                        else
                        {
                            anchor.FocusVirtualCamera = allVcamComponents[0];
                            Debug.LogWarning($"[SSNoir] ModelImporter: No VCam name matched NodeAnchor '{anchor.NodeName}' on '{t.name}'. Falling back to first VCam '{allVcamComponents[0].name}'. Configure FocusVirtualCamera manually if this anchor is only an interaction point.");
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
            var normalized = name.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return string.Equals(normalized, "orbitpivot", System.StringComparison.OrdinalIgnoreCase);
        }

        private static Transform? FindClosestOrbitPivot(Transform cameraTransform, Transform[] orbitPivots)
        {
            if (orbitPivots == null || orbitPivots.Length == 0)
                return null;

            Transform? closest = null;
            float closestDistance = float.MaxValue;
            foreach (var pivot in orbitPivots)
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

        private static void ConfigureDragMode(SSNoirVirtualCameraConfig config, Transform cameraTransform, Transform[] orbitPivots)
        {
            var orbitPivot = FindClosestOrbitPivot(cameraTransform, orbitPivots);
            config.orbitPivot = orbitPivot;
            config.dragMode = orbitPivot != null ? CameraDragMode.Orbit : CameraDragMode.Pan;
        }

        private static string DescribeDragMode(SSNoirVirtualCameraConfig config)
        {
            return config.dragMode == CameraDragMode.Orbit
                ? $"Orbit, pivot '{config.orbitPivot!.name}'"
                : "Pan, no orbit pivot found";
        }
    }
}
#endif
