#if UNITY_EDITOR
#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir.Editor
{
    /// <summary>从已发布的正式几何烘焙地点光照；材质使用世界基准，不生成试用材质。</summary>
    public static class CityWorldLighting
    {
        public static void BakeBanquet()
        {
            const string name = "晚宴";
            if (!Application.isBatchMode) throw new InvalidOperationException("烘焙请在独立验证工程执行，避免替换正在编辑的场景");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var city = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Models/Environment/City"));
            city.name="City";
            float scale=city.transform.lossyScale.x;
            foreach (Transform child in city.transform.Cast<Transform>().ToArray())
                if(child.name != name) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var detail = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("City/Places/"+name),city.transform);
            detail.name=name+" detail";
            var spec = JsonUtility.FromJson<CityWorldVisuals.Spec>(Resources.Load<TextAsset>("City/World.visual").text);
            var palette=CityWorldPalette.Load();
            var profile=spec.profiles.Single(p=>p.name==name);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=profile.worldLinear.gamma;RenderSettings.ambientIntensity=1;
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.reflectionIntensity=0;
            RenderSettings.fog=false;
            foreach(var light in city.GetComponentsInChildren<Light>(true))
            {
                CityWorldVisuals.Configure(light,spec.lights.Single(l=>l.name==light.name),palette,scale);
                light.enabled=true;
            }
            foreach(var renderer in detail.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool line=renderer.sharedMaterials.All(palette.IsLine);
                renderer.shadowCastingMode=line?ShadowCastingMode.Off:ShadowCastingMode.On;
                renderer.receiveShadows=!line;
                var extent=renderer.bounds.size;
                bool emitter=renderer.sharedMaterials.Any(m=>m.IsKeywordEnabled("_EMISSION"))
                    && Mathf.Min(extent.x,Mathf.Min(extent.y,extent.z))<.05f;
                if(!line && !emitter)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI);
                    renderer.receiveGI=ReceiveGI.Lightmaps;
                }
            }
            const string folder="Assets/CityLightingData/晚宴";
            Directory.CreateDirectory(folder);Directory.CreateDirectory("Assets/Resources/City/Lighting");
            var lighting=new LightingSettings {bakedGI=true,realtimeGI=false,
                lightmapper=LightingSettings.Lightmapper.ProgressiveGPU,lightmapResolution=16/scale,lightmapMaxSize=2048,
                directSampleCount=512,indirectSampleCount=128,environmentSampleCount=32,maxBounces=2};
            var settings=new SerializedObject(lighting);
            settings.FindProperty("m_IndirectOutputScale").floatValue=0;
            settings.FindProperty("m_LightmapCompression").intValue=0;
            settings.FindProperty("m_LightmapsBakeMode").intValue=(int)LightmapsMode.NonDirectional;
            settings.FindProperty("m_PVRFilteringMode").intValue=2;
            foreach(var channel in new[]{"Direct","Indirect","AO"})
                settings.FindProperty("m_PVRDenoiserType"+channel).intValue=(int)LightingSettings.DenoiserType.None;
            settings.FindProperty("m_PVRFilteringGaussRadiusDirect").intValue=2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            lighting=Save(lighting,folder+"/Lighting.asset");Lightmapping.lightingSettings=lighting;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),folder+"/Bake.unity");
            if(!Lightmapping.Bake())throw new InvalidOperationException("正式晚宴烘焙失败");
            var data=ScriptableObject.CreateInstance<CityLightmapData>();
            data.SourceHash=spec.placeHashes.Single(p=>p.name==name).hash;
            data.Lightmaps=LightmapSettings.lightmaps.Select(m=>m.lightmapColor).ToArray();
            data.Renderers=detail.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.lightmapIndex>=0 && r.lightmapIndex<data.Lightmaps.Length)
                .Select(r=>new CityLightmapData.Binding {Path=AnimationUtility.CalculateTransformPath(r.transform,detail.transform),
                    Index=r.lightmapIndex,ScaleOffset=r.lightmapScaleOffset}).ToArray();
            var shader=Shader.Find("SSNoir/City/AreaFloorHighlight") ?? throw new InvalidOperationException("缺少地面高光 Shader");
            var highlight=new Material(shader){name="晚宴地面高光"};
            highlight.SetFloat("_Roughness",1-palette.FindMaterial("M_世界_抛光地面")!.GetFloat("_Smoothness"));
            highlight.SetFloat("_Intensity",palette.FloorHighlight);
            var areas=spec.lights.Where(l=>l.profile==name&&l.type=="AREA").ToArray();
            if(areas.Length>8)throw new InvalidOperationException("地面高光最多支持八盏面光");
            for(int i=0;i<areas.Length;i++)
            {
                var row=areas[i];var light=city.GetComponentsInChildren<Light>(true).Single(l=>l.name==row.name);
                var p=light.transform.position;var n=light.transform.forward;
                highlight.SetVector("_AreaPosition"+i,new Vector4(p.x,p.y,p.z,row.size*scale*.5f));
                highlight.SetVector("_AreaNormal"+i,new Vector4(n.x,n.y,n.z,0));
                highlight.SetVector("_AreaRadiance"+i,palette.FindLightColor(row.palette)*(4*row.energy/(Mathf.PI*Mathf.PI*row.size*row.size)));
            }
            highlight=Save(highlight,folder+"/FloorHighlight.mat");data.FloorHighlight=highlight;
            data=Save(data,"Assets/Resources/City/Lighting/"+name+".asset");AssetDatabase.SaveAssets();
            Debug.Log($"[WorldLighting] {name}: {data.Lightmaps.Length} maps, {data.Renderers.Length} bindings, source {data.SourceHash}");
        }
        private static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);return existing;
        }
    }
}
#endif
