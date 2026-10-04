#nullable enable
using UnityEngine;

namespace SSNoir
{
    public enum CameraDragMode
    {
        Pan,
        Orbit,
        Static
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
        // 低机位仍保留，但 10° 太贴地，建筑和卡片容易挤成一层；默认上提一档，
        // 让玩家能获得更清楚的空间关系，又不把城市拍成俯视地图。
        public float minPitch = 16f;
        public float maxPitch = 35f;

        [Header("Model Space")]
        [Tooltip("导入器指定：这台相机所属模型的根。Pan Bounds 与裁剪面按它的变换换算成世界值；为空则视为已是世界值。")]
        public Transform? modelRoot;

        [Header("Pan Bounds")]
        [Tooltip("Only applies to Pan cameras. X / Y = model-space X / Z（modelRoot 为空时即世界 X / Z）。")]
        public bool usePanBounds = false;
        public Vector2 panBoundsMinXZ = new Vector2(-25f, -70f);
        public Vector2 panBoundsMaxXZ = new Vector2(95f, 90f);

        /// <summary>世界空间的机身 XZ 边界，由模型空间的 panBounds 经 modelRoot 换算。</summary>
        public Vector2 WorldPanMinXZ => WorldPanBounds().min;
        public Vector2 WorldPanMaxXZ => WorldPanBounds().max;

        private (Vector2 min, Vector2 max) WorldPanBounds()
        {
            if (modelRoot == null)
                return (panBoundsMinXZ, panBoundsMaxXZ);
            Vector3 a = modelRoot.TransformPoint(new Vector3(panBoundsMinXZ.x, 0f, panBoundsMinXZ.y));
            Vector3 b = modelRoot.TransformPoint(new Vector3(panBoundsMaxXZ.x, 0f, panBoundsMaxXZ.y));
            return (new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z)),
                    new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
        }

        // Persistent start values captured when drag begins
        private float _startYaw;
        private float _startPitch;
        private float _startRadius;

