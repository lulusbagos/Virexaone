using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    /// <summary>
    /// Smart Dispatching & Real-Time Queue Optimization Engine for Open-Pit Mines.
    /// Tracks loading shovel queues, disposal queues, cycle time breakdown, Match Factor (MF),
    /// and generates automated smart dispatch re-routing recommendations.
    /// </summary>
    public class FMSSmartDispatchManager : MonoBehaviour
    {
        public static FMSSmartDispatchManager Instance { get; private set; }

        [Header("Dispatch HUD State")]
        public bool isDispatchHudOpen = false;
        public bool showQueueOverheadBadges = true;

        [System.Serializable]
        public class FrontQueueEntry
        {
            public string frontName = "";
            public string excavatorId = "";
            public string excavatorModel = "";
            public Vector3 worldPosition;
            public FMSUnitController excavatorUnit;
            public FMSUnitController activeLoadingTruck;
            public List<FMSUnitController> queueTrucks = new List<FMSUnitController>();
            public List<FMSUnitController> incomingTrucks = new List<FMSUnitController>();
            public float matchFactor = 0f;
            public string statusLabel = "Belum tersedia";
            public Color statusColor = new Color(0f, 1f, 0.65f);
        }

        [System.Serializable]
        public class DisposalQueueEntry
        {
            public string disposalName = "";
            public Vector3 worldPosition;
            public FMSUnitController activeDumpingTruck;
            public List<FMSUnitController> queueTrucks = new List<FMSUnitController>();
        }

        [Header("Live Operational Queues")]
        public List<FrontQueueEntry> activeFrontQueues = new List<FrontQueueEntry>();
        public List<DisposalQueueEntry> activeDisposalQueues = new List<DisposalQueueEntry>();

        [Header("Smart Dispatch Recommendation")]
        public string latestRecommendation = "Data GPS shovel belum tersedia.";
        public bool hasActiveRecommendation = false;
        public string recommendedTruckId = "";
        public string sourceFrontName = "";
        public string targetFrontName = "";
        public float estimatedGainPercent = 0f;

        private float refreshTimer = 0f;
        private Vector2 hudScrollPos = Vector2.zero;

        // Custom Styles
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle valueStyle;
        private GUIStyle btnActiveStyle;
        private GUIStyle btnNormalStyle;
        private GUIStyle queueBadgeStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            activeFrontQueues.Clear();
            activeDisposalQueues.Clear();
        }

        private void Update()
        {
            // Toggle Dispatch HUD shortcut [Q]
            if (GUIUtility.keyboardControl == 0 && (FMSDashboardUI.Instance == null || !FMSDashboardUI.Instance.HasBlockingModal))
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    ToggleDispatchHUD();
                }
            }

            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 1.0f)
            {
                refreshTimer = 0f;
                UpdateLiveQueueCalculations();
                EvaluateDispatchOptimization();
            }
        }

        public void ToggleDispatchHUD()
        {
            isDispatchHudOpen = !isDispatchHudOpen;
            if (FMSDashboardUI.Instance != null)
            {
                FMSDashboardUI.Instance.ShowNotification($"📊 Panel Smart Dispatch & Antrean: {(isDispatchHudOpen ? "Dibuka" : "Ditutup")}");
            }
        }

        public void UpdateLiveQueueCalculations()
        {
            if (FMSFleetManager.Instance == null || FMSFleetManager.Instance.activeFleet == null) return;

            var fleet = FMSFleetManager.Instance.activeFleet;
            activeFrontQueues.Clear();
            if (FMSFleetManager.Instance.isTelemetryFeedStale) return;
            foreach (var shovel in fleet)
            {
                if (shovel == null || shovel.unitType != UnitType.Excavator ||
                    !shovel.isLiveTelemetryControlled || !shovel.hasValidGpsFix) continue;
                activeFrontQueues.Add(new FrontQueueEntry
                {
                    frontName = shovel.unitId,
                    excavatorId = shovel.unitId,
                    excavatorModel = shovel.modelName,
                    worldPosition = shovel.transform.position,
                    excavatorUnit = shovel
                });
            }

            // Link Excavators
            foreach (var front in activeFrontQueues)
            {
                if (front.excavatorUnit == null)
                {
                    front.excavatorUnit = fleet.Find(u => 
                        u.unitType == UnitType.Excavator && 
                        (u.unitId.Equals(front.excavatorId, StringComparison.OrdinalIgnoreCase) || 
                         u.unitName.Equals(front.excavatorId, StringComparison.OrdinalIgnoreCase) ||
                         Vector3.Distance(u.transform.position, front.worldPosition) < 250f));
                }

                if (front.excavatorUnit != null)
                {
                    front.worldPosition = front.excavatorUnit.transform.position;
                }

                front.activeLoadingTruck = null;
                front.queueTrucks.Clear();
                front.incomingTrucks.Clear();

                foreach (var unit in fleet)
                {
                    if (unit == null || unit.unitType != UnitType.HaulTruck || !unit.isOnline ||
                        !unit.hasActualHaulData || !string.Equals(unit.assignedLoaderId, front.excavatorId, StringComparison.OrdinalIgnoreCase)) continue;

                    float dist = Vector3.Distance(unit.transform.position, front.worldPosition);

                    // 1. In Loading Bay (< 22m)
                    if (dist < 22f && (unit.currentState == UnitState.Loading || unit.currentSpeedKmh < 3.0f))
                    {
                        if (front.activeLoadingTruck == null) front.activeLoadingTruck = unit;
                        else front.queueTrucks.Add(unit);
                    }
                    // 2. Waiting in Queue (22m - 85m)
                    else if (dist >= 22f && dist <= 85f && unit.currentState != UnitState.Hauling)
                    {
                        front.queueTrucks.Add(unit);
                    }
                    // 3. In-Transit Approaching Front (85m - 600m)
                    else if (dist > 85f && dist < 600f && unit.currentState == UnitState.TravellingToLoad)
                    {
                        front.incomingTrucks.Add(unit);
                    }
                }

                front.matchFactor = 0f;
                if (front.queueTrucks.Count >= 3)
                {
                    front.statusLabel = "🔴 Indikasi antrean padat";
                    front.statusColor = new Color(1f, 0.35f, 0.35f);
                }
                else if (front.activeLoadingTruck == null && front.queueTrucks.Count == 0)
                {
                    front.statusLabel = "🟡 Belum ada truk terdeteksi";
                    front.statusColor = new Color(1f, 0.75f, 0.1f);
                }
                else
                {
                    front.statusLabel = "🟢 Aktivitas terdeteksi";
                    front.statusColor = new Color(0f, 1f, 0.65f);
                }
            }

            // Update Disposal Queues
            foreach (var disp in activeDisposalQueues)
            {
                disp.activeDumpingTruck = null;
                disp.queueTrucks.Clear();

                foreach (var unit in fleet)
                {
                    if (unit == null || unit.unitType != UnitType.HaulTruck || !unit.isOnline) continue;

                    float dist = Vector3.Distance(unit.transform.position, disp.worldPosition);
                    if (dist < 25f)
                    {
                        if (disp.activeDumpingTruck == null) disp.activeDumpingTruck = unit;
                        else disp.queueTrucks.Add(unit);
                    }
                    else if (dist >= 25f && dist <= 90f && unit.currentState == UnitState.QueueingAtDump)
                    {
                        disp.queueTrucks.Add(unit);
                    }
                }
            }
        }

        public void EvaluateDispatchOptimization()
        {
            hasActiveRecommendation = false;
            estimatedGainPercent = 0f;
            latestRecommendation = activeFrontQueues.Count == 0
                ? "Data GPS shovel belum tersedia. Indikasi antrean tidak dapat dihitung."
                : "Antrean adalah indikasi dari GPS dan pasangan shovel pada tabel. Verifikasi lapangan sebelum mengubah dispatch.";
        }

        public void ExecuteSmartDispatchReassignment()
        {
            FMSDashboardUI.Instance?.ShowNotification("Dispatch otomatis belum tersedia tanpa data siklus tervalidasi.");
        }

        // =========================================================================
        // GUI DRAWING - DISPATCH & QUEUE HUD MODAL
        // =========================================================================
        public void DrawDispatchHUD(float screenW, float screenH, GUIStyle baseCardStyle)
        {
            if (!isDispatchHudOpen) return;

            InitStyles(baseCardStyle);

            float modalW = Mathf.Min(840f, screenW - 40f);
            float modalH = Mathf.Min(600f, screenH - 70f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f + 16f;

            // Background Window
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, panelStyle);

            // Top Specular Accent
            GUI.Box(new Rect(x, y, modalW, 3), GUIContent.none, btnActiveStyle);

            // Header Title
            GUI.Label(new Rect(x + 20, y + 14, modalW - 60, 24), "⏱️ OPTIMASI ANTREAN & SMART DISPATCHING (DIGITAL TWIN FMS)", titleStyle);

            if (GUI.Button(new Rect(x + modalW - 36, y + 12, 24, 22), "✕", btnNormalStyle))
            {
                isDispatchHudOpen = false;
            }

            GUI.Label(new Rect(x + 20, y + 36, modalW - 40, 18), 
                "Pemantauan antrean alat muat (Shovel/Excavator), Match Factor (MF), breakdown waktu siklus hauling, dan rekomendasi dispatching real-time.", bodyStyle);

            float curY = y + 62;

            // --- 1. SMART DISPATCH AI RECOMMENDATION BANNER ---
            float bannerW = modalW - 40;
            float bannerH = hasActiveRecommendation ? 72 : 46;
            Rect bannerRect = new Rect(x + 20, curY, bannerW, bannerH);
            GUI.Box(bannerRect, GUIContent.none, panelStyle);

            GUI.Label(new Rect(bannerRect.x + 12, bannerRect.y + 8, bannerW - (hasActiveRecommendation ? 180 : 24), bannerH - 16), latestRecommendation, bodyStyle);

            if (hasActiveRecommendation)
            {
                if (GUI.Button(new Rect(bannerRect.x + bannerW - 170, bannerRect.y + 16, 155, 38), "⚡ <b>TERAPKAN DISPATCH</b>", btnActiveStyle))
                {
                    ExecuteSmartDispatchReassignment();
                }
            }
            curY += bannerH + 12;

            // --- 2. CYCLE TIME WATERFALL BREAKDOWN (MINUTES) ---
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "DURASI TAHAP SIKLUS · BELUM ADA TRANSAKSI WAKTU", headerStyle);
            curY += 22;

            float ctBoxW = modalW - 40;
            float ctBoxH = 68;
            GUI.Box(new Rect(x + 20, curY, ctBoxW, ctBoxH), GUIContent.none, panelStyle);

            float colW = ctBoxW / 5f;
            DrawCycleTimeTile(x + 20 + 0 * colW, curY, colW, "1. Spotting & Antre", "--", "Belum tersedia");
            DrawCycleTimeTile(x + 20 + 1 * colW, curY, colW, "2. Pemuatan", "--", "Belum tersedia");
            DrawCycleTimeTile(x + 20 + 2 * colW, curY, colW, "3. Angkut", "--", "Belum tersedia");
            DrawCycleTimeTile(x + 20 + 3 * colW, curY, colW, "4. Dumping", "--", "Belum tersedia");
            DrawCycleTimeTile(x + 20 + 4 * colW, curY, colW, "5. Kembali", "--", "Belum tersedia");

            curY += ctBoxH + 14;

            // --- 3. LIVE LOADING PIT & SHOVEL QUEUE MATRIX ---
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "⛏️ INDIKASI ANTREAN FRONT DARI GPS:", headerStyle);
            curY += 22;

            float listH = modalH - (curY - y) - 52f;
            Rect scrollArea = new Rect(x + 20, curY, modalW - 40, listH);
            Rect viewRect = new Rect(0, 0, modalW - 65, activeFrontQueues.Count * 82f);

            hudScrollPos = GUI.BeginScrollView(scrollArea, hudScrollPos, viewRect);

            for (int i = 0; i < activeFrontQueues.Count; i++)
            {
                var f = activeFrontQueues[i];
                float rowY = i * 82f;
                Rect rowRect = new Rect(0, rowY, modalW - 65, 76f);
                GUI.Box(rowRect, GUIContent.none, panelStyle);

                // Column 1: Front Info & Excavator
                GUI.Label(new Rect(rowRect.x + 12, rowRect.y + 8, 260, 20), $"⛏️ <b>{f.frontName}</b>", valueStyle);
                GUI.Label(new Rect(rowRect.x + 12, rowRect.y + 28, 260, 18), $"Unit Shovel: <color=#00E5FF>{f.excavatorId}</color> ({f.excavatorModel})", bodyStyle);
                GUI.Label(new Rect(rowRect.x + 12, rowRect.y + 48, 260, 18), $"Status: {f.statusLabel}", bodyStyle);

                // Column 2: Live Queue Breakdown
                string activeTruck = f.activeLoadingTruck != null ? $"{f.activeLoadingTruck.unitId} ({f.activeLoadingTruck.unitName})" : "<color=#888888>Kosong (Menunggu DT)</color>";
                GUI.Label(new Rect(rowRect.x + 270, rowRect.y + 10, 240, 18), $"• Sedang Muat: <b>{activeTruck}</b>", bodyStyle);
                GUI.Label(new Rect(rowRect.x + 270, rowRect.y + 30, 240, 18), $"• Antre Menunggu: <color=#FFB800><b>{f.queueTrucks.Count} Unit DT</b></color>", bodyStyle);
                GUI.Label(new Rect(rowRect.x + 270, rowRect.y + 50, 240, 18), $"• Menuju Front: <color=#00FFA3>{f.incomingTrucks.Count} Unit DT</color>", bodyStyle);

                // Column 3: Match Factor & Gauge
                GUI.Label(new Rect(rowRect.x + 515, rowRect.y + 10, 140, 18), "MF: <b>--</b>", valueStyle);
                GUI.Label(new Rect(rowRect.x + 515, rowRect.y + 32, 140, 18), "Perlu durasi aktual", bodyStyle);

                // Focus Camera Button
                if (GUI.Button(new Rect(rowRect.x + rowRect.width - 70, rowRect.y + 22, 60, 32), "🎯 Fokus", btnNormalStyle))
                {
                    FMSCameraController.Instance?.JumpTo(f.worldPosition, 140f);
                    if (f.excavatorUnit != null) FMSFleetManager.Instance?.SelectUnit(f.excavatorUnit);
                    isDispatchHudOpen = false;
                }
            }

            GUI.EndScrollView();

            // Footer
            float footY = y + modalH - 38;
            GUI.Label(new Rect(x + 20, footY + 4, 400, 22),
                "Sumber: GPS unit & pasangan shovel dari tabel hauling", bodyStyle);

            if (GUI.Button(new Rect(x + modalW - 130, footY, 110, 28), "Tutup [Q]", btnNormalStyle))
            {
                isDispatchHudOpen = false;
            }
        }

        private void DrawCycleTimeTile(float x, float y, float w, string title, string duration, string subtext)
        {
            GUI.Label(new Rect(x + 6, y + 8, w - 12, 16), title, headerStyle);
            GUI.Label(new Rect(x + 6, y + 26, w - 12, 22), $"<size=15><b>{duration}</b></size>", valueStyle);
            GUI.Label(new Rect(x + 6, y + 46, w - 12, 16), subtext, bodyStyle);
        }

        // =========================================================================
        // 3D FLOATING OVERHEAD QUEUE BADGE (Rendered above Excavators)
        // =========================================================================
        public void DrawFloatingQueueBadges(float screenW, float screenH)
        {
            if (!showQueueOverheadBadges || Camera.main == null) return;

            Camera cam = Camera.main;
            Vector3 camPos = cam.transform.position;
            const float MAX_DIST = 1800f;
            const float SAFE_Y = 56f;

            foreach (var front in activeFrontQueues)
            {
                // Positioned 13.5m above ground to sit neatly above excavator & hauler overhead tags
                Vector3 markerPos = front.worldPosition + Vector3.up * 13.5f;
                float dist = Vector3.Distance(camPos, markerPos);
                if (dist > MAX_DIST || dist < 4f) continue;

                Vector3 sp = cam.WorldToScreenPoint(markerPos);
                if (sp.z <= 0.5f) continue;

                float screenX = sp.x;
                float screenY = screenH - sp.y;

                float distScale = Mathf.Clamp(1.0f - (dist / MAX_DIST) * 0.45f, 0.70f, 1.0f);
                float badgeW = 205f * distScale;
                float badgeH = 26f * distScale;

                float tagY = screenY - badgeH - 10f;
                if (tagY < SAFE_Y || screenX < 50f || screenX > screenW - 50f) continue;

                Rect badgeRect = new Rect(screenX - (badgeW / 2f), tagY, badgeW, badgeH);

                int qCount = front.queueTrucks.Count;
                string queueStatusText = qCount == 0 
                    ? "<color=#00FFA3>● 0 Antre</color>" 
                    : (qCount >= 3 ? $"<color=#FF4D4D>● {qCount} ANTRE (PADAT)</color>" : $"<color=#FFB800>● {qCount} Antre</color>");

                string badgeLabel = $"<b>⛏️ {front.excavatorId}</b> | {queueStatusText}";

                GUIStyle bStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(10 * distScale),
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    normal = { textColor = Color.white, background = Texture2D.blackTexture }
                };

                if (GUI.Button(badgeRect, badgeLabel, bStyle))
                {
                    if (front.excavatorUnit != null)
                    {
                        FMSFleetManager.Instance?.SelectUnit(front.excavatorUnit);
                    }
                    isDispatchHudOpen = true;
                }
            }
        }

        private void InitStyles(GUIStyle baseCardStyle)
        {
            if (panelStyle != null) return;

            panelStyle = new GUIStyle(baseCardStyle ?? GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            Font poppins = FMSDashboardUI.GetPoppinsFont();

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = poppins,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0f, 0.95f, 1f) }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = poppins,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = poppins,
                fontSize = 11,
                richText = true,
                normal = { textColor = new Color(0.80f, 0.85f, 0.90f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                font = poppins,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = Color.white }
            };

            btnActiveStyle = new GUIStyle(GUI.skin.button)
            {
                font = poppins,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };

            btnNormalStyle = new GUIStyle(GUI.skin.button)
            {
                font = poppins,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }
    }
}
