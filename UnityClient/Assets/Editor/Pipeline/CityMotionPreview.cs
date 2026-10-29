#if UNITY_EDITOR
#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    // 固定帧资产审阅，共用 CityWorldPreview 的正式机位/材质/输出；不驱动游戏流程。
    internal sealed class CityMotionPreview
    {
        [Serializable] private sealed class Request
        {
            public string clip = "";
            public float[] times = Array.Empty<float>();
            public string[] poses = Array.Empty<string>();
        }
        [Serializable] private sealed class Report
        {
            public string clip = "", asset = "";
            public float expectedSeconds, importedSeconds, frameRate;
            public int curveCount;
            public string[] negativeScaleChannels = Array.Empty<string>();
            public Frame[] frames = Array.Empty<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public int index;
            public float seconds;
            public string image = "";
        }
        private readonly struct Pose
        {
            private readonly Transform _transform;
            private readonly Vector3 _position, _scale;
            private readonly Quaternion _rotation;
            public Pose(Transform t)
            { _transform=t; _position=t.localPosition; _scale=t.localScale; _rotation=t.localRotation; }
            public void Restore()
            { _transform.localPosition=_position; _transform.localScale=_scale; _transform.localRotation=_rotation; }
        }
        private readonly Animation _animation;
        private readonly AnimationClip _clip;
        private readonly AnimationClip[] _poses;
        private readonly Pose[] _baseline;
        private readonly Report _report;
        private readonly string _output, _camera;
        private int _index;
        private bool _sampled;

        public CityMotionPreview(string json, string output, string camera)
        {
            var request=JsonUtility.FromJson<Request>(json);
            _output=output; _camera=camera;
            _animation=Resources.FindObjectsOfTypeAll<Animation>().Single(a=>
                a.gameObject.scene.IsValid() && a.GetClip(request.clip)!=null);
            _clip=_animation.GetClip(request.clip);
            _poses=request.poses.Select(n=>_animation.GetClip(n)
                ?? throw new InvalidOperationException("缺少准备姿态："+n)).ToArray();
            _baseline=_animation.GetComponentsInChildren<Transform>(true).Select(t=>new Pose(t)).ToArray();
            _report=Audit(_clip);
            var times=request.times.Length>0 ? request.times : new[]{0f,_clip.length/2,_clip.length};
            if(times.Any(t=>float.IsNaN(t)||float.IsInfinity(t)||t<0||t>_clip.length+0.0001f))
                throw new InvalidOperationException("采样时间必须位于片段范围内，单位为秒。");
            _report.frames=times.Select((t,i)=>new Frame {index=i,seconds=t,image=$"frame-{i:D3}.png"}).ToArray();
        }

        // 调用之间由宿主等待正常 Editor 更新，确保蒙皮使用这次采样的骨骼。
        public bool Advance(Action capture)
        {
            if(_index==_report.frames.Length)
            {
                _animation.Stop();
                File.WriteAllText(Path.Combine(_output,"motion.json"),JsonUtility.ToJson(_report,true));
                return true;
            }
            var frame=_report.frames[_index];
            if(!_sampled)
            {
                _animation.Stop();
                foreach(var p in _baseline)p.Restore();
                foreach(var pose in _poses)pose.SampleAnimation(_animation.gameObject,0);
                _animation.Play(_clip.name,PlayMode.StopAll);
                var state=_animation[_clip.name];
                state.wrapMode=WrapMode.ClampForever; state.speed=0; state.time=frame.seconds;
                _animation.Sample(); _sampled=true;
                return false;
            }
            capture();
            foreach(var ext in new[]{"png","json"})
                File.Move(Path.Combine(_output,"editor-"+_camera+"."+ext),
                    Path.Combine(_output,$"frame-{_index:D3}."+ext));
            _sampled=false; _index++;
            return false;
        }

        private static Report Audit(AnimationClip clip)
        {
            var asset=AssetDatabase.GetAssetPath(clip);
            var spec=PropClips.Parse(File.ReadAllText(Path.ChangeExtension(asset,null)+".clips.json"));
            var declared=spec.clips.Single(c=>c.name==clip.name);
            float expected=(declared.end-declared.start)/(float)spec.fps;
            if(Mathf.Abs(clip.length-expected)>1.1f/spec.fps || Mathf.Abs(clip.frameRate-spec.fps)>.01f)
                throw new InvalidOperationException($"片段范围不符：{clip.name}，声明{expected}s，导入{clip.length}s @{clip.frameRate}fps");
            var group=spec.parts.Single(p=>p.prop==declared.Prop);
            var allowed=declared.participants.Length>0 ? declared.participants : group.names;
            var parts=spec.parts.SelectMany(p=>p.names).ToArray();
            var bindings=AnimationUtility.GetCurveBindings(clip);
            var negative=new System.Collections.Generic.List<string>();
            foreach(var b in bindings)
            {
                var segments=b.path.Split('/');
                if(!segments.Any(s=>allowed.Contains(s)))
                    throw new InvalidOperationException("片段越界驱动其他对象："+b.path);
                if(b.type==typeof(Transform) && segments.Length>=2
                    && segments.Last().StartsWith("描线_",StringComparison.Ordinal)
                    && parts.Contains(segments[segments.Length-2]))
                    throw new InvalidOperationException("刚性描线含独立动画曲线："+b.path);
                if(!b.propertyName.StartsWith("m_LocalScale.",StringComparison.Ordinal))continue;
                var curve=AnimationUtility.GetEditorCurve(clip,b);
                float minimum=float.PositiveInfinity;
                int count=Math.Max(1,Mathf.CeilToInt(clip.length*120));
                for(int i=0;i<=count;i++)
                {
                    float value=curve.Evaluate(clip.length*i/count);
                    if(float.IsNaN(value)||float.IsInfinity(value))
                        throw new InvalidOperationException("非有限缩放："+b.path);
                    minimum=Math.Min(minimum,value);
                }
                // 负缩放可以是作者有意镜像，只报告，不替作者决定。
                if(minimum<-.00001f)negative.Add(b.path+"/"+b.propertyName+" min="+minimum.ToString(CultureInfo.InvariantCulture));
            }
            return new Report {clip=clip.name,asset=asset,expectedSeconds=expected,
                importedSeconds=clip.length,frameRate=clip.frameRate,curveCount=bindings.Length,
                negativeScaleChannels=negative.ToArray()};
        }
    }
}
#endif
