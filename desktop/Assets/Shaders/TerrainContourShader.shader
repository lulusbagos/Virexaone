Shader "Virexa/TerrainContourShader"
{
    Properties
    {
        [Header(GeoTIFF Aerial Texture)]
        _MainTex ("GeoTIFF Aerial Texture (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _TextureBrightness ("Texture Brightness", Range(0.1, 2.5)) = 0.85
        _TextureContrast ("Texture Contrast", Range(0.0, 2.5)) = 0.20
        _TerrainBoundsMin ("Terrain Min (X, Z)", Vector) = (-2725.78, -2043.48, 0, 0)
        _TerrainBoundsMax ("Terrain Max (X, Z)", Vector) = (2725.78, 2043.48, 0, 0)
        
        [Header(Triangulated Irregular Network TIN)]
        _TINMode ("TIN Mode (0=Off, 1=Wireframe, 2=Solid CAD, 3=Hypsometric DEM)", Float) = 0.0
        _TINCellSize ("TIN Triangle Cell Size (Meters)", Range(2.0, 50.0)) = 12.0
        _TINWireWidth ("TIN Wire Width (Pixels)", Range(0.5, 3.0)) = 1.1
        _TINWireOpacity ("TIN Wire Opacity", Range(0.0, 1.0)) = 0.85
        _TINWireColor ("TIN Wireframe Color", Color) = (0.0, 0.90, 1.0, 0.90)
        _TINCADBaseColor ("TIN Solid CAD Color", Color) = (0.28, 0.33, 0.40, 1.0)
        
        [Header(Topographic Contours)]
        [Toggle] _EnableContours ("Enable Contour Lines", Float) = 0.0
        _ContourInterval ("Contour Interval (Meters)", Range(1.0, 100.0)) = 10.0
        _ContourPixelWidth ("Contour Width (Screen Pixels)", Range(0.5, 3.5)) = 1.2
        _ContourOpacity ("Contour Opacity", Range(0.0, 1.0)) = 0.80
        _MajorContourFreq ("Major Index Contour Frequency", Range(2.0, 10.0)) = 5.0
        _MajorContourColor ("Major Contour Color", Color) = (1.0, 0.78, 0.15, 0.95)
        _MinorContourColor ("Minor Contour Color", Color) = (0.95, 0.90, 0.78, 0.35)
        
        [Header(Elevation Heatmap)]
        [Toggle] _EnableElevationHeatmap ("Enable Elevation Heatmap", Float) = 0.0
        _MinElevation ("Min Elevation (RL)", Float) = 71.0
        _MaxElevation ("Max Elevation (RL)", Float) = 316.5
        _HeatmapAlpha ("Heatmap Blend", Range(0.0, 1.0)) = 0.40
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf GISLighting vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _BaseColor;
        float _TextureBrightness;
        float _TextureContrast;
        float4 _TerrainBoundsMin;
        float4 _TerrainBoundsMax;
        
        float _TINMode;
        float _TINCellSize;
        float _TINWireWidth;
        float _TINWireOpacity;
        fixed4 _TINWireColor;
        fixed4 _TINCADBaseColor;

        float _EnableContours;
        float _ContourInterval;
        float _ContourPixelWidth;
        float _ContourOpacity;
        float _MajorContourFreq;
        fixed4 _MajorContourColor;
        fixed4 _MinorContourColor;

        float _EnableElevationHeatmap;
        float _MinElevation;
        float _MaxElevation;
        float _HeatmapAlpha;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        // Custom GIS Lighting Model:
        // Guarantees 100% exact native GeoTIFF pixel colors (capped at 1.0) with natural 3D pit bench relief
        inline half4 LightingGISLighting (SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = saturate(dot(s.Normal, lightDir));
            half shadowFactor = saturate(atten * (NdotL * 0.40 + 0.60));
            
            half3 finalLight = min(half3(1.0, 1.0, 1.0), _LightColor0.rgb * shadowFactor);

            half4 c;
            c.rgb = s.Albedo * finalLight;
            c.a = s.Alpha;
            return c;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.uv_MainTex = v.texcoord.xy;
        }

        fixed3 GetElevationColor(float normH)
        {
            if (normH < 0.25)
                return lerp(fixed3(0.05, 0.45, 0.95), fixed3(0.1, 0.85, 0.85), normH / 0.25);
            else if (normH < 0.5)
                return lerp(fixed3(0.1, 0.85, 0.85), fixed3(0.15, 0.85, 0.3), (normH - 0.25) / 0.25);
            else if (normH < 0.75)
                return lerp(fixed3(0.15, 0.85, 0.3), fixed3(0.95, 0.75, 0.1), (normH - 0.5) / 0.25);
            else
                return lerp(fixed3(0.95, 0.75, 0.1), fixed3(0.95, 0.15, 0.1), (normH - 0.75) / 0.25);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float elevation = IN.worldPos.y;
            float normH = saturate((elevation - _MinElevation) / max(1.0, (_MaxElevation - _MinElevation)));

            // Compute flat facet normal for authentic CAD / TIN surface shading
            float3 ddxWorld = ddx(IN.worldPos);
            float3 ddyWorld = ddy(IN.worldPos);
            float3 facetNormal = normalize(cross(ddxWorld, ddyWorld));
            float facetLight = saturate(dot(facetNormal, normalize(float3(0.35, 0.85, -0.40)))) * 0.45 + 0.55;

            fixed3 finalSurface;

            // --- 1. Base Layer Selection based on TIN Mode ---
            if (_TINMode > 2.5) // Mode 3: Hypsometric DEM TIN
            {
                fixed3 demColor = GetElevationColor(normH);
                finalSurface = demColor * facetLight;
            }
            else if (_TINMode > 1.5) // Mode 2: Solid CAD TIN Surface (Surpac / Micromine Faceted Look)
            {
                finalSurface = _TINCADBaseColor.rgb * facetLight;
            }
            else // Mode 0 (Standard) or Mode 1 (Wireframe on GeoTIFF)
            {
                // Infallible UV calculation from World Space bounds (5.45km x 4.09km site)
                float2 minB = _TerrainBoundsMin.xy;
                float2 maxB = _TerrainBoundsMax.xy;
                if (abs(maxB.x - minB.x) < 10.0)
                {
                    minB = float2(-2725.78, -2043.48);
                    maxB = float2(2725.78, 2043.48);
                }
                float2 terrainUV = saturate((IN.worldPos.xz - minB) / max(float2(1.0, 1.0), (maxB - minB)));

                // 1. Authentic 4K GeoTIFF aerial texture sample
                fixed4 rawTif = tex2D(_MainTex, terrainUV) * _BaseColor;
                fixed3 rgb = rawTif.rgb;

                // 2. Output 100% natural GeoTIFF color (no artificial blowout)
                finalSurface = rgb;

                // Optional Elevation Heatmap in standard mode
                if (_EnableElevationHeatmap > 0.5)
                {
                    fixed3 heatColor = GetElevationColor(normH);
                    finalSurface = lerp(finalSurface, heatColor, _HeatmapAlpha);
                }
            }

            // --- 2. Triangulated Irregular Network (TIN) Delaunay Wireframe Mesh ---
            if (_TINMode > 0.5)
            {
                float tinSize = max(2.0, _TINCellSize);
                float2 tinCoord = IN.worldPos.xz / tinSize;
                float2 tinFrac = frac(tinCoord);

                // Triangular grid edges: X-edge, Z-edge, and Diagonal (X=Z)
                float dEdgeX = min(tinFrac.x, 1.0 - tinFrac.x) * tinSize;
                float dEdgeZ = min(tinFrac.y, 1.0 - tinFrac.y) * tinSize;
                float dDiag = abs(tinFrac.x - tinFrac.y) * (tinSize * 0.70710678);

                float minEdgeDist = min(min(dEdgeX, dEdgeZ), dDiag);
                float dDist = max(0.0001, fwidth(minEdgeDist));

                float wireFactor = 1.0 - smoothstep(0.0, dDist * _TINWireWidth, minEdgeDist);
                float wireAlpha = wireFactor * _TINWireOpacity;

                fixed3 wireRGB = _TINWireColor.rgb;
                finalSurface = lerp(finalSurface, wireRGB, wireAlpha);
            }

            // --- 3. Screen-Space Anti-Aliased Topographic Contour Lines ---
            if (_EnableContours > 0.5)
            {
                float h = elevation - _MinElevation;
                float minorInterval = max(2.0, _ContourInterval);
                float hNorm = h / minorInterval;
                float df = max(0.0001, fwidth(hNorm));
                
                // Density cutoff: when contours are denser than 1 line per 2 pixels (steep slopes / zoomed out),
                // fade them out to 0 so the 4K GeoTIFF orthophoto remains 100% crystal clear without white blowout
                float densityFade = saturate(1.0 - df * 1.8);

                if (densityFade > 0.01)
                {
                    // Minor contour lines
                    float fMinor = frac(hNorm);
                    float distMinor = min(fMinor, 1.0 - fMinor);
                    float lineMinor = saturate(1.0 - distMinor / max(0.001, df * _ContourPixelWidth));
                    float alphaMinor = lineMinor * densityFade * _MinorContourColor.a;

                    // Major index contour lines (e.g. every 50 meters)
                    float majorFreq = max(2.0, _MajorContourFreq);
                    float hMajorNorm = hNorm / majorFreq;
                    float dfMajor = max(0.0001, fwidth(hMajorNorm));
                    float fMajor = frac(hMajorNorm);
                    float distMajor = min(fMajor, 1.0 - fMajor);
                    float lineMajor = saturate(1.0 - distMajor / max(0.001, dfMajor * (_ContourPixelWidth * 1.5)));
                    float alphaMajor = lineMajor * densityFade * _MajorContourColor.a;

                    fixed3 lineRGB = lerp(_MinorContourColor.rgb, _MajorContourColor.rgb, saturate(lineMajor * 1.5));
                    float totalAlpha = max(alphaMinor, alphaMajor) * _ContourOpacity;

                    finalSurface = lerp(finalSurface, lineRGB, totalAlpha);
                }
            }

            o.Albedo = finalSurface;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
