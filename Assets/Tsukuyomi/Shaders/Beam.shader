// ライブの光の筋・レーザー・ペンライト。加算で重なって光る（夜はブルームでにじむ）。
//   _Fade：先へいくほど消える強さ（uv.y が根もと 0 → 先 1）
//   _Edge：幅のまんなかほど明るく（uv.x。0 なら一様）
//   _Soft：面を横から見たとき消える強さ（円すいの光の筋のふちをやわらかくする。0 ならなし）
Shader "Tsukuyomi/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Boost ("Boost", Float) = 1
        _Fade ("Fade", Float) = 1
        _Edge ("Edge", Float) = 0
        _Soft ("Soft", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float4 _Color;
            float _Boost, _Fade, _Edge, _Soft;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex);
                o.color = i.color;
                o.uv = i.uv;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = WorldSpaceViewDir(i.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float along = _Fade > 0 ? pow(saturate(1 - i.uv.y) + 0.0001, _Fade) : 1;
                float across = _Edge > 0 ? pow(saturate(1 - abs(i.uv.x * 2 - 1)) + 0.0001, _Edge) : 1;
                float soft = _Soft > 0 ? pow(abs(dot(normalize(i.n), normalize(i.v))) + 0.0001, _Soft) : 1;
                float3 c = _Color.rgb * i.color.rgb * (_Boost * along * across * soft * _Color.a * i.color.a);
                float4 r = float4(c, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, r, fixed4(0, 0, 0, 0));
                return r;
            }
            ENDCG
        }
    }
}
