// 星空（夜だけ見える）と、ツクヨミの大きな月・月の光の輪。
Shader "Tsukuyomi/Stars"
{
    Properties { _Amount ("Amount", Float) = 0.985 }
    SubShader
    {
        Tags { "Queue"="Transparent-200" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Amount, _TsNight;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(v.vertex); o.dir = normalize(v.vertex.xyz); return o; }
            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float4 frag (v2f i) : SV_Target
            { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float3 c = 0;
                for (int k = 0; k < 2; k++)
                {
                    float sc = k == 0 ? 90.0 : 190.0;
                    float3 p = d * sc;
                    float3 cell = floor(p);
                    float h = hash(cell);
                    float3 off = float3(hash(cell + 1.7), hash(cell + 3.1), hash(cell + 5.3)) - 0.5;
                    float dd = length(frac(p) - 0.5 - off * 0.6);
                    float s = step(_Amount, h) * smoothstep(0.09, 0.0, dd);
                    float tw = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 4.0) + h * 60.0);
                    float3 tint = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.85, 0.95), hash(cell + 9.0));
                    c += s * tw * tint * (k == 0 ? 2.2 : 1.1);
                }
                float horizon = saturate(d.y * 4.0 + 0.1);
                return float4(c * _TsNight * horizon, 1);
            }
            ENDCG
        }
    }
}
