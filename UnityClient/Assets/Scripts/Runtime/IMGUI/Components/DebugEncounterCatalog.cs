#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// Test menu catalog: all story encounters, plus explicitly selected research.
    /// DemoEncounters supplies familiar labels and ordering, not story membership.
    /// Research and experimental scenes are excluded from automatic discovery.
    /// </summary>
    public static class DebugEncounterCatalog
    {
        [Serializable]
        private sealed class Manifest
        {
            public Encounter[] encounters = Array.Empty<Encounter>();
        }

        [Serializable]
        private sealed class Encounter
        {
            public string label = "";
            public string scene = "";
        }

        public readonly struct Entry
        {
            public readonly string Label;
            public readonly string Scene;
            public readonly bool Research;

            public Entry(string label, string scene, bool research)
            {
                Label = label;
                Scene = scene;
                Research = research;
            }
        }

        public static List<Entry> Load()
        {
            var result = new List<Entry>();
            var included = new HashSet<string>(StringComparer.Ordinal);
            Append("DemoEncounters", false, included, result);
            var scenes = Resources.LoadAll<TextAsset>("Content/scenes/encounters");
            Array.Sort(scenes, (left, right) => StringComparer.Ordinal.Compare(left.name, right.name));
            foreach (var asset in scenes)
            {
                var scene = asset.name;
                if (scene.StartsWith("研究", StringComparison.Ordinal)
                    || scene.StartsWith("实验", StringComparison.Ordinal)
                    || !included.Add(scene))
                    continue;
                result.Add(new Entry(scene, scene, false));
            }
            Append("DebugResearchPlaytests", true, included, result);
            return result;
        }

        private static void Append(string resource, bool research,
            HashSet<string> included, List<Entry> result)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Debug.LogError($"Debug encounter allowlist '{resource}.json' is missing. " +
                    "Failing closed: unapproved encounters will not appear.");
                return;
            }

            Manifest? manifest;
            try
            {
                manifest = JsonUtility.FromJson<Manifest>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Invalid debug encounter allowlist '{resource}': {exception.Message}");
                return;
            }

            if (manifest?.encounters == null)
                return;

            foreach (var item in manifest.encounters)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.scene))
                {
                    Debug.LogWarning($"Invalid entry in debug encounter allowlist '{resource}'.");
                    continue;
                }

                // Validation at runtime as well as CI: no stale or renamed buttons.
                var scene = item.scene.Trim();
                if (Resources.Load<TextAsset>("Content/scenes/encounters/" + scene) == null)
                {
                    Debug.LogWarning($"Debug encounter '{scene}' is approved but the scene is missing.");
                    continue;
                }

                if (!included.Add(scene))
                {
                    Debug.LogWarning($"Duplicate debug encounter '{scene}' in '{resource}'.");
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(item.label) ? scene : item.label.Trim();
                result.Add(new Entry(label, scene, research));
            }
        }
    }
}
