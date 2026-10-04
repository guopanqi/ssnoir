#if UNITY_EDITOR
#nullable enable
using System;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>CLI 只在独立工程读取正式 Main；不重建材质、灯光、模型或试用场景。</summary>
    [InitializeOnLoad]
    public static class CityWorldPreview
    {
        private const string Main = "Assets/Scenes/Main.unity";
        private const string Pending = "SSNoir.WorldPreview.Pending";
        private static string? _runtimeError;
        private static int _frames;
        private static int _index;
        private static bool _complete;
        private static CityOutlineState? _editorState;
        private static string Output => Environment.GetEnvironmentVariable("SSNOIR_PREVIEW_OUTPUT")
            ?? throw new InvalidOperationException("缺少预览输出目录");
        private static string[] Names => (Environment.GetEnvironmentVariable("SSNOIR_PREVIEW_SCENES")
            ?? "晚宴|别给他们想要的|老街酒馆|码头").Split('|');
        private static int Height => int.Parse(Environment.GetEnvironmentVariable("SSNOIR_PREVIEW_HEIGHT") ?? "900");
        private static bool VerifyPlay => Environment.GetEnvironmentVariable("SSNOIR_PREVIEW_PLAY") == "1";

        static CityWorldPreview()
        {
            if (Application.isBatchMode && SessionState.GetBool(Pending,false))
            {
                Application.logMessageReceived += CheckError;
                EditorApplication.playModeStateChanged += OnPlayMode;
                EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) StartCaptures(); };
            }
        }
        public static void Render()
        {
            if (!Application.isBatchMode || !File.Exists(".ssnoir-world-preview"))
                throw new InvalidOperationException("预览仅允许 CLI 创建的独立工程，不能在主工程执行");
            Application.logMessageReceived += CheckError;
            Directory.CreateDirectory(Output);
            CityOutlineEditorPreview.AutomaticPreview = false;
            EditorSceneManager.OpenScene(Main);
            _editorState = CityOutlineState.TryCreateFromActiveScene()
                ?? throw new InvalidOperationException("正式 Main 缺少 City");
            StartCaptures();
        }
        private static void CheckError(string message, string trace, LogType type)
        {
            if(type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _runtimeError=message;
        }
        private static void OnPlayMode(PlayModeStateChange mode)
        {
            if(mode == PlayModeStateChange.EnteredPlayMode) StartCaptures();
        }
        private static void StartCaptures()
        {
            _index=0;_frames=0;_complete=false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                // Give renderer/resource teardown normal editor frames before shutting down Unity jobs.
                if(_complete)
                {
                    if(++_frames<3)return;
                    EditorApplication.update-=Tick;EditorApplication.Exit(0);return;
                }
                if(_runtimeError != null) throw new InvalidOperationException("正式预览发生错误："+_runtimeError);
                // 让主相机和 URP 先经历正常帧，避免第一次附加灯阴影尚未建立。
                if(++_frames < 8) return;
                if(_index < Names.Length)
                {
                    Capture(Names[_index++]);_frames=0;return;
                }
                EditorApplication.update -= Tick;
                if(!Application.isPlaying && VerifyPlay)
                {
                    _editorState?.Dispose();
                    _editorState=null;
                    EditorSceneManager.OpenScene(Main); // 丢弃取景状态；Play 从正式 Main 原配置启动。
                    SessionState.SetBool(Pending,true);
                    EditorApplication.playModeStateChanged += OnPlayMode;
                    EditorApplication.EnterPlaymode();
                    return;
                }
                SessionState.SetBool(Pending,false);
                _editorState?.Dispose();
                _editorState=null;
                File.WriteAllText(Path.Combine(Output,"complete.json"),"{\"version\":1,\"formalMain\":true,\"playVerified\":"+(Application.isPlaying?"true":"false")+"}");
                Debug.Log("[WorldPreview] 完成："+Output);
                _complete=true;_frames=0;EditorApplication.update+=Tick;
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);EditorApplication.update -= Tick;
                SessionState.SetBool(Pending,false);EditorApplication.Exit(1);
            }
        }
        private static void Capture(string name)
        {
            var camera=UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>(true)
                .Single(v=>v.name=="Camera_"+name+"_VCam");
            if(Application.isPlaying)
                UnityEngine.Object.FindObjectOfType<SSNoirGameManager>().PresentCamera(camera);
            else _editorState!.SetFocusedCamera(camera);
            // 固定预览检查过渡的终点；实际游戏仍按色盘时间平滑过渡。
            CityWorldVisuals.CompleteEditorTransitions();
            var city=camera.transform;
            while(city.name!="City")city=city.parent ?? throw new InvalidOperationException("机位不属于 City");
            // 静态编辑器预览使用非序列化覆盖；CLI 的状态已经由正式代码控制，清除旧覆盖。
            foreach(var r in city.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
            var source=Camera.main ?? throw new InvalidOperationException("Main 缺少主相机");
            var go=new GameObject("WorldPreviewCapture",typeof(Camera));
            var capture=go.GetComponent<Camera>();var target=CinematicCapture.CreateTarget(Height);
            try
            {
                CinematicCapture.ApplyRenderSettings(capture,source);
                capture.enabled=false;capture.cameraType=CameraType.Game;
                CinematicCapture.ApplyVirtualCamera(capture,camera);
                capture.targetTexture=target;capture.aspect=16f/9;
                capture.Render();capture.Render();
                var mode=Application.isPlaying?"play":"editor";
                var png=CinematicCapture.ReadTarget(target);
                try
                {
                    File.WriteAllBytes(Path.Combine(Output,mode+"-"+name+".png"),png.EncodeToPNG());
                }
                finally {UnityEngine.Object.DestroyImmediate(png);}
                var state=new CaptureState {
                    nightEffects=CityWorldOverview.ActiveNight!=null,
                    worldEffectsWeight=CityWorldOverview.WorldWeight,
                    bloomMultiplier=CityWorldOverview.BloomMultiplier,
                    locationFog=CityWorldOverview.LocationFog,
                    locationFogColor=CityWorldOverview.LocationFogColor,
                    scene=name,mode=mode,position=capture.transform.position,rotation=capture.transform.rotation,
                    fov=capture.fieldOfView,near=capture.nearClipPlane,far=capture.farClipPlane,
                    pipeline=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(CityWorldPalette.Load().Pipeline)),
                    fog=RenderSettings.fog,fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,
                    ambient=RenderSettings.ambientLight,
                    shadowDistance=(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.shadowDistance ?? 0,
                    lights=city.GetComponentsInChildren<Light>(true).OrderBy(l=>l.name).Select(l=>new LightState {
                        name=l.name,enabled=l.enabled,type=l.type.ToString(),position=l.transform.position,rotation=l.transform.rotation,
                        color=l.color,intensity=l.intensity,range=l.range,spot=l.spotAngle}).ToArray(),
                    visible=city.GetComponentsInChildren<Renderer>(true)
                        .Where(r=>r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff)
                        .Select(r=>RelativePath(city,r.transform)).OrderBy(p=>p).ToArray(),
                    decorations=city.GetComponentsInChildren<Renderer>(true)
                        .Where(r=>r.name.Contains("Chandelier") || r.name.Contains("吊灯"))
                        .Select(r=>RelativePath(city,r.transform)+":"+string.Join(",",r.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m))))
                        .OrderBy(p=>p).ToArray(),
                    materials=city.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)
                        .Where(m=>m!=null).Select(m=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).Distinct().OrderBy(g=>g).ToArray()
                };
                File.WriteAllText(Path.Combine(Output,mode+"-"+name+".json"),JsonUtility.ToJson(state,true));
                Debug.Log("[WorldPreview] "+mode+" "+name);
            }
            finally
            {
                capture.targetTexture=null;
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
            }
        }
        private static string RelativePath(Transform root,Transform node)
        {
            string path=node.name;
            while(node.parent != root) {node=node.parent;path=node.name+"/"+path;}
            return path;
        }
        [Serializable] private sealed class CaptureState
        {
            public string scene="",mode="",pipeline="";public Vector3 position;public Quaternion rotation;
            public float fov,near,far,fogStart,fogEnd,shadowDistance;public bool fog,nightEffects;public Color ambient;
            public float worldEffectsWeight,bloomMultiplier;public Vector4 locationFog;public Color locationFogColor;
            public LightState[] lights=Array.Empty<LightState>();public string[] materials=Array.Empty<string>();
            public string[] visible=Array.Empty<string>(),decorations=Array.Empty<string>();
        }
        [Serializable] private sealed class LightState
        {
            public string name="",type="";public bool enabled;public Vector3 position;public Quaternion rotation;
            public Color color;public float intensity,range,spot;
        }
    }
}
#endif
