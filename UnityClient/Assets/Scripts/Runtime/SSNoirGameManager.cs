#nullable enable
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.IMGUI;

namespace SSNoir
{
    public class SelectedResource
    {
        public string Type { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int SourceIndex { get; set; } = -1;
        public string ActorId { get; set; } = string.Empty;
        public int DieIndex { get; set; } = -1;
    }

    internal enum ResourceDragOriginKind
    {
        None,
        Hand,
        Slot
    }

    public class SSNoirGameManager : MonoBehaviour
    {
        private const string WorldRootNodeName = "世界";
        // Font files follow: <family>-Regular.ttf / <family>-SemiBold.ttf.
        private const string FontFamily = "SourceHanSerifCN";
        private const string RegularFontResourcePath = "Fonts/" + FontFamily + "-Regular";
        private const string SemiboldFontResourcePath = "Fonts/" + FontFamily + "-SemiBold";
#if UNITY_EDITOR
        private const string RegularFontAssetPath = "Assets/Resources/Fonts/" + FontFamily + "-Regular.ttf";
        private const string SemiboldFontAssetPath = "Assets/Resources/Fonts/" + FontFamily + "-SemiBold.ttf";
#endif

        [Header("Camera Drag Settings")]
        [SerializeField] private float panSpeed = 0.02f;

        private GameState _gameState = null!;
        private SceneManager _sceneManager = null!;
        private UnityScriptLoader _scriptLoader = null!;
        private SceneDirectory? _sceneDirectory;
        private IMGUIWorldRenderer _renderer = null!;
        private StageTransitionController _stageController = null!;

        private SSNoirCameraManager _cameraManager = null!;
        private Font? _regularFont;
        private Font? _semiboldFont;

        // Core Gameplay / Interaction State
        private string _focusedNodeName = string.Empty;
        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();
        private SelectedResource? _selectedResource;

        private bool _resourceDragActive;
        private bool _resourceDropHandled;
        private ResourceDragOriginKind _resourceDragOrigin = ResourceDragOriginKind.None;
        private string _resourceOriginNodeName = string.Empty;
        private int _resourceOriginSlotIndex = -1;
        private SlottedResource? _resourceOriginSlotResource;

        private readonly Dictionary<string, List<SlottedResource?>> _nodeSlots = new Dictionary<string, List<SlottedResource?>>();
        private readonly HashSet<string> _flippedNodes = new HashSet<string>();
        private PresentationSnapshot _displayedSnapshot = new PresentationSnapshot();

        // Public properties
        public GameState GameState => _gameState;
        public SceneManager SceneManager => _sceneManager;
        public SceneDirectory? SceneDirectory => _sceneDirectory;
        public PresentationSnapshot DisplayedSnapshot => _displayedSnapshot;
        public string FocusedNodeName => _focusedNodeName;
        public List<GameNode> NavigationStack => _navigationStack;
        public List<GameNode> VisibleNodes => _visibleNodes;
        public SelectedResource? SelectedResource => _selectedResource;
        public Font? ChineseFont => _regularFont;
        public Font? SemiboldFont => _semiboldFont;
        public SSNoirCameraManager CameraManager => _cameraManager;
        public StageTransitionController StageController => _stageController;
        public bool IsInputLocked => _renderer != null && _renderer.IsInputLocked;
        public bool PointerOverUI => _renderer != null && _renderer.PointerOverUI;
        public Cinemachine.CinemachineVirtualCamera? CurrentFocusCamera => ResolveCurrentFocusCamera();

        public string? CurrentStageContextId
        {
            get
            {
                return ResolveCurrentStageContextId();
            }
        }

        public void SetInputLocked(bool locked) => _renderer?.SetInputLocked(locked);

        private void Start()
        {
            // 1. Initialize Game State & Script Loader
            LoadFonts();
            _gameState = new GameState();
            _scriptLoader = new UnityScriptLoader();
#if UNITY_EDITOR
            // In editor, share save files with the TerminalApp (project root, same as "save.json" cwd default).
            var projectRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath));
            SaveManager.DefaultSavePath = System.IO.Path.Combine(projectRoot, "save.json");
#else
            SaveManager.DefaultSavePath = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
#endif

