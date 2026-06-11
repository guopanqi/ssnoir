#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Cinemachine;
using System.Linq;

namespace SSNoir.Editor
{
    /// <summary>
    /// Automatically processes imported Blender/FBX models.
    /// Part 1: Converts imported Camera nodes into Cinemachine Virtual Cameras and corrects Blender 100x scale offsets.
    /// Part 2: Identifies Anchor nodes, configures NodeAnchor components, extracts NodeNames, and links corresponding FocusVirtualCameras.
    /// </summary>
    public class SSNoirModelImporter : AssetPostprocessor
    {
        private void OnPostprocessModel(GameObject root)
        {
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

                    // Configure custom camera config for orbit rotation
                    var config = vcamGo.AddComponent<SSNoirVirtualCameraConfig>();
                    config.dragMode = CameraDragMode.Orbit;

                    // Disable the original Camera node to prevent rendering interference
                    cam.gameObject.SetActive(false);

                    Debug.Log($"[SSNoir] ModelImporter: Converted camera '{cam.name}' to Virtual Camera '{vcamGo.name}'.");
                }
            }

            // ==========================================
            // PART 2: Process Nodes starting with 'anchor'
            // ==========================================
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
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
                        : t.name;
                    anchor.NodeName = extractedName;

                    // Bind matching FocusVirtualCamera
                    if (allVcamComponents != null && allVcamComponents.Length > 0)
                    {
                        // Match camera and anchor by suffix (e.g. "黑市商人")
                        var matchedVcam = allVcamComponents.FirstOrDefault(v => v.name.Contains(extractedName));
                        if (matchedVcam != null)
                        {
                            anchor.FocusVirtualCamera = matchedVcam;
                        }
                        else
                        {
                            // Fallback to the first generated virtual camera
                            anchor.FocusVirtualCamera = allVcamComponents[0];
                        }
                    }

                    Debug.Log($"[SSNoir] ModelImporter: Configured NodeAnchor on '{t.name}' (NodeName='{anchor.NodeName}') linking VCam: {(anchor.FocusVirtualCamera != null ? anchor.FocusVirtualCamera.name : "None")}.");
                }
            }
        }
    }
}
#endif
