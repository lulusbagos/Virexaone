Shader "Virexa/RoadGroundOverlay"
{
    Properties
    {
        _Color ("Road Color", Color) = (0.25, 0.76, 0.86, 0.82)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = _Color;
                float edge = min(input.uv.x, 1.0 - input.uv.x);
                color.a *= smoothstep(0.0, 0.10, edge);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
