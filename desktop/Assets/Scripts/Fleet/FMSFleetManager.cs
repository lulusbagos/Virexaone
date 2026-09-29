using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    [System.Serializable]
    public class UnityPosDto
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class TrajectoryPointDto
    {
        public float x;
        public float y;
        public float z;
        public float speed_kmh;
        public float heading_deg;
        public string recorded_at;
    }

    [System.Serializable]
    public class LiveFleetUnitDto
    {
        public long unit_id;
        public string unit_name;
        public string unit_type;
        public string category;
        public long status_id;
        public long activity_id;
        public string activity_name;
        public long operator_id;
        public long equipment_type_id;
        public float reference_length_m;
        public bool is_active;
        public float speed_kmh;
        public float heading_deg;
        public double latitude;
        public double longitude;
        public double easting;
        public double northing;
        public double elevation;
        public UnityPosDto unity_pos;
        public List<TrajectoryPointDto> recent_trajectory;
        public bool haul_data_available;
        public bool has_payload;
        public int recorded_loads;
        public bool payload_available;
        public float payload_ton;
        public string assigned_shovel_name;
        public string haul_updated_at;
        public string last_heard;
        public int last_heard_seconds_ago;
    }

    [System.Serializable]
    public class LiveFleetResponseDto
    {
        public string status;
        public string server_time;
        public bool feed_stale;
        public int feed_age_seconds;
        public int count;
        public List<LiveFleetUnitDto> data;
    }

    public class FMSFleetManager : MonoBehaviour
    {
        public static FMSFleetManager Instance { get; private set; }

        [Header("Fleet Settings")]
        public bool spawnOnStart = true;
        public bool showFleet = true;
        public bool hideOfflineUnits = false; // Keep units visible; offline state is shown by status instead of hiding objects.
        public bool showArchivedUnits = false;
        public bool syncWithBackend = true;
        public bool isSimulationMode = false; // Default FALSE: Strict live GPS API mode (Unit stops when API is offline)
        public bool isTelemetryFeedStale = true;
        public int telemetryFeedAgeSeconds;

        [Header("Fleet Category Visibility Filters")]
        public bool filterShowHaulTrucks = true;
        public bool filterShowExcavators = true;
        public bool filterShowBulldozers = true;
        public bool filterShowGraders = true;
        public bool filterShowWheelLoaders = true;
        public bool filterShowFuelTrucks = true;

        public List<FMSUnitController> activeFleet = new List<FMSUnitController>();
        public FMSUnitController selectedUnit;

        [Header("Production Statistics")]
        public float totalTonnageMoved = 0f;
        public int totalCompletedCycles = 0;
        public float fleetProductivityPerHour = 0f;

        [Header("Containers")]
        private GameObject fleetRoot;

        [Header("Right-Click Context Menu & Fleet Isolation")]
        public bool isContextMenuOpen = false;
        public FMSUnitController contextMenuUnit;
        public Vector2 contextMenuScreenPos = Vector2.zero;

        public bool isFleetIsolated = false;
        public string isolatedLoaderId = "";

        [Header("Fleet Route Breadcrumb & Heatmap")]
        public bool showFleetHeatmap = false;
        public string heatmapLoaderId = "";
        private LineRenderer heatmapLineRenderer;

        public void ApplyFleetCategoryFilters()
        {
            if (activeFleet == null) return;
            foreach (var u in activeFleet)
            {
                if (u == null) continue;
                bool catVisible = true;
                switch (u.unitType)
                {
                    case UnitType.HaulTruck: catVisible = filterShowHaulTrucks; break;
                    case UnitType.Excavator: catVisible = filterShowExcavators; break;
                    case UnitType.Bulldozer: catVisible = filterShowBulldozers; break;
                    case UnitType.Grader: catVisible = filterShowGraders; break;
                    case UnitType.WheelLoader: catVisible = filterShowWheelLoaders; break;
                    case UnitType.FuelTruck: catVisible = filterShowFuelTrucks; break;
                }

                if ((!showArchivedUnits && u.isLiveTelemetryControlled && u.backendLastHeardSeconds > 86400 && u != selectedUnit) ||
                    (hideOfflineUnits && (!u.isOnline || u.currentState == UnitState.Offline)))
                {
                    catVisible = false;
                }

                u.gameObject.SetActive(showFleet && catVisible);
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            // Strictly enforce Live GPS Mode (No autonomous simulated motion when API is down)
            isSimulationMode = false;
            hideOfflineUnits = false;
        }

        private void Start()
        {
            isSimulationMode = false;
            hideOfflineUnits = false;

            if (spawnOnStart && isSimulationMode)
            {
                StartCoroutine(InitFleetCoroutine());
            }

            if (syncWithBackend)
            {
                StartCoroutine(SyncBackendFleetCoroutine());
            }
        }

        public void SetSimulationMode(bool enabled)
        {
            isSimulationMode = false;
        }

        private UnityWebRequest activeFleetRequest;

        private void OnDisable()
        {
            if (activeFleetRequest != null)
            {
                activeFleetRequest.Abort();
                activeFleetRequest = null;
            }
        }

        private IEnumerator SyncBackendFleetCoroutine()
        {
            yield return new WaitForSeconds(1.0f);

            while (true)
            {
                string backendUrl = (FMSDashboardUI.Instance != null && !string.IsNullOrEmpty(FMSDashboardUI.Instance.apiBaseUrl))
                    ? FMSDashboardUI.Instance.apiBaseUrl.TrimEnd('/')
                    : string.Empty;

                if (string.IsNullOrEmpty(backendUrl))
                {
                    SetTelemetryFeedStale(true);
                    yield return new WaitForSeconds(5f);
                    continue;
                }

                UnityWebRequest req = null;
                try
                {
                    req = UnityWebRequest.Get($"{backendUrl}/api/v1/fleet/live");
                    FMSApiSession.Authorize(req);
                    req.timeout = 40;
                }
                catch { req = null; }

                if (req != null)
                {
                    using (req)
                    {
                        activeFleetRequest = req;
                        UnityWebRequestAsyncOperation op = null;
                        try { op = req.SendWebRequest(); } catch { op = null; }

                        if (op != null) yield return op;

                        activeFleetRequest = null;

                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                LiveFleetResponseDto resp = JsonUtility.FromJson<LiveFleetResponseDto>(req.downloadHandler.text);
                                if (resp != null)
                                {
                                    SetTelemetryFeedStale(resp.feed_stale || resp.status != "success");
                                    telemetryFeedAgeSeconds = resp.feed_age_seconds;
                                    if (resp.data != null && resp.data.Count > 0)
                                        UpdateFleetFromBackend(resp.data, resp.server_time);
                                }
                                else SetTelemetryFeedStale(true);
                            }
                            catch (Exception ex)
                            {
                                SetTelemetryFeedStale(true);
                                Debug.LogWarning($"[FMSFleetManager] Failed to parse live fleet json: {ex.Message}");
                            }
                        }
                        else
                        {
                            SetTelemetryFeedStale(true);
                            Debug.LogWarning($"[FMSFleetManager] Live fleet request failed: {req.error}");
                        }
                    }
                }
                else SetTelemetryFeedStale(true);

                yield return new WaitForSeconds(1.5f);
            }
        }

        private void SetTelemetryFeedStale(bool stale)
        {
            if (isTelemetryFeedStale == stale) return;
            isTelemetryFeedStale = stale;
            foreach (var unit in activeFleet)
            {
                if (unit != null) unit.UpdateStatusColor();
            }
        }

        private static float ResolveVisualLength(float reportedLength, FMSUnitAssetManager.UnitCategory category)
        {
            float fallback;
            float minimum;
            float maximum;
            switch (category)
            {
                case FMSUnitAssetManager.UnitCategory.HaulerEmpty:
                case FMSUnitAssetManager.UnitCategory.HaulerLoaded:
                    fallback = 10f; minimum = 8f; maximum = 14f; break;
                case FMSUnitAssetManager.UnitCategory.Excavator:
                    fallback = 17f; minimum = 10f; maximum = 22f; break;
                case FMSUnitAssetManager.UnitCategory.Bulldozer:
                    fallback = 9.2f; minimum = 6f; maximum = 12f; break;
                case FMSUnitAssetManager.UnitCategory.Grader:
                    fallback = 11.5f; minimum = 8f; maximum = 15f; break;
                case FMSUnitAssetManager.UnitCategory.FuelTruck:
                    fallback = 9.2f; minimum = 6f; maximum = 14f; break;
                case FMSUnitAssetManager.UnitCategory.WheelLoader:
                    fallback = 12.5f; minimum = 8f; maximum = 17f; break;
                default:
                    fallback = 4f; minimum = 2f; maximum = 8f; break;
            }
            return float.IsNaN(reportedLength) || float.IsInfinity(reportedLength) ||
                reportedLength < minimum || reportedLength > maximum ? fallback : reportedLength;
        }

        private FMSUnitController FindOrSpawnBackendUnit(LiveFleetUnitDto bUnit, bool allowSpawn)
        {
            if (bUnit == null || string.IsNullOrEmpty(bUnit.unit_name)) return null;

            string cleanBName = bUnit.unit_name.Replace("-", "").Replace("_", "").Replace(" ", "").Trim();

            string digitsB = new string(System.Array.FindAll(cleanBName.ToCharArray(), char.IsDigit));

            FMSUnitController match = activeFleet.Find(u => {
                if (u == null) return false;
                if (u.backendEquipmentId > 0 && bUnit.unit_id > 0)
                    return u.backendEquipmentId == bUnit.unit_id;

                string cleanUName = (u.unitName ?? "").Replace("-", "").Replace("_", "").Replace(" ", "").Trim();
                string cleanUId = (u.unitId ?? "").Replace("-", "").Replace("_", "").Replace(" ", "").Trim();

                if (cleanUName.Equals(cleanBName, StringComparison.OrdinalIgnoreCase) || 
                    cleanUId.Equals(cleanBName, StringComparison.OrdinalIgnoreCase)) return true;

                // Only RD/DT haul-truck aliases may share a numeric identity.
                string digitsU = new string(System.Array.FindAll(cleanUName.ToCharArray(), char.IsDigit));
                if (string.IsNullOrEmpty(digitsU)) digitsU = new string(System.Array.FindAll(cleanUId.ToCharArray(), char.IsDigit));
                bool backendTruckAlias = cleanBName.StartsWith("RD", StringComparison.OrdinalIgnoreCase) ||
                    cleanBName.StartsWith("DT", StringComparison.OrdinalIgnoreCase);
                bool localTruckAlias = cleanUName.StartsWith("RD", StringComparison.OrdinalIgnoreCase) ||
                    cleanUName.StartsWith("DT", StringComparison.OrdinalIgnoreCase) ||
                    cleanUId.StartsWith("RD", StringComparison.OrdinalIgnoreCase) ||
                    cleanUId.StartsWith("DT", StringComparison.OrdinalIgnoreCase);
                if (u.unitType == UnitType.HaulTruck && backendTruckAlias && localTruckAlias &&
                    digitsB.Length >= 4 && digitsB == digitsU)
                {
                    // Synchronize name to match exact backend naming
                    u.unitName = bUnit.unit_name;
                    u.unitId = bUnit.unit_name;
                    return true;
                }

                return false;
            });

            if (match != null)
            {
                match.backendEquipmentId = bUnit.unit_id;
                match.unitName = bUnit.unit_name;
                match.unitId = bUnit.unit_name;
                return match;
            }

            if (!allowSpawn) return null;

            // Dynamically spawn new 3D unit if it exists in API but wasn't in the initial layout
            if (fleetRoot == null)
            {
                fleetRoot = new GameObject("FleetLayerContainer");
                fleetRoot.transform.SetParent(transform, false);
            }

            bool payloadFlag = bUnit.has_payload || (bUnit.payload_available && bUnit.payload_ton > 0.1f);
            FMSUnitAssetManager.UnitCategory cat = payloadFlag
                ? FMSUnitAssetManager.UnitCategory.HaulerLoaded : FMSUnitAssetManager.UnitCategory.HaulerEmpty;
            string catStr = (bUnit.category ?? bUnit.unit_type ?? "").ToLower();
            string nameUpper = bUnit.unit_name.ToUpper();

            if (catStr.Contains("excavator") || catStr.Contains("shovel") || nameUpper.StartsWith("EX")) cat = FMSUnitAssetManager.UnitCategory.Excavator;
            else if (catStr.Contains("dozer") || nameUpper.StartsWith("DZ") || nameUpper.StartsWith("BD")) cat = FMSUnitAssetManager.UnitCategory.Bulldozer;
            else if (catStr.Contains("grader") || nameUpper.StartsWith("MG") || nameUpper.StartsWith("GD")) cat = FMSUnitAssetManager.UnitCategory.Grader;
            else if (catStr.Contains("loader") || nameUpper.StartsWith("WL") || nameUpper.StartsWith("WA")) cat = FMSUnitAssetManager.UnitCategory.WheelLoader;
            else if (catStr.Contains("fuel") || nameUpper.StartsWith("FT")) cat = FMSUnitAssetManager.UnitCategory.FuelTruck;
            else if (catStr.Contains("haul") || catStr.Contains("truck") ||
                nameUpper.StartsWith("RD") || nameUpper.StartsWith("DT"))
                cat = payloadFlag ? FMSUnitAssetManager.UnitCategory.HaulerLoaded : FMSUnitAssetManager.UnitCategory.HaulerEmpty;
            else cat = FMSUnitAssetManager.UnitCategory.Support;

            string model = cat == FMSUnitAssetManager.UnitCategory.Excavator ? "Excavator / Shovel"
                : cat == FMSUnitAssetManager.UnitCategory.Bulldozer ? "Bulldozer"
                : cat == FMSUnitAssetManager.UnitCategory.Grader ? "Motor Grader"
                : cat == FMSUnitAssetManager.UnitCategory.WheelLoader ? "Wheel Loader"
                : cat == FMSUnitAssetManager.UnitCategory.FuelTruck ? "Fuel / Support Truck"
                : cat == FMSUnitAssetManager.UnitCategory.Support ? (bUnit.unit_type ?? "Support") : "Haul Truck";
            string op = bUnit.operator_id > 0 ? $"Operator ID {bUnit.operator_id}" : "Operator --";

            GameObject newObj = FMS3DModelGenerator.CreateUnit(cat, bUnit.unit_name, model, op);
            if (cat == FMSUnitAssetManager.UnitCategory.Support && bUnit.reference_length_m > 0f)
            {
                Transform marker = newObj.transform.Find("VisualModel/UnmappedUnitMarker");
                if (marker != null)
                {
                    float length = Mathf.Clamp(bUnit.reference_length_m, 2f, 30f);
                    marker.localScale = new Vector3(Mathf.Max(2f, length * 0.4f),
                        Mathf.Min(3f, length * 0.3f), length);
                    BoxCollider body = newObj.GetComponent<BoxCollider>();
                    if (body != null) body.size = marker.localScale + Vector3.one;
                }
            }
            var modelLoader = newObj.GetComponentInChildren<FMSGlbModelLoader>();
            float visualLength = ResolveVisualLength(bUnit.reference_length_m, cat);
            modelLoader?.SetReferenceLength(visualLength);
            newObj.transform.SetParent(fleetRoot.transform, false);

            FMSUnitController newCtrl = newObj.GetComponent<FMSUnitController>();
            newCtrl.backendEquipmentId = bUnit.unit_id;
            newCtrl.unitId = bUnit.unit_name;
            newCtrl.unitName = bUnit.unit_name;
            newCtrl.modelName = model;
            newCtrl.operatorName = op;
            newCtrl.assignedLoaderId = bUnit.assigned_shovel_name ?? "";
            newCtrl.assignedLoaderModel = "";
            newCtrl.assignedFrontName = "";
            newCtrl.lastLoadedByLoaderId = "";
            newCtrl.lastLoadedByLoaderModel = "";
            newCtrl.lastLoadedLocation = "";
            newCtrl.totalDistanceKm = 0f;
            newCtrl.backendEquipmentTypeId = bUnit.equipment_type_id;
            newCtrl.referenceLengthMeters = visualLength;
            newCtrl.backendStatusId = bUnit.status_id;
            newCtrl.backendActivityId = bUnit.activity_id;
            newCtrl.payloadTons = 0f;
            newCtrl.hasActualPayload = false;
            newCtrl.hasActualHaulData = false;
            newCtrl.isOnline = bUnit.is_active;
            newCtrl.currentState = (bUnit.speed_kmh > 0.5f) ? UnitState.Hauling : (bUnit.is_active ? UnitState.Idle : UnitState.Offline);
            newCtrl.timeSinceLastGps = 0f;

            activeFleet.Add(newCtrl);
            return newCtrl;
        }

        private void UpdateFleetFromBackend(List<LiveFleetUnitDto> backendUnits, string serverTime)
        {
            if (backendUnits == null || activeFleet == null) return;

            // Do not destroy units during live polling. Some devices briefly disappear from
            // a response or change RD/DT prefixes; keeping objects avoids visual blinking.
            for (int i = activeFleet.Count - 1; i >= 0; i--)
            {
                if (activeFleet[i] == null)
                {
                    activeFleet.RemoveAt(i);
                }
            }

            foreach (var bUnit in backendUnits)
            {
                if (bUnit == null) continue;

                // Find matching unit by name (e.g. "RD5001" or "EX-201") or spawn if new
                bool hasValidGps = bUnit.latitude != 0 && bUnit.longitude != 0 && bUnit.unity_pos != null &&
                    !float.IsNaN(bUnit.unity_pos.x) && !float.IsNaN(bUnit.unity_pos.z) &&
                    Mathf.Abs(bUnit.unity_pos.x) < 50000f && Mathf.Abs(bUnit.unity_pos.z) < 50000f;
                bool archived = bUnit.last_heard_seconds_ago > 86400;
                FMSUnitController match = FindOrSpawnBackendUnit(bUnit, hasValidGps && (!archived || showArchivedUnits));

                if (match != null)
                {
                    if (match.unitType == UnitType.HaulTruck)
                    {
                        bool loaded = bUnit.has_payload || (bUnit.payload_available && bUnit.payload_ton > 0.1f);
                        match.unitCategory = loaded ? FMSUnitAssetManager.UnitCategory.HaulerLoaded
                            : FMSUnitAssetManager.UnitCategory.HaulerEmpty;
                        var modelLoader = match.GetComponentInChildren<FMSGlbModelLoader>();
                        modelLoader?.SetModelVariant(loaded ? "truck_loaded_draco.glb" : "truck_empty_draco.glb");
                    }
                    float visualLength = ResolveVisualLength(bUnit.reference_length_m, match.unitCategory);
                    match.GetComponentInChildren<FMSGlbModelLoader>()?.SetReferenceLength(visualLength);
                    match.backendEquipmentTypeId = bUnit.equipment_type_id;
                    match.referenceLengthMeters = visualLength;
                    match.backendStatusId = bUnit.status_id;
                    match.backendActivityId = bUnit.activity_id;
                    match.operatorName = bUnit.operator_id > 0 ? $"Operator ID {bUnit.operator_id}" : "Operator --";
                    match.hasActualHaulData = bUnit.haul_data_available;
                    match.recordedLoads = bUnit.haul_data_available ? Mathf.Max(0, bUnit.recorded_loads) : 0;
                    match.completedTripsCount = 0;
                    match.hasActualPayload = bUnit.payload_available;
                    match.payloadTons = bUnit.payload_available ? Mathf.Max(0f, bUnit.payload_ton) : 0f;
                    match.assignedLoaderId = bUnit.assigned_shovel_name ?? "";
                    match.lastLoadedByLoaderId = "";
                    Vector3 rawPos = hasValidGps 
                        ? new Vector3(bUnit.unity_pos.x, bUnit.unity_pos.y, bUnit.unity_pos.z) 
                        : match.transform.position;

                    // Set Live Diagnostic Telemetry Metrics
                    match.secondsSinceLastBackendUpdate = 0f;
                    match.backendLastHeardSeconds = bUnit.last_heard_seconds_ago;
                    match.hasValidGpsFix = hasValidGps;

                    // Strict Live GPS Mode
                    if (hasValidGps)
                    {
                        match.latestGpsEasting = bUnit.easting;
                        match.latestGpsNorthing = bUnit.northing;
                        match.latestGpsElevation = bUnit.elevation;
                        match.latestGpsLatitude = bUnit.latitude;
                        match.latestGpsLongitude = bUnit.longitude;
                        match.UpdateFromLiveTelemetry(rawPos, bUnit.speed_kmh, bUnit.heading_deg,
                            bUnit.activity_name, bUnit.is_active, bUnit.recent_trajectory, bUnit.last_heard, serverTime);
                        match.lastRecordedGpsPos = rawPos;
                    }
                    else
                    {
                        string offlineReason = !bUnit.is_active ? "Offline (Device Inactive)" : "Offline (No GPS / Coord NULL)";
                        match.UpdateFromLiveTelemetry(match.transform.position, 0f, match.headingDegrees, offlineReason, false);
                        match.lastRecordedGpsPos = match.transform.position;
                    }
                    match.GetCurrentStoppageReason();
                }
            }

            ApplyFleetCategoryFilters();
        }

        private IEnumerator InitFleetCoroutine()
        {
            // Wait briefly for terrain and spatial metadata to finish loading
            yield return new WaitForSeconds(0.4f);

            if (isSimulationMode)
            {
                SpawnFleetUnits();
            }
        }

        public void SpawnFleetUnits()
        {
            // Clear any existing spawned units
            ClearFleet();

            if (fleetRoot == null)
            {
                fleetRoot = new GameObject("FleetLayerContainer");
                fleetRoot.transform.SetParent(transform, false);
            }

            // 1. Prepare Haul Routes
            Dictionary<string, List<Vector3>> routes = null;
            if (MineSpatialManager.Instance != null && MineSpatialManager.Instance.RouteWaypoints != null && MineSpatialManager.Instance.RouteWaypoints.Count > 0)
            {
                routes = MineSpatialManager.Instance.RouteWaypoints;
            }

            // Fallback synthetic routes if metadata routes not loaded
            if (routes == null || routes.Count == 0)
            {
                routes = GenerateDefaultMineRoutes();
            }

            // 2. Spawn Dump Trucks (HaulerEmpty & HaulerLoaded)
            SpawnHaulTrucks(routes);

            // 3. Spawn Excavator Shovels at Pit Loading Faces
            SpawnExcavators();

            // 4. Spawn Heavy Bulldozers at Disposal Benches
            SpawnBulldozers();

            // 5. Spawn Motor Graders along Haul Corridors
            SpawnGraders(routes);

            // 6. Spawn Wheel Loaders at Stockpile Areas
            SpawnWheelLoaders();

            // 7. Spawn Fuel & Service Support Trucks
            SpawnFuelTrucks();

            // Apply visibility
            SetFleetVisibility(showFleet);

            Debug.Log($"[FMSFleetManager] Successfully spawned full operational fleet of {activeFleet.Count} mining units.");
        }

        private (Vector3 pos, int nextWpIndex, Vector3 travelDir) SampleRoutePosition(List<Vector3> wps, float fraction, bool isForward, float laneOffset)
        {
            if (wps == null || wps.Count == 0) return (Vector3.zero, 0, Vector3.forward);
            if (wps.Count == 1) return (wps[0], 0, Vector3.forward);

            // Calculate total route polyline length
            float totalLen = 0f;
            float[] segLens = new float[wps.Count - 1];
            for (int k = 0; k < wps.Count - 1; k++)
            {
                segLens[k] = Vector3.Distance(wps[k], wps[k + 1]);
                totalLen += segLens[k];
            }

            if (totalLen <= 0.01f) return (wps[0], 0, Vector3.forward);

            // If reverse route (travelling empty to pit), start from opposite fraction
            float targetDist = Mathf.Clamp01(fraction) * totalLen;
            if (!isForward)
            {
                targetDist = (1f - Mathf.Clamp01(fraction)) * totalLen;
            }

            float accumulated = 0f;
            for (int k = 0; k < segLens.Length; k++)
            {
                if (accumulated + segLens[k] >= targetDist || k == segLens.Length - 1)
                {
                    float segFrac = (segLens[k] > 0.01f) ? ((targetDist - accumulated) / segLens[k]) : 0f;
                    segFrac = Mathf.Clamp01(segFrac);

                    Vector3 basePos = Vector3.Lerp(wps[k], wps[k + 1], segFrac);
                    Vector3 segDir = (wps[k + 1] - wps[k]);
                    segDir.y = 0f;
                    if (segDir.sqrMagnitude < 0.001f) segDir = Vector3.forward;
                    segDir.Normalize();

                    Vector3 travelDir = isForward ? segDir : -segDir;
                    Vector3 leftNormal = Vector3.Cross(Vector3.up, travelDir).normalized;
                    Vector3 finalPos = basePos + leftNormal * laneOffset;

                    int nextWpIndex = isForward ? (k + 1) : k;
                    return (finalPos, nextWpIndex, travelDir);
                }
                accumulated += segLens[k];
            }

            return (wps[0], 0, Vector3.forward);
        }

        private void SpawnHaulTrucks(Dictionary<string, List<Vector3>> routes)
        {
            string[] truckModels = new string[] { "CAT 777E", "Komatsu HD785-7", "Hitachi EH1100", "CAT 785D", "CAT 777D", "Komatsu HD465-7" };
            string[] operatorNames = new string[] 
            { 
                "Ahmad S.", "Budi Santoso", "Deni Saputra", "Eko Prasetyo", "Fajar Nugraha", "Guntur W.", "Hadi Pranoto", "Imam Malik",
                "Joko Susilo", "Kurniawan", "Lukman H.", "Miftah F.", "Nanang S.", "Oki Setiawan", "Panji Tri", "Qomaruddin",
                "Rian H.", "Samsul Hadi", "Taufik I.", "Untung S.", "Vicky P.", "Wahyu R.", "Yusuf K.", "Zainal A."
            };

            int truckIdx = 0;
            int totalTrucksTarget = 110; // Target 110 Haul Trucks out of 156 units
            int trucksPerRoute = Mathf.CeilToInt((float)totalTrucksTarget / Mathf.Max(1, routes.Count));

            var routeLoaderMap = new Dictionary<string, (string exId, string exModel, string front, string disposal)>
            {
                { "ROUTE_PANEL_EAST_TO_DISPOSAL_NORTH", ("EX-201", "Komatsu PC2000-8", "Front Pit East (Face-01)", "North Disposal Dump") },
                { "ROUTE_T6U_TO_DISPOSAL_NORTH", ("EX-202", "Hitachi EX1200-6", "Front Pit West T6U", "North Disposal Dump") },
                { "ROUTE_T6S33_TO_DISPOSAL_MAIN", ("EX-203", "CAT 6020B Mining Shovel", "Front Pit South T6S33", "Main South Disposal") },
                { "ROUTE_DEEP_SUMP_TO_INPIT_BACKFILL", ("EX-204", "Komatsu PC2000-8", "Deep Pit Sump Face", "Central In-Pit Backfill") },
                { "ROUTE_COAL_SEAM_TO_ROM_CRUSHER", ("EX-205", "Hitachi EX2600-6", "Central Coal Seam Front", "ROM Stockpile Crusher") },
                { "ROUTE_WEST_RIDGE_TO_WEST_OUTPIT", ("EX-206", "CAT 6040 Hydraulic Shovel", "West Ridge Highwall", "West Outpit Disposal") },
                { "ROUTE_EAST_BENCH_TO_EAST_DUMP", ("EX-207", "Komatsu PC1250-8", "East Bench +160 Face", "East High Dump") },
                { "ROUTE_NORTH_RAMP_TO_CPP_PLANT", ("EX-208", "Hitachi EX1200-6", "North Ramp Pocket", "Central Prep Plant (CPP)") },
                { "ROUTE_SOUTH_RAMP_TO_SOUTH_DUMP", ("EX-209", "CAT 6020B Mining Shovel", "South Pit Ramp", "South Disposal Ridge") },
                { "ROUTE_CENTRAL_TRUNK_TO_WORKSHOP", ("EX-210", "Komatsu PC2000-8", "Central Haul Bench", "Main Workshop Bay") }
            };

            foreach (var kvp in routes)
            {
                List<Vector3> wps = kvp.Value;
                if (wps == null || wps.Count < 2) continue;

                // Determine loader pairing
                string assignedEx = "EX-201";
                string assignedModel = "Komatsu PC2000-8";
                string assignedFront = "Front Pit East";
                string assignedDisp = "North Disposal Dump";

                if (routeLoaderMap.TryGetValue(kvp.Key, out var rInfo))
                {
                    assignedEx = rInfo.exId;
                    assignedModel = rInfo.exModel;
                    assignedFront = rInfo.front;
                    assignedDisp = rInfo.disposal;
                }

                for (int i = 0; i < trucksPerRoute; i++)
                {
                    if (truckIdx >= totalTrucksTarget) break;

                    truckIdx++;
                    string unitId = $"RD{5090 + truckIdx}";
                    string model = truckModels[truckIdx % truckModels.Length];
                    string op = operatorNames[truckIdx % operatorNames.Length];
                    bool isLoaded = (i % 2 == 0);

                    GameObject truckObj = FMS3DModelGenerator.CreateHaulTruck(unitId, model, op, isLoaded);
                    truckObj.transform.SetParent(fleetRoot.transform, false);

                    FMSUnitController ctrl = truckObj.GetComponent<FMSUnitController>();
                    ctrl.unitId = unitId;
                    ctrl.unitName = unitId;
                    ctrl.waypoints = new List<Vector3>(wps);

                    // Fleet Loader Pairing
                    ctrl.assignedLoaderId = assignedEx;
                    ctrl.assignedLoaderModel = assignedModel;
                    ctrl.assignedFrontName = assignedFront;
                    ctrl.assignedDisposalName = assignedDisp;
                    ctrl.lastLoadedByLoaderId = assignedEx;
                    ctrl.lastLoadedByLoaderModel = assignedModel;
                    ctrl.lastLoadedLocation = assignedFront;
                    ctrl.lastLoadedTimestamp = Time.time;
                    ctrl.completedTripsCount = 0;

                    // Continuous spatial staggering along polyline to prevent initial anti-collision lock
                    float routeFraction = (i + 0.5f) / (float)trucksPerRoute;
                    var sample = SampleRoutePosition(wps, routeFraction, isLoaded, ctrl.laneOffsetDistance);

                    ctrl.isForwardRoute = isLoaded;
                    ctrl.currentWaypointIndex = sample.nextWpIndex;
                    ctrl.transform.position = sample.pos;
                    ctrl.transform.rotation = Quaternion.LookRotation(sample.travelDir, Vector3.up);
                    ctrl.currentTargetPoint = sample.pos;
                    ctrl.hasTarget = false;
                    ctrl.isOnline = false;
                    ctrl.currentState = UnitState.Offline;
                    ctrl.payloadTons = isLoaded ? 95f : 0f;
                    ctrl.targetSpeedKmh = 0f;
                    ctrl.currentSpeedKmh = 0f;
                    ctrl.timeSinceLastGps = 999f;
                    ctrl.AlignToGround(instant: true);

                    activeFleet.Add(ctrl);
                }

                if (truckIdx >= totalTrucksTarget) break;
            }
        }

        private void SpawnExcavators()
        {
            // 18 Mining Shovels & Excavators at Pit Loading Faces with precise highwall digging orientations (yaw)
            var pitLocations = new (string unitId, string model, string op, Vector3 pos, float yaw)[]
            {
                ("EX-201", "Komatsu PC2000-8", "Hendra Kurniawan", new Vector3(450f, 160f, -200f), 35f),
                ("EX-202", "Hitachi EX1200-6", "Irfan Maulana", new Vector3(-350f, 150f, 400f), 125f),
                ("EX-203", "CAT 6020B Mining Shovel", "Joko Wahyudi", new Vector3(-150f, 140f, -600f), -45f),
                ("EX-204", "Komatsu PC2000-8", "Kresna Bayu", new Vector3(600f, 165f, -100f), 55f),
                ("EX-205", "Hitachi EX2600-6", "Leo Firmansyah", new Vector3(-200f, 145f, 250f), 140f),
                ("EX-206", "CAT 6040 Hydraulic Shovel", "M. Ridwan", new Vector3(100f, 138f, -450f), -30f),
                ("EX-207", "Komatsu PC1250-8", "Noval Ardi", new Vector3(320f, 152f, 150f), 70f),
                ("EX-208", "Hitachi EX1200-6", "Oscar Pratama", new Vector3(-450f, 142f, -300f), -120f),
                ("EX-209", "CAT 6020B Mining Shovel", "Putra Wijaya", new Vector3(-50f, 130f, -750f), -60f),
                ("EX-210", "Komatsu PC2000-8", "Rahmat Hidayat", new Vector3(720f, 170f, 50f), 80f),
                ("EX-211", "Liebherr R9200", "Suryadi Kusuma", new Vector3(-280f, 148f, 550f), 160f),
                ("EX-212", "CAT 6030 Front Shovel", "Tri Nugroho", new Vector3(250f, 145f, -350f), 15f),
                ("EX-213", "Komatsu PC1250-8", "Umar Said", new Vector3(180f, 135f, 300f), 90f),
                ("EX-214", "Hitachi EX1200-6", "Victor Sihombing", new Vector3(-120f, 128f, -150f), -85f),
                ("EX-215", "CAT 6020B Mining Shovel", "Wawan Kurnia", new Vector3(500f, 158f, -480f), 20f),
                ("EX-216", "Komatsu PC2000-8", "Yogi Pratama", new Vector3(-380f, 144f, 100f), -145f),
                ("EX-217", "Liebherr R9150", "Zulham Efendi", new Vector3(80f, 132f, -550f), -10f),
                ("EX-218", "Hitachi EX2600-6", "Agus Setiawan", new Vector3(380f, 150f, -80f), 45f)
            };

            foreach (var p in pitLocations)
            {
                GameObject exObj = FMS3DModelGenerator.CreateExcavator(p.unitId, p.model, p.op);
                exObj.transform.SetParent(fleetRoot.transform, false);
                exObj.transform.position = p.pos;
                exObj.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);

                FMSUnitController ctrl = exObj.GetComponent<FMSUnitController>();
                ctrl.unitName = p.unitId;
                ctrl.modelName = p.model;
                ctrl.operatorName = p.op;
                ctrl.isOnline = false;
                ctrl.currentState = UnitState.Offline;
                ctrl.targetSpeedKmh = 0f;
                ctrl.currentSpeedKmh = 0f;
                ctrl.timeSinceLastGps = 999f;
                activeFleet.Add(ctrl);
            }
        }

        private void SpawnBulldozers()
        {
            // 12 Heavy Bulldozers at Disposal Benches & Bench Levelling
            var dozerLocations = new (string unitId, string model, string op, Vector3 pos)[]
            {
                ("DZ-301", "Komatsu D375A-8", "Lukman Hakim", new Vector3(850f, 210f, 750f)),
                ("DZ-302", "CAT D10T Heavy Dozer", "Miftah Fauzi", new Vector3(1200f, 225f, -300f)),
                ("DZ-303", "Komatsu D375A-8", "Nanang Supriatna", new Vector3(-700f, 195f, 650f)),
                ("DZ-304", "CAT D11T Super Dozer", "Bambang P.", new Vector3(950f, 215f, 600f)),
                ("DZ-305", "Komatsu D275AX", "Cahyono", new Vector3(1100f, 220f, -150f)),
                ("DZ-306", "CAT D10T Heavy Dozer", "Dedi Mulyadi", new Vector3(-600f, 190f, 500f)),
                ("DZ-307", "Komatsu D375A-8", "Edi Santoso", new Vector3(780f, 205f, 850f)),
                ("DZ-308", "CAT D9T Track Dozer", "Feri Irawan", new Vector3(1300f, 230f, -420f)),
                ("DZ-309", "Komatsu D375A-8", "Gunawan", new Vector3(-780f, 200f, 780f)),
                ("DZ-310", "CAT D10T Heavy Dozer", "Heri Susanto", new Vector3(1020f, 218f, 480f)),
                ("DZ-311", "Komatsu D275AX", "Iskandar", new Vector3(1150f, 222f, -50f)),
                ("DZ-312", "CAT D10T Heavy Dozer", "Junaedi", new Vector3(-520f, 185f, 400f))
            };

            foreach (var d in dozerLocations)
            {
                GameObject dzObj = FMS3DModelGenerator.CreateBulldozer(d.unitId, d.model, d.op);
                dzObj.transform.SetParent(fleetRoot.transform, false);
                dzObj.transform.position = d.pos;

                FMSUnitController ctrl = dzObj.GetComponent<FMSUnitController>();
                ctrl.unitName = d.unitId;
                ctrl.modelName = d.model;
                ctrl.operatorName = d.op;
                ctrl.isOnline = false;
                ctrl.currentState = UnitState.Offline;
                ctrl.targetSpeedKmh = 0f;
                ctrl.currentSpeedKmh = 0f;
                ctrl.timeSinceLastGps = 999f;
                activeFleet.Add(ctrl);
            }
        }

        private void SpawnGraders(Dictionary<string, List<Vector3>> routes)
        {
            // 6 Motor Graders across Arterial Haul Corridors
            var graderConfigs = new (string unitId, string model, string op)[]
            {
                ("MG-401", "CAT 16M Motor Grader", "Oki Setiawan"),
                ("MG-402", "Komatsu GD825A", "Panji Tri"),
                ("MG-403", "CAT 24M Heavy Grader", "Roni Gunawan"),
                ("MG-404", "Komatsu GD825A", "Setyo Budi"),
                ("MG-405", "CAT 16M Motor Grader", "Teguh Wibowo"),
                ("MG-406", "CAT 18M Motor Grader", "Yanto Sukses")
            };

            int gIdx = 0;
            foreach (var kvp in routes)
            {
                if (gIdx >= graderConfigs.Length) break;
                List<Vector3> wps = kvp.Value;
                if (wps == null || wps.Count < 3) continue;

                var g = graderConfigs[gIdx];
                GameObject grObj = FMS3DModelGenerator.CreateGrader(g.unitId, g.model, g.op);
                grObj.transform.SetParent(fleetRoot.transform, false);

                FMSUnitController ctrl = grObj.GetComponent<FMSUnitController>();
                ctrl.unitName = g.unitId;
                ctrl.modelName = g.model;
                ctrl.operatorName = g.op;
                ctrl.waypoints = new List<Vector3>(wps);

                bool isFwd = (gIdx % 2 == 0);
                float fraction = (gIdx + 1f) / (graderConfigs.Length + 1f);
                var sample = SampleRoutePosition(wps, fraction, isFwd, ctrl.laneOffsetDistance);

                ctrl.isForwardRoute = isFwd;
                ctrl.currentWaypointIndex = sample.nextWpIndex;
                ctrl.transform.position = sample.pos;
                ctrl.transform.rotation = Quaternion.LookRotation(sample.travelDir, Vector3.up);
                ctrl.currentTargetPoint = sample.pos;
                ctrl.hasTarget = false;
                ctrl.targetSpeedKmh = 0f;
                ctrl.currentSpeedKmh = 0f;
                ctrl.timeSinceLastGps = 999f;
                ctrl.activityName = "Offline";
                ctrl.isOnline = false;
                ctrl.currentState = UnitState.Offline;
                ctrl.AlignToGround(instant: true);

                activeFleet.Add(ctrl);
                gIdx++;
            }
        }

        private void SpawnWheelLoaders()
        {
            // 5 Wheel Loaders at ROM Stockpiles & Hopper Feeders
            var loaderLocations = new (string unitId, string model, string op, Vector3 pos)[]
            {
                ("WL-501", "CAT 988K Wheel Loader", "Qomaruddin", new Vector3(600f, 175f, 100f)),
                ("WL-502", "Komatsu WA600-8", "Rian Hidayat", new Vector3(-450f, 160f, -150f)),
                ("WL-503", "CAT 992K Heavy Loader", "Sugeng R.", new Vector3(750f, 180f, 220f)),
                ("WL-504", "Komatsu WA800-8", "Tito Ari", new Vector3(-300f, 155f, -250f)),
                ("WL-505", "CAT 988K Wheel Loader", "Usman Hadi", new Vector3(500f, 170f, 50f))
            };

            foreach (var l in loaderLocations)
            {
                GameObject wlObj = FMS3DModelGenerator.CreateWheelLoader(l.unitId, l.model, l.op);
                wlObj.transform.SetParent(fleetRoot.transform, false);
                wlObj.transform.position = l.pos;

                FMSUnitController ctrl = wlObj.GetComponent<FMSUnitController>();
                ctrl.unitName = l.unitId;
                ctrl.modelName = l.model;
                ctrl.operatorName = l.op;
                ctrl.isOnline = false;
                ctrl.currentState = UnitState.Offline;
                ctrl.targetSpeedKmh = 0f;
                ctrl.currentSpeedKmh = 0f;
                ctrl.timeSinceLastGps = 999f;
                activeFleet.Add(ctrl);
            }
        }

        private void SpawnFuelTrucks()
        {
            // 5 Fuel & Service Support Tankers
            var fuelConfigs = new (string unitId, string model, string op, Vector3 pos)[]
            {
                ("FT-601", "Scania P380 Fuel Tanker", "Samsul Hadi", new Vector3(200f, 170f, 50f)),
                ("FT-602", "Hino 500 Service Rig", "Taufik Ismail", new Vector3(-100f, 165f, 250f)),
                ("FT-603", "Mercedes Actros 3340 Fuel Rig", "Wawan H.", new Vector3(350f, 172f, -120f)),
                ("FT-604", "Volvo FMX 400 Lube Rig", "Yahya M.", new Vector3(-250f, 160f, 180f)),
                ("FT-605", "Scania P380 Fuel Tanker", "Zaenal M.", new Vector3(100f, 168f, -300f))
            };

            foreach (var f in fuelConfigs)
            {
                GameObject ftObj = FMS3DModelGenerator.CreateFuelTruck(f.unitId, f.model, f.op);
                ftObj.transform.SetParent(fleetRoot.transform, false);
                ftObj.transform.position = f.pos;

                FMSUnitController ctrl = ftObj.GetComponent<FMSUnitController>();
                ctrl.unitName = f.unitId;
                ctrl.modelName = f.model;
                ctrl.operatorName = f.op;
                ctrl.isOnline = false;
                ctrl.currentState = UnitState.Offline;
                ctrl.targetSpeedKmh = 0f;
                ctrl.currentSpeedKmh = 0f;
                ctrl.timeSinceLastGps = 999f;
                activeFleet.Add(ctrl);
            }
        }

        public void SetFleetVisibility(bool visible)
        {
            showFleet = visible;
            if (fleetRoot != null)
            {
                fleetRoot.SetActive(visible);
            }
            if (visible && hideOfflineUnits)
            {
                ApplyOfflineFilter();
            }
        }

        public void ToggleFleetVisibility()
        {
            SetFleetVisibility(!showFleet);
        }

        public void SetHideOfflineUnits(bool hide)
        {
            hideOfflineUnits = hide;
            ApplyOfflineFilter();
        }

        public void ToggleHideOfflineUnits()
        {
            SetHideOfflineUnits(!hideOfflineUnits);
        }

        public void ToggleShowArchivedUnits()
        {
            showArchivedUnits = !showArchivedUnits;
            ApplyFleetCategoryFilters();
        }

        public void ApplyOfflineFilter()
        {
            ApplyFleetCategoryFilters();
        }

        private float nextVisualSeparationAt;

        private void LateUpdate()
        {
            if (isSimulationMode || activeFleet == null || Time.time < nextVisualSeparationAt) return;
            nextVisualSeparationAt = Time.time + 0.25f;

            var units = new List<FMSUnitController>();
            foreach (var unit in activeFleet)
            {
                if (unit == null) continue;
                unit.gpsProximityConflict = false;
                if (unit.gameObject.activeInHierarchy && unit.isLiveTelemetryControlled && unit.hasValidGpsFix)
                    units.Add(unit);
            }
            units.Sort((a, b) => string.Compare(a.unitId, b.unitId, StringComparison.OrdinalIgnoreCase));

            var placed = new List<Vector3>(units.Count);
            var radii = new List<float>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                Vector3 origin = unit.transform.position;
                float radius = Mathf.Clamp(unit.referenceLengthMeters * 0.55f, 2.2f, 9f);
                Vector3 candidate = origin;
                bool found = false;
                for (int ring = 0; ring <= 16 && !found; ring++)
                {
                    int slots = ring == 0 ? 1 : ring * 12;
                    for (int slot = 0; slot < slots; slot++)
                    {
                        if (ring > 0)
                        {
                            float angle = slot * Mathf.PI * 2f / slots + (unit.backendEquipmentId % 17) * 0.12f;
                            float distance = ring * (radius * 2f + 1f);
                            candidate = origin + new Vector3(Mathf.Cos(angle) * distance, 0f,
                                Mathf.Sin(angle) * distance);
                        }
                        bool clear = true;
                        for (int other = 0; other < placed.Count; other++)
                        {
                            Vector3 delta = candidate - placed[other];
                            delta.y = 0f;
                            float clearance = radius + radii[other] + 0.5f;
                            if (delta.sqrMagnitude < clearance * clearance)
                            {
                                clear = false;
                                break;
                            }
                        }
                        if (clear) { found = true; break; }
                    }
                }

                for (int other = 0; other < i; other++)
                {
                    Vector3 delta = origin - units[other].transform.position;
                    delta.y = 0f;
                    float clearance = radius + Mathf.Clamp(
                        units[other].referenceLengthMeters * 0.55f, 2.2f, 9f);
                    if (delta.sqrMagnitude < clearance * clearance)
                    {
                        unit.gpsProximityConflict = true;
                        units[other].gpsProximityConflict = true;
                    }
                }

                Vector3 visualOffset = candidate - origin;
                if (visualOffset.sqrMagnitude > 0.01f)
                {
                    Terrain terrain = Terrain.activeTerrain;
                    if (terrain != null)
                    {
                        float originalHeight = terrain.SampleHeight(origin);
                        float displayHeight = terrain.SampleHeight(candidate);
                        visualOffset.y = displayHeight - originalHeight;
                    }
                }
                unit.SetLiveVisualOffset(visualOffset);
                placed.Add(candidate);
                radii.Add(radius);
            }
        }

        private void Update()
        {
            // Right-click detection on 3D Units & Overhead Tags
            if (Input.GetMouseButtonDown(1))
            {
                if (FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsPointerOverUI()) return;

                FMSUnitController targetUnit = null;

                // 1. Check screen-space proximity to overhead 3D tags/anchors
                if (Camera.main != null && activeFleet != null)
                {
                    Vector2 mouseScreenPos = Input.mousePosition;
                    float closestDist = float.MaxValue;

                    foreach (var u in activeFleet)
                    {
                        if (u == null || !u.gameObject.activeInHierarchy) continue;
                        Vector3 sp = Camera.main.WorldToScreenPoint(u.VisualWorldPosition + Vector3.up * 4.5f);
                        if (sp.z > 0.5f)
                        {
                            float d = Vector2.Distance(new Vector2(sp.x, sp.y), mouseScreenPos);
                            if (d < 65f && d < closestDist)
                            {
                                closestDist = d;
                                targetUnit = u;
                            }
                        }
                    }
                }

                // 2. RaycastAll in 3D scene sorted by distance to find unit colliders
                if (targetUnit == null && Camera.main != null)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit[] hits = Physics.RaycastAll(ray, 10000f);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    foreach (var hit in hits)
                    {
                        FMSUnitController u = hit.collider.GetComponentInParent<FMSUnitController>();
                        if (u != null)
                        {
                            targetUnit = u;
                            break;
                        }
                    }
                }

                if (targetUnit != null)
                {
                    OpenContextMenu(targetUnit, Input.mousePosition);
                }
                else if (isContextMenuOpen)
                {
                    if (FMSDashboardUI.Instance == null || !FMSDashboardUI.Instance.IsMouseOverContextMenu())
                    {
                        CloseContextMenu();
                    }
                }
            }

            // Close context menu if left-clicked outside
            if (isContextMenuOpen && Input.GetMouseButtonDown(0))
            {
                if (FMSDashboardUI.Instance != null && !FMSDashboardUI.Instance.IsMouseOverContextMenu())
                {
                    CloseContextMenu();
                }
            }

            UpdateFleetHeatmap();
        }

        public void OpenContextMenu(FMSUnitController unit, Vector2 mousePos)
        {
            if (unit == null) return;
            SelectUnit(unit); // Lock camera and selection to the unit
            contextMenuUnit = unit;
            contextMenuScreenPos = mousePos;
            isContextMenuOpen = true;
        }

        public void CloseContextMenu()
        {
            isContextMenuOpen = false;
            contextMenuUnit = null;
        }

        public void SelectUnit(FMSUnitController unit)
        {
            selectedUnit = unit;
            if (FMSCameraController.Instance != null && unit != null)
            {
                FMSCameraController.Instance.SetFollowTarget(unit.transform);
            }
        }

        public void DeselectUnit()
        {
            selectedUnit = null;
            if (FMSCameraController.Instance != null && FMSCameraController.Instance.followTarget != null)
            {
                FMSCameraController.Instance.followTarget = null;
            }
        }

        public FMSUnitController GetUnitById(string id)
        {
            if (string.IsNullOrEmpty(id) || activeFleet == null) return null;
            return activeFleet.Find(x => x != null && (x.unitId.Equals(id, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(x.unitName) && x.unitName.Equals(id, StringComparison.OrdinalIgnoreCase))));
        }

        public void SelectUnitById(string id)
        {
            FMSUnitController u = GetUnitById(id);
            if (u != null)
            {
                SelectUnit(u);
            }
        }

        // =========================================================================
        // FLEET ISOLATION & VISIBILITY CONTROL
        // =========================================================================
        public void IsolateFleet(string loaderId)
        {
            if (string.IsNullOrEmpty(loaderId)) return;

            isFleetIsolated = true;
            isolatedLoaderId = loaderId;

            int visibleCount = 0;
            foreach (var u in activeFleet)
            {
                if (u == null) continue;
                bool isPartOfFleet = u.unitId.Equals(loaderId, StringComparison.OrdinalIgnoreCase) || 
                                     (!string.IsNullOrEmpty(u.assignedLoaderId) && u.assignedLoaderId.Equals(loaderId, StringComparison.OrdinalIgnoreCase));

                u.gameObject.SetActive(isPartOfFleet);
                if (isPartOfFleet) visibleCount++;
            }

            FMSDashboardUI.Instance?.ShowNotification($"🎯 FLEET TERISOLASI: {loaderId} ({visibleCount} Unit Aktif). Semua unit luar disembunyikan.");
        }

        public void HideFleet(string loaderId)
        {
            if (string.IsNullOrEmpty(loaderId)) return;

            int hiddenCount = 0;
            foreach (var u in activeFleet)
            {
                if (u == null) continue;
                bool isPartOfFleet = u.unitId.Equals(loaderId, StringComparison.OrdinalIgnoreCase) || 
                                     (!string.IsNullOrEmpty(u.assignedLoaderId) && u.assignedLoaderId.Equals(loaderId, StringComparison.OrdinalIgnoreCase));

                if (isPartOfFleet)
                {
                    u.gameObject.SetActive(false);
                    hiddenCount++;
                }
            }

            FMSDashboardUI.Instance?.ShowNotification($"🚫 FLEET DISEMBUNYIKAN: {loaderId} ({hiddenCount} Unit).");
        }

        public void HideSingleUnit(FMSUnitController unit)
        {
            if (unit == null) return;
            unit.gameObject.SetActive(false);
            if (selectedUnit == unit) DeselectUnit();
            FMSDashboardUI.Instance?.ShowNotification($"🚫 Unit {unit.unitId} ({unit.unitName}) disembunyikan.");
        }

        public void RestoreAllUnits()
        {
            isFleetIsolated = false;
            isolatedLoaderId = "";

            ApplyFleetCategoryFilters();
            FMSDashboardUI.Instance?.ShowNotification($"🔄 Seluruh Armada ({activeFleet.Count} Unit) Ditampilkan Kembali.");
        }

        // =========================================================================
        // FLEET AGGREGATE METRICS & PRODUCTION ENGINE
        // =========================================================================
        public void RecordCompletedTrip(FMSUnitController truck, float deliveredTons)
        {
            if (truck == null) return;
            totalCompletedCycles++;
            totalTonnageMoved += deliveredTons;

            // Notify loader shovel
            if (!string.IsNullOrEmpty(truck.assignedLoaderId))
            {
                FMSUnitController loader = activeFleet.Find(u => u.unitId.Equals(truck.assignedLoaderId, StringComparison.OrdinalIgnoreCase));
                if (loader != null)
                {
                    loader.completedTripsCount++;
                }
            }

            // Calculate live hourly productivity
            float elapsedHours = Mathf.Max(0.01f, Time.timeSinceLevelLoad / 3600f);
            fleetProductivityPerHour = totalTonnageMoved / elapsedHours;
        }

        public void GetOverallProductionStats(out int totalTrips, out double totalTons, out double totalCoalBcm, out float fleetPa, out float hourlyProductivity)
        {
            totalTrips = 0;
            totalTons = 0.0;
            int onlineCount = 0;
            int totalCount = activeFleet != null ? activeFleet.Count : 0;

            if (activeFleet != null)
            {
                foreach (var u in activeFleet)
                {
                    if (u == null) continue;
                    if (u.isOnline && u.currentState != UnitState.Offline) onlineCount++;

                    if (u.unitType == UnitType.HaulTruck)
                    {
                        if (u.hasActualHaulData) totalTrips += u.recordedLoads;
                        if (u.hasActualPayload) totalTons += u.payloadTons;
                    }
                }
            }

            totalCoalBcm = 0;
            fleetPa = totalCount > 0 ? ((float)onlineCount / totalCount * 100f) : 0f;
            hourlyProductivity = 0f;
        }

        public List<FMSUnitController> GetChildHaulers(string loaderId)
        {
            if (string.IsNullOrEmpty(loaderId) || activeFleet == null) return new List<FMSUnitController>();
            return activeFleet.FindAll(u => !string.IsNullOrEmpty(u.assignedLoaderId) && u.assignedLoaderId.Equals(loaderId, StringComparison.OrdinalIgnoreCase));
        }

        public void GetAggregateFleetStats(string loaderId, out int totalTrips, out double totalTons, out float avgCycleMins, out float matchFactor, out int activeHaulersCount, out float totalFuelLiters)
        {
            totalTrips = 0;
            totalTons = 0.0;
            totalFuelLiters = 0f;
            activeHaulersCount = 0;
            avgCycleMins = 0f;
            matchFactor = 0f;

            var children = GetChildHaulers(loaderId);
            FMSUnitController loader = activeFleet.Find(u => u.unitId.Equals(loaderId, StringComparison.OrdinalIgnoreCase));

            foreach (var truck in children)
            {
                if (truck == null) continue;
                if (truck.hasActualHaulData) totalTrips += truck.recordedLoads;
                if (truck.hasActualPayload) totalTons += truck.payloadTons;
                if (truck.isOnline && truck.currentState != UnitState.Offline)
                {
                    activeHaulersCount++;
                }
            }

        }

        public void ToggleFleetHeatmap(string loaderId)
        {
            if (showFleetHeatmap && heatmapLoaderId == loaderId)
            {
                showFleetHeatmap = false;
                heatmapLoaderId = "";
                if (heatmapLineRenderer != null) heatmapLineRenderer.enabled = false;
                FMSDashboardUI.Instance?.ShowNotification("🔥 Heatmap Rute Fleet Dinonaktifkan.");
            }
            else
            {
                showFleetHeatmap = true;
                heatmapLoaderId = loaderId;
                FMSDashboardUI.Instance?.ShowNotification($"🔥 Heatmap & Lintasan GPS Fleet {loaderId} Diaktifkan!");
            }
        }

        private void UpdateFleetHeatmap()
        {
            if (!showFleetHeatmap || string.IsNullOrEmpty(heatmapLoaderId))
            {
                if (heatmapLineRenderer != null) heatmapLineRenderer.enabled = false;
                return;
            }

            var children = GetChildHaulers(heatmapLoaderId);
            FMSUnitController loader = activeFleet.Find(u => u.unitId.Equals(heatmapLoaderId, StringComparison.OrdinalIgnoreCase));
            if (loader == null && children.Count == 0) return;

            if (heatmapLineRenderer == null)
            {
                GameObject lineObj = new GameObject("FleetHeatmapLine");
                lineObj.transform.SetParent(transform);
                heatmapLineRenderer = lineObj.AddComponent<LineRenderer>();
                heatmapLineRenderer.startWidth = 2.5f;
                heatmapLineRenderer.endWidth = 2.5f;
                heatmapLineRenderer.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                heatmapLineRenderer.startColor = new Color(0f, 0.95f, 1f, 0.85f);
                heatmapLineRenderer.endColor = new Color(1f, 0.65f, 0f, 0.85f);
            }

            heatmapLineRenderer.enabled = true;
            List<Vector3> points = new List<Vector3>();

            if (loader != null) points.Add(loader.transform.position + Vector3.up * 1.5f);

            foreach (var truck in children)
            {
                if (truck != null && truck.gameObject.activeSelf)
                {
                    points.Add(truck.transform.position + Vector3.up * 1.5f);
                    if (loader != null) points.Add(loader.transform.position + Vector3.up * 1.5f);
                }
            }

            heatmapLineRenderer.positionCount = points.Count;
            heatmapLineRenderer.SetPositions(points.ToArray());
        }

        public void ClearFleet()
        {
            if (fleetRoot != null)
            {
                Destroy(fleetRoot);
                fleetRoot = null;
            }
            activeFleet.Clear();
            selectedUnit = null;
        }

        private Dictionary<string, List<Vector3>> GenerateDefaultMineRoutes()
        {
            var dict = new Dictionary<string, List<Vector3>>();

            // Route 1: East Pit to North Disposal (OB Hauling)
            dict["ROUTE_PANEL_EAST_TO_DISPOSAL_NORTH"] = new List<Vector3>
            {
                new Vector3(450f, 160f, -200f),
                new Vector3(550f, 172f, 0f),
                new Vector3(680f, 185f, 280f),
                new Vector3(790f, 198f, 520f),
                new Vector3(850f, 210f, 750f)
            };

            // Route 2: West Pit T6U to North Disposal (OB Hauling)
            dict["ROUTE_T6U_TO_DISPOSAL_NORTH"] = new List<Vector3>
            {
                new Vector3(-350f, 150f, 400f),
                new Vector3(-120f, 162f, 500f),
                new Vector3(200f, 178f, 620f),
                new Vector3(550f, 192f, 700f),
                new Vector3(850f, 210f, 750f)
            };

            // Route 3: South Pit T6S33 to Main Disposal (South Waste Dump)
            dict["ROUTE_T6S33_TO_DISPOSAL_MAIN"] = new List<Vector3>
            {
                new Vector3(-150f, 140f, -600f),
                new Vector3(100f, 155f, -480f),
                new Vector3(450f, 175f, -380f),
                new Vector3(850f, 200f, -320f),
                new Vector3(1200f, 225f, -300f)
            };

            // Route 4: Deep Pit Sump to Central In-Pit Backfill
            dict["ROUTE_DEEP_SUMP_TO_INPIT_BACKFILL"] = new List<Vector3>
            {
                new Vector3(-50f, 130f, -750f),
                new Vector3(80f, 142f, -620f),
                new Vector3(220f, 158f, -450f),
                new Vector3(380f, 172f, -220f),
                new Vector3(520f, 185f, -50f)
            };

            // Route 5: Central Coal Seam to ROM Stockpile Crusher
            dict["ROUTE_COAL_SEAM_TO_ROM_CRUSHER"] = new List<Vector3>
            {
                new Vector3(320f, 152f, 150f),
                new Vector3(420f, 160f, 120f),
                new Vector3(540f, 168f, 80f),
                new Vector3(660f, 174f, 60f),
                new Vector3(750f, 180f, 40f)
            };

            // Route 6: West Ridge OB to West Outpit Disposal
            dict["ROUTE_WEST_RIDGE_TO_WEST_OUTPIT"] = new List<Vector3>
            {
                new Vector3(-450f, 142f, -300f),
                new Vector3(-520f, 158f, -100f),
                new Vector3(-580f, 175f, 150f),
                new Vector3(-640f, 188f, 420f),
                new Vector3(-700f, 195f, 650f)
            };

            // Route 7: East Bench +160 to East High Dump
            dict["ROUTE_EAST_BENCH_TO_EAST_DUMP"] = new List<Vector3>
            {
                new Vector3(600f, 165f, -100f),
                new Vector3(720f, 178f, 120f),
                new Vector3(840f, 192f, 350f),
                new Vector3(960f, 208f, 580f),
                new Vector3(1050f, 218f, 720f)
            };

            // Route 8: North Ramp to Central Preparation Plant
            dict["ROUTE_NORTH_RAMP_TO_CPP_PLANT"] = new List<Vector3>
            {
                new Vector3(-200f, 145f, 250f),
                new Vector3(-50f, 158f, 180f),
                new Vector3(150f, 168f, 120f),
                new Vector3(350f, 174f, 70f),
                new Vector3(500f, 178f, 20f)
            };

            // Route 9: South Pit Ramp to South Disposal Ridge
            dict["ROUTE_SOUTH_RAMP_TO_SOUTH_DUMP"] = new List<Vector3>
            {
                new Vector3(100f, 138f, -450f),
                new Vector3(350f, 160f, -500f),
                new Vector3(650f, 182f, -480f),
                new Vector3(950f, 205f, -420f),
                new Vector3(1250f, 228f, -350f)
            };

            // Route 10: Central Haul Trunkline to Main Workshop & Fuel Bay
            dict["ROUTE_CENTRAL_TRUNK_TO_WORKSHOP"] = new List<Vector3>
            {
                new Vector3(-120f, 128f, -150f),
                new Vector3(40f, 145f, -50f),
                new Vector3(180f, 158f, 60f),
                new Vector3(320f, 168f, 180f),
                new Vector3(450f, 175f, 300f)
            };

            return dict;
        }
    }
}
