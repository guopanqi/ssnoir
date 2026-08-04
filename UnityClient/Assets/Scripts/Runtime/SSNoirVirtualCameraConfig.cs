#nullable enable
using UnityEngine;

namespace SSNoir
{
    public enum CameraDragMode
    {
        Pan,
        Orbit
    }

    public class SSNoirVirtualCameraConfig : MonoBehaviour
    {
        [Header("Drag Mode")]
        public CameraDragMode dragMode = CameraDragMode.Pan;

        [Header("Orbit Settings")]
        [Tooltip("Required when Drag Mode is Orbit. This is the stable scene-space orbit center, not the node interaction anchor.")]
        public Transform? orbitPivot;
        public float orbitSpeedX = 0.2f;
        public float orbitSpeedY = 0.05f;

        [Header("Pitch (Up/Down) Limits")]
        public float minPitch = 10f;
        public float maxPitch = 25f;

        // Persistent start values captured when drag begins
        private float _startYaw;
        private float _startPitch;
        private float _startRadius;

        // The pose this camera was authored with in the scene. Dragging and focus
        // transitions both move the transform at runtime, so the designed framing is
        // remembered once, before anything can touch it, and stays the destination
        // every time focus returns to this building.
        private Vector3 _authoredPosition;
        private Quaternion _authoredRotation;
        private bool _authoredPoseCaptured;

        public Vector3 AuthoredPosition
        {
            get
            {
                CaptureAuthoredPose();
                return _authoredPosition;
            }
        }

        public Quaternion AuthoredRotation
        {
            get
            {
                CaptureAuthoredPose();
                return _authoredRotation;
            }
        }

        private void Awake()
        {
            CaptureAuthoredPose();
        }

        private void CaptureAuthoredPose()
        {
            if (_authoredPoseCaptured)
                return;

            _authoredPosition = transform.position;
            _authoredRotation = transform.rotation;
            _authoredPoseCaptured = true;
        }

        public void SaveDragStartState(Vector3 pivotPosition)
        {
            Vector3 offset = transform.position - pivotPosition;
            _startRadius = offset.magnitude;

            // Calculate current yaw and pitch relative to the pivot position
            Vector3 direction = offset.normalized;
            _startPitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            _startYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            // Clamp initial pitch to defined limits
            _startPitch = Mathf.Clamp(_startPitch, minPitch, maxPitch);
        }

        public void ApplyOrbitFromDrag(Vector3 pivotPosition, float deltaX, float deltaY)
        {
            // Calculate Yaw (horizontal orbit: mouse horizontal delta drives yaw)
            float yaw = _startYaw + deltaX * orbitSpeedX;

            // Calculate Pitch (vertical orbit: mouse vertical delta drives pitch)
            float pitch = _startPitch - deltaY * orbitSpeedY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Reconstruct new 3D coordinates on the sphere
            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            float y = _startRadius * Mathf.Sin(pitchRad);
            float x = _startRadius * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
            float z = _startRadius * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

            transform.position = pivotPosition + new Vector3(x, y, z);
            transform.LookAt(pivotPosition);
        }

        [ContextMenu("Align to Pivot")]
        public void AlignToPivot()
        {
            Transform? pivot = orbitPivot;
            if (pivot == null)
            {
                Debug.LogWarning($"[SSNoir] AlignToPivot failed on '{name}': Orbit Pivot is not configured.");
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(transform, "Align Camera to Pivot");
#endif

            transform.LookAt(pivot.position);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
            Debug.Log($"[SSNoir] Aligned camera '{name}' to look at pivot '{pivot.name}' at position {pivot.position}.");
        }

    }
}

#if UNITY_EDITOR
namespace SSNoir
{
    using UnityEditor;

    [CustomEditor(typeof(SSNoirVirtualCameraConfig))]
    public class SSNoirVirtualCameraConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (SSNoirVirtualCameraConfig)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Align Camera to Pivot (自动对齐中心点)"))
            {
                config.AlignToPivot();
            }
        }
    }
}
#endif
