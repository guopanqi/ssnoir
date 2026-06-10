#nullable enable
using UnityEngine;
using System.Linq;

namespace SSNoir
{
    public class SSNoirCameraManager
    {
        private readonly SSNoirGameManager _gameManager;
        private Cinemachine.CinemachineVirtualCamera? _globalCamera;
        private readonly float _panSpeed;

        // Mouse drag states
        private bool _isDraggingCam = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        public SSNoirCameraManager(SSNoirGameManager gameManager, Cinemachine.CinemachineVirtualCamera? globalCamera, float panSpeed)
        {
            _gameManager = gameManager;
            _globalCamera = globalCamera;
            _panSpeed = panSpeed;
        }

        public void SetGlobalCamera(Cinemachine.CinemachineVirtualCamera? globalCam)
        {
            _globalCamera = globalCam;
        }

        public void Update()
        {
            var activeCamera = GetActiveCamera();
            if (activeCamera == null) return;

            // Handle Camera Drag Panning / Orbiting
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _isDraggingCam = true;
                _dragStartMousePos = Input.mousePosition;
                _dragStartCamPos = activeCamera.transform.position;

                // Save starting极坐标 (polar coordinates) state on virtual camera config if in Orbit mode
                var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
                if (config != null && config.dragMode == CameraDragMode.Orbit)
                {
                    var pivot = GetOrbitPivot(activeCamera);
                    if (pivot != null)
                    {
                        config.SaveDragStartState(pivot.position);
                    }
                }
            }

            if (_isDraggingCam)
            {
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    Vector3 mouseDelta = Input.mousePosition - _dragStartMousePos;

                    var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
                    if (config != null && config.dragMode == CameraDragMode.Orbit)
                    {
                        var pivot = GetOrbitPivot(activeCamera);
                        if (pivot != null)
                        {
                            // Apply orbit rotation using cumulative drag mouseDelta
                            config.ApplyOrbitFromDrag(pivot.position, mouseDelta.x, mouseDelta.y);
                        }
                    }
                    else
                    {
                        // Height-locked RTS/MOBA Pan (moves parallel to XZ ground plane)
                        Vector3 right = activeCamera.transform.right;
                        right.y = 0f;
                        right.Normalize();

                        Vector3 forward = activeCamera.transform.forward;
                        forward.y = 0f;
                        forward.Normalize();

                        Vector3 panTranslation = -mouseDelta.x * right * _panSpeed - mouseDelta.y * forward * _panSpeed;
                        activeCamera.transform.position = _dragStartCamPos + panTranslation;
                    }
                }
                else
                {
                    _isDraggingCam = false;
                }
            }
        }

        public Cinemachine.CinemachineVirtualCamera? GetActiveCamera()
        {
            var sd = _gameManager.SceneDirectory;
            if (sd != null)
            {
                foreach (var a in sd.AllAnchors)
                {
                    if (a.FocusVirtualCamera != null && a.FocusVirtualCamera.Priority > 10)
                        return a.FocusVirtualCamera;
                }
            }
            return _globalCamera;
        }

        private Transform? GetOrbitPivot(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config != null && config.orbitPivot != null)
            {
                return config.orbitPivot;
            }

            // Fallback 1: Find the NodeAnchor in the SceneDirectory that references this virtual camera
            var sd = _gameManager.SceneDirectory;
            if (sd != null)
            {
                var matchingAnchor = sd.AllAnchors.FirstOrDefault(a => a.FocusVirtualCamera == activeCamera);
                if (matchingAnchor != null)
                {
                    return matchingAnchor.transform;
                }
            }

            // Fallback 2: Find the NodeAnchor that corresponds to the focused node name
            string focusedNode = _gameManager.FocusedNodeName;
            if (!string.IsNullOrEmpty(focusedNode))
            {
                var anchor = _gameManager.SceneDirectory?.GetAnchor(focusedNode);
                if (anchor != null)
                {
                    return anchor.transform;
                }
            }

            // If we are in Orbit mode but cannot resolve a pivot, it is a configuration error
            string errorMsg = $"[SSNoir] Camera configuration error: Virtual Camera '{activeCamera.name}' is set to Orbit mode, but no pivot was configured on its SSNoirVirtualCameraConfig, and no matching NodeAnchor could be resolved.";
            Debug.LogError(errorMsg);
            UnityEngine.Assertions.Assert.IsTrue(false, errorMsg);
            throw new System.InvalidOperationException(errorMsg);
        }
    }
}
