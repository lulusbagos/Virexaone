using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSDigitalTwinAtmosphere : MonoBehaviour
    {
        public static FMSDigitalTwinAtmosphere Instance { get; private set; }

        [Header("Atmosphere Settings")]
        public bool enableAtmosphere = true;
        public bool enableDigitalTwinSkirt = true;
        public bool enableDroneInspection = false;

        [Header("Sky & Fog Colors")]
        public Color daySkyColor = new Color(0.12f, 0.16f, 0.22f, 1f); // Deep Cyan Atmospheric Horizon
        public Color dayFogColor = new Color(0.20f, 0.26f, 0.32f, 1f);
        public Color duskSkyColor = new Color(0.24f, 0.14f, 0.12f, 1f);
        public Color duskFogColor = new Color(0.35f, 0.18f, 0.12f, 1f);
        public Color nightSkyColor = new Color(0.02f, 0.03f, 0.06f, 1f);
        public Color nightFogColor = new Color(0.04f, 0.06f, 0.10f, 1f);

        [Header("Drone Cinematic Tour")]
        public float droneOrbitRadius = 2600f;
        public float droneOrbitHeight = 1100f;
        public float droneSpeed = 7.5f;
        public Vector3 droneCenterPivot = new Vector3(0f, 150f, 0f);
        private float droneAngle = 0f;

        [Header("Procedural Skirt & Base")]
        private GameObject skirtObject;
        private Material skirtMaterial;

        [Header("Pit Night Floodlights")]
        private List<GameObject> pitFloodlights = new List<GameObject>();
        public bool areFloodlightsOn = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            SetupCameraAtmosphere();
            CreateDigitalTwinSkirt();
        }

        private void Start()
        {
            ApplyAtmosphere(PlayerPrefs.GetInt("Virexa_TimeOfDay", 0));
        }

        private void Update()
        {
            if (enableDroneInspection)
            {
                UpdateDroneInspection();
            }
        }

        public void SetupCameraAtmosphere()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = daySkyColor;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00012f;
            RenderSettings.fogColor = dayFogColor;
        }

        [Header("Time Cycle State")]
        public int currentCycleIndex = 0;
        public bool IsNightOrDark => currentCycleIndex == 1 || currentCycleIndex == 2;
        public static event Action<bool> OnNightModeChanged;

        public void ApplyAtmosphere(int timeCycleIndex)
        {
            timeCycleIndex = Mathf.Clamp(timeCycleIndex, 0, 3);
            currentCycleIndex = timeCycleIndex;
            bool isNight = (timeCycleIndex == 1 || timeCycleIndex == 2);
            Color targetSky, targetFog;
            switch (timeCycleIndex)
            {
                case 0: // Day
                    targetSky = daySkyColor;
                    targetFog = dayFogColor;
                    SetPitFloodlights(false);
                    break;
                case 1: // Golden Hour / Dusk
                    targetSky = duskSkyColor;
                    targetFog = duskFogColor;
                    SetPitFloodlights(true);
                    break;
                case 2: // Night Shift
                    targetSky = nightSkyColor;
                    targetFog = nightFogColor;
                    SetPitFloodlights(true);
                    break;
                case 3: // Dawn
                    targetSky = new Color(0.18f, 0.18f, 0.26f);
                    targetFog = new Color(0.24f, 0.22f, 0.28f);
                    SetPitFloodlights(false);
                    break;
                default:
                    targetSky = daySkyColor;
                    targetFog = dayFogColor;
                    break;
            }

            if (Camera.main != null)
            {
                Camera.main.backgroundColor = targetSky;
            }
            RenderSettings.fogColor = targetFog;

            OnNightModeChanged?.Invoke(isNight);
        }

        public void CreateDigitalTwinSkirt()
        {
            if (skirtObject != null) return;

            Terrain activeTerrain = Terrain.activeTerrain;
            float minX = -3200f, maxX = 3200f;
            float minZ = -2800f, maxZ = 2800f;
            float baseFloorY = -180f;
            float topY = 120f;

            if (activeTerrain != null)
            {
                Vector3 tPos = activeTerrain.transform.position;
                Vector3 tSize = activeTerrain.terrainData.size;
                minX = tPos.x;
                maxX = tPos.x + tSize.x;
                minZ = tPos.z;
                maxZ = tPos.z + tSize.z;
            }

            skirtObject = new GameObject("--- DIGITAL_TWIN_SOLID_PEDESTAL ---");
            skirtObject.transform.SetParent(this.transform);

            Shader unlit = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            skirtMaterial = new Material(unlit)
            {
                color = new Color(0.04f, 0.07f, 0.11f, 1f) // Obsidian basalt digital twin base
            };

            // Create solid outer pedestal walls around the 4 sides
            CreateWall(skirtObject.transform, new Vector3((minX + maxX) / 2f, (topY + baseFloorY) / 2f, maxZ), new Vector3(maxX - minX, topY - baseFloorY, 8f));
            CreateWall(skirtObject.transform, new Vector3((minX + maxX) / 2f, (topY + baseFloorY) / 2f, minZ), new Vector3(maxX - minX, topY - baseFloorY, 8f));
            CreateWall(skirtObject.transform, new Vector3(minX, (topY + baseFloorY) / 2f, (minZ + maxZ) / 2f), new Vector3(8f, topY - baseFloorY, maxZ - minZ));
            CreateWall(skirtObject.transform, new Vector3(maxX, (topY + baseFloorY) / 2f, (minZ + maxZ) / 2f), new Vector3(8f, topY - baseFloorY, maxZ - minZ));

            // Solid Base Floor Plate
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Pedestal_Floor_Plate";
            floor.transform.SetParent(skirtObject.transform);
            floor.transform.position = new Vector3((minX + maxX) / 2f, baseFloorY - 4f, (minZ + maxZ) / 2f);
            floor.transform.localScale = new Vector3(maxX - minX + 30f, 8f, maxZ - minZ + 30f);
            if (floor.GetComponent<Renderer>() != null) floor.GetComponent<Renderer>().sharedMaterial = skirtMaterial;
            Collider c = floor.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private void CreateWall(Transform parent, Vector3 pos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Pedestal_Wall";
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (wall.GetComponent<Renderer>() != null)
                wall.GetComponent<Renderer>().sharedMaterial = skirtMaterial;
            Collider col = wall.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        public void ToggleDroneInspection()
        {
            enableDroneInspection = !enableDroneInspection;
            if (enableDroneInspection)
            {
                droneAngle = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
                if (FMSCameraController.Instance != null)
                    FMSCameraController.Instance.enabled = false;
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("🚁 Mode Inspeksi Drone Aktif (Cinematic Auto-Orbit)");
            }
            else
            {
                if (FMSCameraController.Instance != null)
                    FMSCameraController.Instance.enabled = true;
                if (FMSDashboardUI.Instance != null)
                    FMSDashboardUI.Instance.ShowNotification("🛑 Mode Drone Selesai. Kontrol Manual Aktif.");
            }
        }

        private void UpdateDroneInspection()
        {
            droneAngle += droneSpeed * Time.unscaledDeltaTime;
            float rad = droneAngle * Mathf.Deg2Rad;

            float x = droneCenterPivot.x + Mathf.Cos(rad) * droneOrbitRadius;
            float z = droneCenterPivot.z + Mathf.Sin(rad) * droneOrbitRadius;
            float y = droneOrbitHeight + Mathf.Sin(rad * 2f) * 120f; // Gentle undulating elevation

            Vector3 camPos = new Vector3(x, y, z);
            if (Camera.main != null)
            {
                Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, camPos, Time.unscaledDeltaTime * 4f);
                Quaternion targetRot = Quaternion.LookRotation(droneCenterPivot - Camera.main.transform.position);
                Camera.main.transform.rotation = Quaternion.Slerp(Camera.main.transform.rotation, targetRot, Time.unscaledDeltaTime * 4f);
            }
        }

        public void SpawnPitFloodlights(List<Vector3> lightPositions)
        {
            foreach (var l in pitFloodlights) if (l != null) Destroy(l);
            pitFloodlights.Clear();

            Shader unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material bulbMat = new Material(unlit) { color = new Color(1f, 0.95f, 0.75f) };

            for (int i = 0; i < lightPositions.Count; i++)
            {
                Vector3 pos = lightPositions[i];
                GameObject tower = new GameObject($"Pit_Light_Tower_{i + 1}");
                tower.transform.SetParent(this.transform);
                tower.transform.position = pos;

                // Tower pole
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.transform.SetParent(tower.transform);
                pole.transform.localPosition = new Vector3(0, 15f, 0);
                pole.transform.localScale = new Vector3(1.2f, 15f, 1.2f);
                Collider col1 = pole.GetComponent<Collider>();
                if (col1 != null) Destroy(col1);

                // Lamp fixture
                GameObject fixture = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fixture.transform.SetParent(tower.transform);
                fixture.transform.localPosition = new Vector3(0, 30f, 0);
                fixture.transform.localScale = new Vector3(4f, 4f, 4f);
                if (fixture.GetComponent<Renderer>() != null) fixture.GetComponent<Renderer>().sharedMaterial = bulbMat;
                Collider col2 = fixture.GetComponent<Collider>();
                if (col2 != null) Destroy(col2);

                // Light component
                Light lightComp = tower.AddComponent<Light>();
                lightComp.type = LightType.Spot;
                lightComp.range = 380f;
                lightComp.spotAngle = 85f;
                lightComp.color = new Color(1f, 0.92f, 0.78f);
                lightComp.intensity = 4.5f;
                tower.transform.rotation = Quaternion.Euler(65f, i * 45f, 0f);

                tower.SetActive(areFloodlightsOn);
                pitFloodlights.Add(tower);
            }
        }

        public void SetPitFloodlights(bool on)
        {
            areFloodlightsOn = on;
            foreach (var tower in pitFloodlights)
            {
                if (tower != null) tower.SetActive(on);
            }
        }
    }
}
