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
            Application.runInBackground = true;
        }

        private void Start()
        {
            isSimulationMode = false;
            hideOfflineUnits = false;
            Application.runInBackground = true;



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
            if (string.IsNullOrWhiteSpace(id) || activeFleet == null) return null;

            // 1. Direct match by unitId or unitName
            var direct = activeFleet.Find(x => x != null && 
                ((x.unitId != null && x.unitId.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrEmpty(x.unitName) && x.unitName.Equals(id, StringComparison.OrdinalIgnoreCase))));
            if (direct != null) return direct;

            // 2. Cleaned whitespace/dash match
            string cleanTarget = id.Replace("-", "").Replace("_", "").Replace(" ", "").Trim();
            var cleanMatch = activeFleet.Find(x => {
                if (x == null) return false;
                string cUId = (x.unitId ?? "").Replace("-", "").Replace("_", "").Replace(" ", "").Trim();
                string cUName = (x.unitName ?? "").Replace("-", "").Replace("_", "").Replace(" ", "").Trim();
                return cUId.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                       cUName.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase);
            });
            if (cleanMatch != null) return cleanMatch;

            // 3. Digit-based alias match (e.g. RD5107 <-> DT5107 <-> 5107)
            string digitsTarget = new string(System.Array.FindAll(cleanTarget.ToCharArray(), char.IsDigit));
            if (!string.IsNullOrEmpty(digitsTarget) && digitsTarget.Length >= 3)
            {
                var digitMatch = activeFleet.Find(x => {
                    if (x == null) return false;
                    string cUId = x.unitId ?? "";
                    string cUName = x.unitName ?? "";
                    string digitsU = new string(System.Array.FindAll(cUId.ToCharArray(), char.IsDigit));
                    if (string.IsNullOrEmpty(digitsU)) digitsU = new string(System.Array.FindAll(cUName.ToCharArray(), char.IsDigit));
                    return digitsU == digitsTarget;
                });
                if (digitMatch != null) return digitMatch;
            }

            return null;
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

