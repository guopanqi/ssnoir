Shader "SSNoir/PreviewComposite"
{
    Properties { _MainTex ("Stage", 2D) = "white" {} }
    SubShader { Pass {
        ZWrite Off ZTest Always Cull Off Blend Off
        HLSLPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _PreviewBackground;
        float _PreviewFade;
        float4 frag(v2f_img input) : SV_Target
        {
            float4 stage = tex2D(_MainTex, input.uv);
            float alpha = stage.a * _PreviewFade;
            return float4(stage.rgb * alpha + _PreviewBackground.rgb * (1-alpha), 1);
        }
        ENDHLSL
    } }
}