        // The pose this camera was authored with in the scene. Dragging and focus
        // transitions both move the transform at runtime, so the designed framing is
        // remembered once, before anything can touch it. AuthoredArc uses it as the
        // destination; DirectApproach uses its distance/pitch while keeping the incoming side.
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
            ScaleClipPlanesToWorld();
            ValidatePanBounds();
            ValidateOrbitFrustum();
        }

        // 模型里的裁剪面是资产单位（米）；City 实例整体缩放 0.1，世界里的近远面也要跟着缩。
        // Cinemachine 的 Lens 是世界单位，导入器拿不到场景缩放，所以在这里做一次。
        private void ScaleClipPlanesToWorld()
        {
            if (modelRoot == null)
                return;
            float s = modelRoot.lossyScale.x;
            var vcam = GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (vcam == null || Mathf.Approximately(s, 1f))
                return;
            vcam.m_Lens.NearClipPlane *= s;
            vcam.m_Lens.FarClipPlane *= s;
        }

        private void ValidatePanBounds()
        {
            if (!usePanBounds)
                return;

            if (dragMode != CameraDragMode.Pan)
            {
                string message =
                    $"Camera '{name}' enables Pan Bounds but uses drag mode '{dragMode}'. " +
                    "Pan Bounds only apply to Pan cameras.";
                Debug.LogError(message, this);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new System.InvalidOperationException(message);
            }

            if (panBoundsMaxXZ.x > panBoundsMinXZ.x && panBoundsMaxXZ.y > panBoundsMinXZ.y)
                return;

            string invalidBoundsMessage =
                $"Camera '{name}' has invalid Pan Bounds: min={panBoundsMinXZ}, max={panBoundsMaxXZ}. " +
                "Each maximum must be greater than its corresponding minimum.";
            Debug.LogError(invalidBoundsMessage, this);
            UnityEngine.Assertions.Assert.IsTrue(false, invalidBoundsMessage);
            throw new System.InvalidOperationException(invalidBoundsMessage);
        }

        // 城市地面。Pan 相机全都俯视同一片平地，所以这里不做射线检测，也不加一个
        // 需要每台相机各填一遍的字段。以后真有不在 0 面上的地点，再谈那个字段。
        private const float GroundY = 0f;

        // 画的时候把框抬离地面一点：贴着 y=0 就和地面网格共面，Scene 视图的 Gizmo 带深度
        // 测试，共面必然 z-fighting——边和角会随视角忽隐忽现（右边两个角先没的就是这个）。
        // 抬高只影响这条辅助线的画法，不参与任何计算。
        private const float GroundDrawLift = 0.15f;

        private void OnDrawGizmosSelected()
        {
            if (dragMode != CameraDragMode.Pan || !usePanBounds
                || panBoundsMaxXZ.x <= panBoundsMinXZ.x
                || panBoundsMaxXZ.y <= panBoundsMinXZ.y)
                return;

            float y = transform.position.y;
            var (bmin, bmax) = WorldPanBounds();
            Vector3[] rig =
            {
                new Vector3(bmin.x, y, bmin.y),
                new Vector3(bmax.x, y, bmin.y),
                new Vector3(bmax.x, y, bmax.y),
                new Vector3(bmin.x, y, bmax.y),
            };

            // Pan 只改 x/z，不改朝向也不改高度。场景里只读两样东西：
            // 灰色虚线 = 机身能站的范围（字段的字面意思）；金色空心线 = 画面能到的地面
            // （机身在框的四角各站一次，frustum 落地四边形的外包络）。形式不同，不会糊。
            bool looksDown = transform.forward.y < -0.01f && y > GroundY;

#if UNITY_EDITOR
            // 城市是实心的，辅助框却是用来看的：关掉深度测试，让它永远浮在最上面。
            // 否则一栋楼、一段码头就能把半个框吃掉，而那半个框正是你要对的地方。
            var savedZTest = UnityEditor.Handles.zTest;
            UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 机位范围：虚线，冷灰。它是这两个字段的字面意思，authoring 时要看得见，
            // 但它不该和地面那块抢注意力。
            var rigColor = new Color(0.62f, 0.68f, 0.80f, 0.85f);
            UnityEditor.Handles.color = rigColor;
            for (int i = 0; i < 4; i++)
                UnityEditor.Handles.DrawDottedLine(rig[i], rig[(i + 1) % 4], 4f);
            DrawGizmoLabel(Center(rig), "机位范围", rigColor);

            if (!looksDown)
            {
                UnityEditor.Handles.zTest = savedZTest;
                return;
            }

            // 画面范围：金色空心线。单个机位的 frustum 白线只代表当前位置，
            // 包络才代表整个拖动范围。
            DrawFrustumReachEnvelope(rig);

            UnityEditor.Handles.zTest = savedZTest;
#endif
        }

        private static Vector3 Center(Vector3[] corners)
            => (corners[0] + corners[2]) * 0.5f;

