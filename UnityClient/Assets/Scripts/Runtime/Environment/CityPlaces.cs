#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// City.fbx is the world layer: base, fill, every place's shell, far outlines and all semantic
    /// nodes (Anchor / Camera / OrbitPivot / PanBounds). Each top-level place's detail — its
    /// focus outline and interior — ships as its own asset, Resources/City/Places/&lt;名&gt;.fbx,
    /// whose root is the same node as the shell root (same name, same transform). This class
    /// instantiates those assets as siblings of their shells under City. Splitting the files is
    /// what lets later places move out of the first package; today everything is local and is
    /// attached once at startup.
    /// </summary>
    public static class CityPlaces
    {
        public const string ResourcesFolder = "City/Places/";

        /// <summary>
        /// The city base (CityBox role="base"): carries the overview camera and anchor but is the
        /// world layer itself — no shell / detail split, no outline, no detail asset.
        /// </summary>
        public const string WorldBaseName = "世界";

        /// <summary>Top-level City children that are places: they own at least one focus camera and are not the base.</summary>
        public static IEnumerable<Transform> ShellRoots(Transform cityRoot)
        {
            foreach (Transform child in cityRoot)
            {
                if (child.name == WorldBaseName)
                    continue;
                if (child.GetComponentInChildren<CinemachineVirtualCamera>(true) != null)
                    yield return child;
            }
        }

        /// <summary>Loads and attaches every place's detail asset. Missing assets are contract errors.</summary>
        public static void AttachAll(Transform cityRoot)
        {
            foreach (var shell in ShellRoots(cityRoot).ToArray())
            {
                if (IsAttached(cityRoot, shell.name))
                    continue;
                var asset = Resources.Load<GameObject>(ResourcesFolder + shell.name);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"[SSNoir] City place '{shell.name}' has no detail asset at Resources/{ResourcesFolder}{shell.name}.fbx. Rebuild and publish CityBox.");
                }
                Attach(cityRoot, shell.name, asset);
            }
        }

        public static GameObject Attach(Transform cityRoot, string placeName, GameObject asset)
        {
            var instance = UnityEngine.Object.Instantiate(asset, cityRoot);
            instance.name = placeName;
            return instance;
        }

        private static bool IsAttached(Transform cityRoot, string placeName)
        {
            int count = 0;
            foreach (Transform child in cityRoot)
                if (child.name == placeName) count++;
            return count > 1;
        }
    }
}
