Shader "Virexa/ZoneSurfaceShader"
{
    Properties
    {
        _MainColor ("Primary Color", Color) = (1.0, 0.55, 0.05, 0.35)
        _GlowColor ("Boundary Glow Color", Color) = (1.0, 0.75, 0.1, 0.9)
        _PatternType ("Pattern Type (0=Disposal Stripe, 1=Front Grid)", Float) = 0.0
        _PatternScale ("Pattern Scale", Float) = 0.25
        _PulseSpeed ("Pulse Speed", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        Offset -2, -2
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _MainColor;
            fixed4 _GlowColor;
            float _PatternType;
            float _PatternScale;
            float _PulseSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float pulse = 0.85 + 0.15 * sin(_Time.y * _PulseSpeed);
                float pattern = 0.0;

                if (_PatternType < 0.5)
                {
                    // ==========================================
                    // 0: DISPOSAL AREA (Diagonal Hazard Striping)
                    // ==========================================
                    float diag = (i.worldPos.x + i.worldPos.z) * _PatternScale;
                    float stripe = frac(diag);
                    float isStripe = step(0.5, stripe);
                    pattern = isStripe * 0.45;
                }
                else
                {
                    // ==========================================
                    // 1: FRONT PIT TAMBANG (Cyber High-Tech Grid)
                    // ==========================================
                    float2 gridPos = abs(frac(i.worldPos.xz * _PatternScale) - 0.5);
                    float gridLine = 1.0 - smoothstep(0.0, 0.06, min(gridPos.x, gridPos.y));
                    pattern = gridLine * 0.65;
                }

                fixed4 finalCol = _MainColor;
                finalCol.rgb += _GlowColor.rgb * pattern;
                finalCol.a = saturate(_MainColor.a + pattern * 0.35) * pulse;

                return finalCol;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
