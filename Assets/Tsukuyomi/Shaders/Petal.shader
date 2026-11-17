// 桜の花びら（パーティクル用）。絵を使わず、形を計算で作る。
Shader "Tsukuyomi/Petal"
{
    Properties { _Color ("Color", Color) = (1, 0.78, 0.86, 1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Color; float _TsNight;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            float4 frag (v2f i) : SV_Target
            { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 q = (i.uv - 0.5) * float2(2.0, 1.4);
                float d = length(q);
                float a = smoothstep(0.62, 0.48, d);
                a *= 1 - smoothstep(0.12, 0.0, length(i.uv - float2(0.5, 0.95))) ;
                float3 c = _Color.rgb * i.color.rgb * lerp(1.0, 1.6, _TsNight);
                float4 col = float4(c, a * i.color.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
