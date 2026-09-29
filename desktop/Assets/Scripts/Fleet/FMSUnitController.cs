using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public enum UnitType
    {
        HaulTruck,
        Excavator,
        Bulldozer,
        Grader,
        Support,
        FuelTruck,
        WheelLoader
    }

    public enum UnitState
    {
        Idle,
        TravellingToLoad,
        QueueingAtPit,
        Loading,
        Hauling,
        QueueingAtDump,
        Dumping,
        Maintenance,
        Offline
    }

    public enum StoppageReason
    {
        Moving,                   // Unit sedang bergerak normal
        OperationalLoading,       // Operasional: Sedang dimuat excavator di front gali
        OperationalQueuePit,      // Operasional: Mengantre di belakang truk lain di loading front
        OperationalDumping,       // Operasional: Sedang membuang muatan / mengangkat dump bed
        OperationalQueueDump,     // Operasional: Mengantre di disposal hopper / waste dump
        OperationalStandby,       // Operasional: Standby istirahat / ganti shift
        AutonomousSimulation,     // Simulasi digital twin
        ApiGpsStaleStatic,        // Masalah API: Data GPS backend tidak berubah / beku (> 15 detik)
        ApiGpsNoFixNull,          // Masalah API: Koordinat Lat/Lon NULL atau (0,0) di database
        ApiGpsDeviceOffline,      // Masalah API: Perangkat GPS mati / sinyal hilang (> 30 detik)
        ApiFleetFeedStale
    }

    public class FMSUnitController : MonoBehaviour
    {
        [Header("Unit Identity")]
        public string unitId = "RD5100";
        public long backendEquipmentId;
        public long backendEquipmentTypeId;
        public long backendStatusId;
        public long backendActivityId;
        public float referenceLengthMeters;
        public string unitName = "";
        public string modelName = "CAT 777E";
        public string operatorName = "Ahmad Supardi";
        public string category = "Hauler";
        public UnitType unitType = UnitType.HaulTruck;
        public FMSUnitAssetManager.UnitCategory unitCategory = FMSUnitAssetManager.UnitCategory.HaulerLoaded;
        public Color statusColor = Color.green;

        public string DisplayName => !string.IsNullOrEmpty(unitName) ? unitName : unitId;

        [Header("Kinematics & Operational State")]
        public UnitState currentState = UnitState.Hauling;
        public float currentSpeedKmh = 28.5f;
        public float targetSpeedKmh = 30.0f;
        public float maxSpeedKmh = 50.0f;
        public float headingDegrees = 0f;
        public bool isLiveTelemetryControlled = false;
        public float gpsVisualDelaySeconds = 0f;
        public bool isOnline = true;
        public float payloadTons = 95f;
        public float maxPayloadTons = 95f;
        public string activityName = "Hauling (Loaded)";

        [Header("Fleet Assignment & Loader Pairing")]
        public string assignedLoaderId = "EX-201";
        public string assignedLoaderModel = "Komatsu PC2000-8";
        public string assignedFrontName = "Front Pit East (Face-01)";
        public string assignedDisposalName = "North Disposal Dump";
        public string lastLoadedByLoaderId = "EX-201";
        public string lastLoadedByLoaderModel = "Komatsu PC2000-8";
        public string lastLoadedLocation = "Front Pit East";
        public float lastLoadedTimestamp = 0f;
        public int completedTripsCount = 0;
        public bool hasActualHaulData = false;
        public int recordedLoads = 0;
        public bool hasActualPayload = false;

        public string GetLastLoadedInfo()
        {
            if (string.IsNullOrEmpty(lastLoadedByLoaderId)) return "Belum ada rekaman muat";
            float minsAgo = (Time.time - lastLoadedTimestamp) / 60f;
            if (minsAgo < 0.2f) return $"Sedang dimuat {lastLoadedByLoaderId} @ {lastLoadedLocation}";
            if (minsAgo < 60f) return $"{lastLoadedByLoaderId} ({lastLoadedByLoaderModel}) • {minsAgo:F1} m lalu";
            return $"{lastLoadedByLoaderId} ({lastLoadedByLoaderModel})";
        }

        [Header("Diagnostic & Stoppage Root-Cause Analysis")]
        public StoppageReason stoppageReason = StoppageReason.Moving;
        public string stoppageDiagnosticText = "Normal Operasional";
        public float secondsSinceLastBackendUpdate = 0f;
        public int backendLastHeardSeconds = 0;
        public bool hasValidGpsFix = true;
        public Vector3 lastRecordedGpsPos = Vector3.zero;
        public double latestGpsEasting;
        public double latestGpsNorthing;
        public double latestGpsElevation;
        public double latestGpsLatitude;
        public double latestGpsLongitude;
        public float timeAtCurrentCoordinate = 0f;

        [Header("Engine & Sensor Telemetry")]
        public float fuelLevelPercent = 82f;
        public float engineRpm = 1750f;
        public float coolantTempC = 88f;
        public float oilPressureKpa = 420f;
        public float totalDistanceKm = 142.5f;
        public float suspensionBounceY = 0f;

        [Header("Waypoints & Navigation Route")]
        public List<Vector3> waypoints = new List<Vector3>();
        public int currentWaypointIndex = 0;
        public bool isForwardRoute = true;
        public Vector3 currentTargetPoint;
        public bool hasTarget = false;
        public float waypointArrivalThreshold = 6.0f;
        public float steeringSmoothSpeed = 3.8f;
        public float accelerationSpeed = 4.5f;

        [Header("Mine Traffic & Lane Separation")]
        public float laneOffsetDistance = 4.5f; // Left-Hand Traffic (Indonesian open-pit mine standard)
        public float safeFollowDistance = 28.0f; // Mining convoy safe separation buffer (28m)
        public float currentSteeringAngleDelta = 0f;

        [System.Serializable]
        public struct GpsTrajectorySnapshot
        {
            public Vector3 position;
            public float speedKmh;
            public float headingDeg;
            public float timestamp;
        }

        [Header("20-Point GPS Trajectory Projection & Spline Buffer")]
        public List<GpsTrajectorySnapshot> trajectoryBuffer = new List<GpsTrajectorySnapshot>(25);
        public const int MAX_TRAJECTORY_POINTS = 20;

        public void AddTrajectoryPoint(Vector3 pos, float speed, float heading)
        {
            if (pos.sqrMagnitude < 1.0f) return;

            if (trajectoryBuffer.Count == 0 || Vector3.Distance(trajectoryBuffer[trajectoryBuffer.Count - 1].position, pos) > 0.4f)
            {
                trajectoryBuffer.Add(new GpsTrajectorySnapshot
                {
                    position = pos,
                    speedKmh = speed,
                    headingDeg = heading,
                    timestamp = Time.time
                });

                if (trajectoryBuffer.Count > MAX_TRAJECTORY_POINTS)
                {
                    trajectoryBuffer.RemoveAt(0);
                }
            }
        }

        public Vector3 GetProjectedTrajectoryTangent()
        {
            if (trajectoryBuffer.Count >= 2)
            {
                int last = trajectoryBuffer.Count - 1;
                Vector3 tangent = trajectoryBuffer[last].position - trajectoryBuffer[last - 1].position;
                tangent.y = 0f;
                if (tangent.sqrMagnitude > 0.01f) return tangent.normalized;
            }
            if (headingDegrees >= 0f)
            {
                return Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            }
            return transform.forward;
        }

        public void LoadTrajectoryFromBackend(List<TrajectoryPointDto> trajectory)
        {
            if (trajectory == null || trajectory.Count == 0) return;
            trajectoryBuffer.Clear();
            foreach (var pt in trajectory)
            {
                if (Mathf.Abs(pt.x) > 0.1f || Mathf.Abs(pt.z) > 0.1f)
                {
                    trajectoryBuffer.Add(new GpsTrajectorySnapshot
                    {
                        position = new Vector3(pt.x, pt.y, pt.z),
                        speedKmh = pt.speed_kmh,
                        headingDeg = pt.heading_deg,
                        timestamp = Time.time
                    });
                }
            }
        }

        private static bool TryParseBackendTimestamp(string raw, out DateTime recordedAt)
        {
            if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out recordedAt))
            {
                recordedAt = recordedAt.ToUniversalTime();
                return true;
            }

            recordedAt = DateTime.MinValue;
            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private struct LiveGpsFix
        {
            public Vector3 position;
            public DateTime recordedAtUtc;
            public float headingDeg;
        }

        private readonly List<LiveGpsFix> liveGpsFixes = new List<LiveGpsFix>(24);
        private DateTime serverUtcAtReceipt = DateTime.MinValue;
        private float localRealtimeAtReceipt;
        private float playbackDelaySeconds = 12f;
        private float desiredPlaybackDelaySeconds = 12f;
        private DateTime playbackUtc = DateTime.MinValue;
        private bool liveGpsInitialized;

        private void UpdateLiveGpsTimeline(List<TrajectoryPointDto> trajectory, Vector3 latestPos,
            float liveHeadingDeg, string lastFixTime, string serverTime)
        {
            var incoming = new List<LiveGpsFix>((trajectory != null ? trajectory.Count : 0) + 1);
            if (trajectory != null)
            {
                foreach (var point in trajectory)
                {
                    Vector3 position = new Vector3(point.x, point.y, point.z);
                    if (position.sqrMagnitude > 1f && TryParseBackendTimestamp(point.recorded_at, out DateTime recordedAt))
                    {
                        incoming.Add(new LiveGpsFix { position = position, recordedAtUtc = recordedAt, headingDeg = point.heading_deg });
                    }
                }
            }

            if (latestPos.sqrMagnitude > 1f && TryParseBackendTimestamp(lastFixTime, out DateTime latestAt))
            {
                incoming.Add(new LiveGpsFix { position = latestPos, recordedAtUtc = latestAt, headingDeg = liveHeadingDeg });
            }
            incoming.Sort((a, b) => a.recordedAtUtc.CompareTo(b.recordedAtUtc));

            foreach (var fix in incoming)
            {
                if (liveGpsFixes.Count > 0)
                {
                    LiveGpsFix previous = liveGpsFixes[liveGpsFixes.Count - 1];
                    double gapSeconds = (fix.recordedAtUtc - previous.recordedAtUtc).TotalSeconds;
                    if (gapSeconds <= 0) continue;
                    if (gapSeconds > 120)
                    {
                        liveGpsFixes.Clear();
                        playbackUtc = DateTime.MinValue;
                        liveGpsInitialized = false;
                    }
                    else if (FlatDistance(previous.position, fix.position) > 25f * (float)gapSeconds + 30f)
                    {
                        continue;
                    }
                }

                liveGpsFixes.Add(fix);
                AddTrajectoryPoint(fix.position, 0f, fix.headingDeg);
                if (liveGpsFixes.Count > MAX_TRAJECTORY_POINTS) liveGpsFixes.RemoveAt(0);
            }

            float observedGap = 0f;
            for (int i = liveGpsFixes.Count - 1; i > Mathf.Max(0, liveGpsFixes.Count - 11); i--)
            {
                LiveGpsFix a = liveGpsFixes[i - 1];
                LiveGpsFix b = liveGpsFixes[i];
                float seconds = (float)(b.recordedAtUtc - a.recordedAtUtc).TotalSeconds;
                if (seconds > 0f && seconds <= 120f && FlatDistance(a.position, b.position) > 0.5f)
                {
                    observedGap = Mathf.Max(observedGap, seconds);
                }
            }
            // Ultra-responsive GPS playback buffer (1.2s - 3.5s jitter absorption)
            desiredPlaybackDelaySeconds = Mathf.Clamp(observedGap + 1.2f, 1.0f, 3.5f);
            if (!liveGpsInitialized) playbackDelaySeconds = desiredPlaybackDelaySeconds;

            float receiptRealtime = Time.realtimeSinceStartup;
            DateTime parsedServerTime = TryParseBackendTimestamp(serverTime, out DateTime responseTime)
                ? responseTime : DateTime.UtcNow;
            DateTime estimatedServerTime = serverUtcAtReceipt == DateTime.MinValue
                ? parsedServerTime : serverUtcAtReceipt.AddSeconds(receiptRealtime - localRealtimeAtReceipt);
            serverUtcAtReceipt = parsedServerTime > estimatedServerTime ? parsedServerTime : estimatedServerTime;
            localRealtimeAtReceipt = receiptRealtime;
            if (liveGpsFixes.Count > 0)
            {
                timeSinceLastGps = Mathf.Max(0f, (float)(serverUtcAtReceipt - liveGpsFixes[liveGpsFixes.Count - 1].recordedAtUtc).TotalSeconds);
                backendLastHeardSeconds = Mathf.FloorToInt(timeSinceLastGps);
                if (timeSinceLastGps > 120f) isOnline = false;
            }
            if (!liveGpsInitialized && liveGpsFixes.Count > 0)
            {
                liveGpsInitialized = true;
                RenderLiveGpsPosition(0f, true);
            }
        }

        private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector3 EvaluateCatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );
        }

        private void RenderLiveGpsPosition(float dt, bool initial = false)
        {
            if (liveGpsFixes.Count == 0 || serverUtcAtReceipt == DateTime.MinValue) return;

            float delayRate = desiredPlaybackDelaySeconds > playbackDelaySeconds ? 0.8f : 0.35f;
            playbackDelaySeconds = Mathf.MoveTowards(playbackDelaySeconds, desiredPlaybackDelaySeconds, dt * delayRate);
            DateTime serverNow = serverUtcAtReceipt.AddSeconds(Time.realtimeSinceStartup - localRealtimeAtReceipt);
            LiveGpsFix first = liveGpsFixes[0];
            LiveGpsFix last = liveGpsFixes[liveGpsFixes.Count - 1];
            DateTime targetPlayback = serverNow.AddSeconds(-playbackDelaySeconds);
            if (playbackUtc == DateTime.MinValue)
            {
                playbackUtc = targetPlayback < first.recordedAtUtc ? first.recordedAtUtc
                    : targetPlayback > last.recordedAtUtc ? last.recordedAtUtc : targetPlayback;
            }
            else if (dt > 0f && playbackUtc < last.recordedAtUtc)
            {
                float lagSeconds = (float)(targetPlayback - playbackUtc).TotalSeconds;
                float playbackRate = Mathf.Clamp(1f + lagSeconds / 4f, 0.7f, 1.4f);
                playbackUtc = playbackUtc.AddSeconds(dt * playbackRate);
                if (playbackUtc > last.recordedAtUtc) playbackUtc = last.recordedAtUtc;
            }
            DateTime displayAt = playbackUtc;
            gpsVisualDelaySeconds = Mathf.Max(0f, (float)(serverNow - displayAt).TotalSeconds);
            timeSinceLastGps = Mathf.Max(0f, (float)(serverNow - last.recordedAtUtc).TotalSeconds);
            backendLastHeardSeconds = Mathf.FloorToInt(timeSinceLastGps);
            if (timeSinceLastGps > 120f || secondsSinceLastBackendUpdate > 30f) isOnline = false;
            
            Vector3 displayPos = first.position;
            Vector3 direction = Vector3.zero;
            float displayedSpeedKmh = 0f;

            if (displayAt >= last.recordedAtUtc)
            {
                // Dead reckoning extrapolation if still rolling
                if (lastKnownVelocityDirection.sqrMagnitude > 0.01f && currentSpeedKmh > 1.0f && (float)(displayAt - last.recordedAtUtc).TotalSeconds < 3.0f)
                {
                    float extraSec = (float)(displayAt - last.recordedAtUtc).TotalSeconds;
                    float decayingSpeed = Mathf.Max(0f, currentSpeedKmh * (1f - extraSec / 3.0f));
                    displayPos = last.position + lastKnownVelocityDirection.normalized * (decayingSpeed / 3.6f * extraSec);
                    direction = lastKnownVelocityDirection;
                    displayedSpeedKmh = decayingSpeed;
                }
                else
                {
                    displayPos = last.position;
                }
            }
            else if (displayAt > first.recordedAtUtc)
            {
                for (int i = 1; i < liveGpsFixes.Count; i++)
                {
                    LiveGpsFix next = liveGpsFixes[i];
                    if (displayAt > next.recordedAtUtc) continue;

                    LiveGpsFix prev = liveGpsFixes[i - 1];
                    float duration = (float)(next.recordedAtUtc - prev.recordedAtUtc).TotalSeconds;
                    if (duration <= 0.001f) break;
                    
                    float fraction = Mathf.Clamp01((float)(displayAt - prev.recordedAtUtc).TotalSeconds / duration);
                    
                    // Catmull-Rom Spline Interpolation for organic, ultra-smooth curve tracking
                    if (liveGpsFixes.Count >= 4)
                    {
                        LiveGpsFix p0 = (i - 2 >= 0) ? liveGpsFixes[i - 2] : prev;
                        LiveGpsFix p1 = prev;
                        LiveGpsFix p2 = next;
                        LiveGpsFix p3 = (i + 1 < liveGpsFixes.Count) ? liveGpsFixes[i + 1] : next;

                        displayPos = EvaluateCatmullRom(p0.position, p1.position, p2.position, p3.position, fraction);
                        direction = EvaluateCatmullRomTangent(p0.position, p1.position, p2.position, p3.position, fraction);
                    }
                    else
                    {
                        // Hermite SmoothStep interpolation fallback
                        float smoothT = Mathf.SmoothStep(0f, 1f, fraction);
                        displayPos = Vector3.Lerp(prev.position, next.position, smoothT);
                        direction = next.position - prev.position;
                    }

                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.001f) lastKnownVelocityDirection = direction.normalized;
                    displayedSpeedKmh = (next.position - prev.position).magnitude / duration * 3.6f;
                    break;
                }
            }

            if (!isOnline)
            {
                displayPos = initial ? last.position : transform.position;
                displayedSpeedKmh = 0f;
            }
            if (initial) ClearTireTracks();
            else if (FlatDistance(transform.position, displayPos) < 25f)
                totalDistanceKm += FlatDistance(transform.position, displayPos) * 0.001f;

            // Smooth position movement and ground terrain tracking
            if (!initial)
            {
                float smoothSpeed = Mathf.Max(6f, displayedSpeedKmh * 0.35f);
                transform.position = Vector3.Lerp(transform.position, displayPos, Mathf.Clamp01(dt * smoothSpeed));
                AlignToGround(instant: false);
            }
            else
            {
                transform.position = displayPos;
                AlignToGround(instant: true);
            }

            currentSpeedKmh = Mathf.Lerp(currentSpeedKmh, displayedSpeedKmh, dt * 8f);
            targetSpeedKmh = displayedSpeedKmh;
            
            bool excavating = unitType == UnitType.Excavator && !string.IsNullOrEmpty(activityName) &&
                (activityName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 activityName.IndexOf("Digging", StringComparison.OrdinalIgnoreCase) >= 0);
            
            UnitState nextState = !isOnline ? UnitState.Offline
                : unitType == UnitType.Excavator
                    ? currentSpeedKmh > 0.5f ? UnitState.TravellingToLoad : excavating ? UnitState.Loading : UnitState.Idle
                    : currentSpeedKmh > 0.5f ? UnitState.Hauling : UnitState.Idle;
            
            if (currentState != nextState)
            {
                currentState = nextState;
                UpdateStatusColor();
            }

            // Smooth rotation & steering interpolation
            if (direction.sqrMagnitude > 0.01f && currentSpeedKmh > 0.4f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                float steerSpeed = Mathf.Max(steeringSmoothSpeed, 6.5f);
                transform.rotation = initial ? targetRotation : Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Clamp01(dt * steerSpeed));
                headingDegrees = transform.eulerAngles.y;
            }
            else if (unitType == UnitType.Excavator && isOnline && last.headingDeg > 0.1f && last.headingDeg < 360f)
            {
                Quaternion targetRotation = Quaternion.Euler(0f, last.headingDeg, 0f);
                transform.rotation = initial ? targetRotation : Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Clamp01(dt * 5.0f));
                headingDegrees = transform.eulerAngles.y;
            }
        }

        [Header("GPS Waypoint FIFO Queue & Dead-Reckoning")]
        private Queue<Vector3> gpsPointQueue = new Queue<Vector3>();
        public float timeSinceLastGps = 0f;
        public float signalLossTimeout = 6.0f;
        public bool isDeadReckoning = false;
        private Vector3 lastKnownVelocityDirection = Vector3.forward;

        [Header("Visual Components & Articulations")]
        public Transform visualModelRoot;
        public bool gpsProximityConflict;
        public float visualSeparationMeters;
        private Vector3 visualOffsetLocal;
        public Transform cabTransform;
        public Transform dumpBedTransform;
        public Transform boomTransform;
        public Transform stickTransform;
        public Transform bucketTransform;
        public Transform bucketOreTransform;
        public Transform bladeTransform;
        public Transform ripperTransform;
        public Transform[] wheels;
        public Renderer statusBeaconRenderer;
        private MaterialPropertyBlock statusBeaconPropertyBlock;

        [Header("Tire Tracks & Visual Effects")]
        public TrailRenderer leftTireTrack;
        public TrailRenderer rightTireTrack;
        private static Material sharedTireTrackMat;

        // Internal State Timers
        private float stateTimer = 0f;
        private float liveLoadingCycleTime;
        private Transform animatedLoadingRoot;
        private Transform animatedLoadingCab;
        private Transform animatedLoadingBoom;
        private Transform animatedLoadingStick;
        private Transform animatedLoadingBucket;
        private Quaternion loadingRootRest;
        private Quaternion loadingCabRest;
        private Quaternion loadingBoomRest;
        private Quaternion loadingStickRest;
        private Quaternion loadingBucketRest;
        private float baseStationaryYaw = 0f;
        private Vector3 patrolBasePos;
        private bool isPatrolForward = true;
        private float stationaryWatchdogTimer = 0f;
        private float queueWatchdogTimer = 0f;

        public static void EnsureEffectsMaterials()
        {
            if (sharedTireTrackMat == null)
            {
                Shader unlitShader = Shader.Find("Particles/Standard Unlit") 
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                    ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                    ?? Shader.Find("Sprites/Default") 
                    ?? Shader.Find("Standard");

                sharedTireTrackMat = new Material(unlitShader);
                sharedTireTrackMat.color = new Color(0.12f, 0.09f, 0.06f, 0.20f);
                if (sharedTireTrackMat.HasProperty("_Color")) sharedTireTrackMat.SetColor("_Color", new Color(0.12f, 0.09f, 0.06f, 0.20f));
            }
        }

        public void ClearTireTracks()
        {
            if (leftTireTrack != null) leftTireTrack.Clear();
            if (rightTireTrack != null) rightTireTrack.Clear();
        }

        private void Start()
        {
            patrolBasePos = transform.position;
            baseStationaryYaw = transform.eulerAngles.y;

            AlignToGround(instant: true);

            if (visualModelRoot == null && transform.childCount > 0)
            {
                visualModelRoot = transform.Find("VisualModel") ?? transform.GetChild(0);
            }

            if (waypoints != null && waypoints.Count > 1 && !hasTarget && gpsPointQueue.Count == 0)
            {
                currentTargetPoint = GetLaneTargetPoint(currentWaypointIndex);
                hasTarget = true;
            }

            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(8f, 6f, 12f);
                col.center = new Vector3(0f, 3f, 0f);
            }

            SetupTireTracks();
            ClearTireTracks();

            if (GetComponent<FMSUnitLighting>() == null)
            {
                gameObject.AddComponent<FMSUnitLighting>();
            }

            UpdateStatusColor();
        }

        private void SetupTireTracks()
        {
            if (unitType == UnitType.Support) return;
            if (leftTireTrack != null && rightTireTrack != null) return;
            EnsureEffectsMaterials();

            float trackSpacing = (unitType == UnitType.HaulTruck || unitType == UnitType.WheelLoader) ? 2.1f : 1.5f;
            float trackWidth = (unitType == UnitType.HaulTruck || unitType == UnitType.WheelLoader) ? 0.95f : 0.65f;

            // Left Track
            GameObject leftObj = new GameObject("LeftTireTrack");
            leftObj.transform.SetParent(transform, false);
            leftObj.transform.localPosition = new Vector3(-trackSpacing, 0.08f, -2.6f);
            leftTireTrack = leftObj.AddComponent<TrailRenderer>();
            leftTireTrack.material = sharedTireTrackMat;
            leftTireTrack.time = 2.0f; // Clean & short tire tracks (fade in 2 seconds)
            leftTireTrack.startWidth = trackWidth;
            leftTireTrack.endWidth = trackWidth * 0.2f;
            leftTireTrack.minVertexDistance = 0.8f;
            leftTireTrack.autodestruct = false;
            leftTireTrack.emitting = false;
            leftTireTrack.alignment = LineAlignment.TransformZ;

            // Right Track
            GameObject rightObj = new GameObject("RightTireTrack");
            rightObj.transform.SetParent(transform, false);
            rightObj.transform.localPosition = new Vector3(trackSpacing, 0.08f, -2.6f);
            rightTireTrack = rightObj.AddComponent<TrailRenderer>();
            rightTireTrack.material = sharedTireTrackMat;
            rightTireTrack.time = 2.0f; // Clean & short tire tracks (fade in 2 seconds)
            rightTireTrack.startWidth = trackWidth;
            rightTireTrack.endWidth = trackWidth * 0.2f;
            rightTireTrack.minVertexDistance = 0.8f;
            rightTireTrack.autodestruct = false;
            rightTireTrack.emitting = false;
            rightTireTrack.alignment = LineAlignment.TransformZ;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            timeSinceLastGps += dt;
            secondsSinceLastBackendUpdate += dt;
            suspensionBounceY = Mathf.MoveTowards(suspensionBounceY, 0f, dt * 0.8f);

            RenderLiveGpsPosition(dt);
            if (unitType == UnitType.Excavator) AnimateLiveExcavatorLoading(dt);

            // 2. Animate Wheel Rotation
            AnimateWheels(dt);

            // Anti-stacking soft separation (prevents units from clipping into each other)
            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet != null)
            {
                foreach (var other in FMSFleetManager.Instance.activeFleet)
                {
                    if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                    Vector3 diff = transform.position - other.transform.position;
                    diff.y = 0f;
                    float d = diff.magnitude;
                    if (d < 7.2f && d > 0.05f) // Bounding radius for large mining haulers
                    {
                        // Push purely sideways relative to transform.forward, never backward
                        Vector3 rightVec = transform.right;
                        float sideSign = (Vector3.Dot(diff, rightVec) >= 0f) ? 1.0f : -1.0f;
                        float pushStrength = (7.2f - d) * 0.6f;
                        transform.position += rightVec * (sideSign * pushStrength * dt * 4f);
                    }
                }
            }

            // Smooth decay of suspension bounce
            if (Mathf.Abs(suspensionBounceY) > 0.001f)
            {
                suspensionBounceY = Mathf.Lerp(suspensionBounceY, 0f, dt * 8f);
            }

            // 3. Terrain Normal & Elevation Alignment
            AlignToGround(instant: false);

            // 4. Update dynamic engine sensor telemetry
            UpdateEngineSensors(dt);

            // 5. Update Tire Tracks
            UpdateVisualEffects(dt);
        }

        private void UpdateVisualEffects(float dt)
        {
            bool isMoving = currentSpeedKmh > 1.2f;

            // Emit tire tracks when rolling on ground
            if (leftTireTrack != null) leftTireTrack.emitting = isMoving;
            if (rightTireTrack != null) rightTireTrack.emitting = isMoving;
        }

        public Vector3 VisualWorldPosition => visualModelRoot != null ? visualModelRoot.position : transform.position;

        public void SetLiveVisualOffset(Vector3 worldOffset)
        {
            if (visualModelRoot == null) return;
            visualOffsetLocal = transform.InverseTransformVector(worldOffset);
            visualModelRoot.localPosition = visualOffsetLocal;
            visualSeparationMeters = worldOffset.magnitude;
            var collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                Vector3 center = collider.center;
                center.x = visualOffsetLocal.x;
                center.y = collider.size.y * 0.5f + visualOffsetLocal.y;
                center.z = visualOffsetLocal.z;
                collider.center = center;
            }
        }

        private void AnimateLiveExcavatorLoading(float dt)
        {
            if (visualModelRoot == null) return;
            bool loading = isOnline && hasValidGpsFix && backendLastHeardSeconds <= 120 &&
                secondsSinceLastBackendUpdate <= 30f && currentSpeedKmh < 1f &&
                (backendActivityId == 23 || backendActivityId == 34 ||
                 (!string.IsNullOrEmpty(activityName) &&
                  (activityName.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   activityName.IndexOf("digging", StringComparison.OrdinalIgnoreCase) >= 0)));

            Transform activeCab = cabTransform != null && cabTransform.gameObject.activeInHierarchy ? cabTransform : null;
            Transform activeBoom = boomTransform != null && boomTransform.gameObject.activeInHierarchy ? boomTransform : null;
            Transform activeStick = stickTransform != null && stickTransform.gameObject.activeInHierarchy ? stickTransform : null;
            Transform activeBucket = bucketTransform != null && bucketTransform.gameObject.activeInHierarchy ? bucketTransform : null;
            if (animatedLoadingRoot != visualModelRoot || animatedLoadingCab != activeCab ||
                animatedLoadingBoom != activeBoom || animatedLoadingStick != activeStick ||
                animatedLoadingBucket != activeBucket)
            {
                animatedLoadingRoot = visualModelRoot;
                animatedLoadingCab = activeCab;
                animatedLoadingBoom = activeBoom;
                animatedLoadingStick = activeStick;
                animatedLoadingBucket = activeBucket;
                loadingRootRest = visualModelRoot.localRotation;
                if (activeCab != null) loadingCabRest = activeCab.localRotation;
                if (activeBoom != null) loadingBoomRest = activeBoom.localRotation;
                if (activeStick != null) loadingStickRest = activeStick.localRotation;
                if (activeBucket != null) loadingBucketRest = activeBucket.localRotation;
            }

            if (loading) liveLoadingCycleTime += dt;
            float phase = liveLoadingCycleTime * (Mathf.PI * 2f / 9f);
            float lift = Mathf.Sin(phase);
            float swing = Mathf.Sin(phase - 1.1f);
            float blend = Mathf.Clamp01(dt * 5f);
            bool hasVisibleRig = activeBoom != null && activeBucket != null;
            Quaternion rootTarget = loading && !hasVisibleRig
                ? loadingRootRest * Quaternion.Euler(0f, swing * 9f, lift * 1.5f)
                : loadingRootRest;
            visualModelRoot.localRotation = Quaternion.Slerp(visualModelRoot.localRotation, rootTarget, blend);
            if (activeCab != null)
                activeCab.localRotation = Quaternion.Slerp(activeCab.localRotation,
                    loadingCabRest * Quaternion.Euler(0f, loading ? swing * 22f : 0f, 0f), blend);
            if (activeBoom != null)
                activeBoom.localRotation = Quaternion.Slerp(activeBoom.localRotation,
                    loadingBoomRest * Quaternion.Euler(loading ? lift * 14f : 0f, 0f, 0f), blend);
            if (activeStick != null)
                activeStick.localRotation = Quaternion.Slerp(activeStick.localRotation,
                    loadingStickRest * Quaternion.Euler(loading ? Mathf.Sin(phase + 1f) * 17f : 0f, 0f, 0f), blend);
            if (activeBucket != null)
                activeBucket.localRotation = Quaternion.Slerp(activeBucket.localRotation,
                    loadingBucketRest * Quaternion.Euler(loading ? Mathf.Sin(phase + 2f) * 25f : 0f, 0f, 0f), blend);
        }

        // =========================================================================
        // HAUL TRUCK / MOVING ROUTE KINEMATICS (100% STRICT LIVE GPS TELEMETRY)
        // =========================================================================
        private void UpdateHaulerMovement(float dt)
        {
            // If GPS timed out (> 15s without update), or unit is offline -> smoothly decelerate to STOP
            if (timeSinceLastGps > 15.0f || !isOnline)
            {
                currentSpeedKmh = Mathf.MoveTowards(currentSpeedKmh, 0f, dt * 20f);
                targetSpeedKmh = 0f;
                hasTarget = false;
                return;
            }

            ProcessWaypointQueue(dt);

            currentSpeedKmh = Mathf.MoveTowards(currentSpeedKmh, targetSpeedKmh, dt * 25f);
            float speedMps = currentSpeedKmh / 3.6f;

            if (currentSpeedKmh > 0.5f)
            {
                Vector3 forwardTangent = GetProjectedTrajectoryTangent();

                // 1. Smoothly orient chassis rotation to trajectory tangent and live GPS heading
                if (forwardTangent.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(forwardTangent, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * steeringSmoothSpeed);
                }
                else if (headingDegrees >= 0f)
                {
                    Quaternion targetRot = Quaternion.Euler(0f, headingDegrees, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * steeringSmoothSpeed);
                }

                // 2. Continuous kinematic advance along forward tangent (smooth dead-reckoning)
                Vector3 moveDelta = transform.forward * (speedMps * dt);
                transform.position += moveDelta;
                totalDistanceKm += speedMps * dt * 0.001f;

                // 3. Smooth lateral corridor correction towards latest verified GPS coordinate
                if (currentTargetPoint.sqrMagnitude > 1.0f)
                {
                    Vector3 toGps = currentTargetPoint - transform.position;
                    toGps.y = 0f;
                    float distToGps = toGps.magnitude;
                    if (distToGps > 0.1f && distToGps < 45f)
                    {
                        // Soft convergence towards GPS road centerline without velocity stutter
                        transform.position += toGps * Mathf.Min(1.0f, dt * 2.2f);
                    }
                }
            }
        }

        public Vector3 GetLaneTargetPoint(int targetIdx)
        {
            if (waypoints == null || waypoints.Count == 0) return transform.position;
            targetIdx = Mathf.Clamp(targetIdx, 0, waypoints.Count - 1);
            Vector3 rawTarget = waypoints[targetIdx];

            // Determine route movement direction segment
            Vector3 segDir = Vector3.forward;
            if (isForwardRoute)
            {
                int prevIdx = Mathf.Max(0, targetIdx - 1);
                if (targetIdx == 0 && waypoints.Count > 1) segDir = (waypoints[1] - waypoints[0]).normalized;
                else segDir = (waypoints[targetIdx] - waypoints[prevIdx]).normalized;
            }
            else
            {
                int nextIdx = Mathf.Min(waypoints.Count - 1, targetIdx + 1);
                if (targetIdx == waypoints.Count - 1 && waypoints.Count > 1) segDir = (waypoints[waypoints.Count - 2] - waypoints[waypoints.Count - 1]).normalized;
                else segDir = (waypoints[targetIdx] - waypoints[nextIdx]).normalized;
            }

            segDir.y = 0f;
            if (segDir.sqrMagnitude < 0.001f) segDir = transform.forward;
            segDir.Normalize();

            // Left normal vector in travel direction (Left-Hand Traffic rule: turn left = -Cross(dir, up))
            Vector3 leftNormal = Vector3.Cross(Vector3.up, segDir).normalized;

            // Offset to the left by laneOffsetDistance
            return rawTarget + leftNormal * laneOffsetDistance;
        }

        private void ProcessWaypointQueue(float dt)
        {
            if (!hasTarget)
            {
                if (gpsPointQueue.Count > 0)
                {
                    currentTargetPoint = gpsPointQueue.Dequeue();
                    hasTarget = true;
                }
                else if (waypoints != null && waypoints.Count > 1)
                {
                    currentTargetPoint = GetLaneTargetPoint(currentWaypointIndex);
                    hasTarget = true;
                }
                return;
            }

            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatTarget = new Vector3(currentTargetPoint.x, 0f, currentTargetPoint.z);
            Vector3 toTarget = flatTarget - flatPos;
            float distToTarget = toTarget.magnitude;

            // Detect arrival OR overshoot (waypoint behind vehicle)
            bool isOvershot = (distToTarget < 12.0f && Vector3.Dot(toTarget, transform.forward) < -0.6f);

            if (distToTarget <= waypointArrivalThreshold || isOvershot)
            {
                if (gpsPointQueue.Count > 0)
                {
                    currentTargetPoint = gpsPointQueue.Dequeue();
                    hasTarget = true;
                }
                else if (waypoints != null && waypoints.Count > 1)
                {
                    if (isForwardRoute)
                    {
                        // Check if arrived at Disposal End
                        if (currentWaypointIndex >= waypoints.Count - 1)
                        {
                            // Arrived at Disposal End -> Start Dumping cycle
                            currentState = UnitState.Dumping;
                            stateTimer = 0f;
                            activityName = "Dumping at Disposal";
                            targetSpeedKmh = 0f;
                            currentSpeedKmh = 0f;
                            UpdateStatusColor();
                            return;
                        }
                        else
                        {
                            currentWaypointIndex++;
                        }
                    }
                    else
                    {
                        // Check if arrived at Pit Loading Front
                        if (currentWaypointIndex <= 0)
                        {
                            // Arrived at Pit Front -> Start Loading cycle
                            currentState = UnitState.Loading;
                            stateTimer = 0f;
                            activityName = "Loading at Pit Face";
                            targetSpeedKmh = 0f;
                            currentSpeedKmh = 0f;
                            UpdateStatusColor();
                            return;
                        }
                        else
                        {
                            currentWaypointIndex--;
                        }
                    }

                    currentTargetPoint = GetLaneTargetPoint(currentWaypointIndex);
                    hasTarget = true;
                }
                else
                {
                    hasTarget = false;
                }
            }
        }

        private void ExecuteKinematicMovement(float dt)
        {
            // 1. HARD ZERO-TOUCH REPEL, AHS FORWARD RADAR CONE, INTERSECTION YIELD & PROPORTIONAL ACC
            float effectiveTargetSpeed = targetSpeedKmh;
            const float HARD_REPEL_RADIUS = 7.5f;        // Physical hull zero-contact bounding circle
            const float CONVOY_FOLLOW_DISTANCE = 24.0f;  // Safe high-speed convoy buffer while travelling on haul road
            const float QUEUE_STOP_DISTANCE = 13.5f;     // Compact realistic staging queue buffer behind stopped/loading trucks
            const float RADAR_LOOKAHEAD_DIST = 40.0f;    // AHS Radar lookahead distance
            bool mustStop = false;

            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet != null)
            {
                Vector3 myPos = transform.position;
                Vector3 myForward = transform.forward;
                myForward.y = 0f;
                if (myForward.sqrMagnitude > 0.01f) myForward.Normalize();

                foreach (var other in FMSFleetManager.Instance.activeFleet)
                {
                    if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;

                    Vector3 toOther = other.transform.position - myPos;
                    toOther.y = 0f;
                    float dist = toOther.magnitude;

                    // Compute relative forward & lateral distance along our travel trajectory
                    float forwardDist = Vector3.Dot(myForward, toOther);
                    Vector3 lateralVec = toOther - myForward * forwardDist;
                    float lateralDist = lateralVec.magnitude;

                    Vector3 otherForward = other.transform.forward;
                    otherForward.y = 0f;
                    if (otherForward.sqrMagnitude > 0.01f) otherForward.Normalize();
                    float alignmentDot = Vector3.Dot(myForward, otherForward);

                    // A. PHYSICAL CONTACT PROXIMITY SAFETY (Zero-Overlap Lateral Separation)
                    if (dist < HARD_REPEL_RADIUS)
                    {
                        // Active resolution: higher instance ID moves at 8 km/h, other crawls at 4 km/h while separating
                        bool hasPriority = (this.GetInstanceID() > other.GetInstanceID());
                        effectiveTargetSpeed = hasPriority ? Mathf.Max(effectiveTargetSpeed, 8f) : Mathf.Min(effectiveTargetSpeed, 4f);

                        // Push apart laterally along corridor normal
                        if (dist > 0.01f)
                        {
                            float penetration = HARD_REPEL_RADIUS - dist;
                            Vector3 sideNormal = Vector3.Cross(Vector3.up, myForward).normalized;
                            if (Vector3.Dot(toOther, sideNormal) > 0f) sideNormal = -sideNormal;
                            transform.position += sideNormal * (penetration * 0.5f * dt * 8f);
                        }
                    }
                    
                    // B. AHS FORWARD RADAR CORRIDOR (Same-Lane Convoy & Stationary Obstacles Ahead)
                    else if (alignmentDot > 0.2f && forwardDist > 0.5f && forwardDist < RADAR_LOOKAHEAD_DIST)
                    {
                        // In-Lane Forward Obstacle / Convoy Leader
                        if (lateralDist < 5.2f)
                        {
                            bool isQueueAhead = (other.currentSpeedKmh < 1.0f || other.currentState == UnitState.Loading || other.currentState == UnitState.QueueingAtPit || other.currentState == UnitState.Dumping || other.currentState == UnitState.QueueingAtDump);
                            float targetSafeDist = isQueueAhead ? QUEUE_STOP_DISTANCE : CONVOY_FOLLOW_DISTANCE;

                            if (forwardDist < 5.5f)
                            {
                                effectiveTargetSpeed = 0f;
                                if (other.currentState == UnitState.Loading || other.currentState == UnitState.QueueingAtPit)
                                {
                                    if (currentState == UnitState.TravellingToLoad)
                                    {
                                        currentState = UnitState.QueueingAtPit;
                                        activityName = "Queueing for Shovel Loading";
                                        UpdateStatusColor();
                                    }
                                }
                                else if (other.currentState == UnitState.Dumping || other.currentState == UnitState.QueueingAtDump)
                                {
                                    if (currentState == UnitState.Hauling)
                                    {
                                        currentState = UnitState.QueueingAtDump;
                                        activityName = "Queueing at Disposal Hopper";
                                        UpdateStatusColor();
                                    }
                                }
                                mustStop = true;
                                break;
                            }
                            else if (forwardDist < targetSafeDist)
                            {
                                // Rolling queue crawl (6-8 km/h) instead of dead-stop
                                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, 6.5f);
                            }
                            else
                            {
                                // Adaptive Cruise Control (ACC) Smooth Slowdown
                                float brakeFactor = Mathf.Clamp01((forwardDist - targetSafeDist) / (RADAR_LOOKAHEAD_DIST - targetSafeDist));
                                float matchedSpeed = Mathf.Min(targetSpeedKmh, Mathf.Max(6f, other.currentSpeedKmh * 0.9f)) * brakeFactor;
                                if (matchedSpeed < effectiveTargetSpeed)
                                {
                                    effectiveTargetSpeed = matchedSpeed;
                                }
                            }
                        }
                    }
                    // C. INTERSECTION / DIAGONAL CROSS-TRAFFIC (Crossing or merging ahead within 18m)
                    else if (alignmentDot > -0.2f && alignmentDot < 0.6f && lateralDist < 8.5f && forwardDist > 0.5f && forwardDist < 18.0f)
                    {
                        if (other.currentSpeedKmh > 1.0f)
                        {
                            bool yieldRightOfWay = (this.GetInstanceID() < other.GetInstanceID());
                            if (yieldRightOfWay)
                            {
                                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, 4f);
                            }
                            else
                            {
                                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, 14f);
                            }
                        }
                    }
                    // D. ONCOMING TRAFFIC (Opposite direction in adjacent lane - Left-Hand Traffic)
                    else if (alignmentDot < -0.2f && forwardDist > 0.5f && forwardDist < 25f && lateralDist < 4.5f)
                    {
                        // Glide past each other smoothly with Left-Hand lateral bias - never dead-stop
                        effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, 16f);
                        transform.position -= transform.right * (dt * 1.5f);
                    }
                }

                if (mustStop)
                {
                    effectiveTargetSpeed = 0f;
                }
                else
                {
                    if (currentState == UnitState.QueueingAtPit)
                    {
                        currentState = UnitState.TravellingToLoad;
                        activityName = "Travelling (Empty)";
                        UpdateStatusColor();
                    }
                    else if (currentState == UnitState.QueueingAtDump)
                    {
                        currentState = UnitState.Hauling;
                        activityName = "Hauling (Loaded Coal/OB)";
                        UpdateStatusColor();
                    }
                }
            }

            // Smooth Acceleration / Deceleration
            float accelRate = (effectiveTargetSpeed < currentSpeedKmh) ? (accelerationSpeed * 10f) : (accelerationSpeed * 4f);
            currentSpeedKmh = Mathf.MoveTowards(currentSpeedKmh, effectiveTargetSpeed, dt * accelRate);
            float speedMps = currentSpeedKmh / 3.6f;

            if (hasTarget && speedMps > 0.02f)
            {
                Vector3 moveDir = (currentTargetPoint - transform.position);
                moveDir.y = 0f;

                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Vector3 normDir = moveDir.normalized;
                    lastKnownVelocityDirection = normDir;

                    // Move Position
                    transform.position += normDir * speedMps * dt;
                    totalDistanceKm += (speedMps * dt) * 0.001f;

                    // Smooth Steering Orientation
                    Quaternion targetRot = Quaternion.LookRotation(normDir, Vector3.up);
                    float prevYaw = transform.eulerAngles.y;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * steeringSmoothSpeed);
                    headingDegrees = transform.eulerAngles.y;

                    // Calculate angular turning delta for roll banking
                    currentSteeringAngleDelta = Mathf.DeltaAngle(prevYaw, headingDegrees) / Mathf.Max(0.001f, dt);
                }
            }
        }

        public void UpdateFromLiveTelemetry(Vector3 liveUnityPos, float liveSpeedKmh, float liveHeadingDeg,
            string liveActivity, bool liveActive, List<TrajectoryPointDto> liveTrajectory = null,
            string lastFixTime = null, string serverTime = null)
        {
            isLiveTelemetryControlled = true;
            isOnline = liveActive;
            if (!string.IsNullOrEmpty(liveActivity)) activityName = liveActivity;

            if (liveUnityPos.sqrMagnitude > 1f)
            {
                UpdateLiveGpsTimeline(liveTrajectory, liveUnityPos, liveHeadingDeg, lastFixTime, serverTime);
                UpdateStatusColor();
            }
            else
            {
                targetSpeedKmh = 0f;
                currentSpeedKmh = 0f;
                currentState = UnitState.Offline;
                UpdateStatusColor();
            }
        }

        // =========================================================================
        // EXCAVATOR BEHAVIOR (Realistic 4-Phase Mining Shovel Loading Haul Truck)
        // =========================================================================
        private void UpdateExcavatorBehavior(float dt)
        {
            bool isSim = (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isSimulationMode);
            if (!isSim && (timeSinceLastGps > 4.0f || !isOnline))
            {
                currentSpeedKmh = 0f;
                return;
            }

            stateTimer += dt;
            currentSpeedKmh = 0f;

            // 1. Search for closest haul truck being loaded nearby (within 35m)
            FMSUnitController targetTruck = null;
            float nearestTruckDist = 35f;

            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet != null)
            {
                foreach (var u in FMSFleetManager.Instance.activeFleet)
                {
                    if (u == null || u == this || u.unitType != UnitType.HaulTruck) continue;
                    float d = Vector3.Distance(transform.position, u.transform.position);
                    if (d < nearestTruckDist && (u.currentState == UnitState.Loading || u.currentState == UnitState.QueueingAtPit))
                    {
                        nearestTruckDist = d;
                        targetTruck = u;
                    }
                }
            }

            // 12-second periodic loading cycle
            float cycleDuration = 12.0f;
            float cycleTime = stateTimer % cycleDuration;

            // Determine target slew angle towards truck bed
            float targetSlew = 0f;
            if (targetTruck != null)
            {
                Vector3 toTruck = targetTruck.transform.position - transform.position;
                toTruck.y = 0f;
                if (toTruck.sqrMagnitude > 0.01f)
                {
                    float angleToTruck = Mathf.Atan2(toTruck.x, toTruck.z) * Mathf.Rad2Deg;
                    targetSlew = Mathf.DeltaAngle(baseStationaryYaw, angleToTruck);
                    targetSlew = Mathf.Clamp(targetSlew, -85f, 85f);
                }
            }
            else
            {
                targetSlew = 65f; // default loading bay swing angle
            }

            float curSlew = 0f;
            float targetBoom = -24f;
            float targetStick = 52f;
            float targetBucket = 15f;
            bool showBucketOre = false;

            if (cycleTime < 4.0f)
            {
                // PHASE 1: Digging Coal/OB at Pit Highwall Face (0 - 4s)
                // Bucket goes down to the ground (-44 deg), stick reaches forward, bucket curls up to scoop (+55 deg)
                float t = cycleTime / 4.0f;
                curSlew = 0f; // Slew aligned with pit face cut
                targetBoom = Mathf.Lerp(-20f, -44f, Mathf.Sin(t * Mathf.PI)); // Boom dips down
                targetStick = Mathf.Lerp(42f, 80f, t); // Stick extends out then crowds in
                targetBucket = Mathf.Lerp(-35f, 55f, t); // Scoop up and curl bucket tight
                activityName = "Scooping Coal at Pit Face";
                showBucketOre = (t > 0.45f); // Ore appears in bucket after curling
            }
            else if (cycleTime < 6.5f)
            {
                // PHASE 2: Hoist Boom High & Slew over Haul Truck (4 - 6.5s)
                // Boom rises high (+8 deg), lifting bucket well above truck sideboard
                float t = (cycleTime - 4.0f) / 2.5f;
                curSlew = Mathf.SmoothStep(0f, targetSlew, t); // Slew towards truck bed
                targetBoom = Mathf.Lerp(-44f, 8f, t); // Hoist boom high up into the air
                targetStick = Mathf.Lerp(80f, 48f, t);
                targetBucket = 55f; // Hold material securely
                activityName = "Swinging over Haul Truck";
                showBucketOre = true;
            }
            else if (cycleTime < 9.0f)
            {
                // PHASE 3: Discharging / Dumping into Truck Hopper (6.5 - 9s)
                // Bucket articulates sharply downwards (-65 deg) to dump material
                float t = (cycleTime - 6.5f) / 2.5f;
                curSlew = targetSlew; // Positioned right above truck hopper
                targetBoom = Mathf.Lerp(8f, -6f, t); // Slight boom lower for precision drop
                targetStick = 52f;
                targetBucket = Mathf.Lerp(55f, -65f, Mathf.SmoothStep(0f, 1f, t * 1.6f)); // Open/tilt bucket downward
                activityName = "Dumping Coal into Truck Bed";
                showBucketOre = (t < 0.45f); // Ore drops out of bucket into truck bed

                // Trigger truck suspension bounce and fill load
                if (targetTruck != null && t > 0.35f && t < 0.85f)
                {
                    targetTruck.TriggerSuspensionLoadBounce();
                    float fillRatio = Mathf.Clamp01((targetTruck.payloadTons + 25f) / 95f);
                    targetTruck.SetCoalLoadScale(fillRatio);
                }
            }
            else
            {
                // PHASE 4: Return Swing back to Pit Face (9 - 12s)
                // Boom lowers back to digging stance, bucket resets
                float t = (cycleTime - 9.0f) / 3.0f;
                curSlew = Mathf.SmoothStep(targetSlew, 0f, t); // Return swing to highwall
                targetBoom = Mathf.Lerp(-6f, -20f, t);
                targetStick = Mathf.Lerp(52f, 42f, t);
                targetBucket = Mathf.Lerp(-65f, -35f, t); // Reset bucket to digging approach angle
                activityName = "Returning Swing to Face";
                showBucketOre = false;
            }

            // Toggle bucket payload visibility
            if (bucketOreTransform != null)
            {
                bucketOreTransform.gameObject.SetActive(showBucketOre);
            }

            // Apply rotations smoothly
            if (cabTransform != null)
            {
                cabTransform.localRotation = Quaternion.Slerp(cabTransform.localRotation, Quaternion.Euler(0f, curSlew, 0f), dt * 4.0f);
            }
            else if (visualModelRoot != null)
            {
                visualModelRoot.localRotation = Quaternion.Slerp(visualModelRoot.localRotation, Quaternion.Euler(0f, curSlew, 0f), dt * 4.0f);
            }

            if (boomTransform != null)
            {
                boomTransform.localRotation = Quaternion.Slerp(boomTransform.localRotation, Quaternion.Euler(targetBoom, 0f, 0f), dt * 3.5f);
            }

            if (stickTransform != null)
            {
                stickTransform.localRotation = Quaternion.Slerp(stickTransform.localRotation, Quaternion.Euler(targetStick, 0f, 0f), dt * 3.5f);
            }

            if (bucketTransform != null)
            {
                bucketTransform.localRotation = Quaternion.Slerp(bucketTransform.localRotation, Quaternion.Euler(targetBucket, 0f, 0f), dt * 4.0f);
            }
        }

        public void TriggerSuspensionLoadBounce()
        {
            suspensionBounceY = -0.18f; // Truck dips under impact of coal dumped into bed
        }

        public void SetCoalLoadScale(float scaleRatio)
        {
            if (dumpBedTransform != null)
            {
                Transform coal = dumpBedTransform.Find("CoalOreLoad");
                if (coal != null)
                {
                    if (scaleRatio <= 0.02f)
                    {
                        coal.gameObject.SetActive(false);
                    }
                    else
                    {
                        coal.gameObject.SetActive(true);
                        coal.localScale = new Vector3(1f, Mathf.Clamp(scaleRatio, 0.15f, 1.0f), 1f);
                    }
                }
            }
        }

        // =========================================================================
        // BULLDOZER BEHAVIOR (Disposal Bench Push-and-Reverse Patrol)
        // =========================================================================
        private void UpdateBulldozerBehavior(float dt)
        {
            bool isSim = (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isSimulationMode);
            if (!isSim && (timeSinceLastGps > 4.0f || !isOnline))
            {
                currentSpeedKmh = 0f;
                return;
            }

            stateTimer += dt;
            float patrolSpeedKmh = 8.5f;
            float patrolDist = 28f;

            float speedMps = patrolSpeedKmh / 3.6f;
            Vector3 forwardVec = Quaternion.Euler(0f, baseStationaryYaw, 0f) * Vector3.forward;

            if (isPatrolForward)
            {
                transform.position += forwardVec * speedMps * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, baseStationaryYaw, 0f), dt * 6f);
                currentSpeedKmh = patrolSpeedKmh;
                activityName = "Pushing Spoil Material";

                if (Vector3.Distance(transform.position, patrolBasePos) >= patrolDist)
                {
                    isPatrolForward = false;
                }
            }
            else
            {
                // Reversing backwards along same line (facing baseStationaryYaw)
                transform.position -= forwardVec * (speedMps * 0.85f) * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, baseStationaryYaw, 0f), dt * 6f);
                currentSpeedKmh = patrolSpeedKmh * 0.85f;
                activityName = "Reversing to Cut Line";

                if (Vector3.Dot(transform.position - patrolBasePos, forwardVec) <= 1.0f)
                {
                    isPatrolForward = true;
                }
            }

            // Blade tilt motion
            if (bladeTransform != null)
            {
                float bladeY = Mathf.Sin(stateTimer * 1.2f) * 0.15f;
                bladeTransform.localPosition = new Vector3(0f, 1.2f + bladeY, 3.5f);
            }
        }

        // =========================================================================
        // WHEEL LOADER BEHAVIOR (Short Stockpile Maneuver)
        // =========================================================================
        private void UpdateWheelLoaderBehavior(float dt)
        {
            bool isSim = (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isSimulationMode);
            if (!isSim && (timeSinceLastGps > 4.0f || !isOnline))
            {
                currentSpeedKmh = 0f;
                return;
            }

            stateTimer += dt;
            float loaderSpeed = 12f;
            float speedMps = loaderSpeed / 3.6f;
            Vector3 forwardVec = Quaternion.Euler(0f, baseStationaryYaw, 0f) * Vector3.forward;

            if (isPatrolForward)
            {
                transform.position += forwardVec * speedMps * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, baseStationaryYaw, 0f), dt * 6f);
                currentSpeedKmh = loaderSpeed;
                activityName = "Loading Stockpile Coal";

                if (Vector3.Distance(transform.position, patrolBasePos) >= 20f)
                {
                    isPatrolForward = false;
                }
            }
            else
            {
                transform.position -= forwardVec * (speedMps * 0.8f) * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, baseStationaryYaw, 0f), dt * 6f);
                currentSpeedKmh = loaderSpeed * 0.8f;
                activityName = "Returning to Hopper";

                if (Vector3.Dot(transform.position - patrolBasePos, forwardVec) <= 1.5f)
                {
                    isPatrolForward = true;
                }
            }

            // Lift boom slightly when moving forward
            if (boomTransform != null)
            {
                float boomAngle = isPatrolForward ? -12f : 10f;
                boomTransform.localRotation = Quaternion.Slerp(boomTransform.localRotation, Quaternion.Euler(boomAngle, 0f, 0f), dt * 2.5f);
            }
        }

        // =========================================================================
        // WHEEL ANIMATION & GROUND CONFORMITY
        // =========================================================================
        private void AnimateWheels(float dt)
        {
            if (wheels == null || wheels.Length == 0 || currentSpeedKmh < 0.1f) return;

            float wheelRadiusMeters = (unitType == UnitType.HaulTruck || unitType == UnitType.WheelLoader) ? 1.35f : 0.8f;
            float angularDeltaDeg = (currentSpeedKmh / 3.6f) / wheelRadiusMeters * Mathf.Rad2Deg * dt;

            foreach (var w in wheels)
            {
                if (w != null)
                {
                    // Rotate child tire cylinder
                    Transform tireChild = w.Find("Tire");
                    if (tireChild != null) tireChild.Rotate(Vector3.up, angularDeltaDeg, Space.Self);
                    else w.Rotate(Vector3.right, angularDeltaDeg, Space.Self);
                }
            }
        }

        public void AlignToGround(bool instant = false)
        {
            Terrain activeTerrain = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            float groundY = transform.position.y;
            Vector3 terrainNormal = Vector3.up;

            if (activeTerrain != null)
            {
                groundY = activeTerrain.SampleHeight(transform.position) + activeTerrain.transform.position.y;
                terrainNormal = activeTerrain.terrainData.GetInterpolatedNormal(
                    (transform.position.x - activeTerrain.transform.position.x) / activeTerrain.terrainData.size.x,
                    (transform.position.z - activeTerrain.transform.position.z) / activeTerrain.terrainData.size.z
                );
            }
            else
            {
                // Multi-hit raycast ignoring self and unit colliders to eliminate vertical ground flickering
                RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 40f, Vector3.down, 120f);
                float bestY = -9999f;
                Vector3 bestNormal = Vector3.up;
                bool foundGround = false;

                foreach (var hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    if (hit.transform.root == transform || hit.transform.GetComponentInParent<FMSUnitController>() != null) continue;

                    if (hit.point.y > bestY)
                    {
                        bestY = hit.point.y;
                        bestNormal = hit.normal;
                        foundGround = true;
                    }
                }

                if (foundGround)
                {
                    groundY = bestY;
                    terrainNormal = bestNormal;
                }
            }

            Vector3 targetPos = new Vector3(transform.position.x, groundY + 0.1f + suspensionBounceY, transform.position.z);
            if (instant)
            {
                transform.position = targetPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 18f);
            }

            // Slope Pitch & Roll alignment with subtle turning chassis roll
            if (terrainNormal.sqrMagnitude > 0.01f)
            {
                Quaternion slopeRot = Quaternion.FromToRotation(transform.up, terrainNormal) * transform.rotation;
                if (!instant && Mathf.Abs(currentSteeringAngleDelta) > 0.05f)
                {
                    float rollBank = Mathf.Clamp(-currentSteeringAngleDelta * 0.04f, -2.5f, 2.5f);
                    slopeRot *= Quaternion.Euler(0f, 0f, rollBank);
                }
                if (instant) transform.rotation = slopeRot;
                else transform.rotation = Quaternion.Slerp(transform.rotation, slopeRot, Time.deltaTime * 6f);
            }
        }

        // =========================================================================
        // SENSORS, BEACON & STATE COLORS
        // =========================================================================
        private void UpdateEngineSensors(float dt)
        {
            // Realistic sensor fluctuations
            if (currentSpeedKmh > 1f)
            {
                float refMaxSpd = maxSpeedKmh > 0.1f ? maxSpeedKmh : 50f;
                engineRpm = Mathf.Lerp(engineRpm, 1400f + (currentSpeedKmh / refMaxSpd) * 750f + UnityEngine.Random.Range(-25f, 25f), dt * 2f);
                coolantTempC = Mathf.Lerp(coolantTempC, 88f + (currentSpeedKmh / refMaxSpd) * 5f, dt * 0.1f);
                fuelLevelPercent = Mathf.Max(10f, fuelLevelPercent - dt * 0.002f);
            }
            else
            {
                engineRpm = Mathf.Lerp(engineRpm, 750f + UnityEngine.Random.Range(-15f, 15f), dt * 2f);
            }
        }

        public void UpdateStatusColor()
        {
            switch (currentState)
            {
                case UnitState.Hauling:
                    statusColor = new Color(0.0f, 0.95f, 0.45f); // Green
                    break;
                case UnitState.TravellingToLoad:
                    statusColor = new Color(0.1f, 0.85f, 0.98f); // Cyan
                    break;
                case UnitState.Loading:
                    statusColor = new Color(0.2f, 0.9f, 1.0f); // Bright Cyan
                    break;
                case UnitState.Dumping:
                case UnitState.QueueingAtPit:
                case UnitState.QueueingAtDump:
                    statusColor = new Color(1.0f, 0.65f, 0.05f); // Orange / Yellow
                    break;
                case UnitState.Maintenance:
                    statusColor = new Color(0.95f, 0.2f, 0.15f);
                    break;
                case UnitState.Offline:
                    statusColor = isLiveTelemetryControlled && FMSFleetManager.Instance != null && FMSFleetManager.Instance.isTelemetryFeedStale
                        ? new Color(0.65f, 0.70f, 0.73f) : new Color(0.95f, 0.2f, 0.15f);
                    break;
            }

            if (statusBeaconRenderer != null)
            {
                if (statusBeaconPropertyBlock == null) statusBeaconPropertyBlock = new MaterialPropertyBlock();
                statusBeaconRenderer.GetPropertyBlock(statusBeaconPropertyBlock);
                statusBeaconPropertyBlock.SetColor("_Color", statusColor);
                statusBeaconPropertyBlock.SetColor("_BaseColor", statusColor);
                statusBeaconPropertyBlock.SetColor("_EmissionColor", statusColor * 2.5f);
                statusBeaconRenderer.SetPropertyBlock(statusBeaconPropertyBlock);
            }
        }

        private void SetCoalLoadVisible(bool visible)
        {
            if (dumpBedTransform != null)
            {
                Transform coal = dumpBedTransform.Find("CoalOreLoad");
                if (coal != null) coal.gameObject.SetActive(visible);
            }
        }

        // =========================================================================
        // STOPPAGE ROOT CAUSE & GPS DIAGNOSTICS
        // =========================================================================
        public StoppageReason GetCurrentStoppageReason()
        {
            if (currentSpeedKmh > 1.0f)
            {
                stoppageReason = StoppageReason.Moving;
                stoppageDiagnosticText = "Bergerak Normal";
                return StoppageReason.Moving;
            }

            if (currentState == UnitState.Loading)
            {
                stoppageReason = StoppageReason.OperationalLoading;
                stoppageDiagnosticText = unitType == UnitType.Excavator
                    ? $"Aktivitas tercatat: {activityName}" : $"Operasional: Sedang Dimuat ({payloadTons:F0}T)";
                return StoppageReason.OperationalLoading;
            }
            if (currentState == UnitState.QueueingAtPit)
            {
                stoppageReason = StoppageReason.OperationalQueuePit;
                stoppageDiagnosticText = "Operasional: Mengantre di Pit Loading Front";
                return StoppageReason.OperationalQueuePit;
            }
            if (currentState == UnitState.Dumping)
            {
                stoppageReason = StoppageReason.OperationalDumping;
                stoppageDiagnosticText = "Operasional: Sedang Dumping Material (Bak Terangkat)";
                return StoppageReason.OperationalDumping;
            }
            if (currentState == UnitState.QueueingAtDump)
            {
                stoppageReason = StoppageReason.OperationalQueueDump;
                stoppageDiagnosticText = "Operasional: Mengantre di Disposal / Hopper";
                return StoppageReason.OperationalQueueDump;
            }

            if (!hasValidGpsFix)
            {
                stoppageReason = StoppageReason.ApiGpsNoFixNull;
                stoppageDiagnosticText = "Belum Ada Sinyal GPS (Menunggu Fix)";
                return StoppageReason.ApiGpsNoFixNull;
            }

            if (isLiveTelemetryControlled && FMSFleetManager.Instance != null && FMSFleetManager.Instance.isTelemetryFeedStale)
            {
                stoppageReason = StoppageReason.ApiFleetFeedStale;
                stoppageDiagnosticText = "Feed GPS pusat belum memperbarui posisi";
                return stoppageReason;
            }

            if (!isOnline || backendLastHeardSeconds > 300)
            {
                stoppageReason = StoppageReason.ApiGpsDeviceOffline;
                stoppageDiagnosticText = $"Device Transponder Offline ({backendLastHeardSeconds}s lalu)";
                return StoppageReason.ApiGpsDeviceOffline;
            }

            if (unitType == UnitType.Excavator)
            {
                stoppageReason = StoppageReason.OperationalStandby;
                stoppageDiagnosticText = $"Aktivitas tercatat: {activityName}";
                return stoppageReason;
            }

            stoppageReason = StoppageReason.OperationalStandby;
            stoppageDiagnosticText = "Operasional: Standby / Parkir Staging Bay";
            return StoppageReason.OperationalStandby;
        }

        public string GetDiagnosticDescription()
        {
            var reason = GetCurrentStoppageReason();
            return reason switch
            {
                StoppageReason.Moving => $"🟢 Bergerak Normal ({currentSpeedKmh:F1} KM/Jam)",
                StoppageReason.OperationalLoading => unitType == UnitType.Excavator
                    ? $"Aktivitas tercatat: {activityName}" : $"🟡 Operasional: Sedang Dimuat di Front Gali ({payloadTons:F0}T)",
                StoppageReason.OperationalQueuePit => "🟡 Operasional: Mengantre Shovel di Pit Front",
                StoppageReason.OperationalDumping => "🟡 Operasional: Dumping Material (Bak Terangkat)",
                StoppageReason.OperationalQueueDump => "🟡 Operasional: Mengantre di Disposal / Hopper",
                StoppageReason.OperationalStandby => unitType == UnitType.Excavator
                    ? $"Aktivitas tercatat: {activityName}" : "🔵 Operasional: Standby / Parkir",
                StoppageReason.ApiGpsStaleStatic => "🔵 Operasional: Standby Staging Bay",
                StoppageReason.ApiFleetFeedStale => "Data GPS pusat tertunda; posisi terakhir ditampilkan",
                StoppageReason.ApiGpsNoFixNull => "🟣 Telemetri API: Menunggu Sinyal GPS",
                StoppageReason.ApiGpsDeviceOffline => $"🔴 Telemetri API: Device Offline ({backendLastHeardSeconds}s)",
                _ => "🟢 Operasional Normal"
            };
        }

        // =========================================================================
        // MOUSE INTERACTION & SELECTION
        // =========================================================================
        private void OnMouseDown()
        {
            if (FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsPointerOverUI()) return;

            if (FMSFleetManager.Instance != null)
            {
                FMSFleetManager.Instance.SelectUnit(this);
            }
        }

        private void OnMouseOver()
        {
            if (FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsPointerOverUI()) return;

            if (Input.GetMouseButtonDown(1)) // Right-Click Unit
            {
                if (FMSFleetManager.Instance != null)
                {
                    FMSFleetManager.Instance.OpenContextMenu(this, Input.mousePosition);
                }
            }
        }

        // =========================================================================
        // INTERACTIVE UNIT COMMANDS (Horn, Hazard Flash, Reroute)
        // =========================================================================
        public void TriggerHorn()
        {
            FMSDashboardUI.Instance?.ShowNotification($"📢 KLAKSON AKTIF: {unitId} ({unitName}) - Safety 1-2-3 Horn Alert!");
            StartCoroutine(HornPulseCoroutine());
        }

        private System.Collections.IEnumerator HornPulseCoroutine()
        {
            for (int i = 0; i < 2; i++)
            {
                suspensionBounceY += 0.20f;
                yield return new WaitForSeconds(0.12f);
                suspensionBounceY -= 0.20f;
                yield return new WaitForSeconds(0.12f);
            }
        }

        public void TriggerHeadlightFlash()
        {
            FMSDashboardUI.Instance?.ShowNotification($"💡 KEDIP LAMPU HAZARD: {unitId} ({unitName}) - Flash Test Aktif!");
            FMSUnitLighting lighting = GetComponent<FMSUnitLighting>();
            if (lighting != null)
            {
                StartCoroutine(FlashLightingCoroutine(lighting));
            }
        }

        private System.Collections.IEnumerator FlashLightingCoroutine(FMSUnitLighting lighting)
        {
            for (int i = 0; i < 4; i++)
            {
                lighting.enabled = false;
                yield return new WaitForSeconds(0.15f);
                lighting.enabled = true;
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
}

