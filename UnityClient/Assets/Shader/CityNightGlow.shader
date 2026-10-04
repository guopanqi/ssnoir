Shader "SSNoir/CityNightGlow"
{
 Properties { _Billboard("Billboard",Float)=0 _Radial("Radial",Float)=1 _CityScale("City scale",Float)=1 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass {
   Blend SrcAlpha One ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vertex
   #pragma fragment Fragment
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 size:TEXCOORD1; float4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   float _Billboard,_Radial,_CityScale;
   Varyings Vertex(Attributes v) {
    Varyings o; float3 p=TransformObjectToWorld(v.positionOS.xyz);
    if(_Billboard>.5) {
     float3 right=UNITY_MATRIX_I_V._m00_m10_m20, up=UNITY_MATRIX_I_V._m01_m11_m21;
     p+=(right*(v.uv.x-.5)*v.size.x+up*(v.uv.y-.5)*v.size.y)*_CityScale;
    }
    o.positionCS=TransformWorldToHClip(p);o.uv=v.uv;o.color=v.color;return o;
   }
   half4 Fragment(Varyings v):SV_Target {
    float radius=length(v.uv*2-1);
    float alpha=_Radial>.5 ? exp(-radius*radius*7)*saturate((1-radius)*5) : 1;
    return half4(v.color.rgb,v.color.a*alpha);
   }
   ENDHLSL
  }
 }
}
