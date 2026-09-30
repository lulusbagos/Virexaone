using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    public class FMSMining3DLayer : MonoBehaviour
    {
        public static FMSMining3DLayer Instance { get; private set; }

        [Header("Global & Layer Toggles (Default Active View)")]
        public bool showAllMarkers = true;
        public bool showLabels = true;
        public bool showDisposals = true;
        public bool showFronts = true;
        public bool showCallPoints = true;
        public bool showRoads = true;

        [Header("API Settings")]
        public string backendUrl = "http://127.0.0.1:8000";
        [Range(150f, 500f)] public float roadTooltipSpacingMeters = 250f;

        [Header("Visual Materials")]
        private Material disposalPadMat;
        private Material disposalGlowMat;
        private Material frontPadMat;
        private Material frontGlowMat;
        private Material callpointPadMat;
        private Material callpointGlowMat;
        private Material roadLineMat;
        private Material hudBadgeBgMat;
        private const float DefaultRoadWidthMeters = 20f;
        private static readonly Color[] RoadColorPresets =
        {
            new Color(0.25f, 0.76f, 0.86f, 0.82f),
            new Color(0.94f, 0.96f, 0.90f, 0.82f),
            new Color(1.00f, 0.68f, 0.38f, 0.82f),
            new Color(0.73f, 0.67f, 0.98f, 0.82f)
        };
        public int RoadColorPresetIndex { get; private set; }
        public float RoadVisualWidthMeters { get; private set; } = DefaultRoadWidthMeters;
        public Color RoadVisualColor => RoadColorPresets[RoadColorPresetIndex];

        // Container GameObjects
        private GameObject layerRoot;
        private GameObject disposalContainer;
        private GameObject frontContainer;
        private GameObject callpointContainer;
        private GameObject roadContainer;

        // Dynamic billboard labels to rotate towards camera
        private List<Transform> activeBillboards = new List<Transform>();
        private List<Transform> rotatingBeacons = new List<Transform>();

        // Marker Tracking
        [System.Serializable]
        public class MarkerEntry
        {
            public LocationItem data;
            public GameObject rootObj;
            public GameObject labelObj;
            public Vector3 worldPos;
        }

        [System.Serializable]
        public class RoadEntry
        {
            public RoadItem data;
            public GameObject rootObj;
            public MeshFilter meshFilter;
            public Vector3 midPoint;
            public string routeName;
        }

        public List<MarkerEntry> spawnedMarkers = new List<MarkerEntry>();
        public HashSet<long> hiddenLocationIds = new HashSet<long>();

        public List<RoadEntry> spawnedRoads = new List<RoadEntry>();
        private readonly List<RoadEntry> roadTooltipRoads = new List<RoadEntry>();
        public HashSet<long> hiddenRoadIds = new HashSet<long>();

        // Cached API Data
        private List<LocationItem> disposalList = new List<LocationItem>();
        private List<LocationItem> frontList = new List<LocationItem>();
        private List<LocationItem> callpointList = new List<LocationItem>();
        private List<RoadItem> roadList = new List<RoadItem>();
        private bool hasBackendRoadData;
        private GUIStyle roadTooltipStyle;

        public bool isLoaded = false;
        private readonly HashSet<UnityWebRequest> activeRequests = new HashSet<UnityWebRequest>();

        private void OnDisable()
        {
            foreach (UnityWebRequest request in activeRequests)
                request.Abort();
            activeRequests.Clear();
        }

        [System.Serializable]
        public class LocationItem
        {
            public long location_id;
            public string name;
            public string type;
            public string category;
            public double easting;
            public double northing;
            public double elevation;
            public UnityPos unity_pos;
            public string updated_at;
        }

        [System.Serializable]
        public class UnityPos
        {
            public float x;
            public float y;
            public float z;
        }

        [System.Serializable]
        public class LocationResponse
        {
            public string status;
            public int count;
            public LocationItem[] data;
        }

        [System.Serializable]
        public class RoadResponse
        {
            public string status;
            public int count;
            public RoadItem[] data;
        }

        [System.Serializable]
        public class RoadItem
        {
            public long road_id;
            public long distance_m;
            public LocationItem start_location;
            public LocationItem end_location;
            public long speed_max;
            public long lane_width;
            public string trajectory;
            public string updated_at;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            RoadColorPresetIndex = Mathf.Clamp(PlayerPrefs.GetInt("Virexa_Road_Color", 0), 0, RoadColorPresets.Length - 1);
            RoadVisualWidthMeters = Mathf.Clamp(PlayerPrefs.GetFloat("Virexa_Road_Width", DefaultRoadWidthMeters), 8f, 40f);
            InitMaterials();
            CreateContainers();
        }

        private void Start()
        {
            LoadFallbackSpatialLayers();
            StartCoroutine(FetchAllMiningLayers());
        }

        public void LoadFallbackSpatialLayers()
        {
            if (isLoaded && spawnedMarkers.Count > 0) return;

            hasBackendRoadData = false;
            CreateContainers();
            InitMaterials();

            disposalList.Clear();
            frontList.Clear();
            callpointList.Clear();

            // 1. Disposals
            string[] dumps = { "Disposal Pit Unggul Utara", "Disposal Pit Unggul Selatan", "In-Pit Dump West Bench", "Waste Dump South Ridge", "Low Grade Stockpile" };
            double[] dx = { 572200, 573100, 571800, 573400, 572800 };
            double[] dy = { 113900, 112800, 113400, 112400, 114100 };
            for (int i = 0; i < dumps.Length; i++)
            {
                disposalList.Add(new LocationItem
                {
                    location_id = 100 + i,
                    name = dumps[i],
                    type = "Dump",
                    category = "Disposal",
                    easting = dx[i],
                    northing = dy[i],
                    elevation = 95.0
                });
            }

            // 2. Front Loading
            string[] fronts = { "Front Loading Pit 1 - Seam A", "Front Loading Pit 1 - Seam B", "Front Loading South Bench", "Front Blast Area Alpha", "Front Blast Area Bravo" };
            double[] fx = { 572600, 572900, 572400, 573200, 572100 };
            double[] fy = { 113300, 113600, 113000, 113200, 113700 };
            for (int i = 0; i < fronts.Length; i++)
            {
                frontList.Add(new LocationItem
                {
                    location_id = 200 + i,
                    name = fronts[i],
                    type = "Blast",
                    category = "Front",
                    easting = fx[i],
                    northing = fy[i],
                    elevation = 45.0
                });
            }

            // 3. Simpang / CallPoints
            for (int i = 1; i <= 20; i++)
            {
                double cx = 572000 + (i % 5) * 350 + (i * 15);
                double cy = 112600 + (i / 5) * 320 + (i * 10);
                callpointList.Add(new LocationItem
                {
                    location_id = 300 + i,
                    name = $"Simpang CP-{i:00}",
                    type = "CallPoint",
                    category = "Simpang",
                    easting = cx,
                    northing = cy,
                    elevation = 70.0
                });
            }

            activeBillboards.Clear();
            rotatingBeacons.Clear();
            spawnedMarkers.Clear();

            BuildDisposalMarkers();
            BuildFrontMarkers();
            BuildCallPointMarkers();

            // 4. Roads
            roadList.Clear();
            var allLocs = new List<LocationItem>();
            allLocs.AddRange(disposalList);
            allLocs.AddRange(frontList);
            for (int i = 0; i < allLocs.Count - 1; i += 2)
            {
                var start = allLocs[i];
                var end = allLocs[i + 1];
                long dist = (long)Math.Sqrt(Math.Pow(end.easting - start.easting, 2) + Math.Pow(end.northing - start.northing, 2));
                roadList.Add(new RoadItem
                {
                    road_id = 1000 + i,
                    distance_m = Math.Max(120, dist),
                    start_location = start,
                    end_location = end
                });
            }
            spawnedRoads.Clear();
            BuildRoadLines();

            isLoaded = true;
        }

        private void InitMaterials()
        {
            if (roadLineMat != null) return;
            Shader unlitColorShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");

            // 1. DISPOSAL MATERIALS (Safety Amber / Gold)
            disposalPadMat = new Material(standardShader) { name = "Mat_Disposal_Pad" };
            disposalPadMat.color = new Color(0.12f, 0.10f, 0.08f, 0.90f);

            disposalGlowMat = new Material(standardShader) { name = "Mat_Disposal_Glow" };
            disposalGlowMat.color = new Color(1.0f, 0.65f, 0.05f, 0.95f);
            if (disposalGlowMat.HasProperty("_EmissionColor"))
            {
                disposalGlowMat.EnableKeyword("_EMISSION");
                disposalGlowMat.SetColor("_EmissionColor", new Color(1.0f, 0.60f, 0.05f) * 1.5f);
            }

            // 2. FRONT LOADING MATERIALS (Electric High-Tech Cyan)
            frontPadMat = new Material(standardShader) { name = "Mat_Front_Pad" };
            frontPadMat.color = new Color(0.06f, 0.12f, 0.16f, 0.90f);

            frontGlowMat = new Material(standardShader) { name = "Mat_Front_Glow" };
            frontGlowMat.color = new Color(0.0f, 0.88f, 1.0f, 0.95f);
            if (frontGlowMat.HasProperty("_EmissionColor"))
            {
                frontGlowMat.EnableKeyword("_EMISSION");
                frontGlowMat.SetColor("_EmissionColor", new Color(0.0f, 0.85f, 1.0f) * 1.5f);
            }

            // 3. CALLPOINT MATERIALS (Tactical GPS Emerald)
            callpointPadMat = new Material(standardShader) { name = "Mat_Callpoint_Pad" };
            callpointPadMat.color = new Color(0.08f, 0.14f, 0.10f, 0.90f);

            callpointGlowMat = new Material(standardShader) { name = "Mat_Callpoint_Glow" };
            callpointGlowMat.color = new Color(0.12f, 0.92f, 0.48f, 0.95f);
            if (callpointGlowMat.HasProperty("_EmissionColor"))
            {
                callpointGlowMat.EnableKeyword("_EMISSION");
                callpointGlowMat.SetColor("_EmissionColor", new Color(0.12f, 0.90f, 0.45f) * 1.3f);
            }

            // The fallback corridor is about 3.4 times the 5.8 m HD truck model width.
            Shader roadShader = Resources.Load<Shader>("RoadGroundOverlay") ??
                Shader.Find("Virexa/RoadGroundOverlay") ?? Shader.Find("Unlit/Transparent") ?? unlitColorShader;
            roadLineMat = new Material(roadShader)
            {
                name = "Mat_Hauling_Road_Band",
                color = RoadVisualColor
            };
            roadLineMat.renderQueue = 2900;

            // 5. HUD BADGE BACKGROUND MATERIAL (Ultra-High Contrast Dark Slate)
            hudBadgeBgMat = new Material(unlitColorShader)
            {
                name = "Mat_HUD_Badge_Bg",
                color = new Color(0.05f, 0.08f, 0.14f, 0.88f)
            };
        }

        private void CreateContainers()
        {
            if (layerRoot == null)
            {
                layerRoot = new GameObject("FMS_Mining_3D_Layers");
                disposalContainer = new GameObject("Disposal_Markers");
                disposalContainer.transform.SetParent(layerRoot.transform, false);

                frontContainer = new GameObject("Front_Loading_Markers");
                frontContainer.transform.SetParent(layerRoot.transform, false);

                callpointContainer = new GameObject("CallPoint_Markers");
                callpointContainer.transform.SetParent(layerRoot.transform, false);

                roadContainer = new GameObject("Hauling_Roads_Network");
                roadContainer.transform.SetParent(layerRoot.transform, false);

                disposalContainer.SetActive(showDisposals);
                frontContainer.SetActive(showFronts);
                callpointContainer.SetActive(showCallPoints);
                roadContainer.SetActive(showRoads);
            }
        }

        public IEnumerator FetchAllMiningLayers()
        {
            if (FMSDashboardUI.Instance != null && !string.IsNullOrEmpty(FMSDashboardUI.Instance.apiBaseUrl))
            {
                backendUrl = FMSDashboardUI.Instance.apiBaseUrl.TrimEnd('/');
            }

            using (UnityWebRequest req = UnityWebRequest.Get($"{backendUrl}/api/v1/locations/all"))
            {
                FMSApiSession.Authorize(req);
                req.timeout = 5;
                activeRequests.Add(req);
                yield return req.SendWebRequest();
                activeRequests.Remove(req);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        LocationResponse resp = JsonUtility.FromJson<LocationResponse>(req.downloadHandler.text);
                        if (resp != null && resp.data != null)
                        {
                            disposalList.Clear();
                            frontList.Clear();
                            callpointList.Clear();

                            foreach (var loc in resp.data)
                            {
                                if (loc.category == "Disposal" || loc.type == "Dump" || loc.type == "InpitDump")
                                    disposalList.Add(loc);
                                else if (loc.category == "Front" || loc.type == "Blast" || loc.type == "Pit")
                                    frontList.Add(loc);
                                else if (loc.category == "Simpang" || loc.type == "CallPoint")
                                    callpointList.Add(loc);
                            }

                            activeBillboards.Clear();
                            rotatingBeacons.Clear();
                            spawnedMarkers.Clear();

                            BuildDisposalMarkers();
                            BuildFrontMarkers();
                            BuildCallPointMarkers();
                            isLoaded = true;
                            Debug.Log($"[FMSMining3DLayer] Clean 3D GIS Layers Loaded: {spawnedMarkers.Count} Active Markers.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FMSMining3DLayer] Error parsing locations: {ex.Message}");
                    }
                }
            }

            using (UnityWebRequest req = UnityWebRequest.Get($"{backendUrl}/api/v1/roads/network"))
            {
                FMSApiSession.Authorize(req);
                req.timeout = 5;
                activeRequests.Add(req);
                yield return req.SendWebRequest();
                activeRequests.Remove(req);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        RoadResponse resp = JsonUtility.FromJson<RoadResponse>(req.downloadHandler.text);
                        if (resp != null && resp.data != null)
                        {
                            roadList.Clear();
                            roadList.AddRange(resp.data);
                            hasBackendRoadData = true;
                            spawnedRoads.Clear();
                            BuildRoadLines();
                            Debug.Log($"[FMSMining3DLayer] Hauling Network Built: {spawnedRoads.Count} Segments.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FMSMining3DLayer] Error parsing roads: {ex.Message}");
                    }
                }
            }
        }

        private Vector3 CalculateTerrainWorldPos(double rawEasting, double rawNorthing, double rawElev)
        {
            float localX = 0f;
            float localZ = 0f;

            if (rawEasting > 400000 && rawEasting < 500000) // Hexagon Local Grid
            {
                localX = (float)(rawEasting - 423580.0);
                localZ = (float)(rawNorthing - 3650.0);
            }
            else // UTM 50N
            {
                Vector3 localPos = GeoCoordinateConverter.UTMToUnity(rawEasting, rawNorthing, rawElev);
                localX = localPos.x;
                localZ = localPos.z;
            }

            localX = Mathf.Clamp(localX, -2600f, 2600f);
            localZ = Mathf.Clamp(localZ, -1950f, 1950f);

            float terrainY = 75f;
            Terrain t = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            if (t != null && t.isActiveAndEnabled && t.terrainData != null)
            {
                Vector3 samplePos = new Vector3(localX, 0f, localZ);
                terrainY = t.SampleHeight(samplePos) + t.transform.position.y;
            }
            else if (rawElev > 1.0)
            {
                terrainY = (float)rawElev;
            }

            return new Vector3(localX, terrainY, localZ);
        }

        private void BuildDisposalMarkers()
        {
            foreach (Transform child in disposalContainer.transform) Destroy(child.gameObject);

            List<Vector3> placedPositions = new List<Vector3>();

            foreach (var d in disposalList)
            {
                Vector3 pos = CalculateTerrainWorldPos(d.easting, d.northing, d.elevation);

                bool isTooClose = false;
                foreach (var p in placedPositions)
                {
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(p.x, p.z)) < 38f)
                    {
                        isTooClose = true;
                        break;
                    }
                }
                if (isTooClose) continue;
                placedPositions.Add(pos);

                if (placedPositions.Count > 35) break;

                CreateSleekBeacon(
                    locItem: d,
                    prefix: "🚜 DISPOSAL",
                    pos: pos,
                    padMat: disposalPadMat,
                    glowMat: disposalGlowMat,
                    parent: disposalContainer.transform,
                    radius: 20.0f
                );
            }
        }

        private void BuildFrontMarkers()
        {
            foreach (Transform child in frontContainer.transform) Destroy(child.gameObject);

            List<Vector3> placedPositions = new List<Vector3>();

            foreach (var f in frontList)
            {
                Vector3 pos = CalculateTerrainWorldPos(f.easting, f.northing, f.elevation);

                bool isTooClose = false;
                foreach (var p in placedPositions)
                {
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(p.x, p.z)) < 32f)
                    {
                        isTooClose = true;
                        break;
                    }
                }
                if (isTooClose) continue;
                placedPositions.Add(pos);

                if (placedPositions.Count > 30) break;

                CreateSleekBeacon(
                    locItem: f,
                    prefix: "⛏️ FRONT",
                    pos: pos,
                    padMat: frontPadMat,
                    glowMat: frontGlowMat,
                    parent: frontContainer.transform,
                    radius: 17.0f
                );
            }

            if (placedPositions.Count > 0 && FMSDigitalTwinAtmosphere.Instance != null)
            {
                FMSDigitalTwinAtmosphere.Instance.SpawnPitFloodlights(placedPositions);
            }
        }

        private void BuildCallPointMarkers()
        {
            foreach (Transform child in callpointContainer.transform) Destroy(child.gameObject);

            List<Vector3> placedPositions = new List<Vector3>();

            foreach (var cp in callpointList)
            {
                Vector3 pos = CalculateTerrainWorldPos(cp.easting, cp.northing, cp.elevation);

                bool isTooClose = false;
                foreach (var p in placedPositions)
                {
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(p.x, p.z)) < 26f)
                    {
                        isTooClose = true;
                        break;
                    }
                }
                if (isTooClose) continue;
                placedPositions.Add(pos);

                if (placedPositions.Count > 45) break;

                CreateCallPointGroundNode(
                    locItem: cp,
                    pos: pos,
                    padMat: callpointPadMat,
                    glowMat: callpointGlowMat,
                    parent: callpointContainer.transform
                );
            }
        }

        private void CreateSleekBeacon(
            LocationItem locItem, 
            string prefix, 
            Vector3 pos, 
            Material padMat, 
            Material glowMat, 
            Transform parent, 
            float radius)
        {
            GameObject markerRoot = new GameObject($"Loc_{locItem.location_id}_{locItem.name}");
            markerRoot.transform.SetParent(parent, false);
            markerRoot.transform.position = pos;

            // 1. Large Ground Safety Hazard Pad (Outer Glowing Ring)
            GameObject glowRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glowRing.name = "GlowRing_Outer";
            glowRing.transform.SetParent(markerRoot.transform, false);
            glowRing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            glowRing.transform.localScale = new Vector3(radius * 2.0f, 0.06f, radius * 2.0f);
            var renGlow = glowRing.GetComponent<Renderer>();
            if (renGlow != null) renGlow.material = glowMat;
            Collider cGlow = glowRing.GetComponent<Collider>();
            if (cGlow != null) Destroy(cGlow);

            // 2. Inner Heavy Dark Platform
            GameObject darkPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            darkPad.name = "DarkPad_Core";
            darkPad.transform.SetParent(markerRoot.transform, false);
            darkPad.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            darkPad.transform.localScale = new Vector3(radius * 1.55f, 0.08f, radius * 1.55f);
            var renPad = darkPad.GetComponent<Renderer>();
            if (renPad != null) renPad.material = padMat;
            Collider cPad = darkPad.GetComponent<Collider>();
            if (cPad != null) Destroy(cPad);

            // 3. Concentric Target Ring
            GameObject targetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetRing.name = "TargetRing_Inner";
            targetRing.transform.SetParent(markerRoot.transform, false);
            targetRing.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            targetRing.transform.localScale = new Vector3(radius * 0.90f, 0.06f, radius * 0.90f);
            var renTarget = targetRing.GetComponent<Renderer>();
            if (renTarget != null) renTarget.material = glowMat;
            Collider cTarget = targetRing.GetComponent<Collider>();
            if (cTarget != null) Destroy(cTarget);

            // 4. Perimeter Corner Warning Pylons (4 Pylons at 90 deg)
            float pylonDist = radius * 0.82f;
            for (int p = 0; p < 4; p++)
            {
                float ang = p * 90f * Mathf.Deg2Rad;
                Vector3 pylonPos = new Vector3(Mathf.Cos(ang) * pylonDist, 1.8f, Mathf.Sin(ang) * pylonDist);
                GameObject pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pylon.name = $"Pylon_{p}";
                pylon.transform.SetParent(markerRoot.transform, false);
                pylon.transform.localPosition = pylonPos;
                pylon.transform.localScale = new Vector3(0.55f, 1.8f, 0.55f);
                var renPylon = pylon.GetComponent<Renderer>();
                if (renPylon != null) renPylon.material = glowMat;
                Collider cP = pylon.GetComponent<Collider>();
                if (cP != null) Destroy(cP);
            }

            // 5. Mega High-Altitude Laser Needle Pillar (Extends 28m into the sky!)
            float pillarH = 28.0f;
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "LaserNeedle_Main";
            stem.transform.SetParent(markerRoot.transform, false);
            stem.transform.localPosition = new Vector3(0f, pillarH * 0.5f, 0f);
            stem.transform.localScale = new Vector3(0.65f, pillarH * 0.5f, 0.65f);
            var renStem = stem.GetComponent<Renderer>();
            if (renStem != null) renStem.material = glowMat;
            Collider cStem = stem.GetComponent<Collider>();
            if (cStem != null) Destroy(cStem);

            // 6. Glowing Rotating 3D Crystal Beacon Head at the Apex (y = 28.8m)
            GameObject beaconTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beaconTip.name = "Beacon_Crystal";
            beaconTip.transform.SetParent(markerRoot.transform, false);
            beaconTip.transform.localPosition = new Vector3(0f, pillarH + 0.8f, 0f);
            beaconTip.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            beaconTip.transform.localScale = new Vector3(2.6f, 3.4f, 2.6f);
            var renTip = beaconTip.GetComponent<Renderer>();
            if (renTip != null) renTip.material = glowMat;
            Collider cTip = beaconTip.GetComponent<Collider>();
            if (cTip != null) Destroy(cTip);

            // Rotating beacon halo ring
            GameObject haloRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            haloRing.name = "Beacon_Halo";
            haloRing.transform.SetParent(beaconTip.transform, false);
            haloRing.transform.localPosition = Vector3.zero;
            haloRing.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
            var renHalo = haloRing.GetComponent<Renderer>();
            if (renHalo != null) renHalo.material = glowMat;
            Collider cHalo = haloRing.GetComponent<Collider>();
            if (cHalo != null) Destroy(cHalo);

            rotatingBeacons.Add(beaconTip.transform);

            // 7. Floating Mega Billboard HUD Badge at High Altitude (y = 33.5m)
            GameObject hudRoot = new GameObject("HUD_Badge");
            hudRoot.transform.SetParent(markerRoot.transform, false);
            hudRoot.transform.localPosition = new Vector3(0f, pillarH + 5.5f, 0f);

            string fullLabel = $"[{prefix}]  {locItem.name.ToUpper()}";
            float badgeWidth = Mathf.Clamp(fullLabel.Length * 0.68f + 3.5f, 9.5f, 28.0f);
            float badgeHeight = 3.6f;

            // Outer Neon Border
            GameObject borderQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            borderQuad.name = "Badge_Border";
            borderQuad.transform.SetParent(hudRoot.transform, false);
            borderQuad.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            borderQuad.transform.localScale = new Vector3(badgeWidth + 0.65f, badgeHeight + 0.55f, 1f);
            var renBorder = borderQuad.GetComponent<Renderer>();
            if (renBorder != null) renBorder.material = glowMat;
            Collider cBorder = borderQuad.GetComponent<Collider>();
            if (cBorder != null) Destroy(cBorder);

            // Inner Dark Slate Backplate
            GameObject bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgQuad.name = "Badge_Background";
            bgQuad.transform.SetParent(hudRoot.transform, false);
            bgQuad.transform.localPosition = new Vector3(0f, 0f, 0f);
            bgQuad.transform.localScale = new Vector3(badgeWidth, badgeHeight, 1f);
            var renBg = bgQuad.GetComponent<Renderer>();
            if (renBg != null) renBg.material = hudBadgeBgMat;
            Collider cBg = bgQuad.GetComponent<Collider>();
            if (cBg != null) Destroy(cBg);

            // Text Label
            GameObject textObj = new GameObject("Badge_Text");
            textObj.transform.SetParent(hudRoot.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.08f);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = fullLabel;
            textMesh.fontSize = 44;
            textMesh.characterSize = 0.38f;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            activeBillboards.Add(hudRoot.transform);

            spawnedMarkers.Add(new MarkerEntry
            {
                data = locItem,
                rootObj = markerRoot,
                labelObj = hudRoot,
                worldPos = pos
            });
        }

        private void CreateCallPointGroundNode(
            LocationItem locItem, 
            Vector3 pos, 
            Material padMat, 
            Material glowMat, 
            Transform parent)
        {
            GameObject nodeRoot = new GameObject($"CP_{locItem.location_id}_{locItem.name}");
            nodeRoot.transform.SetParent(parent, false);
            nodeRoot.transform.position = pos;

            // 1. Large Ground Traffic Radar Disk
            float cpRad = 9.0f;
            GameObject outerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outerRing.name = "OuterRing_Radar";
            outerRing.transform.SetParent(nodeRoot.transform, false);
            outerRing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            outerRing.transform.localScale = new Vector3(cpRad * 2.0f, 0.06f, cpRad * 2.0f);
            var renOuter = outerRing.GetComponent<Renderer>();
            if (renOuter != null) renOuter.material = glowMat;
            Collider cOuter = outerRing.GetComponent<Collider>();
            if (cOuter != null) Destroy(cOuter);

            // 2. Dark Asphalt Plate
            GameObject darkCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            darkCore.name = "DarkCore_Plate";
            darkCore.transform.SetParent(nodeRoot.transform, false);
            darkCore.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            darkCore.transform.localScale = new Vector3(cpRad * 1.55f, 0.08f, cpRad * 1.55f);
            var renDark = darkCore.GetComponent<Renderer>();
            if (renDark != null) renDark.material = padMat;
            Collider cDark = darkCore.GetComponent<Collider>();
            if (cDark != null) Destroy(cDark);

            // 3. Glowing Center Radar Core
            GameObject centerDot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            centerDot.name = "CenterDot_Core";
            centerDot.transform.SetParent(nodeRoot.transform, false);
            centerDot.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            centerDot.transform.localScale = new Vector3(cpRad * 0.70f, 0.06f, cpRad * 0.70f);
            var renDot = centerDot.GetComponent<Renderer>();
            if (renDot != null) renDot.material = glowMat;
            Collider cDot = centerDot.GetComponent<Collider>();
            if (cDot != null) Destroy(cDot);

            // 4. High Vertical Tactical Laser Needle (Height = 22m!)
            float cpPillarH = 22.0f;
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "CP_LaserStem";
            stem.transform.SetParent(nodeRoot.transform, false);
            stem.transform.localPosition = new Vector3(0f, cpPillarH * 0.5f, 0f);
            stem.transform.localScale = new Vector3(0.50f, cpPillarH * 0.5f, 0.50f);
            var renStem = stem.GetComponent<Renderer>();
            if (renStem != null) renStem.material = glowMat;
            Collider cStem = stem.GetComponent<Collider>();
            if (cStem != null) Destroy(cStem);

            // 5. 3D Rotating Octahedral Gem at Peak (y = 22.8m)
            GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gem.name = "CP_Gem";
            gem.transform.SetParent(nodeRoot.transform, false);
            gem.transform.localPosition = new Vector3(0f, cpPillarH + 0.8f, 0f);
            gem.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            gem.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);
            var renGem = gem.GetComponent<Renderer>();
            if (renGem != null) renGem.material = glowMat;
            Collider cGem = gem.GetComponent<Collider>();
            if (cGem != null) Destroy(cGem);

            rotatingBeacons.Add(gem.transform);

            // 6. Floating High-Visibility HUD Badge at (y = 26.5m)
            GameObject hudRoot = new GameObject("HUD_Badge");
            hudRoot.transform.SetParent(nodeRoot.transform, false);
            hudRoot.transform.localPosition = new Vector3(0f, cpPillarH + 4.5f, 0f);

            string fullLabel = $"📍  {locItem.name.ToUpper()}";
            float badgeWidth = Mathf.Clamp(fullLabel.Length * 0.60f + 2.8f, 7.5f, 20.0f);
            float badgeHeight = 2.8f;

            // Outer Glowing Border
            GameObject borderQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            borderQuad.name = "Badge_Border";
            borderQuad.transform.SetParent(hudRoot.transform, false);
            borderQuad.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            borderQuad.transform.localScale = new Vector3(badgeWidth + 0.50f, badgeHeight + 0.45f, 1f);
            var renBorder = borderQuad.GetComponent<Renderer>();
            if (renBorder != null) renBorder.material = glowMat;
            Collider cBorder = borderQuad.GetComponent<Collider>();
            if (cBorder != null) Destroy(cBorder);

            // Inner Dark Plate
            GameObject bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgQuad.name = "Badge_Background";
            bgQuad.transform.SetParent(hudRoot.transform, false);
            bgQuad.transform.localPosition = new Vector3(0f, 0f, 0f);
            bgQuad.transform.localScale = new Vector3(badgeWidth, badgeHeight, 1f);
            var renBg = bgQuad.GetComponent<Renderer>();
            if (renBg != null) renBg.material = hudBadgeBgMat;
            Collider cBg = bgQuad.GetComponent<Collider>();
            if (cBg != null) Destroy(cBg);

            // Text Label
            GameObject textObj = new GameObject("Badge_Text");
            textObj.transform.SetParent(hudRoot.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.07f);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = fullLabel;
            textMesh.fontSize = 38;
            textMesh.characterSize = 0.32f;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = new Color(0.70f, 1.0f, 0.85f);

            activeBillboards.Add(hudRoot.transform);

            spawnedMarkers.Add(new MarkerEntry
            {
                data = locItem,
                rootObj = nodeRoot,
                labelObj = hudRoot,
                worldPos = pos
            });
        }

        private void BuildRoadLines()
        {
            foreach (Transform child in roadContainer.transform)
            {
                MeshFilter oldFilter = child.GetComponent<MeshFilter>();
                if (oldFilter != null && oldFilter.sharedMesh != null) Destroy(oldFilter.sharedMesh);
                Destroy(child.gameObject);
            }
            roadTooltipRoads.Clear();

            Terrain activeT = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            MeshCollider terrainMesh = MineTerrainLoader.Instance != null
                ? MineTerrainLoader.Instance.GetComponent<MeshCollider>() : null;
            int count = 0;

            foreach (var road in roadList)
            {
                if (count++ > 160) break;
                if (road.start_location == null || road.end_location == null) continue;

                Vector3 p1 = CalculateTerrainWorldPos(road.start_location.easting, road.start_location.northing, road.start_location.elevation);
                Vector3 p2 = CalculateTerrainWorldPos(road.end_location.easting, road.end_location.northing, road.end_location.elevation);

                float dist = Vector3.Distance(p1, p2);
                // Only connect genuine adjacent road nodes (max 450m) to prevent diagonal cross-pit lines
                if (dist > 450f || dist < 8f) continue;

                GameObject roadSegObj = new GameObject($"Road_{road.road_id}");
                roadSegObj.transform.SetParent(roadContainer.transform, false);

                MeshFilter meshFilter = roadSegObj.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = roadSegObj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = roadLineMat;
                meshFilter.sharedMesh = CreateRoadSurface(roadSegObj.transform, p1, p2,
                    GetVisualRoadWidth(road), activeT, terrainMesh, out Vector3 midPoint);

                string startN = string.IsNullOrEmpty(road.start_location.name) ? $"Loc-{road.start_location.location_id}" : road.start_location.name;
                string endN = string.IsNullOrEmpty(road.end_location.name) ? $"Loc-{road.end_location.location_id}" : road.end_location.name;

                spawnedRoads.Add(new RoadEntry
                {
                    data = road,
                    rootObj = roadSegObj,
                    meshFilter = meshFilter,
                    midPoint = midPoint,
                    routeName = $"{startN} ➔ {endN}"
                });
            }

            if (hasBackendRoadData)
            {
                float minSpacingSq = roadTooltipSpacingMeters * roadTooltipSpacingMeters;
                foreach (RoadEntry road in spawnedRoads)
                {
                    bool nearExisting = false;
                    foreach (RoadEntry marked in roadTooltipRoads)
                    {
                        Vector3 offset = road.midPoint - marked.midPoint;
                        offset.y = 0f;
                        if (offset.sqrMagnitude < minSpacingSq)
                        {
                            nearExisting = true;
                            break;
                        }
                    }
                    if (!nearExisting) roadTooltipRoads.Add(road);
                }
            }
        }

        private static float SampleRoadHeight(Vector3 point, Terrain terrain, MeshCollider terrainMesh)
        {
            if (terrain != null && terrain.isActiveAndEnabled && terrain.terrainData != null) return terrain.SampleHeight(point) + terrain.transform.position.y;
            if (terrainMesh != null)
            {
                Ray ray = new Ray(new Vector3(point.x, terrainMesh.bounds.max.y + 100f, point.z), Vector3.down);
                if (terrainMesh.Raycast(ray, out RaycastHit hit, terrainMesh.bounds.size.y + 200f))
                    return hit.point.y;
            }
            return point.y;
        }

        private static Mesh CreateRoadSurface(Transform root, Vector3 start, Vector3 end,
            float width, Terrain terrain, MeshCollider terrainMesh, out Vector3 midPoint)
        {
            Vector3 direction = end - start;
            direction.y = 0f;
            int lengthSections = Mathf.Clamp(Mathf.CeilToInt(direction.magnitude / 4f), 2, 120);
            int widthSections = Mathf.Clamp(Mathf.CeilToInt(width / 4f), 2, 12);
            int rowSize = widthSections + 1;
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
            var vertices = new Vector3[(lengthSections + 1) * rowSize];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[lengthSections * widthSections * 6];
            for (int i = 0; i <= lengthSections; i++)
            {
                float along = (float)i / lengthSections;
                Vector3 center = Vector3.Lerp(start, end, along);
                for (int j = 0; j <= widthSections; j++)
                {
                    float across = (float)j / widthSections;
                    Vector3 point = center + side * ((0.5f - across) * width);
                    point.y = SampleRoadHeight(point, terrain, terrainMesh) + 0.10f;
                    int vertex = i * rowSize + j;
                    vertices[vertex] = root.InverseTransformPoint(point);
                    uvs[vertex] = new Vector2(across, along);
                    if (i == lengthSections || j == widthSections) continue;
                    int next = vertex + rowSize;
                    int t = (i * widthSections + j) * 6;
                    triangles[t] = vertex;
                    triangles[t + 1] = vertex + 1;
                    triangles[t + 2] = next;
                    triangles[t + 3] = vertex + 1;
                    triangles[t + 4] = next + 1;
                    triangles[t + 5] = next;
                }
            }
            midPoint = root.TransformPoint(vertices[(lengthSections / 2) * rowSize + widthSections / 2]);
            var mesh = new Mesh { name = "RoadSurface" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private float GetVisualRoadWidth(RoadItem road)
        {
            float sourceWidth = road.lane_width >= 2 && road.lane_width <= 100
                ? road.lane_width : DefaultRoadWidthMeters;
            return sourceWidth * RoadVisualWidthMeters / DefaultRoadWidthMeters;
        }

        public void SetRoadColorPreset(int index)
        {
            RoadColorPresetIndex = Mathf.Clamp(index, 0, RoadColorPresets.Length - 1);
            PlayerPrefs.SetInt("Virexa_Road_Color", RoadColorPresetIndex);
            if (roadLineMat != null) roadLineMat.color = RoadVisualColor;
        }

        public void SetRoadVisualWidth(float widthMeters)
        {
            float value = Mathf.Clamp(Mathf.Round(widthMeters), 8f, 40f);
            if (Mathf.Approximately(RoadVisualWidthMeters, value)) return;
            RoadVisualWidthMeters = value;
            PlayerPrefs.SetFloat("Virexa_Road_Width", value);
            Terrain activeT = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            MeshCollider terrainMesh = MineTerrainLoader.Instance != null
                ? MineTerrainLoader.Instance.GetComponent<MeshCollider>() : null;
            foreach (RoadEntry road in spawnedRoads)
            {
                if (road.meshFilter == null || road.data == null ||
                    road.data.start_location == null || road.data.end_location == null) continue;
                Vector3 start = CalculateTerrainWorldPos(road.data.start_location.easting,
                    road.data.start_location.northing, road.data.start_location.elevation);
                Vector3 end = CalculateTerrainWorldPos(road.data.end_location.easting,
                    road.data.end_location.northing, road.data.end_location.elevation);
                Mesh previousMesh = road.meshFilter.sharedMesh;
                road.meshFilter.sharedMesh = CreateRoadSurface(road.meshFilter.transform, start, end,
                    GetVisualRoadWidth(road.data), activeT, terrainMesh, out Vector3 midPoint);
                road.midPoint = midPoint;
                if (previousMesh != null) Destroy(previousMesh);
            }
        }

        private void OnGUI()
        {
            if (!showRoads || roadContainer == null || !roadContainer.activeInHierarchy || roadTooltipRoads.Count == 0 ||
                Event.current.type != EventType.Repaint) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 10;
            RoadEntry hoveredRoad = null;
            Vector2 hoveredScreen = Vector2.zero;
            Vector2 pointer = Event.current.mousePosition;
            foreach (RoadEntry road in roadTooltipRoads)
            {
                if (road.rootObj == null || !road.rootObj.activeInHierarchy ||
                    Vector3.Distance(cam.transform.position, road.midPoint) > 3000f) continue;

                Vector3 projected = cam.WorldToScreenPoint(road.midPoint + Vector3.up * 2f);
                Vector2 screen = new Vector2(projected.x, Screen.height - projected.y);
                if (projected.z <= 0f || screen.x < 12f || screen.x > Screen.width - 12f ||
                    screen.y < 48f || screen.y > Screen.height - 12f) continue;

                bool hovered = (screen - pointer).sqrMagnitude <= 196f;
                float size = hovered ? 10f : 7f;
                GUI.color = new Color(0.05f, 0.09f, 0.11f, 0.95f);
                GUI.DrawTexture(new Rect(screen.x - size / 2f - 2f, screen.y - size / 2f - 2f,
                    size + 4f, size + 4f), Texture2D.whiteTexture);
                GUI.color = RoadVisualColor;
                GUI.DrawTexture(new Rect(screen.x - size / 2f, screen.y - size / 2f, size, size), Texture2D.whiteTexture);
                if (hovered)
                {
                    hoveredRoad = road;
                    hoveredScreen = screen;
                }
            }

            if (hoveredRoad != null && (FMSDashboardUI.Instance == null || !FMSDashboardUI.Instance.IsPointerOverUI()))
            {
                if (roadTooltipStyle == null)
                {
                    roadTooltipStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = Color.white }
                    };
                }
                float width = Mathf.Clamp(hoveredRoad.routeName.Length * 7f + 24f, 210f, 280f);
                float x = Mathf.Clamp(hoveredScreen.x + 14f, 8f, Screen.width - width - 8f);
                float y = Mathf.Clamp(hoveredScreen.y - 66f, 48f, Screen.height - 64f);
                GUI.color = new Color(0.04f, 0.08f, 0.10f, 0.94f);
                GUI.DrawTexture(new Rect(x, y, width, 58f), Texture2D.whiteTexture);
                GUI.color = RoadVisualColor;
                GUI.DrawTexture(new Rect(x, y, width, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 10f, y + 4f, width - 20f, 19f), hoveredRoad.routeName, roadTooltipStyle);
                GUI.Label(new Rect(x + 10f, y + 23f, width - 20f, 17f),
                    $"Ruas #{hoveredRoad.data.road_id}  |  Panjang {hoveredRoad.data.distance_m} m", roadTooltipStyle);
                string widthLabel = hoveredRoad.data.lane_width >= 2 && hoveredRoad.data.lane_width <= 100
                    ? $"Lebar visual {GetVisualRoadWidth(hoveredRoad.data):F0} m (data {hoveredRoad.data.lane_width} m)"
                    : $"Lebar visual ~{GetVisualRoadWidth(hoveredRoad.data):F0} m (estimasi)";
                GUI.Label(new Rect(x + 10f, y + 40f, width - 20f, 15f), widthLabel, roadTooltipStyle);
            }
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camUp = cam.transform.up;

                for (int i = activeBillboards.Count - 1; i >= 0; i--)
                {
                    Transform t = activeBillboards[i];
                    if (t == null)
                    {
                        activeBillboards.RemoveAt(i);
                        continue;
                    }

                    t.rotation = Quaternion.LookRotation(camForward, camUp);
                }
            }

            // Rotate 3D Beacon Crystal Heads
            float rotDeg = 40f * Time.deltaTime;
            for (int i = rotatingBeacons.Count - 1; i >= 0; i--)
            {
                Transform b = rotatingBeacons[i];
                if (b == null)
                {
                    rotatingBeacons.RemoveAt(i);
                    continue;
                }
                b.Rotate(Vector3.up, rotDeg, Space.World);
            }

            // Sync Marker Visibilities
            bool dispActive = showDisposals;
            bool frontActive = showFronts;
            bool cpActive = showCallPoints;
            bool roadActive = showRoads;

            if (disposalContainer != null && disposalContainer.activeSelf != dispActive)
                disposalContainer.SetActive(dispActive);

            if (frontContainer != null && frontContainer.activeSelf != frontActive)
                frontContainer.SetActive(frontActive);

            if (callpointContainer != null && callpointContainer.activeSelf != cpActive)
                callpointContainer.SetActive(cpActive);

            if (roadContainer != null && roadContainer.activeSelf != roadActive)
                roadContainer.SetActive(roadActive);

            // Individual Markers
            foreach (var m in spawnedMarkers)
            {
                if (m.rootObj != null)
                {
                    bool isLocHidden = hiddenLocationIds.Contains(m.data.location_id);
                    if (m.rootObj.activeSelf == isLocHidden)
                    {
                        m.rootObj.SetActive(!isLocHidden);
                    }

                    if (m.labelObj != null && m.labelObj.activeSelf != showLabels)
                    {
                        m.labelObj.SetActive(showLabels);
                    }
                }
            }

            // Individual Roads
            foreach (var r in spawnedRoads)
            {
                if (r.rootObj != null)
                {
                    bool isRoadHidden = hiddenRoadIds.Contains(r.data.road_id);
                    if (r.rootObj.activeSelf == isRoadHidden)
                    {
                        r.rootObj.SetActive(!isRoadHidden);
                    }
                }
            }
        }

        // ==========================================
        // PUBLIC CONTROLLER API
        // ==========================================

        public void ToggleAllMarkers()
        {
            showAllMarkers = !(showDisposals && showFronts && showCallPoints && showRoads);
            showDisposals = showAllMarkers;
            showFronts = showAllMarkers;
            showCallPoints = showAllMarkers;
            showRoads = showAllMarkers;
            showLabels = showAllMarkers;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showAllMarkers ? "👁️ Semua Titik Lokasi & Jalan: DITAMPILKAN" : "⚪ Semua Titik Lokasi & Jalan: DISEMBUNYIKAN");
        }

        public void ToggleLabels()
        {
            showLabels = !showLabels;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showLabels ? "🏷️ Label Nama Lokasi: DITAMPILKAN" : "⚪ Label Nama Lokasi: DISEMBUNYIKAN");
        }

        public void ToggleDisposals()
        {
            showDisposals = !showDisposals;
            showAllMarkers = showDisposals && showFronts && showCallPoints && showRoads;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showDisposals ? "🚜 Layer Titik Disposal: DIAKTIFKAN" : "⚪ Layer Titik Disposal: DINONAKTIFKAN");
        }

        public void ToggleFronts()
        {
            showFronts = !showFronts;
            showAllMarkers = showDisposals && showFronts && showCallPoints && showRoads;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showFronts ? "⛏️ Layer Front Loading: DIAKTIFKAN" : "⚪ Layer Front Loading: DINONAKTIFKAN");
        }

        public void ToggleCallPoints()
        {
            showCallPoints = !showCallPoints;
            showAllMarkers = showDisposals && showFronts && showCallPoints && showRoads;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showCallPoints ? "📍 Layer Simpang / CallPoint: DIAKTIFKAN" : "⚪ Layer Simpang: DINONAKTIFKAN");
        }

        public void ToggleRoads()
        {
            showRoads = !showRoads;
            showAllMarkers = showDisposals && showFronts && showCallPoints && showRoads;
            if (FMSDashboardUI.Instance != null)
                FMSDashboardUI.Instance.ShowNotification(showRoads ? "🛣️ Layer Jaringan Jalan Hauling: DIAKTIFKAN" : "⚪ Layer Jalan Hauling: DINONAKTIFKAN");
        }

        public void SetMarkerVisible(long locationId, bool visible)
        {
            if (visible)
            {
                hiddenLocationIds.Remove(locationId);
                showAllMarkers = true;
            }
            else
            {
                hiddenLocationIds.Add(locationId);
            }

            foreach (var m in spawnedMarkers)
            {
                if (m.data.location_id == locationId && m.rootObj != null)
                {
                    m.rootObj.SetActive(visible);
                    break;
                }
            }
        }

        public bool IsMarkerVisible(long locationId)
        {
            return !hiddenLocationIds.Contains(locationId);
        }

        public void SelectAllIndividualMarkers(bool selectAll)
        {
            if (selectAll)
            {
                hiddenLocationIds.Clear();
                showAllMarkers = true;
                foreach (var m in spawnedMarkers)
                {
                    if (m.rootObj != null) m.rootObj.SetActive(true);
                }
            }
            else
            {
                foreach (var m in spawnedMarkers)
                {
                    hiddenLocationIds.Add(m.data.location_id);
                    if (m.rootObj != null) m.rootObj.SetActive(false);
                }
            }
        }

        public void FocusOnLocation(long locationId)
        {
            foreach (var m in spawnedMarkers)
            {
                if (m.data.location_id == locationId)
                {
                    FMSCameraController.Instance?.JumpTo(m.worldPos, 450f);
                    if (FMSDashboardUI.Instance != null)
                        FMSDashboardUI.Instance.ShowNotification($"🎯 Kamera difokuskan ke: {m.data.name}");
                    return;
                }
            }
        }

        // ==========================================
        // ROAD SELECTION CONTROLLER
        // ==========================================

        public void SetRoadVisible(long roadId, bool visible)
        {
            if (visible)
            {
                hiddenRoadIds.Remove(roadId);
                showRoads = true;
            }
            else
            {
                hiddenRoadIds.Add(roadId);
            }

            foreach (var r in spawnedRoads)
            {
                if (r.data.road_id == roadId && r.rootObj != null)
                {
                    r.rootObj.SetActive(visible);
                    break;
                }
            }
        }

        public bool IsRoadVisible(long roadId)
        {
            return !hiddenRoadIds.Contains(roadId);
        }

        public void SelectAllRoads(bool selectAll)
        {
            if (selectAll)
            {
                hiddenRoadIds.Clear();
                showRoads = true;
                foreach (var r in spawnedRoads)
                {
                    if (r.rootObj != null) r.rootObj.SetActive(true);
                }
            }
            else
            {
                foreach (var r in spawnedRoads)
                {
                    hiddenRoadIds.Add(r.data.road_id);
                    if (r.rootObj != null) r.rootObj.SetActive(false);
                }
            }
        }

        public void FocusOnRoad(long roadId)
        {
            foreach (var r in spawnedRoads)
            {
                if (r.data.road_id == roadId)
                {
                    FMSCameraController.Instance?.JumpTo(r.midPoint, 550f);
                    if (FMSDashboardUI.Instance != null)
                        FMSDashboardUI.Instance.ShowNotification($"🛣️ Kamera difokuskan ke Ruas Jalan: {r.routeName}");
                    return;
                }
            }
        }
    }
}
