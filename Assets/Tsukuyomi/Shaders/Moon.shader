// ツクヨミの大きな月（昼はうっすら、夜ははっきり）。霧はかけない。
Shader "Tsukuyomi/Moon"
{
    Properties { _Color ("Color", Color) = (1, 0.95, 0.82, 1) }
    SubShader
    {
        Tags { "Queue"="Transparent-190" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _TsNight;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 lp : TEXCOORD1; float3 wpos : TEXCOORD2; };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal); o.lp = v.vertex.xyz;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float noise(float3 p)
            {
                float3 i = floor(p); float3 f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1,0,0)), f.x), lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x), lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 p = i.lp * 6.0;
                float m = noise(p) * 0.5 + noise(p * 2.1) * 0.3 + noise(p * 4.3) * 0.2;
                float maria = smoothstep(0.45, 0.62, noise(i.lp * 2.3 + 4.0));
                float3 c = _Color.rgb * (0.82 + 0.25 * m) * (1 - maria * 0.22);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float rim = saturate(dot(normalize(i.n), v));
                c *= 0.75 + 0.25 * rim;
                float a = lerp(0.28, 1.0, _TsNight);
                return float4(c * lerp(1.0, 1.6, _TsNight), a);
            }
            ENDCG
        }
    }
}