            // 2. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);

            // 3. Initialize camera interaction. Stable camera selection is resolved from the current node focus.
            _cameraManager = new SSNoirCameraManager(this, panSpeed);

            // 4. Find scene directory
            _sceneDirectory = FindObjectOfType<SceneDirectory>();
            if (_sceneDirectory == null)
            {
                var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
                _sceneDirectory = sdGo.GetComponent<SceneDirectory>();
            }

            // 5. Get pre-placed StageTransitionController (must exist in scene with Inspector fields assigned),
            //    then spawn IMGUIWorldRenderer dynamically (no Inspector fields needed).
            _stageController = GetComponent<StageTransitionController>();
            if (_stageController == null)
                throw new InvalidOperationException("[SSNoir] StageTransitionController must be pre-placed on the SSNoirGameManager GameObject.");
            _renderer = gameObject.AddComponent<IMGUIWorldRenderer>();
            _renderer.Initialize(this);
            _stageController.Initialize(this);

            // 6. Listen to scene loads and world refreshes
            _sceneManager.OnSceneLoaded += () => {
                Debug.Log($"[SSNoir] Scene Loaded: {_sceneManager.CurrentSceneName}");
                ResetSceneUiState();
                AdoptLatestSnapshot();
                UpdateCameraFocus();
            };

            _sceneManager.OnWorldRefreshed += () => {
                Debug.Log($"[SSNoir] World Refreshed! Root node: {_sceneManager.CurrentRootNode?.Name ?? "<null>"}");
            };

            string startingLocation = _gameState.Get<string>("location", "world");
            _sceneManager.LoadScene(startingLocation);
        }

        private void LoadFonts()
        {
#if UNITY_EDITOR
            _regularFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(RegularFontAssetPath);
            _semiboldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(SemiboldFontAssetPath);
#else
            _regularFont = Resources.Load<Font>(RegularFontResourcePath);
            _semiboldFont = Resources.Load<Font>(SemiboldFontResourcePath);
#endif
            if (_regularFont == null)
                Debug.LogError($"[SSNoir] Missing regular font: {RegularFontResourcePath}");
            if (_semiboldFont == null)
                Debug.LogError($"[SSNoir] Missing semibold font: {SemiboldFontResourcePath}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_renderer != null && _renderer.IsAnimationPlaying)
                {
                    if (_renderer.IsAnimationReadyToAcknowledge)
                    {
                        _renderer.AcknowledgePresentationRoll();
                    }
                }
                else if (_renderer == null || !_renderer.IsInputLocked)
                {
                    if (_selectedResource != null)
                    {
                        ClearSelectedResource();
                    }
                    else
                    {
                        GoBackNavigation();
                    }
                }
            }

            // Update camera panning & orbiting
            if (_stageController != null && _stageController.IsTransitioning)
            {
                // A stage transition drives the brain itself; the focus arc must not
                // be holding the brain's blend hostage while it does.
                _cameraManager.FinishFocusTravel();
            }
            else
            {
                // The focus arc runs even while input is locked: a focus change during
                // a scripted beat still has to reach its shot.
                _cameraManager.TickFocusTravel();
                if (!IsInputLocked)
                    _cameraManager.Update();
            }
        }

        public bool IsNodeFlipped(string nodeName) => _flippedNodes.Contains(nodeName);

        public void ToggleNodeFlipped(string nodeName)
        {
            ClearTransientNodeUiState(clearSlots: false);
            if (_flippedNodes.Contains(nodeName))
                _flippedNodes.Remove(nodeName);
            else
                _flippedNodes.Add(nodeName);
        }

