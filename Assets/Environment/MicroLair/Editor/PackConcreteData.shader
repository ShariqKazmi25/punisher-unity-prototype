Shader "Hidden/MicroLair/PackConcreteData"
{
    Properties
    {
        _MainTex ("Roughness", 2D) = "white" {}
        _OcclusionTex ("Occlusion", 2D) = "white" {}
    }
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
            sampler2D _OcclusionTex;
            float4 frag(v2f_img i) : SV_Target
            {
                // URP Lit: metal in R, AO in G, smoothness in A.
                return float4(0, tex2D(_OcclusionTex, i.uv).r, 0,
                    1 - saturate(tex2D(_MainTex, i.uv).r));
            }
            ENDCG
        }
    }
}
