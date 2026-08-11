#nullable enable
using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>
    /// CityBox important-building root. The body and semantic nodes always stay in City.fbx;
    /// only its inexpensive Overview outline is exchanged for the separately loaded close-up mesh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityDetailOutlineHost : MonoBehaviour
    {
        private const string DetailResourceRoot =
            "Models/Environment/CityDetailOutlines/";

        // Blender Overview preview uses the same pale-blue line colour at emission 2.6.
        // The shared close-up material is intentionally brighter (4.5), so the Overview
        // renderer receives a local override instead of duplicating the material asset.
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color OverviewEmissionColor =
            new(0.92f * 2.6f, 0.96f * 2.6f, 2.6f, 1.0f);

        [SerializeField] private string locationName = string.Empty;
        [SerializeField] private GameObject? overviewOutline;

        private GameObject? _detailOutline;
        private bool _overviewProfileApplied;

        public string LocationName => locationName;

        public void Configure(string newLocationName, GameObject newOverviewOutline)
        {
            if (string.IsNullOrWhiteSpace(newLocationName))
                throw new ArgumentException("City detail location name cannot be empty.", nameof(newLocationName));
            if (newOverviewOutline == null)
                throw new ArgumentNullException(nameof(newOverviewOutline));

            locationName = newLocationName;
            overviewOutline = newOverviewOutline;
        }

        public bool OwnsCamera(CinemachineVirtualCamera camera)
        {
            return camera != null && camera.transform.IsChildOf(transform);
        }

        public void SetDetailed(bool detailed)
        {
            ValidateConfiguration();
            ApplyOverviewVisualProfile();
            if (detailed)
                EnsureDetailOutline();

            // Never hide the Overview line until the replacement exists. A broken Resources
            // contract throws above and leaves the city visually intact for diagnosis.
            overviewOutline!.SetActive(!detailed);
            if (_detailOutline != null)
                _detailOutline.SetActive(detailed);
        }

        private void EnsureDetailOutline()
        {
            if (_detailOutline != null)
                return;

            string resourcePath = DetailResourceRoot + locationName + "_Outline";
            var detailPrefab = Resources.Load<GameObject>(resourcePath);
            if (detailPrefab == null)
                throw ContractError($"Missing CityBox detail outline Resource '{resourcePath}'.");

            var sourceFilters = detailPrefab.GetComponentsInChildren<MeshFilter>(true);
            if (sourceFilters.Length != 1 || sourceFilters[0].sharedMesh == null)
            {
                throw ContractError(
                    $"CityBox detail outline '{resourcePath}' must contain exactly one MeshFilter " +
                    $"with a mesh; found {sourceFilters.Length}.");
            }

            var overviewRenderer = overviewOutline!.GetComponent<MeshRenderer>();
            if (overviewRenderer == null)
                throw ContractError($"Overview outline '{overviewOutline.name}' has no MeshRenderer.");

            _detailOutline = new GameObject($"描线_{locationName}_Detail");
            _detailOutline.transform.SetParent(transform, false);
            _detailOutline.AddComponent<MeshFilter>().sharedMesh = sourceFilters[0].sharedMesh;

            var detailRenderer = _detailOutline.AddComponent<MeshRenderer>();
            // Reuse the imported line material, but not the Overview renderer's property block.
            // Detail therefore keeps the material's close-up emission while Overview stays at
            // the separately approved 2.6 profile.
            detailRenderer.sharedMaterials = overviewRenderer.sharedMaterials;
            detailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            detailRenderer.receiveShadows = false;
            detailRenderer.lightProbeUsage = LightProbeUsage.Off;
            detailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Debug.Log(
                $"[SSNoir] Loaded CityBox detail outline '{locationName}' " +
                $"({sourceFilters[0].sharedMesh.vertexCount} vertices).",
                this);
        }

        private void ApplyOverviewVisualProfile()
        {
            if (_overviewProfileApplied)
                return;

            var renderer = overviewOutline!.GetComponent<MeshRenderer>();
            if (renderer == null)
                throw ContractError($"Overview outline '{overviewOutline.name}' has no MeshRenderer.");

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor(EmissionColorId, OverviewEmissionColor);
            renderer.SetPropertyBlock(properties);
            _overviewProfileApplied = true;
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(locationName))
                throw ContractError("City detail host has an empty location name.");
            if (overviewOutline == null)
                throw ContractError($"City detail host '{locationName}' has no Overview outline.");
        }

        private InvalidOperationException ContractError(string message)
        {
            string full = $"[SSNoir] {message}";
            Debug.LogError(full, this);
            Debug.Assert(false, full);
            return new InvalidOperationException(full);
        }
    }

    /// <summary>Switches exactly one important building to close-up outline geometry.</summary>
    [DisallowMultipleComponent]
    public sealed class CityDetailOutlineController : MonoBehaviour
    {
        private CityDetailOutlineHost[] _hosts = Array.Empty<CityDetailOutlineHost>();
        private CityDetailOutlineHost? _activeHost;

        private void Awake()
        {
            _hosts = GetComponentsInChildren<CityDetailOutlineHost>(true);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var host in _hosts)
            {
                if (!names.Add(host.LocationName))
                    throw ContractError($"Duplicate City detail host '{host.LocationName}'.");
                host.SetDetailed(false);
            }
        }

        public void SetFocusedCamera(CinemachineVirtualCamera? camera)
        {
            CityDetailOutlineHost? next = null;
            if (camera != null)
            {
                foreach (var host in _hosts)
                {
                    if (!host.OwnsCamera(camera))
                        continue;
                    if (next != null)
                    {
                        throw ContractError(
                            $"Focus camera '{camera.name}' belongs to more than one City detail host.");
                    }
                    next = host;
                }
            }

            if (next == _activeHost)
                return;

            // Load first; if the Resources contract is broken the old visual state remains valid.
            next?.SetDetailed(true);
            _activeHost?.SetDetailed(false);
            _activeHost = next;
        }

        private InvalidOperationException ContractError(string message)
        {
            string full = $"[SSNoir] {message}";
            Debug.LogError(full, this);
            Debug.Assert(false, full);
            return new InvalidOperationException(full);
        }
    }
}
