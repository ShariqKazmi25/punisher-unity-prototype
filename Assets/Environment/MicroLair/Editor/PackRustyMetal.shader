Shader "Hidden/MicroLair/PackRustyMetal"
{
    Properties { _MainTex ("AO Roughness Metallic", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i) : SV_Target
            {
                float3 arm = tex2D(_MainTex, i.uv).rgb;
                return float4(arm.b, arm.r, 0, 1 - arm.g);
            }
            ENDCG
        }
    }
}
