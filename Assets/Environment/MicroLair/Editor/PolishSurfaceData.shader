Shader "Hidden/MicroLair/PolishSurfaceData" {
 Properties { _MainTex("Packed data",2D)="white"{} _Lift("Lift",Float)=0.1 _Gain("Gain",Float)=1 _Max("Maximum",Float)=0.55 }
 SubShader { Cull Off ZWrite Off ZTest Always Pass {
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float _Lift,_Gain,_Max;
 float4 frag(v2f_img i):SV_Target { float4 p=tex2D(_MainTex,i.uv); p.a=min(_Max,_Lift+p.a*_Gain); return p; }
 ENDCG
 } }
}
