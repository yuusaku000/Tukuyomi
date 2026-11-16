// 縦長の光る看板（デジタルサイネージ）。文字のような模様が流れ、ときどき光がはしる。
Shader "Tsukuyomi/Signage"
{
    Properties { _Color ("Color", Color) = (1, 0.4, 0.7, 1) _Seed ("Seed", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Seed, _TsNight;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233)) + _Seed * 7.13) * 43758.5453); }
            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                // 文字のような四角い粒（4列 × 流れる行）
                float2 g = float2(uv.x * 3.0, uv.y * 14.0 + floor(t * 1.5 + _Seed));
                float2 cell = floor(g);
                float2 f = frac(g);
                float on = step(0.45, hash(cell)) * step(0.15, f.x) * step(f.x, 0.85) * step(0.15, f.y) * step(f.y, 0.85);
                float glyph = on * (0.6 + 0.4 * step(0.5, hash(cell + floor(f * 3.0))));
                float sweep = smoothstep(0.08, 0.0, abs(frac(uv.y - t * 0.35 + _Seed) - 0.5));
                float border = step(uv.x, 0.06) + step(0.94, uv.x);
                float k = 0.25 + glyph * 0.9 + sweep * 0.8 + border * 0.6;
                float lvl = lerp(0.9, 2.6, _TsNight);
                return float4(_Color.rgb * k * lvl, 1);
            }
            ENDCG
        }
    }
}
