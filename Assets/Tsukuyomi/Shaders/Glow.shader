// 灯籠・提灯・ネオンの光る面。昼はひかえめ、夜(_TsNight=1)は明るくなってブルームで光る。
Shader "Tsukuyomi/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.6, 0.3, 1)
        _Day ("Day level", Float) = 0.7
        _Night ("Night level", Float) = 3
        _Pulse ("Pulse", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float4 _Color;
            float _Day, _Night, _Pulse;
            float _TsNight;

            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD1; UNITY_FOG_COORDS(0) };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float k = lerp(_Day, _Night, saturate(_TsNight));
                k *= 1 + _Pulse * 0.25 * sin(_Time.y * 2.5 + i.wpos.x * 0.3 + i.wpos.z * 0.2);
                float4 c = float4(_Color.rgb * k, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
