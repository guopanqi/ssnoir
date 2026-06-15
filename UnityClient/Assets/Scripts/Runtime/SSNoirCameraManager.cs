#nullable enable
using UnityEngine;
using System.Linq;

namespace SSNoir
{
    public class SSNoirCameraManager
    {
        private readonly SSNoirGameManager _gameManager;
        private readonly float _panSpeed;

        // Mouse drag states
        private bool _isDraggingCam = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        public SSNoirCameraManager(SSNoirGameManager gameManager, float panSpeed)
        {
            _gameManager = gameManager;
            _panSpeed = panSpeed;
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
            return _gameManager.CurrentFocusCamera;
        }

        private Transform? GetOrbitPivot(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Orbit)
                return null;

            if (config.orbitPivot != null)
            {
                return config.orbitPivot;
            }

            string errorMsg = $"[SSNoir] Camera configuration error: Virtual Camera '{activeCamera.name}' is set to Orbit mode, but no orbitPivot was configured on its SSNoirVirtualCameraConfig.";
            Debug.LogError(errorMsg);
            UnityEngine.Assertions.Assert.IsTrue(false, errorMsg);
            throw new System.InvalidOperationException(errorMsg);
        }
    }
}
