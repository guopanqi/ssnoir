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

    public class RollResult
    {
        public string ActionName { get; set; } = string.Empty;
        public int ChosenDie { get; set; }
        public List<int> RandomDice { get; set; } = new List<int>();
        public int FinalValue { get; set; } = 0;
        public string Outcome { get; set; } = string.Empty;
    }

    public class SSNoirGameManager : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [SerializeField] private Cinemachine.CinemachineVirtualCamera? globalCamera;

        [Header("Font")]
        [SerializeField] private Font? chineseFont;

        [Header("Camera Drag Settings")]
        [SerializeField] private float panSpeed = 0.02f;

        private GameState _gameState = null!;
        private SceneManager _sceneManager = null!;
        private UnityScriptLoader _scriptLoader = null!;
        private SceneDirectory? _sceneDirectory;
        private IMGUIWorldRenderer _renderer = null!;

        private SSNoirCameraManager _cameraManager = null!;

        // Core Gameplay / Interaction State
        private string _focusedNodeName = string.Empty;
        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();
        private SelectedResource? _selectedResource;
        private RollResult? _activeRollResult;

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
        public RollResult? ActiveRollResult => _activeRollResult;
        public Font? ChineseFont => chineseFont;
        public SSNoirCameraManager CameraManager => _cameraManager;

        private void Start()
        {
            // 1. Initialize Game State & Script Loader
            _gameState = new GameState();
            _scriptLoader = new UnityScriptLoader();
            SaveManager.DefaultSavePath = System.IO.Path.Combine(Application.persistentDataPath, "save.json");

            // 2. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);

            // 3. Find scene cameras
            if (globalCamera == null)
            {
                globalCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Global") || c.name.Contains("global"));
            }
            _cameraManager = new SSNoirCameraManager(this, globalCamera, panSpeed);

            // 4. Find scene directory
            _sceneDirectory = FindObjectOfType<SceneDirectory>();
            if (_sceneDirectory == null)
            {
                var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
                _sceneDirectory = sdGo.GetComponent<SceneDirectory>();
            }

            // 5. Spawn IMGUI renderer
            _renderer = gameObject.AddComponent<IMGUIWorldRenderer>();
            _renderer.Initialize(this);

            // 6. Listen to scene loads and world refreshes
            _sceneManager.OnSceneLoaded += () => {
                Debug.Log($"[SSNoir] Scene Loaded: {_sceneManager.CurrentSceneName}");
                ResetSceneUiState();
                RefreshSceneCameraReference();
                AdoptLatestSnapshot();
                UpdateCameraFocus();
            };

            _sceneManager.OnWorldRefreshed += () => {
                Debug.Log($"[SSNoir] World Refreshed! Latest nodes count: {_sceneManager.CurrentWorldNodes.Count}");
            };

            string startingLocation = _gameState.Get<string>("location", "world");
            _sceneManager.LoadScene(startingLocation);
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
            _cameraManager.Update();
        }

        public bool IsNodeFlipped(string nodeName) => _flippedNodes.Contains(nodeName);

        public void ToggleNodeFlipped(string nodeName)
        {
            if (_flippedNodes.Contains(nodeName))
                _flippedNodes.Remove(nodeName);
            else
                _flippedNodes.Add(nodeName);
        }

        public void OnNodeCardClicked(GameNode node)
        {
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
            _focusedNodeName = nodeName ?? string.Empty;

            if (!string.IsNullOrEmpty(_focusedNodeName))
                ClearOtherNodeSlots(_focusedNodeName);

            UpdateCameraFocus();
        }

        private void UpdateCameraFocus()
        {

            // Reset all scene anchor focus cameras
            if (_sceneDirectory != null)
            {
                foreach (var a in _sceneDirectory.AllAnchors)
                {
                    if (a.FocusVirtualCamera != null)
                    {
                        a.FocusVirtualCamera.Priority = 5;
                    }
                }
            }

            // Find target anchor to focus
            NodeAnchor? targetAnchor = null;
            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                targetAnchor = _sceneDirectory?.GetAnchor(_focusedNodeName);
            }
            else if (_navigationStack.Count > 0)
            {
                var parentNode = _navigationStack[_navigationStack.Count - 1];
                targetAnchor = _sceneDirectory?.GetAnchor(parentNode.Name);
            }

            // Set priority on target camera
            if (targetAnchor != null && targetAnchor.FocusVirtualCamera != null)
            {
                targetAnchor.FocusVirtualCamera.Priority = 20;
            }
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
                        SourceIndex = _selectedResource.SourceIndex,
                        ActorId = _selectedResource.ActorId,
                        DieIndex = _selectedResource.DieIndex
                    };
                    _selectedResource = null;
                }
                else if (req.Type == "item" && _selectedResource.Type == "item" && req.ItemId == _selectedResource.ItemName)
                {
                    int totalOwned = _displayedSnapshot.Inventory.TryGetValue(req.ItemId, out var ownedQty) ? ownedQty : 0;
                    int totalSlotted = GetTotalSlottedItemQty(req.ItemId);
                    int available = totalOwned - totalSlotted;

                    if (available >= req.Qty)
                    {
                        slots[slotIndex] = new SlottedResource
                        {
                            Type = "item",
                            ItemId = req.ItemId,
                            Value = req.Qty,
                            Qty = req.Qty
                        };
                        _selectedResource = null;
                    }
                    else
                    {
                        ShowNotification($"缺少数量，需要 {req.Qty} 个 {req.ItemId}");
                    }
                }
                else
                {
                    ShowNotification($"槽位需要: {(req.Type == "die" ? "骰子" : req.ItemId)}");
                }
            }
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
            int sum = 0;
            foreach (var list in _nodeSlots.Values)
            {
                foreach (var s in list)
                {
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

            if (_selectedResource != null && _selectedResource.Type == "item" && _selectedResource.ItemName == itemName)
            {
                if (itemName != "金钱")
                    total -= 1;
            }

            return Mathf.Max(0, total);
        }

        public void ClearSelectedResource()
        {
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

                _renderer.PlayPresentation(report, node.Name, () =>
                {
                    if (sceneChanged)
                    {
                        RefreshSceneCameraReference();
                    }
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

        private void RefreshSceneCameraReference()
        {
            globalCamera = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>().FirstOrDefault(c => c.name.Contains("Global") || c.name.Contains("global"));
            _cameraManager.SetGlobalCamera(globalCamera);
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
            if (_navigationStack.Count == 0)
            {
                _visibleNodes = _displayedSnapshot.Nodes.ToList();
                return;
            }

            var path = new List<string>();
            foreach (var node in _navigationStack)
                path.Add(node.Name);

            _navigationStack.Clear();
            var currentLevel = _displayedSnapshot.Nodes.ToList();

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
                    _visibleNodes = _displayedSnapshot.Nodes.ToList();
                    return;
                }
            }

            _visibleNodes = currentLevel;
        }

        private void CleanupNodeSlots()
        {
            var currentNames = new HashSet<string>();
            CollectAllNodeNamesRecursive(_displayedSnapshot.Nodes.ToList(), currentNames);

            var keysToRemove = new List<string>();
            foreach (var name in _nodeSlots.Keys)
            {
                if (!currentNames.Contains(name))
                    keysToRemove.Add(name);
            }
            foreach (var key in keysToRemove)
                _nodeSlots.Remove(key);
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
            return FindNodeRecursive(_displayedSnapshot.Nodes.ToList(), name);
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
            _gameState.NotificationCenter.Push(message, NotificationKind.Info);
        }

        public void OnSceneButtonClicked(string sc)
        {
            _selectedResource = null;
            _navigationStack.Clear();
            _gameState.Set("location", sc);
        }

        public List<string> LoadAvailableSceneNames()
        {
            return _scriptLoader.LoadSceneNames();
        }

        public void SaveGame()
        {
            try
            {
                _sceneManager.SaveGame();
                _gameState.NotificationCenter.Push("游戏已存档。", NotificationKind.Success);
                Debug.Log($"[SSNoir] Game saved to {SaveManager.DefaultSavePath}");
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"存档失败: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] SaveGame failed: {ex}");
            }
        }

        public void LoadGame()
        {
            if (!System.IO.File.Exists(SaveManager.DefaultSavePath))
            {
                _gameState.NotificationCenter.Push("没有找到存档文件。", NotificationKind.Warning);
                Debug.LogWarning($"[SSNoir] No save file found at {SaveManager.DefaultSavePath}");
                return;
            }
            try
            {
                _sceneManager.LoadGame();
                // OnSceneLoaded fires inside LoadGame → ResetSceneUiState → ResetUiState
                _gameState.NotificationCenter.Push("游戏已读档。", NotificationKind.Success);
                Debug.Log($"[SSNoir] Game loaded from {SaveManager.DefaultSavePath}");
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"读档失败: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] LoadGame failed: {ex}");
            }
        }

        public void OnDieClicked(int dieIndex, int val)
        {
            if (_selectedResource != null && _selectedResource.Type == "die" && _selectedResource.SourceIndex == dieIndex)
                _selectedResource = null;
            else
            {
                string actorId = "";
                int innerDieIndex = -1;
                var owners = _gameState.Get<List<object>>("action-dice-owners");
                if (owners != null && dieIndex >= 0 && dieIndex < owners.Count)
                {
                    actorId = owners[dieIndex]?.ToString() ?? "";
                    int count = 0;
                    for (int j = 0; j < dieIndex; j++)
                    {
                        if (owners[j]?.ToString() == actorId) count++;
                    }
                    innerDieIndex = count;
                }

                _selectedResource = new SelectedResource
                {
                    Type = "die",
                    Value = val,
                    SourceIndex = dieIndex,
                    ActorId = actorId,
                    DieIndex = innerDieIndex
                };
            }
        }

        public void OnItemClicked(string itemName, int qty)
        {
            if (_selectedResource != null && _selectedResource.Type == "item" && _selectedResource.ItemName == itemName)
                _selectedResource = null;
            else
            {
                _selectedResource = new SelectedResource
                {
                    Type = "item",
                    ItemName = itemName,
                    Value = qty
                };
            }
        }

        public void OnEndTurnClicked()
        {
            _selectedResource = null;
            _sceneManager.EndTurn();

            bool done = false;
            _renderer.PlayPresentation(CreateEndTurnReport(), "休息", () =>
            {
                AdoptLatestSnapshot();
                done = true;
            });
            StartCoroutine(WaitForPresentation(() => done));
        }

        private static ActionReport CreateEndTurnReport()
        {
            return new ActionReport
            {
                Type = ActionType.Instant,
                PresentationHints = new List<PresentationHint>
                {
                    new PresentationHint
                    {
                        Kind = PresentationHintKind.ExecuteProgress,
                        Text = "回合结束",
                        DurationSeconds = 0.2f,
                    },
                },
            };
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
            var homeNode = FindNodeByName(_displayedSnapshot.Nodes.ToList(), "家");
            if (homeNode == null)
            {
                Debug.LogWarning("[SSNoir] Expected '家' node in world.");
                return;
            }

            bool alreadyAtHome = _navigationStack.Count > 0
                && _navigationStack[_navigationStack.Count - 1].Name == "家";

            if (alreadyAtHome)
            {
                return;
            }

            _nodeSlots.Clear();
            _selectedResource = null;
            _navigationStack.Clear();
            _navigationStack.Add(homeNode);
            ResolveNavigationStack();
            UpdateCameraFocus();
        }

        private GameNode? FindNodeByName(List<GameNode> nodes, string name)
        {
            foreach (var node in nodes)
            {
                if (node.Name == name) return node;
                var found = FindNodeByName(node.Children, name);
                if (found != null) return found;
            }
            return null;
        }

        public void OnRollAckClicked()
        {
            _renderer.AcknowledgePresentationRoll();
        }
    }
}
