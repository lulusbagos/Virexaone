using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    /// <summary>
    /// Generates high-fidelity 3D mining vehicles by attaching the FMSGlbModelLoader
    /// (to load the authentic GLB 3D models from disk) and creating a clean unscaled
    /// procedural fallback mesh without shearing or skewing.
    /// </summary>
    public static class FMS3DModelGenerator
    {
        private static Material yellowPaintMat;
        private static Material tireRubberMat;
        private static Material darkSteelMat;
        private static Material chromePistonMat;
        private static Material cabGlassMat;
        private static Material coalOreMat;
        private static Material whiteTankerMat;
        private static Material hazardOrangeMat;
        private static Material emissiveGreenMat;
        private static Material emissiveCyanMat;
        private static Material emissiveRedMat;
        private static Material emissiveOrangeMat;
        private static Material beaconMat;

        public static void EnsureMaterials()
        {
            if (yellowPaintMat != null) return;

            Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Mobile/Diffuse");

            yellowPaintMat = new Material(standardShader) { color = new Color(0.96f, 0.72f, 0.06f) };
            if (yellowPaintMat.HasProperty("_Metallic")) yellowPaintMat.SetFloat("_Metallic", 0.35f);
            if (yellowPaintMat.HasProperty("_Glossiness")) yellowPaintMat.SetFloat("_Glossiness", 0.6f);

            tireRubberMat = new Material(standardShader) { color = new Color(0.12f, 0.12f, 0.13f) };
            darkSteelMat = new Material(standardShader) { color = new Color(0.2f, 0.22f, 0.24f) };

            chromePistonMat = new Material(standardShader) { color = new Color(0.88f, 0.90f, 0.94f) };
            if (chromePistonMat.HasProperty("_Metallic")) chromePistonMat.SetFloat("_Metallic", 0.92f);
            if (chromePistonMat.HasProperty("_Glossiness")) chromePistonMat.SetFloat("_Glossiness", 0.92f);

            cabGlassMat = new Material(standardShader) { color = new Color(0.18f, 0.28f, 0.38f, 0.85f) };
            coalOreMat = new Material(standardShader) { color = new Color(0.14f, 0.14f, 0.15f) };
            whiteTankerMat = new Material(standardShader) { color = new Color(0.92f, 0.93f, 0.94f) };
            hazardOrangeMat = new Material(standardShader) { color = new Color(0.98f, 0.45f, 0.05f) };

            emissiveGreenMat = CreateEmissiveMat(standardShader, new Color(0.1f, 0.95f, 0.4f), 2.5f);
            emissiveCyanMat = CreateEmissiveMat(standardShader, new Color(0.1f, 0.85f, 0.98f), 2.5f);
            emissiveRedMat = CreateEmissiveMat(standardShader, new Color(0.98f, 0.2f, 0.1f), 2.5f);
            emissiveOrangeMat = CreateEmissiveMat(standardShader, new Color(0.98f, 0.6f, 0.05f), 2.5f);
            beaconMat = new Material(standardShader) { color = new Color(0.1f, 0.8f, 0.9f) };
        }

        private static Material CreateEmissiveMat(Shader shader, Color col, float intensity)
        {
            Material mat = new Material(shader) { color = col };
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", col * intensity);
            }
            return mat;
        }

        public static GameObject CreateUnit(FMSUnitAssetManager.UnitCategory category, string unitId, string modelName, string operatorName)
        {
            switch (category)
            {
                case FMSUnitAssetManager.UnitCategory.HaulerEmpty:
                    return CreateHaulTruck(unitId, modelName, operatorName, isLoaded: false);
                case FMSUnitAssetManager.UnitCategory.HaulerLoaded:
                    return CreateHaulTruck(unitId, modelName, operatorName, isLoaded: true);
                case FMSUnitAssetManager.UnitCategory.Excavator:
                    return CreateExcavator(unitId, modelName, operatorName);
                case FMSUnitAssetManager.UnitCategory.Bulldozer:
                    return CreateBulldozer(unitId, modelName, operatorName);
                case FMSUnitAssetManager.UnitCategory.Grader:
                    return CreateGrader(unitId, modelName, operatorName);
                case FMSUnitAssetManager.UnitCategory.FuelTruck:
                    return CreateFuelTruck(unitId, modelName, operatorName);
                case FMSUnitAssetManager.UnitCategory.WheelLoader:
                    return CreateWheelLoader(unitId, modelName, operatorName);
                case FMSUnitAssetManager.UnitCategory.Support:
                    return CreateSupportUnit(unitId, modelName, operatorName);
                default:
                    return CreateSupportUnit(unitId, modelName, operatorName);
            }
        }

        public static GameObject CreateSupportUnit(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();
            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrWhiteSpace(modelName) ? "Tipe unit belum dipetakan" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Support";
            controller.unitType = UnitType.Support;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.Support;
            controller.maxPayloadTons = 0f;
            controller.payloadTons = 0f;
            controller.currentState = UnitState.Idle;

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "UnmappedUnitMarker";
            marker.transform.SetParent(visualRoot.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            marker.transform.localScale = new Vector3(2.5f, 2f, 4f);
            marker.GetComponent<Renderer>().sharedMaterial = darkSteelMat;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 3f, 5f);
            collider.center = new Vector3(0f, 1.5f, 0f);
            return root;
        }

        // =========================================================================
        // 1. HAUL TRUCK (100-Ton CAT 777E / HD785 - Empty / Loaded)
        // =========================================================================
        public static GameObject CreateHaulTruck(string unitId, string modelName, string operatorName, bool isLoaded = false)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? (isLoaded ? "CAT 777E (Loaded)" : "CAT 777E (Empty)") : modelName;
            controller.operatorName = operatorName;
            controller.category = "Hauler";
            controller.unitType = UnitType.HaulTruck;
            controller.unitCategory = isLoaded ? FMSUnitAssetManager.UnitCategory.HaulerLoaded : FMSUnitAssetManager.UnitCategory.HaulerEmpty;
            controller.maxPayloadTons = 95f;
            controller.payloadTons = isLoaded ? 95f : 0f;
            controller.currentState = isLoaded ? UnitState.Hauling : UnitState.TravellingToLoad;
            controller.activityName = isLoaded ? "Hauling (Loaded Coal/OB)" : "Travelling (Empty)";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            // Fallback Mesh
            GameObject fallback = BuildProceduralHaulTruck(visualRoot.transform, controller, isLoaded);

            // GLB Loader Component
            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = isLoaded ? "truck_loaded_draco.glb" : "truck_empty_draco.glb";
            loader.targetLengthMeters = 10.0f;
            loader.targetWidthMeters = 5.8f;
            loader.targetHeightMeters = 5.3f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // 2. EXCAVATOR / MINING SHOVEL (Komatsu PC2000 / Hitachi EX1200)
        // =========================================================================
        public static GameObject CreateExcavator(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? "Komatsu PC2000-8 (Mining Shovel)" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Excavator";
            controller.unitType = UnitType.Excavator;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.Excavator;
            controller.maxPayloadTons = 0f;
            controller.currentState = UnitState.Loading;
            controller.activityName = "Front Loading (Pit Face)";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            GameObject fallback = BuildProceduralExcavator(visualRoot.transform, controller);

            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = "excavator_draco.glb";
            loader.targetLengthMeters = 17.0f;
            loader.targetWidthMeters = 6.8f;
            loader.targetHeightMeters = 6.2f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // 3. BULLDOZER (Komatsu D375A / CAT D10T Heavy Track Dozer)
        // =========================================================================
        public static GameObject CreateBulldozer(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? "Komatsu D375A (Heavy Dozer)" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Dozer";
            controller.unitType = UnitType.Bulldozer;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.Bulldozer;
            controller.maxPayloadTons = 0f;
            controller.currentState = UnitState.Hauling;
            controller.activityName = "Pushing / Bench Levelling";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            GameObject fallback = BuildProceduralBulldozer(visualRoot.transform, controller);

            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = "dozer_draco.glb";
            loader.targetLengthMeters = 9.2f;
            loader.targetWidthMeters = 4.8f;
            loader.targetHeightMeters = 4.3f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // 4. MOTOR GRADER (CAT 16M / GD825A Road Maintenance)
        // =========================================================================
        public static GameObject CreateGrader(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? "CAT 16M (Motor Grader)" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Grader";
            controller.unitType = UnitType.Grader;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.Grader;
            controller.maxPayloadTons = 0f;
            controller.currentState = UnitState.Hauling;
            controller.activityName = "Haul Road Grading";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            GameObject fallback = BuildProceduralGrader(visualRoot.transform, controller);

            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = "grader_draco.glb";
            loader.targetLengthMeters = 11.5f;
            loader.targetWidthMeters = 3.4f;
            loader.targetHeightMeters = 3.7f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // 5. FUEL & SERVICE TRUCK (Support Tanker Rig)
        // =========================================================================
        public static GameObject CreateFuelTruck(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? "Scania P380 (Fuel Tanker)" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Support";
            controller.unitType = UnitType.FuelTruck;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.FuelTruck;
            controller.maxPayloadTons = 25f;
            controller.payloadTons = 22f;
            controller.currentState = UnitState.Hauling;
            controller.activityName = "Pit Fuel & Lube Dispensing";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            GameObject fallback = BuildProceduralFuelTruck(visualRoot.transform, controller);

            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = "fueltruck_draco.glb";
            loader.targetLengthMeters = 9.2f;
            loader.targetWidthMeters = 3.2f;
            loader.targetHeightMeters = 3.5f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // 6. WHEEL LOADER (CAT 988K / Komatsu WA600 Stockpile Loader)
        // =========================================================================
        public static GameObject CreateWheelLoader(string unitId, string modelName, string operatorName)
        {
            EnsureMaterials();

            GameObject root = new GameObject(unitId);
            FMSUnitController controller = root.AddComponent<FMSUnitController>();
            controller.unitId = unitId;
            controller.unitName = unitId;
            controller.modelName = string.IsNullOrEmpty(modelName) ? "CAT 988K (Wheel Loader)" : modelName;
            controller.operatorName = operatorName;
            controller.category = "Loader";
            controller.unitType = UnitType.WheelLoader;
            controller.unitCategory = FMSUnitAssetManager.UnitCategory.WheelLoader;
            controller.maxPayloadTons = 35f;
            controller.currentState = UnitState.Hauling;
            controller.activityName = "Stockpile Rehandling";

            GameObject visualRoot = new GameObject("VisualModel");
            visualRoot.transform.SetParent(root.transform, false);
            controller.visualModelRoot = visualRoot.transform;

            GameObject fallback = BuildProceduralWheelLoader(visualRoot.transform, controller);

            FMSGlbModelLoader loader = visualRoot.AddComponent<FMSGlbModelLoader>();
            loader.glbFileName = "loader_draco.glb";
            loader.targetLengthMeters = 12.5f;
            loader.targetWidthMeters = 4.4f;
            loader.targetHeightMeters = 5.0f;
            loader.fallbackVisual = fallback;

            return root;
        }

        // =========================================================================
        // CLEAN PROCEDURAL FALLBACKS (NO NON-UNIFORM SKEWING OR FLOATING SHEETS)
        // =========================================================================
        private static GameObject BuildProceduralHaulTruck(Transform visualRoot, FMSUnitController controller, bool isLoaded)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            CreateBox(container.transform, "Chassis", new Vector3(0f, 1.6f, 0f), new Vector3(4.2f, 1.1f, 9.6f), darkSteelMat);
            CreateBox(container.transform, "EngineHood", new Vector3(0.7f, 2.7f, 2.6f), new Vector3(2.4f, 1.4f, 2.8f), yellowPaintMat);
            CreateBox(container.transform, "Grille", new Vector3(0.7f, 2.5f, 4.05f), new Vector3(2.1f, 1.1f, 0.15f), darkSteelMat);

            GameObject cab = CreateBox(container.transform, "Cabin", new Vector3(-1.4f, 3.2f, 2.5f), new Vector3(1.7f, 1.9f, 2.3f), cabGlassMat);
            controller.cabTransform = cab.transform;

            // Unscaled bed pivot node
            GameObject bedPivot = new GameObject("DumpBedPivot");
            bedPivot.transform.SetParent(container.transform, false);
            bedPivot.transform.localPosition = new Vector3(0f, 2.1f, -3.8f);
            controller.dumpBedTransform = bedPivot.transform;

            CreateBox(bedPivot.transform, "DumpBed", new Vector3(0f, 1.3f, 3.2f), new Vector3(5.6f, 2.2f, 7.6f), yellowPaintMat);
            CreateBox(bedPivot.transform, "Canopy", new Vector3(0f, 2.65f, 5.8f), new Vector3(5.4f, 0.35f, 3.4f), yellowPaintMat);

            if (isLoaded)
            {
                CreateBox(bedPivot.transform, "CoalOreLoad", new Vector3(0f, 2.4f, 3.2f), new Vector3(5.0f, 1.5f, 6.8f), coalOreMat);
            }

            GameObject beaconObj = CreateCylinder(cab.transform, "StatusBeacon", new Vector3(0f, 1.1f, 0f), new Vector3(0.35f, 0.3f, 0.35f), isLoaded ? emissiveGreenMat : emissiveCyanMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            Transform[] wheelList = new Transform[6];
            Vector3[] wheelPos = new Vector3[]
            {
                new Vector3(-2.55f, 1.35f, 3.1f),
                new Vector3(2.55f, 1.35f, 3.1f),
                new Vector3(-2.65f, 1.35f, -3.0f),
                new Vector3(-1.85f, 1.35f, -3.0f),
                new Vector3(1.85f, 1.35f, -3.0f),
                new Vector3(2.65f, 1.35f, -3.0f),
            };

            for (int i = 0; i < 6; i++)
            {
                GameObject wheel = CreateWheelMesh(container.transform, $"Wheel_{i}", wheelPos[i], 2.7f, 0.65f, tireRubberMat, darkSteelMat);
                wheelList[i] = wheel.transform;
            }
            controller.wheels = wheelList;

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.8f, 0.2f);
            col.size = new Vector3(6.0f, 5.5f, 11.2f);

            return container;
        }

        private static GameObject BuildProceduralExcavator(Transform visualRoot, FMSUnitController controller)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            // =========================================================================
            // 1. UNDERCARRIAGE & DUAL CRAWLER TRACKS (X-Chassis & Heavy Track Frames)
            // =========================================================================
            // Central X-Carbody Chassis
            CreateBox(container.transform, "CenterCarbody", new Vector3(0f, 1.2f, 0f), new Vector3(3.8f, 0.9f, 4.6f), darkSteelMat);
            // Turntable Slewing Ring Bearing
            CreateCylinder(container.transform, "SlewingRingBearing", new Vector3(0f, 1.7f, 0f), new Vector3(3.4f, 0.35f, 3.4f), darkSteelMat);

            // Track Frames (Left & Right)
            float[] trackX = new float[] { -2.6f, 2.6f };
            for (int t = 0; t < trackX.Length; t++)
            {
                float tx = trackX[t];
                string side = (t == 0) ? "Left" : "Right";
                GameObject trackRoot = new GameObject($"TrackFrame_{side}");
                trackRoot.transform.SetParent(container.transform, false);

                // Main track beam
                CreateBox(trackRoot.transform, "TrackBeam", new Vector3(tx, 1.1f, 0f), new Vector3(1.35f, 1.7f, 7.8f), darkSteelMat);

                // Front Idler Wheel & Tensioner Ramp
                GameObject frontIdler = CreateCylinder(trackRoot.transform, "FrontIdler", new Vector3(tx, 1.1f, 3.9f), new Vector3(1.7f, 0.65f, 1.7f), darkSteelMat);
                frontIdler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreateBox(trackRoot.transform, "FrontRamp", new Vector3(tx, 1.1f, 4.4f), new Vector3(1.35f, 1.4f, 1.2f), darkSteelMat);

                // Rear Drive Sprocket & Motor
                GameObject rearSprocket = CreateCylinder(trackRoot.transform, "RearSprocket", new Vector3(tx, 1.2f, -3.9f), new Vector3(1.8f, 0.7f, 1.8f), darkSteelMat);
                rearSprocket.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreateBox(trackRoot.transform, "RearRamp", new Vector3(tx, 1.1f, -4.4f), new Vector3(1.35f, 1.4f, 1.2f), darkSteelMat);

                // Track Shoes / Tread Pads (Top and Bottom run)
                CreateBox(trackRoot.transform, "TrackTopShoe", new Vector3(tx, 2.05f, 0f), new Vector3(1.45f, 0.22f, 8.4f), darkSteelMat);
                CreateBox(trackRoot.transform, "TrackBottomShoe", new Vector3(tx, 0.15f, 0f), new Vector3(1.45f, 0.22f, 8.4f), darkSteelMat);

                // 7 Bottom Track Rollers
                for (int r = -3; r <= 3; r++)
                {
                    GameObject roller = CreateCylinder(trackRoot.transform, $"Roller_{r}", new Vector3(tx, 0.45f, r * 1.1f), new Vector3(0.65f, 0.6f, 0.65f), darkSteelMat);
                    roller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                // 2 Top Carrier Rollers
                for (int cr = -1; cr <= 1; cr += 2)
                {
                    GameObject cRoller = CreateCylinder(trackRoot.transform, $"CarrierRoller_{cr}", new Vector3(tx, 1.8f, cr * 1.8f), new Vector3(0.5f, 0.55f, 0.5f), darkSteelMat);
                    cRoller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                // Track Step Ladder (Front Side)
                CreateBox(trackRoot.transform, "StepLadder", new Vector3(tx + (t == 0 ? -0.85f : 0.85f), 1.0f, 2.8f), new Vector3(0.35f, 1.4f, 0.8f), yellowPaintMat);
            }

            // =========================================================================
            // 2. REVOLVING SUPERSTRUCTURE & MACHINERY DECK (Upper House)
            // =========================================================================
            GameObject house = new GameObject("UpperDeckHouse");
            house.transform.SetParent(container.transform, false);
            house.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            controller.cabTransform = house.transform;

            // Main House Foundation Deck
            CreateBox(house.transform, "SuperstructureDeck", new Vector3(0.3f, 0.25f, 0f), new Vector3(5.8f, 0.4f, 7.8f), darkSteelMat);

            // Machinery / Engine Enclosure
            CreateBox(house.transform, "EngineHousingCenter", new Vector3(0.7f, 1.5f, -0.6f), new Vector3(4.2f, 2.1f, 5.6f), yellowPaintMat);
            CreateBox(house.transform, "EngineRoofHood", new Vector3(0.7f, 2.65f, -0.8f), new Vector3(3.8f, 0.4f, 4.8f), yellowPaintMat);

            // Radiator Vents / Cooling Louvres on Top Deck
            CreateBox(house.transform, "CoolingGrillTop", new Vector3(0.7f, 2.9f, -1.2f), new Vector3(3.2f, 0.15f, 2.6f), darkSteelMat);
            CreateBox(house.transform, "SideIntakeLouverLeft", new Vector3(-1.42f, 1.7f, -1.0f), new Vector3(0.1f, 1.3f, 2.2f), darkSteelMat);

            // Twin Exhaust Stacks
            CreateCylinder(house.transform, "ExhaustStack1", new Vector3(0.0f, 3.4f, -2.4f), new Vector3(0.32f, 1.2f, 0.32f), darkSteelMat);
            CreateCylinder(house.transform, "ExhaustStack2", new Vector3(0.6f, 3.4f, -2.4f), new Vector3(0.32f, 1.2f, 0.32f), darkSteelMat);

            // Heavy Cast Iron Counterweight (Curved / Beveled Rear)
            CreateBox(house.transform, "CounterweightMain", new Vector3(0.7f, 1.5f, -4.0f), new Vector3(5.6f, 2.4f, 1.6f), darkSteelMat);
            CreateBox(house.transform, "CounterweightTopBevel", new Vector3(0.7f, 2.6f, -4.0f), new Vector3(5.2f, 0.4f, 1.4f), darkSteelMat);
            CreateBox(house.transform, "CounterweightSafetyReflector", new Vector3(0.7f, 1.5f, -4.82f), new Vector3(4.6f, 0.35f, 0.05f), hazardOrangeMat);

            // Walkway Catwalks & Safety Handrails
            CreateBox(house.transform, "CatwalkRight", new Vector3(3.0f, 0.5f, -0.4f), new Vector3(0.7f, 0.15f, 6.8f), darkSteelMat);
            CreateBox(house.transform, "HandrailRight", new Vector3(3.3f, 1.15f, -0.4f), new Vector3(0.08f, 1.15f, 6.8f), yellowPaintMat);
            CreateBox(house.transform, "CatwalkLeft", new Vector3(-2.4f, 0.5f, -0.8f), new Vector3(0.7f, 0.15f, 5.8f), darkSteelMat);
            CreateBox(house.transform, "HandrailLeft", new Vector3(-2.7f, 1.15f, -0.8f), new Vector3(0.08f, 1.15f, 5.8f), yellowPaintMat);

            // =========================================================================
            // 3. ELEVATED OPERATOR CABIN (Front-Left Panoramic Glass Cockpit)
            // =========================================================================
            GameObject cabBase = new GameObject("OperatorCabBase");
            cabBase.transform.SetParent(house.transform, false);
            cabBase.transform.localPosition = new Vector3(-2.0f, 1.8f, 1.9f);

            // Cabin Shell
            CreateBox(cabBase.transform, "CabLowerBody", new Vector3(0f, 0.2f, 0f), new Vector3(2.0f, 0.6f, 2.6f), yellowPaintMat);
            CreateBox(cabBase.transform, "CabRearWall", new Vector3(0f, 1.2f, -1.15f), new Vector3(1.95f, 1.6f, 0.3f), yellowPaintMat);
            CreateBox(cabBase.transform, "CabRoof", new Vector3(0f, 2.05f, 0f), new Vector3(2.1f, 0.25f, 2.8f), yellowPaintMat);
            CreateBox(cabBase.transform, "CabSunVisor", new Vector3(0f, 2.1f, 1.45f), new Vector3(2.15f, 0.1f, 0.45f), darkSteelMat);

            // Panoramic Tinted Windows
            GameObject windshield = CreateBox(cabBase.transform, "Windshield", new Vector3(0f, 1.15f, 1.22f), new Vector3(1.85f, 1.55f, 0.15f), cabGlassMat);
            windshield.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f); // Sloped aerodynamic windshield
            CreateBox(cabBase.transform, "WindowLeft", new Vector3(-0.95f, 1.15f, 0.05f), new Vector3(0.12f, 1.55f, 2.15f), cabGlassMat);
            CreateBox(cabBase.transform, "WindowRight", new Vector3(0.95f, 1.15f, 0.05f), new Vector3(0.12f, 1.55f, 2.15f), cabGlassMat);
            CreateBox(cabBase.transform, "SkylightRoof", new Vector3(0f, 2.18f, 0.4f), new Vector3(1.2f, 0.08f, 1.0f), cabGlassMat);

            // Operator Seat & Console Silhouette
            CreateBox(cabBase.transform, "OperatorSeat", new Vector3(0.2f, 0.8f, -0.2f), new Vector3(0.65f, 0.8f, 0.65f), darkSteelMat);
            CreateBox(cabBase.transform, "ControlConsole", new Vector3(0.2f, 0.75f, 0.6f), new Vector3(0.8f, 0.5f, 0.4f), darkSteelMat);

            // Roof HVAC AC Unit & GPS Dome
            CreateBox(cabBase.transform, "HVACUnit", new Vector3(0f, 2.3f, -0.6f), new Vector3(1.4f, 0.35f, 1.1f), yellowPaintMat);
            CreateCylinder(cabBase.transform, "GpsDome", new Vector3(0.6f, 2.45f, -0.9f), new Vector3(0.35f, 0.2f, 0.35f), whiteTankerMat);

            // Quad LED Floodlights
            CreateBox(cabBase.transform, "Worklight1", new Vector3(-0.85f, 2.05f, 1.4f), new Vector3(0.25f, 0.18f, 0.15f), emissiveCyanMat);
            CreateBox(cabBase.transform, "Worklight2", new Vector3(0.85f, 2.05f, 1.4f), new Vector3(0.25f, 0.18f, 0.15f), emissiveCyanMat);

            // Status Beacon on Cab Roof
            GameObject beaconObj = CreateCylinder(cabBase.transform, "StatusBeacon", new Vector3(-0.6f, 2.45f, 0.9f), new Vector3(0.35f, 0.3f, 0.35f), emissiveCyanMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            // =========================================================================
            // 4. ARTICULATED GOOSENECK BOOM & DUAL HOIST CYLINDERS
            // =========================================================================
            GameObject boomPivot = new GameObject("BoomPivot");
            boomPivot.transform.SetParent(house.transform, false);
            boomPivot.transform.localPosition = new Vector3(0.7f, 1.9f, 2.4f);
            controller.boomTransform = boomPivot.transform;

            // Boom Main Base Pivot Lugs
            CreateBox(boomPivot.transform, "BoomBaseLugs", new Vector3(0f, 0f, 0f), new Vector3(1.8f, 1.2f, 1.4f), darkSteelMat);

            // Arched Gooseneck Boom Main Structure
            GameObject boomMesh1 = CreateBox(boomPivot.transform, "BoomLowerSegment", new Vector3(0f, 1.5f, 2.1f), new Vector3(1.35f, 1.4f, 5.2f), yellowPaintMat);
            boomMesh1.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);

            GameObject boomMesh2 = CreateBox(boomPivot.transform, "BoomUpperSegment", new Vector3(0f, 3.4f, 4.4f), new Vector3(1.2f, 1.25f, 4.6f), yellowPaintMat);
            boomMesh2.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            // Boom High-Pressure Hydraulic Steel Lines along Spine
            CreateBox(boomPivot.transform, "HydraulicPiping", new Vector3(0f, 2.4f, 3.2f), new Vector3(0.5f, 0.12f, 6.2f), darkSteelMat);

            // Boom Worklights (Mounted on lower boom side)
            CreateBox(boomPivot.transform, "BoomWorklightL", new Vector3(-0.8f, 1.6f, 2.8f), new Vector3(0.22f, 0.22f, 0.25f), emissiveCyanMat);
            CreateBox(boomPivot.transform, "BoomWorklightR", new Vector3(0.8f, 1.6f, 2.8f), new Vector3(0.22f, 0.22f, 0.25f), emissiveCyanMat);

            // Dual Heavy Boom Hoist Hydraulic Cylinders (Barrel + Chrome Piston Rod)
            float[] cylX = new float[] { -0.95f, 0.95f };
            for (int c = 0; c < cylX.Length; c++)
            {
                float cx = cylX[c];
                GameObject barrel = CreateCylinder(boomPivot.transform, $"BoomCylBarrel_{c}", new Vector3(cx, 0.8f, 1.3f), new Vector3(0.48f, 1.8f, 0.48f), darkSteelMat);
                barrel.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);

                GameObject rod = CreateCylinder(boomPivot.transform, $"BoomCylRod_{c}", new Vector3(cx, 1.7f, 2.5f), new Vector3(0.32f, 1.6f, 0.32f), chromePistonMat ?? darkSteelMat);
                rod.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            }

            // =========================================================================
            // 5. ARTICULATED DIPPER STICK (ARM) & CROWD CYLINDER
            // =========================================================================
            GameObject stickPivot = new GameObject("StickPivot");
            stickPivot.transform.SetParent(boomPivot.transform, false);
            stickPivot.transform.localPosition = new Vector3(0f, 4.0f, 6.4f);
            controller.stickTransform = stickPivot.transform;

            // Stick Main Box Beam
            GameObject stickMesh = CreateBox(stickPivot.transform, "StickMainBeam", new Vector3(0f, -1.6f, 1.5f), new Vector3(1.1f, 1.1f, 5.4f), yellowPaintMat);
            stickMesh.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);

            // Stick Crowd Cylinder & Piston Rod
            GameObject stickCyl = CreateCylinder(stickPivot.transform, "StickCylBarrel", new Vector3(0f, 0.6f, 1.2f), new Vector3(0.42f, 1.6f, 0.42f), darkSteelMat);
            stickCyl.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);

            GameObject stickRod = CreateCylinder(stickPivot.transform, "StickCylRod", new Vector3(0f, -0.4f, 2.2f), new Vector3(0.28f, 1.4f, 0.28f), chromePistonMat ?? darkSteelMat);
            stickRod.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);

            // =========================================================================
            // 6. HEAVY ROCK BUCKET (Mining Shovel Shell & 5 Rock Teeth)
            // =========================================================================
            GameObject bucketPivot = new GameObject("BucketPivot");
            bucketPivot.transform.SetParent(stickPivot.transform, false);
            bucketPivot.transform.localPosition = new Vector3(0f, -3.4f, 3.4f);
            controller.bucketTransform = bucketPivot.transform;

            // Curved Rock Bucket Shell Body
            CreateBox(bucketPivot.transform, "BucketShovelBack", new Vector3(0f, -0.4f, 0.4f), new Vector3(3.2f, 2.4f, 1.8f), darkSteelMat);
            CreateBox(bucketPivot.transform, "BucketShovelFloor", new Vector3(0f, -1.3f, 1.2f), new Vector3(3.1f, 0.45f, 2.2f), darkSteelMat);

            // Heavy Side Wear Cutters / Shrouds
            CreateBox(bucketPivot.transform, "BucketSideCutterLeft", new Vector3(-1.55f, -0.3f, 1.1f), new Vector3(0.22f, 2.2f, 2.4f), darkSteelMat);
            CreateBox(bucketPivot.transform, "BucketSideCutterRight", new Vector3(1.55f, -0.3f, 1.1f), new Vector3(0.22f, 2.2f, 2.4f), darkSteelMat);

            // Upper Rock Spill Guard
            CreateBox(bucketPivot.transform, "SpillGuard", new Vector3(0f, 0.85f, -0.2f), new Vector3(3.2f, 0.6f, 0.35f), darkSteelMat);

            // 4-Bar Bucket Tipping Linkage & Dog-Bone Arm
            CreateBox(bucketPivot.transform, "BucketLinkageLeft", new Vector3(-0.6f, 0.5f, 0.5f), new Vector3(0.18f, 0.9f, 0.7f), darkSteelMat);
            CreateBox(bucketPivot.transform, "BucketLinkageRight", new Vector3(0.6f, 0.5f, 0.5f), new Vector3(0.18f, 0.9f, 0.7f), darkSteelMat);

            // 5 Heavy Hardened Digging Teeth with Tapered Rock Chisel Profile
            float[] toothX = new float[] { -1.25f, -0.62f, 0f, 0.62f, 1.25f };
            for (int i = 0; i < toothX.Length; i++)
            {
                CreateBox(bucketPivot.transform, $"BucketTooth_{i + 1}", new Vector3(toothX[i], -1.35f, 2.4f), new Vector3(0.28f, 0.28f, 0.85f), darkSteelMat);
            }

            // Dynamic Ore / Overburden Payload inside Bucket (Visible during scooping cycle)
            GameObject coalInBucket = CreateBox(bucketPivot.transform, "BucketOreLoad", new Vector3(0f, -0.3f, 1.0f), new Vector3(2.8f, 1.8f, 2.0f), darkSteelMat);
            Renderer coalRend = coalInBucket.GetComponent<Renderer>();
            if (coalRend != null) coalRend.material.color = new Color(0.12f, 0.10f, 0.08f);
            coalInBucket.SetActive(false); // starts empty until scooping
            controller.bucketOreTransform = coalInBucket.transform;

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 3.8f, 1.5f);
            col.size = new Vector3(7.6f, 7.6f, 15.5f);

            return container;
        }

        private static GameObject BuildProceduralBulldozer(Transform visualRoot, FMSUnitController controller)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            CreateBox(container.transform, "TrackLeft", new Vector3(-1.85f, 1.0f, 0f), new Vector3(0.9f, 1.9f, 6.8f), darkSteelMat);
            CreateBox(container.transform, "TrackRight", new Vector3(1.85f, 1.0f, 0f), new Vector3(0.9f, 1.9f, 6.8f), darkSteelMat);
            CreateBox(container.transform, "EngineBody", new Vector3(0f, 2.2f, 0.8f), new Vector3(2.6f, 1.8f, 3.6f), yellowPaintMat);

            GameObject cab = CreateBox(container.transform, "Cab", new Vector3(0f, 3.2f, -0.9f), new Vector3(2.4f, 1.8f, 2.2f), cabGlassMat);
            controller.cabTransform = cab.transform;

            GameObject beaconObj = CreateCylinder(cab.transform, "StatusBeacon", new Vector3(0f, 1.05f, 0f), new Vector3(0.35f, 0.3f, 0.35f), emissiveOrangeMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            GameObject bladePivot = new GameObject("BladePivot");
            bladePivot.transform.SetParent(container.transform, false);
            bladePivot.transform.localPosition = new Vector3(0f, 1.2f, 3.5f);
            controller.bladeTransform = bladePivot.transform;
            CreateBox(bladePivot.transform, "BladeCenter", new Vector3(0f, 0.5f, 0f), new Vector3(4.8f, 2.2f, 0.45f), yellowPaintMat);

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.2f, 0f);
            col.size = new Vector3(5.2f, 4.6f, 9.5f);

            return container;
        }

        private static GameObject BuildProceduralGrader(Transform visualRoot, FMSUnitController controller)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            GameObject frame = CreateBox(container.transform, "MainFrame", new Vector3(0f, 2.2f, 1.2f), new Vector3(0.9f, 0.8f, 6.2f), yellowPaintMat);
            frame.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            CreateBox(container.transform, "RearEngine", new Vector3(0f, 2.0f, -3.4f), new Vector3(2.4f, 1.6f, 3.8f), yellowPaintMat);

            GameObject cab = CreateBox(container.transform, "Cab", new Vector3(0f, 2.8f, -1.2f), new Vector3(1.8f, 1.9f, 2.0f), cabGlassMat);
            controller.cabTransform = cab.transform;

            GameObject beaconObj = CreateCylinder(cab.transform, "StatusBeacon", new Vector3(0f, 1.05f, 0f), new Vector3(0.35f, 0.3f, 0.35f), emissiveOrangeMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            GameObject bladePivot = new GameObject("MoldboardPivot");
            bladePivot.transform.SetParent(container.transform, false);
            bladePivot.transform.localPosition = new Vector3(0f, 0.9f, 0.6f);
            controller.bladeTransform = bladePivot.transform;
            CreateBox(bladePivot.transform, "MoldboardBlade", new Vector3(0f, 0f, 0f), new Vector3(4.6f, 0.9f, 0.25f), darkSteelMat);

            Transform[] wheelList = new Transform[6];
            Vector3[] wheelPos = new Vector3[]
            {
                new Vector3(-1.6f, 0.95f, 4.6f),
                new Vector3(1.6f, 0.95f, 4.6f),
                new Vector3(-1.6f, 0.95f, -2.4f),
                new Vector3(1.6f, 0.95f, -2.4f),
                new Vector3(-1.6f, 0.95f, -4.2f),
                new Vector3(1.6f, 0.95f, -4.2f),
            };

            for (int i = 0; i < 6; i++)
            {
                GameObject wheel = CreateWheelMesh(container.transform, $"Wheel_{i}", wheelPos[i], 1.9f, 0.5f, tireRubberMat, darkSteelMat);
                wheelList[i] = wheel.transform;
            }
            controller.wheels = wheelList;

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.8f, 0f);
            col.size = new Vector3(4.8f, 3.8f, 11.8f);

            return container;
        }

        private static GameObject BuildProceduralFuelTruck(Transform visualRoot, FMSUnitController controller)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            CreateBox(container.transform, "Chassis", new Vector3(0f, 1.1f, 0f), new Vector3(2.6f, 0.6f, 9.2f), darkSteelMat);
            GameObject cab = CreateBox(container.transform, "Cab", new Vector3(0f, 2.2f, 2.8f), new Vector3(2.6f, 2.0f, 2.4f), whiteTankerMat);
            controller.cabTransform = cab.transform;

            GameObject beaconObj = CreateCylinder(cab.transform, "StatusBeacon", new Vector3(0f, 1.1f, 0f), new Vector3(0.35f, 0.3f, 0.35f), emissiveOrangeMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            GameObject tank = CreateCylinder(container.transform, "FuelTank", new Vector3(0f, 2.4f, -1.0f), new Vector3(2.6f, 3.2f, 2.0f), whiteTankerMat);
            tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Transform[] wheelList = new Transform[6];
            Vector3[] wheelPos = new Vector3[]
            {
                new Vector3(-1.35f, 0.7f, 3.0f),
                new Vector3(1.35f, 0.7f, 3.0f),
                new Vector3(-1.35f, 0.7f, -1.8f),
                new Vector3(1.35f, 0.7f, -1.8f),
                new Vector3(-1.35f, 0.7f, -3.2f),
                new Vector3(1.35f, 0.7f, -3.2f),
            };

            for (int i = 0; i < 6; i++)
            {
                GameObject wheel = CreateWheelMesh(container.transform, $"Wheel_{i}", wheelPos[i], 1.4f, 0.4f, tireRubberMat, darkSteelMat);
                wheelList[i] = wheel.transform;
            }
            controller.wheels = wheelList;

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.9f, 0f);
            col.size = new Vector3(3.4f, 3.8f, 9.8f);

            return container;
        }

        private static GameObject BuildProceduralWheelLoader(Transform visualRoot, FMSUnitController controller)
        {
            GameObject container = new GameObject("ProceduralMesh");
            container.transform.SetParent(visualRoot, false);

            GameObject rearFrame = CreateBox(container.transform, "RearFrame", new Vector3(0f, 1.8f, -1.8f), new Vector3(3.2f, 1.4f, 4.6f), yellowPaintMat);
            GameObject frontFrame = CreateBox(container.transform, "FrontFrame", new Vector3(0f, 1.6f, 1.8f), new Vector3(2.8f, 1.2f, 3.4f), yellowPaintMat);

            GameObject cab = CreateBox(container.transform, "Cab", new Vector3(0f, 3.5f, -0.4f), new Vector3(2.2f, 2.0f, 2.2f), cabGlassMat);
            controller.cabTransform = cab.transform;

            GameObject beaconObj = CreateCylinder(cab.transform, "StatusBeacon", new Vector3(0f, 1.1f, 0f), new Vector3(0.35f, 0.3f, 0.35f), emissiveCyanMat);
            controller.statusBeaconRenderer = beaconObj.GetComponent<Renderer>();

            GameObject boomPivot = new GameObject("LoaderBoomPivot");
            boomPivot.transform.SetParent(frontFrame.transform, false);
            boomPivot.transform.localPosition = new Vector3(0f, 0.4f, 1.2f);
            controller.boomTransform = boomPivot.transform;

            CreateBox(boomPivot.transform, "Bucket", new Vector3(0f, 0.2f, 4.4f), new Vector3(4.2f, 2.2f, 2.2f), darkSteelMat);

            Transform[] wheelList = new Transform[4];
            Vector3[] wheelPos = new Vector3[]
            {
                new Vector3(-2.1f, 1.4f, 2.0f),
                new Vector3(2.1f, 1.4f, 2.0f),
                new Vector3(-2.1f, 1.4f, -2.4f),
                new Vector3(2.1f, 1.4f, -2.4f),
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject wheel = CreateWheelMesh(container.transform, $"Wheel_{i}", wheelPos[i], 2.8f, 0.85f, tireRubberMat, darkSteelMat);
                wheelList[i] = wheel.transform;
            }
            controller.wheels = wheelList;

            BoxCollider col = controller.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.5f, 0.5f);
            col.size = new Vector3(4.8f, 5.2f, 12.5f);

            return container;
        }

        // =========================================================================
        // HELPER PRIMITIVE BUILDERS
        // =========================================================================
        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        private static GameObject CreateWheelMesh(Transform parent, string name, Vector3 localPos, float diameter, float width, Material tireMat, Material hubMat)
        {
            GameObject wheelRoot = new GameObject(name);
            wheelRoot.transform.SetParent(parent, false);
            wheelRoot.transform.localPosition = localPos;

            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = "Tire";
            tire.transform.SetParent(wheelRoot.transform, false);
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tire.transform.localScale = new Vector3(diameter, width * 0.5f, diameter);
            tire.GetComponent<Renderer>().sharedMaterial = tireMat;
            Object.Destroy(tire.GetComponent<Collider>());

            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(wheelRoot.transform, false);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = new Vector3(diameter * 0.52f, width * 0.52f, diameter * 0.52f);
            hub.GetComponent<Renderer>().sharedMaterial = hubMat;
            Object.Destroy(hub.GetComponent<Collider>());

            return wheelRoot;
        }

        public static GameObject CreateLocationBeacon(string id, string name, Vector3 position, Color beaconColor)
        {
            EnsureMaterials();

            GameObject beacon = new GameObject($"BEACON_{id}");
            beacon.transform.position = position;

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(beacon.transform, false);
            pole.transform.localPosition = new Vector3(0f, 15f, 0f);
            pole.transform.localScale = new Vector3(2.5f, 15f, 2.5f);
            
            Material pMat = new Material(beaconMat) { color = beaconColor };
            pole.GetComponent<Renderer>().sharedMaterial = pMat;
            Object.Destroy(pole.GetComponent<Collider>());

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "TopOrb";
            top.transform.SetParent(beacon.transform, false);
            top.transform.localPosition = new Vector3(0f, 32f, 0f);
            top.transform.localScale = new Vector3(8f, 8f, 8f);
            top.GetComponent<Renderer>().sharedMaterial = pMat;
            Object.Destroy(top.GetComponent<Collider>());

            return beacon;
        }
    }
}
