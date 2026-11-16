// 透けて光るもの（空を泳ぐ魚・マンタ・結晶の舞台・光の橋）。ふちほど明るく、加算で重ねる。
Shader "Tsukuyomi/Holo"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.9, 1, 1)
        _Rim ("Rim power", Float) = 2.2
        _Core ("Core", Float) = 0.12
        _Day ("Day level", Float) = 0.35
        _Night ("Night level", Float) = 1.6
        _Scan ("Scan lines", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Rim, _Core, _Day, _Night, _Scan, _TsNight;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wpos : TEXCOORD1; };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float f = pow(1 - abs(dot(normalize(i.n), v)), _Rim);
                float scan = 1 + _Scan * 0.25 * sin(i.wpos.y * 6.0 - _Time.y * 3.0);
                float k = (f + _Core) * scan * lerp(_Day, _Night, _TsNight);
                return float4(_Color.rgb * k, 1);
            }
            ENDCG
        }
    }
}
