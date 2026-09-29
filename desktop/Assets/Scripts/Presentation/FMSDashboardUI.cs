using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSDashboardUI : MonoBehaviour
    {
        public enum UITheme
        {
            CyberCyan,
            MiningGold,
            DeepOcean,
            TacticalEmerald
        }

        [Header("Futuristic Theme Settings")]
        public UITheme currentTheme = UITheme.CyberCyan;

        public static FMSDashboardUI Instance { get; private set; }

        // --- GUI STYLES ---
        private GUIStyle topNavBgStyle;
        private GUIStyle brandLogoStyle;
        private GUIStyle navBtnStyle;
        private GUIStyle navBtnActiveStyle;
        private GUIStyle dropdownPanelStyle;
        private GUIStyle dropdownItemStyle;
        private GUIStyle dropdownHeaderStyle;
        private GUIStyle cardStyle;
        private GUIStyle coordStyle;
        private GUIStyle hintStyle;
        private GUIStyle compassLabelStyle;
        private GUIStyle compassHeadingStyle;
        private GUIStyle northTextStyle;
        private GUIStyle badgeStyle;
        private GUIStyle badgeSuccessStyle;
        private GUIStyle badgeOfflineStyle;
        private GUIStyle modalBoxStyle;
        private GUIStyle searchBoxStyle;
        private GUIStyle tabBtnStyle;
        private GUIStyle tabBtnActiveStyle;

        // --- TEXTURES ---
        private Texture2D navDarkBgTex;
        private Texture2D panelBgTex;
        private Texture2D dropdownBgTex;
        private Texture2D btnHoverTex;
        private Texture2D btnActiveTex;
        private Texture2D badgeSuccessBgTex;
        private Texture2D badgeOfflineBgTex;
        private Texture2D compassDialTex;
        private Texture2D compassNeedleTex;
        private Texture2D lineAccentTex;
        [SerializeField] private Texture2D bootSiteOrtho;
        private bool autoLoadedSitePreview;
        private bool autoLoadedCompanyLogo;
        private Texture2D decodedCompanyLogo;
        private Texture2D bootPrimaryButtonTex;
        private Texture2D bootPrimaryButtonHoverTex;
        private Texture2D bootSecondaryButtonTex;

        // --- STATE & MENUS ---
        private enum ActiveMenu { None, File, Edit, View, Aset, Windows, Settings, Help }
        private ActiveMenu currentMenu = ActiveMenu.None;
        private FMSMtcPanel mtcPanel;
        private FMSModulePanel fmsModulePanel;
        private FMSMapEditorPanel fmsMapEditorPanel;
        private float fileBtnX = 217f;
        private float editBtnX = 276f;
        private float viewBtnX = 335f;
        private float asetBtnX = 394f;
        private float winBtnX = 453f;
        private float settingsBtnX = 542f;
        private float helpBtnX = 672f;

        // View Toggles
        public bool showCompass = true;
        public bool showCoordHud = true;
        public bool showNavGuide = true;
        public bool showTelemetry = true;
        public bool showScaleBar = true;
        public bool showQuickDock = true;
        public bool showWeatherWidget = true;
        public bool showKpiMiniBar = true;
        private static readonly string[] WeatherConditions = { "Cerah", "Berawan", "Hujan" };
        private static readonly string[] RoadConditions = { "Aman", "Waspada", "Tutup" };
        private bool showWeatherReportModal;
        private bool showWeatherDetailsModal;
        private int weatherConditionIndex;
        private int roadConditionIndex;
        private string weatherArea = "Pit Unggul";
        private string weatherReporter = "";
        private string weatherNote = "";
        private long weatherReportedAtTicks;
        private int draftWeatherCondition;
        private int draftRoadCondition;
        private string draftWeatherArea = "";
        private string draftWeatherReporter = "";
        private string draftWeatherNote = "";
        private Vector2 weatherReportScroll;
        public bool showUnitOverheadTags = true;
        public bool showCommandPalette = false;
        private bool focusCommandInputNextFrame = false;
        private string commandPaletteQuery = "";
        private Vector2 commandPaletteScroll = Vector2.zero;
        private int sunCycleIndex = 0;

        // --- UNIT OVERHEAD TAG / TOOLTIP CUSTOMIZATION SETTINGS ---
        public enum UnitTagPreset
        {
            Custom,
            NameOnly,        // Ringkas Nama Saja (e.g. 🚚 RD05)
            Standard,        // Standar (e.g. 🚚 RD05 | ● 28k)
            PayloadStatus,   // Muatan & Status (e.g. 🚚 RD05 | 95.0 T | HAULING)
            FullTelemetry    // Lengkap (e.g. 🚚 RD05 | 28k | 95.0 T | Ahmad S. | ⛽82%)
        }

        [Header("Unit 3D Overhead Tag / Tooltip Settings")]
        public UnitTagPreset currentTagPreset = UnitTagPreset.Standard;
        public bool showUnitTagSettingsModal = false;

        // Granular Field Toggles
        public bool tagShowIcon = true;
        public bool tagShowName = true;
        public bool tagShowSpeed = true;
        public bool tagShowStatus = true;
        public bool tagShowPayload = false;
        public bool tagShowOperator = false;
        public bool tagShowFuel = false;
        public bool tagShowActivity = false;
        public bool tagShowFtwStatus = true;   // 🟢 Fit / 🟡 Dalam Pengawasan / 🔴 Unfit
        public bool tagShowSleepHours = true;  // 💤 Total Jam Tidur Operator

        // Styling & Visibility
        public bool tagShowStemLine = true;
        public float tagScaleMultiplier = 1.0f; // 0.70x - 1.40x
        public float tagMaxViewDistance = 1600f; // 300m - 3000m

        // Custom High-Tech Styles
        private GUIStyle scaleLabelStyle;
        private GUIStyle scaleSubStyle;
        private GUIStyle quickDockBtnStyle;
        private GUIStyle quickDockBtnActiveStyle;
        private GUIStyle reticleStyle;

        // Boot Splash & Alert Styles
        private GUIStyle splashTitleStyle;
        private GUIStyle splashSubtitleStyle;
        private GUIStyle splashPhaseStyle;
        private GUIStyle splashCheckStyle;
        private GUIStyle alertHeaderStyle;
        private GUIStyle alertBodyStyle;

        // Context Menu Styles
        private GUIStyle contextHeaderStyle;
        private GUIStyle contextSubHeaderStyle;
        private GUIStyle contextItemStyle;
        private GUIStyle contextItemActiveStyle;

        [Header("Typography (Poppins Font System)")]
        public Font customPoppinsFont;
        private static Font defaultPoppinsFont;

        public static Font GetPoppinsFont()
        {
            if (defaultPoppinsFont == null)
            {
                defaultPoppinsFont = Resources.Load<Font>("Fonts/Poppins") 
                    ?? Resources.Load<Font>("Poppins");
            }
            return defaultPoppinsFont;
        }

        // Modals / Dialogs
        private bool showAboutModal = false;
        private bool showGuideModal = false;
        private bool showLocationFilterModal = false;
        private bool showUnitAssetModal = false;
        private FMSUnitAssetManager.UnitCategory selectedUnitTab = FMSUnitAssetManager.UnitCategory.HaulerEmpty;
        private Vector2 unitModalScrollPos = Vector2.zero;

        // Unit Production & Fleet Matrix Modals
        public bool showUnitProductionModal = false;
        public FMSUnitController modalSelectedUnit;
        public bool showFleetMatrixModal = false;
        public string matrixLoaderId = "";
        private Vector2 matrixScrollPos = Vector2.zero;
        private Rect lastContextMenuRect = Rect.zero;

        // Two-Way Radio Dispatch Messenger Modal (Control Room <-> Cabin)
        public bool showDispatchRadioModal = false;
        public string dispatchSelectedUnitTarget = "ALL";
        public string dispatchOutgoingMessage = "";
        private Vector2 dispatchChatScrollPos = Vector2.zero;
        private Vector2 dispatchTargetScrollPos = Vector2.zero;

        // Location Filter Modal State
        private string locationSearchQuery = "";
        private string selectedCategoryTab = "Semua";
        private Vector2 locationScrollPos = Vector2.zero;

        private string notificationMessage = "";
        private float notificationTimer = 0f;

        // Live Coordinate Info
        private string cursorUtmInfo = "Arahkan kursor ke peta";
        private double lastEasting;
        private double lastNorthing;
        private double lastElevation;
        private bool hasCursorCoordinate;

        // Telemetry & API Status
        private float fps = 60f;
        private float fpsTimer = 0f;

        [Header("Backend API Connection")]
        public string apiBaseUrl = "http://127.0.0.1:8000";
        public bool isApiConnected = false;
        public int apiOnlineUnits = 0;
        public float apiLatencyMs = 0f;
        public string apiConnectionMessage = "Menunggu backend lokal";

        [Header("Company Identity")]
        public Texture2D companyLogo;
        public string companyDisplayName = "ASTHA VIREXA TECHNOLOGY";
        public string siteId = "astha";
        private string loadedSiteProfileUrl;
        private bool bootLogoLoadAttempted;
        private bool showFleetInventoryModal;
        private bool inventoryLoading;
        private string inventoryError;
        private int inventoryCategoryIndex;
        private Vector2 inventoryScroll;
        private List<InventoryUnitDto> inventoryUnits = new List<InventoryUnitDto>();

        [Serializable]
        private class InventoryUnitDto
        {
            public long unit_id;
            public string unit_name;
            public string unit_type;
            public string category;
            public string last_heard;
            public string gps_updated_at;
        }

        [Serializable]
        private class InventoryResponseDto
        {
            public string status;
            public List<InventoryUnitDto> data;
        }

        [Serializable]
        private class SiteProfileDto
        {
            public string site_id;
            public string display_name;
        }

        [Header("Startup Boot Splash Animation (Virexa One)")]
        public bool showBootSplash = true;
        public bool isBooting = true;
        private float bootTimer = 0f;
        private const float BOOT_DURATION = 3.8f;
        private float bootProgress = 0f;

        public enum BootApiProbeState { Probing, Connected, FailedTimeout, OfflineBypassed }
        [Header("Startup Subsystem & API Gating")]
        public BootApiProbeState bootApiState = BootApiProbeState.Probing;
        public float bootApiProbeTimeout = 6.0f;

        [Header("API Disconnected Notice State")]
        public bool dismissApiWarning = false;

        [Header("Real Live Production & Fleet Telemetry (PostgreSQL)")]
        public double realCurrentPayloadTons = 0.0;
        public int realRecordedLoads = 0;
        private string realProductionShiftLabel = "";
        public int realTotalUnits = 0;
        public int realExcavatorsTotal = 0;
        public int realExcavatorsOperating = 0;
        public int realHaulersTotal = 0;
        public int realHaulersOperating = 0;
        public int realHaulingCount = 0;
        public int realLoadingCount = 0;
        public int realDumpingCount = 0;
        public int realIdleCount = 0;
        public float realFleetPa = 0.0f;
        public float realMatchFactor = 0.0f;
        public bool hasRealProductionData = false;
        public bool hasRealFleetSummaryData = false;

        [System.Serializable]
        public class ProductionApiResponse
        {
            public string status;
            public ProductionApiData data;
        }

        [System.Serializable]
        public class ProductionApiData
        {
            public string query_time;
            public string metric_source;
            public bool data_available;
            public double total_tonnage_ton;
            public bool tonnage_available;
            public double current_payload_ton;
            public int total_trips;
            public bool completed_trips_available;
            public int recorded_loads;
            public string shift_start;
            public double avg_haul_distance_km;
            public double avg_cycle_time_minutes;
            public string latest_update;
        }

        [System.Serializable]
        public class FleetSummaryApiResponse
        {
            public string status;
            public FleetSummaryApiData data;
        }

        [System.Serializable]
        public class FleetSummaryApiData
        {
            public string timestamp;
            public int total_units;
            public int active_online;
            public int excavators_total;
            public int excavators_operating;
            public int haulers_total;
            public int haulers_operating;
            public int support_total;
            public FleetStatusBreakdown status_breakdown;
        }

        [System.Serializable]
        public class FleetStatusBreakdown
        {
            public int hauling;
            public int loading;
            public int dumping;
            public int standby_idle;
        }

        [Header("Spatial Tool Settings")]
        public bool showMeasureToolbar = false;
        private UnityEngine.Networking.UnityWebRequest activeApiRequest;

        private void AbortActiveApiRequest()
        {
            if (activeApiRequest == null) return;
            activeApiRequest.Abort();
            activeApiRequest = null;
        }

        private void OnDisable()
        {
            AbortActiveApiRequest();
        }

        private void Awake()
        {
            if (Instance == null) { Instance = this; Application.runInBackground = true; }
            else Destroy(gameObject);
        }

        private void Start()
        {
            int savedTheme = PlayerPrefs.GetInt("Virexa_UITheme", 0);
            currentTheme = (UITheme)savedTheme;
            InitTextures();
            LoadTagSettings();
            LoadWeatherReport();
            StartCoroutine(CheckApiHealthLoop());
            EnsureMining3DLayer();
            EnsureUnitAssetManager();
            EnsureMeasureTool();
            EnsureDigitalTwinAtmosphere();
            SetSunLighting(PlayerPrefs.GetInt("Virexa_TimeOfDay", 0));
            EnsureElevationProfiler();
            EnsureMiningDigitalTwinFX();
            EnsureSlopeStabilityHeatmap();
            EnsureFleetManager();
            EnsureSmartDispatchManager();
            EnsureUnitCctvManager();
            EnsureFtwSaveraManager();
            if (FMSWeatherController.Instance == null) gameObject.AddComponent<FMSWeatherController>();
            FMSFleetMessenger.OnNotificationRequested = (msg) => ShowNotification(msg);
        }

        public void EnsureFtwSaveraManager()
        {
            if (FMSFtwSaveraManager.Instance == null)
            {
                var existing = FindFirstObjectByType<FMSFtwSaveraManager>();
                if (existing == null)
                {
                    var go = new GameObject("--- FMS_FTW_SAVERA_MANAGER ---");
                    go.AddComponent<FMSFtwSaveraManager>();
                }
            }
        }

        public void EnsureSmartDispatchManager()
        {
            if (FMSSmartDispatchManager.Instance == null)
            {
                GameObject dispatchObj = new GameObject("--- FMS_SMART_DISPATCH_MANAGER ---");
                dispatchObj.AddComponent<FMSSmartDispatchManager>();
            }
        }

        private FMSMtcPanel EnsureMtcPanel()
        {
            if (mtcPanel == null)
                mtcPanel = GetComponent<FMSMtcPanel>() ?? gameObject.AddComponent<FMSMtcPanel>();
            return mtcPanel;
        }

        private FMSModulePanel EnsureFmsModulePanel()
        {
            if (fmsModulePanel == null)
                fmsModulePanel = GetComponent<FMSModulePanel>() ?? gameObject.AddComponent<FMSModulePanel>();
            fmsModulePanel.OpenMtc = () => EnsureMtcPanel().Open();
            fmsModulePanel.OpenMapEditor = () => EnsureMapEditorPanel().Open();
            return fmsModulePanel;
        }

        private FMSMapEditorPanel EnsureMapEditorPanel()
        {
            if (fmsMapEditorPanel == null)
                fmsMapEditorPanel = GetComponent<FMSMapEditorPanel>() ?? gameObject.AddComponent<FMSMapEditorPanel>();
            return fmsMapEditorPanel;
        }

        public void EnsureFleetManager()
        {
            if (FMSFleetManager.Instance == null)
            {
                GameObject fleetObj = new GameObject("--- FMS_FLEET_MANAGER ---");
                fleetObj.AddComponent<FMSFleetManager>();
            }
        }

        public void EnsureMiningDigitalTwinFX()
        {
            if (FMSMiningDigitalTwinFX.Instance == null)
            {
                GameObject fxObj = new GameObject("--- FMS_DIGITAL_TWIN_FX ---");
                fxObj.AddComponent<FMSMiningDigitalTwinFX>();
            }
        }

        public void EnsureSlopeStabilityHeatmap()
        {
            if (FMSSlopeStabilityHeatmap.Instance == null)
            {
                GameObject hmObj = new GameObject("--- FMS_SLOPE_STABILITY_HEATMAP ---");
                hmObj.AddComponent<FMSSlopeStabilityHeatmap>();
            }
        }

        public void EnsureMeasureTool()
        {
            if (FMSMeasureTool.Instance == null)
            {
                GameObject toolObj = new GameObject("--- FMS_3D_MEASURE_TOOL ---");
                toolObj.AddComponent<FMSMeasureTool>();
            }
        }

        public void EnsureDigitalTwinAtmosphere()
        {
            if (FMSDigitalTwinAtmosphere.Instance == null)
            {
                GameObject atmObj = new GameObject("--- FMS_DIGITAL_TWIN_ATMOSPHERE ---");
                atmObj.AddComponent<FMSDigitalTwinAtmosphere>();
            }
        }

        public void EnsureElevationProfiler()
        {
            if (FMSElevationProfiler.Instance == null)
            {
                GameObject profObj = new GameObject("--- FMS_ELEVATION_PROFILER ---");
                profObj.AddComponent<FMSElevationProfiler>();
            }
        }

        private Texture2D dropShadowTex;
        private Texture2D topHighlightTex;
        private Texture2D rowActiveTex;
        private Texture2D rowInactiveTex;
        private Texture2D rowRoadActiveTex;
        private Texture2D splashCardBgTex;
        private Texture2D splashSubCardBgTex;
        private Texture2D splashGlowCyanTex;
        private Texture2D splashGlowEmeraldTex;
        private Texture2D splashAlertBgTex;
        private Texture2D splashAlertBorderTex;
        private Texture2D tagBgNormalTex;
        private Texture2D tagBgSelectedTex;
        private Texture2D tagBgOfflineTex;
        private Texture2D tagStemTex;
        private Texture2D tagBorderSelectedTex;

        public void SetTheme(UITheme theme)
        {
            currentTheme = theme;
            PlayerPrefs.SetInt("Virexa_UITheme", (int)theme);
            PlayerPrefs.Save();

            // Force recreate textures and styles
            DestroyAllProceduralTextures();
            int savedTheme = PlayerPrefs.GetInt("Virexa_UITheme", 0);
            currentTheme = (UITheme)savedTheme;
            InitTextures();
            InitStyles(true);

            string themeName = theme switch
            {
                UITheme.MiningGold => "Kontras hangat",
                UITheme.DeepOcean => "Biru",
                UITheme.TacticalEmerald => "Hijau",
                _ => "Standar"
            };
            ShowNotification($"Tema antarmuka: {themeName}");
        }

        private void InitTextures()
        {
            Color topNav1 = new Color(0.06f, 0.09f, 0.14f, 0.97f);
            Color topNav2 = new Color(0.03f, 0.04f, 0.07f, 0.98f);
            Color panelCol = new Color(0.07f, 0.10f, 0.15f, 0.92f);
            Color dropCol = new Color(0.06f, 0.08f, 0.13f, 0.98f);
            Color accentCol = new Color(0.0f, 0.90f, 1.0f, 0.85f);
            Color hoverCol = new Color(0.0f, 0.85f, 1.0f, 0.18f);
            Color activeCol = new Color(0.0f, 0.85f, 1.0f, 0.38f);
            Color rowActCol = new Color(0.08f, 0.14f, 0.22f, 0.85f);

            if (currentTheme == UITheme.MiningGold)
            {
                topNav1 = new Color(0.08f, 0.07f, 0.06f, 0.97f);
                topNav2 = new Color(0.04f, 0.04f, 0.04f, 0.98f);
                panelCol = new Color(0.06f, 0.07f, 0.09f, 0.95f);
                dropCol = new Color(0.05f, 0.06f, 0.07f, 0.98f);
                accentCol = new Color(1.0f, 0.78f, 0.10f, 0.92f);
                hoverCol = new Color(1.0f, 0.78f, 0.10f, 0.20f);
                activeCol = new Color(1.0f, 0.78f, 0.10f, 0.40f);
                rowActCol = new Color(0.14f, 0.12f, 0.06f, 0.88f);
            }
            else if (currentTheme == UITheme.DeepOcean)
            {
                topNav1 = new Color(0.04f, 0.08f, 0.16f, 0.97f);
                topNav2 = new Color(0.02f, 0.04f, 0.09f, 0.98f);
                panelCol = new Color(0.05f, 0.10f, 0.18f, 0.94f);
                dropCol = new Color(0.03f, 0.07f, 0.14f, 0.98f);
                accentCol = new Color(0.18f, 0.65f, 1.0f, 0.90f);
                hoverCol = new Color(0.18f, 0.65f, 1.0f, 0.20f);
                activeCol = new Color(0.18f, 0.65f, 1.0f, 0.40f);
                rowActCol = new Color(0.06f, 0.15f, 0.26f, 0.88f);
            }
            else if (currentTheme == UITheme.TacticalEmerald)
            {
                topNav1 = new Color(0.04f, 0.11f, 0.07f, 0.97f);
                topNav2 = new Color(0.02f, 0.05f, 0.03f, 0.98f);
                panelCol = new Color(0.05f, 0.12f, 0.08f, 0.94f);
                dropCol = new Color(0.03f, 0.09f, 0.05f, 0.98f);
                accentCol = new Color(0.0f, 1.0f, 0.55f, 0.90f);
                hoverCol = new Color(0.0f, 1.0f, 0.55f, 0.20f);
                activeCol = new Color(0.0f, 1.0f, 0.55f, 0.40f);
                rowActCol = new Color(0.06f, 0.18f, 0.11f, 0.88f);
            }

            if (navDarkBgTex == null) navDarkBgTex = MakeGradientTex(44, topNav1, topNav2);
            if (panelBgTex == null) panelBgTex = MakeTex(2, 2, panelCol);
            if (dropdownBgTex == null) dropdownBgTex = MakeTex(2, 2, dropCol);
            if (btnHoverTex == null) btnHoverTex = MakeTex(2, 2, hoverCol);
            if (btnActiveTex == null) btnActiveTex = MakeTex(2, 2, activeCol);
            if (badgeSuccessBgTex == null) badgeSuccessBgTex = MakeTex(2, 2, new Color(0.02f, 0.16f, 0.10f, 0.95f));
            if (badgeOfflineBgTex == null) badgeOfflineBgTex = MakeTex(2, 2, new Color(0.18f, 0.04f, 0.05f, 0.95f));
            if (lineAccentTex == null) lineAccentTex = MakeTex(2, 2, accentCol);
            if (bootPrimaryButtonTex == null) bootPrimaryButtonTex = MakeTex(2, 2, new Color(0.32f, 0.82f, 0.70f));
            if (bootPrimaryButtonHoverTex == null) bootPrimaryButtonHoverTex = MakeTex(2, 2, new Color(0.45f, 0.91f, 0.79f));
            if (bootSecondaryButtonTex == null) bootSecondaryButtonTex = MakeTex(2, 2, new Color(0.16f, 0.25f, 0.25f));
            if (topHighlightTex == null) topHighlightTex = MakeTex(2, 2, new Color(1.0f, 1.0f, 1.0f, 0.18f));
            if (dropShadowTex == null) dropShadowTex = MakeVerticalShadowTex(8);
            if (compassDialTex == null) compassDialTex = MakeCircleTex(80, new Color(0.05f, 0.08f, 0.12f, 0.92f), accentCol);
            if (compassNeedleTex == null) compassNeedleTex = MakeNeedleTex(14, 52);
            if (rowActiveTex == null) rowActiveTex = MakeTex(2, 2, rowActCol);
            if (rowInactiveTex == null) rowInactiveTex = MakeTex(2, 2, new Color(0.05f, 0.07f, 0.10f, 0.65f));
            if (rowRoadActiveTex == null) rowRoadActiveTex = MakeTex(2, 2, rowActCol);

            // Textures shared by operational panels and alerts.
            if (splashCardBgTex == null) splashCardBgTex = MakeGradientTex(500, new Color(0.06f, 0.09f, 0.16f, 0.98f), new Color(0.03f, 0.05f, 0.10f, 0.99f));
            if (splashSubCardBgTex == null) splashSubCardBgTex = MakeGradientTex(160, new Color(0.08f, 0.13f, 0.22f, 0.92f), new Color(0.04f, 0.07f, 0.13f, 0.95f));
            if (splashGlowCyanTex == null) splashGlowCyanTex = MakeTex(2, 2, new Color(0.0f, 0.88f, 1.0f, 0.85f));
            if (splashGlowEmeraldTex == null) splashGlowEmeraldTex = MakeTex(2, 2, new Color(0.0f, 1.0f, 0.64f, 0.85f));
            if (splashAlertBgTex == null) splashAlertBgTex = MakeTex(2, 2, new Color(0.16f, 0.03f, 0.04f, 0.95f));
            if (splashAlertBorderTex == null) splashAlertBorderTex = MakeTex(2, 2, new Color(1.0f, 0.35f, 0.35f, 0.85f));

            // Unit 3D Overhead Floating Tag Textures (Aerospace Obsidian Cyber Theme)
            if (tagBgNormalTex == null) tagBgNormalTex = MakeGradientTex(26, new Color(0.05f, 0.08f, 0.15f, 0.94f), new Color(0.02f, 0.04f, 0.09f, 0.96f));
            if (tagBgSelectedTex == null) tagBgSelectedTex = MakeGradientTex(26, new Color(0.06f, 0.16f, 0.25f, 0.98f), new Color(0.02f, 0.08f, 0.15f, 0.98f));
            if (tagBgOfflineTex == null) tagBgOfflineTex = MakeGradientTex(26, new Color(0.16f, 0.04f, 0.06f, 0.94f), new Color(0.08f, 0.02f, 0.03f, 0.96f));
            if (tagStemTex == null) tagStemTex = MakeTex(2, 2, new Color(0.0f, 0.88f, 1.0f, 0.55f));
            if (tagBorderSelectedTex == null) tagBorderSelectedTex = MakeTex(2, 2, new Color(0.0f, 1.0f, 0.65f, 0.95f));
        }

        private Texture2D MakeGradientTex(int height, Color topCol, Color botCol)
        {
            Texture2D tex = new Texture2D(2, height);
            Color[] cols = new Color[2 * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                Color c = Color.Lerp(botCol, topCol, t);
                cols[y * 2] = c;
                cols[y * 2 + 1] = c;
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeVerticalShadowTex(int height)
        {
            Texture2D tex = new Texture2D(2, height);
            Color[] cols = new Color[2 * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                float alpha = Mathf.Lerp(0.55f, 0.0f, 1f - t);
                Color c = new Color(0f, 0f, 0f, alpha);
                cols[y * 2] = c;
                cols[y * 2 + 1] = c;
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private Texture2D MakeCircleTex(int size, Color bg, Color border)
        {
            Texture2D tex = new Texture2D(size, size);
            float r = size / 2f;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    if (dist > r) cols[y * size + x] = Color.clear;
                    else if (dist > r - 2f) cols[y * size + x] = border;
                    else cols[y * size + x] = bg;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeNeedleTex(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] cols = new Color[width * height];
            float midX = width / 2f;
            float midY = height / 2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x - midX);
                    if (y >= midY)
                    {
                        float maxDx = (1f - (y - midY) / (height - midY)) * (width / 2f);
                        cols[y * width + x] = dx <= maxDx ? new Color(1.0f, 0.25f, 0.25f, 0.95f) : Color.clear;
                    }
                    else
                    {
                        float maxDx = (y / midY) * (width / 2f);
                        cols[y * width + x] = dx <= maxDx ? new Color(0.85f, 0.90f, 0.95f, 0.95f) : Color.clear;
                    }
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }


        private void InitStyles(bool forceRecreate = false)
        {
            if (forceRecreate)
            {
                topNavBgStyle = null;
                brandLogoStyle = null;
                navBtnStyle = null;
                navBtnActiveStyle = null;
                dropdownPanelStyle = null;
                dropdownItemStyle = null;
                dropdownHeaderStyle = null;
                cardStyle = null;
                coordStyle = null;
                hintStyle = null;
                tabBtnStyle = null;
                tabBtnActiveStyle = null;
            }

            if (topNavBgStyle == null)
            {
                topNavBgStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = navDarkBgTex, textColor = Color.white },
                    padding = new RectOffset(10, 10, 4, 4),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }

            if (brandLogoStyle == null)
            {
                brandLogoStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            if (navBtnStyle == null)
            {
                navBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = Texture2D.blackTexture, textColor = new Color(0.80f, 0.88f, 0.95f) },
                    hover = { background = btnHoverTex, textColor = new Color(0.0f, 0.95f, 1.0f) },
                    padding = new RectOffset(8, 8, 4, 4)
                };
            }

            if (navBtnActiveStyle == null)
            {
                navBtnActiveStyle = new GUIStyle(navBtnStyle)
                {
                    normal = { background = btnActiveTex, textColor = new Color(0.0f, 0.95f, 1.0f) }
                };
            }

            if (dropdownPanelStyle == null)
            {
                dropdownPanelStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = dropdownBgTex, textColor = Color.white },
                    padding = new RectOffset(8, 8, 8, 8)
                };
            }

            if (dropdownItemStyle == null)
            {
                dropdownItemStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { background = Texture2D.blackTexture, textColor = new Color(0.85f, 0.90f, 0.95f) },
                    hover = { background = btnHoverTex, textColor = new Color(0.0f, 0.95f, 1.0f) },
                    padding = new RectOffset(10, 10, 6, 6)
                };
            }

            if (dropdownHeaderStyle == null)
            {
                dropdownHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.0f, 0.85f, 1.0f, 0.85f) },
                    padding = new RectOffset(8, 4, 4, 2)
                };
            }

            if (cardStyle == null)
            {
                cardStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = panelBgTex, textColor = Color.white },
                    padding = new RectOffset(10, 10, 8, 8),
                    fontSize = 12
                };
            }

            if (coordStyle == null)
            {
                coordStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.2f, 0.95f, 0.65f) }
                };
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.80f, 0.88f, 0.95f) }
                };
            }

            if (compassLabelStyle == null)
            {
                compassLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.8f, 0.9f, 1.0f) }
                };
            }

            if (northTextStyle == null)
            {
                northTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1.0f, 0.3f, 0.3f) }
                };
            }

            if (compassHeadingStyle == null)
            {
                compassHeadingStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
                };
            }

            if (badgeStyle == null)
            {
                badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = panelBgTex, textColor = new Color(0.0f, 0.90f, 1.0f) },
                    padding = new RectOffset(6, 6, 2, 2)
                };
            }

            if (badgeSuccessStyle == null)
            {
                badgeSuccessStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = badgeSuccessBgTex, textColor = new Color(0.2f, 1.0f, 0.6f) },
                    hover = { background = btnHoverTex, textColor = Color.white },
                    padding = new RectOffset(8, 8, 2, 2)
                };
            }

            if (badgeOfflineStyle == null)
            {
                badgeOfflineStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = badgeOfflineBgTex, textColor = new Color(1.0f, 0.4f, 0.4f) },
                    hover = { background = btnHoverTex, textColor = Color.white },
                    padding = new RectOffset(8, 8, 2, 2)
                };
            }

            if (modalBoxStyle == null)
            {
                modalBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = panelBgTex, textColor = Color.white },
                    padding = new RectOffset(16, 16, 16, 16)
                };
            }

            if (searchBoxStyle == null && GUI.skin != null && GUI.skin.textField != null)
            {
                searchBoxStyle = new GUIStyle(GUI.skin.textField);
                searchBoxStyle.fontSize = 13;
                if (searchBoxStyle.normal != null) searchBoxStyle.normal.textColor = Color.white;
                searchBoxStyle.padding = new RectOffset(8, 8, 6, 6);
            }

            if (tabBtnStyle == null)
            {
                tabBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { background = Texture2D.blackTexture, textColor = new Color(0.75f, 0.85f, 0.95f) },
                    hover = { background = btnHoverTex, textColor = Color.cyan }
                };
            }

            if (tabBtnActiveStyle == null)
            {
                tabBtnActiveStyle = new GUIStyle(tabBtnStyle)
                {
                    normal = { background = btnActiveTex, textColor = Color.cyan }
                };
            }

            if (scaleLabelStyle == null)
            {
                scaleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.9f, 0.95f, 1.0f) }
                };
            }

            if (scaleSubStyle == null)
            {
                scaleSubStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.6f, 0.7f, 0.8f) }
                };
            }

            if (quickDockBtnStyle == null)
            {
                quickDockBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = Texture2D.blackTexture, textColor = Color.white },
                    hover = { background = btnHoverTex, textColor = Color.cyan },
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            if (quickDockBtnActiveStyle == null)
            {
                quickDockBtnActiveStyle = new GUIStyle(quickDockBtnStyle)
                {
                    normal = { background = btnActiveTex, textColor = Color.cyan }
                };
            }

            if (reticleStyle == null)
            {
                reticleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            if (splashTitleStyle == null)
            {
                splashTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
                };
            }

            if (splashSubtitleStyle == null)
            {
                splashSubtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.70f, 0.85f, 1.0f) }
                };
            }

            if (splashPhaseStyle == null)
            {
                splashPhaseStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 1.0f, 0.65f) }
                };
            }

            if (splashCheckStyle == null)
            {
                splashCheckStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.85f, 0.92f, 1.0f) }
                };
            }

            if (alertHeaderStyle == null)
            {
                alertHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1.0f, 0.45f, 0.45f) }
                };
            }

            if (alertBodyStyle == null)
            {
                alertBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    wordWrap = true,
                    richText = true,
                    normal = { textColor = new Color(0.90f, 0.94f, 0.98f) }
                };
            }

            // Context Menu Dedicated Modern Styles
            if (contextHeaderStyle == null)
            {
                contextHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            if (contextSubHeaderStyle == null)
            {
                contextSubHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.0f, 0.90f, 1.0f) }
                };
            }

            if (contextItemStyle == null)
            {
                contextItemStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { background = Texture2D.blackTexture, textColor = new Color(0.85f, 0.92f, 0.98f) },
                    hover = { background = btnHoverTex, textColor = new Color(0.0f, 0.95f, 1.0f) },
                    padding = new RectOffset(10, 8, 4, 4)
                };
            }

            if (contextItemActiveStyle == null)
            {
                contextItemActiveStyle = new GUIStyle(contextItemStyle)
                {
                    normal = { background = btnActiveTex, textColor = new Color(0.0f, 1.0f, 0.65f) }
                };
            }

            // Apply Poppins Font across all active GUIStyles if a valid font asset is loaded
            Font poppins = customPoppinsFont != null ? customPoppinsFont : GetPoppinsFont();
            if (poppins != null)
            {
                GUI.skin.font = poppins;
                if (topNavBgStyle != null) topNavBgStyle.font = poppins;
                if (brandLogoStyle != null) brandLogoStyle.font = poppins;
                if (navBtnStyle != null) navBtnStyle.font = poppins;
                if (navBtnActiveStyle != null) navBtnActiveStyle.font = poppins;
                if (dropdownPanelStyle != null) dropdownPanelStyle.font = poppins;
                if (dropdownItemStyle != null) dropdownItemStyle.font = poppins;
                if (dropdownHeaderStyle != null) dropdownHeaderStyle.font = poppins;
                if (cardStyle != null) cardStyle.font = poppins;
                if (coordStyle != null) coordStyle.font = poppins;
                if (hintStyle != null) hintStyle.font = poppins;
                if (compassLabelStyle != null) compassLabelStyle.font = poppins;
                if (compassHeadingStyle != null) compassHeadingStyle.font = poppins;
                if (northTextStyle != null) northTextStyle.font = poppins;
                if (badgeStyle != null) badgeStyle.font = poppins;
                if (badgeSuccessStyle != null) badgeSuccessStyle.font = poppins;
                if (badgeOfflineStyle != null) badgeOfflineStyle.font = poppins;
                if (modalBoxStyle != null) modalBoxStyle.font = poppins;
                if (searchBoxStyle != null) searchBoxStyle.font = poppins;
                if (tabBtnStyle != null) tabBtnStyle.font = poppins;
                if (tabBtnActiveStyle != null) tabBtnActiveStyle.font = poppins;
                if (scaleLabelStyle != null) scaleLabelStyle.font = poppins;
                if (scaleSubStyle != null) scaleSubStyle.font = poppins;
                if (quickDockBtnStyle != null) quickDockBtnStyle.font = poppins;
                if (quickDockBtnActiveStyle != null) quickDockBtnActiveStyle.font = poppins;
                if (splashTitleStyle != null) splashTitleStyle.font = poppins;
                if (splashSubtitleStyle != null) splashSubtitleStyle.font = poppins;
                if (splashPhaseStyle != null) splashPhaseStyle.font = poppins;
                if (splashCheckStyle != null) splashCheckStyle.font = poppins;
                if (alertHeaderStyle != null) alertHeaderStyle.font = poppins;
                if (alertBodyStyle != null) alertBodyStyle.font = poppins;
                if (contextHeaderStyle != null) contextHeaderStyle.font = poppins;
                if (contextSubHeaderStyle != null) contextSubHeaderStyle.font = poppins;
                if (contextItemStyle != null) contextItemStyle.font = poppins;
                if (contextItemActiveStyle != null) contextItemActiveStyle.font = poppins;
            }
            else
            {
                GUI.skin.font = null;
            }
        }

        private void Update()
        {
            if (isBooting && showBootSplash)
            {
                bootTimer += Time.unscaledDeltaTime;

                // REAL SYSTEM TELEMETRY SYNCHRONIZATION & GATING
                if (isApiConnected)
                {
                    bootApiState = BootApiProbeState.Connected;
                }
                else if (bootApiState != BootApiProbeState.OfflineBypassed)
                {
                    if (bootTimer > bootApiProbeTimeout)
                    {
                        bootApiState = BootApiProbeState.FailedTimeout;
                    }
                    else
                    {
                        bootApiState = BootApiProbeState.Probing;
                    }
                }

                // Subsystem target progression
                float targetProgress = 0.15f; // Initial engine baseline

                // Subsystem 1: GIS 3D Layer & Mesh Topography
                if (FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.isLoaded)
                {
                    targetProgress += 0.28f;
                }
                else if (bootTimer > 0.8f)
                {
                    targetProgress += 0.22f;
                }

                // Fleet UI can start before the first telemetry packet arrives.
                if (FMSFleetManager.Instance != null)
                {
                    targetProgress += 0.25f;
                }
                else if (bootTimer > 1.6f)
                {
                    targetProgress += 0.20f;
                }

                // Subsystem 3: Smart Dispatch & Haul Network
                if (FMSSmartDispatchManager.Instance != null)
                {
                    targetProgress += 0.18f;
                }
                else if (bootTimer > 2.4f)
                {
                    targetProgress += 0.15f;
                }

                // Subsystem 4: Cloud Gateway API
                if (bootApiState == BootApiProbeState.Connected || bootApiState == BootApiProbeState.OfflineBypassed)
                {
                    targetProgress += 0.14f; // Completes to 100%
                }
                else if (bootApiState == BootApiProbeState.FailedTimeout)
                {
                    targetProgress = Mathf.Min(targetProgress, 0.85f); // Pause at 85% to indicate error
                }
                else
                {
                    // While still probing API, cap at 85%
                    targetProgress = Mathf.Min(targetProgress + 0.06f, 0.85f);
                }

                // Smooth progressive interpolation
                targetProgress = Mathf.Max(bootProgress, targetProgress);
                bootProgress = Mathf.MoveTowards(bootProgress, targetProgress, (0.35f + (targetProgress - bootProgress) * 2.4f) * Time.unscaledDeltaTime);

                // Auto-enter or keyboard skip ONLY allowed if all systems are verified
                if ((bootApiState == BootApiProbeState.Connected || bootApiState == BootApiProbeState.OfflineBypassed) && bootProgress >= 0.98f)
                {
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    {
                        bootProgress = 1f;
                        isBooting = false;
                    }
                }
                return;
            }

            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer > 0.4f)
            {
                fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                fpsTimer = 0f;
            }

            if (notificationTimer > 0f)
            {
                notificationTimer -= Time.unscaledDeltaTime;
                if (notificationTimer <= 0f) notificationMessage = "";
            }

            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 25000f))
            {
                UTMCoordinate cursorUtm = GeoCoordinateConverter.UnityToUTM(hit.point);
                lastEasting = cursorUtm.Easting;
                lastNorthing = cursorUtm.Northing;
                lastElevation = cursorUtm.Elevation;
                hasCursorCoordinate = true;
                cursorUtmInfo = $"E: {lastEasting:F1} m | N: {lastNorthing:F1} m | Elev: {lastElevation:F1} m";
            }

            // Keyboard Shortcuts (disabled if modal textfield is active)
            if (!IsBlockingModalOpen())
            {
                // [U] Toggle Fleet Units
                if (Input.GetKeyDown(KeyCode.U))
                {
                    EnsureFleetManager();
                    FMSFleetManager.Instance?.ToggleFleetVisibility();
                    ShowNotification($"🚚 Armada Unit Tambang: {(FMSFleetManager.Instance?.showFleet == true ? "Ditampilkan" : "Disembunyikan")}");
                }

                // [T] Toggle 3D Unit Overhead Floating Tags
                if (Input.GetKeyDown(KeyCode.T))
                {
                    showUnitOverheadTags = !showUnitOverheadTags;
                    ShowNotification($"🏷️ Label Overhead Unit 3D: {(showUnitOverheadTags ? "Ditampilkan" : "Disembunyikan")}");
                }

                // [V] Toggle Show / Hide All Markers
                if (Input.GetKeyDown(KeyCode.V))
                {
                    FMSMining3DLayer.Instance?.ToggleAllMarkers();
                }

                // [G] Toggle Spatial UTM 500m Grid
                if (Input.GetKeyDown(KeyCode.G))
                {
                    EnsureMiningDigitalTwinFX();
                    FMSMiningDigitalTwinFX.Instance?.ToggleSpatialGrid();
                }

                // [W] Toggle Water Sumps & Pit Lakes
                if (Input.GetKeyDown(KeyCode.W))
                {
                    EnsureMiningDigitalTwinFX();
                    FMSMiningDigitalTwinFX.Instance?.ToggleWaterSumps();
                }

                // [B] Toggle Blasting Zone & Geofence Boundaries
                if (Input.GetKeyDown(KeyCode.B))
                {
                    EnsureMiningDigitalTwinFX();
                    FMSMiningDigitalTwinFX.Instance?.ToggleBlastingZone();
                }

                // [H] Toggle Geotechnical Slope Stability Heatmap
                if (Input.GetKeyDown(KeyCode.H))
                {
                    EnsureSlopeStabilityHeatmap();
                    FMSSlopeStabilityHeatmap.Instance?.ToggleHeatmap();
                }

                // [F] Focus Pit Center
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
                {
                    FMSCameraController.Instance?.JumpTo(new Vector3(0f, 150f, 0f), 2800f);
                    ShowNotification("🎯 Kamera dipusatkan ke Pit Unggul");
                }

                // [Tab] Toggle Left Quick Dock Toolbar
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    showQuickDock = !showQuickDock;
                    ShowNotification(showQuickDock ? "📌 Bilah menu kiri ditampilkan" : "📌 Bilah menu kiri disembunyikan (Tekan [Tab] atau klik [▶] untuk membuka)");
                }

                // [O] Toggle Sembunyikan / Tampilkan Unit Offline / Dummy
                if (Input.GetKeyDown(KeyCode.O))
                {
                    if (FMSFleetManager.Instance != null)
                    {
                        FMSFleetManager.Instance.hideOfflineUnits = !FMSFleetManager.Instance.hideOfflineUnits;
                        FMSFleetManager.Instance.ApplyFleetCategoryFilters();
                        ShowNotification($"🚫 Filter Unit Offline: {(FMSFleetManager.Instance.hideOfflineUnits ? "Unit Offline Disembunyikan (Hanya Unit Database Aktif)" : "Semua Unit Ditampilkan")}");
                    }
                }

                // [Ctrl + F], [F3], [Ctrl + K], [K] or [/] Toggle Unit Search & Command Palette
                if (Input.GetKeyDown(KeyCode.F3) ||
                    (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F)) ||
                    (Input.GetKey(KeyCode.RightControl) && Input.GetKeyDown(KeyCode.F)) ||
                    Input.GetKeyDown(KeyCode.K) || 
                    (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.K)) || 
                    Input.GetKeyDown(KeyCode.Slash))
                {
                    showCommandPalette = !showCommandPalette;
                    if (showCommandPalette)
                    {
                        commandPaletteQuery = "";
                        focusCommandInputNextFrame = true;
                        ShowNotification("🔍 Cari Unit Tambang, Lokasi, atau Jalan (Ketik Kode Unit / Nama / Opr)");
                    }
                }

                // [Ctrl + D] or [D] Toggle Drone Inspection Tour
                if (Input.GetKeyDown(KeyCode.D))
                {
                    EnsureDigitalTwinAtmosphere();
                    FMSDigitalTwinAtmosphere.Instance?.ToggleDroneInspection();
                }

                // [Ctrl + E] or [E] Toggle Elevation Profiler
                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (FMSElevationProfiler.Instance != null)
                    {
                        if (FMSElevationProfiler.Instance.isProfilerOpen)
                            FMSElevationProfiler.Instance.CloseProfiler();
                        else if (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.points.Count >= 2)
                            FMSElevationProfiler.Instance.GenerateProfileFromPoints(FMSMeasureTool.Instance.points, "Profil Elevasi Garis Ukur");
                        else
                            ShowNotification("ℹ️ Buat garis ukur terlebih dahulu [M] untuk membuat profil elevasi.");
                    }
                }

                // [M] Toggle Distance Measure
                if (Input.GetKeyDown(KeyCode.M))
                {
                    EnsureMeasureTool();
                    FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.Distance);
                    showMeasureToolbar = true;
                }

                // [N] Reset North Up
                if (Input.GetKeyDown(KeyCode.N))
                {
                    FMSCameraController.Instance?.ResetHeadingToNorth();
                    ShowNotification("🧭 Orientasi Kamera Menghadap Utara (0°)");
                }

                // [P] or [2] Toggle 2D / 3D
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Alpha2))
                {
                    bool is2D = Camera.main != null && Camera.main.transform.eulerAngles.x > 75f;
                    if (is2D) FMSCameraController.Instance?.SetIsometricView();
                    else FMSCameraController.Instance?.SetTopDownView();
                }

                // [L] Toggle Labels
                if (Input.GetKeyDown(KeyCode.L))
                {
                    FMSMining3DLayer.Instance?.ToggleLabels();
                }

                // [R] Toggle Roads
                if (Input.GetKeyDown(KeyCode.R))
                {
                    FMSMining3DLayer.Instance?.ToggleRoads();
                }

                // [T] Cycle Sun Lighting
                if (Input.GetKeyDown(KeyCode.T))
                {
                    CycleSunLighting();
                }

                // [O] Toggle Hide Offline Units (Show only active live API units)
                if (Input.GetKeyDown(KeyCode.O))
                {
                    FMSFleetManager.Instance?.ToggleHideOfflineUnits();
                    ShowNotification($"🚫 Unit Offline: {(FMSFleetManager.Instance?.hideOfflineUnits == true ? "Disembunyikan (Hanya Unit Live API)" : "Ditampilkan Semua")}");
                }

                // [F8] Toggle Mobile In-Cabin Operator Dashboard
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    var cabinUI = FindObjectOfType<Virexa.FMS.Mobile.OperatorInCabinUI>();
                    if (cabinUI == null)
                    {
                        GameObject go = new GameObject("OperatorInCabinUI_Manager");
                        cabinUI = go.AddComponent<Virexa.FMS.Mobile.OperatorInCabinUI>();
                        cabinUI.isLoggedIn = true;
                        ShowNotification("📱 Mode Kabin Operator Mobile Diaktifkan! (Tekan F8 untuk Keluar)");
                    }
                    else
                    {
                        cabinUI.enabled = !cabinUI.enabled;
                        ShowNotification(cabinUI.enabled ? "📱 Mode Kabin Operator Mobile Diaktifkan!" : "🖥️ Kembali ke Mode Control Room 3D");
                    }
                }


                // [F9] or [C] Toggle Two-Way Radio Dispatch Messenger Modal
                if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.C))
                {
                    showDispatchRadioModal = !showDispatchRadioModal;
                    ShowNotification(showDispatchRadioModal ? "📻 Radio Komunikasi Dispatch Dibuka" : "📻 Radio Komunikasi Ditutup");
                }

                // [Escape] Close Modals / Menus / Tools
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (showCommandPalette) showCommandPalette = false;
                    else if (showDispatchRadioModal) showDispatchRadioModal = false;
                    else if (FMSUnitCctvManager.Instance != null && FMSUnitCctvManager.Instance.isCctvOpen) FMSUnitCctvManager.Instance.CloseCctv();
                    else if (FMSFtwSaveraManager.Instance != null && FMSFtwSaveraManager.Instance.isFtwModalOpen) FMSFtwSaveraManager.Instance.isFtwModalOpen = false;
                    else if (showUnitProductionModal) showUnitProductionModal = false;
                    else if (showFleetMatrixModal) showFleetMatrixModal = false;
                    else if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isContextMenuOpen) FMSFleetManager.Instance.CloseContextMenu();
                    else if (FMSCameraController.Instance != null && FMSCameraController.Instance.IsInUnitCameraMode) FMSCameraController.Instance.ExitUnitCamera();
                    else if (currentMenu != ActiveMenu.None) currentMenu = ActiveMenu.None;
                    else if (showAboutModal || showGuideModal || showLocationFilterModal || showUnitAssetModal || showUnitTagSettingsModal)
                    {
                        showAboutModal = false;
                        showGuideModal = false;
                        showLocationFilterModal = false;
                        showUnitAssetModal = false;
                        if (showUnitTagSettingsModal) SaveTagSettings();
                        if (showUnitAssetModal) FMSUnitAssetManager.Instance?.SaveSettings();
                        showUnitTagSettingsModal = false;
                    }
                    else if (FMSElevationProfiler.Instance != null && FMSElevationProfiler.Instance.isProfilerOpen)
                    {
                        FMSElevationProfiler.Instance.CloseProfiler();
                    }
                    else if (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.isToolActive)
                    {
                        FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.None);
                        showMeasureToolbar = false;
                    }
                }
            }
            else if (fmsMapEditorPanel != null && fmsMapEditorPanel.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                fmsMapEditorPanel.RequestClose();
            }
            else if (fmsModulePanel != null && fmsModulePanel.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                fmsModulePanel.Close();
            }
            else if (showCommandPalette && Input.GetKeyDown(KeyCode.Escape))
            {
                showCommandPalette = false;
            }
            else if (showWeatherReportModal && Input.GetKeyDown(KeyCode.Escape))
            {
                showWeatherReportModal = false;
            }
            else if (showWeatherDetailsModal && Input.GetKeyDown(KeyCode.Escape))
            {
                showWeatherDetailsModal = false;
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (showLocationFilterModal) showLocationFilterModal = false;
                else if (showUnitAssetModal)
                {
                    FMSUnitAssetManager.Instance?.SaveSettings();
                    showUnitAssetModal = false;
                }
                else if (showUnitTagSettingsModal)
                {
                    SaveTagSettings();
                    showUnitTagSettingsModal = false;
                }
                else if (showAboutModal) showAboutModal = false;
                else if (showGuideModal) showGuideModal = false;
                else if (showDispatchRadioModal) showDispatchRadioModal = false;
                else if (showUnitProductionModal) showUnitProductionModal = false;
                else if (showFleetMatrixModal) showFleetMatrixModal = false;
                else if (showFleetInventoryModal) showFleetInventoryModal = false;
            }

            if (Input.GetMouseButtonDown(0) && currentMenu != ActiveMenu.None)
            {
                if (!IsPointerOverUI())
                {
                    currentMenu = ActiveMenu.None;
                }
            }
        }

        private void OnGUI()
        {
            InitStyles();

            float w = Screen.width;
            float h = Screen.height;

            // 0. STARTUP BOOT SPLASH SCREEN OVERLAY (Virexa One)
            if (isBooting && showBootSplash)
            {
                DrawCleanBootSplashScreen(w, h);
                return;
            }

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !IsBlockingModalOpen();

            // 1. 3D FLOATING OVERHEAD UNIT TAGS & SHOVEL QUEUE BADGES (Drawn on 3D layer under navbars)
            if (showUnitOverheadTags)
            {
                DrawUnitFloatingOverheadTags(w, h);
            }
            if (FMSSmartDispatchManager.Instance != null && FMSSmartDispatchManager.Instance.showQueueOverheadBadges)
            {
                FMSSmartDispatchManager.Instance.DrawFloatingQueueBadges(w, h);
            }

            // 2. MATA ANGIN (COMPASS ROSE)
            if (showCompass)
            {
                DrawCompassWidget(w);
            }

            // 3. LEFT QUICK ACTION DOCK
            if (showQuickDock)
            {
                DrawQuickDock(h);
            }
            else
            {
                DrawQuickDockCollapsedTab(h);
            }

            // 4. GIS DYNAMIC SCALE BAR
            if (showScaleBar)
            {
                DrawScaleBarWidget(w, h);
            }

            // 5. REAL-TIME SURVEYING RETICLE
            DrawSurveyingReticle(w, h);

            // 6. FLEET PRODUCTION KPI MINI-BAR
            if (showKpiMiniBar)
            {
                DrawKpiMiniBar(w);
            }

            // 7. MINING WEATHER & ROAD CONDITION TELEMETRY
            if (showWeatherWidget)
            {
                DrawWeatherWidget(w, h);
            }

            // 8. ELEVATION PROFILER HUD
            if (FMSElevationProfiler.Instance != null && FMSElevationProfiler.Instance.isProfilerOpen)
            {
                FMSElevationProfiler.Instance.DrawProfilerHUD(w, h, cardStyle);
            }

            // 9. GEOTECHNICAL SLOPE STABILITY HEATMAP HUD
            if (FMSSlopeStabilityHeatmap.Instance != null && FMSSlopeStabilityHeatmap.Instance.showHeatmap)
            {
                FMSSlopeStabilityHeatmap.Instance.DrawHeatmapLegendHUD(w, h, cardStyle);
            }

            // 10. LIVE UNIT TELEMETRY INSPECTION HUD (Suppressed while right-click context menu is active)
            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.selectedUnit != null && !FMSFleetManager.Instance.isContextMenuOpen)
            {
                DrawSelectedUnitTelemetryCard(w, h);
            }

            // 11. BOTTOM CONTROL GUIDE
            if (showNavGuide)
            {
                float guideW = 860;
                float guideH = 46;
                GUI.Box(new Rect((w - guideW) / 2, h - guideH - 12, guideW, guideH), GUIContent.none, cardStyle);

                GUI.Label(new Rect((w - guideW) / 2 + 15, h - guideH - 6, guideW - 30, 20),
                    "🖱️ <b>Klik Kiri + Drag:</b> Geser Peta | 🖱️ <b>Klik Kanan + Drag:</b> Orbit 3D | 🎡 <b>Scroll:</b> Zoom",
                    hintStyle);

                GUI.Label(new Rect((w - guideW) / 2 + 15, h - guideH + 12, guideW - 30, 20),
                    "⌨️ <b>[Ctrl+F]:</b> Cari Unit | <b>[F8]:</b> Kabin Mobile | <b>[G]:</b> Grid | <b>[W]:</b> Sump | <b>[B]:</b> Blast | <b>[H]:</b> Slope | <b>[M]:</b> Ukur | <b>[D]:</b> Drone",
                    hintStyle);
            }

            // 12. MEASUREMENT TOOLBAR & STATS HUD
            if (showMeasureToolbar || (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.isToolActive))
            {
                DrawMeasureToolbarWidget(w, h);
            }

            // 13. TOP CORPORATE FUTURISTIC NAVIGATION BAR (Always on top of 3D scene elements)
            DrawTopNavigationBar(w);

            // 14. PERSISTENT BACKEND API DISCONNECTED ALERT BANNER
            if (!isApiConnected && !dismissApiWarning)
            {
                DrawApiDisconnectedBanner(w, h);
            }

            // 15. TOAST NOTIFICATION
            if (!string.IsNullOrEmpty(notificationMessage))
            {
                float notifW = 460;
                GUI.Box(new Rect((w - notifW) / 2, 54, notifW, 36), GUIContent.none, cardStyle);
                GUI.Label(new Rect((w - notifW) / 2 + 10, 60, notifW - 20, 24), notificationMessage, coordStyle);
            }

            // 16. ACTIVE DROPDOWN MENU (Higher Z-Index over Top Nav)
            DrawActiveDropdown();

            GUI.enabled = previousGuiEnabled;

            // 17. MODALS (Floating over all UI)
            if (showAboutModal || showGuideModal || showLocationFilterModal ||
                showUnitAssetModal || showUnitTagSettingsModal)
                GUI.Box(new Rect(0f, 0f, w, h), GUIContent.none, modalBoxStyle);
            if (showAboutModal) DrawAboutModal(w, h);
            if (showGuideModal) DrawGuideModal(w, h);
            if (showLocationFilterModal) DrawLocationFilterModal(w, h);
            if (showUnitAssetModal) DrawUnitAssetModal(w, h);
            if (showUnitTagSettingsModal) DrawUnitTagSettingsModal(w, h);
            if (showWeatherReportModal) DrawWeatherReportModal(w, h);
            if (showWeatherDetailsModal) DrawWeatherDetailsModal(w, h);
            if (FMSSmartDispatchManager.Instance != null && FMSSmartDispatchManager.Instance.isDispatchHudOpen)
            {
                FMSSmartDispatchManager.Instance.DrawDispatchHUD(w, h, cardStyle);
            }

            // 17.5 CAMERA MODE HUD OVERLAY
            if (FMSCameraController.Instance != null && FMSCameraController.Instance.IsInUnitCameraMode)
            {
                DrawCameraModeOverlay(w, h);
            }

            // 17.6 RIGHT-CLICK CONTEXT MENU
            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isContextMenuOpen && FMSFleetManager.Instance.contextMenuUnit != null)
            {
                DrawUnitContextMenu(w, h);
            }

            // 17.7 UNIT PRODUCTION DETAIL MODAL
            if (showUnitProductionModal && modalSelectedUnit != null)
            {
                DrawUnitProductionModal(w, h);
            }

            // 17.8 FLEET MATRIX & AGGREGATE MODAL (EXCAVATOR & ALL CHILD HAULERS)
            if (showFleetMatrixModal)
            {
                DrawFleetMatrixModal(w, h);
            }
            if (showFleetInventoryModal)
            {
                DrawFleetInventoryModal(w, h);
            }

            // 17.9 ONBOARD UNIT LIVE CCTV STREAM MODAL / PIP
            if (FMSUnitCctvManager.Instance != null && FMSUnitCctvManager.Instance.isCctvOpen)
            {
                DrawUnitCctvModal(w, h);
            }

            // 17.95 FTW SAVERA OPERATOR HEALTH & FATIGUE MATRIX MODAL
            if (FMSFtwSaveraManager.Instance != null && FMSFtwSaveraManager.Instance.isFtwModalOpen)
            {
                FMSFtwSaveraManager.Instance.DrawFtwSaveraModal(w, h, cardStyle, brandLogoStyle ?? coordStyle, hintStyle, dropdownPanelStyle, navBtnStyle, navBtnActiveStyle, lineAccentTex);
            }

            // 17.96 TWO-WAY RADIO DISPATCH MESSENGER MODAL (CONTROL ROOM <-> CABIN)
            if (showDispatchRadioModal)
            {
                DrawControlRoomDispatchRadioModal(w, h);
            }

            // 17.97 LIVE RADIO TALKBACK & INCOMING CABIN VOICE OVERLAY
            DrawLiveRadioTransmissionOverlay(w, h);

            // 18. SMART COMMAND PALETTE (CTRL + K)
            if (showCommandPalette)
            {
                DrawCommandPalette(w, h);
            }

            // 19. HIGH-TECH FLOATING TOOLTIP
            if (fmsModulePanel != null && fmsModulePanel.IsOpen)
            {
                fmsModulePanel.Draw(w, h);
            }
            if (mtcPanel != null && mtcPanel.IsOpen)
            {
                mtcPanel.Draw(w, h);
            }
            if (fmsMapEditorPanel != null && fmsMapEditorPanel.IsOpen)
            {
                fmsMapEditorPanel.Draw(w, h);
            }
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                DrawFloatingTooltip(GUI.tooltip);
            }
        }

        private void DrawTopNavigationBar(float w)
        {
            float navH = 44f;

            // 1. 3D Drop Shadow cast onto terrain
            if (dropShadowTex != null)
            {
                GUI.DrawTexture(new Rect(0, navH, w, 8), dropShadowTex);
            }

            // 2. Glassmorphic 3D Nav Bar Background
            GUI.Box(new Rect(0, 0, w, navH), GUIContent.none, topNavBgStyle);

            // 3. Top Specular Bevel Highlight (1px)
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(0, 0, w, 1), topHighlightTex);
            }

            // 4. Bottom Glowing Cyan Laser Accent Line (2px)
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(0, navH - 2, w, 2), lineAccentTex);
            }

            float curX = 14f;
            bool compactNav = w < 1650f;

            // Brand Logo: VIREXAONE
            GUI.Label(new Rect(curX, 10, 160, 24), "⚡ <b>VIREXA<color=#00E5FF>ONE</color></b>", brandLogoStyle);
            curX += 135f;

            if (!compactNav)
            {
                GUI.Box(new Rect(curX, 12, 58, 20), "GIS 3D", badgeStyle);
                curX += 68f;
            }

            // GROUP 1: OPERATIONAL MAIN MENUS
            fileBtnX = curX;
            curX = DrawMenuButton("Data", ActiveMenu.File, curX, 52);
            editBtnX = curX;
            curX = DrawMenuButton("Ukur", ActiveMenu.Edit, curX, 52);
            viewBtnX = curX;
            curX = DrawMenuButton("Peta", ActiveMenu.View, curX, 55);
            asetBtnX = curX;
            curX = DrawMenuButton("Layer", ActiveMenu.Aset, curX, 55);
            winBtnX = curX;
            curX = DrawMenuButton("Panel", ActiveMenu.Windows, curX, 60);
            if (w >= 960f && GUI.Button(new Rect(curX, 8f, 52f, 28f), new GUIContent("FMS", "Buka modul FMS"), navBtnStyle))
            {
                EnsureFmsModulePanel().Open();
                currentMenu = ActiveMenu.None;
            }
            if (w >= 960f) curX += 59f;

            // Vertical Navigation Divider
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(curX + 3, 11, 1, 22), topHighlightTex);
            }
            curX += 9f;

            // GROUP 2: DEDICATED DISTINCT SETTINGS TAB (Distinguished Configuration Hub)
            settingsBtnX = curX;
            curX = DrawSettingsMenuButton(compactNav ? "Pengaturan" : "⚙️ Pengaturan", ActiveMenu.Settings, curX, compactNav ? 82 : 115);

            // Vertical Navigation Divider
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(curX + 3, 11, 1, 22), topHighlightTex);
            }
            curX += 9f;

            // GROUP 3: HELP & SUPPORT
            helpBtnX = curX;
            curX = DrawMenuButton("Bantuan", ActiveMenu.Help, curX, 70);

            // Center: Coordinate HUD
            bool narrowNav = w < 950f;
            float statusStartX = narrowNav ? w - 120f : compactNav ? w - 260f : w - 445f;
            float coordAvailable = statusStartX - curX - 20f;
            if (showCoordHud && coordAvailable >= 320f)
            {
                float centerW = Mathf.Min(420f, coordAvailable);
                float centerX = curX + 10f;
                GUI.Box(new Rect(centerX, 7, centerW, 30), GUIContent.none, cardStyle);
                GUI.Label(new Rect(centerX + 10, 11, centerW - 20, 22), $"📍 <b>UTM 50N:</b> {cursorUtmInfo}", coordStyle);
            }

            // Right: Telemetry Badges & Mining Shift Clock
            if (showTelemetry)
            {
                float rightX = statusStartX;

                // 1. Real-Time Mining Shift Clock (WITA Site Standard)
                DateTime nowWita = DateTime.UtcNow.AddHours(8);
                bool isDayShift = nowWita.Hour >= 6 && nowWita.Hour < 18;
                string shiftIcon = isDayShift ? "☀️" : "🌙";
                string shiftName = isDayShift ? "SHIFT 1" : "SHIFT 2";
                string shiftText = $"{shiftIcon} <b>{nowWita:HH:mm:ss} | {shiftName}</b>";

                if (!narrowNav && GUI.Button(new Rect(rightX, 10, 130, 24), shiftText, badgeStyle))
                {
                    CycleSunLighting();
                }

                // 2. API Status Badge
                int liveFleetCount = FMSFleetManager.Instance != null ? FMSFleetManager.Instance.activeFleet.Count : 0;
                bool feedStale = FMSFleetManager.Instance == null || FMSFleetManager.Instance.isTelemetryFeedStale;
                string apiText = !isApiConnected
                    ? "<color=#FF4D4D>●</color> <b>OFFLINE</b>"
                    : feedStale ? "<color=#FFB800>●</color> <b>DATA LAMA</b>"
                    : $"<color=#00FFA3>●</color> <b>API ({liveFleetCount})</b>";
                GUIStyle apiStyle = !isApiConnected ? (badgeOfflineStyle ?? cardStyle) : feedStale ? badgeStyle : badgeSuccessStyle;

                if (GUI.Button(new Rect(rightX + (narrowNav ? 0f : 135f), 10, 115, 24), apiText, apiStyle))
                {
                    if (isApiConnected && feedStale)
                    {
                        int age = FMSFleetManager.Instance != null ? FMSFleetManager.Instance.telemetryFeedAgeSeconds : 0;
                        ShowNotification(age > 0 ? $"Feed GPS belum diperbarui selama {age / 60} menit." : "Feed GPS belum tersedia.");
                    }
                    else if (isApiConnected)
                    {
                        ShowNotification($"✅ {apiConnectionMessage} | Latensi: {apiLatencyMs:F0}ms");
                    }
                    else
                    {
                        dismissApiWarning = false;
                        RetryApiConnection();
                        ShowNotification($"⚠️ {apiConnectionMessage}. Mencoba menghubungkan ulang...");
                    }
                }

                // 3. GIS Status
                if (!compactNav)
                {
                    bool gisReady = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.isLoaded;
                    GUI.Box(new Rect(rightX + 255, 10, 95, 24), gisReady ? "<b>GIS SIAP</b>" : "<b>GIS MEMUAT</b>", badgeStyle);

                    // 4. FPS Counter
                    GUI.Box(new Rect(rightX + 355, 10, 75, 24), $"⚡ <b>{fps:F0} FPS</b>", badgeStyle);
                }
            }
        }

        private System.Collections.IEnumerator CheckApiHealthLoop()
        {
            string[] candidateUrls = new string[]
            {
                apiBaseUrl
            };

            while (true)
            {
                bool connected = false;

                // 1. Try current apiBaseUrl first, then try candidate ports if failing
                List<string> probeList = new List<string>();
                if (!string.IsNullOrEmpty(apiBaseUrl)) probeList.Add(apiBaseUrl.TrimEnd('/'));
                foreach (var url in candidateUrls)
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        string trimmed = url.TrimEnd('/');
                        if (!probeList.Contains(trimmed)) probeList.Add(trimmed);
                    }
                }

                foreach (var testUrl in probeList)
                {
                    float startTime = Time.realtimeSinceStartup;
                    bool probeSuccess = false;

                    // Probe 1: /health
                    UnityEngine.Networking.UnityWebRequest reqHealth = null;
                    try
                    {
                        reqHealth = UnityEngine.Networking.UnityWebRequest.Get($"{testUrl}/health");
                        reqHealth.timeout = 5;
                    }
                    catch { reqHealth = null; }

                    if (reqHealth != null)
                    {
                        using (reqHealth)
                        {
                            UnityEngine.Networking.UnityWebRequestAsyncOperation op = null;
                            activeApiRequest = reqHealth;
                            try { op = reqHealth.SendWebRequest(); } catch { op = null; }

                            if (op != null) yield return op;
                            activeApiRequest = null;

                            if (reqHealth.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                            {
                                probeSuccess = true;
                            }
                            else if (reqHealth.responseCode == 403)
                            {
                                apiConnectionMessage = reqHealth.downloadHandler != null &&
                                    reqHealth.downloadHandler.text.IndexOf("untrusted_network", StringComparison.OrdinalIgnoreCase) >= 0
                                    ? "Backend menolak IP client. Periksa TrustedClientIps/VPN."
                                    : "Backend menolak akses. Periksa Access/VPN.";
                            }
                        }
                    }

                    // Fallback Probe 2: / if /health didn't respond
                    if (!probeSuccess)
                    {
                        UnityEngine.Networking.UnityWebRequest reqRoot = null;
                        try
                        {
                            reqRoot = UnityEngine.Networking.UnityWebRequest.Get($"{testUrl}/");
                            reqRoot.timeout = 5;
                        }
                        catch { reqRoot = null; }

                        if (reqRoot != null)
                        {
                            using (reqRoot)
                            {
                                UnityEngine.Networking.UnityWebRequestAsyncOperation op = null;
                                activeApiRequest = reqRoot;
                                try { op = reqRoot.SendWebRequest(); } catch { op = null; }

                                if (op != null) yield return op;
                                activeApiRequest = null;

                                if (reqRoot.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                                {
                                    probeSuccess = true;
                                }
                                else if (reqRoot.responseCode == 403)
                                {
                                    apiConnectionMessage = reqRoot.downloadHandler != null &&
                                        reqRoot.downloadHandler.text.IndexOf("untrusted_network", StringComparison.OrdinalIgnoreCase) >= 0
                                        ? "Backend menolak IP client. Periksa TrustedClientIps/VPN."
                                        : "Backend menolak akses. Periksa Access/VPN.";
                                }
                            }
                        }
                    }

                    if (probeSuccess)
                    {
                        connected = true;
                        apiBaseUrl = testUrl;
                        isApiConnected = true;
                        apiConnectionMessage = "Backend lokal terhubung";
                        apiLatencyMs = (Time.realtimeSinceStartup - startTime) * 1000f;

                        // Sync with FMSMining3DLayer backendUrl
                        if (FMSFleetMessenger.Instance != null)
                        {
                            FMSFleetMessenger.Instance.backendBaseUrl = testUrl;
                        }
                        if (FMSMining3DLayer.Instance != null)
                        {
                            FMSMining3DLayer.Instance.backendUrl = testUrl;
                            if (!FMSMining3DLayer.Instance.isLoaded || FMSMining3DLayer.Instance.spawnedMarkers.Count == 0)
                            {
                                StartCoroutine(FMSMining3DLayer.Instance.FetchAllMiningLayers());
                            }
                        }
                        break;
                    }
                }

                if (!connected)
                {
                    isApiConnected = false;
                    if (string.IsNullOrWhiteSpace(apiConnectionMessage) || apiConnectionMessage == "Backend lokal terhubung")
                        apiConnectionMessage = "Backend lokal tidak terhubung";
                }

                if (isApiConnected)
                {
                    if (loadedSiteProfileUrl != apiBaseUrl)
                    {
                        using (UnityEngine.Networking.UnityWebRequest profile = UnityEngine.Networking.UnityWebRequest.Get($"{apiBaseUrl}/api/v1/site/profile"))
                        {
                            FMSApiSession.Authorize(profile);
                            profile.timeout = 10;
                            activeApiRequest = profile;
                            yield return profile.SendWebRequest();
                            activeApiRequest = null;
                            if (profile.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                            {
                                SiteProfileDto site = JsonUtility.FromJson<SiteProfileDto>(profile.downloadHandler.text);
                                if (site != null && !string.IsNullOrWhiteSpace(site.display_name))
                                {
                                    companyDisplayName = site.display_name;
                                    if (!string.IsNullOrWhiteSpace(site.site_id) && site.site_id != siteId)
                                    {
                                        siteId = site.site_id;
                                        if (autoLoadedCompanyLogo) companyLogo = null;
                                        if (decodedCompanyLogo != null) Destroy(decodedCompanyLogo);
                                        decodedCompanyLogo = null;
                                        bootLogoLoadAttempted = false;
                                        if (autoLoadedSitePreview) bootSiteOrtho = null;
                                        autoLoadedCompanyLogo = false;
                                        autoLoadedSitePreview = false;
                                    }
                                    FMSUnitCctvManager.Instance?.SwitchSite(siteId, apiBaseUrl);
                                    loadedSiteProfileUrl = apiBaseUrl;
                                }
                            }
                        }
                    }

                    // 1. Fetch Live Fleet Summary KPI from Backend
                    using (UnityEngine.Networking.UnityWebRequest reqFleet = UnityEngine.Networking.UnityWebRequest.Get($"{apiBaseUrl}/api/v1/fleet/summary"))
                    {
                        FMSApiSession.Authorize(reqFleet);
                        reqFleet.timeout = 15;
                        activeApiRequest = reqFleet;
                        yield return reqFleet.SendWebRequest();
                        activeApiRequest = null;
                        hasRealFleetSummaryData = false;
                        if (reqFleet.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                FleetSummaryApiResponse fResp = JsonUtility.FromJson<FleetSummaryApiResponse>(reqFleet.downloadHandler.text);
                                if (fResp != null && fResp.status == "success" && fResp.data != null)
                                {
                                    hasRealFleetSummaryData = true;
                                    realTotalUnits = fResp.data.total_units;
                                    apiOnlineUnits = fResp.data.active_online;
                                    realExcavatorsTotal = fResp.data.excavators_total;
                                    realExcavatorsOperating = fResp.data.excavators_operating;
                                    realHaulersTotal = fResp.data.haulers_total;
                                    realHaulersOperating = fResp.data.haulers_operating;

                                    if (fResp.data.status_breakdown != null)
                                    {
                                        realHaulingCount = fResp.data.status_breakdown.hauling;
                                        realLoadingCount = fResp.data.status_breakdown.loading;
                                        realDumpingCount = fResp.data.status_breakdown.dumping;
                                        realIdleCount = fResp.data.status_breakdown.standby_idle;
                                    }

                                    if (realTotalUnits > 0)
                                    {
                                        realFleetPa = ((float)apiOnlineUnits / realTotalUnits) * 100f;
                                    }

                                    if (realExcavatorsOperating > 0)
                                    {
                                        realMatchFactor = (float)realHaulersOperating / (realExcavatorsOperating * 5.5f);
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // 2. Fetch Live Production Summary from PostgreSQL Database (tbl_m_hauling_hexagon)
                    using (UnityEngine.Networking.UnityWebRequest reqProd = UnityEngine.Networking.UnityWebRequest.Get($"{apiBaseUrl}/api/v1/production/summary"))
                    {
                        FMSApiSession.Authorize(reqProd);
                        reqProd.timeout = 30;
                        activeApiRequest = reqProd;
                        yield return reqProd.SendWebRequest();
                        activeApiRequest = null;
                        hasRealProductionData = false;
                        if (reqProd.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                ProductionApiResponse pResp = JsonUtility.FromJson<ProductionApiResponse>(reqProd.downloadHandler.text);
                                if (pResp != null && pResp.status == "success" && pResp.data != null &&
                                    pResp.data.data_available && pResp.data.metric_source == "hauling_snapshot")
                                {
                                    realCurrentPayloadTons = pResp.data.current_payload_ton;
                                    realRecordedLoads = pResp.data.recorded_loads;
                                    realProductionShiftLabel = DateTimeOffset.TryParse(pResp.data.shift_start, out var shiftStart)
                                        ? shiftStart.ToOffset(TimeSpan.FromHours(8)).ToString("HH:mm") : "";
                                    hasRealProductionData = true;
                                }
                            }
                            catch { }
                        }
                    }
                }

                yield return new WaitForSeconds(2.5f);
            }
        }

        private float DrawMenuButton(string label, ActiveMenu menu, float x, float width)
        {
            bool isActive = currentMenu == menu;
            GUIStyle style = isActive ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(x, 8, width, 28), label, style))
            {
                currentMenu = isActive ? ActiveMenu.None : menu;
            }

            // Glowing Underline Indicator for Active Menu Tab
            if (isActive && lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, 34, width - 4, 2), lineAccentTex);
            }
            return x + width + 4f;
        }

        private float DrawSettingsMenuButton(string label, ActiveMenu menu, float x, float width)
        {
            bool isActive = currentMenu == menu;

            // Distinct Cyber Outline for Settings Button
            if (splashGlowEmeraldTex != null && isActive)
            {
                GUI.DrawTexture(new Rect(x, 8, width, 2), splashGlowEmeraldTex);
                GUI.DrawTexture(new Rect(x, 34, width, 2), splashGlowEmeraldTex);
            }
            else if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, 34, width - 4, 1.5f), lineAccentTex);
            }

            GUIStyle style = isActive ? navBtnActiveStyle : navBtnStyle;
            if (GUI.Button(new Rect(x, 8, width, 28), label, style))
            {
                currentMenu = isActive ? ActiveMenu.None : menu;
            }
            return x + width + 4f;
        }

        private void DrawActiveDropdown()
        {
            if (currentMenu == ActiveMenu.None) return;
            Rect drop = GetActiveDropdownRect(Screen.width);

            switch (currentMenu)
            {
                case ActiveMenu.File:
                    DrawFileDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.Edit:
                    DrawEditDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.View:
                    DrawViewDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.Aset:
                    DrawAsetDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.Windows:
                    DrawWindowsDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.Settings:
                    DrawSettingsDropdown(drop.x, drop.y, drop.width);
                    break;
                case ActiveMenu.Help:
                    DrawHelpDropdown(drop.x, drop.y, drop.width);
                    break;
            }
        }

        private Rect GetActiveDropdownRect(float screenW)
        {
            float anchor = 0f;
            float width = 290f;
            float height = 265f;
            switch (currentMenu)
            {
                case ActiveMenu.File: anchor = fileBtnX; height = GetFileDropdownHeight(); break;
                case ActiveMenu.Edit: anchor = editBtnX; height = 235f; break;
                case ActiveMenu.View: anchor = viewBtnX; width = 305f; height = 510f; break;
                case ActiveMenu.Aset: anchor = asetBtnX; width = 320f; height = 570f; break;
                case ActiveMenu.Windows: anchor = winBtnX; width = 305f; height = 480f; break;
                case ActiveMenu.Settings: anchor = settingsBtnX; width = 325f; height = 485f; break;
                case ActiveMenu.Help: anchor = helpBtnX; height = 180f; break;
            }
            width = Mathf.Min(width, screenW - 16f);
            float x = Mathf.Clamp(anchor, 8f, Mathf.Max(8f, screenW - width - 8f));
            return new Rect(x, 44f, width, height);
        }

        private float GetFileDropdownHeight()
        {
            bool canOpenFolder = Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer;
            return 213f + (Application.isEditor ? 26f : 0f) + (canOpenFolder ? 26f : 0f);
        }

        private void DrawMenuSeparator(float x, ref float y, float w)
        {
            y += 2f;
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 6, y, w - 12, 1), topHighlightTex);
            }
            y += 4f;
        }

        private void DrawDockSeparator(float x, ref float y, float w)
        {
            y += 2f;
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 6, y, w - 12, 1), lineAccentTex);
            }
            else if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 6, y, w - 12, 1), topHighlightTex);
            }
            y += 4f;
        }

        private void DrawFileDropdown(float x, float y, float w)
        {
            float h = GetFileDropdownHeight();
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "CITRA & EKSPOR PETA", dropdownHeaderStyle);
            itemY += 20f;

#if UNITY_EDITOR
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📂 Impor citra peta...", dropdownItemStyle))
            {
                currentMenu = ActiveMenu.None;
                string selectedFile = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                    "Pilih File Citra GeoTIFF / Orthophoto",
                    "",
                    new string[] { "Citra / Orthophoto", "tif,tiff,png,jpg,jpeg", "GeoTIFF TIF", "tif,tiff", "PNG Image", "png", "JPG Image", "jpg,jpeg", "Semua File", "*" }
                );

                if (!string.IsNullOrEmpty(selectedFile))
                {
                    if (RealMiningGISLoader.Instance == null)
                    {
                        ShowNotification("Layer terrain belum tersedia.");
                    }
                    else
                    {
                        bool applied = RealMiningGISLoader.Instance.LoadAndApplyGeoTIFFFromFile(selectedFile, out string msg);
                        ShowNotification($"{(applied ? "✅" : "⚠️")} {msg}");
                    }
                }
            }
            itemY += spacing;
#endif

            bool canOpenFolder = Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer;
            if (canOpenFolder && GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📁 Buka folder citra...", dropdownItemStyle))
            {
                if (RealMiningGISLoader.Instance != null)
                {
                    RealMiningGISLoader.Instance.OpenGeoTIFFFolderInExplorer();
                    ShowNotification("Folder citra dibuka di Explorer.");
                }
                else ShowNotification("Layer terrain belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            if (canOpenFolder) itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📸 Screenshot Peta Aktif (PNG)", dropdownItemStyle))
            {
                string filename = $"Virexaone_Map_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                ScreenCapture.CaptureScreenshot(filename);
                ShowNotification($"✅ Screenshot tersimpan: {filename}");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📋 Salin Koordinat UTM Kursor", dropdownItemStyle))
            {
                if (hasCursorCoordinate)
                {
                    GUIUtility.systemCopyBuffer = $"UTM Zone 50N | Easting: {lastEasting:F2} | Northing: {lastNorthing:F2} | RL: {lastElevation:F2}";
                    ShowNotification("📋 Koordinat UTM disalin ke clipboard!");
                }
                else ShowNotification("Arahkan kursor ke peta terlebih dahulu.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "MUAT ULANG DATA", dropdownHeaderStyle);
            itemY += 20f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🔄 Reload Layer Terrain GeoTIFF", dropdownItemStyle))
            {
                if (RealMiningGISLoader.Instance != null)
                {
                    RealMiningGISLoader.Instance.BuildRealGISTerrain();
                    ShowNotification("Terrain dimuat ulang.");
                }
                else ShowNotification("Layer terrain belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🔄 Muat ulang titik & jalan", dropdownItemStyle))
            {
                if (FMSMining3DLayer.Instance != null)
                {
                    StartCoroutine(FMSMining3DLayer.Instance.FetchAllMiningLayers());
                    ShowNotification("Memuat ulang titik dan jalan dari layanan peta...");
                }
                else ShowNotification("Layer peta belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), "❌ Tutup Menu", dropdownItemStyle))
            {
                currentMenu = ActiveMenu.None;
            }
        }

        private void DrawEditDropdown(float x, float y, float w)
        {
            float h = 235f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "PENGUKURAN SPASIAL 3D", dropdownHeaderStyle);
            itemY += 20f;

            EnsureMeasureTool();

            bool isDistActive = FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.currentMode == FMSMeasureTool.MeasureMode.Distance;
            string distLabel = isDistActive ? "✅ 📐 Ukur Jarak 3D (Polyline) [M]" : "⬜ 📐 Ukur Jarak 3D (Polyline) [M]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), distLabel, dropdownItemStyle))
            {
                FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.Distance);
                showMeasureToolbar = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            bool isSlopeActive = FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.currentMode == FMSMeasureTool.MeasureMode.SlopeGradient;
            string slopeLabel = isSlopeActive ? "✅ ⛰️ Ukur Kemiringan Lereng (Slope)" : "⬜ ⛰️ Ukur Kemiringan Lereng (Slope)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), slopeLabel, dropdownItemStyle))
            {
                FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.SlopeGradient);
                showMeasureToolbar = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            bool isAreaActive = FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.currentMode == FMSMeasureTool.MeasureMode.PolygonArea;
            string areaLabel = isAreaActive ? "✅ ⬛ Ukur Luas Area (Polygon)" : "⬜ ⬛ Ukur Luas Area (Polygon)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), areaLabel, dropdownItemStyle))
            {
                FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.PolygonArea);
                showMeasureToolbar = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "PROFIL & KALIBRASI", dropdownHeaderStyle);
            itemY += 20f;

            bool isProf = FMSElevationProfiler.Instance != null && FMSElevationProfiler.Instance.isProfilerOpen;
            string profTxt = isProf ? "✅ 📈 Profil Elevasi Penampang [E]" : "⬜ 📈 Profil Elevasi Penampang [E]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), profTxt, dropdownItemStyle))
            {
                EnsureElevationProfiler();
                if (FMSElevationProfiler.Instance.isProfilerOpen)
                    FMSElevationProfiler.Instance.CloseProfiler();
                else if (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.points.Count >= 2)
                    FMSElevationProfiler.Instance.GenerateProfileFromPoints(FMSMeasureTool.Instance.points, "Profil Garis Ukur 3D");
                else
                    ShowNotification("ℹ️ Buat garis ukur terlebih dahulu [M] untuk membuat profil elevasi.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🗑️ Bersihkan Semua Garis Ukur", dropdownItemStyle))
            {
                FMSMeasureTool.Instance.Clear();
                ShowNotification("🗑️ Semua titik dan garis pengukuran dibersihkan.");
                currentMenu = ActiveMenu.None;
            }
        }

        private void DrawViewDropdown(float x, float y, float w)
        {
            float h = 510f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "PENCAHAYAAN AREA", dropdownHeaderStyle);
            itemY += 22f;
            string[] timeLabels = { "Siang", "Senja", "Malam", "Pagi" };
            float modeW = (w - 20f) / timeLabels.Length;
            for (int i = 0; i < timeLabels.Length; i++)
            {
                int mode = i;
                GUIStyle modeStyle = sunCycleIndex == mode ? navBtnActiveStyle : dropdownItemStyle;
                if (GUI.Button(new Rect(x + 10f + i * modeW, itemY, modeW - 3f, 26f),
                    new GUIContent(timeLabels[i], "Ubah pencahayaan area dan lampu unit"), modeStyle))
                    SetSunLighting(mode);
            }
            itemY += 33f;
            DrawMenuSeparator(x, ref itemY, w);

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "KAMERA & SUDUT PANDANG PIT", dropdownHeaderStyle);
            itemY += 20f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🎯 Pusatkan Kamera ke Pit [F]", dropdownItemStyle))
            {
                if (FMSCameraController.Instance != null)
                {
                    FMSCameraController.Instance.JumpTo(new Vector3(0f, 150f, 0f), 2800f);
                    ShowNotification("Kamera dipusatkan ke pit.");
                }
                else ShowNotification("Pengendali kamera belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🧭 Hadap Utara Murni (0°) [N]", dropdownItemStyle))
            {
                if (FMSCameraController.Instance != null)
                {
                    FMSCameraController.Instance.ResetHeadingToNorth();
                    ShowNotification("Kamera menghadap utara.");
                }
                else ShowNotification("Pengendali kamera belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🗺️ Pandangan Peta 2D (90°) [P]", dropdownItemStyle))
            {
                if (FMSCameraController.Instance != null)
                {
                    FMSCameraController.Instance.SetTopDownView();
                    ShowNotification("Tampilan peta 2D aktif.");
                }
                else ShowNotification("Pengendali kamera belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🏔️ Pandangan Isometrik 3D Pit", dropdownItemStyle))
            {
                if (FMSCameraController.Instance != null)
                {
                    FMSCameraController.Instance.SetIsometricView();
                    ShowNotification("Tampilan isometrik aktif.");
                }
                else ShowNotification("Pengendali kamera belum tersedia.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            bool isDrone = FMSDigitalTwinAtmosphere.Instance != null && FMSDigitalTwinAtmosphere.Instance.enableDroneInspection;
            string droneTxt = isDrone ? "✅ Mode inspeksi drone [D]" : "⬜ Mode inspeksi drone [D]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), droneTxt, dropdownItemStyle))
            {
                EnsureDigitalTwinAtmosphere();
                FMSDigitalTwinAtmosphere.Instance?.ToggleDroneInspection();
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "LAYER VISUALISASI MEDAN", dropdownHeaderStyle);
            itemY += 20f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🌈 Garis Kontur Topografi", dropdownItemStyle))
            {
                if (FMSContourVisualizer.Instance != null)
                {
                    FMSContourVisualizer.Instance.ToggleContours();
                    ShowNotification($"🌈 Mode Kontur: {FMSContourVisualizer.Instance.currentMode}");
                }
                else
                {
                    ShowNotification("Layer kontur belum tersedia.");
                }
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            EnsureMiningDigitalTwinFX();
            EnsureSlopeStabilityHeatmap();

            bool isGridOn = FMSMiningDigitalTwinFX.Instance != null && FMSMiningDigitalTwinFX.Instance.showSpatialGrid;
            string gridTxt = isGridOn ? "✅ 🌐 Grid Spasial UTM 500m [G]" : "⬜ 🌐 Grid Spasial UTM 500m [G]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), gridTxt, dropdownItemStyle))
            {
                FMSMiningDigitalTwinFX.Instance?.ToggleSpatialGrid();
            }
            itemY += spacing;

            bool isHeatmapOn = FMSSlopeStabilityHeatmap.Instance != null && FMSSlopeStabilityHeatmap.Instance.showHeatmap;
            string hmTxt = isHeatmapOn ? "✅ Peta kemiringan medan [H]" : "⬜ Peta kemiringan medan [H]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), hmTxt, dropdownItemStyle))
            {
                FMSSlopeStabilityHeatmap.Instance?.ToggleHeatmap();
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "TAMPILAN JALAN", dropdownHeaderStyle);
            itemY += 22f;

            FMSMining3DLayer roadLayer = FMSMining3DLayer.Instance;
            if (roadLayer == null) return;

            Color[] roadSwatches =
            {
                new Color(0.25f, 0.76f, 0.86f),
                new Color(0.94f, 0.96f, 0.90f),
                new Color(1.00f, 0.68f, 0.38f),
                new Color(0.73f, 0.67f, 0.98f)
            };
            string[] swatchNames = { "Biru", "Putih", "Jingga", "Ungu" };
            GUIStyle swatchLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.82f, 0.84f) }
            };
            Color oldColor = GUI.color;
            for (int i = 0; i < roadSwatches.Length; i++)
            {
                float swatchX = x + 16f + i * 70f;
                GUI.color = i == roadLayer.RoadColorPresetIndex ? Color.white : new Color(0.35f, 0.43f, 0.45f);
                GUI.DrawTexture(new Rect(swatchX, itemY, 52f, 27f), Texture2D.whiteTexture);
                GUI.color = roadSwatches[i];
                GUI.DrawTexture(new Rect(swatchX + 2f, itemY + 2f, 48f, 23f), Texture2D.whiteTexture);
                GUI.color = oldColor;
                if (GUI.Button(new Rect(swatchX, itemY, 52f, 27f), GUIContent.none, GUIStyle.none))
                    roadLayer.SetRoadColorPreset(i);
                GUI.Label(new Rect(swatchX, itemY + 28f, 52f, 16f), swatchNames[i], swatchLabelStyle);
            }

            itemY += 51f;
            GUI.Label(new Rect(x + 10f, itemY, w - 20f, 20f),
                $"Lebar acuan   {roadLayer.RoadVisualWidthMeters:F0} m", dropdownHeaderStyle);
            itemY += 25f;
            float width = GUI.HorizontalSlider(new Rect(x + 16f, itemY, w - 32f, 18f),
                roadLayer.RoadVisualWidthMeters, 8f, 40f);
            if (!Mathf.Approximately(width, roadLayer.RoadVisualWidthMeters))
                roadLayer.SetRoadVisualWidth(width);
        }

        private void DrawAsetDropdown(float x, float y, float w)
        {
            float h = 570f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            // SECTION 1: MASTER ARMADA 3D
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "🚚 ARMADA BERGERAK (FLEET 3D)", dropdownHeaderStyle);
            itemY += 20f;

            EnsureFleetManager();
            EnsureMining3DLayer();
            EnsureMiningDigitalTwinFX();

            // 1. Armada Unit Tambang 3D
            bool fleetOn = FMSFleetManager.Instance != null && FMSFleetManager.Instance.showFleet;
            string fleetTxt = fleetOn ? "✅ 🚚 Tampilkan Armada Fleet 3D [U]" : "⬜ 🚚 Tampilkan Armada Fleet 3D [U]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), fleetTxt, dropdownItemStyle))
            {
                FMSFleetManager.Instance?.ToggleFleetVisibility();
            }
            itemY += spacing;

            // 2. Sembunyikan Unit Offline / Dummy [O]
            if (FMSFleetManager.Instance != null)
            {
                bool isHidingOff = FMSFleetManager.Instance.hideOfflineUnits;
                string hideOffTxt = isHidingOff ? "✅ 🚫 Sembunyikan Unit Offline / Dummy [O]" : "⬜ 🚫 Sembunyikan Unit Offline / Dummy [O]";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), hideOffTxt, dropdownItemStyle))
                {
                    FMSFleetManager.Instance.hideOfflineUnits = !FMSFleetManager.Instance.hideOfflineUnits;
                    FMSFleetManager.Instance.ApplyFleetCategoryFilters();
                    ShowNotification($"🚫 Filter Unit Offline: {(FMSFleetManager.Instance.hideOfflineUnits ? "Unit Offline Disembunyikan" : "Semua Unit Ditampilkan")}");
                }
                itemY += spacing;
            }

            // 3. Label Overhead Unit 3D (HUD Tags)
            bool tagOn = showUnitOverheadTags;
            string tagTxt = tagOn ? "✅ 🏷️ Label Overhead Unit 3D [T]" : "⬜ 🏷️ Label Overhead Unit 3D [T]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), tagTxt, dropdownItemStyle))
            {
                showUnitOverheadTags = !showUnitOverheadTags;
                ShowNotification($"🏷️ Label Overhead Unit 3D: {(showUnitOverheadTags ? "Ditampilkan" : "Disembunyikan")}");
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            // SECTION 2: FILTER TIPE ARMADA DENGAN BADGE ANGKA
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "🔍 FILTER TIPE ARMADA (FLEET FILTER)", dropdownHeaderStyle);
            itemY += 20f;

            if (FMSFleetManager.Instance != null)
            {
                var fm = FMSFleetManager.Instance;
                string dtTxt = fm.filterShowHaulTrucks ? "✅ 🚚 Dump Truck / Hauler" : "⬜ 🚚 Dump Truck / Hauler";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), dtTxt, dropdownItemStyle))
                {
                    fm.filterShowHaulTrucks = !fm.filterShowHaulTrucks;
                    fm.ApplyFleetCategoryFilters();
                }
                itemY += spacing;

                int excavatorCount = 0;
                foreach (var unit in fm.activeFleet)
                    if (unit != null && unit.unitType == UnitType.Excavator) excavatorCount++;
                string exTxt = fm.filterShowExcavators
                    ? $"✅ ⛏️ Excavator / Shovel ({excavatorCount})"
                    : $"⬜ ⛏️ Excavator / Shovel ({excavatorCount})";
                if (GUI.Button(new Rect(x + 6, itemY, w - 46, itemH), exTxt, dropdownItemStyle))
                {
                    fm.filterShowExcavators = !fm.filterShowExcavators;
                    fm.ApplyFleetCategoryFilters();
                }
                if (GUI.Button(new Rect(x + w - 36, itemY, 30, itemH),
                    new GUIContent("◎", "Fokus ke excavator dengan GPS terbaru"), dropdownItemStyle))
                {
                    FMSUnitController nearest = null;
                    float bestScore = float.MaxValue;
                    Vector3 cameraPivot = FMSCameraController.Instance != null
                        ? FMSCameraController.Instance.pivotPoint : Vector3.zero;
                    foreach (var unit in fm.activeFleet)
                    {
                        if (unit == null || unit.unitType != UnitType.Excavator || !unit.hasValidGpsFix ||
                            unit.backendLastHeardSeconds > 86400) continue;
                        float score = Vector3.Distance(cameraPivot, unit.transform.position) +
                            (unit.isOnline ? 0f : 100000f) +
                            (unit.backendLastHeardSeconds > 120 ? 10000f : 0f);
                        if (score >= bestScore) continue;
                        bestScore = score;
                        nearest = unit;
                    }
                    if (nearest != null)
                    {
                        fm.filterShowExcavators = true;
                        fm.hideOfflineUnits = false;
                        fm.SetFleetVisibility(true);
                        fm.ApplyFleetCategoryFilters();
                        fm.SelectUnit(nearest);
                        FMSCameraController.Instance?.JumpTo(nearest.transform.position, 260f);
                        showUnitOverheadTags = true;
                        currentMenu = ActiveMenu.None;
                        ShowNotification($"Excavator {nearest.unitId} ditampilkan pada peta");
                    }
                    else ShowNotification("Belum ada excavator dengan GPS valid dalam 24 jam terakhir");
                }
                itemY += spacing;

                string dzTxt = fm.filterShowBulldozers ? "✅ 🚜 Bulldozer" : "⬜ 🚜 Bulldozer";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), dzTxt, dropdownItemStyle))
                {
                    fm.filterShowBulldozers = !fm.filterShowBulldozers;
                    fm.ApplyFleetCategoryFilters();
                }
                itemY += spacing;

                string grTxt = fm.filterShowGraders ? "✅ 🛣️ Motor Grader" : "⬜ 🛣️ Motor Grader";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), grTxt, dropdownItemStyle))
                {
                    fm.filterShowGraders = !fm.filterShowGraders;
                    fm.ApplyFleetCategoryFilters();
                }
                itemY += spacing;

                string wlTxt = fm.filterShowWheelLoaders ? "✅ 🚜 Wheel Loader" : "⬜ 🚜 Wheel Loader";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), wlTxt, dropdownItemStyle))
                {
                    fm.filterShowWheelLoaders = !fm.filterShowWheelLoaders;
                    fm.ApplyFleetCategoryFilters();
                }
                itemY += spacing;

                string ftTxt = fm.filterShowFuelTrucks ? "✅ 🛢️ Fuel / Support Truck" : "⬜ 🛢️ Fuel / Support Truck";
                if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), ftTxt, dropdownItemStyle))
                {
                    fm.filterShowFuelTrucks = !fm.filterShowFuelTrucks;
                    fm.ApplyFleetCategoryFilters();
                }
                itemY += spacing;
            }

            DrawMenuSeparator(x, ref itemY, w);

            // SECTION 3: LAYER SPASIAL TITIK GIS TAMBANG
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "🌐 LAYER SPASIAL TITIK TAMBANG (GIS)", dropdownHeaderStyle);
            itemY += 20f;

            // Master Toggle
            bool allOn = FMSMining3DLayer.Instance != null &&
                FMSMining3DLayer.Instance.showDisposals && FMSMining3DLayer.Instance.showFronts &&
                FMSMining3DLayer.Instance.showCallPoints && FMSMining3DLayer.Instance.showRoads;
            string allTxt = allOn ? "✅ Semua layer peta [V]" : "⬜ Semua layer peta [V]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), allTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleAllMarkers();
            }
            itemY += spacing;

            // Billboard Lokasi
            bool labelOn = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.showLabels;
            string labelTxt = labelOn ? "✅ 🏷️ Label Billboard Lokasi [L]" : "⬜ 🏷️ Label Billboard Lokasi [L]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), labelTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleLabels();
            }
            itemY += spacing;

            // Disposal
            bool dispOn = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.showDisposals;
            string dispTxt = dispOn ? "✅ 🚜 Titik Disposal (Dump Yard)" : "⬜ 🚜 Titik Disposal (Dump Yard)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), dispTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleDisposals();
            }
            itemY += spacing;

            // Front Loading
            bool frontOn = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.showFronts;
            string frontTxt = frontOn ? "✅ ⛏️ Front Loading (Pit Excavation)" : "⬜ ⛏️ Front Loading (Pit Excavation)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), frontTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleFronts();
            }
            itemY += spacing;

            // Simpang CallPoint
            bool cpOn = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.showCallPoints;
            string cpTxt = cpOn ? "✅ 📍 Titik Simpang CallPoint (CP)" : "⬜ 📍 Titik Simpang CallPoint (CP)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), cpTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleCallPoints();
            }
            itemY += spacing;

            // Jalan Hauling
            bool roadOn = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.showRoads;
            string roadTxt = roadOn ? "✅ 🛣️ Jaringan Jalan Hauling [R]" : "⬜ 🛣️ Jaringan Jalan Hauling [R]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), roadTxt, dropdownItemStyle))
            {
                FMSMining3DLayer.Instance?.ToggleRoads();
            }
            itemY += spacing;

            // Kolam Sump
            bool sumpOn = FMSMiningDigitalTwinFX.Instance != null && FMSMiningDigitalTwinFX.Instance.showWaterSumps;
            string sumpTxt = sumpOn ? "✅ 🌊 Kolam Sump & Danau Pit [W]" : "⬜ 🌊 Kolam Sump & Danau Pit [W]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), sumpTxt, dropdownItemStyle))
            {
                FMSMiningDigitalTwinFX.Instance?.ToggleWaterSumps();
            }
            itemY += spacing;

            // Blasting Zone
            bool blastOn = FMSMiningDigitalTwinFX.Instance != null && FMSMiningDigitalTwinFX.Instance.showBlastingZone;
            string blastTxt = blastOn ? "✅ ⚠️ Batas IUP & Blasting Zone [B]" : "⬜ ⚠️ Batas IUP & Blasting Zone [B]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), blastTxt, dropdownItemStyle))
            {
                FMSMiningDigitalTwinFX.Instance?.ToggleBlastingZone();
            }
            itemY += spacing;

            // SECTION 4: HIGH-TECH FILTER MODAL ACTION CTA
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 28), "🎯 <b>Filter & Pencarian Titik Tambang...</b>", navBtnActiveStyle))
            {
                showLocationFilterModal = true;
                currentMenu = ActiveMenu.None;
                if (FMSMining3DLayer.Instance != null && (!FMSMining3DLayer.Instance.isLoaded || FMSMining3DLayer.Instance.spawnedMarkers.Count == 0))
                {
                    StartCoroutine(FMSMining3DLayer.Instance.FetchAllMiningLayers());
                }
            }
        }

        private void DrawSettingsDropdown(float x, float y, float w)
        {
            float h = 485f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            // 1. TEMA TAMPILAN
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "TEMA ANTARMUKA", dropdownHeaderStyle);
            itemY += 20f;

            string t1 = currentTheme == UITheme.CyberCyan ? "✅ Standar" : "⬜ Standar";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), t1, dropdownItemStyle))
            {
                SetTheme(UITheme.CyberCyan);
                currentMenu = ActiveMenu.None;
            }
            itemY += 23f;

            string t2 = currentTheme == UITheme.MiningGold ? "✅ Kontras hangat" : "⬜ Kontras hangat";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), t2, dropdownItemStyle))
            {
                SetTheme(UITheme.MiningGold);
                currentMenu = ActiveMenu.None;
            }
            itemY += 23f;

            string t3 = currentTheme == UITheme.DeepOcean ? "✅ Biru" : "⬜ Biru";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), t3, dropdownItemStyle))
            {
                SetTheme(UITheme.DeepOcean);
                currentMenu = ActiveMenu.None;
            }
            itemY += 23f;

            string t4 = currentTheme == UITheme.TacticalEmerald ? "✅ Hijau" : "⬜ Hijau";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), t4, dropdownItemStyle))
            {
                SetTheme(UITheme.TacticalEmerald);
                currentMenu = ActiveMenu.None;
            }
            itemY += 25f;

            DrawMenuSeparator(x, ref itemY, w);

            // 2. KONFIGURASI TOOLTIP & MODEL ARMADA
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "UNIT & CCTV", dropdownHeaderStyle);
            itemY += 20f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🏷️ Pengaturan Isi Label & Tooltip Unit...", dropdownItemStyle))
            {
                showUnitTagSettingsModal = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🚜 Kelola & Upload Model 3D Unit...", dropdownItemStyle))
            {
                showUnitAssetModal = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📹 Konfigurasi CCTV unit...", dropdownItemStyle))
            {
                EnsureUnitCctvManager();
                FMSUnitController unit = FMSFleetManager.Instance != null
                    ? FMSFleetManager.Instance.selectedUnit : null;
                if (unit != null && FMSUnitCctvManager.Instance != null)
                {
                    FMSUnitCctvManager.Instance.OpenCctv(unit, CctvCameraChannel.CabinDriver);
                    FMSUnitCctvManager.Instance.showUrlConfigPanel = true;
                }
                else ShowNotification("Pilih unit di peta untuk mengatur CCTV.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📏 Kalibrasi Skala Unit Aktual...", dropdownItemStyle))
            {
                showUnitAssetModal = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            // 3. KONEKSI CLOUD GATEWAY
            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "KONEKSI TELEMETRI", dropdownHeaderStyle);
            itemY += 20f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🔄 Hubungkan Ulang Backend API", dropdownItemStyle))
            {
                RetryApiConnection();
                ShowNotification("🔄 Memeriksa kembali endpoint telemetri cloud...");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), "❌ Tutup Pengaturan", dropdownItemStyle))
            {
                currentMenu = ActiveMenu.None;
            }
        }

        private void DrawWindowsDropdown(float x, float y, float w)
        {
            float h = 480f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 24f;
            float spacing = 26f;

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "PANEL, HUD & WIDGET OPERASIONAL", dropdownHeaderStyle);
            itemY += 20f;

            string compTxt = showCompass ? "✅ 🧭 Widget Kompas Mata Angin 3D" : "⬜ 🧭 Widget Kompas Mata Angin 3D";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), compTxt, dropdownItemStyle))
            {
                showCompass = !showCompass;
            }
            itemY += spacing;

            string hudTxt = showCoordHud ? "✅ 📍 Widget Koordinat HUD (UTM 50N)" : "⬜ 📍 Widget Koordinat HUD (UTM 50N)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), hudTxt, dropdownItemStyle))
            {
                showCoordHud = !showCoordHud;
            }
            itemY += spacing;

            string scaleTxt = showScaleBar ? "✅ 📏 Skala Batang Peta GIS (Scale Bar)" : "⬜ 📏 Skala Batang Peta GIS (Scale Bar)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), scaleTxt, dropdownItemStyle))
            {
                showScaleBar = !showScaleBar;
            }
            itemY += spacing;

            string dockTxt = showQuickDock ? "✅ 🕹️ Bilah Akses Cepat Kiri [Tab]" : "⬜ 🕹️ Bilah Akses Cepat Kiri [Tab]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), dockTxt, dropdownItemStyle))
            {
                showQuickDock = !showQuickDock;
            }
            itemY += spacing;

            string kpiTxt = showKpiMiniBar ? "✅ 📊 Widget KPI Produksi Fleet" : "⬜ 📊 Widget KPI Produksi Fleet";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), kpiTxt, dropdownItemStyle))
            {
                showKpiMiniBar = !showKpiMiniBar;
            }
            itemY += spacing;

            string wxTxt = showWeatherWidget ? "✅ Ringkasan kondisi lapangan" : "⬜ Ringkasan kondisi lapangan";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), wxTxt, dropdownItemStyle))
            {
                showWeatherWidget = !showWeatherWidget;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "☁ Isi laporan cuaca & jalan...", dropdownItemStyle))
            {
                OpenWeatherReportModal();
            }
            itemY += spacing;

            EnsureSmartDispatchManager();
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📊 Panel Dispatch & Antrean [Q]", dropdownItemStyle))
            {
                FMSSmartDispatchManager.Instance?.ToggleDispatchHUD();
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "MTC | Pergerakan fleet excavator", dropdownItemStyle))
            {
                EnsureMtcPanel().Open();
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "FMS | Modul operasi", dropdownItemStyle))
            {
                EnsureFmsModulePanel().Open();
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "Inventaris excavator, dozer & grader", dropdownItemStyle))
            {
                OpenFleetInventory();
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            EnsureFtwSaveraManager();
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🩺 Evaluasi K3 FTW SAVERA...", dropdownItemStyle))
            {
                FMSFtwSaveraManager.Instance.isFtwModalOpen = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            string searchTxt = showCommandPalette ? "✅ 🔍 Smart Search (Ctrl+K)" : "⬜ 🔍 Smart Search (Ctrl+K)";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), searchTxt, dropdownItemStyle))
            {
                showCommandPalette = !showCommandPalette;
                if (showCommandPalette)
                {
                    commandPaletteQuery = "";
                    focusCommandInputNextFrame = true;
                }
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            DrawMenuSeparator(x, ref itemY, w);

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "TAMPILAN SISTEM", dropdownHeaderStyle);
            itemY += 20f;

            string fsTxt = Screen.fullScreen ? "🪟 Mode Jendela (Windowed) [F11]" : "🖥️ Layar Penuh (Fullscreen) [F11]";
            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), fsTxt, navBtnActiveStyle))
            {
                Screen.fullScreen = !Screen.fullScreen;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), "❌ Tutup Menu", dropdownItemStyle))
            {
                currentMenu = ActiveMenu.None;
            }
        }

        private void DrawHelpDropdown(float x, float y, float w)
        {
            float h = 180f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, dropdownPanelStyle);

            float itemY = y + 8f;
            float itemH = 26f;
            float spacing = 28f;

            GUI.Label(new Rect(x + 10, itemY, w - 20, 18), "BANTUAN & INFORMASI SISTEM", dropdownHeaderStyle);
            itemY += 22f;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "📖 Panduan Kontrol & Shortcut", dropdownItemStyle))
            {
                showGuideModal = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "🌐 Status & Latensi Telemetri Cloud", dropdownItemStyle))
            {
                ShowNotification(isApiConnected
                    ? $"Telemetri terhubung. Latensi {apiLatencyMs:F0} ms."
                    : "Telemetri belum terhubung.");
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, itemH), "ℹ️ Tentang Virexa One FMS", dropdownItemStyle))
            {
                showAboutModal = true;
                currentMenu = ActiveMenu.None;
            }
            itemY += spacing;

            if (GUI.Button(new Rect(x + 6, itemY, w - 12, 22), "❌ Tutup Menu", dropdownItemStyle))
            {
                currentMenu = ActiveMenu.None;
            }
        }

        public void CycleSunLighting()
        {
            SetSunLighting((sunCycleIndex + 1) % 4);
        }

        public void SetSunLighting(int cycleIndex)
        {
            sunCycleIndex = Mathf.Clamp(cycleIndex, 0, 3);
            EnsureDigitalTwinAtmosphere();
            PlayerPrefs.SetInt("Virexa_TimeOfDay", sunCycleIndex);
            Light mainLight = RenderSettings.sun;
            if (mainLight == null)
            {
                Light[] lights = FindObjectsOfType<Light>();
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional && !l.name.Contains("Compass"))
                    {
                        mainLight = l;
                        break;
                    }
                }
            }

            if (mainLight != null)
            {
                switch (sunCycleIndex)
                {
                    case 0: // Siang Terang
                        mainLight.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                        mainLight.color = new Color(1.0f, 0.98f, 0.95f);
                        mainLight.intensity = 1.35f;
                        RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);
                        ShowNotification("☀️ Pencahayaan: Siang Terang (Clear Day)");
                        break;
                    case 1: // Golden Hour / Sore
                        mainLight.transform.rotation = Quaternion.Euler(18f, -75f, 0f);
                        mainLight.color = new Color(1.0f, 0.72f, 0.42f);
                        mainLight.intensity = 1.25f;
                        RenderSettings.ambientLight = new Color(0.32f, 0.28f, 0.35f);
                        ShowNotification("🌅 Pencahayaan: Sore Hari (Golden Hour)");
                        break;
                    case 2: // Malam / Night Operations
                        mainLight.transform.rotation = Quaternion.Euler(75f, 120f, 0f);
                        mainLight.color = new Color(0.2f, 0.35f, 0.65f);
                        mainLight.intensity = 0.45f;
                        RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.24f);
                        ShowNotification("🌙 Pencahayaan: Malam Hari (Night Shift) • Lampu Sorot Unit Armada Aktif");
                        break;
                    case 3: // Pagi / Dawn
                        mainLight.transform.rotation = Quaternion.Euler(22f, 65f, 0f);
                        mainLight.color = new Color(1.0f, 0.88f, 0.75f);
                        mainLight.intensity = 1.15f;
                        RenderSettings.ambientLight = new Color(0.30f, 0.32f, 0.38f);
                        ShowNotification("🌄 Pencahayaan: Pagi Hari (Dawn Sunrise)");
                        break;
                }
            }

            if (FMSDigitalTwinAtmosphere.Instance != null)
            {
                FMSDigitalTwinAtmosphere.Instance.ApplyAtmosphere(sunCycleIndex);
            }
        }

        private void DrawQuickDock(float h)
        {
            float dockW = 44f;
            float dockH = 444f;
            float dockX = 10f;
            float dockY = 56f;

            GUI.Box(new Rect(dockX, dockY, dockW, dockH), GUIContent.none, cardStyle);

            float btnY = dockY + 6f;
            float btnH = 28f;
            float spacing = 32f;

            // 0. Collapse / Hide Navbar Button [◀]
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, 22), new GUIContent("◀", "<b>◀ Sembunyikan Bilah Menu Kiri</b> <color=#00FFA3>[Tab]</color>\n<color=#B0C8DF>Menutup toolbar navigasi cepat kiri.</color>"), quickDockBtnStyle))
            {
                showQuickDock = false;
                ShowNotification("📌 Bilah menu kiri disembunyikan (Klik tombol [▶] di tepi kiri atau tekan [Tab] untuk membuka)");
            }
            btnY += 26f;

            DrawDockSeparator(dockX, ref btnY, dockW);

            // --- GRUP 1: KAMERA & NAVIGASI CEPAT ---
            // 1. Center Pit [F]
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("🎯", "<b>🎯 Pusatkan Kamera ke Pit</b> <color=#00FFA3>[F / Spasi]</color>\n<color=#B0C8DF>Memusatkan pandangan kamera 3D tepat ke Pit Tambang Utama.</color>"), quickDockBtnStyle))
            {
                FMSCameraController.Instance?.JumpTo(new Vector3(0f, 150f, 0f), 2800f);
                ShowNotification("🎯 Kamera dipusatkan ke Pit Unggul");
            }
            btnY += spacing;

            // 2. North Up [N]
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("🧭", "<b>🧭 Reset Hadap Utara (0°)</b> <color=#00FFA3>[N]</color>\n<color=#B0C8DF>Mereset sudut kompas kamera menghadap Utara murni.</color>"), quickDockBtnStyle))
            {
                FMSCameraController.Instance?.ResetHeadingToNorth();
                ShowNotification("🧭 Orientasi Kamera Menghadap Utara (0°)");
            }
            btnY += spacing;

            // 3. 2D / 3D Mode Toggle [P]
            bool is2D = Camera.main != null && Camera.main.transform.eulerAngles.x > 75f;
            GUIStyle btnStyle2D = is2D ? quickDockBtnActiveStyle : quickDockBtnStyle;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("🗺️", "<b>🗺️ Mode Peta 2D / 3D</b> <color=#00FFA3>[P]</color>\n<color=#B0C8DF>Beralih antara mode Peta Planar 2D (Top-Down) dan Perspektif 3D.</color>"), btnStyle2D))
            {
                if (is2D) FMSCameraController.Instance?.SetIsometricView();
                else FMSCameraController.Instance?.SetTopDownView();
            }
            btnY += spacing;

            DrawDockSeparator(dockX, ref btnY, dockW);

            // --- GRUP 2: PENGUKURAN, ANTREAN & GEOTEKNIK ---
            // 4. Measure Distance [M]
            bool isDist = FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.currentMode == FMSMeasureTool.MeasureMode.Distance;
            GUIStyle distStyle = isDist ? quickDockBtnActiveStyle : quickDockBtnStyle;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("📐", "<b>📐 Alat Ukur Jarak 3D</b> <color=#00FFA3>[M]</color>\n<color=#B0C8DF>Mengukur jarak 3D, beda tinggi, dan kemiringan lereng jalan.</color>"), distStyle))
            {
                EnsureMeasureTool();
                FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.Distance);
                showMeasureToolbar = true;
            }
            btnY += spacing;

            // 5. Elevation Profiler [E]
            bool isProf = FMSElevationProfiler.Instance != null && FMSElevationProfiler.Instance.isProfilerOpen;
            GUIStyle profStyle = isProf ? quickDockBtnActiveStyle : quickDockBtnStyle;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("📈", "<b>📈 Grafik Profil Elevasi</b> <color=#00FFA3>[E]</color>\n<color=#B0C8DF>Menampilkan grafik penampang profil ketinggian lereng / jalan tambang.</color>"), profStyle))
            {
                EnsureElevationProfiler();
                if (FMSElevationProfiler.Instance.isProfilerOpen)
                    FMSElevationProfiler.Instance.CloseProfiler();
                else if (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.points.Count >= 2)
                    FMSElevationProfiler.Instance.GenerateProfileFromPoints(FMSMeasureTool.Instance.points, "Profil Garis Ukur 3D");
                else
                    ShowNotification("ℹ️ Buat garis ukur terlebih dahulu [M] untuk membuat profil elevasi.");
            }
            btnY += spacing;

            // 6. Slope Stability Heatmap [H]
            EnsureSlopeStabilityHeatmap();
            bool isHmOn = FMSSlopeStabilityHeatmap.Instance != null && FMSSlopeStabilityHeatmap.Instance.showHeatmap;
            GUIStyle hmStyle = isHmOn ? quickDockBtnActiveStyle : quickDockBtnStyle;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("⛰️", "Peta kemiringan medan dari elevasi terrain [H]"), hmStyle))
            {
                FMSSlopeStabilityHeatmap.Instance?.ToggleHeatmap();
            }
            btnY += spacing;

            // 6.5 Smart Dispatch & Queue HUD [Q]
            EnsureSmartDispatchManager();
            bool isDispatchOn = FMSSmartDispatchManager.Instance != null && FMSSmartDispatchManager.Instance.isDispatchHudOpen;
            GUIStyle dispStyle = isDispatchOn ? quickDockBtnActiveStyle : quickDockBtnStyle;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("⏱️", "<b>⏱️ Smart Dispatch & Antrean Shovel</b> <color=#00FFA3>[Q]</color>\n<color=#B0C8DF>Monitoring antrean alat muat, Match Factor, dan rekomendasi dispatch.</color>"), dispStyle))
            {
                FMSSmartDispatchManager.Instance?.ToggleDispatchHUD();
            }
            btnY += spacing;

            DrawDockSeparator(dockX, ref btnY, dockW);

            // --- GRUP 3: KONDISI LAPANGAN & PENCARIAN ---
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("☁", "Buka laporan cuaca dan kondisi jalan"), quickDockBtnStyle))
            {
                OpenWeatherReportModal();
            }
            btnY += spacing;

            bool nightSelected = sunCycleIndex == 2;
            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH),
                new GUIContent(nightSelected ? "☀" : "☾", nightSelected
                    ? "Kembali ke pencahayaan siang dan matikan lampu unit"
                    : "Aktifkan mode malam dan lampu unit (T untuk siklus lengkap)"),
                nightSelected ? quickDockBtnActiveStyle : quickDockBtnStyle))
            {
                SetSunLighting(nightSelected ? 0 : 2);
            }
            btnY += spacing;

            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("↻", "Periksa ulang koneksi telemetri dan muat ulang layer peta"), quickDockBtnStyle))
            {
                RetryApiConnection();
                if (FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.isLoaded)
                    StartCoroutine(FMSMining3DLayer.Instance.FetchAllMiningLayers());
                ShowNotification("Memeriksa telemetri dan memuat ulang layer peta...");
            }
            btnY += spacing;

            if (GUI.Button(new Rect(dockX + 6, btnY, 32, btnH), new GUIContent("🔍", "<b>🔍 Smart Command Search</b> <color=#00FFA3>[Ctrl+K]</color>\n<color=#B0C8DF>Pencarian cepat untuk melompat ke unit armada atau titik tambang.</color>"), quickDockBtnStyle))
            {
                showCommandPalette = !showCommandPalette;
                if (showCommandPalette)
                {
                    commandPaletteQuery = "";
                    focusCommandInputNextFrame = true;
                }
            }
        }

        private void DrawQuickDockCollapsedTab(float h)
        {
            float tabW = 28f;
            float tabH = 68f;
            float tabX = 0f;
            float tabY = 120f;

            GUI.Box(new Rect(tabX, tabY, tabW, tabH), GUIContent.none, cardStyle);

            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(tabX + tabW - 2, tabY, 2, tabH), lineAccentTex);
            }

            if (GUI.Button(new Rect(tabX + 2, tabY + 4, tabW - 4, tabH - 8), new GUIContent("▶", "<b>▶ Tampilkan Bilah Menu Kiri</b> <color=#00FFA3>[Tab]</color>\n<color=#B0C8DF>Membuka dan menampilkan kembali toolbar navigasi kiri.</color>"), quickDockBtnStyle))
            {
                showQuickDock = true;
                ShowNotification("📌 Bilah menu kiri ditampilkan");
            }
        }

        private GUIStyle tooltipBoxStyle;
        private GUIStyle tooltipBodyStyle;

        private void DrawFloatingTooltip(string tooltipText)
        {
            if (string.IsNullOrEmpty(tooltipText)) return;

            if (tooltipBodyStyle == null)
            {
                tooltipBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.92f, 0.96f, 1.0f) }
                };
            }

            Vector2 mousePos = Event.current.mousePosition;
            float tooltipW = 290f;

            GUIContent content = new GUIContent(tooltipText);
            float textH = tooltipBodyStyle.CalcHeight(content, tooltipW - 24f);
            float tooltipH = textH + 16f;

            float tooltipX = mousePos.x + 18f;
            float tooltipY = mousePos.y - (tooltipH * 0.35f);

            // Snap cleanly to the right of the left navigation dock
            if (mousePos.x < 75f)
            {
                tooltipX = 58f;
                tooltipY = Mathf.Clamp(mousePos.y - (tooltipH * 0.5f), 54f, Screen.height - tooltipH - 20f);
            }
            else
            {
                if (tooltipX + tooltipW > Screen.width - 12f)
                {
                    tooltipX = mousePos.x - tooltipW - 14f;
                }
                tooltipY = Mathf.Clamp(tooltipY, 52f, Screen.height - tooltipH - 12f);
            }

            // Draw floating card background & border
            GUI.depth = -100;
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipW, tooltipH), GUIContent.none, cardStyle);

            // Glowing cyan left accent bar
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(tooltipX, tooltipY + 2, 3, tooltipH - 4), lineAccentTex);
            }

            // Text content
            GUI.Label(new Rect(tooltipX + 12, tooltipY + 8, tooltipW - 20, textH + 4), tooltipText, tooltipBodyStyle);
        }

        private void DrawKpiMiniBar(float w)
        {
            float kpiW = 390f;
            float kpiH = 70f;
            float compassOffset = showCompass ? 124f : 16f;
            float kpiX = w - kpiW - compassOffset;
            float kpiY = w < 1000f ? 132f : 52f;

            GUI.Box(new Rect(kpiX, kpiY, kpiW, kpiH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(kpiX + 2, kpiY + 2, kpiW - 4, 2), lineAccentTex);
            }

            bool productionAvailable = isApiConnected && hasRealProductionData;
            string badgeMode = productionAvailable ? $"<color=#00FFA3><size=9>SHIFT {realProductionShiftLabel} WITA</size></color>" : "<color=#FFB800><size=9>DATA BELUM TERSEDIA</size></color>";
            string title = $"📊 <b>SNAPSHOT MUATAN FLEET</b>  {badgeMode}";
            GUI.Label(new Rect(kpiX + 14, kpiY + 6, kpiW - 28, 18), title, dropdownHeaderStyle);

            string tonStr = productionAvailable ? (realCurrentPayloadTons >= 1000.0 ? $"{realCurrentPayloadTons / 1000.0:F2} kTon" : $"{realCurrentPayloadTons:F0} Ton") : "--";
            string tripsStr = productionAvailable ? realRecordedLoads.ToString() : "--";
            GUI.Label(new Rect(kpiX + 14, kpiY + 26, kpiW - 28, 18), 
                $"🚛 <b>Muatan aktif:</b> <color=#00FFA3><b>{tonStr}</b></color> <color=#88A0B8>({tripsStr} kali muat)</color>", hintStyle);

            int totalUnits = FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet.Count > 0
                ? FMSFleetManager.Instance.activeFleet.Count
                : realTotalUnits;
            int onlineUnits = FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet.Count > 0
                ? FMSFleetManager.Instance.activeFleet.FindAll(x => x.isOnline).Count
                : apiOnlineUnits;

            string paStr = isApiConnected && hasRealFleetSummaryData && totalUnits > 0 ? $"{realFleetPa:F1}%" : "--";
            string fleetCount = isApiConnected && hasRealFleetSummaryData ? $"{onlineUnits}/{totalUnits}" : "--";
            GUI.Label(new Rect(kpiX + 14, kpiY + 45, kpiW - 28, 18), 
                $"⚡ <b>Armada:</b> <color=#00FFA3><b>{fleetCount}</b></color> <color=#88A0B8>(PA: {paStr})</color>", hintStyle);
        }

        private void DrawWeatherWidget(float w, float h)
        {
            float wxW = Mathf.Min(360f, w - (showQuickDock ? 80f : 32f));
            float wxH = 70f;
            float wxX = showQuickDock ? 64f : 16f;
            float wxY = 52f;

            GUI.Box(new Rect(wxX, wxY, wxW, wxH), GUIContent.none, cardStyle);
            GUI.Label(new Rect(wxX + 12f, wxY + 5f, wxW - 24f, 16f), "CUACA AREA YANG DILIHAT  ·  OPEN-METEO", dropdownHeaderStyle);
            var weather = FMSWeatherController.Instance;
            if (weather != null && weather.IsAvailable)
            {
                var data = weather.Current;
                string condition = data.isRaining ? "Hujan" : data.weatherCode <= 3 ? "Cerah / berawan" : "Berawan / berkabut";
                GUI.Label(new Rect(wxX + 12f, wxY + 24f, wxW - 24f, 18f),
                    $"{data.temperatureC:F1}°C  ·  {condition}  ·  Hujan {data.precipitationMm:F1} mm", hintStyle);
                GUI.Label(new Rect(wxX + 12f, wxY + 44f, wxW - 24f, 18f),
                    $"Model cuaca · {data.latitude:F3}, {data.longitude:F3} · Detail", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(wxX + 12f, wxY + 28f, wxW - 24f, 22f), "Data cuaca area belum tersedia", hintStyle);
            }
            if (GUI.Button(new Rect(wxX, wxY, wxW, wxH), new GUIContent("", "Detail cuaca area yang dilihat"), GUIStyle.none))
                showWeatherDetailsModal = true;
        }

        private void DrawWeatherDetailsModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(470f, screenW - 24f);
            float modalH = Mathf.Min(290f, screenH - 24f);
            float x = (screenW - modalW) * 0.5f;
            float y = (screenH - modalH) * 0.5f;
            GUI.Box(new Rect(0f, 0f, screenW, screenH), GUIContent.none, modalBoxStyle);
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(x, y, modalW, 2f), lineAccentTex);
            GUI.Label(new Rect(x + 20f, y + 16f, modalW - 65f, 22f), "CUACA AREA YANG DILIHAT", dropdownHeaderStyle);
            if (GUI.Button(new Rect(x + modalW - 40f, y + 12f, 26f, 26f), "✕", navBtnStyle))
                showWeatherDetailsModal = false;
            var weather = FMSWeatherController.Instance;
            if (weather == null || !weather.IsAvailable)
            {
                GUI.Label(new Rect(x + 20f, y + 66f, modalW - 40f, 24f), "Data Open-Meteo belum tersedia untuk area ini.", hintStyle);
            }
            else
            {
                var d = weather.Current;
                GUI.Label(new Rect(x + 20f, y + 54f, modalW - 40f, 23f),
                    $"Lokasi model: {d.latitude:F4}°, {d.longitude:F4}°", hintStyle);
                GUI.Label(new Rect(x + 20f, y + 83f, modalW - 40f, 23f),
                    $"Suhu {d.temperatureC:F1}°C  ·  Kelembapan {d.humidityPercent:F0}%", hintStyle);
                GUI.Label(new Rect(x + 20f, y + 112f, modalW - 40f, 23f),
                    $"Hujan {d.rainMm:F1} mm  ·  Pancuran {d.showersMm:F1} mm  ·  Angin {d.windSpeedKmh:F1} km/jam", hintStyle);
                GUI.Label(new Rect(x + 20f, y + 141f, modalW - 40f, 23f),
                    $"Kode cuaca WMO {d.weatherCode}  ·  {(d.isRaining ? "Efek hujan aktif" : "Tidak ada hujan model")}", hintStyle);
                GUI.Label(new Rect(x + 20f, y + 170f, modalW - 40f, 23f),
                    $"Waktu model: {d.modelTimeUtc}  ·  Sumber: Open-Meteo", hintStyle);
                GUI.Label(new Rect(x + 20f, y + 199f, modalW - 40f, 24f),
                    "Prakiraan model, bukan pembacaan sensor cuaca tambang.", hintStyle);
            }
            if (GUI.Button(new Rect(x + 20f, y + modalH - 42f, 170f, 28f), "Laporan operator", navBtnStyle))
            {
                showWeatherDetailsModal = false;
                OpenWeatherReportModal();
            }
            if (GUI.Button(new Rect(x + modalW - 110f, y + modalH - 42f, 90f, 28f), "Tutup", navBtnActiveStyle))
                showWeatherDetailsModal = false;
        }

        private void LoadWeatherReport()
        {
            weatherConditionIndex = Mathf.Clamp(PlayerPrefs.GetInt("Virexa_Weather_Condition", 0), 0, WeatherConditions.Length - 1);
            roadConditionIndex = Mathf.Clamp(PlayerPrefs.GetInt("Virexa_Weather_Road", 0), 0, RoadConditions.Length - 1);
            weatherArea = PlayerPrefs.GetString("Virexa_Weather_Area", "Pit Unggul");
            weatherReporter = PlayerPrefs.GetString("Virexa_Weather_Reporter", "");
            weatherNote = PlayerPrefs.GetString("Virexa_Weather_Note", "");
            long.TryParse(PlayerPrefs.GetString("Virexa_Weather_ReportedAt", "0"), out weatherReportedAtTicks);
            if (weatherReportedAtTicks < DateTime.MinValue.Ticks || weatherReportedAtTicks > DateTime.MaxValue.Ticks)
                weatherReportedAtTicks = 0;
        }

        private void OpenWeatherReportModal()
        {
            draftWeatherCondition = weatherConditionIndex;
            draftRoadCondition = roadConditionIndex;
            draftWeatherArea = weatherArea;
            draftWeatherReporter = weatherReporter;
            draftWeatherNote = weatherNote;
            showWeatherReportModal = true;
            currentMenu = ActiveMenu.None;
        }

        private void SaveWeatherReport()
        {
            if (string.IsNullOrWhiteSpace(draftWeatherArea) || string.IsNullOrWhiteSpace(draftWeatherReporter))
            {
                ShowNotification("Isi area dan nama pelapor sebelum menyimpan.");
                return;
            }
            weatherConditionIndex = draftWeatherCondition;
            roadConditionIndex = draftRoadCondition;
            weatherArea = draftWeatherArea.Trim();
            weatherReporter = draftWeatherReporter.Trim();
            weatherNote = draftWeatherNote.Trim();
            weatherReportedAtTicks = DateTime.UtcNow.Ticks;
            PlayerPrefs.SetInt("Virexa_Weather_Condition", weatherConditionIndex);
            PlayerPrefs.SetInt("Virexa_Weather_Road", roadConditionIndex);
            PlayerPrefs.SetString("Virexa_Weather_Area", weatherArea);
            PlayerPrefs.SetString("Virexa_Weather_Reporter", weatherReporter);
            PlayerPrefs.SetString("Virexa_Weather_Note", weatherNote);
            PlayerPrefs.SetString("Virexa_Weather_ReportedAt", weatherReportedAtTicks.ToString());
            PlayerPrefs.Save();
            showWeatherReportModal = false;
            ShowNotification("Laporan kondisi lapangan tersimpan di perangkat ini.");
        }

        private void DrawWeatherReportModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(520f, screenW - 24f);
            float modalH = Mathf.Min(380f, screenH - 24f);
            float x = (screenW - modalW) * 0.5f;
            float y = (screenH - modalH) * 0.5f;
            float inputX = x + Mathf.Min(140f, modalW * 0.29f);
            float inputW = x + modalW - 40f - inputX;

            GUI.Box(new Rect(0f, 0f, screenW, screenH), GUIContent.none, modalBoxStyle);
            Rect contentRect = new Rect(x + 12f, y + 64f, modalW - 24f, modalH - 118f);
            float scrollContentH = 258f;

            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(x, y, modalW, 2f), lineAccentTex);
            GUI.Label(new Rect(x + 24f, y + 14f, modalW - 70f, 24f), "LAPORAN CUACA & KONDISI JALAN", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 24f, y + 41f, modalW - 48f, 20f),
                "Observasi operator · tersimpan lokal · bukan data sensor langsung", hintStyle);

            weatherReportScroll = GUI.BeginScrollView(contentRect, weatherReportScroll,
                new Rect(0f, 0f, modalW - 40f, scrollContentH));
            float rowX = 12f;
            float fieldX = inputX - contentRect.x;
            float fieldW = inputW;

            GUI.Label(new Rect(rowX, 18f, fieldX - rowX - 6f, 22f), "Area", hintStyle);
            draftWeatherArea = GUI.TextField(new Rect(fieldX, 14f, fieldW, 28f), draftWeatherArea ?? "", 60, searchBoxStyle);
            GUI.Label(new Rect(rowX, 56f, fieldX - rowX - 6f, 22f), "Pelapor", hintStyle);
            draftWeatherReporter = GUI.TextField(new Rect(fieldX, 52f, fieldW, 28f), draftWeatherReporter ?? "", 60, searchBoxStyle);

            GUI.Label(new Rect(rowX, 97f, fieldX - rowX - 6f, 22f), "Cuaca", hintStyle);
            draftWeatherCondition = GUI.Toolbar(new Rect(fieldX, 90f, fieldW, 30f), draftWeatherCondition, WeatherConditions);
            GUI.Label(new Rect(rowX, 141f, fieldX - rowX - 6f, 22f), "Kondisi jalan", hintStyle);
            draftRoadCondition = GUI.Toolbar(new Rect(fieldX, 134f, fieldW, 30f), draftRoadCondition, RoadConditions);

            GUI.Label(new Rect(rowX, 178f, modalW - 48f, 20f), "Catatan lapangan", hintStyle);
            draftWeatherNote = GUI.TextArea(new Rect(rowX, 200f, modalW - 60f, 54f), draftWeatherNote ?? "", 160);
            GUI.EndScrollView();

            float buttonY = y + modalH - 46f;
            if (GUI.Button(new Rect(x + modalW - 246f, buttonY, 104f, 30f), "Batal", navBtnStyle))
                showWeatherReportModal = false;
            if (GUI.Button(new Rect(x + modalW - 132f, buttonY, 108f, 30f), "Simpan", navBtnActiveStyle))
                SaveWeatherReport();
        }

        private void DrawCommandPalette(float screenW, float screenH)
        {
            float palW = 620f;
            float palH = 420f;
            float x = (screenW - palW) / 2f;
            float y = 70f;

            // Semi-transparent backdrop overlay
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);

            // Main Palette Modal Box
            GUI.Box(new Rect(x, y, palW, palH), GUIContent.none, modalBoxStyle);

            // Title & Search Input Field
            GUI.Label(new Rect(x + 16, y + 12, palW - 32, 20), "🔍 <b>PENCARIAN UNIT ARMADA, LOKASI & COMMAND (Ctrl+F / Ctrl+K / F3)</b>", dropdownHeaderStyle);

            if (commandPaletteQuery == null) commandPaletteQuery = "";
            GUIStyle tfStyle = searchBoxStyle ?? GUI.skin?.textField ?? GUIStyle.none;

            try
            {
                GUI.SetNextControlName("CommandPaletteInput");
                commandPaletteQuery = GUI.TextField(new Rect(x + 16, y + 36, palW - 32, 28), commandPaletteQuery, tfStyle);
                if (focusCommandInputNextFrame)
                {
                    GUI.FocusControl("CommandPaletteInput");
                    focusCommandInputNextFrame = false;
                }
            }
            catch (System.Exception)
            {
                commandPaletteQuery = GUI.TextField(new Rect(x + 16, y + 36, palW - 32, 28), commandPaletteQuery ?? "");
            }

            // Results Scrollview
            float listY = y + 70;
            float listH = palH - 110;
            Rect viewRect = new Rect(x + 16, listY, palW - 32, listH);

            List<KeyValuePair<string, Action>> results = new List<KeyValuePair<string, Action>>();

            // 1. Search Active Fleet Units (Prioritas Utama)
            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.activeFleet != null)
            {
                foreach (var unit in FMSFleetManager.Instance.activeFleet)
                {
                    if (unit == null) continue;
                    string uId = unit.unitId ?? "";
                    string uName = unit.unitName ?? "";
                    string opr = unit.operatorName ?? "";
                    string stateStr = unit.currentState.ToString();
                    string typeStr = unit.unitType.ToString();

                    bool match = string.IsNullOrEmpty(commandPaletteQuery) ||
                                 uId.IndexOf(commandPaletteQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 uName.IndexOf(commandPaletteQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 opr.IndexOf(commandPaletteQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 typeStr.IndexOf(commandPaletteQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 stateStr.IndexOf(commandPaletteQuery, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (match)
                    {
                        var targetUnit = unit;
                        string icon = targetUnit.unitType switch
                        {
                            UnitType.HaulTruck => "🚚",
                            UnitType.Excavator => "⛏️",
                            UnitType.Bulldozer => "🚜",
                            UnitType.Grader => "🚜",
                            UnitType.WheelLoader => "🚜",
                            UnitType.FuelTruck => "⛽",
                            _ => "🚛"
                        };

                        string statusText = targetUnit.currentState.ToString().ToUpper();
                        string oprText = !string.IsNullOrEmpty(targetUnit.operatorName) ? $" | Opr: {targetUnit.operatorName}" : "";
                        string speedText = targetUnit.currentSpeedKmh > 0.5f ? $" | {targetUnit.currentSpeedKmh:F0} km/h" : "";
                        string label = $"{icon} <b>[{targetUnit.unitId}]</b> {targetUnit.unitName} — <color=#00FFA3>{statusText}</color>{oprText}{speedText}";

                        results.Add(new KeyValuePair<string, Action>(label, () =>
                        {
                            FMSFleetManager.Instance.SelectUnit(targetUnit);
                            FMSCameraController.Instance?.JumpTo(targetUnit.transform.position, 120f);
                            ShowNotification($"🎯 Mengunci & Melacak Unit [{targetUnit.unitId}] ({targetUnit.unitName}) - Status: {targetUnit.currentState}");
                        }));
                    }
                }
            }

            // 2. Quick Actions & GIS Tools
            if (string.IsNullOrEmpty(commandPaletteQuery) || "grid utm laser graticule".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🌐 Toggle Grid Spasial UTM (500m)", () => { EnsureMiningDigitalTwinFX(); FMSMiningDigitalTwinFX.Instance?.ToggleSpatialGrid(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "sump air pit kolam danau water dewatering".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🌊 Toggle Kolam Sump & Danau Air Pit", () => { EnsureMiningDigitalTwinFX(); FMSMiningDigitalTwinFX.Instance?.ToggleWaterSumps(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "blasting peledakan danger zone geofence iup".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("⚠️ Toggle Zona Bahaya Peledakan & Geofence", () => { EnsureMiningDigitalTwinFX(); FMSMiningDigitalTwinFX.Instance?.ToggleBlastingZone(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "peta kemiringan medan slope terrain".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("Peta kemiringan medan", () => { EnsureSlopeStabilityHeatmap(); FMSSlopeStabilityHeatmap.Instance?.ToggleHeatmap(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "ukur jarak polyline distance".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("📐 Alat Ukur Jarak 3D (Polyline)", () => { EnsureMeasureTool(); FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.Distance); showMeasureToolbar = true; }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "slope kemiringan lereng".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("⛰️ Alat Ukur Kemiringan Lereng (Slope)", () => { EnsureMeasureTool(); FMSMeasureTool.Instance.SetMode(FMSMeasureTool.MeasureMode.SlopeGradient); showMeasureToolbar = true; }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "profil elevasi cross section melintang".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("📈 Buka Grafik Profil Potongan Elevasi Jalan", () => { EnsureElevationProfiler(); if (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.points.Count >= 2) FMSElevationProfiler.Instance.GenerateProfileFromPoints(FMSMeasureTool.Instance.points, "Profil Garis Ukur"); else FMSElevationProfiler.Instance.isProfilerOpen = true; }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "drone inspeksi auto orbit tour".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🚁 Mulai Inspeksi Drone (Cinematic Auto-Orbit)", () => { EnsureDigitalTwinAtmosphere(); FMSDigitalTwinAtmosphere.Instance?.ToggleDroneInspection(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "pusatkan kamera center pit reset".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🎯 Pusatkan Kamera ke Pit Tambang Utama", () => { FMSCameraController.Instance?.JumpTo(new Vector3(0, 150, 0), 2800f); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "north utara hadap 0".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🧭 Hadapkan Kamera ke Utara 0° (North Up)", () => { FMSCameraController.Instance?.ResetHeadingToNorth(); }));
            if (string.IsNullOrEmpty(commandPaletteQuery) || "2d 3d top down peta".Contains(commandPaletteQuery.ToLower()))
                results.Add(new KeyValuePair<string, Action>("🗺️ Ganti Pandangan 2D Top-Down / 3D Isometrik", () => { FMSCameraController.Instance?.SetTopDownView(); }));

            // 3. Mining locations matching query
            if (FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.spawnedMarkers != null)
            {
                foreach (var marker in FMSMining3DLayer.Instance.spawnedMarkers)
                {
                    var loc = marker.data;
                    if (string.IsNullOrEmpty(commandPaletteQuery) || loc.name.ToLower().Contains(commandPaletteQuery.ToLower()) || loc.category.ToLower().Contains(commandPaletteQuery.ToLower()))
                    {
                        var targetMarker = marker;
                        string catIcon = loc.category.ToLower().Contains("front") ? "⛏️" : (loc.category.ToLower().Contains("dump") || loc.category.ToLower().Contains("disposal") ? "🚜" : "📍");
                        results.Add(new KeyValuePair<string, Action>($"{catIcon} [{loc.category.ToUpper()}] {loc.name}", () =>
                        {
                            FMSCameraController.Instance?.JumpTo(targetMarker.worldPos, 400f);
                            ShowNotification($"🎯 Teleportasi ke {targetMarker.data.name}");
                        }));
                    }
                }
            }

            // 4. Mining roads matching query
            if (FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.spawnedRoads != null)
            {
                foreach (var road in FMSMining3DLayer.Instance.spawnedRoads)
                {
                    if (string.IsNullOrEmpty(commandPaletteQuery) || road.routeName.ToLower().Contains(commandPaletteQuery.ToLower()) || road.data.road_id.ToString().Contains(commandPaletteQuery.ToLower()))
                    {
                        var targetRoad = road;
                        results.Add(new KeyValuePair<string, Action>($"🛣️ [JALAN] {targetRoad.routeName} ({targetRoad.data.distance_m}m)", () =>
                        {
                            FMSMining3DLayer.Instance.FocusOnRoad(targetRoad.data.road_id);
                            ShowNotification($"🎯 Fokus ke Jalan {targetRoad.routeName}");
                        }));
                    }
                }
            }

            // Draw scroll list
            float totalContentH = results.Count * 28f;
            commandPaletteScroll = GUI.BeginScrollView(viewRect, commandPaletteScroll, new Rect(0, 0, palW - 55, totalContentH));

            for (int i = 0; i < results.Count; i++)
            {
                var item = results[i];
                if (GUI.Button(new Rect(0, i * 28, palW - 55, 26), item.Key, dropdownItemStyle))
                {
                    item.Value?.Invoke();
                    showCommandPalette = false;
                }
            }

            GUI.EndScrollView();

            // Bottom Footer
            GUI.Label(new Rect(x + 16, y + palH - 30, palW - 120, 20), "💡 <i>Tekan [Esc] untuk menutup | Klik unit / lokasi untuk fokus kamera</i>", hintStyle);
            if (GUI.Button(new Rect(x + palW - 90, y + palH - 32, 74, 24), "Tutup", navBtnStyle))
            {
                showCommandPalette = false;
            }
        }

        private void DrawScaleBarWidget(float w, float h)
        {
            if (Camera.main == null) return;

            float camDist = 2800f;
            if (FMSCameraController.Instance != null)
            {
                camDist = Vector3.Distance(Camera.main.transform.position, FMSCameraController.Instance.pivotPoint);
            }

            float fov = Camera.main.fieldOfView;
            float groundWidth = 2f * camDist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (w / (float)h);
            float metersPerPixel = groundWidth / w;

            // Target roughly 130px on screen
            float targetMeters = metersPerPixel * 130f;

            float[] standardSteps = new float[] { 10f, 25f, 50f, 100f, 200f, 250f, 500f, 1000f, 2000f, 2500f, 5000f, 10000f };
            float chosenMeters = standardSteps[0];
            foreach (float step in standardSteps)
            {
                if (step <= targetMeters * 1.35f) chosenMeters = step;
            }

            float barWidth = chosenMeters / metersPerPixel;
            barWidth = Mathf.Clamp(barWidth, 65f, 200f);

            float basePosX = showQuickDock ? 64f : 16f;
            bool isUnitCardOpen = (FMSFleetManager.Instance != null && FMSFleetManager.Instance.selectedUnit != null);
            float posX = isUnitCardOpen ? (basePosX + 460f + 16f) : basePosX;
            float posY = h - 60f;

            // Background Card
            GUI.Box(new Rect(posX - 8, posY - 18, barWidth + 16, 44), GUIContent.none, cardStyle);

            // Distance label
            string distText = chosenMeters >= 1000f ? $"{chosenMeters / 1000f:F1} km" : $"{chosenMeters:F0} m";
            GUI.Label(new Rect(posX - 8, posY - 16, barWidth + 16, 16), $"<b>{distText}</b>", scaleLabelStyle);

            // Scale bar graphic
            Rect barRect = new Rect(posX, posY + 4, barWidth, 3);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(barRect, lineAccentTex);
                GUI.DrawTexture(new Rect(posX, posY - 1, 2, 8), lineAccentTex);
                GUI.DrawTexture(new Rect(posX + barWidth * 0.5f, posY + 1, 2, 6), lineAccentTex);
                GUI.DrawTexture(new Rect(posX + barWidth - 2, posY - 1, 2, 8), lineAccentTex);
            }

            // Subtitle: Altitude AGL & Pitch
            float altY = Camera.main.transform.position.y;
            float pitch = Camera.main.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            GUI.Label(new Rect(posX - 8, posY + 10, barWidth + 16, 14), $"<color=#8A99AD>ALT:</color> {altY:F0}m | {pitch:F0}°", scaleSubStyle);
        }

        private void DrawSurveyingReticle(float w, float h)
        {
            if (FMSMeasureTool.Instance == null || !FMSMeasureTool.Instance.isToolActive) return;

            Vector2 mPos = Event.current.mousePosition;
            if (mPos.y < 46f || mPos.y > h - 40f || mPos.x < 56f) return;

            // Reticle crosshair (+)
            float rSize = 10f;
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(mPos.x - rSize, mPos.y, rSize * 2, 1), lineAccentTex);
                GUI.DrawTexture(new Rect(mPos.x, mPos.y - rSize, 1, rSize * 2), lineAccentTex);
            }

            // Floating dynamic pill beside cursor
            float pillW = 185f;
            float pillH = 22f;
            float pillX = mPos.x + 14f;
            float pillY = mPos.y - 26f;

            if (pillX + pillW > w - 10f) pillX = mPos.x - pillW - 14f;
            if (pillY < 50f) pillY = mPos.y + 14f;

            GUI.Box(new Rect(pillX, pillY, pillW, pillH), GUIContent.none, cardStyle);

            string modeName = FMSMeasureTool.Instance.currentMode switch
            {
                FMSMeasureTool.MeasureMode.Distance => "📐 JARAK 3D",
                FMSMeasureTool.MeasureMode.SlopeGradient => "⛰️ KEMIRINGAN",
                FMSMeasureTool.MeasureMode.PolygonArea => "⬛ LUAS POLIGON",
                _ => "SURVEY 3D"
            };

            GUI.Label(new Rect(pillX + 6, pillY + 3, pillW - 12, 16),
                $"<b>{modeName}</b> | RL: <color=#00FFA3>{lastElevation:F1}m</color>", reticleStyle);
        }

        private void DrawMeasureToolbarWidget(float screenW, float screenH)
        {
            EnsureMeasureTool();
            var tool = FMSMeasureTool.Instance;
            if (tool == null) return;

            float cardW = 390f;
            float cardH = 230f;
            float cardX = 16f;
            float cardY = screenH - cardH - 68f;

            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);

            // Title
            string modeName = tool.currentMode switch
            {
                FMSMeasureTool.MeasureMode.Distance => "📐 UKUR JARAK 3D (POLYLINE)",
                FMSMeasureTool.MeasureMode.SlopeGradient => "⛰️ UKUR KEMIRINGAN LERENG",
                FMSMeasureTool.MeasureMode.PolygonArea => "⬛ UKUR LUAS AREA (POLYGON)",
                FMSMeasureTool.MeasureMode.PointInspection => "📍 INSPEKSI TITIK SPASIAL",
                _ => "📐 ALAT PENGUKURAN SPASIAL 3D"
            };

            GUI.Label(new Rect(cardX + 12, cardY + 8, cardW - 24, 20), modeName, dropdownHeaderStyle);

            // Tool Switcher Buttons
            float btnY = cardY + 30;
            float btnW = 84f;
            float btnH = 26f;

            GUIStyle distStyle = tool.currentMode == FMSMeasureTool.MeasureMode.Distance ? navBtnActiveStyle : navBtnStyle;
            if (GUI.Button(new Rect(cardX + 12, btnY, btnW, btnH), "📐 Jarak", distStyle))
            {
                tool.SetMode(FMSMeasureTool.MeasureMode.Distance);
            }

            GUIStyle slopeStyle = tool.currentMode == FMSMeasureTool.MeasureMode.SlopeGradient ? navBtnActiveStyle : navBtnStyle;
            if (GUI.Button(new Rect(cardX + 102, btnY, btnW + 8, btnH), "⛰️ Slope", slopeStyle))
            {
                tool.SetMode(FMSMeasureTool.MeasureMode.SlopeGradient);
            }

            GUIStyle areaStyle = tool.currentMode == FMSMeasureTool.MeasureMode.PolygonArea ? navBtnActiveStyle : navBtnStyle;
            if (GUI.Button(new Rect(cardX + 200, btnY, btnW, btnH), "⬛ Luas", areaStyle))
            {
                tool.SetMode(FMSMeasureTool.MeasureMode.PolygonArea);
            }

            if (GUI.Button(new Rect(cardX + 290, btnY, btnW - 4, btnH), "🗑️ Reset", navBtnStyle))
            {
                tool.Clear();
            }

            // Stats Body
            float statY = btnY + 32;
            int ptCount = tool.points.Count;
            GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                $"📍 <b>Titik Terukur:</b> {ptCount} titik {(ptCount < 2 ? "<color=#AAAAAA>(Klik kiri di peta)</color>" : "")}", coordStyle);

            statY += 20;
            if (tool.currentMode == FMSMeasureTool.MeasureMode.PolygonArea)
            {
                GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                    $"⬛ <b>Luas Area:</b> <color=#00FFA3>{tool.polygonAreaHectares:F3} Ha</color> ({tool.polygonAreaSquareMeters:N0} m²)", coordStyle);
                statY += 20;
                GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                    $"📏 <b>Keliling Perimeter:</b> <color=#FFFFFF>{tool.totalDistance3D:F1} m</color>", coordStyle);
            }
            else
            {
                GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                    $"📏 <b>Jarak 3D:</b> <color=#00FFA3>{tool.totalDistance3D:F1} m</color> ({(tool.totalDistance3D / 1000f):F3} km)", coordStyle);
                statY += 20;
                GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                    $"📐 <b>Jarak Datar 2D:</b> <color=#FFFFFF>{tool.totalDistance2D:F1} m</color> | <b>Beda Tinggi ΔZ:</b> <color=#00E5FF>{tool.deltaElevation:+0.0;-0.0;0.0} m</color>", coordStyle);
                statY += 20;
                GUI.Label(new Rect(cardX + 12, statY, cardW - 24, 18), 
                    $"⛰️ <b>Kemiringan:</b> <color=#FFD700>{tool.averageSlopePercent:F1}%</color> ({tool.averageSlopeDegree:F1}°) | {tool.GetSlopeGradeSafetyStatus()}", coordStyle);
            }

            // Action Buttons: Profile, Copy & Close
            float btmY = cardY + cardH - 34;

            if (GUI.Button(new Rect(cardX + 12, btmY, 125, 26), "📈 Profil Elevasi", navBtnActiveStyle))
            {
                EnsureElevationProfiler();
                if (tool.points.Count >= 2)
                {
                    FMSElevationProfiler.Instance.GenerateProfileFromPoints(tool.points, "Profil Garis Ukur 3D");
                }
                else
                {
                    ShowNotification("⚠️ Tambahkan minimal 2 titik untuk membuat grafik profil elevasi.");
                }
            }

            if (GUI.Button(new Rect(cardX + 142, btmY, 115, 26), "📋 Salin", navBtnStyle))
            {
                string report = $"[VIREXAONE GIS MEASUREMENT REPORT]\n" +
                                $"Mode: {tool.currentMode}\n" +
                                $"Titik: {tool.points.Count}\n" +
                                $"Jarak 3D: {tool.totalDistance3D:F2} m ({tool.totalDistance3D/1000f:F3} km)\n" +
                                $"Jarak 2D: {tool.totalDistance2D:F2} m\n" +
                                $"Beda Tinggi (ΔZ): {tool.deltaElevation:+0.0;-0.0;0.0} m\n" +
                                $"Kemiringan (Slope): {tool.averageSlopePercent:F2}% ({tool.averageSlopeDegree:F2}°)\n" +
                                $"Luas Area: {tool.polygonAreaHectares:F3} Ha ({tool.polygonAreaSquareMeters:F2} m²)\n" +
                                $"Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                GUIUtility.systemCopyBuffer = report;
                ShowNotification("📋 Hasil pengukuran disalin ke clipboard!");
            }

            if (GUI.Button(new Rect(cardX + 262, btmY, 115, 26), "❌ Tutup", navBtnStyle))
            {
                tool.SetMode(FMSMeasureTool.MeasureMode.None);
                showMeasureToolbar = false;
            }
        }

        private void DrawLocationFilterModal(float screenW, float screenH)
        {
            float modalW = 700f;
            float modalH = 540f;
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, modalBoxStyle);

            // 1. Header
            GUI.Label(new Rect(x + 20, y + 16, modalW - 100, 26), "🎯 <b>PILIH & FILTER TITIK LOKASI SPASIAL</b>", brandLogoStyle);

            if (GUI.Button(new Rect(x + modalW - 40, y + 16, 26, 26), "✕", navBtnStyle))
            {
                showLocationFilterModal = false;
            }

            // 2. Search Box
            GUI.Label(new Rect(x + 20, y + 52, 60, 24), "🔍 <b>Cari:</b>", hintStyle);
            locationSearchQuery = GUI.TextField(new Rect(x + 75, y + 50, 320, 26), locationSearchQuery, searchBoxStyle);

            if (!string.IsNullOrEmpty(locationSearchQuery))
            {
                if (GUI.Button(new Rect(x + 400, y + 50, 30, 26), "✕", navBtnStyle))
                {
                    locationSearchQuery = "";
                }
            }

            // Quick Actions: Select All / Deselect All
            if (GUI.Button(new Rect(x + 445, y + 50, 115, 26), "✅ Pilih Semua", tabBtnStyle))
            {
                if (selectedCategoryTab == "🛣️ Jalan Hauling")
                {
                    FMSMining3DLayer.Instance?.SelectAllRoads(true);
                    ShowNotification("✅ Semua jalur jalan diaktifkan");
                }
                else
                {
                    FMSMining3DLayer.Instance?.SelectAllIndividualMarkers(true);
                    ShowNotification("✅ Semua titik diaktifkan");
                }
            }

            if (GUI.Button(new Rect(x + 566, y + 50, 115, 26), "⬜ Sembunyikan", tabBtnStyle))
            {
                if (selectedCategoryTab == "🛣️ Jalan Hauling")
                {
                    FMSMining3DLayer.Instance?.SelectAllRoads(false);
                    ShowNotification("⬜ Semua jalur jalan disembunyikan");
                }
                else
                {
                    FMSMining3DLayer.Instance?.SelectAllIndividualMarkers(false);
                    ShowNotification("⬜ Semua titik disembunyikan");
                }
            }

            // 3. Category Tabs
            float tabY = y + 84;
            string[] tabs = new string[] { "Semua Titik", "Disposal (Dump)", "Front Loading", "Simpang (CP)", "🛣️ Jalan Hauling" };
            float tabW = (modalW - 40) / tabs.Length;

            for (int i = 0; i < tabs.Length; i++)
            {
                bool isTabActive = selectedCategoryTab == tabs[i];
                GUIStyle tStyle = isTabActive ? tabBtnActiveStyle : tabBtnStyle;

                if (GUI.Button(new Rect(x + 20 + i * tabW, tabY, tabW - 4, 28), tabs[i], tStyle))
                {
                    selectedCategoryTab = tabs[i];
                }
            }

            // 4. Scrollable List of Items (Points or Roads)
            float listY = tabY + 34;
            float listH = modalH - 180;
            Rect scrollAreaRect = new Rect(x + 20, listY, modalW - 40, listH);

            int visibleCount = 0;
            int totalFiltered = 0;

            if (selectedCategoryTab == "🛣️ Jalan Hauling")
            {
                var roads = FMSMining3DLayer.Instance != null ? FMSMining3DLayer.Instance.spawnedRoads : null;
                if (roads != null && roads.Count > 0)
                {
                    var filteredRoads = new System.Collections.Generic.List<FMSMining3DLayer.RoadEntry>();
                    foreach (var r in roads)
                    {
                        if (!string.IsNullOrEmpty(locationSearchQuery))
                        {
                            string q = locationSearchQuery.ToLower();
                            if (!r.routeName.ToLower().Contains(q) && !r.data.road_id.ToString().Contains(q)) continue;
                        }
                        filteredRoads.Add(r);
                    }

                    totalFiltered = filteredRoads.Count;
                    float contentHeight = filteredRoads.Count * 38f + 10f;
                    Rect viewRect = new Rect(0, 0, modalW - 65, Mathf.Max(listH, contentHeight));

                    locationScrollPos = GUI.BeginScrollView(scrollAreaRect, locationScrollPos, viewRect);

                    float itemY = 4f;
                    foreach (var r in filteredRoads)
                    {
                        bool isVisible = FMSMining3DLayer.Instance.IsRoadVisible(r.data.road_id);
                        if (isVisible) visibleCount++;

                        Texture2D rowTex = isVisible ? rowRoadActiveTex : rowInactiveTex;
                        if (rowTex != null) GUI.DrawTexture(new Rect(4, itemY, viewRect.width - 8, 34), rowTex);

                        // Entire row click to toggle
                        if (GUI.Button(new Rect(4, itemY, viewRect.width - 130, 34), GUIContent.none, GUIStyle.none))
                        {
                            FMSMining3DLayer.Instance.SetRoadVisible(r.data.road_id, !isVisible);
                        }

                        string checkIcon = isVisible ? "☑️" : "⬜";
                        GUI.Label(new Rect(10, itemY + 6, 26, 22), checkIcon);

                        GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 11,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = new Color(1.0f, 0.80f, 0.15f) }
                        };
                        GUI.Label(new Rect(44, itemY + 7, 85, 20), $"🛣️ [#{r.data.road_id}]", tagStyle);

                        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = isVisible ? Color.white : new Color(0.6f, 0.65f, 0.7f) }
                        };
                        GUI.Label(new Rect(135, itemY + 7, 340, 20), $"{r.routeName} ({r.data.distance_m}m)", nameStyle);

                        if (GUI.Button(new Rect(viewRect.width - 120, itemY + 4, 110, 26), "🎯 Fokus Jalan", navBtnStyle))
                        {
                            FMSMining3DLayer.Instance.FocusOnRoad(r.data.road_id);
                        }

                        itemY += 38f;
                    }

                    GUI.EndScrollView();
                }
                else
                {
                    GUI.Label(new Rect(x + 30, listY + 30, modalW - 60, 30), "Memuat data jaringan jalan dari API...", hintStyle);
                }
            }
            else
            {
                var markers = FMSMining3DLayer.Instance != null ? FMSMining3DLayer.Instance.spawnedMarkers : null;
                if (markers != null && markers.Count > 0)
                {
                    var filtered = new System.Collections.Generic.List<FMSMining3DLayer.MarkerEntry>();
                    foreach (var m in markers)
                    {
                        if (selectedCategoryTab == "Disposal (Dump)" && m.data.category != "Disposal") continue;
                        if (selectedCategoryTab == "Front Loading" && m.data.category != "Front") continue;
                        if (selectedCategoryTab == "Simpang (CP)" && m.data.category != "Simpang") continue;

                        if (!string.IsNullOrEmpty(locationSearchQuery))
                        {
                            if (!m.data.name.ToLower().Contains(locationSearchQuery.ToLower())) continue;
                        }

                        filtered.Add(m);
                    }

                    totalFiltered = filtered.Count;
                    float contentHeight = filtered.Count * 38f + 10f;
                    Rect viewRect = new Rect(0, 0, modalW - 65, Mathf.Max(listH, contentHeight));

                    locationScrollPos = GUI.BeginScrollView(scrollAreaRect, locationScrollPos, viewRect);

                    float itemY = 4f;
                    foreach (var m in filtered)
                    {
                        bool isVisible = FMSMining3DLayer.Instance.IsMarkerVisible(m.data.location_id);
                        if (isVisible) visibleCount++;

                        Texture2D rowTex = isVisible ? rowActiveTex : rowInactiveTex;
                        if (rowTex != null) GUI.DrawTexture(new Rect(4, itemY, viewRect.width - 8, 34), rowTex);

                        // Entire row click to toggle
                        if (GUI.Button(new Rect(4, itemY, viewRect.width - 130, 34), GUIContent.none, GUIStyle.none))
                        {
                            FMSMining3DLayer.Instance.SetMarkerVisible(m.data.location_id, !isVisible);
                        }

                        string checkIcon = isVisible ? "☑️" : "⬜";
                        GUI.Label(new Rect(10, itemY + 6, 26, 22), checkIcon);

                        string catTag = m.data.category == "Disposal" ? "🚜 [DSP]" : (m.data.category == "Front" ? "⛏️ [FL]" : "📍 [CP]");
                        Color tagCol = m.data.category == "Disposal" ? new Color(1.0f, 0.75f, 0.2f) : (m.data.category == "Front" ? Color.cyan : new Color(0.3f, 1.0f, 0.5f));

                        GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 11,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = tagCol }
                        };
                        GUI.Label(new Rect(44, itemY + 7, 70, 20), catTag, tagStyle);

                        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = isVisible ? Color.white : new Color(0.6f, 0.65f, 0.7f) }
                        };
                        GUI.Label(new Rect(118, itemY + 7, 360, 20), m.data.name, nameStyle);

                        if (GUI.Button(new Rect(viewRect.width - 120, itemY + 4, 110, 26), "🎯 Fokus Kamera", navBtnStyle))
                        {
                            FMSMining3DLayer.Instance.FocusOnLocation(m.data.location_id);
                        }

                        itemY += 38f;
                    }

                    GUI.EndScrollView();
                }
                else
                {
                    GUI.Label(new Rect(x + 30, listY + 30, modalW - 60, 30), "Memuat data titik dari API...", hintStyle);
                }
            }

            // 5. Footer Summary & Close Button
            float footerY = y + modalH - 42;
            string targetUnit = selectedCategoryTab == "🛣️ Jalan Hauling" ? "ruas jalan" : "titik";
            GUI.Label(new Rect(x + 24, footerY + 6, 400, 22), 
                $"📊 <b>Status:</b> Menampilkan {visibleCount} dari {totalFiltered} {targetUnit} terpilih.", coordStyle);

            if (GUI.Button(new Rect(x + modalW - 140, footerY, 120, 32), "Tutup", navBtnStyle))
            {
                showLocationFilterModal = false;
            }
        }

        public bool HasBlockingModal => IsBlockingModalOpen();
        public bool IsMapEditorOpen => fmsMapEditorPanel != null && fmsMapEditorPanel.IsOpen;

        private bool IsBlockingModalOpen()
        {
            return showWeatherReportModal || showWeatherDetailsModal || showAboutModal || showGuideModal ||
                   showLocationFilterModal || showUnitAssetModal || showUnitTagSettingsModal ||
                   showUnitProductionModal || showFleetMatrixModal || showFleetInventoryModal || showDispatchRadioModal ||
                   showCommandPalette || (mtcPanel != null && mtcPanel.IsOpen) ||
                   (fmsModulePanel != null && fmsModulePanel.IsOpen) ||
                   (fmsMapEditorPanel != null && fmsMapEditorPanel.IsOpen) ||
                   (FMSUnitCctvManager.Instance != null && FMSUnitCctvManager.Instance.isCctvOpen) ||
                   (FMSFtwSaveraManager.Instance != null && FMSFtwSaveraManager.Instance.isFtwModalOpen) ||
                   (FMSSmartDispatchManager.Instance != null && FMSSmartDispatchManager.Instance.isDispatchHudOpen);
        }

        public bool IsPointerOverUI()
        {
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            float screenW = Screen.width;
            float screenH = Screen.height;

            if (IsBlockingModalOpen()) return true;

            // 1. Top Nav Bar
            if (new Rect(0, 0, screenW, 44f).Contains(mousePos)) return true;

            // 2. Active Dropdown Menus
            if (currentMenu != ActiveMenu.None)
            {
                if (GetActiveDropdownRect(screenW).Contains(mousePos)) return true;
            }

            // 3. Location Filter Modal
            if (showLocationFilterModal)
            {
                float modalW = 700f;
                float modalH = 540f;
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 4. Unit Asset Modal
            if (showUnitAssetModal)
            {
                float modalW = 760f;
                float modalH = 580f;
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 4.5 Unit Tag / Tooltip Settings Modal
            if (showUnitTagSettingsModal)
            {
                float modalW = 720f;
                float modalH = 650f;
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 4.8 Smart Dispatch & Queue HUD Modal
            if (FMSSmartDispatchManager.Instance != null && FMSSmartDispatchManager.Instance.isDispatchHudOpen)
            {
                float modalW = Mathf.Min(840f, screenW - 40f);
                float modalH = Mathf.Min(600f, screenH - 70f);
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f + 16f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 5. Smart Command Palette
            if (showCommandPalette)
            {
                float palW = 540f;
                float palH = 340f;
                float x = (screenW - palW) / 2f;
                float y = 70f;
                if (new Rect(x, y, palW, palH).Contains(mousePos)) return true;
            }

            // 6. Left Quick Dock
            if (showQuickDock)
            {
                if (new Rect(10f, 56f, 44f, 412f).Contains(mousePos)) return true;
            }

            if (showWeatherWidget)
            {
                float wxX = showQuickDock ? 64f : 16f;
                float wxW = Mathf.Min(360f, screenW - (showQuickDock ? 80f : 32f));
                if (new Rect(wxX, 52f, wxW, 70f).Contains(mousePos)) return true;
            }

            // 7. About Modal
            if (showAboutModal)
            {
                float modalW = 460f;
                float modalH = 260f;
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 8. Guide Modal
            if (showGuideModal)
            {
                float modalW = 600f;
                float modalH = 460f;
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 9. Compass Widget
            if (showCompass)
            {
                float compassSize = 96f;
                float posX = screenW - compassSize - 16f;
                float posY = 52f;
                if (new Rect(posX, posY, compassSize, compassSize + 32f).Contains(mousePos)) return true;
            }

            // 9.5 KPI Mini Bar
            if (showKpiMiniBar)
            {
                float kpiW = 390f;
                float kpiH = 70f;
                float compassOffset = showCompass ? 124f : 16f;
                float kpiX = screenW - kpiW - compassOffset;
                float kpiY = screenW < 1000f ? 132f : 52f;
                if (new Rect(kpiX, kpiY, kpiW, kpiH).Contains(mousePos)) return true;
            }

            // 10. Measure Toolbar & Profiler
            if (showMeasureToolbar || (FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.isToolActive))
            {
                float cardW = 390f;
                float cardH = 220f;
                float cardX = 16f;
                float cardY = screenH - cardH - 68f;
                if (new Rect(cardX, cardY, cardW, cardH).Contains(mousePos)) return true;
            }

            if (FMSElevationProfiler.Instance != null && FMSElevationProfiler.Instance.isProfilerOpen)
            {
                float profW = Mathf.Min(820f, screenW - 120f);
                float profH = 250f;
                float profX = (screenW - profW) / 2f;
                float profY = screenH - profH - 65f;
                if (new Rect(profX, profY, profW, profH).Contains(mousePos)) return true;
            }

            // 11. Right-Click Context Menu
            if (FMSFleetManager.Instance != null && FMSFleetManager.Instance.isContextMenuOpen)
            {
                if (lastContextMenuRect.Contains(mousePos)) return true;
            }

            // 12. Unit Production Detail Modal
            if (showUnitProductionModal)
            {
                float modalW = Mathf.Min(700f, screenW - 40f);
                float modalH = Mathf.Min(560f, screenH - 60f);
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 13. Fleet Matrix & All Children Modal
            if (showFleetMatrixModal)
            {
                float modalW = Mathf.Min(960f, screenW - 40f);
                float modalH = Mathf.Min(640f, screenH - 60f);
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 14. Unit Live CCTV Stream Modal / PiP
            if (FMSUnitCctvManager.Instance != null && FMSUnitCctvManager.Instance.isCctvOpen)
            {
                float modalW = Mathf.Min(820f, screenW - 30f);
                float modalH = FMSUnitCctvManager.Instance.showUrlConfigPanel ? Mathf.Min(640f, screenH - 30f) : Mathf.Min(580f, screenH - 30f);
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            // 15. FTW SAVERA Operator Fatigue & Health Modal
            if (FMSFtwSaveraManager.Instance != null && FMSFtwSaveraManager.Instance.isFtwModalOpen)
            {
                float modalW = Mathf.Min(980f, screenW - 40f);
                float modalH = Mathf.Min(650f, screenH - 50f);
                float x = (screenW - modalW) / 2f;
                float y = (screenH - modalH) / 2f;
                if (new Rect(x, y, modalW, modalH).Contains(mousePos)) return true;
            }

            return false;
        }

        public bool IsMouseOverContextMenu()
        {
            if (FMSFleetManager.Instance == null || !FMSFleetManager.Instance.isContextMenuOpen) return false;
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return lastContextMenuRect.Contains(mousePos);
        }

        private void DrawCompassWidget(float screenW)
        {
            if (FMS3DCompass.Instance == null)
            {
                GameObject compassObj = new GameObject("FMS_3D_Compass_System");
                compassObj.AddComponent<FMS3DCompass>();
            }

            float compassSize = 96f;
            float posX = screenW - compassSize - 16f;
            float posY = 52f;
            Rect compassRect = new Rect(posX, posY, compassSize, compassSize + 32f);

            float camYaw = 0f;
            if (FMSCameraController.Instance != null) camYaw = FMSCameraController.Instance.CurrentYaw;
            else if (Camera.main != null) camYaw = Camera.main.transform.eulerAngles.y;

            float heading = (camYaw % 360f + 360f) % 360f;
            string cardinal = GetCardinalDirection(heading);

            GUI.Box(compassRect, GUIContent.none, cardStyle);

            Vector2 center = new Vector2(posX + compassSize / 2f, posY + 8f + compassSize / 2f);

            if (FMS3DCompass.Instance != null && FMS3DCompass.Instance.CompassRenderTexture != null)
            {
                GUI.DrawTexture(new Rect(posX + 4f, posY + 4f, compassSize - 8f, compassSize - 8f), FMS3DCompass.Instance.CompassRenderTexture);
            }
            else
            {
                if (compassDialTex != null)
                {
                    GUI.DrawTexture(new Rect(center.x - 38f, center.y - 38f, 76f, 76f), compassDialTex);
                }

                Matrix4x4 oldMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(-camYaw, center);

                GUI.Label(new Rect(center.x - 15f, center.y - 36f, 30f, 18f), "U", northTextStyle);
                GUI.Label(new Rect(center.x + 20f, center.y - 9f, 18f, 18f), "T", compassLabelStyle);
                GUI.Label(new Rect(center.x - 15f, center.y + 18f, 30f, 18f), "S", compassLabelStyle);
                GUI.Label(new Rect(center.x - 38f, center.y - 9f, 18f, 18f), "B", compassLabelStyle);

                if (compassNeedleTex != null)
                {
                    GUI.DrawTexture(new Rect(center.x - 7f, center.y - 26f, 14f, 52f), compassNeedleTex);
                }

                GUI.matrix = oldMatrix;
            }

            GUI.Label(new Rect(posX, posY + compassSize + 4f, compassSize, 20f), $"🧭 <b>{heading:000}° {cardinal}</b>", compassHeadingStyle);

            if (GUI.Button(new Rect(posX, posY, compassSize, compassSize + 28f), GUIContent.none, GUIStyle.none))
            {
                FMSCameraController.Instance?.ResetHeadingToNorth();
                ShowNotification("🧭 Reset Pandangan Menghadap Utara (0°)");
            }

            if (compassRect.Contains(Event.current.mousePosition))
            {
                GUI.Label(new Rect(posX - 145f, posY + 32f, 140f, 36f), "🧭 <b>Mata Angin 3D</b>\nKlik: Reset Arah Utara", hintStyle);
            }
        }

        private void DrawAboutModal(float screenW, float screenH)
        {
            float modalW = 460f;
            float modalH = 260f;
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, modalBoxStyle);

            GUI.Label(new Rect(x + 20, y + 20, modalW - 40, 26), "⚡ <b>VIREXA<color=#00E5FF>ONE</color></b> - 3D Mining GIS Platform", brandLogoStyle);
            GUI.Label(new Rect(x + 20, y + 50, modalW - 40, 20), "<b>Versi:</b> 2.0.0 (Enterprise Edition)", hintStyle);
            GUI.Label(new Rect(x + 20, y + 74, modalW - 40, 20), "<b>Dataset:</b> GeoTIFF Orthophoto & LiDAR DTM Pit Unggul", hintStyle);
            GUI.Label(new Rect(x + 20, y + 98, modalW - 40, 20), "<b>Koordinat:</b> WGS 84 / UTM Zone 50N", hintStyle);
            GUI.Label(new Rect(x + 20, y + 122, modalW - 40, 20), "<b>Cakupan Area:</b> 5,451.6 m × 4,087.0 m (2,228 Hektar)", hintStyle);
            GUI.Label(new Rect(x + 20, y + 146, modalW - 40, 20), "<b>Engine:</b> Unity 6 High-Performance Terrain GIS", hintStyle);

            if (GUI.Button(new Rect(x + (modalW - 120) / 2, y + 195, 120, 32), "Tutup", navBtnStyle))
            {
                showAboutModal = false;
            }
        }

        private void DrawGuideModal(float screenW, float screenH)
        {
            float modalW = 600f;
            float modalH = 460f;
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, modalBoxStyle);

            GUI.Label(new Rect(x + 20, y + 16, modalW - 40, 24), "📖 <b>PANDUAN KONTROL & KEYBOARD SHORTCUTS</b>", brandLogoStyle);

            float curY = y + 46;

            // 1. Navigasi Kamera & Mouse
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "🎥 <b>NAVIGASI PETA & KAMERA 3D</b>", dropdownHeaderStyle);
            curY += 20;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• 🖱️ <b>Klik Kiri + Drag:</b> Geser Peta (Pan GIS)   |   • 🖱️ <b>Klik Kanan + Drag:</b> Orbit & Rotasi 3D", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• 🎡 <b>Scroll Mouse:</b> Zoom In / Out Halus   |   • ⌨️ <b>W / A / S / D:</b> Geser Peta Keyboard", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[Space] / [F]:</b> Fokus Pit Tambang   |   • ⌨️ <b>[N]:</b> Hadap Utara (North Up 0°)", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[P] / [2]:</b> Toggle Mode 2D Peta Top-Down (90°) / 3D Isometrik", hintStyle);
            curY += 26;

            // 2. Alat Ukur & Analisis Spasial
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "📐 <b>ALAT UKUR, ANALISIS & SEARCH</b>", dropdownHeaderStyle);
            curY += 20;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[Ctrl + K] / [/]:</b> Buka <b>Smart Command & Location Search</b>", coordStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[M]:</b> Aktifkan / Matikan Alat Ukur Jarak 3D (Polyline)", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[E]:</b> Buka Grafik Profil Potongan Elevasi Jalan (Cross-Section)", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• 🖱️ <b>Backspace / Klik Kanan:</b> Batalkan Titik Ukur Terakhir", hintStyle);
            curY += 26;

            // 3. Layer Digital Twin & Visual
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "🛰️ <b>LAYER, ARMADA & DIGITAL TWIN</b>", dropdownHeaderStyle);
            curY += 20;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[Tab]:</b> Bilah Kiri   |   • ⌨️ <b>[U]:</b> Armada 3D   |   • ⌨️ <b>[T]:</b> Label Overhead Unit", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[Q]:</b> Optimasi Antrean & Smart Dispatch   |   • ⌨️ <b>[T]:</b> Label Overhead Unit", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[V]:</b> Tampilkan/Sembunyikan Semua Titik   |   • ⌨️ <b>[L]:</b> Label Titik 3D", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[G]:</b> Grid UTM 500m   |   • ⌨️ <b>[W]:</b> Sump / Kolam Air   |   • ⌨️ <b>[B]:</b> Batas IUP/Blasting", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[H]:</b> Heatmap Lereng   |   • ⌨️ <b>[D]:</b> Drone 360°   |   • ⌨️ <b>[R]:</b> Jalan Hauling", hintStyle);
            curY += 18;
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "• ⌨️ <b>[F11]:</b> Fullscreen   |   • ⌨️ <b>[Esc]:</b> Tutup Menu", hintStyle);
            curY += 28;

            if (GUI.Button(new Rect(x + (modalW - 140) / 2, y + modalH - 42, 140, 30), "Mengerti", navBtnActiveStyle))
            {
                showGuideModal = false;
            }
        }

        private void EnsureMining3DLayer()
        {
            if (FMSMining3DLayer.Instance == null)
            {
                GameObject layerObj = new GameObject("FMS_Mining_3D_Layer_System");
                layerObj.AddComponent<FMSMining3DLayer>();
            }
        }

        public void ShowNotification(string msg)
        {
            notificationMessage = msg;
            notificationTimer = 3.5f;
        }

        private string GetCardinalDirection(float degrees)
        {
            if (degrees >= 337.5f || degrees < 22.5f) return "U";
            if (degrees >= 22.5f && degrees < 67.5f) return "TL";
            if (degrees >= 67.5f && degrees < 112.5f) return "T";
            if (degrees >= 112.5f && degrees < 157.5f) return "TG";
            if (degrees >= 157.5f && degrees < 202.5f) return "S";
            if (degrees >= 202.5f && degrees < 247.5f) return "BD";
            if (degrees >= 247.5f && degrees < 292.5f) return "B";
            return "BL";
        }

        
        private void EnsureUnitAssetManager()
        {
            if (FMSUnitAssetManager.Instance == null)
            {
                GameObject mgrObj = new GameObject("FMS_Unit_Asset_Manager");
                mgrObj.AddComponent<FMSUnitAssetManager>();
            }
        }

        private void DrawUnitAssetModal(float screenW, float screenH)
        {
            EnsureUnitAssetManager();

            float modalW = 760f;
            float modalH = 580f;
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Background Modal Window
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);

            // Top Cyan Accent Bar
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x, y, modalW, 3), lineAccentTex);
            }

            // Title & Subtitle
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };
            GUI.Label(new Rect(x + 20, y + 14, modalW - 40, 24), "🚜 PENGELOLA MODEL UNIT 3D & KALIBRASI UKURAN AKTUAL", titleStyle);

            GUI.Label(new Rect(x + 20, y + 38, modalW - 40, 20),
                "Pilih, ganti, upload file 3D (.GLB, .FBX, .OBJ, .3DS), dan sesuaikan skala proporsional aktual alat berat tambang.", hintStyle);

            // Horizontal Separator
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 20, y + 62, modalW - 40, 1), topHighlightTex);
            }

            // Category Tabs
            float tabY = y + 72;
            var categories = new (FMSUnitAssetManager.UnitCategory cat, string label)[]
            {
                (FMSUnitAssetManager.UnitCategory.HaulerEmpty, "🚚 DT Kosong"),
                (FMSUnitAssetManager.UnitCategory.HaulerLoaded, "🚚 DT Muatan"),
                (FMSUnitAssetManager.UnitCategory.Excavator, "⛏️ Shovel"),
                (FMSUnitAssetManager.UnitCategory.Bulldozer, "🚜 Bulldozer"),
                (FMSUnitAssetManager.UnitCategory.Grader, "🛣️ Grader"),
                (FMSUnitAssetManager.UnitCategory.FuelTruck, "🛢️ Fuel Truck"),
                (FMSUnitAssetManager.UnitCategory.WheelLoader, "🚜 Loader")
            };

            float tabW = (modalW - 40) / categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                bool isTabActive = selectedUnitTab == categories[i].cat;
                GUIStyle tStyle = isTabActive ? tabBtnActiveStyle : tabBtnStyle;

                if (GUI.Button(new Rect(x + 20 + i * tabW, tabY, tabW - 4, 30), categories[i].label, tStyle))
                {
                    selectedUnitTab = categories[i].cat;
                }
            }

            // Active Category Configuration Card
            var cfg = FMSUnitAssetManager.Instance != null ? FMSUnitAssetManager.Instance.GetConfig(selectedUnitTab) : null;
            if (cfg != null)
            {
                float cardY = tabY + 40;
                float cardH = modalH - 185;
                Rect cardRect = new Rect(x + 20, cardY, modalW - 40, cardH);
                GUI.Box(cardRect, GUIContent.none, cardStyle);

                float contentX = cardRect.x + 18;
                float curY = cardRect.y + 14;

                // Category Header Badge
                GUIStyle catHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(contentX, curY, 400, 22), $"{cfg.icon} <b>Kategori:</b> {cfg.displayName}", catHeaderStyle);

                GUIStyle statusTagStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.2f, 0.95f, 0.65f) }
                };
                GUI.Label(new Rect(cardRect.x + cardRect.width - 240, curY, 220, 20), "✅ Format GLB 2.0 PBR Siap", statusTagStyle);

                curY += 28;

                // Machine Spec & Benchmark Reference
                GUIStyle labelBoldStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.95f) }
                };

                GUI.Label(new Rect(contentX, curY, 200, 20), "📌 <b>Model Acuan Aktual:</b>", labelBoldStyle);
                GUI.Label(new Rect(contentX + 180, curY, 350, 20), cfg.realMachineReference, coordStyle);
                curY += 24;

                // Active 3D Model File Path
                GUI.Label(new Rect(contentX, curY, 200, 20), "📁 <b>File Model 3D Aktif:</b>", labelBoldStyle);
                GUIStyle pathStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.85f, 0.92f, 1.0f) }
                };
                GUI.TextField(new Rect(contentX + 180, curY, cardRect.width - 220, 22), cfg.assignedFilePath, pathStyle);
                curY += 32;

                // Real Dimensions Info Panel
                float dimBoxW = cardRect.width - 36;
                float dimBoxH = 68;
                GUI.Box(new Rect(contentX, curY, dimBoxW, dimBoxH), GUIContent.none, dropdownPanelStyle);

                GUI.Label(new Rect(contentX + 12, curY + 6, dimBoxW - 24, 18), "📏 <b>DIMENSI AKTUAL ALAT BERAT DI DUNIA NYATA (STANDAR TAMBANG):</b>", dropdownHeaderStyle);

                string dimText = $"• <b>Panjang:</b> {cfg.actualLengthMeters:F1} m   |   • <b>Lebar:</b> {cfg.actualWidthMeters:F1} m   |   • <b>Tinggi:</b> {cfg.actualHeightMeters:F1} m";
                GUI.Label(new Rect(contentX + 12, curY + 26, dimBoxW - 24, 20), dimText, hintStyle);

                GUI.Label(new Rect(contentX + 12, curY + 44, dimBoxW - 24, 20), "✨ <i>Skala visual 1 Unity Unit = 1 Meter proporsional terhadap kontur & lebar jalan hauling.</i>", hintStyle);
                curY += dimBoxH + 16;

                // Scale Multiplier Slider
                GUI.Label(new Rect(contentX, curY, 180, 20), "🔍 <b>Faktor Pengali Skala:</b>", labelBoldStyle);
                GUI.Label(new Rect(contentX + 180, curY, 80, 20), $"{cfg.scaleMultiplier:F2}x", coordStyle);

                cfg.scaleMultiplier = GUI.HorizontalSlider(new Rect(contentX + 265, curY + 4, 280, 20), cfg.scaleMultiplier, 0.2f, 3.0f);
                cfg.scaleMultiplier = Mathf.Round(cfg.scaleMultiplier * 100f) / 100f;

                if (GUI.Button(new Rect(contentX + 560, curY - 2, 130, 26), "🔄 Reset Skala 1.0x", navBtnStyle))
                {
                    cfg.scaleMultiplier = 1.0f;
                    FMSUnitAssetManager.Instance?.SaveSettings();
                }

                curY += 40;

                // Action Buttons for this category
                if (GUI.Button(new Rect(contentX, curY, 220, 32), "📂 Upload / Ganti File Model...", navBtnActiveStyle))
                {
#if UNITY_EDITOR
                    string selectedFile = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                        $"Pilih Model 3D untuk {cfg.displayName}",
                        @"D:\4. PROJECT\20. Astha\Unit\Asset full GLB",
                        new string[] { "3D Model Files", "glb,gltf,fbx,obj,3ds", "glTF / GLB Binary", "glb,gltf", "Autodesk FBX", "fbx", "Wavefront OBJ", "obj", "3D Studio Mesh", "3ds", "Semua File", "*" }
                    );

                    if (!string.IsNullOrEmpty(selectedFile))
                    {
                        string msg = "Manager unit tidak siap";
                        if (FMSUnitAssetManager.Instance != null && FMSUnitAssetManager.Instance.AssignCustomModel(selectedUnitTab, selectedFile, out msg))
                        {
                            ShowNotification($"✅ {msg}");
                        }
                        else
                        {
                            ShowNotification($"⚠️ {msg}");
                        }
                    }
#else
                    ShowNotification("📂 Silakan taruh file 3D model ke folder Assets/Models/UnitModels/");
#endif
                }

                if (GUI.Button(new Rect(contentX + 230, curY, 240, 32), "⚡ Terapkan Model GLB Resmi", navBtnStyle))
                {
                    cfg.assignedFilePath = $"Assets/Models/UnitModels/{cfg.defaultGlbFile}";
                    FMSUnitAssetManager.Instance?.SaveSettings();
                    ShowNotification($"✅ Model {cfg.displayName} dikembalikan ke '{cfg.defaultGlbFile}'");
                }
            }

            // Modal Bottom Actions
            float footerY = y + modalH - 46;

            if (GUI.Button(new Rect(x + 20, footerY, 260, 32), "⚡ Import Semua 7 Model GLB Otomatis", navBtnActiveStyle))
            {
                FMSUnitAssetManager.Instance?.ApplyAllDefaultGlbModels();
                ShowNotification("✅ Semua 7 Model GLB resmi tambang berhasil diimpor & dikonfigurasi!");
            }

            if (GUI.Button(new Rect(x + 290, footerY, 180, 32), "💾 Simpan Konfigurasi", navBtnStyle))
            {
                FMSUnitAssetManager.Instance?.SaveSettings();
                ShowNotification("💾 Pengaturan Model & Skala Unit Tersimpan.");
            }

            if (GUI.Button(new Rect(x + modalW - 130, footerY, 110, 32), "Tutup", navBtnStyle))
            {
                FMSUnitAssetManager.Instance?.SaveSettings();
                showUnitAssetModal = false;
            }
        }

        private void DrawSelectedUnitTelemetryCard(float screenW, float screenH)
        {
            var unit = FMSFleetManager.Instance?.selectedUnit;
            if (unit == null) return;

            float cardW = 460f;
            float cardH = 378f;
            float cardX = showQuickDock ? 64f : 16f; // Placed right next to Quick Dock
            float cardY = screenH - cardH - 58f; // Positioned right above bottom navigation bar

            // 1. Premium Glassmorphic Card Background & Glowing Border
            if (splashCardBgTex != null)
            {
                GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), splashCardBgTex);
            }
            else
            {
                GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);
            }

            if (splashGlowCyanTex != null)
            {
                GUI.DrawTexture(new Rect(cardX, cardY, cardW, 2), splashGlowCyanTex);
            }
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(cardX, cardY, 1, cardH), topHighlightTex);
                GUI.DrawTexture(new Rect(cardX + cardW - 1, cardY, 1, cardH), topHighlightTex);
                GUI.DrawTexture(new Rect(cardX, cardY + cardH - 1, cardW, 1), topHighlightTex);
            }

            // Top Header: Unit Icon + ID + Model + Close Button
            string icon = unit.unitType switch
            {
                UnitType.HaulTruck => unit.hasActualPayload && unit.payloadTons > 0 ? "🚚" : "🚛",
                UnitType.Excavator => "⛏️",
                UnitType.Bulldozer => "🚜",
                UnitType.Grader => "🛣️",
                UnitType.FuelTruck => "🛢️",
                UnitType.WheelLoader => "🚜",
                _ => "🚚"
            };

            // Clean title without repeating unit names in redundant parentheses
            string cleanTitle = $"{icon} <size=13><b>{unit.unitId}</b></size>  <color=#00E5FF>• {unit.modelName}</color>";
            GUI.Label(new Rect(cardX + 12, cardY + 7, cardW - 48, 22), cleanTitle, coordStyle);

            if (GUI.Button(new Rect(cardX + cardW - 30, cardY + 7, 22, 22), "✕", quickDockBtnStyle))
            {
                FMSFleetManager.Instance.DeselectUnit();
                return;
            }

            // Operator & FTW SAVERA Subtitle
            EnsureFtwSaveraManager();
            var ftw = FMSFtwSaveraManager.Instance?.GetFtwRecord(unit.unitId, unit.operatorName);
            string ftwBadge = ftw != null ? (ftw.status_ftw == FtwStatus.FitToWork ? "<color=#00FFA3>🟢 FIT</color>" : (ftw.status_ftw == FtwStatus.DalamPengawasan ? "<color=#FFB800>🟡 WASPADA</color>" : "<color=#FF4D4D>🔴 UNFIT</color>")) : "FTW --";
            string sleepTxt = ftw != null ? $"💤 <b>{ftw.jam_tidur:F1}j Tidur</b> | <color=#00E5FF>{ftw.NIK}</color>" : unit.category;
            GUI.Label(new Rect(cardX + 12, cardY + 29, cardW - 24, 18), $"👷 <b>{unit.operatorName}</b>   |   {ftwBadge}   |   {sleepTxt}", hintStyle);

            // 1. Diagnostic Root-Cause Box (Sleek Glassmorphic Pill)
            string diagText = unit.GetDiagnosticDescription();
            var stopReason = unit.GetCurrentStoppageReason();
            bool feedStale = stopReason == StoppageReason.ApiFleetFeedStale;
            bool isApiProblem = (stopReason == StoppageReason.ApiGpsStaleStatic || stopReason == StoppageReason.ApiGpsNoFixNull || stopReason == StoppageReason.ApiGpsDeviceOffline);
            
            float diagY = cardY + 49;
            float diagH = 24;
            if (splashSubCardBgTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, diagY, cardW - 20, diagH), splashSubCardBgTex);
            }
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, diagY, cardW - 20, 1), topHighlightTex);
                GUI.DrawTexture(new Rect(cardX + 10, diagY + diagH - 1, cardW - 20, 1), topHighlightTex);
            }

            string diagColor = isApiProblem ? "#FF4D4D" : (unit.currentSpeedKmh > 1.0f ? "#00FFA3" : "#FFB800");
            string diagIcon = isApiProblem || feedStale ? "⚠️" : (unit.currentSpeedKmh > 1.0f ? "🟢" : "🟡");
            GUI.Label(new Rect(cardX + 18, diagY + 3, cardW - 36, 18), $"{diagIcon} <color={diagColor}><b>{diagText}</b></color>", hintStyle);

            // 2. FLEET LOADER & LOADING SOURCE INFO BOX
            float loaderBoxY = cardY + 76;
            float loaderBoxH = 48;
            if (splashSubCardBgTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, loaderBoxY, cardW - 20, loaderBoxH), splashSubCardBgTex);
            }
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, loaderBoxY, cardW - 20, 1), topHighlightTex);
                GUI.DrawTexture(new Rect(cardX + 10, loaderBoxY + loaderBoxH - 1, cardW - 20, 1), topHighlightTex);
            }

            if (unit.isLiveTelemetryControlled)
            {
                UTMCoordinate shown = GeoCoordinateConverter.UnityToUTM(unit.transform.position);
                string positionLabel = GeoCoordinateConverter.IsInsideMappedTerrain(shown.Easting, shown.Northing)
                    ? "Posisi peta" : "Di luar cakupan peta";
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 4, cardW - 36, 18),
                    $"{positionLabel}: E {shown.Easting:F1}  N {shown.Northing:F1}  RL {shown.Elevation:F1}", hintStyle);
                string gpsPosition = unit.hasValidGpsFix
                    ? $"Fix GPS terbaru: E {unit.latestGpsEasting:F1}  N {unit.latestGpsNorthing:F1}  RL {unit.latestGpsElevation:F1}"
                    : "Fix GPS terbaru: tidak tersedia";
                string gpsTooltip = unit.hasValidGpsFix
                    ? $"WGS84 {unit.latestGpsLatitude:F6}, {unit.latestGpsLongitude:F6}"
                    : "Tidak ada koordinat GPS valid";
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 24, cardW - 36, 18),
                    new GUIContent(gpsPosition, gpsTooltip), hintStyle);
            }
            else if (unit.unitType == UnitType.HaulTruck)
            {
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 4, cardW - 36, 18), 
                    $"🚜 <b>Fleet Shovel :</b> <color=#00FFA3>{unit.assignedLoaderId} ({unit.assignedLoaderModel})</color> • {unit.assignedFrontName}", hintStyle);
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 24, cardW - 36, 18), 
                    $"⛏️ <b>Shovel tercatat:</b> <color=#00E5FF>{unit.assignedLoaderId}</color> • <color=#FFB800>{(unit.hasActualHaulData ? unit.recordedLoads.ToString() : "--")} kali muat</color>", hintStyle);
            }
            else if (unit.unitType == UnitType.Excavator)
            {
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 4, cardW - 36, 18), 
                    $"⛏️ <b>Aktivitas tercatat:</b> <color=#00FFA3>{unit.activityName}</color>", hintStyle);
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 24, cardW - 36, 18), 
                    $"🚚 <b>Hauler berpasangan:</b> <color=#00E5FF>{FMSFleetManager.Instance.GetChildHaulers(unit.unitId).Count} unit tercatat</color>", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 4, cardW - 36, 18), 
                    $"🚜 <b>Aktivitas Operasi :</b> <color=#00FFA3>{unit.activityName}</color>", hintStyle);
                GUI.Label(new Rect(cardX + 18, loaderBoxY + 24, cardW - 36, 18), 
                    $"📍 <b>Area Sektor :</b> <color=#88A0B8>--</color>", hintStyle);
            }

            // 3. STRUCTURED LIVE TELEMETRY & PRODUCTION METRICS PANEL
            float teleBoxY = cardY + 127;
            float teleBoxH = 142;
            if (splashSubCardBgTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, teleBoxY, cardW - 20, teleBoxH), splashSubCardBgTex);
            }
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 10, teleBoxY, cardW - 20, 1), topHighlightTex);
                GUI.DrawTexture(new Rect(cardX + 10, teleBoxY + teleBoxH - 1, cardW - 20, 1), topHighlightTex);
            }

            GUI.Label(new Rect(cardX + 18, teleBoxY + 4, cardW - 36, 16), "📊 <b>LIVE TELEMETRI & SIKLUS PRODUKSI:</b>", dropdownHeaderStyle);

            float col1X = cardX + 18;
            float col2X = cardX + 235;
            float r1Y = teleBoxY + 22;
            float r2Y = teleBoxY + 42;
            float r3Y = teleBoxY + 62;
            float r4Y = teleBoxY + 82;

            // Row 1: Speed & Payload (Unit Type Aware)
            string speedCol = unit.currentSpeedKmh > 1.0f ? "#00FFA3" : "#FFB800";
            GUI.Label(new Rect(col1X, r1Y, 205, 18), $"⚡ Kecepatan: <color={speedCol}><b>{unit.currentSpeedKmh:F1} KM/Jam</b></color>", hintStyle);
            
            if (unit.unitType == UnitType.Excavator)
            {
                GUI.Label(new Rect(col2X, r1Y, 205, 18), "⚖️ Bucket aktual: <color=#88A0B8><b>--</b></color>", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(col2X, r1Y, 205, 18), $"⚖️ Muatan: <color=#00E5FF><b>{(unit.hasActualPayload ? $"{unit.payloadTons:F1} Ton" : "--")}</b></color>", hintStyle);
            }

            // Row 2: Fuel & Status
            GUI.Label(new Rect(col1X, r2Y, 205, 18), "⛽ Solar: <color=#88A0B8><b>--</b></color>", hintStyle);
            string stCol = unit.currentState == UnitState.Hauling ? "#00FFA3" : (unit.currentState == UnitState.Loading ? "#FFB800" : "#00E5FF");
            string stateLabel = feedStale ? "Data GPS tertunda"
                : unit.isLiveTelemetryControlled && !string.IsNullOrEmpty(unit.activityName)
                    ? unit.activityName : unit.currentState.ToString();
            GUI.Label(new Rect(col2X, r2Y, 205, 18), $"🚦 Status: <color={stCol}><b>{stateLabel}</b></color>", hintStyle);

            // Row 3: Trips & Total Tonnage
            string tripLabel = unit.unitType == UnitType.Excavator ? "Muat fleet" : "Siklus muat";
            int recordedLoads = unit.recordedLoads;
            bool hasPairedHaulers = unit.unitType == UnitType.Excavator && FMSFleetManager.Instance != null &&
                FMSFleetManager.Instance.GetChildHaulers(unit.unitId).Count > 0;
            bool hasLoadData = unit.unitType == UnitType.Excavator ? hasPairedHaulers : unit.hasActualHaulData;
            if (unit.unitType == UnitType.Excavator && FMSFleetManager.Instance != null)
                FMSFleetManager.Instance.GetAggregateFleetStats(unit.unitId, out recordedLoads, out _, out _, out _, out _, out _);
            GUI.Label(new Rect(col1X, r3Y, 205, 18), $"🔄 {tripLabel}: <color=#00FFA3><b>{(hasLoadData ? recordedLoads.ToString() : "--")}</b></color>", hintStyle);
            
            double totalTonnage = 0.0;
            if (unit.unitType == UnitType.Excavator)
            {
                if (FMSFleetManager.Instance != null)
                {
                    FMSFleetManager.Instance.GetAggregateFleetStats(unit.unitId, out _, out totalTonnage, out _, out _, out _, out _);
                }
                else totalTonnage = 0;
            }
            else
            {
                totalTonnage = unit.hasActualPayload ? unit.payloadTons : 0;
            }
            string tonLabel = unit.unitType == UnitType.Excavator ? "Muatan fleet" : "Muatan kini";
            string tonValue = (unit.unitType == UnitType.Excavator ? hasPairedHaulers : unit.hasActualPayload) ? $"{totalTonnage:N0} Ton" : "--";
            GUI.Label(new Rect(col2X, r3Y, 205, 18), $"📈 {tonLabel}: <color=#00E5FF><b>{tonValue}</b></color>", hintStyle);

            // Row 4: Distance & Elevation
            if (unit.unitType == UnitType.Excavator)
            {
                GUI.Label(new Rect(col1X, r4Y, 205, 18), $"ID tipe: <b>{(unit.backendEquipmentTypeId > 0 ? unit.backendEquipmentTypeId.ToString() : "--")}</b>", hintStyle);
                GUI.Label(new Rect(col2X, r4Y, 205, 18), $"Panjang ref: <b>{(unit.referenceLengthMeters > 0f ? $"{unit.referenceLengthMeters:F1} m" : "--")}</b>", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(col1X, r4Y, 205, 18), $"Lintasan sesi: <color=#00E5FF><b>{unit.totalDistanceKm:F1} km</b></color>", hintStyle);
                GUI.Label(new Rect(col2X, r4Y, 205, 18), $"⛰️ Elevasi terrain: <b>RL {unit.transform.position.y:F1} m</b>", hintStyle);
            }

            // Mini Payload Gauge Bar / Status Footer Line
            float loadPct = unit.hasActualPayload ? Mathf.Clamp01(unit.payloadTons / Mathf.Max(1f, unit.maxPayloadTons)) : 0f;
            float loadBarW = cardW - 36;
            float loadBarY = teleBoxY + 106;
            
            if (splashGlowCyanTex != null)
            {
                GUI.DrawTexture(new Rect(col1X, loadBarY, loadBarW, 2), Texture2D.blackTexture);
                if (loadPct > 0f)
                {
                    GUI.DrawTexture(new Rect(col1X, loadBarY, loadBarW * loadPct, 2), splashGlowCyanTex);
                }
            }

            string fixAge = unit.backendLastHeardSeconds >= 3600
                ? $"{unit.backendLastHeardSeconds / 3600}j {(unit.backendLastHeardSeconds % 3600) / 60}m"
                : $"{unit.backendLastHeardSeconds / 60}m {unit.backendLastHeardSeconds % 60}d";
            string footerMetric = unit.isLiveTelemetryControlled
                ? feedStale
                    ? $"<color=#88A0B8><size=9>Fix GPS terakhir {fixAge} lalu  •  Menunggu pembaruan feed</size></color>"
                    : $"<color=#88A0B8><size=9>Fix GPS {fixAge} lalu  •  Tampilan tertunda {unit.gpsVisualDelaySeconds:F0} dtk</size></color>"
                : "<color=#88A0B8><size=9>Data telemetri belum tersedia</size></color>";
            GUI.Label(new Rect(col1X, loadBarY + 5, loadBarW, 16), footerMetric, hintStyle);

            // 4. ACTION BUTTONS (2 Balanced Rows x 5 Uniform Buttons)
            float btnRow1Y = cardY + 276;
            float btnSpacing = 4f;
            float btnW = (cardW - 20f - (4f * btnSpacing)) / 5f; // Perfectly uniform 85.6px width

            if (GUI.Button(new Rect(cardX + 10 + 0 * (btnW + btnSpacing), btnRow1Y, btnW, 26), "🪟 Kabin", navBtnActiveStyle))
            {
                EnsureUnitCctvManager();
                FMSUnitCctvManager.Instance?.OpenCctv(unit, CctvCameraChannel.CabinDriver);
                ShowNotification($"📹 Membuka CCTV Kabin Supir: {unit.unitId} ({unit.operatorName})");
            }

            if (GUI.Button(new Rect(cardX + 10 + 1 * (btnW + btnSpacing), btnRow1Y, btnW, 26), "🏎️ Chase", navBtnStyle))
            {
                FMSCameraController.Instance?.SetFollowMode(CameraFollowMode.ChaseBehind, unit.transform);
                ShowNotification($"🏎️ Kamera Chase: {unit.unitId}");
            }

            if (GUI.Button(new Rect(cardX + 10 + 2 * (btnW + btnSpacing), btnRow1Y, btnW, 26), "📹 CCTV", navBtnActiveStyle))
            {
                EnsureUnitCctvManager();
                FMSUnitCctvManager.Instance?.OpenCctv(unit, CctvCameraChannel.CabinDriver);
            }

            if (GUI.Button(new Rect(cardX + 10 + 3 * (btnW + btnSpacing), btnRow1Y, btnW, 26), "📊 Produksi", navBtnStyle))
            {
                modalSelectedUnit = unit;
                showUnitProductionModal = true;
            }

            string matrixLoader = unit.unitType == UnitType.Excavator ? unit.unitId : unit.assignedLoaderId;
            if (GUI.Button(new Rect(cardX + 10 + 4 * (btnW + btnSpacing), btnRow1Y, btnW, 26), "🏆 Matrix", navBtnStyle))
            {
                if (!string.IsNullOrEmpty(matrixLoader))
                {
                    matrixLoaderId = matrixLoader;
                    showFleetMatrixModal = true;
                }
                else ShowNotification("Shovel pasangan belum tercatat untuk unit ini");
            }

            // Action Buttons Row 2 (Follow, Focus, FTW, Model, Context Menu)
            float btnRow2Y = cardY + 306;
            bool isFollowing = FMSCameraController.Instance != null && FMSCameraController.Instance.followTarget == unit.transform;
            string followBtnTxt = isFollowing ? "🎥 Lepas" : "🎥 Ikuti";
            GUIStyle followBtnStyle = isFollowing ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(cardX + 10 + 0 * (btnW + btnSpacing), btnRow2Y, btnW, 26), followBtnTxt, followBtnStyle))
            {
                if (isFollowing)
                {
                    FMSCameraController.Instance.followTarget = null;
                    ShowNotification("🎥 Kamera Bebas (Free Orbit)");
                }
                else
                {
                    FMSCameraController.Instance?.SetFollowTarget(unit.transform);
                    ShowNotification($"🎥 Kamera Mengikuti {unit.unitId}");
                }
            }

            if (GUI.Button(new Rect(cardX + 10 + 1 * (btnW + btnSpacing), btnRow2Y, btnW, 26), "🎯 Fokus", navBtnStyle))
            {
                FMSCameraController.Instance?.JumpTo(unit.transform.position, 120f);
                ShowNotification($"🎯 Kamera Fokus ke {unit.unitId}");
            }

            if (GUI.Button(new Rect(cardX + 10 + 2 * (btnW + btnSpacing), btnRow2Y, btnW, 26), "🩺 FTW", navBtnActiveStyle))
            {
                EnsureFtwSaveraManager();
                FMSFtwSaveraManager.Instance.searchFilter = unit.unitId;
                FMSFtwSaveraManager.Instance.isFtwModalOpen = true;
            }

            if (GUI.Button(new Rect(cardX + 10 + 3 * (btnW + btnSpacing), btnRow2Y, btnW, 26), "⚙️ 3D Model", navBtnStyle))
            {
                selectedUnitTab = unit.unitCategory;
                showUnitAssetModal = true;
            }

            if (GUI.Button(new Rect(cardX + 10 + 4 * (btnW + btnSpacing), btnRow2Y, btnW, 26), "⋮ Menu", navBtnActiveStyle))
            {
                FMSFleetManager.Instance?.OpenContextMenu(unit, new Vector2(cardX + 220, screenH - cardY - 80));
            }

            // Action Row 3: Live Talkback / Voice Dispatch Button directly to this unit
            float btnRow3Y = cardY + 338;
            bool isTransmittingToMe = FMSFleetMessenger.Instance != null && FMSFleetMessenger.Instance.isTalkbackActive && 
                                     (FMSFleetMessenger.Instance.talkbackTargetUnit == unit.unitId || FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL");
            
            string tbBtnText = isTransmittingToMe ? $"🔴 BICARA KE KABIN ({unit.unitId}) - ON AIR ({FMSFleetMessenger.Instance.talkbackDuration:F1}s)" : $"🎙️ TALKBACK RADIO KE KABIN {unit.unitId}";
            GUIStyle tbBtnStyle = isTransmittingToMe ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(cardX + 10, btnRow3Y, cardW - 20, 28), tbBtnText, tbBtnStyle))
            {
                if (FMSFleetMessenger.Instance != null)
                {
                    if (!FMSFleetMessenger.Instance.isTalkbackActive)
                    {
                        FMSFleetMessenger.Instance.StartTalkback(unit.unitId);
                    }
                    else
                    {
                        FMSFleetMessenger.Instance.StopTalkback();
                    }
                }
            }
        }

        // =========================================================================
        // 17.5 CAMERA MODE HUD OVERLAY (Cockpit POV, Chase, Bumper)
        // =========================================================================
        private void DrawCameraModeOverlay(float screenW, float screenH)
        {
            if (FMSCameraController.Instance == null || !FMSCameraController.Instance.IsInUnitCameraMode) return;

            float hudW = 560f;
            float hudH = 38f;
            float hudX = (screenW - hudW) / 2f;
            float hudY = 52f;

            GUI.Box(new Rect(hudX, hudY, hudW, hudH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(hudX + 2, hudY + 2, hudW - 4, 2), lineAccentTex);
            }

            string label = FMSCameraController.Instance.ActiveCameraModeLabel;
            var targetUnit = FMSCameraController.Instance.followTarget != null ? FMSCameraController.Instance.followTarget.GetComponent<FMSUnitController>() : null;
            if (targetUnit != null)
            {
                label = $"{label} : <b><color=#00FFA3>{targetUnit.unitId} ({targetUnit.modelName})</color></b> | 👷 {targetUnit.operatorName}";
            }

            GUI.Label(new Rect(hudX + 14, hudY + 9, hudW - 140, 22), label, coordStyle);

            if (GUI.Button(new Rect(hudX + hudW - 120, hudY + 6, 110, 26), "✕ KELUAR [ESC]", tabBtnActiveStyle))
            {
                FMSCameraController.Instance.ExitUnitCamera();
                ShowNotification("🌐 Kembali ke Kamera Bebas");
            }
        }

        // =========================================================================
        // 17.6 RIGHT-CLICK CONTEXT MENU (Comprehensive Unit & Fleet Actions)
        // =========================================================================
        private void DrawUnitContextMenu(float screenW, float screenH)
        {
            if (FMSFleetManager.Instance == null || !FMSFleetManager.Instance.isContextMenuOpen || FMSFleetManager.Instance.contextMenuUnit == null) return;

            var unit = FMSFleetManager.Instance.contextMenuUnit;
            float menuW = 320f;
            float menuH = unit.unitType == UnitType.Excavator ? 495f : 515f;

            Vector2 rawPos = FMSFleetManager.Instance.contextMenuScreenPos;

            float menuX = Mathf.Clamp(rawPos.x, 10f, screenW - menuW - 10f);
            float menuY = Mathf.Clamp(screenH - rawPos.y - 20f, 50f, screenH - menuH - 20f);
            lastContextMenuRect = new Rect(menuX, menuY, menuW, menuH);

            // Outer Modern Glassmorphism Context Box
            GUI.Box(lastContextMenuRect, GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(menuX + 2, menuY + 2, menuW - 4, 2), lineAccentTex);
            }

            // Top Header: Unit Icon + ID + Model + Close Button
            string icon = unit.unitType switch
            {
                UnitType.HaulTruck => "🚚",
                UnitType.Excavator => "⛏️",
                UnitType.Bulldozer => "🚜",
                UnitType.Grader => "🛣️",
                UnitType.FuelTruck => "🛢️",
                _ => "🚜"
            };

            EnsureFtwSaveraManager();
            var ftw = FMSFtwSaveraManager.Instance?.GetFtwRecord(unit.unitId, unit.operatorName);
            string ftwBadge = ftw != null ? (ftw.status_ftw == FtwStatus.FitToWork ? "<color=#00FFA3>🟢 FIT</color>" : (ftw.status_ftw == FtwStatus.DalamPengawasan ? "<color=#FFB800>🟡 WASPADA</color>" : "<color=#FF4D4D>🔴 UNFIT</color>")) : "FTW --";
            string sleepTxt = ftw != null ? $"{ftw.jam_tidur:F1}j Tidur" : "Tidur --";

            GUI.Label(new Rect(menuX + 12, menuY + 8, menuW - 44, 20), 
                $"{icon} <size=13><b>{unit.unitId}</b></size> <color=#00E5FF>({unit.modelName})</color>  <color=#00FFA3>● {unit.currentSpeedKmh:F1} KM/Jam</color>", contextHeaderStyle ?? coordStyle);

            if (GUI.Button(new Rect(menuX + menuW - 28, menuY + 7, 22, 22), "✕", quickDockBtnStyle))
            {
                FMSFleetManager.Instance.CloseContextMenu();
                return;
            }

            // Sub Header: Operator & Health
            GUI.Label(new Rect(menuX + 12, menuY + 28, menuW - 24, 16), 
                $"👷 <b>{unit.operatorName}</b> | {ftwBadge} | 💤 {sleepTxt}", hintStyle);

            float curY = menuY + 48f;

            // -------------------------------------------------------------
            // SECTION 1: KAMERA & MONITORING VIDEO
            // -------------------------------------------------------------
            float sec1H = 68f;
            GUI.Box(new Rect(menuX + 8, curY, menuW - 16, sec1H), GUIContent.none, dropdownPanelStyle);
            GUI.Label(new Rect(menuX + 14, curY + 4, menuW - 28, 16), "🎥 <b>KAMERA & MONITORING:</b>", contextSubHeaderStyle ?? dropdownHeaderStyle);

            float camBtnW = (menuW - 32) / 3f;
            float camBtnY = curY + 22;

            if (GUI.Button(new Rect(menuX + 12, camBtnY, camBtnW, 36), "📹 <b>CCTV Kabin\n(API Stream)</b>", navBtnActiveStyle))
            {
                EnsureUnitCctvManager();
                FMSUnitCctvManager.Instance?.OpenCctv(unit, CctvCameraChannel.CabinDriver);
                FMSFleetManager.Instance.CloseContextMenu();
            }

            if (GUI.Button(new Rect(menuX + 16 + camBtnW, camBtnY, camBtnW, 36), "🏎️ <b>Chase\nFollow (3D)</b>", navBtnStyle))
            {
                FMSCameraController.Instance?.SetFollowMode(CameraFollowMode.ChaseBehind, unit.transform);
                FMSFleetManager.Instance.CloseContextMenu();
                ShowNotification($"🏎️ Kamera Chase: {unit.unitId}");
            }

            if (GUI.Button(new Rect(menuX + 20 + camBtnW * 2, camBtnY, camBtnW, 36), "🎯 <b>Fokus Peta\n(3D Jump)</b>", navBtnStyle))
            {
                FMSCameraController.Instance?.JumpTo(unit.transform.position, 120f);
                FMSFleetManager.Instance.CloseContextMenu();
                ShowNotification($"🎯 Kamera Fokus ke {unit.unitId}");
            }
            curY += sec1H + 6f;

            // -------------------------------------------------------------
            // SECTION 2: KONTROL ISOLASI & FLEET
            // -------------------------------------------------------------
            string loaderInduk = unit.unitType == UnitType.Excavator 
                ? unit.unitId 
                : unit.assignedLoaderId;

            bool isIsolated = FMSFleetManager.Instance.isFleetIsolated && FMSFleetManager.Instance.isolatedLoaderId == loaderInduk;
            bool isHeatmap = FMSFleetManager.Instance.showFleetHeatmap && FMSFleetManager.Instance.heatmapLoaderId == loaderInduk;

            float sec2H = unit.unitType == UnitType.Excavator ? 104f : 124f;
            GUI.Box(new Rect(menuX + 8, curY, menuW - 16, sec2H), GUIContent.none, dropdownPanelStyle);
            GUI.Label(new Rect(menuX + 14, curY + 4, menuW - 28, 16), "🎯 <b>KONTROL FLEET & LINTASAN:</b>", contextSubHeaderStyle ?? dropdownHeaderStyle);

            float fleetY = curY + 22f;

            // Isolasi Button
            string isoTxt = isIsolated ? "🔄 Batal Isolasi (Tampilkan Semua)"
                : string.IsNullOrEmpty(loaderInduk) ? "Shovel belum tercatat" : $"🎯 Isolasi Fleet Shovel ({loaderInduk})";
            GUIStyle isoStyle = isIsolated ? navBtnActiveStyle : (contextItemStyle ?? navBtnStyle);
            if (GUI.Button(new Rect(menuX + 12, fleetY, menuW - 24, 26), isoTxt, isoStyle))
            {
                if (isIsolated) FMSFleetManager.Instance.RestoreAllUnits();
                else if (!string.IsNullOrEmpty(loaderInduk)) FMSFleetManager.Instance.IsolateFleet(loaderInduk);
                else ShowNotification("Shovel pasangan belum tercatat untuk unit ini");
                FMSFleetManager.Instance.CloseContextMenu();
            }
            fleetY += 28f;

            // Heatmap Button
            string hmTxt = string.IsNullOrEmpty(loaderInduk) ? "Shovel belum tercatat"
                : isHeatmap ? $"🔥 Matikan Heatmap ({loaderInduk})" : $"🔥 Aktifkan Heatmap Lintasan ({loaderInduk})";
            GUIStyle hmStyle = isHeatmap ? navBtnActiveStyle : (contextItemStyle ?? navBtnStyle);
            if (GUI.Button(new Rect(menuX + 12, fleetY, menuW - 24, 26), hmTxt, hmStyle))
            {
                if (!string.IsNullOrEmpty(loaderInduk)) FMSFleetManager.Instance.ToggleFleetHeatmap(loaderInduk);
                else ShowNotification("Shovel pasangan belum tercatat untuk unit ini");
                FMSFleetManager.Instance.CloseContextMenu();
            }
            fleetY += 28f;

            // Hide / Show single or fleet
            float halfW = (menuW - 28f) / 2f;
            if (unit.unitType == UnitType.Excavator)
            {
                if (GUI.Button(new Rect(menuX + 12, fleetY, halfW, 24), "🚫 Sembunyikan Fleet", contextItemStyle ?? navBtnStyle))
                {
                    FMSFleetManager.Instance.HideFleet(unit.unitId);
                    FMSFleetManager.Instance.CloseContextMenu();
                }
                if (GUI.Button(new Rect(menuX + 16 + halfW, fleetY, halfW, 24), "🔄 Pulihkan Semua", contextItemStyle ?? navBtnStyle))
                {
                    FMSFleetManager.Instance.RestoreAllUnits();
                    FMSFleetManager.Instance.CloseContextMenu();
                }
            }
            else
            {
                if (GUI.Button(new Rect(menuX + 12, fleetY, halfW, 24), "🚫 Sembunyikan Unit", contextItemStyle ?? navBtnStyle))
                {
                    FMSFleetManager.Instance.HideSingleUnit(unit);
                    FMSFleetManager.Instance.CloseContextMenu();
                }
                if (GUI.Button(new Rect(menuX + 16 + halfW, fleetY, halfW, 24), "🔄 Pulihkan Semua", contextItemStyle ?? navBtnStyle))
                {
                    FMSFleetManager.Instance.RestoreAllUnits();
                    FMSFleetManager.Instance.CloseContextMenu();
                }
            }
            curY += sec2H + 6f;

            // -------------------------------------------------------------
            // SECTION 3: RADIO DISPATCH & TALKBACK SUARA LIVE (CONTROL ROOM -> CABIN)
            // -------------------------------------------------------------
            float secRadioH = 64f;
            GUI.Box(new Rect(menuX + 8, curY, menuW - 16, secRadioH), GUIContent.none, dropdownPanelStyle);
            GUI.Label(new Rect(menuX + 14, curY + 4, menuW - 28, 16), "📻 <b>RADIO DISPATCH & TALKBACK SUARA:</b>", contextSubHeaderStyle ?? dropdownHeaderStyle);

            float radY = curY + 22f;
            bool isTransmittingToThis = FMSFleetMessenger.Instance != null && FMSFleetMessenger.Instance.isTalkbackActive && 
                                        (FMSFleetMessenger.Instance.talkbackTargetUnit == unit.unitId || FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL");
            
            float rHalfW = (menuW - 28f) / 2f;

            // 1. Live Voice Talkback Button
            string talkLabel = isTransmittingToThis ? $"🔴 ON-AIR ({FMSFleetMessenger.Instance.talkbackDuration:F1}s)" : $"🎙️ Talkback ke {unit.unitId}";
            GUIStyle talkStyle = isTransmittingToThis ? navBtnActiveStyle : navBtnActiveStyle;
            if (isTransmittingToThis) GUI.color = new Color(1.0f, 0.35f, 0.35f);
            
            if (GUI.Button(new Rect(menuX + 12, radY, rHalfW, 32), talkLabel, talkStyle))
            {
                if (FMSFleetMessenger.Instance != null)
                {
                    if (isTransmittingToThis)
                    {
                        FMSFleetMessenger.Instance.StopTalkback();
                    }
                    else
                    {
                        FMSFleetMessenger.Instance.StartTalkback(unit.unitId);
                    }
                }
            }
            GUI.color = Color.white;

            // 2. Open Full Radio Messenger Modal
            if (GUI.Button(new Rect(menuX + 16 + rHalfW, radY, rHalfW, 32), "💬 Radio Chat [F9]", navBtnStyle))
            {
                dispatchSelectedUnitTarget = unit.unitId;
                showDispatchRadioModal = true;
                FMSFleetManager.Instance.CloseContextMenu();
            }
            curY += secRadioH + 6f;

            // -------------------------------------------------------------
            // SECTION 4: PRODUKSI, ANALYTICS & FTW
            // -------------------------------------------------------------
            float sec3H = 58f;
            GUI.Box(new Rect(menuX + 8, curY, menuW - 16, sec3H), GUIContent.none, dropdownPanelStyle);
            GUI.Label(new Rect(menuX + 14, curY + 4, menuW - 28, 16), "📊 <b>DATA PRODUKSI & KESEHATAN:</b>", contextSubHeaderStyle ?? dropdownHeaderStyle);

            float dataY = curY + 22f;
            float dataHalfW = (menuW - 28f) / 2f;

            if (GUI.Button(new Rect(menuX + 12, dataY, dataHalfW, 26), "🩺 FTW Savera", navBtnActiveStyle))
            {
                EnsureFtwSaveraManager();
                FMSFtwSaveraManager.Instance.searchFilter = unit.unitId;
                FMSFtwSaveraManager.Instance.isFtwModalOpen = true;
                FMSFleetManager.Instance.CloseContextMenu();
            }

            if (GUI.Button(new Rect(menuX + 16 + dataHalfW, dataY, dataHalfW, 26), "📈 Data Produksi", contextItemStyle ?? navBtnStyle))
            {
                modalSelectedUnit = unit;
                showUnitProductionModal = true;
                FMSFleetManager.Instance.CloseContextMenu();
            }
            curY += sec3H + 6f;

            // -------------------------------------------------------------
            // SECTION 5: INTERAKSI CEPAT UNIT
            // -------------------------------------------------------------
            float actHalfW = (menuW - 28f) / 2f;
            if (GUI.Button(new Rect(menuX + 12, curY, actHalfW, 24), "📢 Klakson Safety", contextItemStyle ?? navBtnStyle))
            {
                unit.TriggerHorn();
            }
            if (GUI.Button(new Rect(menuX + 16 + actHalfW, curY, actHalfW, 24), "💡 Uji Lampu Hazard", contextItemStyle ?? navBtnStyle))
            {
                unit.TriggerHeadlightFlash();
            }
        }

        // =========================================================================
        // 17.7 UNIT PRODUCTION DETAIL MODAL (Single Unit Production & Telemetry)
        // =========================================================================
        private void DrawUnitProductionModal(float screenW, float screenH)
        {
            if (modalSelectedUnit == null) { showUnitProductionModal = false; return; }

            var unit = modalSelectedUnit;
            float modalW = Mathf.Min(740f, screenW - 40f);
            float modalH = Mathf.Min(580f, screenH - 60f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Dark Backdrop
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);

            // Modal Card Box
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, y + 2, modalW - 4, 2), lineAccentTex);
            }

            // Header
            GUI.Label(new Rect(x + 20, y + 16, modalW - 60, 24), 
                $"📊 <b>DETAIL PRODUKSI & TELEMETRI: <color=#00FFA3>{unit.unitId}</color> ({unit.modelName})</b>", brandLogoStyle);

            if (GUI.Button(new Rect(x + modalW - 36, y + 16, 24, 22), "✕", quickDockBtnStyle))
            {
                showUnitProductionModal = false;
            }

            EnsureFtwSaveraManager();
            var ftw = FMSFtwSaveraManager.Instance?.GetFtwRecord(unit.unitId, unit.operatorName);
            string ftwText = ftw != null ? (ftw.status_ftw == FtwStatus.FitToWork ? "<color=#00FFA3>🟢 FIT TO WORK</color>" : (ftw.status_ftw == FtwStatus.DalamPengawasan ? "<color=#FFB800>🟡 DALAM PENGAWASAN</color>" : "<color=#FF4D4D>🔴 UNFIT</color>")) : "--";
            string sleepText = ftw != null ? $"💤 <b>{ftw.jam_tidur:F1} Jam Tidur</b> ({ftw.NIK} | Tensi: {ftw.tensimeter})" : "";

            GUI.Label(new Rect(x + 20, y + 42, modalW - 40, 20), 
                $"👷 Operator: <b>{unit.operatorName}</b> | Status K3 FTW: {ftwText} | {sleepText}", hintStyle);

            float curY = y + 68;

            // 4 KPI GAUGE CARDS (2x2 Grid)
            float colW = (modalW - 52) / 2f;
            float kpiH = 78f;

            // Card 1: Muatan & Tonase
            Rect kpi1Rect = new Rect(x + 20, curY, colW, kpiH);
            GUI.Box(kpi1Rect, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(kpi1Rect.x + 2, kpi1Rect.y + 2, kpi1Rect.width - 4, 1.5f), lineAccentTex);
            
            double totalTons = 0.0;
            if (unit.unitType == UnitType.Excavator)
            {
                GUI.Label(new Rect(x + 30, curY + 6, colW - 20, 18), "⛏️ <b>MUATAN AKTIF FLEET</b>", dropdownHeaderStyle);
                if (FMSFleetManager.Instance != null)
                {
                    FMSFleetManager.Instance.GetAggregateFleetStats(unit.unitId, out _, out totalTons, out _, out _, out _, out _);
                }
                GUI.Label(new Rect(x + 30, curY + 26, colW - 20, 24), $"<color=#00FFA3><size=17><b>{totalTons:N0} Ton</b></size></color>", hintStyle);
                GUI.Label(new Rect(x + 30, curY + 52, colW - 20, 18), "Tonase selesai: -- (belum ada transaksi timbang)", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 30, curY + 6, colW - 20, 18), "⚖️ <b>TONASE & MUATAN AKTIF</b>", dropdownHeaderStyle);
                totalTons = unit.hasActualPayload ? unit.payloadTons : 0;
                GUI.Label(new Rect(x + 30, curY + 26, colW - 20, 24), $"<color=#00FFA3><size=17><b>{(unit.hasActualPayload ? $"{totalTons:F1} Ton" : "--")}</b></size></color>", hintStyle);
                GUI.Label(new Rect(x + 30, curY + 52, colW - 20, 18), "Tonase selesai: -- (belum ada transaksi timbang)", hintStyle);
            }

            // Card 2: Ritase & Target
            Rect kpi2Rect = new Rect(x + 32 + colW, curY, colW, kpiH);
            GUI.Box(kpi2Rect, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(kpi2Rect.x + 2, kpi2Rect.y + 2, kpi2Rect.width - 4, 1.5f), lineAccentTex);
            
            string tripTitle = unit.unitType == UnitType.Excavator ? "🔄 <b>SIKLUS MUAT FLEET</b>" : "🔄 <b>SIKLUS MUAT UNIT</b>";
            int loads = unit.recordedLoads;
            if (unit.unitType == UnitType.Excavator && FMSFleetManager.Instance != null)
                FMSFleetManager.Instance.GetAggregateFleetStats(unit.unitId, out loads, out _, out _, out _, out _, out _);
            GUI.Label(new Rect(x + 42 + colW, curY + 6, colW - 20, 18), tripTitle, dropdownHeaderStyle);
            GUI.Label(new Rect(x + 42 + colW, curY + 26, colW - 20, 24), $"<color=#00E5FF><size=17><b>{(unit.hasActualHaulData || unit.unitType == UnitType.Excavator ? loads.ToString() : "--")} Kali Muat</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 42 + colW, curY + 52, colW - 20, 18), "Ritase selesai: -- (tidak tercatat)", hintStyle);

            curY += kpiH + 10;

            // Card 3: Kecepatan & Jarak Tempuh
            Rect kpi3Rect = new Rect(x + 20, curY, colW, kpiH);
            GUI.Box(kpi3Rect, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(kpi3Rect.x + 2, kpi3Rect.y + 2, kpi3Rect.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 30, curY + 6, colW - 20, 18), "⚡ <b>KECEPATAN & JARAK TEMPUH</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 30, curY + 26, colW - 20, 24), $"<color=#FFB800><size=17><b>{unit.currentSpeedKmh:F1} KM/Jam</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 30, curY + 52, colW - 20, 18), $"Lintasan sesi peta: <color=#00E5FF><b>{unit.totalDistanceKm:F1} km</b></color>", hintStyle);

            // Card 4: Bahan Bakar & Konsumsi
            Rect kpi4Rect = new Rect(x + 32 + colW, curY, colW, kpiH);
            GUI.Box(kpi4Rect, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(kpi4Rect.x + 2, kpi4Rect.y + 2, kpi4Rect.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 42 + colW, curY + 6, colW - 20, 18), "⛽ <b>EFISIENSI BAHAN BAKAR (SOLAR)</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 42 + colW, curY + 26, colW - 20, 24), "<color=#88A0B8><size=17><b>--</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 42 + colW, curY + 52, colW - 20, 18), "Sensor bahan bakar belum tersedia", hintStyle);

            curY += kpiH + 14;

            // FLEET SHOVEL PAIRING & SIKLUS DETAIL
            float cycleBoxH = 142f;
            Rect cycleRect = new Rect(x + 20, curY, modalW - 40, cycleBoxH);
            GUI.Box(cycleRect, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(cycleRect.x + 2, cycleRect.y + 2, cycleRect.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 30, curY + 6, modalW - 60, 18), "🚜 <b>ARMADA FLEET SHOVEL & ANALISIS SIKLUS (HAUL CYCLE BREAKDOWN):</b>", dropdownHeaderStyle);

            if (unit.unitType == UnitType.HaulTruck)
            {
                GUI.Label(new Rect(x + 30, curY + 28, modalW - 60, 18), 
                    $"• <b>Shovel tercatat:</b> <color=#00FFA3>{(string.IsNullOrEmpty(unit.assignedLoaderId) ? "--" : unit.assignedLoaderId)}</color>   |   • <b>Lokasi Front:</b> --", hintStyle);
                GUI.Label(new Rect(x + 30, curY + 48, modalW - 60, 18), 
                    "• <b>Disposal:</b> --   |   • <b>Riwayat bongkar:</b> belum tersedia", hintStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 30, curY + 28, modalW - 60, 18), 
                    $"• <b>Aktivitas Operasi:</b> <color=#00FFA3>{unit.activityName}</color>", hintStyle);
                GUI.Label(new Rect(x + 30, curY + 48, modalW - 60, 18), 
                    $"• <b>Armada terhubung:</b> {(FMSFleetManager.Instance != null ? FMSFleetManager.Instance.GetChildHaulers(unit.unitId).Count : 0)} unit berdasarkan shovel pada tabel hauling", hintStyle);
            }

            // 4 Segmented Cycle Breakdown Tiles
            float stepW = (modalW - 76) / 4f;
            float stepY = curY + 74f;
            float stepH = 54f;

            // Step 1: Loading
            GUI.Box(new Rect(x + 30, stepY, stepW, stepH), GUIContent.none, cardStyle);
            GUI.Label(new Rect(x + 36, stepY + 4, stepW - 12, 16), "1. Shovel Loading", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 36, stepY + 22, stepW - 12, 18), "<color=#88A0B8><b>--</b></color>", hintStyle);
            GUI.Label(new Rect(x + 36, stepY + 36, stepW - 12, 14), "<size=9><color=#88A0B8>Titik Muat Front</color></size>", hintStyle);

            // Step 2: Haul
            GUI.Box(new Rect(x + 34 + stepW, stepY, stepW, stepH), GUIContent.none, cardStyle);
            GUI.Label(new Rect(x + 40 + stepW, stepY + 4, stepW - 12, 16), "2. Haul Muatan", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 40 + stepW, stepY + 22, stepW - 12, 18), "<color=#88A0B8><b>--</b></color>", hintStyle);
            GUI.Label(new Rect(x + 40 + stepW, stepY + 36, stepW - 12, 14), "<size=9><color=#88A0B8>Jalan Hauling Utama</color></size>", hintStyle);

            // Step 3: Dumping
            GUI.Box(new Rect(x + 38 + stepW * 2, stepY, stepW, stepH), GUIContent.none, cardStyle);
            GUI.Label(new Rect(x + 44 + stepW * 2, stepY + 4, stepW - 12, 16), "3. Dumping", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 44 + stepW * 2, stepY + 22, stepW - 12, 18), "<color=#88A0B8><b>--</b></color>", hintStyle);
            GUI.Label(new Rect(x + 44 + stepW * 2, stepY + 36, stepW - 12, 14), "<size=9><color=#88A0B8>Disposal / Hopper</color></size>", hintStyle);

            // Step 4: Return Empty
            GUI.Box(new Rect(x + 42 + stepW * 3, stepY, stepW, stepH), GUIContent.none, cardStyle);
            GUI.Label(new Rect(x + 48 + stepW * 3, stepY + 4, stepW - 12, 16), "4. Return Kosong", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 48 + stepW * 3, stepY + 22, stepW - 12, 18), "<color=#88A0B8><b>--</b></color>", hintStyle);
            GUI.Label(new Rect(x + 48 + stepW * 3, stepY + 36, stepW - 12, 14), "<size=9><color=#88A0B8>Durasi belum ada</color></size>", hintStyle);

            // Modal Footer Buttons
            float btmY = y + modalH - 46;
            if (GUI.Button(new Rect(x + 20, btmY, 130, 32), "🪟 Kabin POV", navBtnActiveStyle))
            {
                FMSCameraController.Instance?.SetFollowMode(CameraFollowMode.CockpitPOV, unit.transform);
                showUnitProductionModal = false;
            }

            if (GUI.Button(new Rect(x + 158, btmY, 120, 32), "📹 Live CCTV", navBtnActiveStyle))
            {
                EnsureUnitCctvManager();
                FMSUnitCctvManager.Instance?.OpenCctv(unit);
                showUnitProductionModal = false;
            }

            if (GUI.Button(new Rect(x + 286, btmY, 130, 32), "🎯 Fokus 3D", navBtnStyle))
            {
                FMSCameraController.Instance?.JumpTo(unit.transform.position, 120f);
                showUnitProductionModal = false;
            }

            if (GUI.Button(new Rect(x + 424, btmY, 150, 32), "🏆 Matrix Fleet", navBtnStyle))
            {
                string loaderId = unit.unitType == UnitType.Excavator ? unit.unitId : unit.assignedLoaderId;
                if (!string.IsNullOrEmpty(loaderId))
                {
                    matrixLoaderId = loaderId;
                    showFleetMatrixModal = true;
                    showUnitProductionModal = false;
                }
                else ShowNotification("Shovel pasangan belum tercatat untuk unit ini");
            }

            if (GUI.Button(new Rect(x + modalW - 110, btmY, 90, 32), "Tutup", navBtnStyle))
            {
                showUnitProductionModal = false;
            }
        }

        private void OpenFleetInventory()
        {
            showFleetInventoryModal = true;
            inventoryCategoryIndex = 0;
            inventoryScroll = Vector2.zero;
            if (!inventoryLoading) StartCoroutine(LoadFleetInventory());
        }

        private System.Collections.IEnumerator LoadFleetInventory()
        {
            inventoryLoading = true;
            inventoryError = null;
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                inventoryError = "Alamat backend belum tersedia.";
                inventoryLoading = false;
                yield break;
            }

            using (var request = UnityEngine.Networking.UnityWebRequest.Get(apiBaseUrl.TrimEnd('/') + "/api/v1/fleet/inventory"))
            {
                FMSApiSession.Authorize(request);
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<InventoryResponseDto>(request.downloadHandler.text);
                        if (response == null || response.status != "success" || response.data == null)
                            throw new FormatException("Respons inventaris tidak lengkap.");
                        inventoryUnits = response.data;
                    }
                    catch (Exception ex)
                    {
                        inventoryError = ex.Message;
                    }
                }
                else
                {
                    inventoryError = "Inventaris tidak dapat dimuat: " + request.responseCode;
                }
            }
            inventoryLoading = false;
        }

        private void DrawFleetInventoryModal(float screenW, float screenH)
        {
            float width = Mathf.Min(850f, screenW - 24f);
            float height = Mathf.Min(630f, screenH - 28f);
            float x = (screenW - width) * 0.5f;
            float y = (screenH - height) * 0.5f;
            bool compact = width < 610f;
            string[] categories = { "Excavator", "Bulldozer", "Grader" };

            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, cardStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(x, y, width, 2f), lineAccentTex);

            GUI.Label(new Rect(x + 18f, y + 15f, width - 110f, 24f), "INVENTARIS UNIT", brandLogoStyle);
            if (GUI.Button(new Rect(x + width - 89f, y + 12f, 72f, 28f), "Tutup", navBtnStyle))
                showFleetInventoryModal = false;

            GUI.Label(new Rect(x + 18f, y + 43f, width - 36f, 34f),
                "Berasal dari tabel equipment. Unit tanpa titik GPS tidak ditempatkan pada peta.", hintStyle);

            float tabWidth = (width - 36f) / 3f;
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                int count = inventoryUnits.FindAll(u => u.category == category).Count;
                string label = (category == "Bulldozer" ? "Dozer" : category) + " (" + count + ")";
                if (GUI.Button(new Rect(x + 18f + i * tabWidth, y + 79f, tabWidth - 5f, 29f),
                    label, inventoryCategoryIndex == i ? tabBtnActiveStyle : tabBtnStyle))
                {
                    inventoryCategoryIndex = i;
                    inventoryScroll = Vector2.zero;
                }
            }

            float toolsY = y + 116f;
            if (GUI.Button(new Rect(x + 18f, toolsY, 88f, 27f), "Muat ulang", navBtnStyle) && !inventoryLoading)
                StartCoroutine(LoadFleetInventory());
            var manager = FMSFleetManager.Instance;
            if (manager != null && GUI.Button(new Rect(x + 116f, toolsY, Mathf.Min(210f, width - 150f), 27f),
                manager.showArchivedUnits ? "Posisi GPS lama: tampil" : "Posisi GPS lama: sembunyi",
                manager.showArchivedUnits ? navBtnActiveStyle : navBtnStyle))
                manager.ToggleShowArchivedUnits();

            if (inventoryLoading || !string.IsNullOrEmpty(inventoryError))
            {
                GUI.Label(new Rect(x + 20f, y + 160f, width - 40f, 30f),
                    inventoryLoading ? "Memuat inventaris..." : inventoryError, hintStyle);
                return;
            }

            List<InventoryUnitDto> visible = inventoryUnits.FindAll(u => u.category == categories[inventoryCategoryIndex]);
            float listY = y + 154f;
            float listHeight = Mathf.Max(80f, height - 205f);
            float rowHeight = compact ? 58f : 42f;
            if (visible.Count == 0)
            {
                GUI.Label(new Rect(x + 20f, listY + 24f, width - 40f, 28f),
                    "Tidak ada unit kategori ini di tabel equipment.", hintStyle);
            }
            else
            {
                Rect scrollRect = new Rect(x + 18f, listY, width - 36f, listHeight);
                Rect contentRect = new Rect(0, 0, scrollRect.width - 18f, visible.Count * rowHeight);
                inventoryScroll = GUI.BeginScrollView(scrollRect, inventoryScroll, contentRect);
                for (int i = 0; i < visible.Count; i++)
                {
                    InventoryUnitDto unit = visible[i];
                    float rowY = i * rowHeight;
                    GUI.Label(new Rect(4f, rowY + 8f, compact ? 120f : 150f, 22f), unit.unit_name, coordStyle);
                    GUI.Label(new Rect(compact ? 125f : 160f, rowY + 8f, compact ? 95f : 120f, 22f),
                        unit.unit_type, hintStyle);

                    string contact = "Kontak: --";
                    if (DateTimeOffset.TryParse(unit.last_heard, out DateTimeOffset heardAt))
                        contact = "Kontak: " + heardAt.ToLocalTime().ToString("dd MMM HH:mm");
                    string gps = "GPS belum tersedia";
                    if (DateTimeOffset.TryParse(unit.gps_updated_at, out DateTimeOffset gpsAt))
                        gps = DateTimeOffset.UtcNow - gpsAt.ToUniversalTime() <= TimeSpan.FromSeconds(120)
                            ? "GPS aktif" : "GPS lama";

                    if (compact)
                    {
                        GUI.Label(new Rect(4f, rowY + 30f, contentRect.width * 0.55f, 18f), contact, hintStyle);
                        GUI.Label(new Rect(contentRect.width * 0.56f, rowY + 30f, contentRect.width * 0.44f, 18f), gps, hintStyle);
                    }
                    else
                    {
                        GUI.Label(new Rect(300f, rowY + 8f, 185f, 22f), contact, hintStyle);
                        GUI.Label(new Rect(500f, rowY + 8f, contentRect.width - 504f, 22f), gps, hintStyle);
                    }
                    if (topHighlightTex != null)
                        GUI.DrawTexture(new Rect(0f, rowY + rowHeight - 1f, contentRect.width, 1f), topHighlightTex);
                }
                GUI.EndScrollView();
            }

            GUI.Label(new Rect(x + 18f, y + height - 41f, width - 36f, 25f),
                "Total " + visible.Count + " unit | Posisi peta memerlukan GPS yang valid.", hintStyle);
        }

        // =========================================================================
        // 17.8 FLEET MATRIX & ALL CHILDREN MODAL (Excavator + All Assigned Dump Trucks)
        // =========================================================================
        private void DrawFleetMatrixModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(980f, screenW - 40f);
            float modalH = Mathf.Min(650f, screenH - 50f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Dark Backdrop
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);

            // Modal Box
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, y + 2, modalW - 4, 2), lineAccentTex);
            }

            if (FMSFleetManager.Instance == null)
            {
                GUI.Label(new Rect(x + 20, y + 20, 300, 20), "Fleet manager tidak tersedia", hintStyle);
                if (GUI.Button(new Rect(x + modalW - 120, y + modalH - 40, 100, 28), "Tutup", navBtnStyle)) showFleetMatrixModal = false;
                return;
            }

            string loaderId = matrixLoaderId ?? "";
            var loader = FMSFleetManager.Instance.activeFleet.Find(u => u.unitId.Equals(loaderId, StringComparison.OrdinalIgnoreCase));
            var children = FMSFleetManager.Instance.GetChildHaulers(loaderId);

            FMSFleetManager.Instance.GetAggregateFleetStats(loaderId, out int totalTrips, out double totalTons, out float avgCycleMins, out float matchFactor, out int activeCount, out float totalFuel);

            // Header
            string loaderModel = loader != null ? loader.modelName : "--";
            string loaderFront = loader != null ? loader.activityName : "--";
            GUI.Label(new Rect(x + 20, y + 14, modalW - 60, 24), 
                $"🏆 <b>MATRIKS PRODUKSI FLEET SHOVEL: <color=#00FFA3>{loaderId}</color> ({loaderModel})</b>", brandLogoStyle);

            if (GUI.Button(new Rect(x + modalW - 36, y + 14, 24, 22), "✕", quickDockBtnStyle))
            {
                showFleetMatrixModal = false;
            }

            GUI.Label(new Rect(x + 20, y + 40, modalW - 40, 20), 
                $"📍 Front Penggalian: <b>{loaderFront}</b> | Armada Terhubung: <b>{children.Count} Unit Haul Truck</b> ({activeCount} Beroperasi)", hintStyle);

            float curY = y + 66;

            // 4 BIG KPI CARDS (Columns)
            float kpiColW = (modalW - 58) / 4f;
            float kpiH = 74f;

            // KPI 1: Total Tonase Gabungan
            Rect mkpi1 = new Rect(x + 20, curY, kpiColW, kpiH);
            GUI.Box(mkpi1, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(mkpi1.x + 2, mkpi1.y + 2, mkpi1.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 28, curY + 6, kpiColW - 16, 16), "⚖️ <b>MUATAN AKTIF FLEET</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 28, curY + 24, kpiColW - 16, 24), $"<color=#00FFA3><size=17><b>{totalTons:N0} Ton</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 28, curY + 48, kpiColW - 16, 16), "Snapshot, bukan produksi selesai", hintStyle);

            // KPI 2: Total Ritase
            Rect mkpi2 = new Rect(x + 26 + kpiColW, curY, kpiColW, kpiH);
            GUI.Box(mkpi2, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(mkpi2.x + 2, mkpi2.y + 2, mkpi2.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 6, kpiColW - 16, 16), "🔄 <b>SIKLUS MUAT FLEET</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 24, kpiColW - 16, 24), $"<color=#00E5FF><size=17><b>{totalTrips} Kali</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 48, kpiColW - 16, 16), "Ritase selesai belum tercatat", hintStyle);

            // KPI 3: Match Factor
            Rect mkpi3 = new Rect(x + 32 + kpiColW * 2, curY, kpiColW, kpiH);
            GUI.Box(mkpi3, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(mkpi3.x + 2, mkpi3.y + 2, mkpi3.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 6, kpiColW - 16, 16), "⚖️ <b>MATCH FACTOR (MF)</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 24, kpiColW - 16, 24), "<color=#88A0B8><size=17><b>--</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 48, kpiColW - 16, 16), "Durasi siklus belum tercatat", hintStyle);

            // KPI 4: Konsumsi Bahan Bakar
            Rect mkpi4 = new Rect(x + 38 + kpiColW * 3, curY, kpiColW, kpiH);
            GUI.Box(mkpi4, GUIContent.none, dropdownPanelStyle);
            if (lineAccentTex != null) GUI.DrawTexture(new Rect(mkpi4.x + 2, mkpi4.y + 2, mkpi4.width - 4, 1.5f), lineAccentTex);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 6, kpiColW - 16, 16), "⛽ <b>TOTAL KONSUMSI SOLAR</b>", dropdownHeaderStyle);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 24, kpiColW - 16, 24), "<color=#88A0B8><size=17><b>--</b></size></color>", hintStyle);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 48, kpiColW - 16, 16), "Sensor belum tersedia", hintStyle);

            curY += kpiH + 12;

            // TABLE OF ALL CHILD HAUL TRUCKS
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "📋 <b>DAFTAR LENGKAP DUMP TRUCK ANAK YANG TERGABUNG DALAM FLEET INI:</b>", dropdownHeaderStyle);
            curY += 22;

            // Table Header Row
            float tableW = modalW - 40;
            float thH = 26f;
            GUI.Box(new Rect(x + 20, curY, tableW, thH), GUIContent.none, dropdownPanelStyle);
            GUI.Label(new Rect(x + 26, curY + 4, 35, 18), "<b>NO</b>", hintStyle);
            GUI.Label(new Rect(x + 65, curY + 4, 85, 18), "<b>ID UNIT</b>", hintStyle);
            GUI.Label(new Rect(x + 155, curY + 4, 110, 18), "<b>OPERATOR</b>", hintStyle);
            GUI.Label(new Rect(x + 270, curY + 4, 80, 18), "<b>MUATAN</b>", hintStyle);
            GUI.Label(new Rect(x + 355, curY + 4, 60, 18), "<b>MUAT</b>", hintStyle);
            GUI.Label(new Rect(x + 420, curY + 4, 180, 18), "<b>STATUS SIKLUS</b>", hintStyle);
            GUI.Label(new Rect(x + 605, curY + 4, 85, 18), "<b>KECEPATAN</b>", hintStyle);
            GUI.Label(new Rect(x + 695, curY + 4, 55, 18), "<b>FUEL</b>", hintStyle);
            GUI.Label(new Rect(x + 760, curY + 4, 160, 18), "<b>AKSI KAMERA / DETAIL</b>", hintStyle);
            curY += thH + 4;

            // Scrollable List of Children
            float listH = (y + modalH - 56) - curY;
            float totalContentH = children.Count * 36f + 10f;
            Rect scrollArea = new Rect(x + 20, curY, tableW, listH);
            Rect viewArea = new Rect(0, 0, tableW - 20, Mathf.Max(listH, totalContentH));

            matrixScrollPos = GUI.BeginScrollView(scrollArea, matrixScrollPos, viewArea);
            float rowY = 2f;

            for (int i = 0; i < children.Count; i++)
            {
                var truck = children[i];
                if (truck == null) continue;

                // Alternating row background
                if (i % 2 == 0)
                {
                    GUI.Box(new Rect(0, rowY, viewArea.width, 32), GUIContent.none, dropdownPanelStyle);
                }

                GUI.Label(new Rect(6, rowY + 6, 35, 18), $"#{i + 1}", hintStyle);
                GUI.Label(new Rect(45, rowY + 6, 85, 18), $"🚚 <b>{truck.unitId}</b>", coordStyle);
                GUI.Label(new Rect(135, rowY + 6, 110, 18), truck.operatorName, hintStyle);

                string loadCol = truck.hasActualPayload && truck.payloadTons > 10f ? "<color=#00FFA3>" : "<color=#AAAAAA>";
                GUI.Label(new Rect(250, rowY + 6, 80, 18), $"{loadCol}<b>{(truck.hasActualPayload ? $"{truck.payloadTons:F0} T" : "--")}</b></color>", hintStyle);
                GUI.Label(new Rect(335, rowY + 6, 60, 18), $"<b>{(truck.hasActualHaulData ? truck.recordedLoads.ToString() : "--")}</b> Muat", hintStyle);

                string stColor = truck.currentState == UnitState.Hauling ? "#00FFA3" : (truck.currentState == UnitState.Loading ? "#FFB800" : (truck.currentState == UnitState.Dumping ? "#00E5FF" : "#FFFFFF"));
                GUI.Label(new Rect(400, rowY + 6, 180, 18), $"<color={stColor}>{truck.currentState}</color>", hintStyle);

                GUI.Label(new Rect(585, rowY + 6, 85, 18), $"{truck.currentSpeedKmh:F1} KM/Jam", hintStyle);
                GUI.Label(new Rect(675, rowY + 6, 55, 18), "⛽ --", hintStyle);

                // Row Action Buttons
                if (GUI.Button(new Rect(725, rowY + 3, 46, 24), "🎯 3D", navBtnStyle))
                {
                    FMSFleetManager.Instance.SelectUnit(truck);
                    FMSCameraController.Instance?.JumpTo(truck.transform.position, 120f);
                    showFleetMatrixModal = false;
                }

                if (GUI.Button(new Rect(775, rowY + 3, 52, 24), "📹 Kabin", navBtnActiveStyle))
                {
                    FMSFleetManager.Instance.SelectUnit(truck);
                    EnsureUnitCctvManager();
                    FMSUnitCctvManager.Instance?.OpenCctv(truck, CctvCameraChannel.CabinDriver);
                    showFleetMatrixModal = false;
                }

                if (GUI.Button(new Rect(831, rowY + 3, 44, 24), "🏎️ Chase", navBtnStyle))
                {
                    FMSFleetManager.Instance.SelectUnit(truck);
                    FMSCameraController.Instance?.SetFollowMode(CameraFollowMode.ChaseBehind, truck.transform);
                    showFleetMatrixModal = false;
                }

                if (GUI.Button(new Rect(879, rowY + 3, 46, 24), "📊 Info", navBtnStyle))
                {
                    modalSelectedUnit = truck;
                    showUnitProductionModal = true;
                    showFleetMatrixModal = false;
                }

                rowY += 36f;
            }

            GUI.EndScrollView();

            // Footer Action Bar
            float btmY = y + modalH - 46;

            bool isIsolated = FMSFleetManager.Instance.isFleetIsolated && FMSFleetManager.Instance.isolatedLoaderId == loaderId;
            string isoText = isIsolated ? "🔄 Batal Isolasi (Tampilkan Semua)" : $"🎯 Isolasi Fleet {loaderId} di Peta 3D";
            GUIStyle isoStyle = isIsolated ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(x + 20, btmY, 260, 32), isoText, isoStyle))
            {
                if (isIsolated) FMSFleetManager.Instance.RestoreAllUnits();
                else FMSFleetManager.Instance.IsolateFleet(loaderId);
            }

            bool isHeatmap = FMSFleetManager.Instance.showFleetHeatmap && FMSFleetManager.Instance.heatmapLoaderId == loaderId;
            string hmText = isHeatmap ? "🔥 Matikan Heatmap Lintasan" : "🔥 Aktifkan Heatmap Lintasan Fleet";
            GUIStyle hmStyle = isHeatmap ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(x + 290, btmY, 250, 32), hmText, hmStyle))
            {
                FMSFleetManager.Instance.ToggleFleetHeatmap(loaderId);
            }

            if (GUI.Button(new Rect(x + 550, btmY, 180, 32), "🔄 Refresh Telemetri", navBtnStyle))
            {
                ShowNotification("🔄 Data Telemetri & Matriks Produksi Fleet Diperbarui.");
            }

            if (GUI.Button(new Rect(x + modalW - 120, btmY, 100, 32), "Tutup", navBtnStyle))
            {
                showFleetMatrixModal = false;
            }
        }

        // =========================================================================
        // 17.85 TWO-WAY RADIO DISPATCH MESSENGER MODAL (CONTROL ROOM DISPATCHER)
        // =========================================================================
        private void DrawControlRoomDispatchRadioModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(880f, screenW - 30f);
            float modalH = Mathf.Min(600f, screenH - 30f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Semi-transparent dark backdrop
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);

            // Main Modal Card
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
                GUI.DrawTexture(new Rect(x + 2, y + 2, modalW - 4, 2), lineAccentTex);

            // Modal Header
            GUI.Label(new Rect(x + 20, y + 14, modalW - 100, 24), "📻 <b>FMS TWO-WAY RADIO & DISPATCH MESSENGER (CONTROL ROOM)</b>", brandLogoStyle ?? coordStyle);

            if (GUI.Button(new Rect(x + modalW - 100, y + 10, 80, 26), "✕ Tutup", navBtnStyle))
            {
                showDispatchRadioModal = false;
            }

            float curY = y + 44;

            // Target Unit Selector Row
            GUI.Label(new Rect(x + 20, curY, 130, 24), "🎯 <b>PILIH TUJUAN:</b>", hintStyle);
            bool isAll = dispatchSelectedUnitTarget == "ALL";
            if (GUI.Button(new Rect(x + 140, curY, modalW - 160f, 24), isAll ? "SEMUA ARMADA (AKTIF)" : "Kirim ke semua armada", isAll ? navBtnActiveStyle : navBtnStyle))
            {
                dispatchSelectedUnitTarget = "ALL";
            }
            curY += 28f;
            var fleet = FMSFleetManager.Instance != null ? FMSFleetManager.Instance.activeFleet : null;
            int unitCount = fleet != null ? fleet.Count : 0;
            float quickBtnW = (modalW - 83f) / 4f;
            dispatchTargetScrollPos = GUI.BeginScrollView(
                new Rect(x + 20f, curY, modalW - 40f, 58f), dispatchTargetScrollPos,
                new Rect(0f, 0f, modalW - 58f, Mathf.Max(54f, Mathf.CeilToInt(unitCount / 4f) * 27f)));
            for (int i = 0; i < unitCount; i++)
            {
                var fleetUnit = fleet[i];
                if (fleetUnit == null || string.IsNullOrEmpty(fleetUnit.unitId)) continue;
                bool isSel = dispatchSelectedUnitTarget == fleetUnit.unitId;
                if (GUI.Button(new Rect((i % 4) * (quickBtnW + 5f), (i / 4) * 27f,
                    quickBtnW, 24f), fleetUnit.unitId, isSel ? navBtnActiveStyle : navBtnStyle))
                    dispatchSelectedUnitTarget = fleetUnit.unitId;
            }
            if (unitCount == 0)
                GUI.Label(new Rect(0f, 0f, modalW - 58f, 24f), "Belum ada unit aktif; gunakan siaran semua armada.", hintStyle);
            GUI.EndScrollView();
            curY += 60f;

            // Chat Message Scroll View
            float chatBoxH = Mathf.Max(80f, modalH - 285f);
            var msgs = FMSFleetMessenger.Instance != null ? FMSFleetMessenger.Instance.messageHistory : new List<FMSFleetMessenger.ChatMessage>();

            Rect scrollArea = new Rect(x + 20, curY, modalW - 40, chatBoxH);
            float totalContentH = Mathf.Max(chatBoxH, msgs.Count * 60f + 20f);
            Rect viewArea = new Rect(0, 0, modalW - 60, totalContentH);

            dispatchChatScrollPos = GUI.BeginScrollView(scrollArea, dispatchChatScrollPos, viewArea);
            float itemY = 4f;

            foreach (var msg in msgs)
            {
                bool isDispatcher = msg.senderRole == "DISPATCHER";
                float msgW = viewArea.width * 0.85f;
                float msgX = isDispatcher ? 4f : (viewArea.width - msgW - 6);

                GUI.Box(new Rect(msgX, itemY, msgW, 52), GUIContent.none, dropdownPanelStyle);

                string badgeColor = isDispatcher ? "#00E5FF" : "#00FFA3";
                string roleBadge = isDispatcher ? "[DISPATCH CONTROL]" : "[KABIN OPERATOR]";
                string targetBadge = msg.targetUnitId == "ALL" ? "<color=#FFB800>[BROADCAST ALL]</color>" : $"<color=#00FFA3>[Target: {msg.targetUnitId}]</color>";

                GUI.Label(new Rect(msgX + 10, itemY + 4, msgW - 20, 18), $"<color={badgeColor}><b>{roleBadge} {msg.senderName}</b></color>  {targetBadge}  <color=#88A0B8>{msg.timestamp}</color>", hintStyle);
                GUI.Label(new Rect(msgX + 10, itemY + 22, msgW - 20, 26), msg.messageText, hintStyle);

                itemY += 58f;
            }

            GUI.EndScrollView();
            curY += chatBoxH + 10;

            // Mining Dispatch Macro Chips
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "⚡ <b>TEMPLATE PERINTAH DISPATCH CEPAT (1-CLICK MACRO):</b>", hintStyle);
            curY += 22;

            float mBtnW = (modalW - 40 - 15) / 4f;
            if (GUI.Button(new Rect(x + 20, curY, mBtnW, 26), "Kondisi jalan", navBtnStyle))
            {
                dispatchOutgoingMessage = "Perhatian armada: Periksa kondisi jalan dan sesuaikan kecepatan dengan instruksi pengawas lapangan.";
            }
            if (GUI.Button(new Rect(x + 25 + mBtnW, curY, mBtnW, 26), "Zona peledakan", navBtnStyle))
            {
                dispatchOutgoingMessage = "Perhatian armada: Konfirmasi jadwal dan zona aman peledakan kepada pengawas sebelum melintas.";
            }
            if (GUI.Button(new Rect(x + 30 + mBtnW * 2, curY, mBtnW, 26), "Bahan bakar", navBtnStyle))
            {
                dispatchOutgoingMessage = "Silakan konfirmasi kebutuhan pengisian bahan bakar dan lokasi stasiun yang tersedia.";
            }
            if (GUI.Button(new Rect(x + 35 + mBtnW * 3, curY, mBtnW, 26), "Pengalihan rute", navBtnStyle))
            {
                dispatchOutgoingMessage = "Konfirmasi pengalihan rute dan unit pemuat tujuan dengan dispatcher sebelum bergerak.";
            }
            curY += 32;

            // Outgoing Message Input & Talkback Voice Row
            float inputRowW = modalW - 40;
            float pttBtnW = 200f;
            float sendBtnW = 120f;
            float textInputW = inputRowW - pttBtnW - sendBtnW - 16;

            GUI.Box(new Rect(x + 20, curY, textInputW, 36), GUIContent.none, dropdownPanelStyle);
            dispatchOutgoingMessage = GUI.TextField(new Rect(x + 26, curY + 6, textInputW - 12, 24), dispatchOutgoingMessage, GUI.skin.textField);

            if (GUI.Button(new Rect(x + 28 + textInputW, curY, sendBtnW, 36), "KIRIM 📨", navBtnActiveStyle))
            {
                if (!string.IsNullOrEmpty(dispatchOutgoingMessage))
                {
                    if (FMSFleetMessenger.Instance != null)
                    {
                        FMSFleetMessenger.Instance.SendFromControlRoom(dispatchSelectedUnitTarget, dispatchOutgoingMessage);
                    }
                    dispatchOutgoingMessage = "";
                }
            }

            // PUSH-TO-TALK TALKBACK VOICE BUTTON
            bool isTransmitting = FMSFleetMessenger.Instance != null && FMSFleetMessenger.Instance.isTalkbackActive;
            string pttLabel = isTransmitting ? $"🔴 ON-AIR ({FMSFleetMessenger.Instance.talkbackDuration:F1}s)" : "🎙️ TALKBACK (VOICE)";
            GUIStyle pttStyle = isTransmitting ? navBtnActiveStyle : navBtnStyle;

            if (GUI.Button(new Rect(x + 36 + textInputW + sendBtnW, curY, pttBtnW, 36), pttLabel, pttStyle))
            {
                if (FMSFleetMessenger.Instance != null)
                {
                    if (!FMSFleetMessenger.Instance.isTalkbackActive)
                    {
                        FMSFleetMessenger.Instance.StartTalkback(dispatchSelectedUnitTarget);
                    }
                    else
                    {
                        FMSFleetMessenger.Instance.StopTalkback();
                    }
                }
            }

            // Live Audio Waveform Banner when transmitting
            if (isTransmitting)
            {
                curY += 40;
                string waveBars = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ ▆ ▇ █ ▇ ▆ ▅ ▄ ▃ ▂ " : " █ ▇ ▆ ▅ ▄ ▃ ▂   ▂ ▃ ▄ ▅ ▆ ▇ █ ";
                string targetName = dispatchSelectedUnitTarget == "ALL" ? "SELURUH ARMADA (BROADCAST)" : $"UNIT {dispatchSelectedUnitTarget}";
                GUI.Box(new Rect(x + 20, curY, modalW - 40, 28), GUIContent.none, dropdownPanelStyle);
                GUI.Label(new Rect(x + 26, curY + 4, modalW - 52, 20), 
                    $"<color=#FF4D4D><b>🔴 LIVE AUDIO STREAM:</b></color> <color=#00E5FF>{waveBars}</color> Transmisi suara aktif ke <color=#00FFA3><b>{targetName}</b></color>", hintStyle);
            }
        }

        // =========================================================================
        // 17.97 LIVE RADIO TALKBACK & INCOMING CABIN VOICE OVERLAY
        // =========================================================================
        private void DrawLiveRadioTransmissionOverlay(float screenW, float screenH)
        {
            if (FMSFleetMessenger.Instance == null) return;

            // 1. INCOMING LIVE VOICE CALL FROM CABIN OPERATOR
            if (FMSFleetMessenger.Instance.isCabinTalkbackActive)
            {
                string sourceUnit = FMSFleetMessenger.Instance.cabinTalkbackSourceUnit;
                string opName = FMSFleetMessenger.Instance.cabinTalkbackOperatorName;
                float dur = FMSFleetMessenger.Instance.cabinTalkbackDuration;

                float cardW = 620f;
                float cardH = 68f;
                float cardX = (screenW - cardW) / 2f;
                float cardY = 56f;

                GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);
                if (lineAccentTex != null)
                {
                    GUI.DrawTexture(new Rect(cardX + 2, cardY + 2, cardW - 4, 3), lineAccentTex);
                }

                string waveBars = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ ▆ ▇ █ ▇ ▆ ▅ ▄ ▃ ▂ " : " █ ▇ ▆ ▅ ▄ ▃ ▂   ▂ ▃ ▄ ▅ ▆ ▇ █ ";
                GUI.Label(new Rect(cardX + 16, cardY + 8, cardW - 240, 24), 
                    $"<color=#FF4D4D><size=14><b>📞 [RADIO KABIN MASUK]</b></size></color> <color=#00FFA3><b>{sourceUnit}</b></color> <color=#00E5FF>({opName})</color>", brandLogoStyle ?? coordStyle);
                
                GUI.Label(new Rect(cardX + 16, cardY + 34, cardW - 240, 20), 
                    $"<color=#FFB800>Live PTT ({dur:F1}s)</color> <color=#00E5FF>{waveBars}</color> Operator sedang berbicara...", hintStyle);

                // Action Buttons
                float btnY = cardY + 16;
                
                // Jump / Focus Camera
                if (GUI.Button(new Rect(cardX + cardW - 220, btnY, 100, 36), "🎯 Jump 3D", navBtnStyle))
                {
                    var unit = FMSFleetManager.Instance?.GetUnitById(sourceUnit);
                    if (unit != null)
                    {
                        FMSCameraController.Instance?.JumpTo(unit.transform.position, 120f);
                        FMSFleetManager.Instance.SelectUnit(unit);
                    }
                }

                // Reply Talkback
                bool isReplying = FMSFleetMessenger.Instance.isTalkbackActive && FMSFleetMessenger.Instance.talkbackTargetUnit == sourceUnit;
                string repLabel = isReplying ? "🔴 Putus" : "🎙️ Balas";
                GUIStyle repStyle = isReplying ? navBtnActiveStyle : navBtnActiveStyle;
                if (GUI.Button(new Rect(cardX + cardW - 110, btnY, 95, 36), repLabel, repStyle))
                {
                    if (isReplying)
                    {
                        FMSFleetMessenger.Instance.StopTalkback();
                    }
                    else
                    {
                        FMSFleetMessenger.Instance.StartTalkback(sourceUnit);
                    }
                }
            }
            // 2. PENDING REQUEST-TO-TALK PERMISSION FROM CABIN
            else if (FMSFleetMessenger.Instance.hasPendingTalkbackRequest)
            {
                string sourceUnit = FMSFleetMessenger.Instance.pendingRequestUnitId;
                string opName = FMSFleetMessenger.Instance.pendingRequestOperatorName;

                float cardW = 600f;
                float cardH = 68f;
                float cardX = (screenW - cardW) / 2f;
                float cardY = 56f;

                GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);
                GUI.Label(new Rect(cardX + 16, cardY + 8, cardW - 220, 24), 
                    $"<color=#FFB800><size=14><b>🔔 [PERMINTAAN BICARA KABIN]</b></size></color> <color=#00FFA3><b>{sourceUnit}</b></color>", brandLogoStyle ?? coordStyle);
                
                GUI.Label(new Rect(cardX + 16, cardY + 34, cardW - 220, 20), 
                    $"Operator <color=#FFFFFF><b>{opName}</b></color> meminta izin membuka channel transmisi radio.", hintStyle);

                // Approve & Reject Buttons
                float btnY = cardY + 16;
                if (GUI.Button(new Rect(cardX + cardW - 200, btnY, 110, 36), "✅ Izinkan PTT", navBtnActiveStyle))
                {
                    FMSFleetMessenger.Instance.ApproveCabinTalkback(sourceUnit);
                }
                if (GUI.Button(new Rect(cardX + cardW - 80, btnY, 65, 36), "✕ Tolak", navBtnStyle))
                {
                    FMSFleetMessenger.Instance.DismissTalkbackRequest();
                }
            }
            // 3. DISPATCHER ACTIVE OUTGOING TRANSMISSION BANNER
            else if (FMSFleetMessenger.Instance.isTalkbackActive)
            {
                string target = FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL" ? "SELURUH ARMADA (BROADCAST)" : $"UNIT {FMSFleetMessenger.Instance.talkbackTargetUnit}";
                float dur = FMSFleetMessenger.Instance.talkbackDuration;

                float pillW = 440f;
                float pillH = 38f;
                float px = (screenW - pillW) / 2f;
                float py = 52f;

                GUI.Box(new Rect(px, py, pillW, pillH), GUIContent.none, dropdownPanelStyle);
                string waveBars = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ ▆ ▇ █ ▇ ▆ ▅ ▄ ▃ ▂ " : " █ ▇ ▆ ▅ ▄ ▃ ▂   ▂ ▃ ▄ ▅ ▆ ▇ █ ";
                
                GUI.Label(new Rect(px + 12, py + 8, pillW - 120, 22), 
                    $"<color=#FF4D4D><b>🔴 DISPATCH TALKBACK:</b></color> <color=#00FFA3><b>{target}</b></color> ({dur:F1}s)", hintStyle);

                if (GUI.Button(new Rect(px + pillW - 95, py + 5, 85, 28), "🔴 STOP", navBtnActiveStyle))
                {
                    FMSFleetMessenger.Instance.StopTalkback();
                }
            }
        }

        // =========================================================================
        // 17.9 ONBOARD UNIT LIVE CCTV STREAM MODAL / PIP
        // =========================================================================
        private void DrawUnitCctvModal(float screenW, float screenH)
        {
            if (FMSUnitCctvManager.Instance == null || !FMSUnitCctvManager.Instance.isCctvOpen || FMSUnitCctvManager.Instance.targetUnit == null) return;

            var unit = FMSUnitCctvManager.Instance.targetUnit;
            var cctv = FMSUnitCctvManager.Instance;
            float modalW = Mathf.Min(820f, screenW - 30f);
            float modalH = cctv.showUrlConfigPanel ? Mathf.Min(640f, screenH - 30f) : Mathf.Min(580f, screenH - 30f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Semi-transparent dark backdrop
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, modalBoxStyle);

            // Main Modal Card
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, y + 2, modalW - 4, 2), lineAccentTex);
            }

            // Top Header Bar
            bool isBlink = ((int)(Time.time * 2f) % 2) == 0;
            string recBadge = isBlink ? "<color=#FF3344>● REC [LIVE]</color>" : "<color=#881122>● REC [LIVE]</color>";

            GUI.Label(new Rect(x + 18, y + 10, modalW - 300, 22), 
                $"📹 <b>CCTV STREAM KABIN: <color=#00FFA3>{unit.unitId}</color></b> ({unit.modelName})  {recBadge}", brandLogoStyle ?? coordStyle);

            // Stream Source Status Badge
            string srcBadge = cctv.currentStreamSource switch
            {
                CctvStreamSource.ApiVideoStream => "<color=#00FFA3>● API VIDEO STREAM</color>",
                CctvStreamSource.ApiSnapshotStream => "<color=#00E5FF>● API SNAPSHOT</color>",
                _ => "<color=#FFB800>● SENSOR DIGITAL TWIN 3D</color>"
            };
            GUI.Label(new Rect(x + modalW - 285, y + 10, 160, 22), srcBadge, hintStyle);

            // Settings / URL Config Button
            string cfgTxt = cctv.showUrlConfigPanel ? "▲ Tutup URL" : "⚙️ URL API";
            GUIStyle cfgStyle = cctv.showUrlConfigPanel ? navBtnActiveStyle : navBtnStyle;
            if (GUI.Button(new Rect(x + modalW - 120, y + 8, 80, 24), cfgTxt, cfgStyle))
            {
                cctv.showUrlConfigPanel = !cctv.showUrlConfigPanel;
            }

            // Close Button
            if (GUI.Button(new Rect(x + modalW - 34, y + 8, 24, 24), "✕", quickDockBtnStyle))
            {
                cctv.CloseCctv();
                return;
            }

            // Driver FTW Savera & Health Telemetry Header
            EnsureFtwSaveraManager();
            var ftw = FMSFtwSaveraManager.Instance?.GetFtwRecord(unit.unitId, unit.operatorName);
            string ftwBadge = ftw != null ? (ftw.status_ftw == FtwStatus.FitToWork ? "<color=#00FFA3>🟢 FIT</color>" : (ftw.status_ftw == FtwStatus.DalamPengawasan ? "<color=#FFB800>🟡 WASPADA</color>" : "<color=#FF4D4D>🔴 UNFIT</color>")) : "--";
            string sleepHours = ftw != null ? $"{ftw.jam_tidur:F1} Jam" : "--";
            string nik = ftw != null ? ftw.NIK : "--";

            GUI.Label(new Rect(x + 18, y + 34, modalW - 36, 18), 
                $"👷 Supir: <b>{unit.operatorName}</b> ({nik}) | FTW: {ftwBadge} | 💤 Tidur: <b>{sleepHours}</b> | Saluran: <b><color=#00E5FF>{cctv.GetChannelName(cctv.currentChannel)}</color></b>", hintStyle);

            float curY = y + 54f;

            // Optional CCTV API URL / Stream Configuration Drawer
            if (cctv.showUrlConfigPanel)
            {
                float cfgBoxH = 58f;
                GUI.Box(new Rect(x + 16, curY, modalW - 32, cfgBoxH), GUIContent.none, dropdownPanelStyle);
                GUI.Label(new Rect(x + 24, curY + 6, modalW - 48, 16), 
                    "⚙️ <b>URL Endpoint CCTV API (Dapat Menggunakan HLS, MP4, MJPEG, atau URL Stream Kamera Kabin Anda):</b>", dropdownHeaderStyle);

                string inputUrl = GUI.TextField(new Rect(x + 24, curY + 26, modalW - 200, 24), cctv.customStreamUrl, searchBoxStyle ?? GUI.skin.textField);
                if (inputUrl != cctv.customStreamUrl)
                {
                    cctv.customStreamUrl = inputUrl;
                }

                if (GUI.Button(new Rect(x + modalW - 168, curY + 26, 136, 24), "▶ Hubungkan Stream", navBtnActiveStyle))
                {
                    cctv.SaveCustomUrl(cctv.customStreamUrl);
                    ShowNotification("🔄 Menghubungkan ke URL CCTV baru...");
                }

                curY += cfgBoxH + 6f;
            }

            // Video Feed Canvas
            float videoX = x + 16f;
            float videoY = curY;
            float videoW = modalW - 32f;
            float videoH = modalH - (curY - y) - 95f;
            Rect videoRect = new Rect(videoX, videoY, videoW, videoH);

            // Dark Screen Frame
            GUI.Box(videoRect, GUIContent.none, dropdownPanelStyle);

            if (cctv.CctvRenderTexture != null)
            {
                GUI.DrawTexture(videoRect, cctv.CctvRenderTexture, ScaleMode.ScaleAndCrop);
            }

            // High-Tech CCTV OSD Overlay (HUD on top of video feed)
            // Top-Left OSD: Camera ID + Status
            GUI.Label(new Rect(videoX + 12, videoY + 8, 480, 20), 
                $"<color=#00FFA3>● CAM-{((int)cctv.currentChannel + 1):D2} HD 1080p</color>  |  FPS: {cctv.streamFps:F0}  |  BITRATE: {cctv.streamBitrateMbps:F1} Mbps  |  <color=#00E5FF>{cctv.streamStatusMessage}</color>", hintStyle);

            // Top-Right OSD: Live UTC+7 Timestamp
            string timeStr = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss.ff");
            GUI.Label(new Rect(videoX + videoW - 250, videoY + 8, 240, 20), 
                $"<color=#00FFA3>{timeStr} UTC+7</color>", hintStyle);

            // Center Crosshair
            float cx = videoX + videoW / 2f;
            float cy = videoY + videoH / 2f;
            GUI.Label(new Rect(cx - 10, cy - 10, 20, 20), "<color=#00FFA344>+</color>", compassHeadingStyle ?? coordStyle);

            // Bottom-Left OSD: Telemetry Matrix
            GUI.Label(new Rect(videoX + 12, videoY + videoH - 42, videoW - 24, 18), 
                $"<b>UNIT:</b> {unit.unitId} ({unit.modelName})  |  <b>KECEPATAN:</b> <color=#00FFA3>{unit.currentSpeedKmh:F1} KM/Jam</color>  |  <b>STATUS:</b> {unit.currentState}  |  <b>MUATAN:</b> {(unit.hasActualPayload ? $"{unit.payloadTons:F0} Ton" : "--")}", hintStyle);
            GUI.Label(new Rect(videoX + 12, videoY + videoH - 22, videoW - 24, 18), 
                $"<b>SUPIR:</b> {unit.operatorName}  |  <b>FTW:</b> {ftwBadge}  |  <b>TIDUR:</b> {sleepHours}  |  <b>KORDINAT:</b> E:{(572728.3 + unit.transform.position.x):F1} N:{(113338.3 + unit.transform.position.z):F1} ELEV:{unit.transform.position.y:F1}m", hintStyle);

            // Bottom Section: 4-Channel Switcher
            float chBtnY = videoY + videoH + 8f;
            float chBtnW = (modalW - 56f) / 4f;

            var ch0 = CctvCameraChannel.CabinDriver;
            var ch1 = CctvCameraChannel.FrontDashcam;
            var ch2 = CctvCameraChannel.RearBackup;
            var ch3 = CctvCameraChannel.ElevatedMast;

            bool isCh0 = cctv.currentChannel == ch0;
            bool isCh1 = cctv.currentChannel == ch1;
            bool isCh2 = cctv.currentChannel == ch2;
            bool isCh3 = cctv.currentChannel == ch3;

            if (GUI.Button(new Rect(x + 16, chBtnY, chBtnW, 28), "👨‍✈️ CH 1: Kabin Supir", isCh0 ? navBtnActiveStyle : navBtnStyle))
            {
                cctv.SetChannel(ch0);
            }

            if (GUI.Button(new Rect(x + 24 + chBtnW, chBtnY, chBtnW, 28), "🪟 CH 2: Dashcam Depan", isCh1 ? navBtnActiveStyle : navBtnStyle))
            {
                cctv.SetChannel(ch1);
            }

            if (GUI.Button(new Rect(x + 32 + chBtnW * 2, chBtnY, chBtnW, 28), "🔙 CH 3: Belakang/Dump", isCh2 ? navBtnActiveStyle : navBtnStyle))
            {
                cctv.SetChannel(ch2);
            }

            if (GUI.Button(new Rect(x + 40 + chBtnW * 3, chBtnY, chBtnW, 28), "🔭 CH 4: Orbit 360 Mast", isCh3 ? navBtnActiveStyle : navBtnStyle))
            {
                cctv.SetChannel(ch3);
            }

            // Bottom Action Footer Bar
            float btmActionY = chBtnY + 34f;
            if (GUI.Button(new Rect(x + 16, btmActionY, 140, 26), "🎯 Fokus Peta 3D", navBtnStyle))
            {
                FMSCameraController.Instance?.JumpTo(unit.transform.position, 120f);
            }

            if (GUI.Button(new Rect(x + 164, btmActionY, 150, 26), "🩺 Cek FTW Savera", navBtnActiveStyle))
            {
                EnsureFtwSaveraManager();
                FMSFtwSaveraManager.Instance.searchFilter = unit.unitId;
                FMSFtwSaveraManager.Instance.isFtwModalOpen = true;
            }

            if (GUI.Button(new Rect(x + 322, btmActionY, 150, 26), "📊 Detail Produksi", navBtnStyle))
            {
                modalSelectedUnit = unit;
                showUnitProductionModal = true;
                cctv.CloseCctv();
            }

            if (GUI.Button(new Rect(x + 480, btmActionY, 140, 26), "🌙 Mode IR Night", cctv.isNightVision ? navBtnActiveStyle : navBtnStyle))
            {
                cctv.ToggleNightVision();
            }

            if (GUI.Button(new Rect(x + modalW - 130, btmActionY, 114, 26), "Tutup [ESC]", navBtnActiveStyle))
            {
                cctv.CloseCctv();
            }
        }

        public void EnsureUnitCctvManager()
        {
            if (FMSUnitCctvManager.Instance == null)
            {
                var existing = FindFirstObjectByType<FMSUnitCctvManager>();
                if (existing == null)
                {
                    var go = new GameObject("--- FMS_UNIT_CCTV_MANAGER ---");
                    go.AddComponent<FMSUnitCctvManager>();
                }
            }
        }

        private void DrawUnitFloatingOverheadTags(float screenW, float screenH)
        {
            if (!showUnitOverheadTags || Camera.main == null) return;
            if (isBooting && showBootSplash) return;
            if (showCommandPalette || showAboutModal || showGuideModal || showLocationFilterModal || showUnitAssetModal || showUnitTagSettingsModal) return;
            if (FMSFleetManager.Instance == null || !FMSFleetManager.Instance.showFleet || FMSFleetManager.Instance.activeFleet == null) return;

            Camera cam = Camera.main;
            Vector3 camPos = cam.transform.position;
            float maxDist = tagMaxViewDistance > 100f ? tagMaxViewDistance : 1600f;
            const float TOP_NAV_SAFE_Y = 56f; // Strictly below top navigation bar (44px + buffer)

            foreach (var unit in FMSFleetManager.Instance.activeFleet)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;

                Vector3 unitWorldPos = unit.VisualWorldPosition;
                float distToCam = Vector3.Distance(camPos, unitWorldPos);
                if (distToCam > maxDist || distToCam < 2.0f) continue;

                // Position tag dynamically above vehicle cab roof depending on model height
                float roofOffset = unit.unitType switch
                {
                    UnitType.WheelLoader => 4.8f,
                    UnitType.Bulldozer => 4.4f,
                    UnitType.Grader => 4.0f,
                    UnitType.FuelTruck => 4.0f,
                    UnitType.Excavator => 6.2f,
                    _ => 5.4f // HaulTruck
                };
                Vector3 overheadWorldPos = unitWorldPos + Vector3.up * roofOffset;
                Vector3 screenPoint = cam.WorldToScreenPoint(overheadWorldPos);

                // Check if in front of camera
                if (screenPoint.z <= 0.5f) continue;

                float screenX = screenPoint.x;
                float screenY = screenH - screenPoint.y; // Invert Y for GUI

                // Scale tag slightly by distance and user multiplier
                float distScale = Mathf.Clamp(1.0f - (distToCam / maxDist) * 0.45f, 0.70f, 1.0f);
                float finalScale = distScale * Mathf.Clamp(tagScaleMultiplier, 0.70f, 1.40f);

                bool isSelected = FMSFleetManager.Instance.selectedUnit == unit;
                bool isOff = !unit.isOnline || unit.currentState == UnitState.Offline;
                bool staleOff = isOff && unit.isLiveTelemetryControlled && FMSFleetManager.Instance.isTelemetryFeedStale;

                // If hideOfflineUnits is enabled, skip rendering tags for offline dummy units completely
                if (isOff && FMSFleetManager.Instance != null && FMSFleetManager.Instance.hideOfflineUnits && !isSelected) continue;

                // Build tag content dynamically according to user settings
                string tagLabel = BuildUnitTagString(unit, isSelected, isOff);
                string cleanText = StripRichText(tagLabel);

                float baseWidth = Mathf.Max(64f, cleanText.Length * 7.2f + 20f);
                float tagW = baseWidth * finalScale;
                float tagH = 26f * finalScale;
                float stemH = tagShowStemLine ? 10f * distScale : 0f;

                float tagY = screenY - tagH - stemH;

                // CRITICAL: Ensure tooltip is ALWAYS below the top navbar (never cover the navbar)
                if (tagY < TOP_NAV_SAFE_Y) continue;

                // Check horizontal & bottom screen bounds
                if (screenX < 60f || screenX > screenW - 60f || tagY > screenH - 60f) continue;

                // If dropdown is open on top-left, avoid overlapping dropdown area
                if (currentMenu != ActiveMenu.None && screenX < 360f && tagY < 420f) continue;

                Rect tagRect = new Rect(screenX - (tagW / 2f), tagY, tagW, tagH);

                // 1. Draw 3D Stem Line Anchor from Tag down to Vehicle Roof
                if (tagShowStemLine)
                {
                    Texture2D stemTex = isSelected ? (splashGlowEmeraldTex ?? tagStemTex) : tagStemTex;
                    if (stemTex != null)
                    {
                        GUI.DrawTexture(new Rect(screenX - 0.5f, tagY + tagH, 1f, stemH), stemTex);
                        GUI.DrawTexture(new Rect(screenX - 1.5f, screenY - 2f, 3f, 3f), stemTex);
                    }
                }

                // 2. Draw Tag Glassmorphic Background with Sleek Rounded Bevel & Glow
                Texture2D bgTex = isSelected ? tagBgSelectedTex : (isOff && !staleOff ? tagBgOfflineTex : tagBgNormalTex);
                if (bgTex != null)
                {
                    GUI.DrawTexture(tagRect, bgTex);
                }

                bool isTalkbackOnUnit = FMSFleetMessenger.Instance != null && 
                    ((FMSFleetMessenger.Instance.isCabinTalkbackActive && FMSFleetMessenger.Instance.cabinTalkbackSourceUnit.Equals(unit.unitId, StringComparison.OrdinalIgnoreCase)) ||
                     (FMSFleetMessenger.Instance.isTalkbackActive && (FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL" || FMSFleetMessenger.Instance.talkbackTargetUnit.Equals(unit.unitId, StringComparison.OrdinalIgnoreCase))));

                // 3. Draw Outer Specular Bevel Border (Red Pulsing for Talkback, Emerald for Selected, Red for Offline, Cyan for Normal)
                Texture2D borderTex = isTalkbackOnUnit ? (splashAlertBorderTex ?? splashGlowEmeraldTex) : (isSelected ? tagBorderSelectedTex : (isOff && !staleOff ? splashAlertBorderTex : splashGlowCyanTex));
                if (borderTex != null)
                {
                    GUI.DrawTexture(new Rect(tagRect.x, tagRect.y, tagRect.width, (isSelected || isTalkbackOnUnit) ? 2 : 1), borderTex);
                    GUI.DrawTexture(new Rect(tagRect.x, tagRect.y + tagRect.height - 1, tagRect.width, 1), borderTex);
                    GUI.DrawTexture(new Rect(tagRect.x, tagRect.y, 1, tagRect.height), borderTex);
                    GUI.DrawTexture(new Rect(tagRect.x + tagRect.width - 1, tagRect.y, 1, tagRect.height), borderTex);
                }

                // 4. Content Button with High-Contrast Typography & Transparent Backdrop
                GUIStyle tagBtnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(11 * finalScale), 9, 15),
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    normal = { textColor = Color.white },
                    hover = { textColor = new Color(0.0f, 0.95f, 1.0f) },
                    padding = new RectOffset(4, 4, 0, 0)
                };

                // Drop shadow for ultra-clear legibility against terrain/sky
                GUIStyle shadowStyle = new GUIStyle(tagBtnStyle)
                {
                    normal = { textColor = new Color(0f, 0f, 0f, 0.85f) }
                };
                GUI.Label(new Rect(tagRect.x + 1, tagRect.y + 1, tagRect.width, tagRect.height), cleanText, shadowStyle);

                // Interactive Tag Click (Left Click = Select & Chase, Right Click = Select & Open Context Menu / Talkback)
                Event e = Event.current;
                if (e != null && e.type == EventType.MouseDown && tagRect.Contains(e.mousePosition))
                {
                    if (e.button == 1) // Right Click
                    {
                        FMSFleetManager.Instance?.OpenContextMenu(unit, e.mousePosition);
                        e.Use();
                    }
                    else if (e.button == 0) // Left Click
                    {
                        FMSFleetManager.Instance?.SelectUnit(unit);
                        e.Use();
                    }
                }
                else
                {
                    string tagTooltip = unit.gpsProximityConflict
                        ? unit.visualSeparationMeters > 1f
                            ? $"Titik GPS asli tetap; model digeser {unit.visualSeparationMeters:F0} m agar tidak bertumpuk."
                            : "Beberapa unit memiliki titik GPS yang terlalu berdekatan."
                        : "";
                    GUI.Label(tagRect, new GUIContent(tagLabel, tagTooltip), tagBtnStyle);
                }
            }
        }

        private string BuildUnitTagString(FMSUnitController unit, bool isSelected, bool isOff)
        {
            List<string> parts = new List<string>();
            bool staleTelemetry = isOff && unit.isLiveTelemetryControlled &&
                FMSFleetManager.Instance != null && FMSFleetManager.Instance.isTelemetryFeedStale;

            // 0. Active Voice Radio / Talkback Status (High Priority Visual Wave)
            if (FMSFleetMessenger.Instance != null)
            {
                if (FMSFleetMessenger.Instance.isCabinTalkbackActive && 
                    FMSFleetMessenger.Instance.cabinTalkbackSourceUnit.Equals(unit.unitId, StringComparison.OrdinalIgnoreCase))
                {
                    string wave = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ " : " ▅ ▄ ▃ ▂ ";
                    parts.Add($"<color=#FF4D4D><b>🔴 🎙️ ON-AIR ({wave})</b></color>");
                }
                else if (FMSFleetMessenger.Instance.isTalkbackActive && 
                         (FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL" || FMSFleetMessenger.Instance.talkbackTargetUnit.Equals(unit.unitId, StringComparison.OrdinalIgnoreCase)))
                {
                    string wave = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ " : " ▅ ▄ ▃ ▂ ";
                    parts.Add($"<color=#00E5FF><b>🔊 RX RADIO ({wave})</b></color>");
                }
            }

            // 1. Icon & Name
            string nameSection = "";
            if (tagShowIcon)
            {
                string icon = unit.unitType switch
                {
                    UnitType.HaulTruck => unit.hasActualPayload && unit.payloadTons > 0 ? "🚚" : "🚛",
                    UnitType.Excavator => "⛏️",
                    UnitType.Bulldozer => "🚜",
                    UnitType.Grader => "🛣️",
                    UnitType.FuelTruck => "🛢️",
                    UnitType.WheelLoader => "🚜",
                    _ => "🚚"
                };
                nameSection += icon + " ";
            }
            if (tagShowName || string.IsNullOrEmpty(nameSection))
            {
                string displayName = !string.IsNullOrEmpty(unit.unitName) ? unit.unitName : (!string.IsNullOrEmpty(unit.unitId) ? unit.unitId : "UNIT");
                nameSection += $"<b>{displayName}</b>";
            }
            if (staleTelemetry) nameSection = "<color=#FFB800>●</color> " + nameSection;
            nameSection = nameSection.Trim();
            if (!string.IsNullOrEmpty(nameSection))
            {
                parts.Add(nameSection);
            }
            if (unit.gpsProximityConflict)
                parts.Add("<color=#FFB800>GPS RAPAT</color>");

            // 2. Speed
            if (tagShowSpeed)
            {
                if (staleTelemetry)
                {
                    if (isSelected) parts.Add("<color=#AEB9C2>GPS LAMA</color>");
                }
                else if (isOff)
                {
                    if (unit.isLiveTelemetryControlled && unit.backendLastHeardSeconds > 120)
                    {
                        int age = unit.backendLastHeardSeconds;
                        string ageText = age >= 86400 ? $"{age / 86400} hari" : age >= 3600 ? $"{age / 3600} jam" : $"{age / 60} menit";
                        parts.Add($"<color=#AEB9C2>GPS {ageText} lalu</color>");
                    }
                    else parts.Add("<color=#64748B>0.0 KM/Jam</color>");
                }
                else if (unit.currentSpeedKmh > 1.0f)
                {
                    parts.Add($"<color=#00FFA3>{unit.currentSpeedKmh:F1} KM/Jam</color>");
                }
                else
                {
                    parts.Add("<color=#FFB800>0.0 KM/Jam</color>");
                }
            }

            // 3. Status & Diagnostic Stoppage Indicator
            if (tagShowStatus)
            {
                if (isOff)
                {
                    if (!staleTelemetry)
                        parts.Add("<color=#FF4D4D>● OFFLINE</color>");
                }
                else if (unit.currentSpeedKmh > 1.0f)
                {
                    if (unit.currentState == UnitState.Hauling) parts.Add("<color=#00FFA3>● HAUL</color>");
                    else if (unit.currentState == UnitState.TravellingToLoad) parts.Add("<color=#00FFA3>● TRAVEL</color>");
                    else parts.Add($"<color=#00FFA3>● {unit.currentState.ToString().ToUpper()}</color>");
                }
                else
                {
                    // Stationary Unit: Distinguish Operational vs API Stale
                    if (unit.currentState == UnitState.Loading)
                    {
                        parts.Add("<color=#00E5FF>● LOAD</color>");
                    }
                    else if (unit.currentState == UnitState.QueueingAtPit)
                    {
                        parts.Add("<color=#FFB800>⏳ ANTRE PIT</color>");
                    }
                    else if (unit.currentState == UnitState.Dumping)
                    {
                        parts.Add("<color=#FFB800>● DUMP</color>");
                    }
                    else if (unit.currentState == UnitState.QueueingAtDump)
                    {
                        parts.Add("<color=#FFB800>⏳ ANTRE DUMP</color>");
                    }
                    else if (!unit.hasValidGpsFix)
                    {
                        parts.Add("<color=#94A3B8>⏳ MENUNGGU GPS</color>");
                    }
                    else
                    {
                        parts.Add("<color=#FFB800>⏳ STANDBY</color>");
                    }
                }
            }

            // 3.5 FTW SAVERA Health & Sleep Fatigue Status (Fit / Pengawasan / Unfit)
            if (tagShowFtwStatus)
            {
                EnsureFtwSaveraManager();
                if (FMSFtwSaveraManager.Instance != null)
                {
                    string ftwSnippet = FMSFtwSaveraManager.Instance.GetFtwTagSnippet(unit.unitId, tagShowSleepHours, unit.operatorName);
                    if (!string.IsNullOrEmpty(ftwSnippet))
                    {
                        parts.Add(ftwSnippet);
                    }
                }
            }

            // 4. Payload / Muatan (Isi)
            if (tagShowPayload)
            {
                if (unit.hasActualPayload)
                {
                    parts.Add($"<color=#00E5FF>⚖️ {unit.payloadTons:F0}T</color>");
                }
                else
                {
                    parts.Add("<color=#88A0B0>⚖️ --</color>");
                }
            }

            // 5. Operator Name
            if (tagShowOperator && !string.IsNullOrEmpty(unit.operatorName))
            {
                string[] nameParts = unit.operatorName.Split(' ');
                string shortName = nameParts.Length > 1 ? $"{nameParts[0]} {nameParts[1][0]}." : unit.operatorName;
                parts.Add($"👤 {shortName}");
            }

            // 6. Fuel %
            if (tagShowFuel)
            {
                parts.Add("⛽ --");
            }

            // 7. Activity / Assigned Shovel Loader
            if (tagShowActivity)
            {
                if (unit.unitType == UnitType.HaulTruck && !string.IsNullOrEmpty(unit.assignedLoaderId))
                {
                    parts.Add($"<color=#00FFA3>🚜 {unit.assignedLoaderId}</color>");
                }
                else if (!string.IsNullOrEmpty(unit.activityName))
                {
                    parts.Add($"({unit.activityName})");
                }
            }

            if (parts.Count == 0)
            {
                return $"<b>{unit.unitName}</b>";
            }

            string combined = string.Join(" | ", parts);
            if (isSelected)
            {
                combined = $"<color=#00E5FF>◀</color> {combined} <color=#00E5FF>▶</color>";
            }
            return combined;
        }

        private string BuildSampleTagPreview()
        {
            List<string> parts = new List<string>();
            string nameSec = "";
            if (tagShowIcon) nameSec += "🚚 ";
            if (tagShowName || string.IsNullOrEmpty(nameSec)) nameSec += "<b>RD05</b>";
            nameSec = nameSec.Trim();
            if (!string.IsNullOrEmpty(nameSec)) parts.Add(nameSec);

            if (tagShowSpeed) parts.Add("<color=#00FFA3>28k</color>");
            if (tagShowStatus) parts.Add("<color=#00FFA3>● HAUL</color>");
            if (tagShowFtwStatus)
            {
                if (tagShowSleepHours) parts.Add("<color=#00FFA3>🟢 FIT</color> <color=#00FFA3>💤 7.5j</color>");
                else parts.Add("<color=#00FFA3>🟢 FIT</color>");
            }
            if (tagShowPayload) parts.Add("<color=#88A0B0>⚖️ --</color>");
            if (tagShowOperator) parts.Add("👤 Ahmad S.");
            if (tagShowFuel) parts.Add("⛽ 82%");
            if (tagShowActivity) parts.Add("(Hauling Loaded)");

            if (parts.Count == 0) return "🚚 <b>RD05</b>";
            return string.Join(" | ", parts);
        }

        private string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        public void ApplyTagPreset(UnitTagPreset preset)
        {
            currentTagPreset = preset;
            switch (preset)
            {
                case UnitTagPreset.NameOnly:
                    tagShowIcon = true;
                    tagShowName = true;
                    tagShowSpeed = false;
                    tagShowStatus = false;
                    tagShowFtwStatus = false;
                    tagShowSleepHours = false;
                    tagShowPayload = false;
                    tagShowOperator = false;
                    tagShowFuel = false;
                    tagShowActivity = false;
                    break;
                case UnitTagPreset.Standard:
                    tagShowIcon = true;
                    tagShowName = true;
                    tagShowSpeed = true;
                    tagShowStatus = true;
                    tagShowFtwStatus = true;
                    tagShowSleepHours = true;
                    tagShowPayload = false;
                    tagShowOperator = false;
                    tagShowFuel = false;
                    tagShowActivity = false;
                    break;
                case UnitTagPreset.PayloadStatus:
                    tagShowIcon = true;
                    tagShowName = true;
                    tagShowSpeed = false;
                    tagShowStatus = true;
                    tagShowFtwStatus = true;
                    tagShowSleepHours = false;
                    tagShowPayload = true;
                    tagShowOperator = false;
                    tagShowFuel = false;
                    tagShowActivity = false;
                    break;
                case UnitTagPreset.FullTelemetry:
                    tagShowIcon = true;
                    tagShowName = true;
                    tagShowSpeed = true;
                    tagShowStatus = true;
                    tagShowFtwStatus = true;
                    tagShowSleepHours = true;
                    tagShowPayload = true;
                    tagShowOperator = true;
                    tagShowFuel = true;
                    tagShowActivity = true;
                    break;
                case UnitTagPreset.Custom:
                    break;
            }
            SaveTagSettings();
        }

        public void SaveTagSettings()
        {
            PlayerPrefs.SetInt("Virexa_Tag_Preset", (int)currentTagPreset);
            PlayerPrefs.SetInt("Virexa_Tag_ShowIcon", tagShowIcon ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowName", tagShowName ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowSpeed", tagShowSpeed ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowStatus", tagShowStatus ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowFtwStatus", tagShowFtwStatus ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowSleepHours", tagShowSleepHours ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowPayload", tagShowPayload ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowOperator", tagShowOperator ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowFuel", tagShowFuel ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_ShowActivity", tagShowActivity ? 1 : 0);
            PlayerPrefs.SetInt("Virexa_Tag_StemLine", tagShowStemLine ? 1 : 0);
            PlayerPrefs.SetFloat("Virexa_Tag_Scale", tagScaleMultiplier);
            PlayerPrefs.SetFloat("Virexa_Tag_MaxDist", tagMaxViewDistance);
            PlayerPrefs.Save();
        }

        public void LoadTagSettings()
        {
            if (PlayerPrefs.HasKey("Virexa_Tag_Preset"))
            {
                currentTagPreset = (UnitTagPreset)PlayerPrefs.GetInt("Virexa_Tag_Preset", (int)UnitTagPreset.Standard);
                tagShowIcon = PlayerPrefs.GetInt("Virexa_Tag_ShowIcon", 1) == 1;
                tagShowName = PlayerPrefs.GetInt("Virexa_Tag_ShowName", 1) == 1;
                tagShowSpeed = PlayerPrefs.GetInt("Virexa_Tag_ShowSpeed", 1) == 1;
                tagShowStatus = PlayerPrefs.GetInt("Virexa_Tag_ShowStatus", 1) == 1;
                tagShowFtwStatus = PlayerPrefs.GetInt("Virexa_Tag_ShowFtwStatus", 1) == 1;
                tagShowSleepHours = PlayerPrefs.GetInt("Virexa_Tag_ShowSleepHours", 1) == 1;
                tagShowPayload = PlayerPrefs.GetInt("Virexa_Tag_ShowPayload", 0) == 1;
                tagShowOperator = PlayerPrefs.GetInt("Virexa_Tag_ShowOperator", 0) == 1;
                tagShowFuel = PlayerPrefs.GetInt("Virexa_Tag_ShowFuel", 0) == 1;
                tagShowActivity = PlayerPrefs.GetInt("Virexa_Tag_ShowActivity", 0) == 1;
                tagShowStemLine = PlayerPrefs.GetInt("Virexa_Tag_StemLine", 1) == 1;
                tagScaleMultiplier = PlayerPrefs.GetFloat("Virexa_Tag_Scale", 1.0f);
                tagMaxViewDistance = PlayerPrefs.GetFloat("Virexa_Tag_MaxDist", 1600f);
            }
        }

        private void DrawUnitTagSettingsModal(float screenW, float screenH)
        {
            float modalW = 720f;
            float modalH = 650f;
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Modal Outer Window Glass Box
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);

            // Cyan Specular Header Line
            if (lineAccentTex != null)
            {
                GUI.DrawTexture(new Rect(x, y, modalW, 3), lineAccentTex);
            }

            // Modal Header Title
            GUIStyle modalHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };
            GUI.Label(new Rect(x + 20, y + 12, modalW - 60, 22), "🏷️ PENGATURAN LABEL & FILTER VISIBILITAS ARMADA 3D", modalHeaderStyle);

            if (GUI.Button(new Rect(x + modalW - 36, y + 10, 24, 22), "✕", quickDockBtnStyle))
            {
                showUnitTagSettingsModal = false;
                SaveTagSettings();
            }

            GUI.Label(new Rect(x + 20, y + 34, modalW - 40, 18), 
                "Atur informasi telemetri yang ditampilkan di label overhead 3D dan filter tipe armada yang aktif di viewport.", hintStyle);

            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 20, y + 54, modalW - 40, 1), topHighlightTex);
            }

            float curY = y + 62;

            // --- 1. PRESET CEPAT (QUICK PRESETS) ---
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "⚡ PRESET TAMPILAN CEPAT:", dropdownHeaderStyle);
            curY += 20;

            float pBtnW = (modalW - 52) / 4f;
            var presets = new (UnitTagPreset p, string label, string desc)[]
            {
                (UnitTagPreset.NameOnly, "🏷️ Nama Saja", "Hanya Icon + Nama Unit"),
                (UnitTagPreset.Standard, "⚡ Standar", "Nama + Speed + FTW + Status"),
                (UnitTagPreset.PayloadStatus, "📦 Muatan & Status", "Nama + Tonase + FTW"),
                (UnitTagPreset.FullTelemetry, "🛰️ Telemetri Penuh", "Lengkap Semua Data")
            };

            for (int i = 0; i < presets.Length; i++)
            {
                bool isActive = currentTagPreset == presets[i].p;
                GUIStyle pStyle = isActive ? navBtnActiveStyle : navBtnStyle;
                if (GUI.Button(new Rect(x + 20 + i * (pBtnW + 4), curY, pBtnW, 28), presets[i].label, pStyle))
                {
                    ApplyTagPreset(presets[i].p);
                }
            }
            curY += 34;

            // --- 2. LIVE INTERACTIVE PREVIEW PANEL ---
            float prevBoxW = modalW - 40;
            float prevBoxH = 64;
            GUI.Box(new Rect(x + 20, curY, prevBoxW, prevBoxH), GUIContent.none, dropdownPanelStyle);

            GUI.Label(new Rect(x + 28, curY + 6, prevBoxW - 16, 16), "👁️ <b>SIMULASI PRATINJAU DESAIN LABEL 3D (LIVE PREVIEW):</b>", dropdownHeaderStyle);

            // Mock preview tag
            string previewText = BuildSampleTagPreview();
            float previewScale = Mathf.Clamp(tagScaleMultiplier, 0.75f, 1.35f);
            float pTagW = Mathf.Max(90f, (StripRichText(previewText).Length * 7.2f + 24f)) * previewScale;
            float pTagH = 26f * previewScale;
            float pTagX = x + 20 + (prevBoxW - pTagW) / 2f;
            float pTagY = curY + 28;

            Rect pTagRect = new Rect(pTagX, pTagY, pTagW, pTagH);
            if (tagBgNormalTex != null) GUI.DrawTexture(pTagRect, tagBgNormalTex);
            if (splashGlowCyanTex != null)
            {
                GUI.DrawTexture(new Rect(pTagRect.x, pTagRect.y, pTagRect.width, 1), splashGlowCyanTex);
                GUI.DrawTexture(new Rect(pTagRect.x, pTagRect.y + pTagRect.height - 1, pTagRect.width, 1), splashGlowCyanTex);
                GUI.DrawTexture(new Rect(pTagRect.x, pTagRect.y, 1, pTagRect.height), splashGlowCyanTex);
                GUI.DrawTexture(new Rect(pTagRect.x + pTagRect.width - 1, pTagRect.y, 1, pTagRect.height), splashGlowCyanTex);
            }

            GUIStyle pBtnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(11 * previewScale), 9, 15),
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(pTagRect, previewText, pBtnStyle);

            curY += prevBoxH + 10;

            // --- 3. GRANULAR FIELD TOGGLES (TWO COLUMNS) ---
            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "⚙️ ELEMEN DATA YANG DITAMPILKAN DI LABEL:", dropdownHeaderStyle);
            curY += 20;

            float colW = (modalW - 50) / 2f;
            float col1X = x + 20;
            float col2X = x + 20 + colW + 10;
            float rowH = 24f;

            // Column 1
            DrawTagToggleOption(col1X, curY + 0 * rowH, colW, ref tagShowIcon, "🚚 Icon Jenis Unit", "Icon Shovel, DT, Dozer, Grader, dll.");
            DrawTagToggleOption(col1X, curY + 1 * rowH, colW, ref tagShowName, "🏷️ Nama / ID Unit", "Nomor lambung unit (contoh: RD5100 / RD5091)");
            DrawTagToggleOption(col1X, curY + 2 * rowH, colW, ref tagShowSpeed, "⚡ Kecepatan Unit", "Speed real-time (contoh: 28.5 KM/Jam)");
            DrawTagToggleOption(col1X, curY + 3 * rowH, colW, ref tagShowStatus, "🚦 Status Operasional & Antrean", "Status HAUL, LOAD, DUMP, ANTRE PIT, OFF");
            DrawTagToggleOption(col1X, curY + 4 * rowH, colW, ref tagShowFtwStatus, "🩺 Status FTW SAVERA", "Status K3: 🟢 Fit / 🟡 Pengawasan / 🔴 Unfit");

            // Column 2
            DrawTagToggleOption(col2X, curY + 0 * rowH, colW, ref tagShowPayload, "📦 Isi / Muatan (Payload)", "Tonase muatan (contoh: 95.0 T / Kosong)");
            DrawTagToggleOption(col2X, curY + 1 * rowH, colW, ref tagShowOperator, "👤 Nama Operator", "Nama driver / operator di lapangan");
            DrawTagToggleOption(col2X, curY + 2 * rowH, colW, ref tagShowFuel, "⛽ Level Bahan Bakar (Solar)", "Persentase sisa tangki solar (%)");
            DrawTagToggleOption(col2X, curY + 3 * rowH, colW, ref tagShowActivity, "📝 Keterangan Aktivitas", "Keterangan rute (contoh: Hauling Loaded)");
            DrawTagToggleOption(col2X, curY + 4 * rowH, colW, ref tagShowSleepHours, "💤 Total Jam Tidur Operator", "Total jam tidur riil operator (contoh: 💤 7.5j)");

            curY += 5 * rowH + 8;

            // Direct button to open full FTW SAVERA Matrix Modal
            EnsureFtwSaveraManager();
            if (GUI.Button(new Rect(x + 20, curY, modalW - 40, 26), "🩺 Buka evaluasi K3 dan fatigue operator...", navBtnActiveStyle))
            {
                FMSFtwSaveraManager.Instance.isFtwModalOpen = true;
                showUnitTagSettingsModal = false;
            }
            curY += 32;

            // --- 4. FLEET VISIBILITY FILTERS ---
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 20, curY, modalW - 40, 1), topHighlightTex);
            }
            curY += 6;

            GUI.Label(new Rect(x + 20, curY, modalW - 40, 18), "🚜 FILTER TIPE ARMADA YANG DITAMPILKAN DI VIEWPORT:", dropdownHeaderStyle);
            curY += 20;

            if (FMSFleetManager.Instance != null)
            {
                var fm = FMSFleetManager.Instance;
                float fColW = (modalW - 60) / 3f;
                float fRowH = 24f;
                bool hasFleet = fm.activeFleet.Count > 0;
                string haulCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && u.unitType == UnitType.HaulTruck).Count} unit" : "--";
                string excavatorCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && u.unitType == UnitType.Excavator).Count} unit" : "--";
                string dozerCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && u.unitType == UnitType.Bulldozer).Count} unit" : "--";
                string graderCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && u.unitType == UnitType.Grader).Count} unit" : "--";
                string loaderCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && u.unitType == UnitType.WheelLoader).Count} unit" : "--";
                string supportCount = hasFleet ? $"{fm.activeFleet.FindAll(u => u != null && (u.unitType == UnitType.FuelTruck || u.unitType == UnitType.Support)).Count} unit" : "--";

                DrawFleetFilterToggle(x + 20 + 0 * (fColW + 10), curY + 0 * fRowH, fColW, ref fm.filterShowHaulTrucks, "🚚 Dump Truck", haulCount);
                DrawFleetFilterToggle(x + 20 + 1 * (fColW + 10), curY + 0 * fRowH, fColW, ref fm.filterShowExcavators, "⛏️ Excavator", excavatorCount);
                DrawFleetFilterToggle(x + 20 + 2 * (fColW + 10), curY + 0 * fRowH, fColW, ref fm.filterShowBulldozers, "🚜 Bulldozer", dozerCount);

                DrawFleetFilterToggle(x + 20 + 0 * (fColW + 10), curY + 1 * fRowH, fColW, ref fm.filterShowGraders, "🛣️ Motor Grader", graderCount);
                DrawFleetFilterToggle(x + 20 + 1 * (fColW + 10), curY + 1 * fRowH, fColW, ref fm.filterShowWheelLoaders, "🚜 Wheel Loader", loaderCount);
                DrawFleetFilterToggle(x + 20 + 2 * (fColW + 10), curY + 1 * fRowH, fColW, ref fm.filterShowFuelTrucks, "🛢️ Fuel/Support", supportCount);

                curY += 2 * fRowH + 4;

                // Quick Filter Buttons
                float qfW = (modalW - 60) / 3f;
                if (GUI.Button(new Rect(x + 20, curY, qfW, 22), "✅ Tampilkan Semua", navBtnStyle))
                {
                    fm.filterShowHaulTrucks = true;
                    fm.filterShowExcavators = true;
                    fm.filterShowBulldozers = true;
                    fm.filterShowGraders = true;
                    fm.filterShowWheelLoaders = true;
                    fm.filterShowFuelTrucks = true;
                    fm.ApplyFleetCategoryFilters();
                }
                if (GUI.Button(new Rect(x + 30 + qfW, curY, qfW, 22), "🚚 Hanya Truk & Shovel", navBtnStyle))
                {
                    fm.filterShowHaulTrucks = true;
                    fm.filterShowExcavators = true;
                    fm.filterShowBulldozers = false;
                    fm.filterShowGraders = false;
                    fm.filterShowWheelLoaders = false;
                    fm.filterShowFuelTrucks = false;
                    fm.ApplyFleetCategoryFilters();
                }
                string hideOffBtnTxt = fm.hideOfflineUnits ? "✅ Unit Offline: Sembunyi" : "⬜ Sembunyikan Offline";
                if (GUI.Button(new Rect(x + 40 + qfW * 2, curY, qfW, 22), hideOffBtnTxt, navBtnStyle))
                {
                    fm.ToggleHideOfflineUnits();
                }
                curY += 28;
                string archiveBtnText = fm.showArchivedUnits ? "☑ Tampilkan arsip GPS >24 jam" : "□ Tampilkan arsip GPS >24 jam";
                if (GUI.Button(new Rect(x + 20, curY, modalW - 40, 22), archiveBtnText, dropdownItemStyle))
                {
                    fm.ToggleShowArchivedUnits();
                }
                curY += 28;
            }

            // --- 5. VISUAL & DISPLAY PARAMETERS (SLIDERS) ---
            if (topHighlightTex != null)
            {
                GUI.DrawTexture(new Rect(x + 20, curY, modalW - 40, 1), topHighlightTex);
            }
            curY += 6;

            GUI.Label(new Rect(x + 20, curY, 160, 18), "📏 <b>Skala Ukuran Label:</b>", hintStyle);
            GUI.Label(new Rect(x + 180, curY, 45, 18), $"{tagScaleMultiplier:F2}x", coordStyle);
            float newScale = GUI.HorizontalSlider(new Rect(x + 230, curY + 3, 270, 18), tagScaleMultiplier, 0.70f, 1.40f);
            if (Mathf.Abs(newScale - tagScaleMultiplier) > 0.01f)
            {
                tagScaleMultiplier = Mathf.Round(newScale * 100f) / 100f;
                currentTagPreset = UnitTagPreset.Custom;
            }
            if (GUI.Button(new Rect(x + 510, curY - 2, 190, 22), "🔄 Reset Skala 1.0x", navBtnStyle))
            {
                tagScaleMultiplier = 1.0f;
            }
            curY += 24;

            GUI.Label(new Rect(x + 20, curY, 160, 18), "👁️ <b>Jarak Pandang Maks:</b>", hintStyle);
            GUI.Label(new Rect(x + 180, curY, 45, 18), $"{tagMaxViewDistance:F0}m", coordStyle);
            float newDist = GUI.HorizontalSlider(new Rect(x + 230, curY + 3, 270, 18), tagMaxViewDistance, 300f, 3000f);
            if (Mathf.Abs(newDist - tagMaxViewDistance) > 10f)
            {
                tagMaxViewDistance = Mathf.Round(newDist / 50f) * 50f;
            }
            if (GUI.Button(new Rect(x + 510, curY - 2, 190, 22), "🔄 Reset 1600m", navBtnStyle))
            {
                tagMaxViewDistance = 1600f;
            }
            curY += 24;

            // Stem Line Toggle
            string stemTxt = tagShowStemLine ? "✅ 📍 Tampilkan Garis Jangkar 3D ke Atap Unit" : "⬜ 📍 Garis Jangkar 3D Dinonaktifkan";
            if (GUI.Button(new Rect(x + 20, curY, 380, 22), stemTxt, dropdownItemStyle))
            {
                tagShowStemLine = !tagShowStemLine;
            }

            // --- 6. FOOTER BUTTONS ---
            // --- 6. FOOTER BUTTONS ---
            float footY = y + modalH - 38;
            if (GUI.Button(new Rect(x + 20, footY, 140, 28), "🔄 Reset Standar", navBtnStyle))
            {
                ApplyTagPreset(UnitTagPreset.Standard);
                tagScaleMultiplier = 1.0f;
                tagMaxViewDistance = 1600f;
                tagShowStemLine = true;
                if (FMSFleetManager.Instance != null)
                {
                    var fm = FMSFleetManager.Instance;
                    fm.filterShowHaulTrucks = true;
                    fm.filterShowExcavators = true;
                    fm.filterShowBulldozers = true;
                    fm.filterShowGraders = true;
                    fm.filterShowWheelLoaders = true;
                    fm.filterShowFuelTrucks = true;
                    fm.ApplyFleetCategoryFilters();
                }
                SaveTagSettings();
                ShowNotification("🔄 Pengaturan Label & Filter dikembalikan ke standar");
            }

            GUIStyle autoSaveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 1.0f, 0.65f) }
            };
            GUI.Label(new Rect(x + 180, footY + 4, modalW - 320, 20), "Pengaturan disimpan saat modal ditutup", autoSaveStyle);

            if (GUI.Button(new Rect(x + modalW - 130, footY, 110, 28), "✓ Selesai", navBtnActiveStyle))
            {
                SaveTagSettings();
                FMSFleetManager.Instance?.ApplyFleetCategoryFilters();
                showUnitTagSettingsModal = false;
            }
        }

        private void DrawFleetFilterToggle(float posX, float posY, float width, ref bool filterVal, string label, string countTxt)
        {
            string prefix = filterVal ? "✅" : "⬜";
            string btnTxt = $"{prefix} <b>{label}</b> <color=#00E5FF>({countTxt})</color>";
            GUIStyle optStyle = new GUIStyle(dropdownItemStyle)
            {
                fontSize = 11,
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };

            if (GUI.Button(new Rect(posX, posY, width, 22), btnTxt, optStyle))
            {
                filterVal = !filterVal;
                FMSFleetManager.Instance?.ApplyFleetCategoryFilters();
            }
        }

        private void DrawTagToggleOption(float posX, float posY, float width, ref bool toggleVal, string label, string tooltip)
        {
            string prefix = toggleVal ? "✅" : "⬜";
            string btnTxt = $"{prefix} <b>{label}</b>";
            GUIStyle optStyle = new GUIStyle(dropdownItemStyle)
            {
                fontSize = 11,
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };

            if (GUI.Button(new Rect(posX, posY, width, 24), btnTxt, optStyle))
            {
                toggleVal = !toggleVal;
                currentTagPreset = UnitTagPreset.Custom;
            }
        }

        private void DrawCleanBootSplashScreen(float screenW, float screenH)
        {
            Color oldColor = GUI.color;
            if (bootSiteOrtho == null && siteId == "astha")
            {
                bootSiteOrtho = Resources.Load<Texture2D>("BootSiteOrtho");
                autoLoadedSitePreview = bootSiteOrtho != null;
            }
            if (!bootLogoLoadAttempted)
            {
                bootLogoLoadAttempted = true;
                if (siteId == "astha" && (companyLogo == null || companyLogo.name == "AsthaVirexaLogo"))
                {
                    TextAsset logoBytes = Resources.Load<TextAsset>("AsthaVirexaLogoRaw");
                    if (logoBytes != null)
                    {
                        decodedCompanyLogo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (ImageConversion.LoadImage(decodedCompanyLogo, logoBytes.bytes, true))
                        {
                            decodedCompanyLogo.filterMode = FilterMode.Bilinear;
                            decodedCompanyLogo.wrapMode = TextureWrapMode.Clamp;
                            companyLogo = decodedCompanyLogo;
                        }
                        else
                        {
                            Destroy(decodedCompanyLogo);
                            decodedCompanyLogo = null;
                        }
                    }
                    if (companyLogo == null) companyLogo = Resources.Load<Texture2D>("AsthaVirexaLogo");
                    autoLoadedCompanyLogo = companyLogo != null;
                }
            }
            if (bootSiteOrtho != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), bootSiteOrtho, ScaleMode.ScaleAndCrop);
            }
            GUI.color = new Color(0.035f, 0.065f, 0.068f, bootSiteOrtho != null ? 0.82f : 1f);
            GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float contentW = Mathf.Min(640f, screenW - 40f);
            float x = (screenW - contentW) * 0.5f;
            float top = Mathf.Max(28f, (screenH - 400f) * 0.5f);
            Color textColor = new Color(0.96f, 0.98f, 0.97f);
            Color mutedColor = new Color(0.70f, 0.77f, 0.76f);
            Color accentColor = new Color(0.32f, 0.82f, 0.70f);
            Color warningColor = new Color(0.95f, 0.68f, 0.30f);
            bool compactIdentity = x < 270f;
            float logoSize = compactIdentity ? 88f : Mathf.Min(320f, x - 48f);
            Rect logoRect = compactIdentity
                ? new Rect(x, top + 4f, logoSize, logoSize)
                : new Rect(x - logoSize - 28f, top - 12f, logoSize, logoSize);
            if (companyLogo != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(logoRect, companyLogo, ScaleMode.ScaleToFit, false);
                GUI.color = oldColor;
            }
            else
            {
                GUIStyle logoPlaceholderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = compactIdentity ? 28 : 64,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = accentColor }
                };
                GUI.color = Color.white;
                GUI.Label(logoRect, "AV", logoPlaceholderStyle);
                GUI.color = oldColor;
            }

            float identityX = compactIdentity ? x + logoSize + 16f : x;
            float identityW = compactIdentity ? contentW - logoSize - 16f : contentW;

            GUIStyle eyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = compactIdentity ? 10 : 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accentColor }
            };
            float nameWidth = eyebrowStyle.CalcSize(new GUIContent(companyDisplayName)).x;
            if (nameWidth > identityW)
                eyebrowStyle.fontSize = Mathf.Max(8, Mathf.FloorToInt(eyebrowStyle.fontSize * identityW / nameWidth));
            GUI.Label(new Rect(identityX, top, identityW, 22f), companyDisplayName, eyebrowStyle);

            GUIStyle brandStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = compactIdentity ? Mathf.Min(27, Mathf.FloorToInt(identityW / 8f)) : 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = textColor }
            };
            GUI.Label(new Rect(identityX, top + 24f, identityW, 62f), "VIREXA <color=#52D1B2>ONE</color>", brandStyle);

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = compactIdentity ? 12 : 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = mutedColor }
            };
            GUI.Label(new Rect(identityX, top + 90f, identityW, 26f), "Ruang kendali operasi tambang", subtitleStyle);

            bool connected = bootApiState == BootApiProbeState.Connected;
            bool feedStale = FMSFleetManager.Instance == null || FMSFleetManager.Instance.isTelemetryFeedStale;
            bool failed = bootApiState == BootApiProbeState.FailedTimeout;
            bool ready = (connected || bootApiState == BootApiProbeState.OfflineBypassed) && bootProgress >= 0.98f;
            string phase = failed ? "Telemetri belum tersedia"
                : bootProgress < 0.43f ? "Menyiapkan peta operasi"
                : bootProgress < 0.68f ? "Memuat area tambang"
                : bootProgress < 0.98f ? "Menyiapkan armada"
                : "Ruang kendali siap";

            GUIStyle phaseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor }
            };
            GUIStyle percentStyle = new GUIStyle(phaseStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = mutedColor }
            };
            float progressY = top + 168f;
            GUI.Label(new Rect(x, progressY, contentW - 70f, 20f), phase, phaseStyle);
            GUI.Label(new Rect(x + contentW - 70f, progressY, 70f, 20f),
                $"{Mathf.RoundToInt(bootProgress * 100f)}%", percentStyle);

            float barY = progressY + 32f;
            GUI.color = new Color(0.27f, 0.35f, 0.34f);
            GUI.DrawTexture(new Rect(x, barY, contentW, 5f), Texture2D.whiteTexture);
            float fillW = contentW * Mathf.Clamp01(bootProgress);
            GUI.color = failed ? warningColor : accentColor;
            if (fillW > 0f) GUI.DrawTexture(new Rect(x, barY, fillW, 5f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = mutedColor }
            };
            float statusY = barY + 27f;
            float statusW = contentW / 3f;
            bool mapReady = FMSMining3DLayer.Instance != null && FMSMining3DLayer.Instance.isLoaded;
            bool fleetReady = FMSFleetManager.Instance != null;
            string[] statusTexts = { "Peta area", "Armada", "Telemetri" };
            Color[] statusColors = {
                mapReady ? accentColor : mutedColor,
                fleetReady ? accentColor : mutedColor,
                connected ? (feedStale ? warningColor : accentColor) : failed ? warningColor : mutedColor
            };
            for (int i = 0; i < statusTexts.Length; i++)
            {
                float itemX = x + statusW * i;
                GUI.color = statusColors[i];
                GUI.DrawTexture(new Rect(itemX, statusY + 7f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color = oldColor;
                GUI.Label(new Rect(itemX + 13f, statusY, statusW - 13f, 20f), statusTexts[i], statusStyle);
            }

            string connectionNote = connected
                ? feedStale ? "Pembaruan GPS tertunda. Peta tetap tersedia." : "Data operasional terhubung."
                : failed ? "Koneksi data belum tersedia. Peta tetap dapat dibuka."
                : "Menyambungkan data operasional...";
            GUIStyle noteStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = (connected && feedStale) || failed ? warningColor : mutedColor }
            };
            GUI.Label(new Rect(x, statusY + 40f, contentW, 26f), connectionNote, noteStyle);

            float buttonY = statusY + 84f;
            GUIStyle actionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = bootPrimaryButtonTex, textColor = new Color(0.035f, 0.11f, 0.105f) },
                hover = { background = bootPrimaryButtonHoverTex, textColor = new Color(0.035f, 0.11f, 0.105f) },
                active = { background = bootPrimaryButtonHoverTex, textColor = new Color(0.035f, 0.11f, 0.105f) }
            };
            if (ready)
            {
                float buttonW = Mathf.Min(240f, contentW);
                if (GUI.Button(new Rect(x, buttonY, buttonW, 46f), "Masuk ke peta operasi", actionStyle))
                {
                    bootProgress = 1f;
                    isBooting = false;
                }
            }
            else if (failed)
            {
                float gap = 12f;
                bool stackedButtons = contentW < 450f;
                float buttonW = stackedButtons ? contentW : (contentW - gap) * 0.5f;
                if (GUI.Button(new Rect(x, buttonY, buttonW, 46f), "Coba lagi", actionStyle))
                {
                    RetryApiConnection();
                    bootTimer = 0f;
                    bootApiState = BootApiProbeState.Probing;
                }

                GUIStyle secondaryStyle = new GUIStyle(actionStyle)
                {
                    normal = { background = bootSecondaryButtonTex, textColor = textColor },
                    hover = { background = bootSecondaryButtonTex, textColor = textColor },
                    active = { background = bootSecondaryButtonTex, textColor = textColor }
                };
                float secondaryX = stackedButtons ? x : x + buttonW + gap;
                float secondaryY = stackedButtons ? buttonY + 46f + gap : buttonY;
                if (GUI.Button(new Rect(secondaryX, secondaryY, buttonW, 46f), "Buka tanpa telemetri", secondaryStyle))
                {
                    bootApiState = BootApiProbeState.OfflineBypassed;
                    bootProgress = 1f;
                    isBooting = false;
                }
            }
        }


        private void DrawApiDisconnectedBanner(float screenW, float screenH)
        {
            float alertW = Mathf.Min(680f, screenW - 30f);
            float alertH = 96f;
            float alertX = (screenW - alertW) / 2f;
            float alertY = 52f;

            // Background & Border
            if (splashAlertBgTex != null)
            {
                GUI.DrawTexture(new Rect(alertX, alertY, alertW, alertH), splashAlertBgTex);
            }
            if (splashAlertBorderTex != null)
            {
                GUI.DrawTexture(new Rect(alertX, alertY, alertW, 2), splashAlertBorderTex);
                GUI.DrawTexture(new Rect(alertX, alertY + alertH - 2, alertW, 2), splashAlertBorderTex);
                GUI.DrawTexture(new Rect(alertX, alertY, 2, alertH), splashAlertBorderTex);
                GUI.DrawTexture(new Rect(alertX + alertW - 2, alertY, 2, alertH), splashAlertBorderTex);
            }

            // Header & Dismiss Button
            GUI.Label(new Rect(alertX + 16, alertY + 8, alertW - 60, 22), 
                "⚠️ <b>PERINGATAN: LAYANAN TELEMETRI CLOUD BELUM TERHUBUNG</b> <color=#FF8888>(OFFLINE)</color>", alertHeaderStyle);

            if (GUI.Button(new Rect(alertX + alertW - 32, alertY + 8, 24, 22), "✕", quickDockBtnStyle))
            {
                dismissApiWarning = true;
            }

            // Body Explanation (No domain or IP exposed)
            string bodyText = "Layanan telemetri armada cloud belum terhubung. Pergerakan real-time GPS unit armada dan rekaman produksi membutuhkan koneksi cloud aktif.\n<i>Petunjuk: Pastikan reverse proxy tunnel aktif atau jalankan backend server.</i>";
            GUI.Label(new Rect(alertX + 16, alertY + 30, alertW - 32, 36), bodyText, alertBodyStyle);

            // Action Buttons
            if (GUI.Button(new Rect(alertX + 16, alertY + 68, 200, 22), "🔄 Coba Hubungkan Ulang", badgeOfflineStyle ?? navBtnActiveStyle))
            {
                RetryApiConnection();
                ShowNotification("🔄 Memeriksa kembali koneksi telemetri cloud...");
            }

            if (GUI.Button(new Rect(alertX + 224, alertY + 68, 175, 22), "✕ Sembunyikan Pesan Ini", navBtnStyle))
            {
                dismissApiWarning = true;
            }
        }

        public void RetryApiConnection()
        {
            AbortActiveApiRequest();
            StopAllCoroutines();
            StartCoroutine(CheckApiHealthLoop());
            if (FMSMining3DLayer.Instance != null && !FMSMining3DLayer.Instance.isLoaded)
            {
                StartCoroutine(FMSMining3DLayer.Instance.FetchAllMiningLayers());
            }
        }

        private void DestroyAllProceduralTextures()
        {
            SafeDestroyTexture(ref navDarkBgTex);
            SafeDestroyTexture(ref panelBgTex);
            SafeDestroyTexture(ref dropdownBgTex);
            SafeDestroyTexture(ref btnHoverTex);
            SafeDestroyTexture(ref btnActiveTex);
            SafeDestroyTexture(ref badgeSuccessBgTex);
            SafeDestroyTexture(ref badgeOfflineBgTex);
            SafeDestroyTexture(ref compassDialTex);
            SafeDestroyTexture(ref compassNeedleTex);
            SafeDestroyTexture(ref lineAccentTex);
            SafeDestroyTexture(ref bootPrimaryButtonTex);
            SafeDestroyTexture(ref bootPrimaryButtonHoverTex);
            SafeDestroyTexture(ref bootSecondaryButtonTex);
            SafeDestroyTexture(ref dropShadowTex);
            SafeDestroyTexture(ref topHighlightTex);
            SafeDestroyTexture(ref rowActiveTex);
            SafeDestroyTexture(ref rowInactiveTex);
            SafeDestroyTexture(ref rowRoadActiveTex);
            SafeDestroyTexture(ref splashCardBgTex);
            SafeDestroyTexture(ref splashAlertBgTex);
            SafeDestroyTexture(ref splashAlertBorderTex);
            SafeDestroyTexture(ref tagBgNormalTex);
            SafeDestroyTexture(ref tagBgSelectedTex);
            SafeDestroyTexture(ref tagBgOfflineTex);
            SafeDestroyTexture(ref tagStemTex);
            SafeDestroyTexture(ref tagBorderSelectedTex);
        }

        private void OnDestroy()
        {
            DestroyAllProceduralTextures();
        }

        private void SafeDestroyTexture(ref Texture2D tex)
        {
            if (tex != null)
            {
                DestroyImmediate(tex);
                tex = null;
            }
        }
    }
}
