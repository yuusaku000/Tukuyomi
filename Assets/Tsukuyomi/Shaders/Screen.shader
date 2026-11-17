// 大画面（ヤチヨの配信）。うっすら走査線。霧はかけず、いつも明るく見える。
Shader "Tsukuyomi/Screen"
{
    Properties { _MainTex ("Texture", 2D) = "black" {} _Bright ("Bright", Float) = 1.25 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Bright, _TsNight;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float4 frag (v2f i) : SV_Target
            { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 c = tex2D(_MainTex, i.uv).rgb * _Bright * lerp(1.0, 1.25, _TsNight);
                c *= 0.93 + 0.07 * sin(i.uv.y * 900.0);
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
