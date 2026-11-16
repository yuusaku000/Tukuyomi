// 板の真ん中から外へうすれていく光（月のまわりの光の輪・ライトの光）。加算で重ねる。
Shader "Tsukuyomi/Halo"
{
    Properties { _Color ("Color", Color) = (1, 0.9, 0.7, 1) _Power ("Power", Float) = 2.2 _DayK ("Day", Float) = 0.15 }
    SubShader
    {
        Tags { "Queue"="Transparent-180" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Power, _DayK, _TsNight;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2;
                float k = pow(saturate(1 - d), _Power);
                return float4(_Color.rgb * k * lerp(_DayK, 1, _TsNight), 1);
            }
            ENDCG
        }
    }
}
