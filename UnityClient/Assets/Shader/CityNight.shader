Shader "SSNoir/CityNight"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        TEXTURE2D_X(_NightBloomTexture);
        TEXTURE2D_X(_NightWideBloomTexture);
        float4 _Fog, _Grade, _Bloom, _FogLow, _FogHigh, _NightBlurDirection, _NightSourceTexel;
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p) {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        float3 sampleColor(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb; }
        float3 BrightColor(float2 uv) {
            float3 c=sampleColor(uv);float l=max(c.r,max(c.g,c.b));
            return c*max(l-_Bloom.y,0)/max(l,.0001);
        }
        half4 Bright(Varyings input):SV_Target {
            // Filter source coverage before decimation so thin landmark rims do not blink out.
            float2 t=_NightSourceTexel.xy;
            float3 c=BrightColor(input.texcoord+t)+BrightColor(input.texcoord-t);
            c+=BrightColor(input.texcoord+float2(t.x,-t.y))+BrightColor(input.texcoord+float2(-t.x,t.y));
            return half4(c*.25,1);
        }
        half4 Blur(Varyings input):SV_Target {
            float2 off=_NightBlurDirection.zw*_NightBlurDirection.xy;
            float3 c=sampleColor(input.texcoord)*.227027;
            c+=(sampleColor(input.texcoord+off*1.384615)+sampleColor(input.texcoord-off*1.384615))*.316216;
            c+=(sampleColor(input.texcoord+off*3.230769)+sampleColor(input.texcoord-off*3.230769))*.070270;
            return half4(c,1);
        }
        half4 Resolve(Varyings input):SV_Target {
            float2 uv=input.texcoord;
            float3 tight=SAMPLE_TEXTURE2D_X(_NightBloomTexture,sampler_LinearClamp,uv).rgb;
            float3 wide=SAMPLE_TEXTURE2D_X(_NightWideBloomTexture,sampler_LinearClamp,uv).rgb;
            float3 c=sampleColor(uv)+(tight+wide*_Bloom.w)*_Bloom.x;
            float depth=SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
            #endif
            float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
            float3 delta=world-_WorldSpaceCameraPos;
            float dist=length(delta); float ry=delta.y/max(dist,.0001);
            // Stable form of the height-fog integral, including a nearly horizontal ray.
            float a=exp(clamp(-_WorldSpaceCameraPos.y*_Fog.y,-60,30));
            float b=exp(clamp(-world.y*_Fog.y,-60,30));
            float f1=abs(ry)<.0001 ? _Fog.x*a*dist : (_Fog.x/_Fog.y)*(a-b)/ry;
            float n=.65*noise(world.xz*.0026+_Time.y*float2(.018,.007))+.35*noise(world.xz*.009-float2(_Time.y*.03,0));
            float mist=smoothstep(.52,.95,n)*_Fog.w*exp(-max(world.y-3,0)*.11)*(1-exp(-dist*.0011));
            float fog=saturate(saturate(f1)*.9+(1-exp(-dist*_Fog.z))*.75+mist);
            c=lerp(c,lerp(_FogLow.rgb,_FogHigh.rgb,saturate(world.y/90)),fog);
            c=clamp((c-.02)*_Grade.x+.02,0,8);
            float l=dot(c,float3(.2126,.7152,.0722));
            c=lerp(l.xxx,c,_Grade.y)+float3(0,.0008,.0022)*(1-smoothstep(0,.25,l));
            float2 v=uv-.5;c*=1-_Grade.z*smoothstep(.12,.62,dot(v,v));
            float grain=frac(sin(dot(uv*_ScreenParams.xy+fmod(_Time.y,10)*61.7,float2(12.9898,78.233)))*43758.5453);
            c=max(c+(grain-.5)*_Grade.w*(1.35-l),0)*_Bloom.z;
            // ACES fitted curve in linear space; the camera target performs the sRGB conversion.
            c=saturate((c*(2.51*c+.03))/(c*(2.43*c+.59)+.14));
            return half4(c,1);
        }
        ENDHLSL
        Pass { HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Bright
        ENDHLSL }
        Pass { HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
        ENDHLSL }
        Pass { HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Resolve
        ENDHLSL }
    }
}
