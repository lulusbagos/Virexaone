using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    [Serializable]
    public class MineLocationData
    {
        public string id;
        public string name;
        public double[] utm;
        public string type;
    }

    [Serializable]
    public class HaulRouteData
    {
        public string route_id;
        public string name;
        public List<double[]> waypoints_utm;
    }

    [Serializable]
    public class MineMetadataWrapper
    {
        public string crs;
        public List<MineLocationData> locations;
        public List<HaulRouteData> haul_routes;
    }

    public class MineSpatialManager : MonoBehaviour
    {
        public static MineSpatialManager Instance { get; private set; }

        [Header("Spatial Data Assets")]
        [SerializeField] private TextAsset spatialMetadataJson;
        [SerializeField] private MeshCollider terrainCollider;

        [Header("Runtime Cache")]
        public MineMetadataWrapper Metadata { get; private set; }
        public Dictionary<string, Vector3> LocationPositions { get; private set; } = new Dictionary<string, Vector3>();
        public Dictionary<string, List<Vector3>> RouteWaypoints { get; private set; } = new Dictionary<string, List<Vector3>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            LoadMetadata();
        }

        public void LoadMetadata()
        {
            if (spatialMetadataJson == null)
            {
                spatialMetadataJson = Resources.Load<TextAsset>("mine_spatial_metadata");
                if (spatialMetadataJson == null)
                {
                    Debug.LogWarning("[MineSpatialManager] Metadata json not assigned in inspector. Attempting manual load.");
                }
            }

            if (spatialMetadataJson != null)
            {
                try
                {
                    Metadata = JsonUtility.FromJson<MineMetadataWrapper>(spatialMetadataJson.text);
                    ParseLocationsAndRoutes();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MineSpatialManager] Failed to parse spatial metadata: {ex.Message}");
                }
            }
        }

        private void ParseLocationsAndRoutes()
        {
            LocationPositions.Clear();
            if (Metadata?.locations != null)
            {
                foreach (var loc in Metadata.locations)
                {
                    if (loc.utm != null && loc.utm.Length >= 3)
                    {
                        Vector3 unityPos = GeoCoordinateConverter.UTMToUnity(loc.utm[0], loc.utm[1], loc.utm[2]);
                        // Align elevation with raycast if collider is active
                        float groundY = GetTerrainHeightAt(unityPos.x, unityPos.z);
                        if (groundY > -500f)
                        {
                            unityPos.y = groundY;
                        }
                        LocationPositions[loc.id] = unityPos;
                    }
                }
            }

            RouteWaypoints.Clear();
            if (Metadata?.haul_routes != null)
            {
                foreach (var route in Metadata.haul_routes)
                {
                    List<Vector3> pts = new List<Vector3>();
                    if (route.waypoints_utm != null)
                    {
                        foreach (var wp in route.waypoints_utm)
                        {
                            if (wp.Length >= 3)
                            {
                                Vector3 pos = GeoCoordinateConverter.UTMToUnity(wp[0], wp[1], wp[2]);
                                float gy = GetTerrainHeightAt(pos.x, pos.z);
                                if (gy > -500f) pos.y = gy + 0.5f;
                                pts.Add(pos);
                            }
                        }
                    }
                    RouteWaypoints[route.route_id] = pts;
                }
            }
            Debug.Log($"[MineSpatialManager] Loaded {LocationPositions.Count} mine locations and {RouteWaypoints.Count} haul routes.");
        }

        /// <summary>
        /// Raycasts down to get exact 3D terrain elevation at (x, z).
        /// </summary>
        public float GetTerrainHeightAt(float worldX, float worldZ)
        {
            Vector3 rayOrigin = new Vector3(worldX, 600f, worldZ);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f))
            {
                return hit.point.y;
            }
            return 150f; // fallback baseline
        }

        /// <summary>
        /// Gets terrain normal vector at (x, z) for vehicle slope alignment.
        /// </summary>
        public Vector3 GetTerrainNormalAt(float worldX, float worldZ)
        {
            Vector3 rayOrigin = new Vector3(worldX, 600f, worldZ);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f))
            {
                return hit.normal;
            }
            return Vector3.up;
        }

        public void SetTerrainCollider(MeshCollider col)
        {
            terrainCollider = col;
        }
    }
}
