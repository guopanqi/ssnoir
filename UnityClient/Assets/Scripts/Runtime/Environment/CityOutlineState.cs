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
    /// Runtime focus state for the places embedded in City.fbx.
    /// This is deliberately a plain C# object: imported model assets never receive scripts.
    ///
    /// One top-level City child = one place. Focusing any camera inside a place switches that
    /// whole subtree (including nested prefabs) to "focused"; everything else is in the world view.
    /// Per prefab the CityBox pipeline emits:
    ///   描线_&lt;名&gt;         standard outline (shared prop lines / figure hulls as children) — focused only
    ///   描线_&lt;名&gt;_远景    optional hand-built far outline — world view only
    ///   内部_&lt;名&gt;         optional node holding interior geometry (furniture, props, figures) — focused only
    /// Nothing else changes between the two states; the split exists purely to keep the world
    /// view cheap.
    /// </summary>
    public sealed class CityOutlineState
    {
        private const string CityRootName = "City";
        private const string OutlinePrefix = "描线_";
        private const string FarSuffix = "_远景";
        private const string InteriorPrefix = "内部_";

        private sealed class PrefabView
        {
            public PrefabView(string name, Renderer[] standard, Renderer? far, GameObject? interior)
            {
                Name = name;
                Standard = standard;
                Far = far;
                Interior = interior;
            }

            public string Name { get; }
            public Renderer[] Standard { get; }
            public Renderer? Far { get; }
            public GameObject? Interior { get; }

            public void SetFocused(bool focused)
            {
                foreach (var renderer in Standard)
                    renderer.enabled = focused;
                if (Far != null)
                    Far.enabled = !focused;
                if (Interior != null)
                    Interior.SetActive(focused);
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

            public void SetFocused(bool focused)
            {
                foreach (var view in Views)
                    view.SetFocused(focused);
            }
        }

        private readonly Dictionary<CinemachineVirtualCamera, Place> _cameraOwners = new();
        private Place? _activePlace;

        private CityOutlineState(Transform cityRoot)
        {
            foreach (var renderer in cityRoot.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            int viewCount = 0;
            foreach (Transform placeRoot in cityRoot)
            {
                var descendants = placeRoot.GetComponentsInChildren<Transform>(true);
                var views = new List<PrefabView>();
                foreach (var standard in descendants.Where(t =>
                             t.name.StartsWith(OutlinePrefix, StringComparison.Ordinal) &&
                             !t.name.EndsWith(FarSuffix, StringComparison.Ordinal)))
                {
                    string prefabName = standard.name.Substring(OutlinePrefix.Length);
                    if (descendants.Count(t => t.name == standard.name) != 1)
                        throw ContractError($"City place '{placeRoot.name}' has more than one '{standard.name}'.");
                    if (standard.GetComponent<Renderer>() == null)
                        throw ContractError($"City outline node '{standard.name}' must contain a Renderer.");

                    var farMatches = descendants.Where(t => t.name == standard.name + FarSuffix).ToArray();
                    if (farMatches.Length > 1)
                        throw ContractError($"City place '{placeRoot.name}' has more than one '{standard.name}{FarSuffix}'.");
                    Renderer? far = farMatches.Length == 1 ? farMatches[0].GetComponent<Renderer>() : null;
                    if (farMatches.Length == 1 && far == null)
                        throw ContractError($"City outline node '{farMatches[0].name}' must contain a Renderer.");

                    var interior = descendants.FirstOrDefault(t => t.name == InteriorPrefix + prefabName);

                    var view = new PrefabView(prefabName, standard.GetComponentsInChildren<Renderer>(true), far,
                        interior != null ? interior.gameObject : null);
                    view.SetFocused(false);
                    views.Add(view);
                    viewCount++;
                }

                if (views.Count == 0)
                    continue;

                var place = new Place(placeRoot.name, views);
                foreach (var camera in placeRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true))
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

            return new CityOutlineState(candidates[0]);
        }

        public bool HasOwner(CinemachineVirtualCamera? camera)
        {
            return camera != null && _cameraOwners.ContainsKey(camera);
        }

        public void SetFocusedCamera(CinemachineVirtualCamera? camera)
        {
            Place? next = null;
            if (camera != null)
                _cameraOwners.TryGetValue(camera, out next);

            if (next == _activePlace)
                return;

            _activePlace?.SetFocused(false);
            next?.SetFocused(true);
            _activePlace = next;
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
