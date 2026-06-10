#nullable enable
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

        [Header("Camera Drag Settings")]
        [SerializeField] private float panSpeed = 0.02f;

        private GameState _gameState = null!;
        private SceneManager _sceneManager = null!;
        private UnityScriptLoader _scriptLoader = null!;
        private SceneDirectory? _sceneDirectory;
        private UIManager _uiManager = null!;

        // Core Gameplay / Interaction State
        private string _focusedNodeName = string.Empty;
        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();
        private SelectedResource? _selectedResource;
        private RollResult? _activeRollResult;
        private string _notification = string.Empty;
        private float _notificationTimer = 0f;

        private bool _isDraggingCam = false;
        private bool _isActionRoutineRunning = false;
        private bool _hasDeferredWorldRefresh = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        private readonly Dictionary<string, List<SlottedResource?>> _nodeSlots = new Dictionary<string, List<SlottedResource?>>();
        private readonly HashSet<string> _flippedNodes = new HashSet<string>();

        // Public properties accessed by UIManager
        public GameState GameState => _gameState;
        public SceneManager SceneManager => _sceneManager;
        public SceneDirectory? SceneDirectory => _sceneDirectory;
        public string FocusedNodeName => _focusedNodeName;
        public List<GameNode> NavigationStack => _navigationStack;
        public List<GameNode> VisibleNodes => _visibleNodes;
        public SelectedResource? SelectedResource => _selectedResource;
        public RollResult? ActiveRollResult => _activeRollResult;
        public string Notification => _notification;

        private void Start()
        {
            // 0. Set global font
            if (fontAsset != null)
            {
                UIHelper.DefaultFont = fontAsset;
            }

            // 1. Initialize Game State & Script Loader
            _gameState = new GameState();
            _scriptLoader = new UnityScriptLoader();

            // 2. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);

            // 3. Find scene cameras
            if (globalCamera == null)
            {
                globalCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Global") || c.name.Contains("global"));
            }
            if (focusCamera == null)
            {
                focusCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Focus") || c.name.Contains("focus"));
            }

            // 4. Find scene directory
            _sceneDirectory = FindObjectOfType<SceneDirectory>();
            if (_sceneDirectory == null)
            {
                var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
                _sceneDirectory = sdGo.GetComponent<SceneDirectory>();
            }

            // 5. Spawn UIManager component
            _uiManager = gameObject.AddComponent<UIManager>();
            _uiManager.Initialize(this);

            // 6. Listen to scene loads and world refreshes
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

                if (_isActionRoutineRunning)
                {
                    _hasDeferredWorldRefresh = true;
                    return;
                }

                _uiManager.RefreshWorld(_visibleNodes);
            };

            // 7. Load starting location from gameState
            string startingLocation = _gameState.Get<string>("location", "home");
            _sceneManager.LoadScene(startingLocation);
        }

        private void Update()
        {
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f)
                {
                    _notification = string.Empty;
                    _uiManager.BuildOverlay();
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
            _uiManager.RefreshWorld(_sceneManager.CurrentWorldNodes);
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

            _uiManager.RefreshWorld(_visibleNodes);
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
            
            _uiManager.RefreshWorld(_visibleNodes);
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

        // --- Execute Actions via Async Coroutine ---
        public void ExecuteNodeAction(GameNode node)
        {
            if (_isActionRoutineRunning) return;
            StartCoroutine(ExecuteRoutine(node));
        }

        private IEnumerator ExecuteRoutine(GameNode node)
        {
            var slots = GetSlotsForNode(node.Name);
            if (slots == null) yield break;

            bool shouldClearFocus = false;

            _isActionRoutineRunning = true;
            _hasDeferredWorldRefresh = false;
            _uiManager.SetInputLocked(true);

            try
            {
                // 1. Execute Action on engine. World refresh events are deferred while this routine runs.
                ActionReport report = _sceneManager.ExecuteAction(node, slots);
                Debug.Log($"[SSNoir] Action executed: {node.Name}, reportType={report.Type}, finalRoll={report.FinalRollValue}, outcome={report.Outcome}");

                // Clean local temporary slots state, but do not redraw until the performance finishes.
                _nodeSlots.Remove(node.Name);
                _selectedResource = null;

                // 2. Performance Animation
                if (report.Type == ActionType.Roll)
                {
                    Debug.Log($"[SSNoir] Playing roll animation for action: {node.Name}");
                    yield return StartCoroutine(DiceAnimator.PlayRoll(report.FinalRollValue, report.Outcome, _uiManager.Canvas!));
                }

                // 3. Sync UI. Engine refreshes during execution are intentionally deferred so state
                // changes become visible only after the action performance finishes.
                if (!_hasDeferredWorldRefresh)
                {
                    ResolveNavigationStack();
                    CleanupNodeSlots();
                }
                _uiManager.RefreshWorld(_visibleNodes);
                shouldClearFocus = true;
            }
            finally
            {
                _isActionRoutineRunning = false;
                _hasDeferredWorldRefresh = false;
                _uiManager.SetInputLocked(false);
            }

            if (shouldClearFocus)
            {
                SetFocusedNode(null);
            }
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
            _notificationTimer = 3.0f;
            _uiManager.BuildOverlay();
        }

        // --- UI Callbacks from UIManager ---
        public void OnSceneButtonClicked(string sc)
        {
            _selectedResource = null;
            _navigationStack.Clear();
            _gameState.Set("location", sc);
        }

        public void OnDieClicked(int dieIndex, int val)
        {
            if (_selectedResource != null && _selectedResource.Type == "die" && _selectedResource.SourceIndex == dieIndex)
            {
                _selectedResource = null;
            }
            else
            {
                _selectedResource = new SelectedResource
                {
                    Type = "die",
                    Value = val,
                    SourceIndex = dieIndex
                };
            }
            _uiManager.BuildBottomPanel();
        }

        public void OnItemClicked(string itemName, int qty)
        {
            if (_selectedResource != null && _selectedResource.Type == "item" && _selectedResource.ItemName == itemName)
            {
                _selectedResource = null;
            }
            else
            {
                _selectedResource = new SelectedResource
                {
                    Type = "item",
                    ItemName = itemName,
                    Value = qty
                };
            }
            _uiManager.BuildBottomPanel();
        }

        public void OnEndTurnClicked()
        {
            _selectedResource = null;
            _sceneManager.EndTurn();
        }

        public void OnRollAckClicked()
        {
            _activeRollResult = null;
            _uiManager.BuildOverlay();
        }
    }
}
