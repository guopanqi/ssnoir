#if UNITY_EDITOR
#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cinemachine;
namespace SSNoir.Editor
{
    /// <summary>只在独立缓存中导演开场试镜；正式 Main、材质与渲染，不保存场景覆盖。</summary>
    [InitializeOnLoad]
    public static class OpeningCameraReview
    {
        [Serializable] public sealed class Knot { public float time; public Vector3 position,lookAt; public float fov=27; }
        [Serializable] public sealed class Route { public float duration=18,focusAt=15; public string destination="老街酒馆"; public Knot[] knots=Array.Empty<Knot>(); }
        [Serializable] private sealed class CameraRow { public string name="";public Vector3 position,forward,up;public float fov,near,far; }
        [Serializable] private sealed class PlaceRow {public string name="";public Vector3 center,size;}
        [Serializable] private sealed class SceneInfo {public CameraRow[] cameras=Array.Empty<CameraRow>();public PlaceRow[] places=Array.Empty<PlaceRow>();}
        [Serializable] private sealed class Sample {public float time,speed,angularSpeed;public Vector3 position,lookAt;public string[] insideBounds=Array.Empty<string>();}
        [Serializable] private sealed class Result {public string source="正式 Unity Main（独立试镜，未替换开场）";public int fps,frames;public float duration,maxSpeed,maxAngularSpeed;public Sample[] samples=Array.Empty<Sample>();}
        private static string Output=>Environment.GetEnvironmentVariable("SSNOIR_OPENING_OUTPUT")!;
        private const string Pending="OpeningCameraReview.Play";
        static OpeningCameraReview()
        {
            if(Application.isBatchMode)Application.logMessageReceived+=(message,trace,type)=> {
                if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)
                {
                    SessionState.SetBool(Pending,false);EditorApplication.delayCall+=()=>EditorApplication.Exit(1);
                }
            };
            if(Application.isBatchMode && SessionState.GetBool(Pending,false))
                EditorApplication.playModeStateChanged+=mode=> {
                    if(mode==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>UnityEngine.Object.FindObjectOfType<SSNoirGameManager>().StartCoroutine(Environment.GetEnvironmentVariable("SSNOIR_OPENING_BASELINE")=="1"?Baseline():Candidate());
                };
        }
        public static void Render()
        {
            if(!Application.isBatchMode || !File.Exists(".ssnoir-world-preview"))throw new InvalidOperationException("只接受正式预览的独立缓存");
            CityOutlineEditorPreview.AutomaticPreview=false;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            if(Environment.GetEnvironmentVariable("SSNOIR_OPENING_BASELINE")=="1"
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSNOIR_OPENING_ROUTE")))
            {
                SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();return;
            }
            using var state=CityOutlineState.TryCreateFromActiveScene() ?? throw new InvalidOperationException("缺少正式 City");
            var cams=UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>(true);
            var city=UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t=>t.name=="City" && t.parent==null);
            var info=new SceneInfo {cameras=cams.OrderBy(c=>c.name).Select(c=>new CameraRow{name=c.name,position=c.transform.position,forward=c.transform.forward,up=c.transform.up,fov=c.m_Lens.FieldOfView,near=c.m_Lens.NearClipPlane,far=c.m_Lens.FarClipPlane}).ToArray(),
                places=city.Cast<Transform>().Where(t=>t.GetComponentsInChildren<Renderer>(true).Length>0).Select(t=> {
                    var renderers=t.GetComponentsInChildren<Renderer>(true);var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                    return new PlaceRow{name=t.name,center=b.center,size=b.size};}).ToArray()};
            File.WriteAllText(Path.Combine(Output,"scene.json"),JsonUtility.ToJson(info,true));
            string routeFile=Environment.GetEnvironmentVariable("SSNOIR_OPENING_ROUTE") ?? "";
            if(routeFile.Length==0){File.WriteAllText(Path.Combine(Output,"result.json"),"{\"inspected\":true}");Quit();return;}
            throw new InvalidOperationException("路线候选必须通过 Play 回放");
        }
        private static Vector3 SampleAim(Route route,float t)
        {
            int i=0;while(i<route.knots.Length-2 && t>route.knots[i+1].time)i++;
            var a=route.knots[i];var b=route.knots[i+1];var previous=route.knots[Mathf.Max(0,i-1)];var next=route.knots[Mathf.Min(route.knots.Length-1,i+2)];
            float dt=b.time-a.time,u=Mathf.InverseLerp(a.time,b.time,t),u2=u*u,u3=u2*u;
            Vector3 m0=(b.lookAt-previous.lookAt)/(b.time-previous.time),m1=(next.lookAt-a.lookAt)/(next.time-a.time);
            return (2*u3-3*u2+1)*a.lookAt+(u3-2*u2+u)*dt*m0+(-2*u3+3*u2)*b.lookAt+(u3-u2)*dt*m1;
        }
        private static float DistanceProgress(float t)
        {
            const float accelerate=.06f,decelerate=.2f;
            float area=1-(accelerate+decelerate)*.5f;
            if(t<accelerate){float u=t/accelerate;return accelerate*(u*u*u-.5f*u*u*u*u)/area;}
            if(t<1-decelerate)return (accelerate*.5f+t-accelerate)/area;
            float v=(t-1+decelerate)/decelerate;
            return (accelerate*.5f+1-decelerate-accelerate+decelerate*(v-v*v*v+.5f*v*v*v*v))/area;
        }
        private static IEnumerator Candidate()
        {
            Time.captureFramerate=int.Parse(Environment.GetEnvironmentVariable("SSNOIR_OPENING_FPS")!);
            yield return new WaitForSecondsRealtime(2.5f);
            var route=JsonUtility.FromJson<Route>(File.ReadAllText(Environment.GetEnvironmentVariable("SSNOIR_OPENING_ROUTE")!));
            if(route.knots.Length<4 || route.duration<=0 || route.focusAt<0 || route.focusAt>route.duration || route.knots[0].time!=0 || route.knots.Last().time!=1)
                throw new InvalidOperationException("路线时间或控制点无效");
            for(int i=1;i<route.knots.Length;i++)if(route.knots[i].time<=route.knots[i-1].time)throw new InvalidOperationException("注视控制点时间必须递增");
            var game=UnityEngine.Object.FindObjectOfType<SSNoirGameManager>();
            var cams=UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>(true);
            var end=cams.Single(c=>c.name=="Camera_"+route.destination+"_VCam");
            var world=cams.Single(c=>c.name=="Camera_世界_VCam");
            if(Vector3.Distance(route.knots.Last().position,end.transform.position)>.01f)throw new InvalidOperationException("候选终点未匹配正式机位");
            var trackObject=new GameObject("OpeningTrack");var track=trackObject.AddComponent<CinemachineSmoothPath>();track.m_Resolution=40;
            track.m_Waypoints=route.knots.Select(k=>new CinemachineSmoothPath.Waypoint{position=k.position}).ToArray();track.InvalidateDistanceCache();
            var aimObject=new GameObject("OpeningAim");var aim=aimObject.AddComponent<CinemachineSmoothPath>();aim.m_Waypoints=route.knots.Select(k=>new CinemachineSmoothPath.Waypoint{position=k.lookAt}).ToArray();aim.InvalidateDistanceCache();
            var source=Camera.main;var go=new GameObject("OpeningCapture",typeof(Camera));var capture=go.GetComponent<Camera>();
            CinematicCapture.ApplyRenderSettings(capture,source);capture.enabled=false;capture.orthographic=false;
            var target=CinematicCapture.CreateTarget(int.Parse(Environment.GetEnvironmentVariable("SSNOIR_OPENING_HEIGHT")!));capture.targetTexture=target;capture.aspect=16f/9;
            var palette=CityWorldPalette.Load();var addedColliders=new List<MeshCollider>();
            var city=UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t=>t.name=="City" && t.parent==null);
            foreach(var renderer in city.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled || !renderer.sharedMaterials.Any(m=>palette.Surfaces.Any(role=>role.Material==m)))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null || filter.sharedMesh==null)continue;
                var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;addedColliders.Add(collider);
            }
            Physics.SyncTransforms();
            var transitions=city.GetComponentsInChildren<CityEnvironmentTransition>(true);
            foreach(var transition in transitions)transition.enabled=false;
            int sampleFps=int.Parse(Environment.GetEnvironmentVariable("SSNOIR_OPENING_FPS")!);
            var samples=new List<Sample>();Directory.CreateDirectory(Path.Combine(Output,"frames"));int frame=0;float start=Time.unscaledTime;
            Vector3 previous=route.knots[0].position;Quaternion previousRotation=world.transform.rotation;float previousTime=0;bool focused=false;
            while(true)
            {
                if(frame>0)foreach(var transition in transitions)transition.AdvanceEditorPreview(1f/sampleFps);
                float seconds=frame/(float)sampleFps,t=Mathf.Clamp01(seconds/route.duration),path=track.ToNativePathUnits(DistanceProgress(t)*track.PathLength,CinemachinePathBase.PositionUnits.Distance);
                Vector3 position=track.EvaluatePosition(path),lookAt=SampleAim(route,t);
                Quaternion rotation=Quaternion.LookRotation(lookAt-position,Vector3.up);
                rotation=Quaternion.Slerp(world.transform.rotation,rotation,Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.08f)));
                rotation=Quaternion.Slerp(rotation,end.transform.rotation,Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.86f)/.14f)));
                if(!focused && seconds>=route.focusAt){game.PresentCamera(end);focused=true;}
                int segment=Mathf.Min(Mathf.FloorToInt(path),route.knots.Length-2);
                capture.transform.SetPositionAndRotation(position,rotation);capture.fieldOfView=Mathf.Lerp(route.knots[segment].fov,route.knots[segment+1].fov,path-segment);capture.backgroundColor=source.backgroundColor;
                capture.nearClipPlane=Mathf.Lerp(world.m_Lens.NearClipPlane,end.m_Lens.NearClipPlane,Mathf.SmoothStep(0,1,t));capture.farClipPlane=Mathf.Lerp(world.m_Lens.FarClipPlane,end.m_Lens.FarClipPlane,Mathf.SmoothStep(0,1,t));
                Vector3 delta=position-previous;float dt=Mathf.Max(.001f,seconds-previousTime);
                string[] hits=delta.magnitude<.001f?Array.Empty<string>():Physics.SphereCastAll(previous,2,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Where(h=>addedColliders.Contains(h.collider as MeshCollider)).Select(h=>h.collider.name).Distinct().ToArray();
                capture.Render();var png=CinematicCapture.ReadTarget(target);File.WriteAllBytes(Path.Combine(Output,"frames",frame.ToString("D5")+".png"),png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
                samples.Add(new Sample{time=seconds,position=position,lookAt=lookAt,speed=frame==0?0:delta.magnitude/dt,angularSpeed=frame==0?0:Quaternion.Angle(previousRotation,rotation)/dt,insideBounds=hits});
                previous=position;previousRotation=rotation;previousTime=seconds;frame++;
                if(seconds>=route.duration+.6f)break;
                yield return null;
            }
            if(Vector3.Distance(previous,end.transform.position)>.01f || Quaternion.Angle(previousRotation,end.transform.rotation)>.1f)throw new InvalidOperationException("候选落点未对齐");
            if(city.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m!=null && m.shader.name=="SSNoir/CityOutlineFade")))throw new InvalidOperationException("落点尚有残留渐变材质");
            capture.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(trackObject);UnityEngine.Object.DestroyImmediate(aimObject);
            foreach(var collider in addedColliders)UnityEngine.Object.DestroyImmediate(collider);
            foreach(var transition in transitions)transition.enabled=true;
            SessionState.SetBool(Pending,false);
            File.WriteAllText(Path.Combine(Output,"result.json"),JsonUtility.ToJson(new Result{source="正式 Unity Play 城市、固定步长离屏采样的连续运镜候选（未接入开场）",fps=sampleFps,frames=frame,duration=(frame-1f)/sampleFps,samples=samples.ToArray(),maxSpeed=samples.Max(s=>s.speed),maxAngularSpeed=samples.Max(s=>s.angularSpeed)},true));
            Time.captureFramerate=0;
            Quit();
        }
        private static IEnumerator Baseline()
        {
            yield return new WaitForSecondsRealtime(1);
            var game=UnityEngine.Object.FindObjectOfType<SSNoirGameManager>();
            bool done=false;game.StartCoroutine(BaselineTour(game,()=>done=true));
            var source=Camera.main;var go=new GameObject("OpeningBaselineCapture",typeof(Camera));var capture=go.GetComponent<Camera>();
            CinematicCapture.ApplyRenderSettings(capture,source);capture.enabled=false;
            var target=CinematicCapture.CreateTarget(int.Parse(Environment.GetEnvironmentVariable("SSNOIR_OPENING_HEIGHT")!));capture.targetTexture=target;capture.aspect=16f/9;
            var samples=new List<Sample>();Directory.CreateDirectory(Path.Combine(Output,"frames"));float start=Time.unscaledTime;int frame=0;
            while(!done)
            {
                capture.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                capture.fieldOfView=source.fieldOfView;capture.nearClipPlane=source.nearClipPlane;capture.farClipPlane=source.farClipPlane;
                capture.Render();var png=CinematicCapture.ReadTarget(target);File.WriteAllBytes(Path.Combine(Output,"frames",frame.ToString("D5")+".png"),png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
                samples.Add(new Sample{time=Time.unscaledTime-start,position=capture.transform.position,lookAt=capture.transform.position+capture.transform.forward*100});frame++;
                if(Time.unscaledTime-start>60)throw new InvalidOperationException("原版开场运镜超时");
                yield return null;
            }
            capture.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
            SessionState.SetBool(Pending,false);
            File.WriteAllText(Path.Combine(Output,"result.json"),JsonUtility.ToJson(new Result{source="正式 Unity PlayStoryCamera 原版三段运镜",frames=frame,duration=Time.unscaledTime-start,samples=samples.ToArray()},true));
            Quit();
        }
        private static IEnumerator BaselineTour(SSNoirGameManager game,Action done)
        {
            yield return game.PlayStoryCamera("格兰德酒店",.8f,"");
            yield return game.PlayStoryCamera("剧院",.8f,"");
            yield return game.PlayStoryCamera("老街酒馆",.4f,"");
            done();
        }
        private static void Quit()
        {
            int frames=0;EditorApplication.CallbackFunction? finish=null;
            finish=()=>{if(++frames<4)return;EditorApplication.update-=finish;EditorApplication.Exit(0);};
            EditorApplication.update+=finish;
        }
    }
}
#endif
