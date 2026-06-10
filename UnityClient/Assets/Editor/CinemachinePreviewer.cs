#nullable enable
using UnityEditor;
using UnityEngine;
using Cinemachine;
using SSNoir;

namespace SSNoir.Editor
{
    [InitializeOnLoad]
    public static class CinemachinePreviewer
    {
        private static Camera? _previewCamera;
        private static RenderTexture? _previewTexture;
        private const float PreviewWidth = 240f;
        private const float PreviewHeight = 135f; // 16:9 aspect ratio
        private const float TitleHeight = 22f;
        private const float PanelPadding = 7f;
        private const float PanelWidth = PreviewWidth + PanelPadding * 2f;
        private const float PanelHeight = TitleHeight + PreviewHeight + PanelPadding;

        // Pinning state variables
        private static bool _isPinned;
        private static CinemachineVirtualCamera? _pinnedVcam;
        private static GameObject? _pinnedObj;

        static CinemachinePreviewer()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Determine active target camera and selection source
            CinemachineVirtualCamera? targetVcam = null;
            GameObject? targetObj = null;

            if (_isPinned)
            {
                targetVcam = _pinnedVcam;
                targetObj = _pinnedObj;

                // Handle case where target virtual camera is destroyed while pinned
                if (targetVcam == null)
                {
                    _isPinned = false;
                    _pinnedVcam = null;
                    _pinnedObj = null;
                }
            }

            if (!_isPinned)
            {
                var activeGo = Selection.activeGameObject;
                if (activeGo != null)
                {
                    targetVcam = activeGo.GetComponent<CinemachineVirtualCamera>();
                    targetObj = activeGo;
                    if (targetVcam == null)
                    {
                        var anchor = activeGo.GetComponent<NodeAnchor>();
                        if (anchor != null)
                        {
                            targetVcam = anchor.FocusVirtualCamera;
                            targetObj = activeGo;
                        }
                    }
                }
            }

            if (targetVcam == null)
            {
                Cleanup();
                return;
            }

            // Handle preview camera generation only on Repaint events
            if (Event.current.type == EventType.Repaint)
            {
                EnsureResources();
                if (_previewCamera != null && _previewTexture != null)
                {
                    // Sync preview camera transform and lens properties
                    _previewCamera.transform.position = targetVcam.transform.position;
                    _previewCamera.transform.rotation = targetVcam.transform.rotation;

                    _previewCamera.fieldOfView = targetVcam.m_Lens.FieldOfView;
                    _previewCamera.nearClipPlane = targetVcam.m_Lens.NearClipPlane;
                    _previewCamera.farClipPlane = targetVcam.m_Lens.FarClipPlane;
                    _previewCamera.orthographic = targetVcam.m_Lens.Orthographic;
                    _previewCamera.orthographicSize = targetVcam.m_Lens.OrthographicSize;

                    // Render viewport to texture
                    _previewCamera.targetTexture = _previewTexture;
                    _previewCamera.Render();
                    _previewCamera.targetTexture = null;
                }
            }

            // Draw overlay UI panel
            Handles.BeginGUI();

            // Match Unity's native Camera Preview placement and compact dark frame.
            float panelX = sceneView.position.width - PanelWidth - 8f;
            float panelY = sceneView.position.height - PanelHeight - 32f;
            var panelRect = new Rect(panelX, panelY, PanelWidth, PanelHeight);
            var titleRect = new Rect(panelRect.x, panelRect.y, panelRect.width, TitleHeight);
            var imageRect = new Rect(panelRect.x + PanelPadding, panelRect.y + TitleHeight, PreviewWidth, PreviewHeight);

            EditorGUI.DrawRect(panelRect, new Color(0.015f, 0.015f, 0.015f, 0.94f));
            EditorGUI.DrawRect(titleRect, new Color(0.040f, 0.040f, 0.040f, 0.96f));
            DrawBorder(panelRect, new Color(0.23f, 0.23f, 0.23f, 1f), 1f);

            // Draw preview image texture (rely on pre-rendered texture during repaint)
            if (_previewTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(imageRect, _previewTexture, ScaleMode.StretchToFill, false);
            }
            else
            {
                EditorGUI.DrawRect(imageRect, new Color(0.09f, 0.09f, 0.09f, 1f));
            }

            DrawBorder(imageRect, new Color(0.0f, 0.0f, 0.0f, 1f), 1f);
            EditorGUI.DrawRect(new Rect(imageRect.x, imageRect.yMax + 2f, imageRect.width, 2f), new Color(0.47f, 0.47f, 0.47f, 1f));

            // Draw small action buttons in top-right corner
            float btnW = 30f;
            float btnH = 15f;
            float btnSpacing = 4f;

            var pinRect = new Rect(titleRect.xMax - btnW - 6f, titleRect.y + 4f, btnW, btnH);
            var selRect = new Rect(pinRect.x - btnW - btnSpacing, pinRect.y, btnW, btnH);

            // Handle UI button events before drawing text to avoid overlapping/click-throughs
            if (DrawSmallButton(pinRect, _isPinned ? "PIN" : "PIN", _isPinned))
            {
                _isPinned = !_isPinned;
                if (_isPinned)
                {
                    _pinnedVcam = targetVcam;
                    _pinnedObj = targetObj;
                }
                else
                {
                    _pinnedVcam = null;
                    _pinnedObj = null;
                }
                Event.current.Use();
            }

            if (DrawSmallButton(selRect, "SEL", false))
            {
                if (targetObj != null)
                {
                    Selection.activeGameObject = targetObj;
                    EditorGUIUtility.PingObject(targetObj);
                }
                Event.current.Use();
            }

            // Draw native-like title label (truncated to leave room for buttons).
            var labelRect = new Rect(titleRect.x + 9f, titleRect.y + 2f, titleRect.width - (btnW * 2f) - btnSpacing - 26f, 17f);
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            GUI.Label(labelRect, targetVcam.name, labelStyle);

            Handles.EndGUI();
        }

        private static bool DrawSmallButton(Rect rect, string text, bool active)
        {
            var style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                margin = new RectOffset(),
                padding = new RectOffset(3, 3, 0, 1),
                fixedHeight = 0f
            };

            if (!active)
            {
                return GUI.Button(rect, text, style);
            }

            return GUI.Toggle(rect, true, text, style) == false;
        }

        private static void EnsureResources()
        {
            if (_previewTexture == null)
            {
                _previewTexture = new RenderTexture((int)PreviewWidth, (int)PreviewHeight, 24, RenderTextureFormat.ARGB32);
                _previewTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_previewCamera == null)
            {
                var go = new GameObject("~CinemachinePreviewCamera", typeof(Camera));
                go.hideFlags = HideFlags.HideAndDontSave;
                _previewCamera = go.GetComponent<Camera>();
                _previewCamera.enabled = false; // Disable auto-update to render manually
                _previewCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private static void Cleanup()
        {
            if (_previewCamera != null)
            {
                Object.DestroyImmediate(_previewCamera.gameObject);
                _previewCamera = null;
            }

            if (_previewTexture != null)
            {
                Object.DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
