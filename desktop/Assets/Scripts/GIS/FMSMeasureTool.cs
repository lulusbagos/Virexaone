using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSMeasureTool : MonoBehaviour
    {
        public static FMSMeasureTool Instance { get; private set; }

        public enum MeasureMode
        {
            None,
            Distance,
            SlopeGradient,
            PolygonArea,
            PointInspection
        }

        [Header("State")]
        public MeasureMode currentMode = MeasureMode.None;
        public bool isToolActive => currentMode != MeasureMode.None;

        [Header("Measurement Points")]
        public List<Vector3> points = new List<Vector3>();
        public Vector3 currentHoverPoint;
        public bool hasHoverPoint = false;

        [Header("Visual Components")]
        private GameObject measureRoot;
        private LineRenderer activeLineRenderer;
        private LineRenderer previewLineRenderer;
        private List<GameObject> vertexMarkers = new List<GameObject>();
        private List<GameObject> segmentLabels = new List<GameObject>();

        [Header("Materials")]
        private Material lineMat;
        private Material previewLineMat;
        private Material pointMat;
        private Material labelBgMat;

        // Statistics
        public float totalDistance3D = 0f;
        public float totalDistance2D = 0f;
        public float deltaElevation = 0f;
        public float averageSlopePercent = 0f;
        public float averageSlopeDegree = 0f;
        public float polygonAreaSquareMeters = 0f;
        public float polygonAreaHectares = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            InitMaterials();
            CreateVisualHierarchy();
        }

        private void InitMaterials()
        {
            Shader unlitShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

            lineMat = new Material(unlitShader)
            {
                color = new Color(0.0f, 0.95f, 1.0f, 0.95f) // Electric Cyan Laser
            };

            previewLineMat = new Material(unlitShader)
            {
                color = new Color(1.0f, 0.85f, 0.1f, 0.65f) // Amber Glow
            };

            pointMat = new Material(unlitShader)
            {
                color = new Color(1.0f, 0.25f, 0.35f, 0.95f) // High-contrast Red/Pink
            };

            labelBgMat = new Material(unlitShader)
            {
                color = new Color(0.04f, 0.08f, 0.14f, 0.88f)
            };
        }

        private void CreateVisualHierarchy()
        {
            if (measureRoot == null)
            {
                measureRoot = new GameObject("--- FMS_3D_MEASUREMENT_TOOL ---");
                DontDestroyOnLoad(measureRoot);

                GameObject lineObj = new GameObject("Measurement_Line");
                lineObj.transform.SetParent(measureRoot.transform);
                activeLineRenderer = lineObj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(activeLineRenderer, lineMat, 0.8f);

                GameObject prevLineObj = new GameObject("Preview_Line");
                prevLineObj.transform.SetParent(measureRoot.transform);
                previewLineRenderer = prevLineObj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(previewLineRenderer, previewLineMat, 0.5f);
            }
        }

        private void ConfigureLineRenderer(LineRenderer lr, Material mat, float width)
        {
            lr.sharedMaterial = mat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 0;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        public void SetMode(MeasureMode mode)
        {
            if (currentMode == mode)
            {
                // Toggle off
                Clear();
                currentMode = MeasureMode.None;
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("📐 Alat Pengukuran Dinonaktifkan.");
                return;
            }

            Clear();
            currentMode = mode;

            string notif = mode switch
            {
                MeasureMode.Distance => "📐 Mode Ukur Jarak 3D Aktif: Klik kiri di peta untuk menambah titik.",
                MeasureMode.SlopeGradient => "⛰️ Mode Ukur Kemiringan Lereng: Klik 2 titik untuk melihat grade lereng.",
                MeasureMode.PolygonArea => "⬛ Mode Ukur Luas Area: Klik beberapa titik untuk menghitung luas (Ha).",
                MeasureMode.PointInspection => "📍 Mode Inspeksi Titik: Klik lokasi di peta untuk cek elevasi & UTM.",
                _ => "📐 Alat Pengukuran Siap."
            };

            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(notif);
        }

        private void Update()
        {
            if (!isToolActive)
            {
                if (activeLineRenderer != null && activeLineRenderer.positionCount > 0)
                    activeLineRenderer.positionCount = 0;
                if (previewLineRenderer != null && previewLineRenderer.positionCount > 0)
                    previewLineRenderer.positionCount = 0;
                return;
            }

            // Check if mouse is over UI
            bool isOverUI = FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsPointerOverUI();

            // Raycast terrain
            Ray ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : default;
            RaycastHit hit;
            hasHoverPoint = false;

            if (!isOverUI && Camera.main != null && Physics.Raycast(ray, out hit, 45000f))
            {
                currentHoverPoint = hit.point;
                hasHoverPoint = true;

                // 1. Left Click: Add measurement point
                if (Input.GetMouseButtonDown(0))
                {
                    AddPoint(hit.point);
                }
            }

            // 2. Right Click or Backspace: Remove last point
            if (!isOverUI && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Backspace)))
            {
                RemoveLastPoint();
            }

            // 3. Escape: Exit tool or clear
            if (Input.GetKeyDown(KeyCode.Escape) &&
                (FMSDashboardUI.Instance == null || !FMSDashboardUI.Instance.HasBlockingModal))
            {
                if (points.Count > 0) Clear();
                else SetMode(MeasureMode.None);
            }

            UpdateVisuals();
            CalculateMetrics();
        }

        public void AddPoint(Vector3 worldPos)
        {
            // Sample terrain height for maximum precision
            Terrain t = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            if (t != null)
            {
                float groundY = t.SampleHeight(worldPos) + t.transform.position.y;
                worldPos.y = groundY + 0.35f; // Slight elevation offset to avoid z-fighting
            }

            points.Add(worldPos);

            // Create 3D pin marker
            CreateVertexMarker(worldPos, points.Count);

            if (currentMode == MeasureMode.SlopeGradient && points.Count >= 2)
            {
                // Auto finish 2-point slope measurement
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification($"⛰️ Kemiringan Lereng: {averageSlopePercent:F1}% ({averageSlopeDegree:F1}°) | Beda Tinggi: {deltaElevation:+0.0;-0.0;0.0}m");
            }
        }

        public void RemoveLastPoint()
        {
            if (points.Count > 0)
            {
                points.RemoveAt(points.Count - 1);
                if (vertexMarkers.Count > 0)
                {
                    int lastIdx = vertexMarkers.Count - 1;
                    if (vertexMarkers[lastIdx] != null) Destroy(vertexMarkers[lastIdx]);
                    vertexMarkers.RemoveAt(lastIdx);
                }
            }
        }

        public void Clear()
        {
            points.Clear();
            foreach (var m in vertexMarkers) if (m != null) Destroy(m);
            vertexMarkers.Clear();

            foreach (var l in segmentLabels) if (l != null) Destroy(l);
            segmentLabels.Clear();

            if (activeLineRenderer != null) activeLineRenderer.positionCount = 0;
            if (previewLineRenderer != null) previewLineRenderer.positionCount = 0;

            totalDistance3D = 0f;
            totalDistance2D = 0f;
            deltaElevation = 0f;
            averageSlopePercent = 0f;
            averageSlopeDegree = 0f;
            polygonAreaSquareMeters = 0f;
            polygonAreaHectares = 0f;
        }

        private void CreateVertexMarker(Vector3 pos, int index)
        {
            GameObject pinObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pinObj.name = $"MeasurePoint_{index}";
            pinObj.transform.SetParent(measureRoot.transform);
            pinObj.transform.position = pos;
            pinObj.transform.localScale = Vector3.one * 4.5f;

            var ren = pinObj.GetComponent<Renderer>();
            if (ren != null) ren.sharedMaterial = pointMat;

            Collider col = pinObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            vertexMarkers.Add(pinObj);
        }

        private void UpdateVisuals()
        {
            if (activeLineRenderer == null) return;

            // 1. Draw solid line between confirmed points
            if (points.Count > 0)
            {
                int count = points.Count;
                if (currentMode == MeasureMode.PolygonArea && count >= 3)
                {
                    // Closed loop for polygon area
                    activeLineRenderer.positionCount = count + 1;
                    for (int i = 0; i < count; i++) activeLineRenderer.SetPosition(i, points[i]);
                    activeLineRenderer.SetPosition(count, points[0]);
                }
                else
                {
                    activeLineRenderer.positionCount = count;
                    for (int i = 0; i < count; i++) activeLineRenderer.SetPosition(i, points[i]);
                }
            }
            else
            {
                activeLineRenderer.positionCount = 0;
            }

            // 2. Draw dynamic preview line to hover point
            if (previewLineRenderer != null)
            {
                if (points.Count > 0 && hasHoverPoint)
                {
                    previewLineRenderer.positionCount = 2;
                    previewLineRenderer.SetPosition(0, points[points.Count - 1]);
                    previewLineRenderer.SetPosition(1, currentHoverPoint + Vector3.up * 0.35f);
                }
                else
                {
                    previewLineRenderer.positionCount = 0;
                }
            }
        }

        private void CalculateMetrics()
        {
            if (points.Count < 2)
            {
                totalDistance3D = 0f;
                totalDistance2D = 0f;
                deltaElevation = 0f;
                averageSlopePercent = 0f;
                averageSlopeDegree = 0f;
                polygonAreaSquareMeters = 0f;
                polygonAreaHectares = 0f;
                return;
            }

            float dist3D = 0f;
            float dist2D = 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];

                dist3D += Vector3.Distance(p1, p2);
                dist2D += Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
            }

            totalDistance3D = dist3D;
            totalDistance2D = dist2D;
            deltaElevation = points[points.Count - 1].y - points[0].y;

            if (dist2D > 0.01f)
            {
                averageSlopePercent = (Mathf.Abs(deltaElevation) / dist2D) * 100f;
                averageSlopeDegree = Mathf.Atan2(Mathf.Abs(deltaElevation), dist2D) * Mathf.Rad2Deg;
            }

            // Polygon Area Calculation (Shoelace formula)
            if (currentMode == MeasureMode.PolygonArea && points.Count >= 3)
            {
                double area = 0.0;
                int n = points.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 pA = points[i];
                    Vector3 pB = points[(i + 1) % n];
                    area += (pA.x * pB.z) - (pB.x * pA.z);
                }
                polygonAreaSquareMeters = (float)(Math.Abs(area) * 0.5);
                polygonAreaHectares = polygonAreaSquareMeters / 10000f;
            }
        }

        public string GetSlopeGradeSafetyStatus()
        {
            if (averageSlopePercent < 8f) return "<color=#00FFA3>🟢 AMAN (Grade Hauling Normal < 8%)</color>";
            if (averageSlopePercent <= 12f) return "<color=#FFCC00>🟡 RAMP STANDAR (Grade 8% - 12%)</color>";
            if (averageSlopePercent <= 18f) return "<color=#FF8800>🟠 CURAM (Grade 12% - 18%, Hati-hati)</color>";
            return "<color=#FF3344>🔴 SANGAT CURAM (Grade > 18%, Dilarang Hauler HD)</color>";
        }
    }
}
