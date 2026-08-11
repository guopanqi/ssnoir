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
    /// Runtime state for the Low / High outline renderers already embedded in City.fbx.
    /// This is deliberately a plain C# object: imported model assets never receive scripts.
    /// </summary>
    public sealed class CityOutlineState
    {
        private const string CityRootName = "City";
        private const string LowSuffix = "_Low";
        private const string HighSuffix = "_High";

        private sealed class OutlinePair
        {
            public OutlinePair(string locationName, Renderer low, Renderer high)
            {
                LocationName = locationName;
                Low = low;
                High = high;
            }

            public string LocationName { get; }
            public Renderer Low { get; }
            public Renderer High { get; }

            public void SetHigh(bool high)
            {
                Low.enabled = !high;
                High.enabled = high;
            }
        }

        private readonly Dictionary<CinemachineVirtualCamera, OutlinePair> _cameraOwners = new();
        private OutlinePair? _activePair;

        private CityOutlineState(Transform cityRoot)
        {
            foreach (var renderer in cityRoot.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            int pairCount = 0;
            foreach (Transform buildingRoot in cityRoot)
            {
                string lowName = $"描线_{buildingRoot.name}{LowSuffix}";
                string highName = $"描线_{buildingRoot.name}{HighSuffix}";
                var descendants = buildingRoot.GetComponentsInChildren<Transform>(true);
                var lowMatches = descendants.Where(t => t.name == lowName).ToArray();
                var highMatches = descendants.Where(t => t.name == highName).ToArray();

                if (lowMatches.Length == 0 && highMatches.Length == 0)
                    continue;
                if (lowMatches.Length != 1 || highMatches.Length != 1)
                {
                    throw ContractError(
                        $"City building '{buildingRoot.name}' requires exactly one '{lowName}' " +
                        $"and one '{highName}'; found {lowMatches.Length} Low and " +
                        $"{highMatches.Length} High.");
                }

                var lowRenderer = lowMatches[0].GetComponent<Renderer>();
                var highRenderer = highMatches[0].GetComponent<Renderer>();
                if (lowRenderer == null || highRenderer == null)
                {
                    throw ContractError(
                        $"City building '{buildingRoot.name}' Low / High outline nodes " +
                        "must each contain a Renderer.");
                }

                var pair = new OutlinePair(buildingRoot.name, lowRenderer, highRenderer);
                pair.SetHigh(false);
                pairCount++;

                foreach (var camera in buildingRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true))
                {
                    if (!_cameraOwners.TryAdd(camera, pair))
                    {
                        throw ContractError(
                            $"City focus camera '{camera.name}' belongs to multiple buildings.");
                    }
                }
            }

            if (pairCount == 0)
                throw ContractError("City contains no Low / High outline pairs.");

            Debug.Log(
                $"[SSNoir] City outline state initialized: {pairCount} Low / High pairs, " +
                $"{_cameraOwners.Count} focus cameras.");
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

        public void SetFocusedCamera(CinemachineVirtualCamera? camera)
        {
            OutlinePair? next = null;
            if (camera != null)
                _cameraOwners.TryGetValue(camera, out next);

            if (next == _activePair)
                return;

            _activePair?.SetHigh(false);
            next?.SetHigh(true);
            _activePair = next;

            Debug.Log(next == null
                ? "[SSNoir] City outlines switched to all Low."
                : $"[SSNoir] City outline switched to High: '{next.LocationName}'.");
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
