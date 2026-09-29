using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSMiningDigitalTwinFX : MonoBehaviour
    {
        public static FMSMiningDigitalTwinFX Instance { get; private set; }

        [Header("Feature Toggles")]
        public bool showSpatialGrid = false;
        public bool showWaterSumps = false;
        public bool showGeofenceZones = false;
        public bool showBlastingZone = false;

        [Header("3D Spatial Grid Settings")]
        public float gridSpacing = 500f; // 500 meters grid
        private GameObject gridRoot;
        private Material gridLineMat;

        [Header("Water Sump Bodies")]
        private GameObject sumpRoot;
        private List<GameObject> sumpObjects = new List<GameObject>();
        private Material waterMat;

        [Header("Geofence & Blasting Boundaries")]
        private GameObject geofenceRoot;
        private LineRenderer iupBoundaryLine;
        private LineRenderer blastingZoneLine;
        private LineRenderer disposalZoneLine;
        private Material iupLineMat;
        private Material blastingLineMat;
        private Material disposalLineMat;

        // Blasting animation pulse
        private float blastPulseTimer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            InitMaterials();
            BuildSpatialGrid();
            BuildWaterSumps();
            BuildGeofences();

            if (gridRoot != null) gridRoot.SetActive(showSpatialGrid);
            if (sumpRoot != null) sumpRoot.SetActive(showWaterSumps);
            if (geofenceRoot != null) geofenceRoot.SetActive(showGeofenceZones || showBlastingZone);
        }

        private void Update()
        {
            // Pulse blasting zone warning color
            if (showBlastingZone && blastingLineMat != null)
            {
                blastPulseTimer += Time.unscaledDeltaTime * 3.5f;
                float alpha = 0.5f + Mathf.Sin(blastPulseTimer) * 0.45f;
                blastingLineMat.color = new Color(1.0f, 0.15f, 0.15f, alpha);
            }

            // Animate water sumps
            if (showWaterSumps && waterMat != null)
            {
                float t = Time.unscaledTime * 0.8f;
                waterMat.color = new Color(0.05f, 0.65f, 0.82f, 0.82f + Mathf.Sin(t) * 0.06f);
            }
        }

        private void InitMaterials()
        {
            Shader unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

            gridLineMat = new Material(unlit)
            {
                color = new Color(0.0f, 0.85f, 1.0f, 0.22f) // Subtle cyan laser grid
            };

            waterMat = new Material(unlit)
            {
                color = new Color(0.05f, 0.65f, 0.82f, 0.85f) // Tropical pit water
            };

            iupLineMat = new Material(unlit)
            {
                color = new Color(0.0f, 0.95f, 0.65f, 0.95f) // Emerald IUP boundary
            };

            blastingLineMat = new Material(unlit)
            {
                color = new Color(1.0f, 0.15f, 0.15f, 0.95f) // Red warning blast zone
            };

            disposalLineMat = new Material(unlit)
            {
                color = new Color(1.0f, 0.75f, 0.15f, 0.90f) // Gold disposal boundary
            };
        }

        #region Spatial UTM Grid
        public void BuildSpatialGrid()
        {
            if (gridRoot != null) Destroy(gridRoot);

            gridRoot = new GameObject("--- 3D_SPATIAL_UTM_GRID ---");
            gridRoot.transform.SetParent(this.transform);

            float minX = -3000f, maxX = 3000f;
            float minZ = -2500f, maxZ = 2500f;
            float gridElevation = 180f; // Floating slightly above terrain

            // Easting grid lines (vertical lines along Z)
            for (float x = minX; x <= maxX; x += gridSpacing)
            {
                GameObject lineObj = new GameObject($"Grid_Easting_{x}");
                lineObj.transform.SetParent(gridRoot.transform);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                ConfigureGridLine(lr, new Vector3(x, gridElevation, minZ), new Vector3(x, gridElevation, maxZ));
            }

            // Northing grid lines (horizontal lines along X)
            for (float z = minZ; z <= maxZ; z += gridSpacing)
            {
                GameObject lineObj = new GameObject($"Grid_Northing_{z}");
                lineObj.transform.SetParent(gridRoot.transform);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                ConfigureGridLine(lr, new Vector3(minX, gridElevation, z), new Vector3(maxX, gridElevation, z));
            }

            gridRoot.SetActive(showSpatialGrid);
        }

        private void ConfigureGridLine(LineRenderer lr, Vector3 start, Vector3 end)
        {
            lr.sharedMaterial = gridLineMat;
            lr.startWidth = 0.75f;
            lr.endWidth = 0.75f;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        public void ToggleSpatialGrid()
        {
            showSpatialGrid = !showSpatialGrid;
            if (gridRoot != null) gridRoot.SetActive(showSpatialGrid);
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showSpatialGrid ? "🌐 Grid Spasial UTM (500m) Aktif" : "🌐 Grid Spasial UTM Disembunyikan");
        }
        #endregion

        #region Water Sumps & Drainage
        public void BuildWaterSumps()
        {
            if (sumpRoot != null) Destroy(sumpRoot);
            sumpObjects.Clear();

            sumpRoot = new GameObject("--- PIT_WATER_SUMPS_3D ---");
            sumpRoot.transform.SetParent(this.transform);

            // Sump 1: Pit Central North Water Body (RL 42.5m)
            CreateSumpPlane("Sump Pit Central North", new Vector3(-120f, 42.5f, 220f), new Vector3(380f, 1f, 260f), 42.5f, 142000f);

            // Sump 2: Pit South Water Sump (RL 38.0m)
            CreateSumpPlane("Sump Pit South Deep", new Vector3(480f, 38.0f, -650f), new Vector3(450f, 1f, 320f), 38.0f, 218000f);

            // Sump 3: East River Sedimentation Pond (RL 48.0m)
            CreateSumpPlane("Sedimentation Pond East", new Vector3(1850f, 48.0f, 150f), new Vector3(520f, 1f, 180f), 48.0f, 95000f);

            sumpRoot.SetActive(showWaterSumps);
        }

        private void CreateSumpPlane(string name, Vector3 pos, Vector3 size, float waterRL, float volumeM3)
        {
            GameObject sumpObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sumpObj.name = name;
            sumpObj.transform.SetParent(sumpRoot.transform);
            sumpObj.transform.position = pos;
            sumpObj.transform.localScale = new Vector3(size.x, 2.5f, size.z);

            if (sumpObj.GetComponent<Renderer>() != null)
                sumpObj.GetComponent<Renderer>().sharedMaterial = waterMat;

            Collider c = sumpObj.GetComponent<Collider>();
            if (c != null) Destroy(c);

            sumpObjects.Add(sumpObj);
        }

        public void ToggleWaterSumps()
        {
            showWaterSumps = !showWaterSumps;
            if (sumpRoot != null) sumpRoot.SetActive(showWaterSumps);
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showWaterSumps ? "🌊 Water Sump & Indikator Genangan Aktif" : "🌊 Water Sump Disembunyikan");
        }
        #endregion

        #region Geofencing & Blasting Zones
        public void BuildGeofences()
        {
            if (geofenceRoot != null) Destroy(geofenceRoot);

            geofenceRoot = new GameObject("--- MINING_GEOFENCE_BOUNDARIES ---");
            geofenceRoot.transform.SetParent(this.transform);

            // 1. IUP / Pit Concession Boundary (Emerald Glowing Polygon)
            GameObject iupObj = new GameObject("IUP_Concession_Boundary");
            iupObj.transform.SetParent(geofenceRoot.transform);
            iupBoundaryLine = iupObj.AddComponent<LineRenderer>();
            ConfigurePerimeterLine(iupBoundaryLine, iupLineMat, 2.8f);

            Vector3[] iupCoords = new Vector3[]
            {
                new Vector3(-2400f, 190f, -1800f),
                new Vector3(-2500f, 210f, 1600f),
                new Vector3(-800f, 230f, 2200f),
                new Vector3(1400f, 180f, 2300f),
                new Vector3(2500f, 170f, 800f),
                new Vector3(2600f, 160f, -1500f),
                new Vector3(600f, 180f, -2200f),
                new Vector3(-1400f, 185f, -2100f)
            };
            SetLoopLinePoints(iupBoundaryLine, iupCoords);

            // 2. Active Blasting Danger Zone (Red Warning Polygon)
            GameObject blastObj = new GameObject("Active_Blasting_Zone");
            blastObj.transform.SetParent(geofenceRoot.transform);
            blastingZoneLine = blastObj.AddComponent<LineRenderer>();
            ConfigurePerimeterLine(blastingZoneLine, blastingLineMat, 3.5f);

            Vector3[] blastCoords = new Vector3[]
            {
                new Vector3(350f, 125f, 400f),
                new Vector3(720f, 130f, 650f),
                new Vector3(950f, 115f, 420f),
                new Vector3(880f, 95f, 180f),
                new Vector3(520f, 110f, 150f)
            };
            SetLoopLinePoints(blastingZoneLine, blastCoords);

            // 3. Disposal North Dumping Boundary (Gold Perimeter)
            GameObject dispObj = new GameObject("Disposal_North_Boundary");
            dispObj.transform.SetParent(geofenceRoot.transform);
            disposalZoneLine = dispObj.AddComponent<LineRenderer>();
            ConfigurePerimeterLine(disposalZoneLine, disposalLineMat, 2.2f);

            Vector3[] dispCoords = new Vector3[]
            {
                new Vector3(-1450f, 180f, 850f),
                new Vector3(-1100f, 190f, 1350f),
                new Vector3(-650f, 185f, 1200f),
                new Vector3(-850f, 175f, 750f)
            };
            SetLoopLinePoints(disposalZoneLine, dispCoords);

            geofenceRoot.SetActive(showGeofenceZones);
        }

        private void ConfigurePerimeterLine(LineRenderer lr, Material mat, float width)
        {
            lr.sharedMaterial = mat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void SetLoopLinePoints(LineRenderer lr, Vector3[] rawCoords)
        {
            lr.positionCount = rawCoords.Length;
            for (int i = 0; i < rawCoords.Length; i++)
            {
                Vector3 pt = rawCoords[i];
                if (Physics.Raycast(new Ray(new Vector3(pt.x, 3000f, pt.z), Vector3.down), out RaycastHit hit, 6000f))
                {
                    pt.y = hit.point.y + 1.2f; // Slight offset above terrain
                }
                lr.SetPosition(i, pt);
            }
        }

        public void ToggleGeofenceZones()
        {
            showGeofenceZones = !showGeofenceZones;
            if (geofenceRoot != null) geofenceRoot.SetActive(showGeofenceZones);
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showGeofenceZones ? "⚠️ Geofence IUP & Batas Disposal Aktif" : "⚠️ Geofence Batas Tambang Disembunyikan");
        }

        public void ToggleBlastingZone()
        {
            showBlastingZone = !showBlastingZone;
            if (blastingZoneLine != null) blastingZoneLine.gameObject.SetActive(showBlastingZone);
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showBlastingZone ? "💥 Area Bahaya Peledakan (Blasting Zone) Aktif" : "💥 Area Peledakan Dinonaktifkan");
        }
        #endregion
    }
}
