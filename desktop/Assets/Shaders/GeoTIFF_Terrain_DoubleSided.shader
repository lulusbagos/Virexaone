Shader "Virexa/GeoTIFF_Terrain_DoubleSided"
{
    Properties
    {
        _MainTex ("GeoTIFF Orthophoto (RGB)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.05
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off // No backface culling - visible from every angle!

        CGPROGRAM
        #pragma surface surf Lambert addshadow

        sampler2D _MainTex;
        fixed4 _Color;
        half _Brightness;
        half _Contrast;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Subtle contrast & brightness enhancement for mining aerial imagery
            half3 enhanced = (c.rgb - 0.5) * _Contrast + 0.5;
            o.Albedo = saturate(enhanced * _Brightness);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