        public void OnNodeCardClicked(GameNode node)
        {
            ClearTransientNodeUiState(clearSlots: false);

            if (node.IsContainer)
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
                if (node.Resolve != null && node.Resolve.Type == ResolveType.Instant)
                {
                    SetFocusedNode(node.Name);
                }
                else
                {
                    ExecuteNodeAction(node);
                }
            }
            else
            {
                SetFocusedNode(node.Name);
            }
        }

        public void SetFocusedNode(string? nodeName)
        {
            ClearTransientNodeUiState(clearSlots: false);
            _focusedNodeName = nodeName ?? string.Empty;

            if (!string.IsNullOrEmpty(_focusedNodeName))
                ClearOtherNodeSlots(_focusedNodeName);

            UpdateCameraFocus();
        }

        private void UpdateCameraFocus()
        {
            if (ShouldDeferFocusToPendingPortal())
                return;

            var focusPath = GetCurrentFocusPathNames();
            var focusCamera = ResolveCurrentFocusCamera(focusPath, out var usedWorldFallback);
            if (usedWorldFallback)
            {
                Debug.LogWarning($"[SSNoir] No focus camera resolved for world node path '{string.Join(" > ", focusPath)}'. Falling back to '{WorldRootNodeName}' focus camera for testing.");
            }
            else if (focusCamera == null)
            {
                Debug.LogWarning($"[SSNoir] No focus camera resolved for current node path '{string.Join(" > ", focusPath)}'. Keeping the current camera priority state.");
            }

            if (focusCamera != null)
            {
                // An orbit building keeps its authored shot as the destination; only the
                // path there is taken over, so the camera arcs around the building
                // instead of blending straight through it.
                if (_stageController == null || !_stageController.IsTransitioning)
                    _cameraManager.BeginFocusTravel(focusCamera);

                ResetFocusCameraPriorities();
                focusCamera.Priority = 20;
            }
        }

        private void ResetFocusCameraPriorities()
        {
            if (_sceneDirectory == null)
                return;

            foreach (var a in _sceneDirectory.AllAnchors)
            {
                if (a.FocusVirtualCamera != null)
                    a.FocusVirtualCamera.Priority = 5;
            }
        }

        private bool ShouldDeferFocusToPendingPortal()
        {
            if (_stageController == null)
                return false;

            var currentContextId = CurrentStageContextId;
            if (currentContextId == null || currentContextId == _stageController.CurrentContextId)
                return false;

            if (_stageController.HasActivePortal)
                return true;

            var anchor = _sceneDirectory?.GetAnchor(currentContextId);
            return anchor != null && anchor.GetComponent<StagePortalConfig>() != null;
        }

        private Cinemachine.CinemachineVirtualCamera? ResolveCurrentFocusCamera()
        {
            return ResolveCurrentFocusCamera(GetCurrentFocusPathNames(), out _);
        }

        private Cinemachine.CinemachineVirtualCamera? ResolveCurrentFocusCamera(List<string> focusPath, out bool usedWorldFallback)
        {
            usedWorldFallback = false;
            foreach (var nodeName in focusPath.AsEnumerable().Reverse())
            {
                var anchor = _sceneDirectory?.GetAnchor(nodeName);
                if (anchor != null && anchor.FocusVirtualCamera != null)
                    return anchor.FocusVirtualCamera;
            }

            if (string.Equals(_sceneManager.CurrentSceneName, "world", StringComparison.OrdinalIgnoreCase))
            {
                var worldAnchor = _sceneDirectory?.GetAnchor(WorldRootNodeName);
                if (worldAnchor != null && worldAnchor.FocusVirtualCamera != null)
                {
                    usedWorldFallback = true;
                    return worldAnchor.FocusVirtualCamera;
                }
            }

            return null;
        }

        private List<string> GetCurrentFocusPathNames()
        {
            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                var focusedPath = FindPathToNode(_focusedNodeName);
                if (focusedPath.Count > 0)
                    return focusedPath;
            }

            return GetCurrentNavigationPathNames();
        }

        private List<string> GetCurrentNavigationPathNames()
        {
            var path = new List<string>();
            var root = GetRootNode();
            if (root == null)
                return path;

            path.Add(root.Name);
            foreach (var node in _navigationStack)
                path.Add(node.Name);
            return path;
        }

        private string? ResolveCurrentStageContextId()
        {
            foreach (var nodeName in GetCurrentNavigationPathNames().AsEnumerable().Reverse())
            {
                var anchor = _sceneDirectory?.GetAnchor(nodeName);
                if (anchor != null && anchor.GetComponent<StagePortalConfig>() != null)
                    return nodeName;
            }

            return null;
        }

        private GameNode? GetCurrentNavigationNode()
        {
            if (_navigationStack.Count > 0)
                return _navigationStack[_navigationStack.Count - 1];
            return GetRootNode();
        }

        private GameNode? GetRootNode()
        {
            return _displayedSnapshot.RootNode;
        }

        private List<string> FindPathToNode(string nodeName)
        {
            var result = new List<string>();
            var root = GetRootNode();
            if (root != null && FindPathToNodeRecursive(root, nodeName, result))
                return result;
            return new List<string>();
        }

        private bool FindPathToNodeRecursive(GameNode node, string nodeName, List<string> path)
        {
            path.Add(node.Name);
            if (node.Name == nodeName)
                return true;

            foreach (var child in node.Children)
            {
                if (FindPathToNodeRecursive(child, nodeName, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
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
                        list.Add(null);
                    _nodeSlots[nodeName] = list;
                }
            }
            return list;
        }

        public void OnSlotClicked(GameNode node, int slotIndex)
        {
            ClearTransientNodeUiState(clearSlots: false);
            BeginSlotResourceDrag(node, slotIndex);
        }

        private SelectedResource CreateSelectedResourceFromSlot(SlottedResource resource)
        {
            if (resource.Type == "die")
            {
                return new SelectedResource
                {
                    Type = "die",
                    Value = resource.Value,
                    SourceIndex = resource.SourceIndex,
                    ActorId = resource.ActorId,
                    DieIndex = resource.DieIndex
                };
            }

            return new SelectedResource
            {
                Type = "item",
                ItemName = resource.ItemId,
                Value = resource.Qty > 0 ? resource.Qty : resource.Value
            };
        }

        public bool CanMatchRequirement(ActionCost req)
        {
            if (_selectedResource == null)
            {
                return false;
            }

            if (req.Type == "die")
            {
                return _selectedResource.Type == "die";
            }

            if (req.Type == "item")
            {
                return _selectedResource.Type == "item"
                    && req.ItemId.Equals(_selectedResource.ItemName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public bool CanPlaceSelectedResource(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            // A filled slot is a valid target too — placing replaces it, and the displaced
            // die returns to hand automatically (slot state is derived, not consumed).
            if (_selectedResource == null
                || node.Requires == null
                || slots == null
                || slotIndex < 0
                || slotIndex >= node.Requires.Count
                || slotIndex >= slots.Count)
            {
                return false;
            }

            var req = node.Requires[slotIndex];
            if (!CanMatchRequirement(req))
            {
                return false;
            }

            if (req.Type == "die")
            {
                return true;
            }

            int totalOwned = _displayedSnapshot.Inventory.TryGetValue(req.ItemId, out var ownedQty) ? ownedQty : 0;
            int totalSlotted = GetTotalSlottedItemQty(req.ItemId, slots, slotIndex);
            return totalOwned - totalSlotted >= req.Qty;
        }

        public bool TryPlaceSelectedResource(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            if (_selectedResource == null || slots == null || node.Requires == null)
            {
                return false;
            }

            var req = node.Requires[slotIndex];
            if (!CanPlaceSelectedResource(node, slotIndex))
            {
                ShowNotification(CanMatchRequirement(req)
                    ? $"缺少数量，需要 {req.Qty} 个 {req.ItemId}"
                    : $"槽位需要: {(req.Type == "die" ? "骰子" : req.ItemId)}");
                return false;
            }

            ClearOtherNodeSlots(node.Name);
            if (req.Type == "die" && _selectedResource.Type == "die")
            {
                ClearTransientNodeUiState(clearSlots: false);
                ClearDieFromAllSlots(_selectedResource.SourceIndex);
                slots[slotIndex] = new SlottedResource
                {
                    Type = "die",
                    Value = _selectedResource.Value,
                    SourceIndex = _selectedResource.SourceIndex,
                    ActorId = _selectedResource.ActorId,
                    DieIndex = _selectedResource.DieIndex
                };
                _selectedResource = null;
                return true;
            }

            if (req.Type == "item" && _selectedResource.Type == "item")
            {
                ClearTransientNodeUiState(clearSlots: false);
                slots[slotIndex] = new SlottedResource
                {
                    Type = "item",
                    ItemId = req.ItemId,
                    Value = req.Qty,
                    Qty = req.Qty
                };
                _selectedResource = null;
                return true;
            }

            return false;
        }

        private void ClearOtherNodeSlots(string activeNodeName)
        {
            foreach (var pair in _nodeSlots)
            {
                if (pair.Key != activeNodeName)
                {
                    var slots = pair.Value;
                    for (int i = 0; i < slots.Count; i++)
                        slots[i] = null;
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
                        list[i] = null;
                }
            }
        }

        public int GetTotalSlottedItemQty(string itemName)
        {
            return GetTotalSlottedItemQty(itemName, null, -1);
        }

        private int GetTotalSlottedItemQty(string itemName, List<SlottedResource?>? targetSlots, int targetSlotIndex)
        {
            int sum = 0;
            foreach (var list in _nodeSlots.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list, targetSlots) && i == targetSlotIndex)
                    {
                        continue;
                    }

                    var s = list[i];
                    if (s != null && s.Type == "item" && s.ItemId == itemName)
                        sum += s.Qty > 0 ? s.Qty : s.Value;
                }
            }
            return sum;
        }

        public bool IsDieSlotted(int dieIndex)
        {
            if (_selectedResource != null && _selectedResource.Type == "die" && _selectedResource.SourceIndex == dieIndex)
                return true;
            foreach (var slots in _nodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "die" && slot.SourceIndex == dieIndex)
                        return true;
                }
            }
            return false;
        }

        public int GetRemainingItemQty(string itemName)
        {
            int total = _displayedSnapshot.Inventory.TryGetValue(itemName, out var qty) ? qty : 0;

            foreach (var slots in _nodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "item" && slot.ItemId == itemName)
                        total -= slot.Qty > 0 ? slot.Qty : slot.Value;
                }
            }

            return Mathf.Max(0, total);
        }

        public void ClearSelectedResource()
        {
            if (_resourceDragActive)
            {
                RestoreResourceDragOrigin();
                _resourceDragActive = false;
                ClearResourceDragState();
                return;
            }

            _selectedResource = null;
        }

        // --- Execute Actions via Async Coroutine ---
        public void ExecuteNodeAction(GameNode node)
        {
            StartCoroutine(ExecuteRoutine(node));
        }

        private IEnumerator ExecuteRoutine(GameNode node)
        {
            var slots = GetSlotsForNode(node.Name) ?? new List<SlottedResource?>();

            _renderer.ClearCardResidues();
            _renderer.SetInputLocked(true);

            bool done = false;
            try
            {
                string sceneBefore = _sceneManager.CurrentSceneName;
                ActionReport report = _sceneManager.ExecuteAction(node, slots);
                _nodeSlots.Remove(node.Name);
                _selectedResource = null;
                bool sceneChanged = !string.Equals(sceneBefore, _sceneManager.CurrentSceneName, System.StringComparison.OrdinalIgnoreCase);
                if (sceneChanged)
                {
                    ResetSceneUiState();
                }

                _renderer.PlayPresentation(report, sceneChanged ? string.Empty : node.Name, () =>
                {
                    AdoptLatestSnapshot();
                    UpdateCameraFocus();
                    done = true;
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExecuteNodeAction] Exception during execution: {ex}");
                ShowNotification($"执行异常: {ex.Message}");
                done = true;
            }

            while (!done)
            {
                yield return null;
            }

            _renderer.SetInputLocked(false);
        }

        private void ResetSceneUiState()
        {
            _navigationStack.Clear();
            _nodeSlots.Clear();
            _flippedNodes.Clear();
            _selectedResource = null;
            _focusedNodeName = string.Empty;
            _renderer?.ResetUiState();
        }

        public void AdoptLatestSnapshot()
        {
            _displayedSnapshot = _sceneManager.LatestSnapshot;
            ResolveNavigationStack();
            CleanupNodeSlots();
        }

        public void UpgradeActorStat(string actorId, string statKey)
        {
            try
            {
                _gameState.Team.UpgradeActorStat(actorId, statKey);
                _sceneManager.RebuildRenderTree();
                AdoptLatestSnapshot();

                var actor = _gameState.Team.FindActor(actorId);
                string actorName = actor?.Name ?? actorId;
                _gameState.NotificationCenter.Push($"{actorName} upgraded {statKey}!", NotificationKind.Success);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UpgradeActorStat] Exception: {ex}");
                ShowNotification($"升级异常: {ex.Message}");
            }
        }

        public void GoBackNavigation()
        {
            ClearTransientNodeUiState(clearSlots: false);

            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                // 从聚焦状态返回：取消聚焦
                SetFocusedNode(null);
            }
            else if (_navigationStack.Count > 0)
            {
                // 从导航栈返回：弹出栈
                _nodeSlots.Clear();
                _navigationStack.RemoveAt(_navigationStack.Count - 1);
                ResolveNavigationStack();

                // Update camera focus priority for navigation parent or global view
                UpdateCameraFocus();
            }
        }

        private void ResolveNavigationStack()
        {
            var root = GetRootNode();
            if (root == null)
            {
                _navigationStack.Clear();
                _visibleNodes = new List<GameNode>();
                return;
            }

            if (_navigationStack.Count == 0)
            {
                _visibleNodes = root.Children.ToList();
                return;
            }

            var path = new List<string>();
            foreach (var node in _navigationStack)
                path.Add(node.Name);

            _navigationStack.Clear();
            var currentLevel = root.Children.ToList();

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.IsContainer)
                {
                    _navigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    _navigationStack.Clear();
                    _visibleNodes = root.Children.ToList();
                    return;
                }
            }

            _visibleNodes = currentLevel;
        }

        private void CleanupNodeSlots()
        {
            var currentNames = new HashSet<string>();
            var root = GetRootNode();
            if (root != null)
                CollectAllNodeNamesRecursive(root, currentNames);

            var keysToRemove = new List<string>();
            foreach (var name in _nodeSlots.Keys)
            {
                if (!currentNames.Contains(name))
                    keysToRemove.Add(name);
            }
            foreach (var key in keysToRemove)
                _nodeSlots.Remove(key);
        }

        private void CollectAllNodeNamesRecursive(GameNode node, HashSet<string> result)
        {
            result.Add(node.Name);
            foreach (var child in node.Children)
                CollectAllNodeNamesRecursive(child, result);
        }

        private GameNode? FindNodeByName(string name)
        {
            var root = GetRootNode();
            return root == null ? null : FindNodeRecursive(root, name);
        }

        private GameNode? FindNodeRecursive(GameNode node, string name)
        {
            if (node.Name == name)
                return node;

            foreach (var childNode in node.Children)
            {
                var child = FindNodeRecursive(childNode, name);
                if (child != null) return child;
            }
            return null;
        }

        public void ShowNotification(string message)
        {
            _gameState.NotificationCenter.Push(message, NotificationKind.Info);
        }

        public void OnSceneButtonClicked(string sc)
        {
            _selectedResource = null;
            _navigationStack.Clear();
            _sceneManager.GoToLocation(sc);
        }

        public List<string> LoadAvailableSceneNames()
        {
            return _scriptLoader.LoadSceneNames();
        }

        public void SaveGame(string? filePath = null)
        {
            try
            {
                var path = filePath ?? SaveManager.DefaultSavePath;
                _sceneManager.SaveGame(path);
                _gameState.NotificationCenter.Push("游戏已存档。", NotificationKind.Success);
                Debug.Log($"[SSNoir] Game saved to {path}");
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"存档失败: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] SaveGame failed: {ex}");
            }
        }

        public void LoadGame(string? filePath = null)
        {
            var path = filePath ?? SaveManager.DefaultSavePath;
            if (!System.IO.File.Exists(path))
            {
                _gameState.NotificationCenter.Push("没有找到存档文件。", NotificationKind.Warning);
                Debug.LogWarning($"[SSNoir] No save file found at {path}");
                return;
            }
            try
            {
                _sceneManager.LoadGame(path);
                // OnSceneLoaded fires inside LoadGame → ResetSceneUiState → ResetUiState
                _gameState.NotificationCenter.Push("游戏已读档。", NotificationKind.Success);
                Debug.Log($"[SSNoir] Game loaded from {path}");
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"读档失败: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] LoadGame failed: {ex}");
            }
        }

        public void RestartGame()
        {
            _sceneManager.ResetForNewGame();
        }

        public bool IsDraggingResource => _resourceDragActive;

        public void BeginDieDrag(int dieIndex, int val, Vector2 mouse)
        {
            ResolveDieOwner(dieIndex, out string actorId, out int innerDieIndex);
            BeginResourceDrag(new SelectedResource
            {
                Type = "die",
                Value = val,
                SourceIndex = dieIndex,
                ActorId = actorId,
                DieIndex = innerDieIndex
            }, ResourceDragOriginKind.Hand);
        }

        public void BeginItemDrag(string itemName, int qty, Vector2 mouse)
        {
            BeginResourceDrag(new SelectedResource
            {
                Type = "item",
                ItemName = itemName,
                Value = qty
            }, ResourceDragOriginKind.Hand);
        }

        public void BeginSlotResourceDrag(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;

            var existing = slots[slotIndex];
            if (existing == null) return;

            ClearTransientNodeUiState(clearSlots: false);
            slots[slotIndex] = null;
            _resourceOriginNodeName = node.Name;
            _resourceOriginSlotIndex = slotIndex;
            _resourceOriginSlotResource = CloneSlottedResource(existing);
            BeginResourceDrag(CreateSelectedResourceFromSlot(existing), ResourceDragOriginKind.Slot);
        }

        public void MarkResourceDropHandled() => _resourceDropHandled = true;

        public void EndResourceDrag(Vector2 mouse)
        {
            if (!_resourceDragActive) return;
            _resourceDragActive = false;

            if (!_resourceDropHandled)
                RestoreResourceDragOrigin();

            ClearResourceDragState();
        }

        private void BeginResourceDrag(SelectedResource resource, ResourceDragOriginKind origin)
        {
            _selectedResource = resource;
            _resourceDragActive = true;
            _resourceDropHandled = false;
            _resourceDragOrigin = origin;
            if (origin != ResourceDragOriginKind.Slot)
            {
                _resourceOriginNodeName = string.Empty;
                _resourceOriginSlotIndex = -1;
                _resourceOriginSlotResource = null;
            }
        }

        private void RestoreResourceDragOrigin()
        {
            if (_resourceDragOrigin == ResourceDragOriginKind.Slot)
            {
                RestoreDraggedSlotResource();
            }

            _selectedResource = null;
        }

        private void RestoreDraggedSlotResource()
        {
            if (string.IsNullOrEmpty(_resourceOriginNodeName)
                || _resourceOriginSlotIndex < 0
                || _resourceOriginSlotResource == null)
            {
                Debug.LogError("[SSNoir] Resource drag lost its slot origin.");
                return;
            }

            var slots = GetSlotsForNode(_resourceOriginNodeName);
            if (slots == null || _resourceOriginSlotIndex >= slots.Count)
            {
                Debug.LogError($"[SSNoir] Resource drag origin slot is invalid: {_resourceOriginNodeName}[{_resourceOriginSlotIndex}].");
                return;
            }

            if (slots[_resourceOriginSlotIndex] != null)
            {
                Debug.LogError($"[SSNoir] Resource drag origin slot was unexpectedly occupied: {_resourceOriginNodeName}[{_resourceOriginSlotIndex}].");
                return;
            }

            slots[_resourceOriginSlotIndex] = CloneSlottedResource(_resourceOriginSlotResource);
        }

        private void ClearResourceDragState()
        {
            _selectedResource = null;
            _resourceDropHandled = false;
            _resourceDragOrigin = ResourceDragOriginKind.None;
            _resourceOriginNodeName = string.Empty;
            _resourceOriginSlotIndex = -1;
            _resourceOriginSlotResource = null;
        }

        private static SlottedResource CloneSlottedResource(SlottedResource resource)
        {
            return new SlottedResource
            {
                Type = resource.Type,
                ItemId = resource.ItemId,
                Value = resource.Value,
                SourceIndex = resource.SourceIndex,
                ActorId = resource.ActorId,
                DieIndex = resource.DieIndex,
                Qty = resource.Qty
            };
        }

        // Maps a flat action-die index (as enumerated by the hand panel:
        // Team.Actors in order, each actor's ActionDice in order) back to the
        // owning actor and that actor's local die index.
        private void ResolveDieOwner(int flatIndex, out string actorId, out int innerIndex)
        {
            int running = 0;
            foreach (var actor in _gameState.Team.Actors)
            {
                for (int i = 0; i < actor.ActionDice.Count; i++)
                {
                    if (running == flatIndex)
                    {
                        actorId = actor.Id;
                        innerIndex = actor.ActionDiceSlotIds[i];
                        return;
                    }
                    running++;
                }
            }
            actorId = string.Empty;
            innerIndex = -1;
        }

        public void OnEndTurnClicked()
        {
            _selectedResource = null;
            var report = _sceneManager.EndTurn();

            bool done = false;
            _renderer.PlayPresentation(report, "休息", () =>
            {
                AdoptLatestSnapshot();
                done = true;
            });
            StartCoroutine(WaitForPresentation(() => done));
        }

        public void OnUseEncounterConsumable(string itemId)
        {
            _selectedResource = null;
            var report = _sceneManager.UseEncounterConsumable(itemId);
            bool done = false;
            _renderer.PlayPresentation(report, itemId == "香烟" ? "抽烟" : "喝酒", () =>
            {
                AdoptLatestSnapshot();
                done = true;
            });
            StartCoroutine(WaitForPresentation(() => done));
        }

        private IEnumerator WaitForPresentation(Func<bool> isDone)
        {
            while (!isDone())
            {
                yield return null;
            }
        }

        public void NavigateToHome()
        {
            var homePath = FindPathToNode("家");
            if (homePath.Count < 2)
            {
                Debug.LogWarning("[SSNoir] Expected '家' node in world.");
                return;
            }

            bool alreadyAtHome = _navigationStack.Count > 0
                && _navigationStack[_navigationStack.Count - 1].Name == "家";

            ClearTransientNodeUiState(clearSlots: true);
            _selectedResource = null;

            if (alreadyAtHome)
            {
                return;
            }

            _navigationStack.Clear();
            foreach (var nodeName in homePath.GetRange(1, homePath.Count - 1))
            {
                var node = FindNodeByName(nodeName);
                Debug.Assert(node != null, $"[SSNoir] Failed to resolve path node '{nodeName}' while navigating home.");
                if (node != null)
                    _navigationStack.Add(node);
            }
            ResolveNavigationStack();
            UpdateCameraFocus();
        }

        private void ClearTransientNodeUiState(bool clearSlots)
        {
            _renderer?.ClearCardResidues();
            if (clearSlots)
                _nodeSlots.Clear();
        }
    }
}
