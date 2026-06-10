#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;

namespace SSNoir
{
    public class UIManager : MonoBehaviour
    {
        private SSNoirGameManager _gameManager = null!;

        // UI Root Canvas and elements
        private Canvas? _canvas;
        private GameObject? _worldNodesContainer;
        private GameObject? _childNodesContainer;
        private GameObject? _bottomPanel;
        private GameObject? _overlayContainer;
        private GameObject? _cursorFollower;
        private GameObject? _globalInputBlocker;

        // Active widgets diff dictionary (string name -> widget)
        private readonly Dictionary<string, NodeUIWidget> _activeWidgets = new Dictionary<string, NodeUIWidget>();

        public GameObject? WorldNodesContainer => _worldNodesContainer;
        public Canvas? Canvas => _canvas;

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
            InitCanvas();
        }

        public void InitCanvas()
        {
            var canvasGo = new GameObject("SSNoirCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            // World Nodes Container (stretch)
            _worldNodesContainer = new GameObject("WorldNodesContainer", typeof(RectTransform));
            _worldNodesContainer.transform.SetParent(_canvas.transform, false);
            var worldRt = _worldNodesContainer.GetComponent<RectTransform>();
            worldRt.anchorMin = Vector2.zero;
            worldRt.anchorMax = Vector2.one;
            worldRt.sizeDelta = Vector2.zero;

            // Child Nodes Container (horizontal layout)
            _childNodesContainer = new GameObject("ChildNodesContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _childNodesContainer.transform.SetParent(_canvas.transform, false);
            var childRt = _childNodesContainer.GetComponent<RectTransform>();
            childRt.anchorMin = new Vector2(0f, 0.2f);
            childRt.anchorMax = new Vector2(1f, 0.9f);
            childRt.sizeDelta = Vector2.zero;
            var chlg = _childNodesContainer.GetComponent<HorizontalLayoutGroup>();
            chlg.padding = new RectOffset(40, 40, 40, 40);
            chlg.spacing = 20f;
            chlg.childAlignment = TextAnchor.MiddleCenter;
            chlg.childControlWidth = false;
            chlg.childControlHeight = false;
            chlg.childForceExpandWidth = false;
            chlg.childForceExpandHeight = false;

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

            // Global Input Blocker Mask (disabled by default)
            _globalInputBlocker = UIHelper.CreatePanel(_canvas.transform, "GlobalInputBlocker", new Color(0, 0, 0, 0), default, true);
            var blockerRt = _globalInputBlocker.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.sizeDelta = Vector2.zero;
            _globalInputBlocker.SetActive(false);
        }

        private void Update()
        {
            // Update Cursor Follower position and content
            if (_cursorFollower != null)
            {
                var selectedRes = _gameManager.SelectedResource;
                if (selectedRes != null)
                {
                    if (!_cursorFollower.activeSelf)
                    {
                        _cursorFollower.SetActive(true);
                    }

                    var txt = _cursorFollower.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                    {
                        txt.text = selectedRes.Type == "die" 
                            ? "D" + selectedRes.Value 
                            : selectedRes.ItemName;
                    }

                    var rt = _cursorFollower.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(selectedRes.Type == "die" ? 50f : 100f, 26f);
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
        }

        public void SetInputLocked(bool isLocked)
        {
            if (_globalInputBlocker != null)
            {
                _globalInputBlocker.SetActive(isLocked);
            }
        }

        public void RefreshWorld(List<GameNode> newNodes)
        {
            if (_worldNodesContainer == null || _childNodesContainer == null) return;

            // 增量 Diff 逻辑
            var newNodesMap = new Dictionary<string, GameNode>();

            foreach (var node in newNodes)
            {
                if (!string.IsNullOrEmpty(_gameManager.FocusedNodeName) && node.Name != _gameManager.FocusedNodeName)
                {
                    continue; // 聚焦时隐藏非相关节点
                }
                newNodesMap[node.Name] = node;
            }

            // 1. 销毁从世界地图里消失的节点（字典里有，新数据里没有的）
            var keysToRemove = new List<string>();
            foreach (var pair in _activeWidgets)
            {
                if (!newNodesMap.ContainsKey(pair.Key))
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                var widget = _activeWidgets[key];
                if (widget != null)
                {
                    Destroy(widget.gameObject);
                }
                _activeWidgets.Remove(key);
            }

            // 2. 新增或更新节点卡片（数据更新时只做 SetData，不销毁外层 Widget 容器）
            foreach (var node in newNodesMap.Values)
            {
                var anchor = _gameManager.SceneDirectory?.GetAnchor(node.Name);
                bool inChildLevel = _gameManager.NavigationStack.Count > 0;
                
                if (anchor == null && !inChildLevel)
                {
                    Debug.LogWarning($"[SSNoir] Node '{node.Name}' has no anchor and is at root level. It will be placed in the child container as a fallback.");
                }

                if (_activeWidgets.TryGetValue(node.Name, out var existingWidget))
                {
                    existingWidget.SetData(node);
                    existingWidget.transform.SetParent(
                        (inChildLevel || anchor == null) ? _childNodesContainer.transform : _worldNodesContainer.transform, 
                        false);
                }
                else
                {
                    var widgetGo = new GameObject("NodeWidget_" + node.Name, typeof(RectTransform), typeof(NodeUIWidget));
                    widgetGo.transform.SetParent(
                        (inChildLevel || anchor == null) ? _childNodesContainer.transform : _worldNodesContainer.transform, 
                        false);

                    var newWidget = widgetGo.GetComponent<NodeUIWidget>();
                    newWidget.Setup(node, anchor, _gameManager);
                    _activeWidgets[node.Name] = newWidget;
                }
            }

            // 3. 刷新底部与覆盖遮罩
            BuildBottomPanel();
            BuildOverlay();
        }

        public void BuildBottomPanel()
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
            var leftGo = new GameObject("LeftSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
            leftGo.transform.SetParent(layoutGo.transform, false);
            
            var leftVlg = leftGo.GetComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 8f;
            leftVlg.childAlignment = TextAnchor.MiddleLeft;
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = false;
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;

            int health = _gameManager.GameState.Get<int>("health");
            var statusText = UIHelper.CreateText(leftGo.transform, $"生命: {health}", 14, Color.white, TextAlignmentOptions.Left);
            statusText.fontStyle = FontStyles.Bold;

            UIHelper.CreateText(leftGo.transform, "场景切换:", 11, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Left);
            
            var sceneButtonsRow = new GameObject("SceneButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            sceneButtonsRow.transform.SetParent(leftGo.transform, false);
            var sceneButtonsHlg = sceneButtonsRow.GetComponent<HorizontalLayoutGroup>();
            sceneButtonsHlg.spacing = 6f;
            sceneButtonsHlg.childAlignment = TextAnchor.MiddleLeft;
            sceneButtonsHlg.childControlWidth = false;
            sceneButtonsHlg.childControlHeight = false;
            sceneButtonsHlg.childForceExpandWidth = false;
            sceneButtonsHlg.childForceExpandHeight = false;

            string[] scenes = { "home", "office", "combat" };
            foreach (var sc in scenes)
            {
                bool isCurrent = _gameManager.SceneManager.CurrentSceneName == sc;
                Color btnColor = isCurrent ? new Color(0.2f, 0.55f, 0.35f) : new Color(0.25f, 0.25f, 0.3f);
                string label = sc == "home" ? "家" : sc == "office" ? "办公室" : "战斗";
                UIHelper.CreateButton(sceneButtonsRow.transform, label, btnColor, () =>
                {
                    _gameManager.OnSceneButtonClicked(sc);
                }, new Vector2(70, 26));
            }

            // --- Middle Section: Hand Resources ---
            var middleGo = new GameObject("MiddleSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
            middleGo.transform.SetParent(layoutGo.transform, false);
            
            var middleVlg = middleGo.GetComponent<VerticalLayoutGroup>();
            middleVlg.spacing = 8f;
            middleVlg.childAlignment = TextAnchor.MiddleCenter;
            middleVlg.childControlWidth = true;
            middleVlg.childControlHeight = false;
            middleVlg.childForceExpandWidth = true;
            middleVlg.childForceExpandHeight = false;

            string selectedLabel = _gameManager.SelectedResource != null 
                ? $"已选中: {(_gameManager.SelectedResource.Type == "die" ? "D" + _gameManager.SelectedResource.Value : _gameManager.SelectedResource.ItemName)}" 
                : "选择骰子/道具以投入槽位";
            var selectedText = UIHelper.CreateText(middleGo.transform, selectedLabel, 11, new Color(0.9f, 0.9f, 0.9f));
            selectedText.fontStyle = FontStyles.Italic;

            var cardsRow = new GameObject("CardsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardsRow.transform.SetParent(middleGo.transform, false);
            var cardsHlg = cardsRow.GetComponent<HorizontalLayoutGroup>();
            cardsHlg.spacing = 10f;
            cardsHlg.childAlignment = TextAnchor.MiddleCenter;
            cardsHlg.childControlWidth = false;
            cardsHlg.childControlHeight = false;
            cardsHlg.childForceExpandWidth = false;
            cardsHlg.childForceExpandHeight = false;
            
            // Dice
            var diceList = _gameManager.GameState.Get<List<object>>("action-dice");
            if (diceList != null)
            {
                for (int i = 0; i < diceList.Count; i++)
                {
                    int val = 0;
                    if (diceList[i] is double d) val = (int)d;
                    else if (diceList[i] is long l) val = (int)l;
                    else if (diceList[i] is int valInt) val = valInt;

                    int dieIndex = i;
                    bool isSelected = _gameManager.SelectedResource != null && _gameManager.SelectedResource.Type == "die" && _gameManager.SelectedResource.SourceIndex == dieIndex;
                    
                    Color dieColor = isSelected ? new Color(1f, 0.75f, 0.2f) : new Color(0.18f, 0.35f, 0.55f);
                    UIHelper.CreateButton(cardsHlg.transform, $"D{val}", dieColor, () =>
                    {
                        _gameManager.OnDieClicked(dieIndex, val);
                    }, new Vector2(60, 45));
                }
            }

            // Items
            int currentMoney = _gameManager.GameState.Get<int>("money");
            if (currentMoney > 0)
            {
                bool isSelected = _gameManager.SelectedResource != null && _gameManager.SelectedResource.Type == "item" && _gameManager.SelectedResource.ItemName == "金钱";
                Color itemColor = isSelected ? new Color(1f, 0.75f, 0.2f) : new Color(0.15f, 0.45f, 0.3f);
                UIHelper.CreateButton(cardsHlg.transform, $"金钱 x{currentMoney}", itemColor, () =>
                {
                    _gameManager.OnItemClicked("金钱", currentMoney);
                }, new Vector2(85, 45));
            }

            foreach (var kvp in _gameManager.GameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    string name = kvp.Key.Substring("item:".Length);
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        bool isSelected = _gameManager.SelectedResource != null && _gameManager.SelectedResource.Type == "item" && _gameManager.SelectedResource.ItemName == name;
                        Color itemColor = isSelected ? new Color(1f, 0.75f, 0.2f) : new Color(0.15f, 0.45f, 0.3f);
                        UIHelper.CreateButton(cardsHlg.transform, $"{name} x{qty}", itemColor, () =>
                        {
                            _gameManager.OnItemClicked(name, qty);
                        }, new Vector2(85, 45));
                    }
                }
            }

            // --- Right Section: Operations ---
            var rightGo = new GameObject("RightSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rightGo.transform.SetParent(layoutGo.transform, false);
            
            var rightVlg = rightGo.GetComponent<VerticalLayoutGroup>();
            rightVlg.spacing = 8f;
            rightVlg.childAlignment = TextAnchor.MiddleRight;
            rightVlg.childControlWidth = false;
            rightVlg.childControlHeight = false;
            rightVlg.childForceExpandWidth = false;
            rightVlg.childForceExpandHeight = false;

            string path = string.Join(" > ", _gameManager.NavigationStack.Select(n => n.Name));
            if (!string.IsNullOrEmpty(path))
            {
                UIHelper.CreateText(rightGo.transform, $"层级: {path}", 11, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Right);
            }
            else
            {
                UIHelper.CreateText(rightGo.transform, "根层级", 11, new Color(0.4f, 0.4f, 0.4f), TextAlignmentOptions.Right);
            }

            bool showReturn = (_gameManager.NavigationStack.Count > 0) || !string.IsNullOrEmpty(_gameManager.FocusedNodeName);
            if (showReturn)
            {
                string returnLabel = !string.IsNullOrEmpty(_gameManager.FocusedNodeName) ? "返回" : "返回上一级";
                UIHelper.CreateButton(rightGo.transform, returnLabel, new Color(0.2f, 0.2f, 0.25f), () =>
                {
                    _gameManager.GoBackNavigation();
                }, new Vector2(130, 26));
            }
            else
            {
                var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                spacer.transform.SetParent(rightGo.transform, false);
                var spacerRt = spacer.GetComponent<RectTransform>();
                spacerRt.sizeDelta = new Vector2(130f, 26f);
                var spacerLe = spacer.GetComponent<LayoutElement>();
                spacerLe.preferredWidth = 130f;
                spacerLe.preferredHeight = 26f;
            }

            UIHelper.CreateButton(rightGo.transform, "结束回合", new Color(0.55f, 0.15f, 0.15f), () =>
            {
                _gameManager.OnEndTurnClicked();
            }, new Vector2(130, 32));
        }

        public void BuildOverlay()
        {
            if (_overlayContainer == null) return;

            foreach (Transform child in _overlayContainer.transform)
            {
                Destroy(child.gameObject);
            }

            var activeRoll = _gameManager.ActiveRollResult;
            if (activeRoll != null)
            {
                var blocker = UIHelper.CreatePanel(_overlayContainer.transform, "Blocker", new Color(0, 0, 0, 0.6f), default, true);
                var blockerRt = blocker.GetComponent<RectTransform>();
                blockerRt.anchorMin = Vector2.zero;
                blockerRt.anchorMax = Vector2.one;
                blockerRt.sizeDelta = Vector2.zero;

                var popup = UIHelper.CreatePanel(blocker.transform, "Popup", new Color(0.1f, 0.15f, 0.2f, 0.95f));
                var popupRt = popup.GetComponent<RectTransform>();
                popupRt.anchorMin = new Vector2(0.5f, 0.5f);
                popupRt.anchorMax = new Vector2(0.5f, 0.5f);
                popupRt.pivot = new Vector2(0.5f, 0.5f);
                popupRt.anchoredPosition = Vector2.zero;
                popupRt.sizeDelta = new Vector2(320f, 220f);

                var vlgGo = UIHelper.CreateVerticalLayout(popup.transform, "PopupContent", 8f, new RectOffset(16, 16, 16, 16));
                var vlg = vlgGo.GetComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.MiddleCenter;

                var popupContentRt = vlgGo.GetComponent<RectTransform>();
                popupContentRt.anchorMin = Vector2.zero;
                popupContentRt.anchorMax = Vector2.one;
                popupContentRt.sizeDelta = Vector2.zero;

                var title = UIHelper.CreateText(vlgGo.transform, $"判定行动: {activeRoll.ActionName}", 14, Color.white);
                title.fontStyle = FontStyles.Bold;

                UIHelper.CreateText(vlgGo.transform, $"投入骰子点数: D{activeRoll.ChosenDie}", 12, new Color(0.9f, 0.9f, 0.9f));

                string randDiceText = activeRoll.RandomDice.Count > 0 
                    ? "附加掷骰: " + string.Join(", ", activeRoll.RandomDice) 
                    : "无附加掷骰 (技能等级为1)";
                UIHelper.CreateText(vlgGo.transform, randDiceText, 12, new Color(0.8f, 0.8f, 0.8f));

                UIHelper.CreateText(vlgGo.transform, $"最终最大点数: {activeRoll.FinalValue}", 14, Color.white);

                Color outcomeColor = activeRoll.Outcome == "成功" ? new Color(0.2f, 0.8f, 0.4f) 
                                   : activeRoll.Outcome == "中性" ? new Color(0.9f, 0.8f, 0.2f) 
                                   : new Color(0.9f, 0.2f, 0.2f);
                var outcomeText = UIHelper.CreateText(vlgGo.transform, $"判定结果: {activeRoll.Outcome}", 15, outcomeColor);
                outcomeText.fontStyle = FontStyles.Bold;

                UIHelper.CreateButton(vlgGo.transform, "确定", new Color(0.2f, 0.4f, 0.6f), () =>
                {
                    _gameManager.OnRollAckClicked();
                }, new Vector2(100, 30));
            }
            else if (!string.IsNullOrEmpty(_gameManager.Notification))
            {
                var toast = UIHelper.CreatePanel(_overlayContainer.transform, "Toast", new Color(0.12f, 0.12f, 0.16f, 0.9f));
                var toastRt = toast.GetComponent<RectTransform>();
                toastRt.anchorMin = new Vector2(0.5f, 0.85f);
                toastRt.anchorMax = new Vector2(0.5f, 0.85f);
                toastRt.pivot = new Vector2(0.5f, 0.5f);
                toastRt.anchoredPosition = Vector2.zero;
                toastRt.sizeDelta = new Vector2(400f, 40f);

                var toastText = UIHelper.CreateText(toast.transform, _gameManager.Notification, 12, Color.white, TextAlignmentOptions.Center);
                var ttRt = toastText.GetComponent<RectTransform>();
                ttRt.anchorMin = Vector2.zero;
                ttRt.anchorMax = Vector2.one;
                ttRt.sizeDelta = Vector2.zero;
            }
        }
    }
}
