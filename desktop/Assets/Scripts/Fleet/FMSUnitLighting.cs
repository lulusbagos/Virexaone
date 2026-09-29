using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    /// <summary>
    /// Ultra-realistic, high-performance Mining Machine Lighting Rig for GLB & procedural units.
    /// Uses pure emissive unlit materials for zero-overhead, flicker-free rendering across 156 units,
    /// with volumetric dust light cones, rotating amber ESDM safety beacon, and selective close-range spotlights.
    /// </summary>
    public class FMSUnitLighting : MonoBehaviour
    {
        [Header("Rig Status")]
        public bool isNightActive = false;
        public bool forceLightsOn = false;

        [Header("Lighting Components")]
        private GameObject lightRigRoot;
        private Light mainSpotlight;
        private List<GameObject> volumetricBeams = new List<GameObject>();
        private List<Renderer> headlightRenderers = new List<Renderer>();
        private List<Renderer> taillightRenderers = new List<Renderer>();
        private List<Renderer> worklightRenderers = new List<Renderer>();
        
        [Header("Rotating Amber Safety Beacon")]
        private Transform beaconRotatorHead;
        private float beaconAngle = 0f;

        // Shared Static Materials (Zero Garbage Collection, Pure Unlit / Self-Illuminated)
        private static Material sharedHeadlightLensMat;
        private static Material sharedTaillightLensMat;
        private static Material sharedBrakeActiveLensMat;
        private static Material sharedWorklightLensMat;
        private static Material sharedAmberBeaconMat;
        private static Material sharedVolumetricBeamMat;
        private static Mesh sharedVolumetricConeMesh;

        private FMSUnitController controller;
        private float lodCheckTimer = 0f;
        private bool isCloseRange = false;
        private const float CLOSE_SPOTLIGHT_RANGE = 75f; // Real-time terrain spotlight active within 75m to prevent light popping

        public static void EnsureLightingMaterials()
        {
            if (sharedHeadlightLensMat != null) return;

            Shader unlitShader = Shader.Find("Unlit/Color") 
                ?? Shader.Find("Mobile/Unlit (Supports Lightmap)") 
                ?? Shader.Find("Particles/Standard Unlit") 
                ?? Shader.Find("Sprites/Default") 
                ?? Shader.Find("Standard");

            Shader transparentShader = Shader.Find("Particles/Standard Unlit") 
                ?? Shader.Find("Mobile/Particles/Alpha Blended") 
                ?? Shader.Find("Sprites/Default") 
                ?? Shader.Find("Unlit/Transparent") 
                ?? unlitShader;

            // 1. Headlight Xenon Lens (Self-Illuminated Bright Bulb)
            sharedHeadlightLensMat = new Material(unlitShader)
            {
                color = new Color(1.0f, 0.98f, 0.90f, 1f)
            };

            // 2. Taillight Normal Red
            sharedTaillightLensMat = new Material(unlitShader)
            {
                color = new Color(0.85f, 0.12f, 0.08f, 1f)
            };

            // 3. Brake Light Active Red (Intense Glow)
            sharedBrakeActiveLensMat = new Material(unlitShader)
            {
                color = new Color(1.0f, 0.22f, 0.12f, 1f)
            };

            // 4. Heavy-Duty Mining LED Worklight Lens
            sharedWorklightLensMat = new Material(unlitShader)
            {
                color = new Color(0.90f, 0.95f, 1.0f, 1f)
            };

            // 5. Amber ESDM Mining Safety Rotator Lens
            sharedAmberBeaconMat = new Material(unlitShader)
            {
                color = new Color(1.0f, 0.65f, 0.05f, 1f)
            };

            // 6. Volumetric Fog/Dust Light Beam Cone (Authentic Mining Night Atmosphere)
            sharedVolumetricBeamMat = new Material(transparentShader);
            if (sharedVolumetricBeamMat.HasProperty("_Color"))
                sharedVolumetricBeamMat.SetColor("_Color", new Color(1.0f, 0.96f, 0.85f, 0.085f));
            sharedVolumetricBeamMat.renderQueue = 3100; // Transparent queue

            // Shared Volumetric Cone Geometry
            sharedVolumetricConeMesh = BuildVolumetricConeMesh(0.35f, 4.2f, 32.0f, 12);
        }

        private static Mesh BuildVolumetricConeMesh(float startRadius, float endRadius, float length, int segments)
        {
            Mesh mesh = new Mesh { name = "Mesh_VolumetricLightCone" };

            int vertCount = segments * 2;
            Vector3[] verts = new Vector3[vertCount];
            Color[] colors = new Color[vertCount];
            int[] tris = new int[segments * 6];

            float angleStep = 360f / segments * Mathf.Deg2Rad;

            for (int i = 0; i < segments; i++)
            {
                float a = i * angleStep;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // Start ring (near bulb)
                verts[i * 2] = new Vector3(cos * startRadius, sin * startRadius, 0f);
                colors[i * 2] = new Color(1f, 1f, 1f, 0.50f);

                // End ring (far beam spread)
                verts[i * 2 + 1] = new Vector3(cos * endRadius, sin * endRadius, length);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0.0f); // Fades to zero
            }

            // Triangles
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int baseIndex = i * 6;

                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int n0 = next * 2;
                int n1 = next * 2 + 1;

                tris[baseIndex + 0] = i0;
                tris[baseIndex + 1] = i1;
                tris[baseIndex + 2] = n0;

                tris[baseIndex + 3] = n0;
                tris[baseIndex + 4] = i1;
                tris[baseIndex + 5] = n1;
            }

            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Awake()
        {
            controller = GetComponent<FMSUnitController>();
            EnsureLightingMaterials();
        }

        private void Start()
        {
            BuildLightingRig();

            // Sync with current atmosphere time
            if (FMSDigitalTwinAtmosphere.Instance != null)
            {
                SetNightMode(FMSDigitalTwinAtmosphere.Instance.IsNightOrDark);
            }
            else
            {
                SetNightMode(false);
            }
        }

        private void OnEnable()
        {
            FMSDigitalTwinAtmosphere.OnNightModeChanged += SetNightMode;
        }

        private void OnDisable()
        {
            FMSDigitalTwinAtmosphere.OnNightModeChanged -= SetNightMode;
        }

        public void SetNightMode(bool night)
        {
            isNightActive = night;
            UpdateRigVisibility();
        }

        /// <summary>
        /// Generates authentic machine-specific light positions and attachments.
        /// </summary>
        public void BuildLightingRig()
        {
            if (lightRigRoot != null)
            {
                Destroy(lightRigRoot);
            }

            mainSpotlight = null;
            volumetricBeams.Clear();
            headlightRenderers.Clear();
            taillightRenderers.Clear();
            worklightRenderers.Clear();

            lightRigRoot = new GameObject("--- UNIT_LIGHTS_RIG ---");
            lightRigRoot.transform.SetParent(this.transform, false);

            UnitType type = (controller != null) ? controller.unitType : UnitType.HaulTruck;

            switch (type)
            {
                case UnitType.HaulTruck:
                case UnitType.FuelTruck:
                    BuildHaulTruckLights();
                    break;
                case UnitType.Excavator:
                    BuildExcavatorLights();
                    break;
                case UnitType.Bulldozer:
                    BuildBulldozerLights();
                    break;
                case UnitType.Grader:
                    BuildGraderLights();
                    break;
                case UnitType.WheelLoader:
                    BuildWheelLoaderLights();
                    break;
                case UnitType.Support:
                    AddSafetyBeacon(new Vector3(0f, 2.5f, 0f));
                    break;
                default:
                    AddSafetyBeacon(new Vector3(0f, 2.5f, 0f));
                    break;
            }

            UpdateRigVisibility();
        }

        private void BuildHaulTruckLights()
        {
            // 1. Dual Front Xenon Headlight Lenses (Bumper / Grille)
            AddHeadlightFixture(new Vector3(-1.40f, 2.15f, 4.85f), Quaternion.Euler(7f, -1.5f, 0f), hasBeam: true);
            AddHeadlightFixture(new Vector3(1.40f, 2.15f, 4.85f), Quaternion.Euler(7f, 1.5f, 0f), hasBeam: true);

            // 2. Single Combined Forward Real-time Spotlight (Center Bumper, active only at close range)
            AddForwardSpotlight(new Vector3(0f, 2.30f, 4.85f), Quaternion.Euler(8f, 0f, 0f), 55f, 75f, 2.8f);

            // 3. Cab Roof Dual LED Worklights (Facing Downward onto haul road)
            AddWorklightFixture(new Vector3(-1.60f, 4.90f, 2.10f), Quaternion.Euler(24f, -4f, 0f));
            AddWorklightFixture(new Vector3(-1.10f, 4.90f, 2.10f), Quaternion.Euler(24f, 4f, 0f));

            // 4. Rotating Amber Safety Beacon (Top of Cab)
            AddSafetyBeacon(new Vector3(-1.60f, 5.35f, 1.55f));

            // 5. Rear Red Taillights / Brake Markers
            AddTaillight(new Vector3(-1.85f, 1.65f, -4.95f));
            AddTaillight(new Vector3(1.85f, 1.65f, -4.95f));
        }

        private void BuildExcavatorLights()
        {
            // 1. Cab Canopy LED Worklights
            AddWorklightFixture(new Vector3(-2.25f, 4.95f, 2.10f), Quaternion.Euler(18f, -6f, 0f));
            AddWorklightFixture(new Vector3(-1.75f, 4.95f, 2.10f), Quaternion.Euler(18f, 6f, 0f));

            // 2. Boom Pit Face Heavy Floodlight (Shines into dig bench)
            AddWorklightFixture(new Vector3(0.85f, 4.30f, 3.60f), Quaternion.Euler(12f, 0f, 0f));
            AddForwardSpotlight(new Vector3(0.85f, 4.30f, 3.60f), Quaternion.Euler(14f, 0f, 0f), 65f, 85f, 3.2f);

            // 3. Rotating Amber Safety Beacon
            AddSafetyBeacon(new Vector3(-2.25f, 5.45f, 1.15f));

            // 4. Rear Counterweight Hazard Markers
            AddTaillight(new Vector3(-1.95f, 3.40f, -4.30f));
            AddTaillight(new Vector3(1.95f, 3.40f, -4.30f));
        }

        private void BuildBulldozerLights()
        {
            // 1. ROPS Canopy Front LED Worklights
            AddHeadlightFixture(new Vector3(-1.15f, 3.85f, 1.25f), Quaternion.Euler(12f, -2f, 0f), hasBeam: true);
            AddHeadlightFixture(new Vector3(1.15f, 3.85f, 1.25f), Quaternion.Euler(12f, 2f, 0f), hasBeam: true);
            AddForwardSpotlight(new Vector3(0f, 3.85f, 1.25f), Quaternion.Euler(14f, 0f, 0f), 48f, 75f, 2.6f);

            // 2. Blade Floodlights
            AddWorklightFixture(new Vector3(-1.55f, 1.75f, 3.20f), Quaternion.Euler(16f, 0f, 0f));
            AddWorklightFixture(new Vector3(1.55f, 1.75f, 3.20f), Quaternion.Euler(16f, 0f, 0f));

            // 3. Safety Beacon
            AddSafetyBeacon(new Vector3(0f, 4.25f, 0.15f));

            // 4. Rear Ripper Work / Hazard Lights
            AddTaillight(new Vector3(-1.05f, 3.35f, -2.75f));
            AddTaillight(new Vector3(1.05f, 3.35f, -2.75f));
        }

        private void BuildGraderLights()
        {
            // 1. Front Nose Haulroad Projector Lamps
            AddHeadlightFixture(new Vector3(-0.75f, 1.85f, 4.60f), Quaternion.Euler(8f, -2f, 0f), hasBeam: true);
            AddHeadlightFixture(new Vector3(0.75f, 1.85f, 4.60f), Quaternion.Euler(8f, 2f, 0f), hasBeam: true);
            AddForwardSpotlight(new Vector3(0f, 1.85f, 4.60f), Quaternion.Euler(9f, 0f, 0f), 52f, 70f, 2.6f);

            // 2. Cab Roof Worklights
            AddWorklightFixture(new Vector3(-0.85f, 3.80f, -0.20f), Quaternion.Euler(22f, -8f, 0f));
            AddWorklightFixture(new Vector3(0.85f, 3.80f, -0.20f), Quaternion.Euler(22f, 8f, 0f));

            // 3. Safety Beacon
            AddSafetyBeacon(new Vector3(0f, 4.15f, -0.45f));

            // 4. Rear Taillights
            AddTaillight(new Vector3(-0.75f, 1.80f, -4.70f));
            AddTaillight(new Vector3(0.75f, 1.80f, -4.70f));
        }

        private void BuildWheelLoaderLights()
        {
            // 1. Front Frame Work / Driving Lights
            AddHeadlightFixture(new Vector3(-1.10f, 2.20f, 3.40f), Quaternion.Euler(10f, -2f, 0f), hasBeam: true);
            AddHeadlightFixture(new Vector3(1.10f, 2.20f, 3.40f), Quaternion.Euler(10f, 2f, 0f), hasBeam: true);
            AddForwardSpotlight(new Vector3(0f, 2.20f, 3.40f), Quaternion.Euler(12f, 0f, 0f), 50f, 75f, 2.6f);

            // 2. Cab Worklights
            AddWorklightFixture(new Vector3(-1.0f, 4.30f, 0.40f), Quaternion.Euler(20f, 0f, 0f));
            AddWorklightFixture(new Vector3(1.0f, 4.30f, 0.40f), Quaternion.Euler(20f, 0f, 0f));

            // 3. Safety Beacon
            AddSafetyBeacon(new Vector3(0f, 4.70f, 0.10f));

            // 4. Rear Engine Hood Taillights
            AddTaillight(new Vector3(-1.0f, 2.10f, -3.80f));
            AddTaillight(new Vector3(1.0f, 2.10f, -3.80f));
        }

        // =========================================================================
        // HELPER BUILDERS (Clean & Zero Overhead)
        // =========================================================================
        private void AddHeadlightFixture(Vector3 localPos, Quaternion localRot, bool hasBeam)
        {
            GameObject lampObj = new GameObject("Headlight_Fixture");
            lampObj.transform.SetParent(lightRigRoot.transform, false);
            lampObj.transform.localPosition = localPos;
            lampObj.transform.localRotation = localRot;

            // 1. Glowing Bulb Lens Mesh (Unlit Emissive)
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb_Lens";
            bulb.transform.SetParent(lampObj.transform, false);
            bulb.transform.localScale = new Vector3(0.42f, 0.42f, 0.18f);
            var r = bulb.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = sharedHeadlightLensMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            headlightRenderers.Add(r);
            var col = bulb.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 2. Volumetric Fog Beam Mesh
            if (hasBeam && sharedVolumetricConeMesh != null)
            {
                GameObject beam = new GameObject("Volumetric_Beam_Cone");
                beam.transform.SetParent(lampObj.transform, false);
                beam.transform.localPosition = Vector3.forward * 0.1f;
                var mf = beam.AddComponent<MeshFilter>();
                mf.sharedMesh = sharedVolumetricConeMesh;
                var mr = beam.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sharedVolumetricBeamMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                volumetricBeams.Add(beam);
            }
        }

        private void AddForwardSpotlight(Vector3 localPos, Quaternion localRot, float range, float spotAngle, float intensity)
        {
            GameObject spotObj = new GameObject("Unit_Terrain_Spotlight");
            spotObj.transform.SetParent(lightRigRoot.transform, false);
            spotObj.transform.localPosition = localPos;
            spotObj.transform.localRotation = localRot;

            Light lightComp = spotObj.AddComponent<Light>();
            lightComp.type = LightType.Spot;
            lightComp.range = range;
            lightComp.spotAngle = spotAngle;
            lightComp.color = new Color(1.0f, 0.96f, 0.88f);
            lightComp.intensity = intensity;
            lightComp.shadows = LightShadows.None;
            lightComp.renderMode = LightRenderMode.Auto;
            lightComp.enabled = false;

            mainSpotlight = lightComp;
        }

        private void AddWorklightFixture(Vector3 localPos, Quaternion localRot)
        {
            GameObject lampObj = new GameObject("Worklight_LED");
            lampObj.transform.SetParent(lightRigRoot.transform, false);
            lampObj.transform.localPosition = localPos;
            lampObj.transform.localRotation = localRot;

            // Lens (Unlit LED)
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = "Worklight_Lens";
            bulb.transform.SetParent(lampObj.transform, false);
            bulb.transform.localScale = new Vector3(0.48f, 0.28f, 0.16f);
            var r = bulb.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = sharedWorklightLensMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            worklightRenderers.Add(r);
            var col = bulb.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void AddSafetyBeacon(Vector3 localPos)
        {
            GameObject beaconRoot = new GameObject("ESDM_Safety_Rotator_Beacon");
            beaconRoot.transform.SetParent(lightRigRoot.transform, false);
            beaconRoot.transform.localPosition = localPos;

            // Base bracket
            GameObject bBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bBase.transform.SetParent(beaconRoot.transform, false);
            bBase.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
            var bCol = bBase.GetComponent<Collider>();
            if (bCol != null) Destroy(bCol);

            // Rotating head (Self-illuminated Amber)
            GameObject bHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bHead.name = "Amber_Rotator_Head";
            bHead.transform.SetParent(beaconRoot.transform, false);
            bHead.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            bHead.transform.localScale = new Vector3(0.30f, 0.18f, 0.30f);
            var headRen = bHead.GetComponent<Renderer>();
            if (headRen != null)
            {
                headRen.sharedMaterial = sharedAmberBeaconMat;
                headRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                headRen.receiveShadows = false;
            }
            var headCol = bHead.GetComponent<Collider>();
            if (headCol != null) Destroy(headCol);

            beaconRotatorHead = bHead.transform;
        }

        private void AddTaillight(Vector3 localPos)
        {
            GameObject tailObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tailObj.name = "Tail_Marker_Red";
            tailObj.transform.SetParent(lightRigRoot.transform, false);
            tailObj.transform.localPosition = localPos;
            tailObj.transform.localScale = new Vector3(0.38f, 0.22f, 0.12f);
            var r = tailObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = sharedTaillightLensMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            taillightRenderers.Add(r);
            var col = tailObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // =========================================================================
        // REAL-TIME UPDATE & ANIMATION (Flicker-Free)
        // =========================================================================
        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. Smoothly Rotate Amber Safety Beacon (Continuous 90 RPM Rotation)
            if (beaconRotatorHead != null)
            {
                beaconAngle = (beaconAngle + dt * 360f * 1.5f) % 360f;
                beaconRotatorHead.localRotation = Quaternion.Euler(0f, beaconAngle, 0f);
            }

            // 2. Dynamic Brake Light Response
            bool isBraking = (controller != null) && (controller.currentSpeedKmh < 1.0f || controller.currentState == UnitState.QueueingAtPit || controller.currentState == UnitState.QueueingAtDump);
            Material targetTailMat = isBraking ? sharedBrakeActiveLensMat : sharedTaillightLensMat;

            for (int i = 0; i < taillightRenderers.Count; i++)
            {
                if (taillightRenderers[i] != null && taillightRenderers[i].sharedMaterial != targetTailMat)
                {
                    taillightRenderers[i].sharedMaterial = targetTailMat;
                }
            }

            // 3. Proximity LOD for Spotlight (Checks every 0.5s to save CPU)
            lodCheckTimer += dt;
            if (lodCheckTimer > 0.5f)
            {
                lodCheckTimer = 0f;
                UpdateLodState();
            }
        }

        private void UpdateLodState()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            bool isSelected = (FMSFleetManager.Instance != null && FMSFleetManager.Instance.selectedUnit == controller);
            float distSq = (transform.position - cam.transform.position).sqrMagnitude;
            bool shouldBeClose = isSelected || (distSq < (CLOSE_SPOTLIGHT_RANGE * CLOSE_SPOTLIGHT_RANGE));

            if (shouldBeClose != isCloseRange)
            {
                isCloseRange = shouldBeClose;
                if (mainSpotlight != null)
                {
                    mainSpotlight.enabled = (isNightActive || forceLightsOn) && isCloseRange;
                }
            }
        }

        private void UpdateRigVisibility()
        {
            bool lightsOn = (isNightActive || forceLightsOn);

            // Single Spotlight (Active only when night and close/selected)
            if (mainSpotlight != null)
            {
                mainSpotlight.enabled = lightsOn && isCloseRange;
            }

            // Volumetric dust light beams (Always visible at night, zero draw call overhead)
            for (int i = 0; i < volumetricBeams.Count; i++)
            {
                if (volumetricBeams[i] != null) volumetricBeams[i].SetActive(lightsOn);
            }

            // Emissive Lenses (Always active & bright at night with zero flickering)
            for (int i = 0; i < headlightRenderers.Count; i++)
            {
                if (headlightRenderers[i] != null) headlightRenderers[i].gameObject.SetActive(lightsOn);
            }
            for (int i = 0; i < worklightRenderers.Count; i++)
            {
                if (worklightRenderers[i] != null) worklightRenderers[i].gameObject.SetActive(lightsOn);
            }
        }
    }
}
