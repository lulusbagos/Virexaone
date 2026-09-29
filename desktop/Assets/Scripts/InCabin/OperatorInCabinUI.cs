using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Ultra-Futuristic Flutter-Grade In-Cabin Mining FMS Tablet Cockpit HUD.
    /// Features:
    /// - High-DPI Anti-Aliased Modern Typography with dynamic OS font loading (Roboto, Segoe UI, SF Pro)
    /// - True Volumetric 3D Extruded Aeronautical Navigation Chevron Arrow with dynamic specular lighting
    /// - Tactile Flutter-Style Neumorphic / Glassmorphic Action & Stepper Cards with luminous LED indicators
    /// - High-Tech Holographic Concentric Radar & Compass Horizon Dial HUD
    /// - Responsive Auto-Scaling for all mobile & tablet aspect ratios
    /// - Interactive Touch PIN Numeric Keypad on Login
    /// - Real-Time Gauge Telemetry (Speed, Fuel, Payload, Engine/Trans Temp Bars, Tire Temp, Slope Grade)
    /// - Live Bidirectional Voice Dispatch Radio & Talkback PTT
    /// </summary>
    public class OperatorInCabinUI : MonoBehaviour
    {
        public static OperatorInCabinUI Instance { get; private set; }

        [Header("Operator & Login Credentials")]
        public bool isLoggedIn = false;
        public string operatorNik = "OPR-88421";
        public string operatorName = "Budi Santoso";
        public string operatorPin = "";
        public string defaultPin = "1234";
        public string selectedUnitId = "RD5105";
        public string selectedUnitModel = "CAT 777E (Off-Highway Truck)";
        public string selectedLocation = "Pit B Selatan";
        public string selectedShift = "Shift 1 (Pagi)";
        public string unitStatusDesc = "Siap Operasi";

        [Header("Operational State & Target Telemetry")]
        public UnitState currentState = UnitState.TravellingToLoad;
        public OperatorNavigationCompass navCompass;
        public string activeAssignedLoader = "EXCAVATOR-012";
        public string activeAssignedLoaderType = "CAT 6060";
        public string activeAssignedDisposal = "Disposal Barat #2";
        
        [Header("Vehicle Real-Time Telemetry")]
        public float currentSpeedKmh = 28.0f;
        public float fuelPercent = 65.0f;
        public float activePayloadTons = 0.0f;
        public float maxPayloadCapacityTons = 95.0f;
        public float engineTempC = 86.0f;
        public float transTempC = 92.0f;
        public string tireTempStatus = "Normal (68°C)";
        public float roadGradePercent = 2.1f;

        [Header("Modals & Dialogs")]
        public bool showRadioChatModal = false;
        public bool showDelayPickerModal = false;
        public bool showBahayaConfirmModal = false;
        public bool hasActiveDispatchAlert = false;
        public string dispatchAlertText = "";
        public string dispatchNewTarget = "";
        public string customReplyText = "";
        public string currentDelayReason = "";
        public bool isUnderDelay = false;

        private Vector2 cabinChatScrollPos = Vector2.zero;
        private float compassAnimAngle = 0f;
        private float forwardPulseTime = 0f;
        private int selectedOperatorIndex = 0;
        private readonly string[] demoOperators = {
            "OPR-88421 - Budi Santoso",
            "OPR-77219 - Agus Wijaya",
            "OPR-99104 - Bambang S.",
            "OPR-66520 - Dedi Supriyadi"
        };

        // Procedural Textures (Smooth Anti-Aliased & Tactile)
        private Texture2D bgFuturisticDarkTex;
        private Texture2D glassCard3DTex;
        private Texture2D glassHeader3DTex;
        private Texture2D glassPillTex;
        private Texture2D gaugeBarBgTex;
        private Texture2D gaugeBarFillTex;
        
        // 3D Tactile Action & State Textures
        private Texture2D btn3DNormalTex;
        private Texture2D btn3DActiveGreenTex;
        private Texture2D btn3DRedTex;
        private Texture2D btn3DAmberTex;
        private Texture2D btn3DBlueTex;
        private Texture2D btn3DOrangeTex;
        private Texture2D btn3DGreyTex;
        
        private Texture2D arrow3DVolumetricTex;
        private Texture2D radarCompassGridTex;
        private Texture2D miniMap3DBevelTex;
        private Texture2D roundNotificationBadgeTex;

        // Fonts
        private static Font modernSansFont;
        private static Font modernBoldFont;
        private float lastScreenHeight = 0f;

        // Dedicated Pre-allocated GUIStyles
        private GUIStyle topHeaderStyle;
        private GUIStyle topHeaderCenterStyle;
        private GUIStyle topPillTextStyle;
        private GUIStyle clockStyle;
        private GUIStyle loginTitleStyle;
        
        private GUIStyle cardTitleBoldStyle;
        private GUIStyle cardSublabelStyle;
        private GUIStyle cardActiveTitleStyle;
        
        private GUIStyle bigDistBadgeStyle;
        private GUIStyle bigDistSubStyle;
        private GUIStyle navTargetBoldStyle;
        private GUIStyle navSubStyle;

        private GUIStyle badgeStyle;
        private GUIStyle bottomStatusPillStyle;
        private GUIStyle keypadBtnStyle;
        private GUIStyle pinFieldStyle;
        private GUIStyle labelHeaderStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            LoadModernFonts();
            InitTextures();
        }

        private void Start()
        {
            BindToUnit(selectedUnitId);
        }

        private void Update()
        {
            forwardPulseTime += Time.deltaTime * 2.2f;
            UpdateTargetDestination();
        }

        private void LoadModernFonts()
        {
            if (modernSansFont == null)
            {
                string[] preferredFonts = { "Segoe UI", "Roboto", "SF Pro Display", "Ubuntu", "Montserrat", "Arial" };
                modernSansFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 16);
            }
            if (modernBoldFont == null)
            {
                string[] preferredBoldFonts = { "Segoe UI Bold", "Roboto Bold", "SF Pro Display Bold", "Ubuntu Bold", "Arial Bold", "Arial" };
                modernBoldFont = Font.CreateDynamicFontFromOSFont(preferredBoldFonts, 18);
                if (modernBoldFont == null) modernBoldFont = modernSansFont;
            }
        }

        private void InitTextures()
        {
            // Dark Futuristic Cyber Background
            bgFuturisticDarkTex = MakeTex(4, 4, new Color(0.012f, 0.024f, 0.045f, 1.0f));

            // Glassmorphic Card Backgrounds (Anti-aliased, sleek borders)
            glassCard3DTex = GenerateSmoothRoundedCardTex(128, 128, 10f, 
                new Color(0.030f, 0.065f, 0.115f, 0.95f), 
                new Color(0.015f, 0.035f, 0.070f, 0.98f), 
                new Color(0.0f, 0.70f, 1.0f, 0.45f), 
                new Color(0.35f, 0.85f, 1.0f, 0.40f));

            glassHeader3DTex = GenerateSmoothRoundedCardTex(128, 64, 8f, 
                new Color(0.025f, 0.055f, 0.095f, 0.98f), 
                new Color(0.012f, 0.028f, 0.055f, 1.0f), 
                new Color(0.0f, 0.55f, 0.90f, 0.55f), 
                new Color(0.20f, 0.75f, 1.0f, 0.35f));

            glassPillTex = GenerateSmoothRoundedCardTex(64, 32, 12f, 
                new Color(0.035f, 0.080f, 0.145f, 0.92f), 
                new Color(0.020f, 0.045f, 0.085f, 0.96f), 
                new Color(0.0f, 0.85f, 1.0f, 0.60f), 
                new Color(0.40f, 0.95f, 1.0f, 0.50f));
            
            gaugeBarBgTex = MakeTex(4, 4, new Color(0.04f, 0.08f, 0.14f, 0.90f));
            gaugeBarFillTex = MakeTex(4, 4, new Color(0.0f, 0.95f, 0.55f, 1.0f));

            // High-Tech Tactile Flutter-Style Buttons with Integrated LED Strips & Specular Bevels
            btn3DNormalTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.065f, 0.125f, 0.210f), new Color(0.025f, 0.055f, 0.100f), 
                new Color(0.18f, 0.42f, 0.68f), new Color(0.0f, 0.70f, 1.0f, 0.65f), new Color(0.0f, 0.80f, 1.0f, 0.90f));

            btn3DActiveGreenTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.02f, 0.42f, 0.22f), new Color(0.01f, 0.18f, 0.09f), 
                new Color(0.25f, 0.98f, 0.55f), new Color(0.0f, 1.0f, 0.55f, 0.95f), new Color(0.0f, 1.0f, 0.60f, 1.0f));

            btn3DBlueTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.04f, 0.26f, 0.50f), new Color(0.015f, 0.12f, 0.24f), 
                new Color(0.30f, 0.82f, 1.0f), new Color(0.0f, 0.90f, 1.0f, 0.90f), new Color(0.0f, 0.95f, 1.0f, 1.0f));

            btn3DRedTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.48f, 0.08f, 0.09f), new Color(0.22f, 0.03f, 0.04f), 
                new Color(1.0f, 0.45f, 0.45f), new Color(1.0f, 0.22f, 0.22f, 0.95f), new Color(1.0f, 0.35f, 0.35f, 1.0f));

            btn3DAmberTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.46f, 0.30f, 0.03f), new Color(0.20f, 0.12f, 0.01f), 
                new Color(1.0f, 0.82f, 0.25f), new Color(1.0f, 0.72f, 0.10f, 0.90f), new Color(1.0f, 0.80f, 0.20f, 1.0f));

            btn3DOrangeTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.48f, 0.22f, 0.02f), new Color(0.22f, 0.09f, 0.01f), 
                new Color(1.0f, 0.65f, 0.18f), new Color(1.0f, 0.55f, 0.02f, 0.92f), new Color(1.0f, 0.65f, 0.10f, 1.0f));

            btn3DGreyTex = GenerateTactileButtonTex(128, 64, 10f, 
                new Color(0.11f, 0.15f, 0.20f), new Color(0.05f, 0.07f, 0.10f), 
                new Color(0.35f, 0.48f, 0.62f), new Color(0.50f, 0.70f, 0.85f, 0.45f), new Color(0.55f, 0.75f, 0.90f, 0.70f));

            roundNotificationBadgeTex = GenerateSmoothRoundedCardTex(32, 32, 14f,
                new Color(1.0f, 0.15f, 0.15f, 1.0f), new Color(0.75f, 0.05f, 0.05f, 1.0f),
                new Color(1.0f, 0.60f, 0.60f, 0.95f), new Color(1.0f, 0.85f, 0.85f, 0.80f));

            // Generate True Volumetric 3D Aeronautical Chevron Navigation Arrow (Forward Pointing with Specular Bevels)
            arrow3DVolumetricTex = Generate3DVolumetricArrowTex(360, 360);

            // Generate High-Tech Holographic Concentric Radar Compass Grid
            radarCompassGridTex = GenerateRadarHorizonGridTex(360, 360);

            // Generate 3D Beveled Mini-Map Thumbnail Texture
            miniMap3DBevelTex = GenerateProceduralMiniMapTex(160, 100);
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Generates an anti-aliased rounded rectangle card texture with subtle glass shine and neon border.
        /// </summary>
        private Texture2D GenerateSmoothRoundedCardTex(int w, int h, float r, Color bgTop, Color bgBot, Color borderGlow, Color topHighlight)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedBoxDist(x, y, w, h, r);
                    
                    if (dist > 1.0f)
                    {
                        colors[y * w + x] = Color.clear;
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(1.0f - dist);
                        
                        // Gradient fill
                        Color fillColor = Color.Lerp(bgBot, bgTop, ny);
                        
                        // Top specular edge shine
                        if (y >= h - 3 && dist < 0.5f)
                        {
                            fillColor = Color.Lerp(fillColor, topHighlight, 0.65f);
                        }

                        // Outer glowing border
                        if (dist > -1.2f)
                        {
                            float borderT = Mathf.Clamp01((dist + 1.2f) / 1.5f);
                            fillColor = Color.Lerp(fillColor, borderGlow, borderT);
                        }

                        fillColor.a *= alpha;
                        colors[y * w + x] = fillColor;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a tactile Flutter-style button with chamfered borders, specular top ridge, and glowing LED pill bar.
        /// </summary>
        private Texture2D GenerateTactileButtonTex(int w, int h, float r, Color bgTop, Color bgBot, Color topHighlight, Color borderGlow, Color ledColor)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedBoxDist(x, y, w, h, r);

                    if (dist > 1.0f)
                    {
                        colors[y * w + x] = Color.clear;
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(1.0f - dist);
                        Color pixel = Color.Lerp(bgBot, bgTop, ny);

                        // 1. Embedded LED status pill indicator strip on left
                        if (x >= 5 && x <= 8 && y >= 8 && y <= h - 9)
                        {
                            pixel = ledColor;
                        }
                        // 2. Top Specular Bevel
                        else if (y >= h - 3 && dist < 0.2f)
                        {
                            pixel = Color.Lerp(pixel, topHighlight, 0.75f);
                        }
                        // 3. Subtle outer luminous border
                        else if (dist > -1.4f)
                        {
                            float borderT = Mathf.Clamp01((dist + 1.4f) / 1.6f);
                            pixel = Color.Lerp(pixel, borderGlow, borderT);
                        }
                        // 4. Subtle center sheen
                        else if (ny > 0.60f && ny < 0.85f)
                        {
                            pixel = Color.Lerp(pixel, Color.white, 0.08f);
                        }

                        pixel.a *= alpha;
                        colors[y * w + x] = pixel;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private float GetRoundedBoxDist(float x, float y, float w, float h, float r)
        {
            float cx = Mathf.Clamp(x, r, w - 1 - r);
            float cy = Mathf.Clamp(y, r, h - 1 - r);
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// <summary>
        /// Procedurally generates a True 3D Volumetric Extruded Aeronautical Navigation Chevron Arrow.
        /// Features:
        /// - 3D Perspective isometric projection
        /// - Chamfered beveled edges with high-specular reflective highlights
        /// - Darker emerald side extrusion walls
        /// - Vibrant neon cyber-emerald / cyan top faces
        /// - Illuminated center ridge spine
        /// </summary>
        private Texture2D Generate3DVolumetricArrowTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color emeraldTop = new Color(0.0f, 1.0f, 0.60f, 1.0f);
            Color cyanHighlight = new Color(0.60f, 1.0f, 0.95f, 1.0f);
            Color emeraldMid = new Color(0.02f, 0.68f, 0.35f, 1.0f);
            Color extrudedWall = new Color(0.008f, 0.28f, 0.12f, 1.0f);
            Color deepShadow = new Color(0.0f, 0.08f, 0.03f, 0.75f);
            Color spineColor = new Color(0.90f, 1.0f, 0.98f, 1.0f);
            Color outerNeonGlow = new Color(0.0f, 0.95f, 1.0f, 0.85f);

            float cx = w * 0.5f;
            float cy = h * 0.48f;
            float arrowLen = h * 0.70f;
            float halfWing = w * 0.38f;
            float extrusionDepth = h * 0.065f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Evaluate Arrow Geometry in Normalized Space
                    // Tip is at (cx, cy + arrowLen * 0.48f)
                    // Left Wing Tip is at (cx - halfWing, cy - arrowLen * 0.36f)
                    // Right Wing Tip is at (cx + halfWing, cy - arrowLen * 0.36f)
                    // Inner Notch is at (cx, cy - arrowLen * 0.16f)

                    float topTipY = cy + arrowLen * 0.46f;
                    float baseNotchY = cy - arrowLen * 0.15f;
                    float wingTipY = cy - arrowLen * 0.36f;

                    float relX = Mathf.Abs(x - cx);
                    float relY = y;

                    // 1. Check Top Face of the 3D Chevron
                    // Left/Right Outer Leading Edge: from (0, topTipY) to (halfWing, wingTipY)
                    // Inner Trailing Notch: from (0, baseNotchY) to (halfWing, wingTipY)
                    float edgeSlope = (topTipY - wingTipY) / halfWing;
                    float notchSlope = (baseNotchY - wingTipY) / halfWing;

                    float expectedTopY = topTipY - relX * edgeSlope;
                    float expectedBotY = baseNotchY - relX * notchSlope;

                    bool insideTopFace = (relX <= halfWing) && (relY <= expectedTopY) && (relY >= expectedBotY);

                    // 2. Check 3D Extruded Bottom Walls (extrusionDepth downwards)
                    bool insideExtrusion = (relX <= halfWing) && (relY <= expectedBotY) && (relY >= expectedBotY - extrusionDepth);

                    // 3. Drop Shadow below extrusion
                    bool insideShadow = (relX <= halfWing + 6f) && (relY <= expectedBotY - extrusionDepth) && (relY >= expectedBotY - extrusionDepth - 10f);

                    if (insideTopFace)
                    {
                        // Calculate lighting & specular gradients across top face
                        float normalizedX = relX / halfWing;
                        float ny = (relY - expectedBotY) / (expectedTopY - expectedBotY + 0.001f);

                        // Center spine ridge highlight
                        if (relX <= 3.5f)
                        {
                            colors[y * w + x] = Color.Lerp(spineColor, cyanHighlight, ny * 0.5f);
                        }
                        // Left/Right Chamfered Leading Edges
                        else if (Mathf.Abs(relY - expectedTopY) <= 3.5f)
                        {
                            colors[y * w + x] = (x < cx) ? cyanHighlight : outerNeonGlow;
                        }
                        // Inner Notch Bevel
                        else if (Mathf.Abs(relY - expectedBotY) <= 3.0f)
                        {
                            colors[y * w + x] = emeraldMid;
                        }
                        // Facet shading: Left wing gets direct highlight, Right wing is slightly darker
                        else
                        {
                            Color faceCol = (x < cx) 
                                ? Color.Lerp(emeraldMid, emeraldTop, ny * 0.8f + (1f - normalizedX) * 0.2f)
                                : Color.Lerp(emeraldMid * 0.85f, emeraldTop * 0.90f, ny * 0.7f);
                            
                            // Top tip specular bloom
                            if (ny > 0.75f)
                            {
                                faceCol = Color.Lerp(faceCol, cyanHighlight, (ny - 0.75f) * 3f);
                            }

                            colors[y * w + x] = faceCol;
                        }
                    }
                    else if (insideExtrusion)
                    {
                        // Extruded vertical wall with depth shading
                        float wallPct = (relY - (expectedBotY - extrusionDepth)) / extrusionDepth;
                        Color wallCol = Color.Lerp(extrudedWall, emeraldMid * 0.6f, wallPct);
                        
                        // Bottom outer edge rim
                        if (relY <= expectedBotY - extrusionDepth + 2.0f)
                        {
                            wallCol = outerNeonGlow * 0.8f;
                        }

                        colors[y * w + x] = wallCol;
                    }
                    else if (insideShadow)
                    {
                        colors[y * w + x] = deepShadow;
                    }
                    else
                    {
                        colors[y * w + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private Texture2D GenerateRadarHorizonGridTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color ringCol1 = new Color(0.0f, 0.75f, 1.0f, 0.35f);
            Color ringCol2 = new Color(0.0f, 1.0f, 0.55f, 0.28f);
            Color tickCol = new Color(0.0f, 0.95f, 1.0f, 0.65f);
            Color cardinalCol = new Color(0.0f, 1.0f, 0.60f, 0.85f);

            float cx = w / 2f;
            float cy = h / 2f;
            float maxR = w * 0.46f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    bool isOuterRing = Mathf.Abs(r - maxR) <= 1.5f;
                    bool isMidRing = Mathf.Abs(r - maxR * 0.70f) <= 1.2f;
                    bool isInnerRing = Mathf.Abs(r - maxR * 0.40f) <= 1.2f;

                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    bool isCompassTick = (r > maxR - 8f && r < maxR + 2f) && (Mathf.Abs(angle % 30f) <= 0.8f || Mathf.Abs(angle % 30f - 30f) <= 0.8f);

                    bool isCardinalN = (Mathf.Abs(dx) <= 1.0f && dy > maxR * 0.75f && dy < maxR + 8f);
                    bool isCardinalS = (Mathf.Abs(dx) <= 1.0f && dy < -maxR * 0.75f && dy > -maxR - 8f);
                    bool isCardinalE = (Mathf.Abs(dy) <= 1.0f && dx > maxR * 0.75f && dx < maxR + 8f);
                    bool isCardinalW = (Mathf.Abs(dy) <= 1.0f && dx < -maxR * 0.75f && dx > -maxR - 8f);

                    if (isOuterRing) colors[y * w + x] = ringCol1;
                    else if (isMidRing) colors[y * w + x] = ringCol2;
                    else if (isInnerRing) colors[y * w + x] = ringCol1;
                    else if (isCardinalN || isCardinalS || isCardinalE || isCardinalW) colors[y * w + x] = cardinalCol;
                    else if (isCompassTick) colors[y * w + x] = tickCol;
                    else colors[y * w + x] = Color.clear;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private Texture2D GenerateProceduralMiniMapTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color mapBg = new Color(0.035f, 0.075f, 0.130f, 0.96f);
            Color haulRoad = new Color(0.95f, 0.82f, 0.25f, 0.95f);
            Color roadBorder = new Color(0.0f, 0.80f, 1.0f, 0.80f);
            Color truckDot = new Color(0.0f, 0.95f, 1.0f, 1.0f);
            Color shovelDot = new Color(1.0f, 0.25f, 0.25f, 1.0f);
            Color gridLine = new Color(0.0f, 0.40f, 0.65f, 0.18f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    {
                        colors[y * w + x] = new Color(0.0f, 0.75f, 1.0f, 0.9f);
                        continue;
                    }

                    float expectedY = (float)x * 0.48f + 20f;
                    float distFromRoad = Mathf.Abs(y - expectedY);

                    if (distFromRoad <= 6.5f)
                    {
                        colors[y * w + x] = distFromRoad > 5.0f ? roadBorder : haulRoad;
                    }
                    else if (x % 22 == 0 || y % 22 == 0)
                    {
                        colors[y * w + x] = gridLine;
                    }
                    else
                    {
                        colors[y * w + x] = mapBg;
                    }
                }
            }

            // Draw Truck Dot (RD5105) and Shovel Dot (EX-012)
            int tx = 102, ty = 68;
            int sx = 46, sy = 40;
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    if (dx * dx + dy * dy <= 14)
                    {
                        int pxT = Mathf.Clamp(tx + dx, 0, w - 1);
                        int pyT = Mathf.Clamp(ty + dy, 0, h - 1);
                        colors[pyT * w + pxT] = truckDot;

                        int pxS = Mathf.Clamp(sx + dx, 0, w - 1);
                        int pyS = Mathf.Clamp(sy + dy, 0, h - 1);
                        colors[pyS * w + pxS] = shovelDot;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private void EnsureStyles()
        {
            float curH = Screen.height;
            if (topHeaderStyle != null && Mathf.Abs(lastScreenHeight - curH) < 2f) return;
            lastScreenHeight = curH;

            // Responsive Dynamic Font Scaling based on resolution / tablet display
            float fontScale = Mathf.Clamp(curH / 720f, 0.85f, 1.85f);
            int ScaleFont(int baseSize) => Mathf.RoundToInt(baseSize * fontScale);

            LoadModernFonts();

            topHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = Color.white }
            };

            topHeaderCenterStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.white }
            };

            topPillTextStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };

            clockStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(15),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                richText = true,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };

            loginTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };

            cardTitleBoldStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = Color.white }
            };

            cardSublabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(10),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = new Color(0.68f, 0.82f, 0.94f) }
            };

            cardActiveTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = new Color(0.0f, 1.0f, 0.55f) }
            };

            bigDistBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(22),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.0f, 1.0f, 0.55f) }
            };

            bigDistSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(11),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.0f, 0.90f, 1.0f) }
            };

            navTargetBoldStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = Color.white }
            };

            navSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(11),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = new Color(0.70f, 0.85f, 0.95f) }
            };

            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(11),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            bottomStatusPillStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(11),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.85f, 0.92f, 1.0f) }
            };

            keypadBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = btn3DNormalTex }
            };

            pinFieldStyle = new GUIStyle(GUI.skin.box)
            {
                font = modernBoldFont ?? modernSansFont,
                fontSize = ScaleFont(20),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f), background = glassHeader3DTex }
            };

            labelHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = modernSansFont,
                fontSize = ScaleFont(12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.65f, 0.78f, 0.90f) }
            };
        }

        public void BindToUnit(string unitId)
        {
            selectedUnitId = string.IsNullOrEmpty(unitId) ? "RD5105" : unitId;
            selectedUnitModel = "CAT 777E (Off-Highway Truck)";
            selectedLocation = "Pit B Selatan";
            activeAssignedLoader = "EXCAVATOR-012";
            activeAssignedLoaderType = "CAT 6060";
            activeAssignedDisposal = "Disposal Barat #2";

            navCompass = GetComponent<OperatorNavigationCompass>();
            if (navCompass == null)
            {
                navCompass = gameObject.AddComponent<OperatorNavigationCompass>();
            }
            UpdateTargetDestination();
        }

        private void UpdateTargetDestination()
        {
            if (navCompass == null) return;

            bool isHeadingToDisposal = currentState == UnitState.Hauling || 
                                       currentState == UnitState.QueueingAtDump || 
                                       currentState == UnitState.Dumping;

            if (isHeadingToDisposal)
            {
                Vector3 disposalPos = transform.position + transform.forward * 850f + transform.right * 150f;
                navCompass.SetTarget($"📍 {activeAssignedDisposal}", disposalPos);
            }
            else
            {
                Vector3 loaderPos = transform.position + transform.forward * 350f;
                navCompass.SetTarget($"⛏️ {activeAssignedLoader}", loaderPos);
            }
        }

        private void OnGUI()
        {
            if (bgFuturisticDarkTex == null) InitTextures();
            EnsureStyles();

            float w = Screen.width;
            float h = Screen.height;

            if (!isLoggedIn)
            {
                DrawAuthenticLoginScreen(w, h);
            }
            else
            {
                DrawAuthenticInCabinDashboard(w, h);
            }
        }

        // =========================================================================
        // 1. FUTURISTIC FLUTTER FMS LOGIN SCREEN WITH TOUCH NUMERIC KEYPAD
        // =========================================================================
        private void DrawAuthenticLoginScreen(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), bgFuturisticDarkTex);

            float cardW = Mathf.Min(680f, w - 24f);
            float cardH = Mathf.Min(490f, h - 24f);
            float cx = (w - cardW) / 2f;
            float cy = (h - cardH) / 2f;

            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(cx, cy, cardW, cardH), glassCard3DTex);

            // TOP BAR: [SYS] VIREXA FMS ... 14:30
            float topH = 44f;
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(cx, cy, cardW, topH), glassHeader3DTex);

            GUI.Label(new Rect(cx + 16, cy + 10, 140, 24), "<color=#00FFA3><b>[SYS]</b></color> <color=#00E5FF>VIREXA FMS</color>", topHeaderStyle);
            GUI.Label(new Rect(cx + (cardW - 200) / 2f, cy + 10, 200, 24), "<b>LOGIN OPERATOR CABIN</b>", topHeaderCenterStyle);
            GUI.Label(new Rect(cx + cardW - 110, cy + 10, 98, 24), DateTime.Now.ToString("HH:mm"), clockStyle);

            float curY = cy + topH + 12f;

            // TITLE: MASUKKAN ID & PIN OPERATOR
            GUI.Label(new Rect(cx, curY, cardW, 22), "<b>AUTENTIKASI ID & PIN OPERATOR</b>", loginTitleStyle);
            curY += 28f;

            // FORM 1: ID OPERATOR
            float padX = cx + 24f;
            float formW = cardW - 48f;

            GUI.Label(new Rect(padX, curY, formW, 16), "ID & NAMA OPERATOR:", labelHeaderStyle);
            curY += 18f;

            Rect oprBox = new Rect(padX, curY, formW, 36);
            if (btn3DNormalTex != null) GUI.DrawTexture(oprBox, btn3DNormalTex);
            
            string currentOprDisplay = demoOperators[selectedOperatorIndex];
            GUI.Label(new Rect(oprBox.x + 14, oprBox.y + 8, oprBox.width - 90, 20), $"👤 <b>{currentOprDisplay}</b>", topHeaderStyle);
            
            if (GUI.Button(new Rect(oprBox.x + oprBox.width - 86, oprBox.y + 4, 80, 28), "GANTI ▾", keypadBtnStyle))
            {
                selectedOperatorIndex = (selectedOperatorIndex + 1) % demoOperators.Length;
                string[] parts = demoOperators[selectedOperatorIndex].Split('-');
                if (parts.Length >= 2)
                {
                    operatorNik = parts[0].Trim();
                    operatorName = parts[1].Trim();
                }
            }
            curY += 42f;

            // FORM 2: PIN DISPLAY
            GUI.Label(new Rect(padX, curY, formW, 16), "PIN AKSES CABIN:", labelHeaderStyle);
            curY += 18f;

            Rect pinBox = new Rect(padX, curY, formW, 38);
            if (glassHeader3DTex != null) GUI.DrawTexture(pinBox, glassHeader3DTex);

            string pinDisplay = "";
            for (int i = 0; i < operatorPin.Length; i++) pinDisplay += "● ";
            if (string.IsNullOrEmpty(pinDisplay)) pinDisplay = "<color=#556677>Sentuh angka pada keypad di bawah...</color>";
            GUI.Label(pinBox, pinDisplay, pinFieldStyle);
            curY += 44f;

            // ROW: KEYPAD ON LEFT (50%) + UNIT DETAILS ON RIGHT (50%)
            float subW = (formW - 16f) / 2f;
            float blockH = 142f;

            // 1. TOUCH NUMERIC KEYPAD [1-9, C, 0, <-]
            Rect keypadRect = new Rect(padX, curY, subW, blockH);
            if (glassHeader3DTex != null) GUI.DrawTexture(keypadRect, glassHeader3DTex);

            float kw = (subW - 16f) / 3f;
            float kh = (blockH - 16f) / 4f;

            string[,] keys = {
                { "1", "2", "3" },
                { "4", "5", "6" },
                { "7", "8", "9" },
                { "C", "0", "⌫" }
            };

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Rect kRect = new Rect(keypadRect.x + 4 + c * (kw + 4), keypadRect.y + 4 + r * (kh + 3), kw, kh);
                    string keyVal = keys[r, c];
                    
                    Texture2D kTex = (keyVal == "C") ? btn3DRedTex : ((keyVal == "⌫") ? btn3DAmberTex : btn3DNormalTex);
                    if (kTex != null) GUI.DrawTexture(kRect, kTex);

                    if (GUI.Button(kRect, keyVal, keypadBtnStyle))
                    {
                        OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                        if (keyVal == "C")
                        {
                            operatorPin = "";
                        }
                        else if (keyVal == "⌫")
                        {
                            if (operatorPin.Length > 0) operatorPin = operatorPin.Substring(0, operatorPin.Length - 1);
                        }
                        else
                        {
                            if (operatorPin.Length < 6) operatorPin += keyVal;
                        }
                    }
                }
            }

            // 2. ASSIGNED UNIT TELEMETRY CARD (RIGHT SIDE)
            Rect unitCardRect = new Rect(padX + subW + 16f, curY, subW, blockH);
            if (glassHeader3DTex != null) GUI.DrawTexture(unitCardRect, glassHeader3DTex);

            float uy = unitCardRect.y + 8f;
            GUI.Label(new Rect(unitCardRect.x + 12, uy, unitCardRect.width - 24, 18), $"Unit FMS: <color=#00FFA3><b>{selectedUnitId}</b></color>", topHeaderStyle);
            uy += 22f;
            GUI.Label(new Rect(unitCardRect.x + 12, uy, unitCardRect.width - 24, 18), $"Model: <color=#00E5FF><b>{selectedUnitModel}</b></color>", navSubStyle);
            uy += 20f;
            GUI.Label(new Rect(unitCardRect.x + 12, uy, unitCardRect.width - 24, 18), $"Lokasi: <color=#FFB800><b>{selectedLocation}</b></color>", navSubStyle);
            uy += 20f;
            GUI.Label(new Rect(unitCardRect.x + 12, uy, unitCardRect.width - 24, 18), $"Shift: <color=#FFFFFF><b>{selectedShift}</b></color>", navSubStyle);
            uy += 20f;
            GUI.Label(new Rect(unitCardRect.x + 12, uy, unitCardRect.width - 24, 18), "Status: <color=#00FFA3>● Siap Operasi</color>", topHeaderStyle);

            curY += blockH + 12f;

            // ACTION BUTTONS: BATAL & LOGIN
            float btnRowW = (formW - 16f) / 2f;
            
            // Cancel Button
            if (btn3DRedTex != null) GUI.DrawTexture(new Rect(padX, curY, btnRowW, 38), btn3DRedTex);
            if (GUI.Button(new Rect(padX, curY, btnRowW, 38), "✕  RESET PIN", keypadBtnStyle))
            {
                operatorPin = "";
            }

            // Login Button
            if (btn3DActiveGreenTex != null) GUI.DrawTexture(new Rect(padX + btnRowW + 16f, curY, btnRowW, 38), btn3DActiveGreenTex);
            if (GUI.Button(new Rect(padX + btnRowW + 16f, curY, btnRowW, 38), "🔓  MASUK CABIN", keypadBtnStyle))
            {
                OperatorAudioFeedbackManager.Instance?.PlaySuccessChime();
                isLoggedIn = true;
            }

            // FOOTER STATUS BAR
            float footerY = cy + cardH - 24f;
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(cx, footerY, cardW, 24), glassHeader3DTex);
            GUI.Label(new Rect(cx + 14, footerY + 3, cardW - 28, 16), 
                "Status: <color=#00FFA3>● Online</color>  |  GPS: <color=#00FFA3>● 5G RTK Fix (18 Sats)</color>  |  Virexa FMS Mobile v2.4", bottomStatusPillStyle);
        }

        // =========================================================================
        // 2. ULTRA-FUTURISTIC FLUTTER-STYLE IN-CABIN COCKPIT DASHBOARD
        // =========================================================================
        private void DrawAuthenticInCabinDashboard(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), bgFuturisticDarkTex);

            float topBarH = Mathf.Clamp(h * 0.08f, 44f, 52f);
            float botBarH = Mathf.Clamp(h * 0.065f, 32f, 38f);
            float mainBodyH = h - topBarH - botBarH;

            // =====================================================================
            // 2.1 FLUTTER APPBAR (TOP HEADER)
            // =====================================================================
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(0, 0, w, topBarH), glassHeader3DTex);

            // Left Pill: GPS & 5G Signal
            float leftPillW = Mathf.Clamp(w * 0.20f, 150f, 210f);
            Rect leftPillRect = new Rect(8f, 6f, leftPillW, topBarH - 12f);
            if (glassPillTex != null) GUI.DrawTexture(leftPillRect, glassPillTex);
            string signalBars = (Time.time % 2.0f > 1.0f) ? "📶 5G RTK" : "📶 5G LIVE";
            GUI.Label(leftPillRect, $"<color=#00FFA3><b>● {signalBars}</b></color> <color=#00E5FF>(18 Sat)</color>", topPillTextStyle);

            // Center Floating Segmented Pill: Unit ID | Operator | Cycle Status
            float rightPillW = Mathf.Clamp(w * 0.16f, 130f, 170f);
            float centerPillW = w - leftPillW - rightPillW - 36f;
            float centerPillX = leftPillW + 18f;
            Rect centerPillRect = new Rect(centerPillX, 6f, centerPillW, topBarH - 12f);
            if (glassPillTex != null) GUI.DrawTexture(centerPillRect, glassPillTex);

            string stateLabel = (currentState == UnitState.Hauling || currentState == UnitState.QueueingAtDump || currentState == UnitState.Dumping) ? "LOADED (MUATAN)" : "EMPTY (KOSONG)";
            string centerText = $"Unit: <color=#00FFA3><b>{selectedUnitId}</b></color>   |   " +
                                $"Opr: <color=#FFFFFF><b>{operatorName}</b></color>   |   " +
                                $"Status: <color=#00E5FF><b>● {stateLabel}</b></color>";
            GUI.Label(centerPillRect, centerText, topHeaderCenterStyle);

            // Right Pill: Clock & Battery
            float rightPillX = w - rightPillW - 8f;
            Rect rightPillRect = new Rect(rightPillX, 6f, rightPillW, topBarH - 12f);
            if (glassPillTex != null) GUI.DrawTexture(rightPillRect, glassPillTex);
            GUI.Label(rightPillRect, $"<b>{DateTime.Now:HH:mm} WIB</b>  <color=#00FFA3>🔋 98%</color>", topPillTextStyle);

            // =====================================================================
            // 2.2 MAIN 3-COLUMN COCKPIT GRID
            // =====================================================================
            float col1W = Mathf.Clamp(w * 0.20f, 145f, 210f); // Left Action Buttons
            float col3W = Mathf.Clamp(w * 0.22f, 160f, 225f); // Right Operational States
            float col2W = w - col1W - col3W - 16f;             // Center Viewport

            float bodyY = topBarH + 4f;

            // COLUMN 1: LEFT FLUTTER ACTION CARDS
            DrawLeftActionColumn(4f, bodyY, col1W, mainBodyH - 6f);

            // COLUMN 2: CENTER VIEWPORT (3D FORWARD ARROW & RADAR)
            DrawCenterNavigationColumn(8f + col1W, bodyY, col2W, mainBodyH - 6f);

            // COLUMN 3: RIGHT FLUTTER OPERATIONAL STATES & SENSORS
            DrawRightStateColumn(12f + col1W + col2W, bodyY, col3W, mainBodyH - 6f);

            // =====================================================================
            // 2.3 BOTTOM STATUS BAR (FLUTTER CAPSULES)
            // =====================================================================
            float botY = h - botBarH;
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(0, botY, w, botBarH), glassHeader3DTex);

            float pillW = (w - 32f) / 5f;
            float py = botY + 4f;
            float ph = botBarH - 8f;

            // Pill 1: Kecepatan
            Rect p1 = new Rect(6f, py, pillW, ph);
            if (glassPillTex != null) GUI.DrawTexture(p1, glassPillTex);
            GUI.Label(p1, $"⚡ <b>{currentSpeedKmh:F0} km/jam</b>", bottomStatusPillStyle);

            // Pill 2: BBM
            Rect p2 = new Rect(8f + pillW, py, pillW, ph);
            if (glassPillTex != null) GUI.DrawTexture(p2, glassPillTex);
            GUI.Label(p2, $"⛽ BBM: <color=#FFB800><b>{fuelPercent:F0}%</b></color>", bottomStatusPillStyle);

            // Pill 3: Payload
            Rect p3 = new Rect(10f + pillW * 2, py, pillW, ph);
            if (glassPillTex != null) GUI.DrawTexture(p3, glassPillTex);
            GUI.Label(p3, $"⚖️ Muatan: <color=#00E5FF><b>{activePayloadTons:F1} Ton</b></color>", bottomStatusPillStyle);

            // Pill 4: Suhu Ban
            Rect p4 = new Rect(12f + pillW * 3, py, pillW, ph);
            if (glassPillTex != null) GUI.DrawTexture(p4, glassPillTex);
            GUI.Label(p4, $"🛞 Ban: <color=#00FFA3><b>{tireTempStatus}</b></color>", bottomStatusPillStyle);

            // Pill 5: Grade Tanjakan
            Rect p5 = new Rect(14f + pillW * 4, py, pillW, ph);
            if (glassPillTex != null) GUI.DrawTexture(p5, glassPillTex);
            GUI.Label(p5, $"📐 Slope: <color=#FFFFFF><b>{roadGradePercent:+0.0;-0.0;0.0}%</b></color>", bottomStatusPillStyle);

            // =====================================================================
            // 2.4 POPUP DIALOGS & ALERTS
            // =====================================================================
            if (showRadioChatModal) DrawInCabinRadioChatModal(w, h);
            if (showDelayPickerModal) DrawDelayPickerModal(w, h);
            if (showBahayaConfirmModal) DrawBahayaConfirmModal(w, h);

            // Incoming Live Dispatch Voice Alert Banner
            bool isIncomingVoice = FMSFleetMessenger.Instance != null && FMSFleetMessenger.Instance.isTalkbackActive &&
                                  (FMSFleetMessenger.Instance.talkbackTargetUnit == "ALL" || FMSFleetMessenger.Instance.talkbackTargetUnit.Equals(selectedUnitId, StringComparison.OrdinalIgnoreCase));
            if (isIncomingVoice)
            {
                float bannerW = Mathf.Min(560f, w - 40f);
                float bannerH = 46f;
                float bx = (w - bannerW) / 2f;
                float by = topBarH + 8f;

                if (btn3DRedTex != null) GUI.DrawTexture(new Rect(bx, by, bannerW, bannerH), btn3DRedTex);

                string waveBars = (Time.time % 0.4f > 0.2f) ? " ▂ ▃ ▄ ▅ ▆ ▇ █ ▇ ▆ ▅ ▄ ▃ ▂ " : " █ ▇ ▆ ▅ ▄ ▃ ▂   ▂ ▃ ▄ ▅ ▆ ▇ █ ";
                GUI.Label(new Rect(bx + 12, by + 4, bannerW - 24, 20), $"<color=#FFFFFF><b>🎙️ [DISPATCH TALKBACK MASUK]</b></color> <color=#00FFA3>{waveBars}</color>", topHeaderStyle);
                GUI.Label(new Rect(bx + 12, by + 24, bannerW - 24, 16), $"Ruang Kontrol Dispatcher sedang berbicara ({FMSFleetMessenger.Instance.talkbackDuration:F1}s)...", bottomStatusPillStyle);
            }
        }

        // =========================================================================
        // COLUMN 1: LEFT FLUTTER ACTION CARDS (Tactile Glow, Badges & Subtitles)
        // =========================================================================
        private void DrawLeftActionColumn(float x, float y, float w, float h)
        {
            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(x, y, w, h), glassCard3DTex);

            float totalButtons = 5f;
            float btnGap = 8f;
            float btnH = (h - 16f - (totalButtons - 1) * btnGap) / totalButtons;
            float curY = y + 8f;

            // 1. [ 💬 PESAN ] Tactical Blue Card
            int unreadCount = FMSFleetMessenger.Instance != null ? FMSFleetMessenger.Instance.unreadCabinMessagesCount : 0;
            Rect msgRect = new Rect(x + 6, curY, w - 12, btnH);
            
            if (btn3DBlueTex != null) GUI.DrawTexture(msgRect, btn3DBlueTex);
            GUI.Label(new Rect(msgRect.x + 14, msgRect.y + (btnH * 0.16f), msgRect.width - 24, 20), "💬  <b>PESAN</b>", cardTitleBoldStyle);
            GUI.Label(new Rect(msgRect.x + 14, msgRect.y + (btnH * 0.52f), msgRect.width - 24, 16), "Radio Dispatch", cardSublabelStyle);

            // Red Badge Pill on top-right of Pesan Button
            if (unreadCount > 0)
            {
                Rect badgeRect = new Rect(msgRect.x + msgRect.width - 28, msgRect.y + 4, 24, 18);
                if (roundNotificationBadgeTex != null) GUI.DrawTexture(badgeRect, roundNotificationBadgeTex);
                GUI.Label(badgeRect, $"<b>{unreadCount}</b>", badgeStyle);
            }

            if (GUI.Button(msgRect, GUIContent.none, GUIStyle.none))
            {
                OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                showRadioChatModal = true;
                if (FMSFleetMessenger.Instance != null) FMSFleetMessenger.Instance.MarkAllRead(selectedUnitId);
            }
            curY += btnH + btnGap;

            // 2. [ 🎙️ TALKBACK ] Green/Red PTT Card
            bool isCabinTalking = FMSFleetMessenger.Instance != null && FMSFleetMessenger.Instance.isCabinTalkbackActive;
            Rect pttRect = new Rect(x + 6, curY, w - 12, btnH);

            if (isCabinTalking)
            {
                if (btn3DRedTex != null) GUI.DrawTexture(pttRect, btn3DRedTex);
                GUI.Label(new Rect(pttRect.x + 14, pttRect.y + (btnH * 0.16f), pttRect.width - 24, 20), "🔴  <b>ON-AIR</b>", cardTitleBoldStyle);
                GUI.Label(new Rect(pttRect.x + 14, pttRect.y + (btnH * 0.52f), pttRect.width - 24, 16), $"Transmitting ({FMSFleetMessenger.Instance.cabinTalkbackDuration:F1}s)", cardSublabelStyle);

                if (GUI.Button(pttRect, GUIContent.none, GUIStyle.none))
                {
                    OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                    FMSFleetMessenger.Instance.StopCabinTalkback();
                }
            }
            else
            {
                if (btn3DActiveGreenTex != null) GUI.DrawTexture(pttRect, btn3DActiveGreenTex);
                GUI.Label(new Rect(pttRect.x + 14, pttRect.y + (btnH * 0.16f), pttRect.width - 24, 20), "🎙️  <b>TALKBACK</b>", cardTitleBoldStyle);
                GUI.Label(new Rect(pttRect.x + 14, pttRect.y + (btnH * 0.52f), pttRect.width - 24, 16), "Push-To-Talk (Radio)", cardSublabelStyle);

                if (GUI.Button(pttRect, GUIContent.none, GUIStyle.none))
                {
                    OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                    FMSFleetMessenger.Instance?.StartCabinTalkback(selectedUnitId, operatorName);
                }
            }
            curY += btnH + btnGap;

            // 3. [ ⚠️ BAHAYA ] Red Emergency Card
            Rect bahayaRect = new Rect(x + 6, curY, w - 12, btnH);
            if (btn3DRedTex != null) GUI.DrawTexture(bahayaRect, btn3DRedTex);
            GUI.Label(new Rect(bahayaRect.x + 14, bahayaRect.y + (btnH * 0.16f), bahayaRect.width - 24, 20), "⚠️  <b>BAHAYA</b>", cardTitleBoldStyle);
            GUI.Label(new Rect(bahayaRect.x + 14, bahayaRect.y + (btnH * 0.52f), bahayaRect.width - 24, 16), "Lapor Emergency", cardSublabelStyle);

            if (GUI.Button(bahayaRect, GUIContent.none, GUIStyle.none))
            {
                OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                showBahayaConfirmModal = true;
            }
            curY += btnH + btnGap;

            // 4. [ ⏳ DELAY ] Industrial Amber Card
            Rect delayRect = new Rect(x + 6, curY, w - 12, btnH);
            Texture2D dTex = isUnderDelay ? btn3DAmberTex : btn3DNormalTex;
            if (dTex != null) GUI.DrawTexture(delayRect, dTex);
            
            string delaySub = isUnderDelay ? currentDelayReason : "Pilih Alasan Delay";
            GUI.Label(new Rect(delayRect.x + 14, delayRect.y + (btnH * 0.16f), delayRect.width - 24, 20), "⏳  <b>DELAY</b>", isUnderDelay ? cardActiveTitleStyle : cardTitleBoldStyle);
            GUI.Label(new Rect(delayRect.x + 14, delayRect.y + (btnH * 0.52f), delayRect.width - 24, 16), delaySub, cardSublabelStyle);

            if (GUI.Button(delayRect, GUIContent.none, GUIStyle.none))
            {
                OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                showDelayPickerModal = true;
            }
            curY += btnH + btnGap;

            // 5. [ 🚨 BREAKDOWN ] Deep Crimson Card
            Rect bdRect = new Rect(x + 6, curY, w - 12, btnH);
            if (btn3DRedTex != null) GUI.DrawTexture(bdRect, btn3DRedTex);
            GUI.Label(new Rect(bdRect.x + 14, bdRect.y + (btnH * 0.16f), bdRect.width - 24, 20), "🚨  <b>BREAKDOWN</b>", cardTitleBoldStyle);
            GUI.Label(new Rect(bdRect.x + 14, bdRect.y + (btnH * 0.52f), bdRect.width - 24, 16), "Kerusakan Alat", cardSublabelStyle);

            if (GUI.Button(bdRect, GUIContent.none, GUIStyle.none))
            {
                OperatorAudioFeedbackManager.Instance?.PlayWarningBeep();
                isUnderDelay = true;
                currentDelayReason = "Kerusakan Mekanikal (Breakdown)";
                currentState = UnitState.Maintenance;
                currentSpeedKmh = 0f;
                FMSFleetMessenger.Instance?.SendFromCabin(selectedUnitId, operatorName, "🚨 LAPORAN BREAKDOWN: Unit mengalami kerusakan teknis!", FMSFleetMessenger.MessagePriority.Emergency);
            }
        }

        // =========================================================================
        // COLUMN 2: CENTER VIEWPORT (TRUE 3D CHEVRON ARROW & RADAR COMPASS HUD)
        // =========================================================================
        private void DrawCenterNavigationColumn(float x, float y, float w, float h)
        {
            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(x, y, w, h), glassCard3DTex);

            // =====================================================================
            // 1. FLOATING DISTANCE BADGE [ 📍 350 METER ] (CENTERED AT TOP)
            // =====================================================================
            float distMeters = navCompass != null ? navCompass.distanceToTargetMeters : 350f;
            string distText = distMeters >= 1000f ? $"{ (distMeters / 1000f):F1} KM" : $"{distMeters:F0} METER";
            
            float badgeW = Mathf.Clamp(w * 0.55f, 220f, 320f);
            float badgeH = 50f;
            float badgeX = x + (w - badgeW) / 2f;
            float badgeY = y + 10f;

            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), glassHeader3DTex);
            GUI.Label(new Rect(badgeX, badgeY + 4, badgeW, 26), $"📍 <b>{distText}</b>", bigDistBadgeStyle);
            
            bool isHeadingToDisposal = currentState == UnitState.Hauling || currentState == UnitState.QueueingAtDump || currentState == UnitState.Dumping;
            string targetSub = isHeadingToDisposal ? $"MENUJU AREA DUMPING ({activeAssignedDisposal})" : $"MENUJU FRONT GALI ({activeAssignedLoader})";
            GUI.Label(new Rect(badgeX, badgeY + 28, badgeW, 18), targetSub, bigDistSubStyle);

            // =====================================================================
            // 2. VISUAL CENTER COORDINATE OF THE VIEWPORT
            // =====================================================================
            float centerX = x + w / 2f;
            float centerY = y + (h / 2f) - 6f;

            // Concentric Holographic Radar Grid in Background Center
            float ringSize = Mathf.Min(w * 0.72f, h * 0.62f, 320f);
            if (radarCompassGridTex != null)
            {
                GUI.DrawTexture(new Rect(centerX - ringSize / 2f, centerY - ringSize / 2f, ringSize, ringSize), radarCompassGridTex);
            }

            // Animated Forward Guidance Chevrons (Pulsing forward >>>)
            float pulseOffset = (forwardPulseTime % 1.0f) * 45f;
            for (int i = 0; i < 3; i++)
            {
                float py = centerY - (ringSize * 0.38f) - pulseOffset + (i * 24f);
                float pAlpha = Mathf.Clamp01(1.0f - (centerY - py) / (ringSize * 0.55f));
                Color oldColor = GUI.color;
                GUI.color = new Color(0.0f, 1.0f, 0.65f, pAlpha * 0.65f);
                GUI.Label(new Rect(centerX - 30, py, 60, 20), "▲ ▲ ▲", topHeaderCenterStyle);
                GUI.color = oldColor;
            }

            // =====================================================================
            // 3. TRUE VOLUMETRIC 3D AERONAUTICAL CHEVRON ARROW (POINTING FORWARD)
            // =====================================================================
            float arrowSize = Mathf.Min(w * 0.44f, h * 0.42f, 220f);
            float arrowX = centerX - arrowSize / 2f;
            float arrowY = centerY - arrowSize / 2f;

            // Arrow points directly forward (0° along haul route, rotates smoothly if heading off-path)
            float bearing = navCompass != null ? navCompass.relativeBearingDegrees : 0f;
            compassAnimAngle = Mathf.LerpAngle(compassAnimAngle, bearing, Time.deltaTime * 6f);

            Matrix4x4 matrixBackup = GUI.matrix;
            Vector2 pivot = new Vector2(centerX, centerY);
            GUIUtility.RotateAroundPivot(compassAnimAngle, pivot);

            if (arrow3DVolumetricTex != null)
            {
                GUI.DrawTexture(new Rect(arrowX, arrowY, arrowSize, arrowSize), arrow3DVolumetricTex);
            }
            GUI.matrix = matrixBackup;

            // Compass Degree Readout Pill below Arrow
            float degW = 120f;
            float degH = 22f;
            Rect degRect = new Rect(centerX - degW / 2f, centerY + ringSize * 0.44f, degW, degH);
            if (glassPillTex != null) GUI.DrawTexture(degRect, glassPillTex);
            
            float absHdg = navCompass != null ? navCompass.absoluteCompassBearing : 14f;
            string cardinalDir = GetCardinalDirection(absHdg);
            GUI.Label(degRect, $"HDG: <b>{absHdg:000}° {cardinalDir}</b>", bottomStatusPillStyle);

            // =====================================================================
            // 4. BOTTOM TARGET INFO CARD & 3D MINI-MAP (BALANCED AT BOTTOM OF CENTER)
            // =====================================================================
            float infoH = Mathf.Clamp(h * 0.20f, 76f, 92f);
            float infoY = y + h - infoH - 8f;
            float mapW = Mathf.Clamp(w * 0.32f, 130f, 180f);
            float mapH = infoH;
            float mapX = x + w - mapW - 10f;
            float infoW = w - mapW - 28f;

            // Target Info Card Container
            Rect infoCardRect = new Rect(x + 10f, infoY, infoW, infoH);
            if (glassHeader3DTex != null) GUI.DrawTexture(infoCardRect, glassHeader3DTex);

            string targetTitle = isHeadingToDisposal ? $"TARGET: <color=#00FFA3>{activeAssignedDisposal}</color>" : $"TARGET: <color=#00FFA3>{activeAssignedLoader}</color>";
            string targetType = isHeadingToDisposal ? "TIPE: <color=#00E5FF>Area Dumping (Disposal)</color>" : $"TIPE: <color=#00E5FF>{activeAssignedLoaderType} (Pit B)</color>";
            string targetEta = $"JARAK: <color=#FFFFFF><b>{distText}</b></color>   |   ETA: <color=#00FFA3><b>1.4 Menit</b></color>";

            GUI.Label(new Rect(infoCardRect.x + 12, infoCardRect.y + 6, infoCardRect.width - 24, 22), $"<b>{targetTitle}</b>", navTargetBoldStyle);
            GUI.Label(new Rect(infoCardRect.x + 12, infoCardRect.y + 28, infoCardRect.width - 24, 18), $"<b>{targetType}</b>", navSubStyle);
            GUI.Label(new Rect(infoCardRect.x + 12, infoCardRect.y + 48, infoCardRect.width - 24, 22), $"<b>{targetEta}</b>", navTargetBoldStyle);

            // Embedded 3D Mini-Map Viewport
            if (miniMap3DBevelTex != null)
            {
                GUI.DrawTexture(new Rect(mapX, infoY, mapW, mapH), miniMap3DBevelTex);
            }
        }

        private string GetCardinalDirection(float deg)
        {
            if (deg >= 337.5f || deg < 22.5f) return "N";
            if (deg >= 22.5f && deg < 67.5f) return "NE";
            if (deg >= 67.5f && deg < 112.5f) return "E";
            if (deg >= 112.5f && deg < 157.5f) return "SE";
            if (deg >= 157.5f && deg < 202.5f) return "S";
            if (deg >= 202.5f && deg < 247.5f) return "SW";
            if (deg >= 247.5f && deg < 292.5f) return "W";
            return "NW";
        }

        // =========================================================================
        // COLUMN 3: RIGHT FLUTTER OPERATIONAL STATE LIFECYCLE & TELEMETRY GAUGES
        // =========================================================================
        private void DrawRightStateColumn(float x, float y, float w, float h)
        {
            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(x, y, w, h), glassCard3DTex);

            float totalCycleButtons = 4f;
            float btnGap = 8f;
            float sensorBoxH = Mathf.Clamp(h * 0.22f, 80f, 96f);
            float btnH = (h - sensorBoxH - 24f - (totalCycleButtons - 1) * btnGap) / totalCycleButtons;
            float curY = y + 8f;

            // 1. [ ⚪ STANDBY ]
            bool isStandby = currentState == UnitState.Idle || isUnderDelay;
            DrawFlutterStateCard(x + 6, curY, w - 12, btnH, "⚪  STANDBY", "Tunggu Antrean", isStandby, btn3DGreyTex, () => {
                SetTruckState(UnitState.Idle, "Standby di Lokasi");
            });
            curY += btnH + btnGap;

            // 2. [ 🟢 ARRIVE (Tiba) ]
            bool isArrive = currentState == UnitState.QueueingAtPit || currentState == UnitState.QueueingAtDump;
            DrawFlutterStateCard(x + 6, curY, w - 12, btnH, "🟢  ARRIVE (TIBA)", "Tiba di Front/Dump", isArrive, btn3DActiveGreenTex, () => {
                SetTruckState(UnitState.QueueingAtPit, "Tiba di Antrean Front");
            });
            curY += btnH + btnGap;

            // 3. [ 🟠 LOADING (Isi) ]
            bool isLoading = currentState == UnitState.Loading;
            DrawFlutterStateCard(x + 6, curY, w - 12, btnH, "🟠  LOADING (ISI)", "Sedang Dimuat", isLoading, btn3DOrangeTex, () => {
                SetTruckState(UnitState.Loading, "Sedang Dimuat di Front Gali");
            });
            curY += btnH + btnGap;

            // 4. [ 🔵 FULL / HAUL (Muat) ]
            bool isFullHaul = currentState == UnitState.Hauling || currentState == UnitState.Dumping;
            DrawFlutterStateCard(x + 6, curY, w - 12, btnH, "🔵  FULL / HAUL", "Hauling ke Disposal", isFullHaul, btn3DBlueTex, () => {
                SetTruckState(UnitState.Hauling, "Berangkat Hauling Bermuatan");
            });
            curY += btnH + 10f;

            // VEHICLE HEALTH SENSORS: Engine & Transmission Temp Gauge Bars
            Rect sensorBox = new Rect(x + 6, curY, w - 12, sensorBoxH);
            if (glassHeader3DTex != null) GUI.DrawTexture(sensorBox, glassHeader3DTex);

            float sy = sensorBox.y + 6f;
            
            // Engine Temp Gauge
            GUI.Label(new Rect(sensorBox.x + 10, sy, sensorBox.width - 20, 16), $"Engine: <color=#00FFA3><b>{engineTempC:F0}°C</b></color>", topHeaderStyle);
            sy += 18f;
            DrawHorizontalGauge(sensorBox.x + 10, sy, sensorBox.width - 20, 8f, engineTempC / 120f, new Color(0.0f, 1.0f, 0.5f));
            sy += 14f;

            // Transmission Temp Gauge
            GUI.Label(new Rect(sensorBox.x + 10, sy, sensorBox.width - 20, 16), $"Trans: <color=#FFB800><b>{transTempC:F0}°C</b></color>", topHeaderStyle);
            sy += 18f;
            DrawHorizontalGauge(sensorBox.x + 10, sy, sensorBox.width - 20, 8f, transTempC / 120f, new Color(1.0f, 0.75f, 0.1f));
        }

        private void DrawFlutterStateCard(float bx, float by, float bw, float bh, string title, string sublabel, bool isActive, Texture2D activeTex, Action onClick)
        {
            Rect btnRect = new Rect(bx, by, bw, bh);

            if (isActive)
            {
                if (activeTex != null) GUI.DrawTexture(btnRect, activeTex);
                GUI.Label(new Rect(btnRect.x + 12, btnRect.y + (bh * 0.14f), btnRect.width - 24, 20), $"<b>{title}</b>", cardTitleBoldStyle);
                GUI.Label(new Rect(btnRect.x + 12, btnRect.y + (bh * 0.52f), btnRect.width - 24, 16), $"<color=#00FFA3>● {sublabel}</color>", cardSublabelStyle);
            }
            else
            {
                if (btn3DNormalTex != null) GUI.DrawTexture(btnRect, btn3DNormalTex);
                GUI.Label(new Rect(btnRect.x + 12, btnRect.y + (bh * 0.14f), btnRect.width - 24, 20), title, cardTitleBoldStyle);
                GUI.Label(new Rect(btnRect.x + 12, btnRect.y + (bh * 0.52f), btnRect.width - 24, 16), sublabel, cardSublabelStyle);
            }

            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
            {
                OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
                onClick?.Invoke();
            }
        }

        private void DrawHorizontalGauge(float gx, float gy, float gw, float gh, float fillPct, Color fillColor)
        {
            fillPct = Mathf.Clamp01(fillPct);
            if (gaugeBarBgTex != null) GUI.DrawTexture(new Rect(gx, gy, gw, gh), gaugeBarBgTex);
            
            Color old = GUI.color;
            GUI.color = fillColor;
            if (gaugeBarFillTex != null) GUI.DrawTexture(new Rect(gx, gy, gw * fillPct, gh), gaugeBarFillTex);
            GUI.color = old;
        }

        private void SetTruckState(UnitState newState, string logMsg)
        {
            currentState = newState;
            isUnderDelay = false;
            
            if (newState == UnitState.Hauling)
            {
                activePayloadTons = 92.5f;
                currentSpeedKmh = 32.0f;
            }
            else if (newState == UnitState.QueueingAtPit || newState == UnitState.Loading)
            {
                activePayloadTons = 0.0f;
                currentSpeedKmh = 0.0f;
            }
            else if (newState == UnitState.Idle)
            {
                currentSpeedKmh = 0.0f;
            }

            UpdateTargetDestination();
            FMSFleetMessenger.Instance?.SendFromCabin(selectedUnitId, operatorName, $"STATUS UPDATE: {logMsg}", FMSFleetMessenger.MessagePriority.Normal);
            OperatorAudioFeedbackManager.Instance?.PlaySuccessChime();
        }

        // =========================================================================
        // 3. DIALOGS & MODALS: RADIO CHAT, DELAY REASONS, BAHAYA CONFIRMATION
        // =========================================================================
        private void DrawInCabinRadioChatModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(640f, screenW - 40f);
            float modalH = Mathf.Min(480f, screenH - 50f);
            float mx = (screenW - modalW) / 2f;
            float my = (screenH - modalH) / 2f;

            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(mx, my, modalW, modalH), glassCard3DTex);

            // Modal Header
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(mx, my, modalW, 40), glassHeader3DTex);
            GUI.Label(new Rect(mx + 16, my + 10, modalW - 60, 24), "💬 <b>RADIO CHAT DISPATCHER & CABIN</b>", topHeaderStyle);

            if (GUI.Button(new Rect(mx + modalW - 36, my + 8, 26, 26), "✕", keypadBtnStyle))
            {
                showRadioChatModal = false;
            }

            // Quick Pre-defined Replies
            float qy = my + 50f;
            GUI.Label(new Rect(mx + 16, qy, modalW - 32, 18), "BALASAN CEPAT:", labelHeaderStyle);
            qy += 22f;

            string[] quickMsgs = { "Siap, Dimengerti!", "Menuju Lokasi", "Antrean Penuh", "Minta Arahan Ulang" };
            float qbW = (modalW - 48f) / 4f;
            for (int i = 0; i < quickMsgs.Length; i++)
            {
                if (GUI.Button(new Rect(mx + 16 + i * (qbW + 8), qy, qbW, 30), quickMsgs[i], keypadBtnStyle))
                {
                    FMSFleetMessenger.Instance?.SendFromCabin(selectedUnitId, operatorName, quickMsgs[i], FMSFleetMessenger.MessagePriority.Normal);
                }
            }
            qy += 38f;

            // Message Scroll Area
            float chatH = modalH - (qy - my) - 60f;
            Rect chatAreaRect = new Rect(mx + 16, qy, modalW - 32, chatH);
            if (glassHeader3DTex != null) GUI.DrawTexture(chatAreaRect, glassHeader3DTex);

            var messages = FMSFleetMessenger.Instance != null ? FMSFleetMessenger.Instance.GetMessagesForUnit(selectedUnitId) : new List<FMSFleetMessenger.ChatMessage>();

            GUILayout.BeginArea(chatAreaRect);
            cabinChatScrollPos = GUILayout.BeginScrollView(cabinChatScrollPos, GUILayout.Width(chatAreaRect.width), GUILayout.Height(chatAreaRect.height));

            if (messages.Count == 0)
            {
                GUILayout.Label("<color=#556677>Belum ada pesan radio masuk.</color>", topHeaderStyle);
            }
            else
            {
                foreach (var msg in messages)
                {
                    bool isMine = msg.senderRole == "OPERATOR" || (!string.IsNullOrEmpty(msg.senderName) && msg.senderName.Contains(selectedUnitId));
                    string senderTag = isMine ? "<color=#00FFA3><b>[SAYA]</b></color>" : $"<color=#00E5FF><b>[{msg.senderName}]</b></color>";
                    string timeTag = $"<color=#8899aa><size=10>{msg.timestamp}</size></color>";
                    
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"{senderTag} {timeTag}\n{msg.messageText}", topHeaderStyle);
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Bottom Close
            float btmY = my + modalH - 44f;
            if (GUI.Button(new Rect(mx + 16, btmY, modalW - 32, 34), "TUTUP WINDOW CHAT", keypadBtnStyle))
            {
                showRadioChatModal = false;
            }
        }

        private void DrawDelayPickerModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(560f, screenW - 40f);
            float modalH = Mathf.Min(430f, screenH - 50f);
            float mx = (screenW - modalW) / 2f;
            float my = (screenH - modalH) / 2f;

            if (glassCard3DTex != null) GUI.DrawTexture(new Rect(mx, my, modalW, modalH), glassCard3DTex);
            if (glassHeader3DTex != null) GUI.DrawTexture(new Rect(mx, my, modalW, 40), glassHeader3DTex);
            GUI.Label(new Rect(mx + 16, my + 10, modalW - 60, 24), "⏳ <b>PILIH KATEGORI DELAY OPERASIONAL</b>", topHeaderStyle);

            if (GUI.Button(new Rect(mx + modalW - 36, my + 8, 26, 26), "✕", keypadBtnStyle))
            {
                showDelayPickerModal = false;
            }

            string[] delayOptions = {
                "Hujan Lebat / Slippery (Licin)",
                "Kabut Tebal / Jarak Pandang Rendah",
                "Refueling BBM di Jalur",
                "Antrean Dump / Front Padat",
                "Perbaikan Jalan Tambang (Grader/Dozer)",
                "Istirahat Operator / Sholat / Makan",
                "Safety Talk / P5M Singkat",
                "Pemeriksaan Ban & Tekanan Angin"
            };

            float curY = my + 50f;
            for (int i = 0; i < delayOptions.Length; i++)
            {
                Rect bRect = new Rect(mx + 16, curY, modalW - 32, 34);
                if (btn3DAmberTex != null) GUI.DrawTexture(bRect, btn3DAmberTex);

                if (GUI.Button(bRect, $"⏳  {delayOptions[i]}", keypadBtnStyle))
                {
                    isUnderDelay = true;
                    currentDelayReason = delayOptions[i];
                    currentState = UnitState.Idle;
                    FMSFleetMessenger.Instance?.SendFromCabin(selectedUnitId, operatorName, $"LAPORAN DELAY: {currentDelayReason}", FMSFleetMessenger.MessagePriority.Urgent);
                    showDelayPickerModal = false;
                }
                curY += 40f;
            }
        }

        private void DrawBahayaConfirmModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(520f, screenW - 40f);
            float modalH = Mathf.Min(320f, screenH - 50f);
            float mx = (screenW - modalW) / 2f;
            float my = (screenH - modalH) / 2f;

            if (btn3DRedTex != null) GUI.DrawTexture(new Rect(mx, my, modalW, modalH), btn3DRedTex);

            GUI.Label(new Rect(mx + 16, my + 20, modalW - 32, 30), "<color=#FFFFFF><size=18><b>⚠️ KONFIRMASI LAPORAN BAHAYA</b></size></color>", topHeaderCenterStyle);
            GUI.Label(new Rect(mx + 20, my + 70, modalW - 40, 90), 
                "Anda akan menyiarkan sinyal <b>EMERGENCY MAYDAY</b> ke seluruh armada dan Pengawas Tambang (Dispatcher Room).\n\nGunakan hanya jika terjadi insiden kritis atau kondisi jalur sangat berbahaya!", topHeaderCenterStyle);

            float btnW = (modalW - 60f) / 2f;
            float by = my + modalH - 60f;

            if (GUI.Button(new Rect(mx + 20, by, btnW, 40), "✕ BATAL", keypadBtnStyle))
            {
                showBahayaConfirmModal = false;
            }

            if (GUI.Button(new Rect(mx + 40 + btnW, by, btnW, 40), "🚨 KIRIM SINYAL BAHAYA", keypadBtnStyle))
            {
                OperatorAudioFeedbackManager.Instance?.PlayWarningBeep();
                FMSFleetMessenger.Instance?.SendFromCabin(selectedUnitId, operatorName, "🚨 EMERGENCY MAYDAY: Operator melaporkan kondisi bahaya kritis di lokasi!", FMSFleetMessenger.MessagePriority.Emergency);
                showBahayaConfirmModal = false;
            }
        }
    }
}
