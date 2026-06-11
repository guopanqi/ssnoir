#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Cinemachine;

namespace SSNoir.Editor
{
    public class SSNoirModelCameraImporter : AssetPostprocessor
    {
        // When a model (FBX/Blend) is imported, Unity automatically calls this method
        private void OnPostprocessModel(GameObject root)
        {
            // Recursively search for all Camera components in the imported model hierarchy
            var cameras = root.GetComponentsInChildren<Camera>(true);
            if (cameras == null || cameras.Length == 0) return;

            foreach (var cam in cameras)
            {
                // 1. Create a sibling GameObject for the Cinemachine Virtual Camera at the same transform location
                GameObject vcamGo = new GameObject(cam.name + "_VCam");
                vcamGo.transform.SetParent(cam.transform.parent, false);
                vcamGo.transform.localPosition = cam.transform.localPosition;
                vcamGo.transform.localRotation = cam.transform.localRotation;
                vcamGo.transform.localScale = cam.transform.localScale;

                // 2. Add CinemachineVirtualCamera component and copy lens settings
                var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
                vcam.m_Lens.FieldOfView = cam.fieldOfView;
                vcam.m_Lens.NearClipPlane = cam.nearClipPlane;
                vcam.m_Lens.FarClipPlane = cam.farClipPlane;
                vcam.m_Lens.Orthographic = cam.orthographic;
                vcam.m_Lens.OrthographicSize = cam.orthographicSize;
                vcam.Priority = 5; // Default focus priority

                // 3. Automatically add our custom Orbit/Pan configuration component
                var config = vcamGo.AddComponent<SSNoirVirtualCameraConfig>();
                config.dragMode = CameraDragMode.Orbit; // Default to Orbit for local node cameras

                // 4. Disable the original camera GameObject to prevent rendering interference
                cam.gameObject.SetActive(false);

                Debug.Log($"[SSNoir] Importer: Automatically processed camera '{cam.name}' in model '{root.name}'. Created virtual camera '{vcamGo.name}' and disabled the original camera.");
            }
        }
    }
}
#endif
