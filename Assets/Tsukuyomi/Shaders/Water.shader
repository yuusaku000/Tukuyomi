// 海・川・釣り堀の水。波のゆらぎ・空の映りこみ・太陽(夜は月)のきらめき・ツクヨミらしい光の格子。
Shader "Tsukuyomi/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.02, 0.14, 0.26, 1)
        _Shallow ("Shallow", Color) = (0.08, 0.42, 0.52, 1)
        _Scale ("Wave scale", Float) = 0.12
        _Speed ("Speed", Float) = 0.7
        _Alpha ("Alpha", Float) = 0.86
        _Flow ("Flow (xy)", Vector) = (0, 0, 0, 0)
        _Grid ("Grid", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float4 _Deep, _Shallow, _Flow;
            float _Scale, _Speed, _Alpha, _Grid;
            float4 _TsSkyTop, _TsSkyHorizon, _TsSunDir, _TsSunColor;
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

            float2 wave(float2 p, float2 dir, float f, float a, float s, float t)
            {
                float ph = dot(p, dir) * f + t * s;
                return dir * (f * a * cos(ph));
            }

            float4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.wpos.xz * _Scale + _Flow.xy * _Time.y;
                float2 g = 0;
                g += wave(p, normalize(float2(1, 0.3)), 1.0, 0.18, 1.0, t);
                g += wave(p, normalize(float2(-0.4, 1)), 1.7, 0.10, 1.3, t);
                g += wave(p, normalize(float2(0.7, -0.8)), 3.1, 0.05, 1.9, t);
                g += wave(p, normalize(float2(-1, -0.2)), 5.3, 0.025, 2.6, t);
                float3 n = normalize(float3(-g.x, 1, -g.y));
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float fres = pow(1 - saturate(dot(n, v)), 4) * 0.6 + 0.05;
                float3 r = reflect(-v, n);
                float3 sky = lerp(_TsSkyHorizon.rgb, _TsSkyTop.rgb, saturate(r.y * 2.5));
                float depthMix = saturate(0.5 + 0.5 * sin(i.wpos.x * 0.013) * sin(i.wpos.z * 0.011));
                float3 baseC = lerp(_Shallow.rgb, _Deep.rgb, depthMix);
                float3 c = lerp(baseC, sky, fres);
                float3 sd = normalize(_TsSunDir.xyz + float3(0, 0.0001, 0));
                c += pow(saturate(dot(r, sd)), 220) * _TsSunColor.rgb * 3.0;
                c += pow(saturate(dot(r, sd)), 24) * _TsSunColor.rgb * 0.15;
                // ツクヨミの水面に浮かぶ、うっすら光る格子（夜ほど見える）
                float2 cell = abs(frac(i.wpos.xz / 12.0) - 0.5);
                float gridLine = smoothstep(0.485, 0.5, max(cell.x, cell.y));
                float dist = length(_WorldSpaceCameraPos - i.wpos);
                c += float3(0.3, 0.9, 1.0) * gridLine *_Grid * (0.05 + 0.25 * _TsNight) * saturate(1 - dist / 260.0);
                float4 col = float4(c, lerp(_Alpha, 1, fres));
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
