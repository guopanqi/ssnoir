#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;

namespace SSNoir
{
    public class SelectedResource
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int SourceIndex { get; set; } = -1;
    }

    public class RollResult
    {
        public string ActionName { get; set; } = string.Empty;
        public int ChosenDie { get; set; }
        public List<int> RandomDice { get; set; } = new List<int>();
        public int FinalValue { get; set; }
        public string Outcome { get; set; } = string.Empty;
    }

    public class SSNoirGameManager : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [SerializeField] private Cinemachine.CinemachineVirtualCamera? globalCamera;
        [SerializeField] private Cinemachine.CinemachineVirtualCamera? focusCamera;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset? fontAsset;

        private GameState _gameState = null!;
        private SceneManager _sceneManager = null!;
        private UnityScriptLoader _scriptLoader = null!;
        private SceneDirectory? _sceneDirectory;

        // UI Root Canvas and elements
        private Canvas? _canvas;
        private GameObject? _nodesContainer;
        private GameObject? _bottomPanel;
        private GameObject? _overlayContainer;

        // Core Gameplay / Interaction State
        private string _focusedNodeName = string.Empty;
        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();
        private SelectedResource? _selectedResource;
        private RollResult? _activeRollResult;
        private string _notification = string.Empty;
        private float _notificationTimer = 0f;

        private readonly Dictionary<string, List<SlottedResource?>> _nodeSlots = new Dictionary<string, List<SlottedResource?>>();
        private readonly List<NodeUIWidget> _spawnedWidgets = new List<NodeUIWidget>();
        private readonly HashSet<string> _flippedNodes = new HashSet<string>();
        private GameObject? _cursorFollower;

        [Header("Camera Drag Settings")]
        [SerializeField] private float panSpeed = 0.02f;
        private bool _isDraggingCam = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        public GameState GameState => _gameState;
        public SceneManager SceneManager => _sceneManager;
        public string FocusedNodeName => _focusedNodeName;

        private void Start()
        {
            // 0. Set global font
            if (fontAsset != null)
            {
                UIHelper.DefaultFont = fontAsset;
            }

            // 1. Initialize Game State
            _gameState = new GameState();

            // 2. Initialize Unity specific script loader
            _scriptLoader = new UnityScriptLoader();

            // 3. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);

            // 4. Find scene cameras if not manually assigned
            if (globalCamera == null)
            {
                globalCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Global") || c.name.Contains("global"));
            }
            if (focusCamera == null)
            {
                focusCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Focus") || c.name.Contains("focus"));
            }

            // 5. Find scene directory
            _sceneDirectory = FindObjectOfType<SceneDirectory>();
            if (_sceneDirectory == null)
            {
                Debug.LogWarning("[SSNoir] SceneDirectory not found in scene. Creating a dummy SceneDirectory.");
                var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
                _sceneDirectory = sdGo.GetComponent<SceneDirectory>();
            }

            // 6. Initialize UI Canvas
            InitCanvas();

            // 7. Listen to scene loads and world refreshes
            _sceneManager.OnSceneLoaded += () => {
                Debug.Log($"[SSNoir] Scene Loaded: {_sceneManager.CurrentSceneName}");
                _navigationStack.Clear();
                _selectedResource = null;
                _focusedNodeName = string.Empty;
                if (focusCamera != null) focusCamera.Priority = 5;
            };

            _sceneManager.OnWorldRefreshed += () => {
                Debug.Log($"[SSNoir] World Refreshed! Current nodes count: {_sceneManager.CurrentWorldNodes.Count}");
                ResolveNavigationStack();
                CleanupNodeSlots();
                RebuildAllUI();
            };

            // 8. Load starting location from gameState
            string startingLocation = _gameState.Get<string>("location", "home");
            _sceneManager.LoadScene(startingLocation);

            Debug.Log("[SSNoir] Core Engine initialized successfully in UnityClient.");
        }

        private void Update()
        {
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f)
                {
                    _notification = string.Empty;
                    BuildOverlay();
                }
            }

            // Update Cursor Follower position and content
            if (_cursorFollower != null)
            {
                if (_selectedResource != null)
                {
                    if (!_cursorFollower.activeSelf)
                    {
                        _cursorFollower.SetActive(true);
                    }

                    var txt = _cursorFollower.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                    {
                        txt.text = _selectedResource.Type == "die" 
                            ? "D" + _selectedResource.Value 
                            : _selectedResource.ItemName;
                    }

                    var rt = _cursorFollower.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(_selectedResource.Type == "die" ? 50f : 100f, 26f);
                    rt.position = Input.mousePosition + new Vector3(15f, -15f, 0f);
                }
                else
                {
                    if (_cursorFollower.activeSelf)
                    {
                        _cursorFollower.SetActive(false);
                    }
                }
            }

            // Map Drag Panning Logic
            if (string.IsNullOrEmpty(_focusedNodeName) && globalCamera != null)
            {
                bool isMouseOverUI = UnityEngine.EventSystems.EventSystem.current != null && 
                                     UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

                if (Input.GetMouseButtonDown(0) && !isMouseOverUI)
                {
                    _isDraggingCam = true;
                    _dragStartMousePos = Input.mousePosition;
                    _dragStartCamPos = globalCamera.transform.position;
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    _isDraggingCam = true;
                    _dragStartMousePos = Input.mousePosition;
                    _dragStartCamPos = globalCamera.transform.position;
                }

                if (_isDraggingCam)
                {
                    if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                    {
                        Vector3 mouseDelta = Input.mousePosition - _dragStartMousePos;
                        Vector3 right = globalCamera.transform.right;
                        Vector3 up = globalCamera.transform.up;
                        Vector3 panTranslation = -mouseDelta.x * right * panSpeed - mouseDelta.y * up * panSpeed;
                        globalCamera.transform.position = _dragStartCamPos + panTranslation;
                    }
                    else
                    {
                        _isDraggingCam = false;
                    }
                }
            }
            else
            {
                _isDraggingCam = false;
            }
        }

        private void InitCanvas()
        {
            var canvasGo = new GameObject("SSNoirCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            // Nodes Container (stretch)
            _nodesContainer = new GameObject("NodesContainer", typeof(RectTransform));
            _nodesContainer.transform.SetParent(_canvas.transform, false);
            var nodesRt = _nodesContainer.GetComponent<RectTransform>();
            nodesRt.anchorMin = Vector2.zero;
            nodesRt.anchorMax = Vector2.one;
            nodesRt.sizeDelta = Vector2.zero;

            // Bottom Panel Container (bottom stretch)
            _bottomPanel = new GameObject("BottomPanel", typeof(RectTransform));
            _bottomPanel.transform.SetParent(_canvas.transform, false);
            var bottomRt = _bottomPanel.GetComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0f, 0f);
            bottomRt.anchorMax = new Vector2(1f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.anchoredPosition = new Vector2(0f, 0f);
            bottomRt.sizeDelta = new Vector2(0f, 160f); // Height 160px

            // Overlay Container (stretch)
            _overlayContainer = new GameObject("OverlayContainer", typeof(RectTransform));
            _overlayContainer.transform.SetParent(_canvas.transform, false);
            var overlayRt = _overlayContainer.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.sizeDelta = Vector2.zero;

            // EventSystem
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            // Cursor Follower
            _cursorFollower = UIHelper.CreatePanel(_canvas.transform, "CursorFollower", new Color(0.9f, 0.75f, 0.2f, 0.9f), new Vector2(90, 26));
            var followerRt = _cursorFollower.GetComponent<RectTransform>();
            followerRt.pivot = new Vector2(0f, 1f);
            var followerTxt = UIHelper.CreateText(_cursorFollower.transform, "", 11, Color.black);
            
            var followerImg = _cursorFollower.GetComponent<Image>();
            if (followerImg != null) followerImg.raycastTarget = false;
            if (followerTxt != null) followerTxt.raycastTarget = false;
            _cursorFollower.SetActive(false);
        }

        public void RebuildAllUI()
        {
            RebuildWidgets();
            BuildBottomPanel();
            BuildOverlay();
        }

        private void RebuildWidgets()
        {
            if (_nodesContainer == null) return;

            // Clear old widgets
            foreach (var widget in _spawnedWidgets)
            {
                if (widget != null) Destroy(widget.gameObject);
            }
            _spawnedWidgets.Clear();

            // Refresh scene directory anchors
            if (_sceneDirectory != null)
            {
                _sceneDirectory.CollectAnchors();
            }

            // Spawn only visible nodes, or if one is focused, only that one
            foreach (var node in _visibleNodes)
            {
                if (!string.IsNullOrEmpty(_focusedNodeName) && node.Name != _focusedNodeName)
                {
                    continue; // Hide unrelated nodes during focus state
                }

                var anchor = _sceneDirectory?.GetAnchor(node.Name);
                if (anchor == null)
                {
                    Debug.LogError($"[SSNoir] Missing NodeAnchor in scene for SCM node: '{node.Name}'! Visual mapping skipped.");
                    continue;
                }

                var widgetGo = new GameObject("NodeWidget_" + node.Name, typeof(RectTransform), typeof(NodeUIWidget));
                widgetGo.transform.SetParent(_nodesContainer.transform, false);

                var widget = widgetGo.GetComponent<NodeUIWidget>();
                widget.Setup(node, anchor, this);
                _spawnedWidgets.Add(widget);
            }
        }

        private void BuildBottomPanel()
        {
            if (_bottomPanel == null) return;

            foreach (Transform child in _bottomPanel.transform)
            {
                Destroy(child.gameObject);
            }

            var bgPanel = UIHelper.CreatePanel(_bottomPanel.transform, "BottomBg", new Color(0.06f, 0.06f, 0.08f, 0.95f));
            var bgRt = bgPanel.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            var layoutGo = new GameObject("BottomLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            layoutGo.transform.SetParent(bgPanel.transform, false);
            var layoutRt = layoutGo.GetComponent<RectTransform>();
            layoutRt.anchorMin = Vector2.zero;
            layoutRt.anchorMax = Vector2.one;
            layoutRt.sizeDelta = Vector2.zero;

            var hlg = layoutGo.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.spacing = 30f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // --- Left Section: Status & Scene Switchers ---
            var leftGo = UIHelper.CreateVerticalLayout(layoutGo.transform, "LeftSection", 6f);
            var leftVlg = leftGo.GetComponent<VerticalLayoutGroup>();
            leftVlg.childAlignment = TextAnchor.MiddleLeft;

            int health = _gameState.Get<int>("health");
            int money = _gameState.Get<int>("money");
            var statusText = UIHelper.CreateText(leftGo.transform, $"生命: {health} | 资金: {money}", 14, Color.white, TextAlignmentOptions.Left);
            statusText.fontStyle = FontStyles.Bold;

            UIHelper.CreateText(leftGo.transform, "场景切换:", 11, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Left);
            
            var sceneButtonsRow = UIHelper.CreateHorizontalLayout(leftGo.transform, "SceneButtons", 6f);
            var sceneButtonsHlg = sceneButtonsRow.GetComponent<HorizontalLayoutGroup>();
            sceneButtonsHlg.childAlignment = TextAnchor.MiddleLeft;

            string[] scenes = { "home", "office", "combat" };
            foreach (var sc in scenes)
            {
                bool isCurrent = _sceneManager.CurrentSceneName == sc;
                Color btnColor = isCurrent ? new Color(0.2f, 0.55f, 0.35f) : new Color(0.25f, 0.25f, 0.3f);
                string label = sc == "home" ? "家" : sc == "office" ? "办公室" : "战斗";
                UIHelper.CreateButton(sceneButtonsRow.transform, label, btnColor, () =>
                {
                    _selectedResource = null;
                    _navigationStack.Clear();
                    _gameState.Set("location", sc);
                }, new Vector2(70, 26));
            }

            // --- Middle Section: Hand Resources ---
            var middleGo = UIHelper.CreateVerticalLayout(layoutGo.transform, "MiddleSection", 6f);
            var middleVlg = middleGo.GetComponent<VerticalLayoutGroup>();
            middleVlg.childAlignment = TextAnchor.MiddleCenter;

            string selectedLabel = _selectedResource != null 
                ? $"已选中: {(_selectedResource.Type == "die" ? "骰子 " + _selectedResource.Value : _selectedResource.ItemName)}" 
                : "选择骰子/道具以投入槽位";
            var selectedText = UIHelper.CreateText(middleGo.transform, selectedLabel, 11, new Color(0.9f, 0.9f, 0.9f));
            selectedText.fontStyle = FontStyles.Italic;

            var cardsRow = UIHelper.CreateHorizontalLayout(middleGo.transform, "CardsRow", 10f);
            
            // Dice
            var diceList = _gameState.Get<List<object>>("action-dice");
            if (diceList != null)
            {
                for (int i = 0; i < diceList.Count; i++)
                {
                    int val = 0;
                    if (diceList[i] is double d) val = (int)d;
                    else if (diceList[i] is long l) val = (int)l;
                    else if (diceList[i] is int valInt) val = valInt;

                    int dieIndex = i;
                    bool isSelected = _selectedResource != null && _selectedResource.Type == "die" && _selectedResource.SourceIndex == dieIndex;
                    
                    Color dieColor = isSelected ? new Color(1f, 0.75f, 0.2f) : new Color(0.18f, 0.35f, 0.55f);
                    UIHelper.CreateButton(cardsRow.transform, $"骰子 {val}", dieColor, () =>
                    {
                        if (isSelected)
                        {
                            _selectedResource = null;
                        }
                        else
                        {
                            _selectedResource = new SelectedResource { Type = "die", Value = val, SourceIndex = dieIndex };
                        }
                        RebuildAllUI();
                    }, new Vector2(60, 45));
                }
            }

            // Items
            foreach (var kvp in _gameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    string itemName = kvp.Key.Substring(5);
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        bool isSelected = _selectedResource != null && _selectedResource.Type == "item" && _selectedResource.ItemName == itemName;
                        Color itemColor = isSelected ? new Color(1f, 0.75f, 0.2f) : new Color(0.15f, 0.45f, 0.3f);
                        
                        UIHelper.CreateButton(cardsRow.transform, $"{itemName} x{qty}", itemColor, () =>
                        {
                            if (isSelected)
                            {
                                _selectedResource = null;
                            }
                            else
                            {
                                _selectedResource = new SelectedResource { Type = "item", ItemName = itemName, Value = 1 };
                            }
                            RebuildAllUI();
                        }, new Vector2(85, 45));
                    }
                }
            }

            // --- Right Section: Actions ---
            var rightGo = UIHelper.CreateVerticalLayout(layoutGo.transform, "RightSection", 8f);
            var rightVlg = rightGo.GetComponent<VerticalLayoutGroup>();
            rightVlg.childAlignment = TextAnchor.MiddleRight;

            if (_navigationStack.Count > 0)
            {
                string path = string.Join(" > ", _navigationStack.Select(n => n.Name));
                UIHelper.CreateText(rightGo.transform, $"层级: {path}", 11, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Right);
                
                UIHelper.CreateButton(rightGo.transform, "返回上一级", new Color(0.2f, 0.2f, 0.25f), () =>
                {
                    GoBackNavigation();
                }, new Vector2(110, 26));
            }

            UIHelper.CreateButton(rightGo.transform, "结束回合", new Color(0.6f, 0.15f, 0.15f), () =>
            {
                _selectedResource = null;
                _sceneManager.EndTurn();
            }, new Vector2(110, 32));

            Canvas.ForceUpdateCanvases();
        }

        private void BuildOverlay()
        {
            if (_overlayContainer == null) return;

            foreach (Transform child in _overlayContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // 1. Toast Notification
            if (!string.IsNullOrEmpty(_notification))
            {
                var toastPanel = UIHelper.CreatePanel(_overlayContainer.transform, "ToastPanel", new Color(0.7f, 0.15f, 0.15f, 0.95f), new Vector2(400, 40));
                var toastRt = toastPanel.GetComponent<RectTransform>();
                toastRt.anchorMin = new Vector2(0.5f, 0.9f);
                toastRt.anchorMax = new Vector2(0.5f, 0.9f);
                toastRt.pivot = new Vector2(0.5f, 0.5f);
                toastRt.anchoredPosition = Vector2.zero;

                UIHelper.CreateText(toastPanel.transform, _notification, 13, Color.white);
            }

            // 2. Roll Result Modal
            if (_activeRollResult != null)
            {
                var blocker = UIHelper.CreatePanel(_overlayContainer.transform, "ModalBlocker", new Color(0f, 0f, 0f, 0.6f));
                var blockerRt = blocker.GetComponent<RectTransform>();
                blockerRt.anchorMin = Vector2.zero;
                blockerRt.anchorMax = Vector2.one;
                blockerRt.sizeDelta = Vector2.zero;

                var modalPanel = UIHelper.CreatePanel(blocker.transform, "ModalPanel", new Color(0.12f, 0.12f, 0.15f, 0.98f), new Vector2(380, 260));
                var modalRt = modalPanel.GetComponent<RectTransform>();
                modalRt.anchorMin = new Vector2(0.5f, 0.5f);
                modalRt.anchorMax = new Vector2(0.5f, 0.5f);
                modalRt.pivot = new Vector2(0.5f, 0.5f);
                modalRt.anchoredPosition = Vector2.zero;

                var vlgGo = new GameObject("ModalLayout", typeof(RectTransform), typeof(VerticalLayoutGroup));
                vlgGo.transform.SetParent(modalPanel.transform, false);
                var vlgRt = vlgGo.GetComponent<RectTransform>();
                vlgRt.anchorMin = Vector2.zero;
                vlgRt.anchorMax = Vector2.one;
                vlgRt.sizeDelta = Vector2.zero;

                var vlg = vlgGo.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(20, 20, 20, 20);
                vlg.spacing = 10f;
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var title = UIHelper.CreateText(vlgGo.transform, $"[判定结果] {_activeRollResult.ActionName}", 15, Color.white);
                title.fontStyle = FontStyles.Bold;

                UIHelper.CreateText(vlgGo.transform, $"投入骰子值: {_activeRollResult.ChosenDie}", 12, new Color(0.8f, 0.8f, 0.8f));
                
                string randDiceText = _activeRollResult.RandomDice.Count > 0 
                    ? "附加掷骰: " + string.Join(", ", _activeRollResult.RandomDice) 
                    : "无附加掷骰 (技能等级为1)";
                UIHelper.CreateText(vlgGo.transform, randDiceText, 12, new Color(0.8f, 0.8f, 0.8f));

                UIHelper.CreateText(vlgGo.transform, $"最终最大点数: {_activeRollResult.FinalValue}", 14, Color.white);

                Color outcomeColor = _activeRollResult.Outcome == "成功" ? new Color(0.2f, 0.8f, 0.4f) 
                                   : _activeRollResult.Outcome == "中性" ? new Color(0.9f, 0.8f, 0.2f) 
                                   : new Color(0.9f, 0.2f, 0.2f);
                var outcomeText = UIHelper.CreateText(vlgGo.transform, $"判定结果: {_activeRollResult.Outcome}", 15, outcomeColor);
                outcomeText.fontStyle = FontStyles.Bold;

                UIHelper.CreateButton(vlgGo.transform, "确定", new Color(0.2f, 0.4f, 0.6f), () =>
                {
                    _activeRollResult = null;
                    RebuildAllUI();
                }, new Vector2(100, 30));
            }
        }

        public bool IsNodeFlipped(string nodeName) => _flippedNodes.Contains(nodeName);

        public void ToggleNodeFlipped(string nodeName)
        {
            if (_flippedNodes.Contains(nodeName))
            {
                _flippedNodes.Remove(nodeName);
            }
            else
            {
                _flippedNodes.Add(nodeName);
            }
            RebuildAllUI();
        }

        public void OnNodeCardClicked(GameNode node)
        {
            if (node.HasChildren)
            {
                _nodeSlots.Clear();
                _navigationStack.Add(node);
                ResolveNavigationStack();
                _selectedResource = null;
                SetFocusedNode(null);
            }
            else if (node.Resolve != null && node.Resolve.Type == ResolveType.Observe && (node.Requires == null || node.Requires.Count == 0))
            {
                ToggleNodeFlipped(node.Name);
            }
            else if (node.Requires == null || node.Requires.Count == 0)
            {
                // Action with no requirements: execute instantly!
                ExecuteNodeAction(node);
            }
            else
            {
                SetFocusedNode(node.Name);
            }
        }

        public void SetFocusedNode(string? nodeName)
        {
            _focusedNodeName = nodeName ?? string.Empty;

            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                ClearOtherNodeSlots(_focusedNodeName);
            }

            // Camera transition
            bool cameraApplied = false;
            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                var anchor = _sceneDirectory?.GetAnchor(_focusedNodeName);
                if (anchor != null && anchor.FocusCameraTransform != null && focusCamera != null)
                {
                    focusCamera.transform.position = anchor.FocusCameraTransform.position;
                    focusCamera.transform.rotation = anchor.FocusCameraTransform.rotation;
                    focusCamera.Priority = 20;
                    cameraApplied = true;
                }
            }

            if (!cameraApplied)
            {
                if (_navigationStack.Count > 0 && focusCamera != null)
                {
                    var parentNode = _navigationStack[_navigationStack.Count - 1];
                    var anchor = _sceneDirectory?.GetAnchor(parentNode.Name);
                    if (anchor != null && anchor.FocusCameraTransform != null)
                    {
                        focusCamera.transform.position = anchor.FocusCameraTransform.position;
                        focusCamera.transform.rotation = anchor.FocusCameraTransform.rotation;
                        focusCamera.Priority = 20;
                    }
                    else
                    {
                        focusCamera.Priority = 5;
                    }
                }
                else if (focusCamera != null)
                {
                    focusCamera.Priority = 5;
                }
            }

            RebuildAllUI();
        }

        public List<SlottedResource?>? GetSlotsForNode(string nodeName)
        {
            if (!_nodeSlots.TryGetValue(nodeName, out var list))
            {
                var node = FindNodeByName(nodeName);
                if (node != null && node.Requires != null)
                {
                    list = new List<SlottedResource?>();
                    for (int i = 0; i < node.Requires.Count; i++)
                    {
                        list.Add(null);
                    }
                    _nodeSlots[nodeName] = list;
                }
            }
            return list;
        }

        public void OnSlotClicked(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;

            var existing = slots[slotIndex];
            if (existing != null)
            {
                // Clear slot (return to hand)
                slots[slotIndex] = null;
            }
            else if (_selectedResource != null)
            {
                ClearOtherNodeSlots(node.Name);

                var req = node.Requires[slotIndex];
                if (req.Type == "die" && _selectedResource.Type == "die")
                {
                    ClearDieFromAllSlots(_selectedResource.SourceIndex);
                    
                    slots[slotIndex] = new SlottedResource
                    {
                        Type = "die",
                        Value = _selectedResource.Value,
                        SourceIndex = _selectedResource.SourceIndex
                    };
                    _selectedResource = null;
                }
                else if (req.Type == "item" && _selectedResource.Type == "item" && req.ItemName == _selectedResource.ItemName)
                {
                    int totalOwned = (req.ItemName == "金钱") ? _gameState.Get<int>("money") : _gameState.Get<int>("item:" + req.ItemName, 0);
                    int totalSlotted = GetTotalSlottedItemQty(req.ItemName);
                    int available = totalOwned - totalSlotted;

                    if (available >= req.Qty)
                    {
                        slots[slotIndex] = new SlottedResource
                        {
                            Type = "item",
                            ItemName = req.ItemName,
                            Value = req.Qty
                        };
                        _selectedResource = null;
                    }
                    else
                    {
                        ShowNotification($"缺少数量，需要 {req.Qty} 个 {req.ItemName}");
                    }
                }
                else
                {
                    ShowNotification($"槽位需要: {(req.Type == "die" ? "骰子" : req.ItemName)}");
                }
            }
            
            RebuildAllUI();
        }

        private void ClearOtherNodeSlots(string activeNodeName)
        {
            foreach (var pair in _nodeSlots)
            {
                if (pair.Key != activeNodeName)
                {
                    var slots = pair.Value;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        slots[i] = null;
                    }
                }
            }
        }

        private void ClearDieFromAllSlots(int dieIndex)
        {
            foreach (var list in _nodeSlots.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var slot = list[i];
                    if (slot != null && slot.Type == "die" && slot.SourceIndex == dieIndex)
                    {
                        list[i] = null;
                    }
                }
            }
        }

        private int GetTotalSlottedItemQty(string itemName)
        {
            int sum = 0;
            foreach (var list in _nodeSlots.Values)
            {
                foreach (var s in list)
                {
                    if (s != null && s.Type == "item" && s.ItemName == itemName)
                    {
                        sum += s.Value;
                    }
                }
            }
            return sum;
        }

        public void ExecuteNodeAction(GameNode node)
        {
            var slots = GetSlotsForNode(node.Name);
            if (slots == null) return;

            // 1. Consume resources
            var diceToConsume = new List<int>();
            foreach (var s in slots)
            {
                if (s != null && s.Type == "die")
                {
                    diceToConsume.Add(s.SourceIndex);
                }
            }
            diceToConsume.Sort((a, b) => b.CompareTo(a));

            var diceList = _gameState.Get<List<object>>("action-dice");
            if (diceList != null)
            {
                var newDice = new List<object>(diceList);
                foreach (var idx in diceToConsume)
                {
                    if (idx >= 0 && idx < newDice.Count)
                    {
                        newDice.RemoveAt(idx);
                    }
                }
                _gameState.Set("action-dice", newDice);
            }

            foreach (var s in slots)
            {
                if (s != null && s.Type == "item")
                {
                    if (s.ItemName == "金钱")
                    {
                        int owned = _gameState.Get<int>("money");
                        _gameState.Set("money", System.Math.Max(0, owned - s.Value));
                    }
                    else
                    {
                        int owned = _gameState.Get<int>("item:" + s.ItemName, 0);
                        _gameState.Set("item:" + s.ItemName, System.Math.Max(0, owned - s.Value));
                    }
                }
            }

            // 2. Clear slots
            _nodeSlots.Remove(node.Name);

            // 3. Resolve Effect
            if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    node.Resolve.Effect?.Invoke();
                    _sceneManager.OnActionExecuted();
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    var dieSlot = slots.Find(s => s != null && s.Type == "die");
                    int chosenDieVal = dieSlot != null ? dieSlot.Value : 1;

                    int skillLevel = _gameState.Get<int>("skill:" + node.Resolve.SkillName, 1);
                    var rand = new System.Random();
                    var randomDice = new List<int>();
                    int finalValue = chosenDieVal;

                    for (int i = 0; i < skillLevel - 1; i++)
                    {
                        int r = rand.Next(1, 7);
                        randomDice.Add(r);
                        if (r > finalValue)
                        {
                            finalValue = r;
                        }
                    }

                    string outcome = "";
                    if (finalValue <= 2)
                    {
                        outcome = "失败";
                        node.Resolve.OnFail?.Invoke();
                    }
                    else if (finalValue <= 4)
                    {
                        outcome = "中性";
                        node.Resolve.OnNeutral?.Invoke();
                    }
                    else
                    {
                        outcome = "成功";
                        node.Resolve.OnSuccess?.Invoke();
                    }

                    _activeRollResult = new RollResult
                    {
                        ActionName = node.Name,
                        ChosenDie = chosenDieVal,
                        RandomDice = randomDice,
                        FinalValue = finalValue,
                        Outcome = outcome
                    };
                    
                    _sceneManager.OnActionExecuted();
                }
                else if (node.Resolve.Type == ResolveType.Observe)
                {
                    ToggleNodeFlipped(node.Name);
                    _sceneManager.OnActionExecuted();
                }
            }

            SetFocusedNode(null);
            RebuildAllUI();
        }

        public void GoBackNavigation()
        {
            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                SetFocusedNode(null);
            }
            else if (_navigationStack.Count > 0)
            {
                _nodeSlots.Clear();
                _navigationStack.RemoveAt(_navigationStack.Count - 1);
                ResolveNavigationStack();
                SetFocusedNode(null);
                RebuildAllUI();
            }
        }

        private void ResolveNavigationStack()
        {
            if (_navigationStack.Count == 0)
            {
                _visibleNodes = _sceneManager.CurrentWorldNodes;
                return;
            }

            var path = new List<string>();
            foreach (var node in _navigationStack)
            {
                path.Add(node.Name);
            }

            _navigationStack.Clear();
            var currentLevel = _sceneManager.CurrentWorldNodes;

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.HasChildren)
                {
                    _navigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    _navigationStack.Clear();
                    _visibleNodes = _sceneManager.CurrentWorldNodes;
                    return;
                }
            }

            _visibleNodes = currentLevel;
        }

        private void CleanupNodeSlots()
        {
            var currentNames = new HashSet<string>();
            CollectAllNodeNamesRecursive(_sceneManager.CurrentWorldNodes, currentNames);

            var keysToRemove = new List<string>();
            foreach (var name in _nodeSlots.Keys)
            {
                if (!currentNames.Contains(name))
                {
                    keysToRemove.Add(name);
                }
            }
            foreach (var key in keysToRemove)
            {
                _nodeSlots.Remove(key);
            }
        }

        private void CollectAllNodeNamesRecursive(List<GameNode> nodes, HashSet<string> result)
        {
            foreach (var node in nodes)
            {
                result.Add(node.Name);
                CollectAllNodeNamesRecursive(node.Children, result);
            }
        }

        private GameNode? FindNodeByName(string name)
        {
            return FindNodeRecursive(_sceneManager.CurrentWorldNodes, name);
        }

        private GameNode? FindNodeRecursive(List<GameNode> nodes, string name)
        {
            foreach (var node in nodes)
            {
                if (node.Name == name) return node;
                var child = FindNodeRecursive(node.Children, name);
                if (child != null) return child;
            }
            return null;
        }

        public void ShowNotification(string message)
        {
            _notification = message;
            _notificationTimer = 3.0f; // Show toast for 3 seconds
            BuildOverlay();
        }
    }
}
