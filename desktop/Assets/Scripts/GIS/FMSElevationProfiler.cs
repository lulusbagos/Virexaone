using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSElevationProfiler : MonoBehaviour
    {
        public static FMSElevationProfiler Instance { get; private set; }

        public struct ProfileSample
        {
            public float distanceAlongRoute;
            public float elevationRL;
            public Vector3 worldPos;
            public float gradePercent;
        }

        [Header("State")]
        public bool isProfilerOpen = false;
        public string profileTitle = "Profil Potongan Melintang Jalan (Hauling Cross-Section)";
        public List<ProfileSample> samples = new List<ProfileSample>();

        // Statistics
        public float totalRouteDistance = 0f;
        public float minRL = 0f;
        public float maxRL = 0f;
        public float totalAscent = 0f;
        public float totalDescent = 0f;
        public float maxSlopeGrade = 0f;
        public float avgSlopeGrade = 0f;

        // Hover tracking
        public int hoveredSampleIndex = -1;
        private GameObject targetMarker3D;
        private Material markerMat;

        // Textures for graph rendering
        private Texture2D graphBgTex;
        private Texture2D gridLineTex;
        private Texture2D curveGreenTex;
        private Texture2D curveYellowTex;
        private Texture2D curveRedTex;
        private Texture2D cursorLineTex;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            InitTextures();
            CreateHoverMarker();
        }

        private void InitTextures()
        {
            graphBgTex = MakeSolidTex(2, 2, new Color(0.04f, 0.07f, 0.12f, 0.94f));
            gridLineTex = MakeSolidTex(2, 2, new Color(0.2f, 0.35f, 0.5f, 0.25f));
            curveGreenTex = MakeSolidTex(2, 2, new Color(0.0f, 0.95f, 0.6f, 0.95f)); // Safe < 8%
            curveYellowTex = MakeSolidTex(2, 2, new Color(1.0f, 0.85f, 0.1f, 0.95f)); // Caution 8-12%
            curveRedTex = MakeSolidTex(2, 2, new Color(1.0f, 0.25f, 0.25f, 0.95f)); // Critical > 12%
            cursorLineTex = MakeSolidTex(2, 2, new Color(0.0f, 0.95f, 1.0f, 0.95f));
        }

        private Texture2D MakeSolidTex(int w, int h, Color col)
        {
            Texture2D tex = new Texture2D(w, h);
            Color[] cols = new Color[w * h];
            for (int i = 0; i < cols.Length; i++) cols[i] = col;
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void CreateHoverMarker()
        {
            if (targetMarker3D == null)
            {
                targetMarker3D = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                targetMarker3D.name = "--- Profiler_3D_Cursor_Marker ---";
                targetMarker3D.transform.SetParent(this.transform);
                targetMarker3D.transform.localScale = new Vector3(8f, 8f, 8f);

                Shader unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                markerMat = new Material(unlit) { color = new Color(0.0f, 0.95f, 1.0f, 0.95f) };
                if (targetMarker3D.GetComponent<Renderer>() != null)
                    targetMarker3D.GetComponent<Renderer>().sharedMaterial = markerMat;

                Collider c = targetMarker3D.GetComponent<Collider>();
                if (c != null) Destroy(c);
                targetMarker3D.SetActive(false);
            }
        }

        public void GenerateProfileFromPoints(List<Vector3> inputPoints, string title = "Profil Jalur Pengukuran 3D")
        {
            if (inputPoints == null || inputPoints.Count < 2)
            {
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("⚠️ Butuh minimal 2 titik untuk membuat grafik profil elevasi.");
                return;
            }

            profileTitle = title;
            samples.Clear();

            totalRouteDistance = 0f;
            minRL = float.MaxValue;
            maxRL = float.MinValue;
            totalAscent = 0f;
            totalDescent = 0f;
            maxSlopeGrade = 0f;

            float sampleStep = 8f; // Sample every 8 meters

            for (int i = 0; i < inputPoints.Count - 1; i++)
            {
                Vector3 pA = inputPoints[i];
                Vector3 pB = inputPoints[i + 1];

                float segDist2D = Vector2.Distance(new Vector2(pA.x, pA.z), new Vector2(pB.x, pB.z));
                int steps = Mathf.Max(1, Mathf.RoundToInt(segDist2D / sampleStep));

                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    Vector3 interp = Vector3.Lerp(pA, pB, t);

                    // Snap to actual terrain surface
                    float terrainY = interp.y;
                    Ray r = new Ray(new Vector3(interp.x, 3000f, interp.z), Vector3.down);
                    if (Physics.Raycast(r, out RaycastHit hit, 6000f))
                    {
                        terrainY = hit.point.y;
                    }

                    Vector3 samplePos = new Vector3(interp.x, terrainY, interp.z);

                    float distFromLast = samples.Count > 0 ? Vector2.Distance(new Vector2(samples[samples.Count - 1].worldPos.x, samples[samples.Count - 1].worldPos.z), new Vector2(samplePos.x, samplePos.z)) : 0f;
                    totalRouteDistance += distFromLast;

                    float elev = samplePos.y;
                    if (elev < minRL) minRL = elev;
                    if (elev > maxRL) maxRL = elev;

                    float grade = 0f;
                    if (samples.Count > 0 && distFromLast > 0.01f)
                    {
                        float deltaH = elev - samples[samples.Count - 1].elevationRL;
                        grade = (deltaH / distFromLast) * 100f;

                        if (deltaH > 0f) totalAscent += deltaH;
                        else totalDescent += Mathf.Abs(deltaH);

                        if (Mathf.Abs(grade) > maxSlopeGrade) maxSlopeGrade = Mathf.Abs(grade);
                    }

                    samples.Add(new ProfileSample
                    {
                        distanceAlongRoute = totalRouteDistance,
                        elevationRL = elev,
                        worldPos = samplePos,
                        gradePercent = grade
                    });
                }
            }

            // Add final point
            Vector3 lastPt = inputPoints[inputPoints.Count - 1];
            float lastY = lastPt.y;
            if (Physics.Raycast(new Ray(new Vector3(lastPt.x, 3000f, lastPt.z), Vector3.down), out RaycastHit hitLast, 6000f))
                lastY = hitLast.point.y;

            samples.Add(new ProfileSample
            {
                distanceAlongRoute = totalRouteDistance,
                elevationRL = lastY,
                worldPos = new Vector3(lastPt.x, lastY, lastPt.z),
                gradePercent = 0f
            });

            isProfilerOpen = true;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification($"📈 Grafik Profil Elevasi Dihasilkan: {totalRouteDistance:F0}m | Min: {minRL:F1}m | Max: {maxRL:F1}m");
        }

        public void CloseProfiler()
        {
            isProfilerOpen = false;
            if (targetMarker3D != null) targetMarker3D.SetActive(false);
        }

        public void DrawProfilerHUD(float screenW, float screenH, GUIStyle cardStyle)
        {
            if (!isProfilerOpen || samples.Count < 2) return;

            float panelW = Mathf.Min(820f, screenW - 140f);
            float panelH = 175f;
            float panelX = (screenW - panelW) / 2f;
            float panelY = screenH - panelH - 58f;

            // Background Card
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, cardStyle);

            // Title & Quick Stats Header
            string header = $"📈 <b>{profileTitle}</b> | Jarak Total: <color=#00FFA3>{totalRouteDistance:F0} m</color> | Elevasi: <color=#00E5FF>{minRL:F1} m</color> - <color=#00E5FF>{maxRL:F1} m</color> (ΔZ: {maxRL - minRL:F1}m) | Grade Max: <color={(maxSlopeGrade > 12f ? "#FF4444" : "#FFD700")}>{maxSlopeGrade:F1}%</color>";
            GUI.Label(new Rect(panelX + 12, panelY + 6, panelW - 120, 20), header);

            // Close button
            if (GUI.Button(new Rect(panelX + panelW - 65, panelY + 6, 52, 20), "Tutup"))
            {
                CloseProfiler();
                return;
            }

            // Graph Viewport Rect
            float graphX = panelX + 50f;
            float graphY = panelY + 30f;
            float graphW = panelW - 65f;
            float graphH = panelH - 55f;

            GUI.DrawTexture(new Rect(graphX, graphY, graphW, graphH), graphBgTex);

            float elevSpan = Mathf.Max(10f, maxRL - minRL);
            float padElevMin = minRL - (elevSpan * 0.1f);
            float padElevMax = maxRL + (elevSpan * 0.1f);
            float totalSpan = padElevMax - padElevMin;

            // Draw Horizontal Grid Lines & Elevation Labels (RL)
            int numGridLines = 4;
            for (int g = 0; g <= numGridLines; g++)
            {
                float t = (float)g / numGridLines;
                float yPos = graphY + graphH - (t * graphH);
                float rlVal = padElevMin + (t * totalSpan);

                GUI.DrawTexture(new Rect(graphX, yPos, graphW, 1), gridLineTex);
                GUI.Label(new Rect(panelX + 4, yPos - 8, 44, 16), $"{rlVal:F0}m");
            }

            // Draw Profile Curve
            hoveredSampleIndex = -1;
            Vector2 mousePos = Event.current.mousePosition;
            bool isMouseInGraph = mousePos.x >= graphX && mousePos.x <= graphX + graphW && mousePos.y >= graphY && mousePos.y <= graphY + graphH;

            float closestDist = float.MaxValue;

            for (int i = 0; i < samples.Count - 1; i++)
            {
                var s1 = samples[i];
                var s2 = samples[i + 1];

                float x1 = graphX + (s1.distanceAlongRoute / totalRouteDistance) * graphW;
                float y1 = graphY + graphH - ((s1.elevationRL - padElevMin) / totalSpan) * graphH;

                float x2 = graphX + (s2.distanceAlongRoute / totalRouteDistance) * graphW;
                float y2 = graphY + graphH - ((s2.elevationRL - padElevMin) / totalSpan) * graphH;

                // Pick color by slope grade
                float absGrade = Mathf.Abs(s2.gradePercent);
                Texture2D segTex = absGrade < 8f ? curveGreenTex : (absGrade < 12f ? curveYellowTex : curveRedTex);

                // Draw segment
                DrawThickLine(new Vector2(x1, y1), new Vector2(x2, y2), segTex, 2.5f);

                // Check closest sample to mouse X
                if (isMouseInGraph)
                {
                    float d = Mathf.Abs(mousePos.x - x1);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        hoveredSampleIndex = i;
                    }
                }
            }

            // Draw Hover Indicator & Station Marker
            if (hoveredSampleIndex >= 0 && hoveredSampleIndex < samples.Count)
            {
                var hSample = samples[hoveredSampleIndex];
                float hX = graphX + (hSample.distanceAlongRoute / totalRouteDistance) * graphW;
                float hY = graphY + graphH - ((hSample.elevationRL - padElevMin) / totalSpan) * graphH;

                // Vertical cursor line
                GUI.DrawTexture(new Rect(hX, graphY, 1, graphH), cursorLineTex);
                // Dot
                GUI.DrawTexture(new Rect(hX - 4, hY - 4, 8, 8), cursorLineTex);

                // Hover info badge
                float badgeW = 190f;
                float badgeX = Mathf.Clamp(hX - badgeW / 2f, graphX, graphX + graphW - badgeW);
                float badgeY = graphY + 6f;

                GUI.Box(new Rect(badgeX, badgeY, badgeW, 22), GUIContent.none, cardStyle);
                string badgeText = $"STA: <b>{hSample.distanceAlongRoute:F0}m</b> | RL: <b>{hSample.elevationRL:F1}m</b> | Slope: <b>{hSample.gradePercent:F1}%</b>";
                GUI.Label(new Rect(badgeX + 6, badgeY + 2, badgeW - 12, 18), badgeText);

                // Update 3D world target marker
                if (targetMarker3D != null)
                {
                    targetMarker3D.SetActive(true);
                    targetMarker3D.transform.position = hSample.worldPos + Vector3.up * 2f;
                }
            }
            else
            {
                if (targetMarker3D != null) targetMarker3D.SetActive(false);
            }

            // Distance Axis Label
            GUI.Label(new Rect(graphX, panelY + panelH - 18, 100, 16), "0 m");
            GUI.Label(new Rect(graphX + graphW / 2f - 40, panelY + panelH - 18, 80, 16), $"{totalRouteDistance / 2f:F0} m");
            GUI.Label(new Rect(graphX + graphW - 60, panelY + panelH - 18, 60, 16), $"{totalRouteDistance:F0} m");
        }

        private void DrawThickLine(Vector2 p1, Vector2 p2, Texture2D tex, float width)
        {
            Vector2 d = p2 - p1;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float length = d.magnitude;

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - width / 2f, length, width), tex);
            GUIUtility.RotateAroundPivot(-angle, p1);
        }

        private void OnDestroy()
        {
            if (graphBgTex != null) DestroyImmediate(graphBgTex);
            if (gridLineTex != null) DestroyImmediate(gridLineTex);
            if (curveGreenTex != null) DestroyImmediate(curveGreenTex);
            if (curveYellowTex != null) DestroyImmediate(curveYellowTex);
            if (curveRedTex != null) DestroyImmediate(curveRedTex);
            if (cursorLineTex != null) DestroyImmediate(cursorLineTex);
        }
    }
}