#if UNITY_EDITOR
        // 机身四角 × frustum 四角 = 16 条射线打地面，外包络即画面范围。
        // 金色空心线，不填面；灰色虚线是机位框，形式不同不会糊。
        private void DrawFrustumReachEnvelope(Vector3[] rig)
        {
            var vcam = GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (vcam == null || vcam.m_Lens.Orthographic)
                return;
            float tanV = Mathf.Tan(vcam.m_Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * vcam.m_Lens.Aspect;
            Vector3 fwd = transform.forward;
            Vector3[] cornerDir =
            {
                fwd - transform.right * tanH - transform.up * tanV,
                fwd + transform.right * tanH - transform.up * tanV,
                fwd + transform.right * tanH + transform.up * tanV,
                fwd - transform.right * tanH + transform.up * tanV,
            };
            float planeY = GroundY + GroundDrawLift;
            var hits = new System.Collections.Generic.List<Vector3>();
            foreach (var c in rig)
            foreach (var d in cornerDir)
            {
                if (d.y >= -0.001f)
                    continue;
                float s = (planeY - c.y) / d.y;
                if (s > 0f)
                    hits.Add(c + d * s);
            }
            var hull = ConvexHullXZ(hits);
            if (hull.Count < 3)
                return;
            var gold = new Color(0.95f, 0.72f, 0.2f, 0.95f);
            UnityEditor.Handles.color = gold;
            for (int i = 0; i < hull.Count; i++)
                UnityEditor.Handles.DrawLine(hull[i], hull[(i + 1) % hull.Count]);
            Vector3 centroid = Vector3.zero;
            foreach (var p in hull) centroid += p;
            DrawGizmoLabel(centroid / hull.Count, "画面范围", gold);
        }

        private static System.Collections.Generic.List<Vector3> ConvexHullXZ(
            System.Collections.Generic.List<Vector3> pts)
        {
            var hull = new System.Collections.Generic.List<Vector3>();
            if (pts.Count < 3)
                return hull;
            Vector3 pivot = pts[0];
            foreach (var p in pts)
                if (p.z < pivot.z || (p.z == pivot.z && p.x < pivot.x))
                    pivot = p;
            var sorted = new System.Collections.Generic.List<Vector3>(pts);
            sorted.Sort((a, b) =>
            {
                float aa = Mathf.Atan2(a.z - pivot.z, a.x - pivot.x);
                float ab = Mathf.Atan2(b.z - pivot.z, b.x - pivot.x);
                int c = aa.CompareTo(ab);
                if (c != 0) return c;
                return Vector3.SqrMagnitude(a - pivot).CompareTo(Vector3.SqrMagnitude(b - pivot));
            });
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && CrossXZ(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            return hull;
        }

        private static float CrossXZ(Vector3 a, Vector3 b, Vector3 c)
            => (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
#endif

#if UNITY_EDITOR
        // 标签压在框中央，不挤在同一个角上；带一个暗底，免得落在城市线稿上读不出来。
        private static void DrawGizmoLabel(Vector3 position, string text, Color color)
        {
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color, background = Texture2D.grayTexture },
                padding = new RectOffset(6, 6, 2, 2),
            };
            UnityEditor.Handles.Label(position, text, style);
        }
#endif

        private void ValidateOrbitFrustum()
        {
            if (dragMode != CameraDragMode.Orbit || orbitPivot == null)
                return;

            var vcam = GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (vcam == null)
                return;

            float subjectDistance = Vector3.Distance(
                transform.position, orbitPivot.position);
            bool reachesSubject = vcam.m_Lens.FarClipPlane > subjectDistance;
            if (reachesSubject)
                return;

            string message =
                $"Orbit camera '{name}' cannot see pivot '{orbitPivot.name}': " +
                $"FarClip={vcam.m_Lens.FarClipPlane:F3}, distance={subjectDistance:F3}. " +
                "The model importer or authored camera clip range is invalid.";
            Debug.LogError(message, this);
            UnityEngine.Assertions.Assert.IsTrue(false, message);
            throw new System.InvalidOperationException(message);
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
#if UNITY_EDITOR
            Debug.Log($"[SSNoir] Aligned camera '{name}' to look at pivot '{pivot.name}' at position {pivot.position}.");
#endif
        }

        [ContextMenu("Align Pivot to Camera")]
        public void AlignPivotToCamera()
        {
            Transform? pivot = orbitPivot;
            if (pivot == null)
            {
                Debug.LogWarning($"[SSNoir] AlignPivotToCamera failed on '{name}': Orbit Pivot is not configured.");
                return;
            }

            // 沿当前镜头的视线移动中心点，同时保持已有的轨道半径。这样不会碰相机的
            // 构图，却会让它下一次 Orbit 时围绕它现在正在看的中心转。
            float radius = Vector3.Distance(transform.position, pivot.position);
            if (radius <= Mathf.Epsilon)
            {
                Debug.LogWarning($"[SSNoir] AlignPivotToCamera failed on '{name}': camera and Orbit Pivot overlap, so the orbit radius is zero.");
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(pivot, "Align Orbit Pivot to Camera");
#endif

            pivot.position = transform.position + transform.forward * radius;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(pivot);
#endif
#if UNITY_EDITOR
            Debug.Log($"[SSNoir] Aligned pivot '{pivot.name}' to camera '{name}' at position {pivot.position}.");
#endif
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
            var config = (SSNoirVirtualCameraConfig)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("dragMode"));
            if (config.dragMode == CameraDragMode.Orbit)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Orbit Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("orbitPivot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("orbitSpeedX"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("orbitSpeedY"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minPitch"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPitch"));
            }
            else if (config.dragMode == CameraDragMode.Pan)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Pan Bounds", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("usePanBounds"));
                if (config.usePanBounds)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("panBoundsMinXZ"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("panBoundsMaxXZ"));
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (config.dragMode != CameraDragMode.Orbit)
                return;

            GUILayout.Space(10);
            if (GUILayout.Button("Align Camera to Pivot (自动对齐中心点)"))
            {
                config.AlignToPivot();
            }

            if (GUILayout.Button("Align Orbit Pivot to Camera (按当前构图移动中心点)"))
            {
                config.AlignPivotToCamera();
            }
        }
    }
}
#endif
