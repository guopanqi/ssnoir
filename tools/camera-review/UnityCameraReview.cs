// 仅由 verify_unity.py 放入独立缓存工程；不进入正式游戏。
#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Cinemachine;

namespace SSNoir.Editor
{
    public static class UnityCameraReview
    {
        [Serializable] private sealed class CameraRow
        {
            public string name = "";
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
            public bool hasTravelSpace;
        }
        [Serializable] private sealed class Result
        {
            public string scope = "Imported City asset metadata and Unity/shared quaternion parity; no Play Mode";
            public CameraRow[] cameras = Array.Empty<CameraRow>();
            public float maximumRotationError;
            public int verifiedLiftFocusTargets;
        }
        public static void Verify()
        {
            string output = Environment.GetEnvironmentVariable("SSNOIR_CAMERA_REVIEW_OUTPUT")
                ?? throw new InvalidOperationException("Missing output directory");
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Environment/City.fbx");
                if (asset == null) throw new InvalidOperationException("Missing City asset");
                var instance = UnityEngine.Object.Instantiate(asset);
                try
                {
                    var lift = instance.GetComponentsInChildren<CinemachineVirtualCamera>(true)
                        .Where(c => c.name.StartsWith("Camera_联合起吊", StringComparison.Ordinal)).ToArray();
                    if (lift.Length != 4) throw new InvalidOperationException("Expected four lift cameras");
                    foreach (var camera in lift)
                    {
                        var cfg = camera.GetComponent<SSNoirVirtualCameraConfig>();
                        if (cfg == null || cfg.focusTarget == null)
                            throw new InvalidOperationException("Missing imported focus target: " + camera.name);
                        if (Vector3.Dot(camera.transform.forward, cfg.focusTarget.position-camera.transform.position) <= 0)
                            throw new InvalidOperationException("Focus target behind camera: " + camera.name);
                    }
                    var cameras = instance.GetComponentsInChildren<CinemachineVirtualCamera>(true)
                        .Where(c => c.name.StartsWith("Camera_巷战", StringComparison.Ordinal) || c.name == "Camera_巷子里在打人_VCam").ToArray();
                    if (cameras.Length != 8) throw new InvalidOperationException($"Expected 8 alley cameras, got {cameras.Length}");
                    foreach (var camera in cameras)
                    {
                        var config = camera.GetComponent<SSNoirVirtualCameraConfig>();
                        if (config == null || config.TravelVolume == null)
                            throw new InvalidOperationException($"Missing imported travel volume: {camera.name}");
                        var p = camera.transform.position;
                        if (!config.TravelVolume.Contains(new System.Numerics.Vector3(p.x,p.y,p.z)))
                            throw new InvalidOperationException($"Authored camera outside volume: {camera.name}");
                    }
                    float error = 0;
                    // Verify the managed rotation convention against Unity, including rolled authored views.
                    for (int yaw=-170; yaw<=170; yaw+=34)
                    for (int pitch=-80; pitch<=80; pitch+=20)
                    {
                        Quaternion rotation = Quaternion.Euler(pitch,yaw,17);
                        Vector3 forward = rotation * Vector3.forward;
                        Quaternion unity = Quaternion.LookRotation(forward,Vector3.up);
                        var managed = FocusTravelMath.Look(new System.Numerics.Vector3(forward.x,forward.y,forward.z));
                        error = Mathf.Max(error,Quaternion.Angle(unity,new Quaternion(managed.X,managed.Y,managed.Z,managed.W)));
                    }
                    if (error > .1f) throw new InvalidOperationException($"Managed/Unity rotation mismatch: {error}");
                    var result = new Result {verifiedLiftFocusTargets=lift.Length,maximumRotationError=error,cameras=cameras.Select(c=>new CameraRow {
                        name=c.name,position=c.transform.position,rotation=c.transform.rotation,fov=c.m_Lens.FieldOfView,hasTravelSpace=true
                    }).ToArray()};
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
                EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
