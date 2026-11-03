// Installed only in the owned benchmark cache, never in the source Unity project.
#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    public static class AnimationImportBenchmark
    {
        [Serializable] private sealed class Request { public string[] assets = Array.Empty<string>(); public bool profileOnly; }
        [Serializable] private sealed class CurveRow
        {
            public string path = "", property = "", type = "", digest = "";
            public long offset;
            public int keys;
            public int preWrap, postWrap;
        }
        [Serializable] private sealed class ClipRow
        {
            public string name = "";
            public float length, frameRate;
            public bool legacy, loopTime;
            public int wrapMode;
            public CurveRow[] curves = Array.Empty<CurveRow>();
            public string objectCurvesAndEvents = "";
        }
        [Serializable] private sealed class Result
        {
            public string asset = "";
            public ClipRow[] clips = Array.Empty<ClipRow>();
        }
        public static void Run()
        {
            try
            {
                string output = Environment.GetEnvironmentVariable("SSNOIR_ANIMATION_REVIEW_OUTPUT")
                    ?? throw new InvalidOperationException("Missing benchmark output");
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(output, "request.json")));
                foreach (string asset in request.assets)
                {
                    var clips = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>()
                        .OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
                    if (clips.Length == 0) throw new InvalidOperationException("No clips: " + asset);
                    string prefix = Path.Combine(output, Path.GetFileNameWithoutExtension(asset));
                    var result = new Result { asset = asset };
                    if (request.profileOnly)
                    {
                        result.clips = clips.Select(clip => new ClipRow {
                            name = clip.name, length = clip.length, frameRate = clip.frameRate,
                            legacy = clip.legacy, wrapMode = (int)clip.wrapMode,
                            loopTime = AnimationUtility.GetAnimationClipSettings(clip).loopTime
                        }).ToArray();
                        File.WriteAllText(prefix + ".json", JsonUtility.ToJson(result, true));
                        continue;
                    }
                    var clipRows = new List<ClipRow>();
                    using (var stream = File.Create(prefix + ".curves.bin"))
                    using (var writer = new BinaryWriter(stream))
                    {
                        foreach (var clip in clips)
                        {
                            var settings = AnimationUtility.GetAnimationClipSettings(clip);
                            var row = new ClipRow { name = clip.name, length = clip.length, frameRate = clip.frameRate,
                                legacy = clip.legacy, loopTime = settings.loopTime, wrapMode = (int)clip.wrapMode };
                            var curves = new List<CurveRow>();
                            foreach (var binding in AnimationUtility.GetCurveBindings(clip)
                                .OrderBy(b => b.path, StringComparer.Ordinal).ThenBy(b => b.type.FullName, StringComparer.Ordinal)
                                .ThenBy(b => b.propertyName, StringComparer.Ordinal))
                            {
                                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                var keys = curve.keys;
                                var entry = new CurveRow { path = binding.path, property = binding.propertyName,
                                    type = binding.type.FullName ?? binding.type.Name, keys = keys.Length, offset = stream.Position,
                                    preWrap = (int)curve.preWrapMode, postWrap = (int)curve.postWrapMode };
                                using (var buffer = new MemoryStream())
                                {
                                    using (var values = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
                                    {
                                        for (int i = 0; i < keys.Length; i++)
                                        {
                                            var k = keys[i];
                                            values.Write(k.time); values.Write(k.value); values.Write(k.inTangent); values.Write(k.outTangent);
                                            values.Write(k.inWeight); values.Write(k.outWeight); values.Write((int)k.weightedMode);
                                            values.Write((int)AnimationUtility.GetKeyLeftTangentMode(curve, i));
                                            values.Write((int)AnimationUtility.GetKeyRightTangentMode(curve, i));
                                            values.Write(AnimationUtility.GetKeyBroken(curve, i) ? 1 : 0);
                                        }
                                    }
                                    byte[] bytes = buffer.ToArray();
                                    writer.Write(bytes);
                                    using var hash = SHA256.Create();
                                    entry.digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
                                }
                                curves.Add(entry);
                            }
                            row.curves = curves.ToArray();
                            using (var references = new MemoryStream())
                            {
                                using (var values = new BinaryWriter(references, System.Text.Encoding.UTF8, leaveOpen: true))
                                {
                                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)
                                        .OrderBy(b => b.path, StringComparer.Ordinal).ThenBy(b => b.propertyName, StringComparer.Ordinal))
                                    {
                                        values.Write(binding.path); values.Write(binding.propertyName); values.Write(binding.type.FullName ?? "");
                                        foreach (var k in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                                        {
                                            values.Write(k.time); values.Write(k.value != null ? k.value.name : "");
                                            string guid = ""; long id = 0;
                                            if (k.value != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(k.value, out guid, out id);
                                            values.Write(guid ?? ""); values.Write(id);
                                        }
                                    }
                                    foreach (var ev in AnimationUtility.GetAnimationEvents(clip))
                                    {
                                        values.Write(ev.time); values.Write(ev.functionName); values.Write(ev.stringParameter);
                                        values.Write(ev.floatParameter); values.Write(ev.intParameter);
                                        values.Write(ev.objectReferenceParameter != null ? ev.objectReferenceParameter.name : "");
                                    }
                                }
                                using var hash = SHA256.Create();
                                row.objectCurvesAndEvents = BitConverter.ToString(hash.ComputeHash(references.ToArray())).Replace("-", "");
                            }
                            clipRows.Add(row);
                        }
                    }
                    result.clips = clipRows.ToArray();
                    File.WriteAllText(prefix + ".json", JsonUtility.ToJson(result, true));
                }
                File.WriteAllText(Path.Combine(output, "completed.json"), "{\"status\":\"complete\"}");
                Debug.Log("ANIMATION_IMPORT_BENCHMARK| snapshot complete");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
