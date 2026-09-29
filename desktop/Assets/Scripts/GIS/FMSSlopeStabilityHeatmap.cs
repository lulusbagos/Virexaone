using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSSlopeStabilityHeatmap : MonoBehaviour
    {
        public static FMSSlopeStabilityHeatmap Instance { get; private set; }

        [Header("State")]
        public bool isHeatmapActive = false;
        public bool showHeatmap => isHeatmapActive;
        private GameObject heatmapRoot;
        private Material heatmapMaterial;

        [Header("Statistics")]
        public float safePercent;
        public float moderatePercent;
        public float steepPercent;
        public float criticalPercent;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            InitMaterial();
        }

        private void InitMaterial()
        {
            Shader unlit = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            heatmapMaterial = new Material(unlit)
            {
                color = new Color(1f, 1f, 1f, 0.75f)
            };
        }

        public void ToggleHeatmap()
        {
            isHeatmapActive = !isHeatmapActive;

            if (isHeatmapActive)
            {
                if (Terrain.activeTerrain == null)
                {
                    isHeatmapActive = false;
                    FMSDashboardUI.Instance?.ShowNotification("Terrain belum tersedia untuk peta kemiringan.");
                    return;
                }
                if (heatmapRoot == null) BuildHeatmapMesh();
                else heatmapRoot.SetActive(true);

                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("Peta kemiringan medan aktif.");
            }
            else
            {
                if (heatmapRoot != null) heatmapRoot.SetActive(false);
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("Peta kemiringan medan ditutup.");
            }
        }

        private void BuildHeatmapMesh()
        {
            if (heatmapRoot != null) Destroy(heatmapRoot);

            heatmapRoot = new GameObject("--- GEOTECHNICAL_SLOPE_HEATMAP ---");
            heatmapRoot.transform.SetParent(this.transform);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            TerrainData tData = terrain.terrainData;
            int res = 64; // 64x64 grid for high performance
            float width = tData.size.x;
            float length = tData.size.z;

            Vector3[] vertices = new Vector3[res * res];
            Color[] colors = new Color[res * res];
            int[] triangles = new int[(res - 1) * (res - 1) * 6];
            int[] slopeBands = new int[4];

            Vector3 tPos = terrain.transform.position;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)z / (res - 1);

                    float worldX = tPos.x + u * width;
                    float worldZ = tPos.z + v * length;
                    float worldY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + tPos.y + 0.85f; // offset slightly above terrain

                    vertices[z * res + x] = new Vector3(worldX, worldY, worldZ);

                    // Calculate slope steepness from normal
                    Vector3 normal = tData.GetInterpolatedNormal(u, v);
                    float slopeAngle = Vector3.Angle(normal, Vector3.up);

                    Color c;
                    if (slopeAngle < 15f) { c = new Color(0.0f, 0.95f, 0.35f, 0.65f); slopeBands[0]++; }
                    else if (slopeAngle < 30f) { c = new Color(0.95f, 0.90f, 0.15f, 0.68f); slopeBands[1]++; }
                    else if (slopeAngle < 45f) { c = new Color(1.0f, 0.55f, 0.10f, 0.72f); slopeBands[2]++; }
                    else { c = new Color(1.0f, 0.15f, 0.15f, 0.85f); slopeBands[3]++; }

                    colors[z * res + x] = c;
                }
            }

            float sampleCount = res * res;
            safePercent = slopeBands[0] * 100f / sampleCount;
            moderatePercent = slopeBands[1] * 100f / sampleCount;
            steepPercent = slopeBands[2] * 100f / sampleCount;
            criticalPercent = slopeBands[3] * 100f / sampleCount;

            int triIndex = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int current = z * res + x;
                    int next = current + res;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Slope_Stability_Mesh",
                vertices = vertices,
                colors = colors,
                triangles = triangles
            };
            mesh.RecalculateNormals();

            MeshFilter mf = heatmapRoot.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = heatmapRoot.AddComponent<MeshRenderer>();
            mr.sharedMaterial = heatmapMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void DrawHeatmapLegendHUD(float screenW, float screenH, GUIStyle cardStyle)
        {
            if (!isHeatmapActive) return;

            float cardW = 285f;
            float cardH = 148f;
            float cardX = screenW - cardW - 14f;
            float cardY = 188f; // Below compass rose widget (52 + 128 = 180 + 8 = 188)

            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);
            GUIStyle legendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(cardX + 10, cardY + 4, cardW - 20, 18), "<b>PETA KEMIRINGAN MEDAN</b>", legendStyle);

            GUI.Label(new Rect(cardX + 10, cardY + 24, cardW - 20, 18),
                $"<b>&lt;15°:</b> <color=#00FFA3>{safePercent:F1}%</color> area sampel", legendStyle);

            GUI.Label(new Rect(cardX + 10, cardY + 44, cardW - 20, 18),
                $"<b>15°-30°:</b> <color=#FFD700>{moderatePercent:F1}%</color> area sampel", legendStyle);

            GUI.Label(new Rect(cardX + 10, cardY + 64, cardW - 20, 18),
                $"<b>30°-45°:</b> <color=#FFA500>{steepPercent:F1}%</color> area sampel", legendStyle);

            GUI.Label(new Rect(cardX + 10, cardY + 84, cardW - 20, 18),
                $"<b>&gt;45°:</b> <color=#FF4444>{criticalPercent:F1}%</color> area sampel", legendStyle);

            GUI.Label(new Rect(cardX + 10, cardY + 104, cardW - 20, 18), "Analisis lereng perlu data geoteknik.", legendStyle);

            if (GUI.Button(new Rect(cardX + cardW - 65, cardY + cardH - 26, 55, 20), "Tutup"))
            {
                ToggleHeatmap();
            }
        }
    }
}
