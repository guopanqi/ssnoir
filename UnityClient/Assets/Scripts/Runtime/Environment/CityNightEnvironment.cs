#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>只把 CityBox 的空间声明解释成表现。灯晕/光池批成两个网格，真实点光最多14盏。</summary>
    [ExecuteAlways]
    public sealed class CityNightEnvironment : MonoBehaviour
    {
        [Serializable] private sealed class Route { public string name=""; public Vector3[] points=Array.Empty<Vector3>(); }
        [Serializable] private sealed class Neon { public Vector3 position,size; public int palette; }
        [Serializable] private sealed class Spec {
            public int version; public string coordinates="";
            public Vector3[] lamps=Array.Empty<Vector3>(), river=Array.Empty<Vector3>();
            public Neon[] neon=Array.Empty<Neon>();
            public Route[] roads=Array.Empty<Route>();
        }
        public CityWorldOverview.NightSettings Settings { get; private set; } = null!;
        private readonly List<UnityEngine.Object> _owned=new();
        private readonly List<Moving> _moving=new();
        private Material _halo=null!, _pool=null!, _ribbon=null!;
        private float _elapsed;
        private Mesh _trails=null!;
        private Vector3[] _trailVertices=Array.Empty<Vector3>();
        private sealed class Path {
            public readonly Vector3[] Points; public readonly float[] Distance;
            public float Length => Distance[Distance.Length-1];
            public Path(Vector3[] points) {
                Points=points; Distance=new float[points.Length];
                for(int i=1;i<points.Length;i++) Distance[i]=Distance[i-1]+Vector3.Distance(points[i-1],points[i]);
                if(Length<=0) throw new InvalidOperationException("城市路径长度必须为正");
            }
            public Vector3 Sample(float distance) {
                distance=Mathf.Clamp(distance,0,Length);int i=1;
                while(i<Distance.Length-1 && Distance[i]<distance)i++;
                return Vector3.Lerp(Points[i-1],Points[i],(distance-Distance[i-1])/Mathf.Max(.001f,Distance[i]-Distance[i-1]));
            }
        }
        private sealed class Moving {
            public Transform Body=null!; public Path Path=null!;
            public float Start,Speed,Length; public int Direction; public bool Boat;
        }
        public static CityNightEnvironment Create(Transform parent,CityWorldPalette palette,Transform cityRoot) {
            var go=new GameObject("夜城环境") { hideFlags=HideFlags.DontSave };go.SetActive(false);go.transform.SetParent(parent,false);
            var instance=go.AddComponent<CityNightEnvironment>();instance.Initialize(palette,cityRoot);return instance;
        }
        private T Own<T>(T obj) where T:UnityEngine.Object { _owned.Add(obj);return obj; }
        private static Vector3 Convert(Vector3 p) {
            if(float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.z))
                throw new InvalidOperationException("城市空间声明必须使用有限坐标");
            return new Vector3(-p.x,p.z,-p.y);
        }
        private Material Glow(Shader shader,bool billboard,bool radial) {
            var m=Own(new Material(shader) { hideFlags=HideFlags.DontSave });m.SetFloat("_Billboard",billboard?1:0);
            m.SetFloat("_Radial",radial?1:0);m.SetFloat("_CityScale",transform.lossyScale.x);return m;
        }
        // Blender 河道按北→南发布；东侧为正，西侧（市中心）为负。
        // 从实际河曲线求最近点，不用 x=0 或画面左右猜岸别。
        private static (bool developed,float distance) RiverBank(Vector3 p,Vector3[] river)
        {
            float nearest=float.PositiveInfinity,side=0;
            for(int i=1;i<river.Length;i++) {
                Vector3 a=river[i-1],d=river[i]-a;a.z=0;d.z=0;
                if(d.sqrMagnitude<.0001f)continue;
                Vector3 q=p;q.z=0;Vector3 offset=q-(a+d*Mathf.Clamp01(Vector3.Dot(q-a,d)/d.sqrMagnitude));
                if(offset.sqrMagnitude>=nearest)continue;
                nearest=offset.sqrMagnitude;side=d.x*offset.y-d.y*offset.x;
            }
            if(float.IsInfinity(nearest))throw new InvalidOperationException("河道没有有效线段");
            return (side<0,Mathf.Sqrt(nearest));
        }
        private static IEnumerable<Vector3> SpacedLights(List<Vector3> candidates,int count,System.Random random)
        {
            var selected=new List<Vector3>();
            foreach(var p in candidates.OrderBy(_=>random.Next())) {
                if(selected.Count>=count)break;
                if(selected.Any(q=>Vector3.Distance(p,q)<100))continue;
                selected.Add(p);
            }
            return selected;
        }
        private float _riverHeight;
        private const float BoatWaterlineOffset = .25f;
        private void ReadRiverHeight(Transform cityRoot)
        {
            var surfaces = cityRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(mesh => mesh.name == "河面").ToArray();
            if (surfaces.Length != 1 || surfaces[0].sharedMesh == null)
                throw new InvalidOperationException($"夜城船只要求 City 下唯一的正式河面网格，找到 {surfaces.Length} 个");
            var mesh = surfaces[0];
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer == null || renderer.bounds.size.y > .01f)
                throw new InvalidOperationException("夜城船只要求水平河面");
            _riverHeight = transform.InverseTransformPoint(
                mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center)).y;
        }
        private void Initialize(CityWorldPalette palette,Transform cityRoot) {
            Settings=palette.Night;
            ReadRiverHeight(cityRoot);
            if(palette.NightGlowShader==null) throw new InvalidOperationException("夜城缺少灯晕 Shader");
            var text=Resources.Load<TextAsset>("City/World.environment") ?? throw new InvalidOperationException("请通过 CityBox 发布 World.environment.json");
            var spec=JsonUtility.FromJson<Spec>(text.text);
            if(spec.version!=1 || spec.coordinates!="BlenderWorldMetres" || spec.lamps.Length==0 || spec.roads.Length==0 || spec.river.Length<2)
                throw new InvalidOperationException("城市环境声明无效");
            if(Settings.Cars<0 || Settings.Cars>80 || Settings.Boats<0 || Settings.Boats>6 || Settings.FogFalloff<=0 || Settings.AccentPoolSize<=0 || Settings.AccentPoolGain<0 || Settings.BloomScatter<0 || Settings.BloomScatter>1)
                throw new InvalidOperationException("夜城参数超出预算或雾衰减无效");
            _halo=Glow(palette.NightGlowShader,true,true);_pool=Glow(palette.NightGlowShader,false,true);_ribbon=Glow(palette.NightGlowShader,false,false);
            var rnd=new System.Random(1104);var halo=new Quads();var pools=new Quads();
            var developed=new List<Vector3>();var poorShore=new List<Vector3>();
            if(Settings.DevelopedAccentLights<0 || Settings.PoorAccentLights<0 || Settings.DevelopedAccentLights+Settings.PoorAccentLights>14
                || Settings.DevelopedLampGain<0 || Settings.PoorLampGain<0 || Settings.PoorRiverBand<=0)
                throw new InvalidOperationException("两岸灯光参数无效或超过14盏真实点光预算");
            foreach(var position in spec.lamps) {
                var p=Convert(position);var bank=RiverBank(position,spec.river);
                float bankGain=bank.developed ? Settings.DevelopedLampGain : Settings.PoorLampGain;
                bool dead=rnd.NextDouble()<.08;float gain=dead?.12f:.65f+(float)rnd.NextDouble()*.55f;
                halo.Add(p+Vector3.up*6.8f,4.5f+(float)rnd.NextDouble()*2.5f,ColorOf("#e6d1ac",.34f*gain*Settings.LampGlow*bankGain),true);
                if(dead)continue;
                pools.Add(new Vector3(p.x,p.y+.14f,p.z),13+(float)rnd.NextDouble()*5,ColorOf("#e9c597",.07f*(.7f+(float)rnd.NextDouble()*.5f)*Settings.LampPool*bankGain),false);
                if(bank.developed)developed.Add(p);
                else if(bank.distance<=Settings.PoorRiverBand)poorShore.Add(p);
            }
            var neonColors=new[]{"#de655f","#4abdb5","#dcad58"};
            foreach(var sign in spec.neon) {
                if(sign.palette<0 || sign.palette>=neonColors.Length)throw new InvalidOperationException("未知霓虹色角色");
                var material=Own(new Material(palette.FindMaterial("主线")!) {name="夜城霓虹",hideFlags=HideFlags.DontSave});
                material.SetColor("_BaseColor",ColorOf(neonColors[sign.palette],1).linear*2.3f);
                var go=new GameObject("立面霓虹");go.transform.SetParent(transform,false);
                Box(go.transform,new Vector3(sign.size.x,sign.size.z,sign.size.y),Convert(sign.position),material);
                halo.Add(Convert(sign.position),sign.size.z*2.4f,ColorOf(neonColors[sign.palette],.24f),true);
            }
            var principal=SpacedLights(developed,Settings.DevelopedAccentLights,rnd)
                .Concat(SpacedLights(poorShore,Settings.PoorAccentLights,rnd)).ToArray();
            foreach(var p in principal) {
                // Only principal street lights have a broad pool. Road and building depth still occlude it.
                pools.Add(p+Vector3.up*.14f,Settings.AccentPoolSize,ColorOf("#eec18e",Settings.AccentPoolGain*Settings.LampPool),false);
                var go=new GameObject("街灯立面光");go.transform.SetParent(transform,false);go.transform.localPosition=p+Vector3.up*9.5f;
                var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=ColorOf("#ffa04a",1);l.intensity=6000*Settings.WarmLight; // Three PointLight uses candela, unlike Blender energy declarations.
                l.range=200*transform.lossyScale.x;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.ForcePixel;
            }
            Batch("沿街灯晕",halo,_halo);Batch("路面光池",pools,_pool);
            var roads=spec.roads.Select(r=>new Path(r.points.Select(Convert).ToArray())).ToArray();
            var river=new Path(spec.river.Select(p=>{var v=Convert(p);v.y=_riverHeight;return v;}).ToArray());
            rnd=new System.Random(7311);
            for(int i=0;i<Settings.Cars+Settings.Boats;i++) {
                bool boat=i>=Settings.Cars;var route=boat?river:roads[rnd.Next(roads.Length)];
                var go=new GameObject((boat?"船_":"车_")+i);go.transform.SetParent(transform,false);
                var body=palette.FindMaterial("M_建筑_oldtown")!;var window=palette.FindMaterial("M_窗光_white")!;
                Box(go.transform,boat?new Vector3(7,.8f,22):new Vector3(3.8f,1.4f,1.7f),Vector3.zero,body);
                Box(go.transform,boat?new Vector3(4,1.2f,8):new Vector3(2.1f,.6f,1.45f),Vector3.up*(boat?.65f:1),body);
                if(boat)Box(go.transform,new Vector3(3,.3f,1),new Vector3(0,.9f,4.1f),window);
                else {
                    Box(go.transform,new Vector3(.15f,.25f,.3f),new Vector3(1.95f,.1f,-.55f),window);
                    Box(go.transform,new Vector3(.15f,.25f,.3f),new Vector3(1.95f,.1f,.55f),window);
                }
                _moving.Add(new Moving { Body=go.transform,Path=route,Start=(float)rnd.NextDouble()*route.Length,
                    Direction=rnd.NextDouble()<.5?1:-1,Speed=boat?6+(float)rnd.NextDouble()*3:9+(float)rnd.NextDouble()*10,Length=boat?40:Settings.HeadlightRange,Boat=boat });
            }
            _trailVertices=new Vector3[_moving.Count*24];var colors=new Color[_trailVertices.Length];var triangles=new List<int>();
            for(int m=0;m<_moving.Count;m++) {
                var motion=_moving[m];var color=ColorOf(motion.Boat?"#718f9c":"#e6ebee",1).linear;
                for(int i=0;i<12;i++) {
                    color.a=(motion.Boat?Settings.WakeGain*.2f:Settings.HeadlightGain*.35f)*(1-i/11f);
                    colors[m*24+i*2]=colors[m*24+i*2+1]=color;
                    if(i<11) {int k=m*24+i*2;triangles.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}
                }
            }
            _trails=Own(new Mesh {name="汽车头灯与船尾波",hideFlags=HideFlags.DontSave});_trails.vertices=_trailVertices;_trails.colors=colors;_trails.SetTriangles(triangles,0);
            var trails=new GameObject("汽车头灯与船尾波");trails.transform.SetParent(transform,false);trails.AddComponent<MeshFilter>().sharedMesh=_trails;
            var trailRenderer=trails.AddComponent<MeshRenderer>();trailRenderer.sharedMaterial=_ribbon;trailRenderer.shadowCastingMode=ShadowCastingMode.Off;trailRenderer.receiveShadows=false;
            UpdateMotion(0);
        }
        private static Color ColorOf(string hex,float alpha) { ColorUtility.TryParseHtmlString(hex,out var c);c.a=alpha;return c; }
        private void Box(Transform parent,Vector3 size,Vector3 position,Material material) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="实体";go.transform.SetParent(parent,false);go.transform.localScale=size;go.transform.localPosition=position;
            DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private void Batch(string name,Quads data,Material mat) {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var mesh=Own(data.Mesh());
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        private void Update() { if(!Application.isPlaying || Settings==null)return;_elapsed+=Time.deltaTime;UpdateMotion(_elapsed); }
        private void UpdateMotion(float time) {
            for(int index=0;index<_moving.Count;index++) {
                var m=_moving[index];
                float travel=m.Start+time*m.Speed*(m.Boat?Settings.BoatSpeed:Settings.CarSpeed)*m.Direction;
                float phase=Mathf.Repeat(travel,m.Path.Length*2);int direction=(phase<=m.Path.Length?1:-1)*m.Direction;
                float d=Mathf.PingPong(travel,m.Path.Length);var p=m.Path.Sample(d);var ahead=m.Path.Sample(d+direction*2)-m.Path.Sample(d-direction*2);
                if(ahead.sqrMagnitude>.001f) m.Body.localRotation=Quaternion.LookRotation(ahead,Vector3.up)*(m.Boat?Quaternion.identity:Quaternion.Euler(0,-90,0));
                p.y=m.Boat?_riverHeight+BoatWaterlineOffset:1.5f;m.Body.localPosition=p;
                for(int i=0;i<12;i++) {
                    Vector3 q,side;
                    if(m.Boat) {
                        float at=d-direction*(m.Length*i/11);q=m.Path.Sample(at);q.y=_riverHeight+.03f;
                        var tangent=m.Path.Sample(at+1)-m.Path.Sample(at-1);
                        side=Vector3.Cross(tangent.normalized,Vector3.up)*1.25f;
                    } else {
                        // 头灯照向车头前方，不沿走过的道路留下拖尾。
                        float progress=i/11f;
                        var forward=m.Body.localRotation*Vector3.right;
                        q=p+forward*(2.1f+m.Length*progress);q.y=1.05f;
                        side=Vector3.Cross(forward,Vector3.up)*Mathf.Lerp(.7f,1.8f,progress);
                    }
                    _trailVertices[index*24+i*2]=q-side;_trailVertices[index*24+i*2+1]=q+side;
                }
            }
            _trails.vertices=_trailVertices;_trails.RecalculateBounds();
        }
        private void OnDestroy() { foreach(var obj in _owned)if(obj!=null)DestroyImmediate(obj); }
        private sealed class Quads {
            private readonly List<Vector3> _vertices=new();private readonly List<Vector2> _uv=new(),_size=new();
            private readonly List<Color> _colors=new();private readonly List<int> _indices=new();
            public void Add(Vector3 p,float size,Color color,bool billboard) {
                int start=_vertices.Count;
                var corners=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
                foreach(var c in corners) { _vertices.Add(billboard?p:p+new Vector3(c.x-.5f,0,c.y-.5f)*size);_uv.Add(c);_size.Add(Vector2.one*size);_colors.Add(color.linear); }
                foreach(var i in new[]{0,1,2,0,2,3})_indices.Add(start+i);
            }
            public Mesh Mesh() {
                var m=new Mesh {name="夜城灯光批次",hideFlags=HideFlags.DontSave};m.SetVertices(_vertices);m.SetUVs(0,_uv);m.SetUVs(1,_size);m.SetColors(_colors);m.SetTriangles(_indices,0);m.RecalculateBounds();
                var bounds=m.bounds;bounds.Expand(30);m.bounds=bounds;return m;
            }
        }
    }
}
