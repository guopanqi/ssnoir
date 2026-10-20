#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>
    /// Applies the shared CityBox/Unity outline visibility contract.
    /// This is deliberately a plain C# object: imported model assets never receive scripts.
    ///
    /// A top-level City prefab is one place. Camera_世界 shows world + always outlines; presenting
    /// any camera under a place shows that place's focus outlines plus all always outlines.
    /// World/focus outlines crossfade during presentation; shared materials are restored at endpoints.
    ///   描线_focus_&lt;名&gt;   real-model outline, visible while the place is focused
    ///   描线_world_&lt;名&gt;   optional proxy outline, visible in world view
    ///   描线_always_&lt;类&gt;  background city outline, never switched here
    ///   内部_&lt;名&gt;         focused-place interior
    ///   随卡_&lt;锚点&gt;       under 内部_: furniture that exists only while some node in the current
    ///                       render tree hangs on that anchor (bought things). See SetReferencedAnchors.
    /// </summary>
    public sealed class CityOutlineState : IDisposable
    {
        private const string CityRootName = "City";
        private const string FocusOutlinePrefix = "描线_focus_";
        private const string WorldOutlinePrefix = "描线_world_";
        private const string InteriorPrefix = "内部_";
        private const string PresencePrefix = "随卡_";

        private sealed class PrefabView
        {
            public PrefabView(string name, Renderer[] focus, Renderer? world, GameObject? interior)
            {
                Name = name;
                Focus = focus;
                World = world;
                Interior = interior;
            }

            public string Name { get; }
            public Renderer[] Focus { get; }
            public Renderer? World { get; }
            public GameObject? Interior { get; }

            public void SetWeights(float focus, float world, CityOutlineFade fade, bool entering = false)
            {
                foreach (var renderer in Focus) fade.Apply(renderer,focus);
                if (World != null) fade.Apply(World,world);
                if (Interior != null) Interior.SetActive(entering || focus>0);
            }
        }

        private sealed class Place
        {
            public Place(string name, List<PrefabView> views)
            {
                Name = name;
                Views = views;
            }

            public string Name { get; }
            public List<PrefabView> Views { get; }

            public float FocusWeight { get; private set; }
            public float WorldWeight { get; private set; } = 1;
            public void SetWeights(float focus, float world, CityOutlineFade fade, bool entering = false)
            {
                FocusWeight=focus; WorldWeight=world;
                foreach (var view in Views) view.SetWeights(focus,world,fade,entering);
            }
        }

        private readonly Dictionary<CinemachineVirtualCamera, Place> _cameraOwners = new();
        private readonly Dictionary<string, List<GameObject>> _presenceByAnchor = new(StringComparer.Ordinal);
        private Place? _activePlace;
        private readonly CityWorldVisuals _visuals;
        private readonly List<Place> _places = new();
        private readonly CityOutlineFade _fade;
        private readonly CityEnvironmentTransition _transition;
        private readonly float _duration;
#if UNITY_EDITOR
        private static readonly HashSet<CityOutlineState> EditorInstances = new();
        public static void CompleteEditorTransitions()
        {
            foreach(var instance in EditorInstances)instance._transition.Complete();
        }
#endif

        private CityOutlineState(Transform cityRoot)
        {
            var palette=CityWorldPalette.Load();
            _duration=palette.Focus.TransitionSeconds;
            _fade=new CityOutlineFade(palette.OutlineFadeShader);
            var host=new GameObject("~描线交接") { hideFlags=HideFlags.DontSave };
            host.transform.SetParent(cityRoot,false);
            _transition=host.AddComponent<CityEnvironmentTransition>();
#if UNITY_EDITOR
            EditorInstances.Add(this);
#endif
            _visuals = new CityWorldVisuals(cityRoot);
            _visuals.SetFocused(null);
            foreach (var renderer in cityRoot.GetComponentsInChildren<Renderer>(true))
            {
                bool outline = false;
                for (var node = renderer.transform; node != null && node != cityRoot; node = node.parent)
                {
                    if (node.name.StartsWith("描线_", StringComparison.Ordinal)
                        || node.name.StartsWith("线_", StringComparison.Ordinal)
                        || node.name.StartsWith("壳_", StringComparison.Ordinal))
                    {
                        outline = true;
                        break;
                    }
                }
                // 实体投影建立空间；描线不投影，避免细线把表面弄脏。
                renderer.shadowCastingMode = outline ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !outline;
            }

            int viewCount = 0;
            var byName = new Dictionary<string, List<Transform>>();
            foreach (Transform child in cityRoot)
            {
                if (!byName.TryGetValue(child.name, out var list))
                    byName[child.name] = list = new List<Transform>();
                list.Add(child);
            }
            foreach (var entry in byName)
            {
                string placeName = entry.Key;
                var roots = entry.Value;
                var descendants = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var views = new List<PrefabView>();
                foreach (var focus in descendants.Where(IsFocusOutlineRoot))
                {
                    string prefabName = focus.name.Substring(FocusOutlinePrefix.Length);
                    if (descendants.Count(t => t.name == focus.name) != 1)
                        throw ContractError($"City place '{placeName}' has more than one '{focus.name}'.");
                    if (focus.GetComponent<Renderer>() == null)
                        throw ContractError($"City outline node '{focus.name}' must contain a Renderer.");

                    string worldName = WorldOutlinePrefix + prefabName;
                    var worldMatches = descendants.Where(t => t.name == worldName).ToArray();
                    if (worldMatches.Length > 1)
                        throw ContractError($"City place '{placeName}' has more than one '{worldName}'.");
                    Renderer? world = worldMatches.Length == 1 ? worldMatches[0].GetComponent<Renderer>() : null;
                    if (worldMatches.Length == 1 && world == null)
                        throw ContractError($"City outline node '{worldName}' must contain a Renderer.");

                    var interior = descendants.FirstOrDefault(t => t.name == InteriorPrefix + prefabName);

                    var view = new PrefabView(prefabName, focus.GetComponentsInChildren<Renderer>(true), world,
                        interior != null ? interior.gameObject : null);
                    view.SetWeights(0,1,_fade);
                    views.Add(view);
                    viewCount++;
                }

                if (views.Count == 0)
                    continue;

                var place = new Place(placeName, views);
                _places.Add(place);
                foreach (var camera in roots.SelectMany(r => r.GetComponentsInChildren<CinemachineVirtualCamera>(true)))
                {
                    if (!_cameraOwners.TryAdd(camera, place))
                    {
                        throw ContractError(
                            $"City focus camera '{camera.name}' belongs to multiple places.");
                    }
                }
            }

            if (viewCount == 0)
                throw ContractError("City contains no place outlines.");

            foreach (var t in cityRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(PresencePrefix, StringComparison.Ordinal))
                    continue;
                if (t.parent == null || !t.parent.name.StartsWith(InteriorPrefix, StringComparison.Ordinal))
                    throw ContractError($"Presence node '{t.name}' must sit directly under an '{InteriorPrefix}' node.");
                string anchor = t.name.Substring(PresencePrefix.Length);
                if (anchor.Length == 0)
                    throw ContractError($"Presence node '{t.name}' names no anchor.");
                if (!_presenceByAnchor.TryGetValue(anchor, out var list))
                    _presenceByAnchor[anchor] = list = new List<GameObject>();
                list.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 随卡的件只在有卡挂着它的锚点时才在场：传入当前渲染树里全部节点的有效锚点名。
        /// 买回来的家具靠脚本多渲染一张卡就长出来，不需要另一条状态通道，读档也自然对齐。
        /// </summary>
        public void SetReferencedAnchors(ISet<string> anchors)
        {
            foreach (var entry in _presenceByAnchor)
            {
                bool present = anchors.Contains(entry.Key);
                foreach (var go in entry.Value)
                {
                    if (go.activeSelf != present)
                        go.SetActive(present);
                }
            }
        }

        /// <summary>
        /// Creates state for the unique active-scene City root. Scenes without City are valid.
        /// </summary>
        public static CityOutlineState? TryCreateFromActiveScene()
        {
            var candidates = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => string.Equals(t.name, CityRootName, StringComparison.Ordinal))
                .ToArray();

            if (candidates.Length == 0)
                return null;
            if (candidates.Length != 1)
                throw ContractError($"Expected one active-scene City root; found {candidates.Length}.");

            CityPlaces.AttachAll(candidates[0]);
            return new CityOutlineState(candidates[0]);
        }

        public bool HasOwner(CinemachineVirtualCamera? camera)
        {
            return camera != null && _cameraOwners.ContainsKey(camera);
        }

        // 冻帧捕获前保持此刻的完整世界状态，避免旧画面被目标描线/配光污染。
        public void FreezePresentation()
        {
            _transition.Cancel();
            _visuals.FreezePresentation();
        }

        public void SetFocusedCamera(CinemachineVirtualCamera? camera, float? transitionSeconds = null)
        {
            Place? next = null;
            if (camera != null)
                _cameraOwners.TryGetValue(camera, out next);

            if (next == _activePlace && transitionSeconds != 0f)
                return;

            // 重定向时采样当前权重；重复呈现同一地点不会重启过渡。
            var changing=_places.Where(p=>p.FocusWeight!=(p==next?1:0) || p.WorldWeight!=(p==next?0:1)).ToArray();
            var fromFocus=changing.Select(p=>p.FocusWeight).ToArray();
            var fromWorld=changing.Select(p=>p.WorldWeight).ToArray();
            _activePlace = next;
            _transition.Begin(transitionSeconds ?? _duration,t=> {
                // 离开的线先退（0–0.6），目标线后入（0.4–1）；两者只在中段短暂交叠。
                float outgoing=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/0.6f));
                float incoming=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-0.4f)/0.6f));
                for(int i=0;i<changing.Length;i++)
                {
                    bool entering=changing[i]==next;
                    float targetFocus=entering?1:0, targetWorld=entering?0:1;
                    changing[i].SetWeights(
                        Mathf.Lerp(fromFocus[i],targetFocus,targetFocus>fromFocus[i]?incoming:outgoing),
                        Mathf.Lerp(fromWorld[i],targetWorld,targetWorld>fromWorld[i]?incoming:outgoing),
                        _fade,entering);
                }
            },unscaled:true,smooth:false);
            _visuals.SetFocused(next?.Name, immediate: transitionSeconds == 0f);
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            EditorInstances.Remove(this);
#endif
            if(_transition!=null)_transition.Cancel();
            foreach(var place in _places)place.SetWeights(0,1,_fade);
            _fade.Dispose();
            if(_transition!=null)UnityEngine.Object.DestroyImmediate(_transition.gameObject);
            _visuals.Dispose();
        }

        private static bool IsFocusOutlineRoot(Transform transform)
        {
            if (!transform.name.StartsWith(FocusOutlinePrefix, StringComparison.Ordinal))
                return false;
            string placeName = transform.name.Substring(FocusOutlinePrefix.Length);
            return transform.parent != null && string.Equals(transform.parent.name, placeName, StringComparison.Ordinal);
        }

        private static InvalidOperationException ContractError(string message)
        {
            string full = $"[SSNoir] {message}";
            Debug.LogError(full);
            Debug.Assert(false, full);
            return new InvalidOperationException(full);
        }
    }
}
