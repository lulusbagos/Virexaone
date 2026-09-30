using System;
using System.IO;
using UnityEngine;

namespace Virexa.FMS
{
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class RealMiningGISLoader : MonoBehaviour
    {
        public static RealMiningGISLoader Instance { get; private set; }

        [Header("Real LiDAR Dimensions")]
        public float terrainWidth = 5451.56f;   // 5.45 km East-West
        public float terrainLength = 4086.95f;  // 4.09 km North-South
        public float minElevation = 71.02f;     // Datum 71.02m ASL
        public float maxElevation = 316.46f;    // 316.46m ASL

        [Header("Assets")]
        public Texture2D geotiffTexture;
        public Material terrainMaterial;
        public TerrainLayer terrainLayer;

        [Header("Terrain References")]
        public Terrain activeTerrain;
        public TerrainData terrainData;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildRealGISTerrain();
        }

        private void Start()
        {
            BuildRealGISTerrain();
        }

        [ContextMenu("Rebuild Real GIS Terrain")]
        public void BuildRealGISTerrain()
        {
            // 1. Load Assets
            LoadAssets();

            // 2. Load Raw LiDAR 16-bit Heightmap
            int res = 513;
            float totalHeightRange = maxElevation - minElevation; // ~245.44m

            if (terrainData == null)
            {
                terrainData = new TerrainData();
                terrainData.name = "RealMining_TerrainData";
            }

            terrainData.heightmapResolution = res;
            terrainData.size = new Vector3(terrainWidth, totalHeightRange, terrainLength);

            float[,] heights = LoadHeightmapArray(res);
            terrainData.SetHeights(0, 0, heights);

            // 3. Setup TerrainLayer
            SetupTerrainLayers();

            // 4. Create or Update Terrain Component
            if (activeTerrain == null)
            {
                Terrain[] existing = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                foreach (var t in existing)
                {
                    if (t.gameObject.name == "RealMining_Terrain_GIS" || t.gameObject.name == "Terrain")
                    {
                        activeTerrain = t;
                        break;
                    }
                }

                if (activeTerrain == null)
                {
                    GameObject tObj = Terrain.CreateTerrainGameObject(terrainData);
                    tObj.name = "RealMining_Terrain_GIS";
                    activeTerrain = tObj.GetComponent<Terrain>();
                }
            }

            activeTerrain.terrainData = terrainData;
            activeTerrain.heightmapPixelError = 1;
            activeTerrain.basemapDistance = 25000;
            activeTerrain.drawTreesAndFoliage = false;

            // Center the terrain around (0, 0)
            float halfW = terrainWidth * 0.5f;
            float halfL = terrainLength * 0.5f;
            activeTerrain.transform.position = new Vector3(-halfW, minElevation, -halfL);

            // 5. Setup Material with Contour & GeoTIFF shader
            SetupMaterial();

            Debug.Log($"[RealMiningGISLoader] Real GIS Terrain successfully built! Size: {terrainWidth:F1}m x {terrainLength:F1}m, Height: {minElevation:F1}m - {maxElevation:F1}m");
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            if (geotiffTexture == null)
            {
                geotiffTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PitUnggul_TIF_Texture.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PitUnggul_RealOrtho.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/geotiff_ortho_4k.jpg");
            }

            if (terrainMaterial == null)
            {
                terrainMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_RealGIS_Terrain.mat");
            }

            if (terrainLayer == null)
            {
                terrainLayer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Materials/Layer_PitUnggul_TIF.terrainlayer");
            }
#endif
            if (geotiffTexture == null)
            {
                geotiffTexture = Resources.Load<Texture2D>("Textures/PitUnggul_TIF_Texture")
                              ?? Resources.Load<Texture2D>("Textures/geotiff_ortho_4k");
            }

            if (terrainMaterial == null)
            {
                Shader shader = Shader.Find("Virexa/TerrainContourShader")
                             ?? Shader.Find("Virexa/GeoTIFF_Terrain_DoubleSided")
                             ?? Shader.Find("Standard");
                terrainMaterial = new Material(shader) { name = "Mat_RealGIS_Terrain_Runtime" };
            }
        }

        private float[,] LoadHeightmapArray(int res)
        {
            float[,] heights = new float[res, res];
            string fullRawPath = Path.Combine(Application.dataPath, "Textures/RealMine_Heightmap.raw");

            if (File.Exists(fullRawPath))
            {
                byte[] rawBytes = File.ReadAllBytes(fullRawPath);
                int byteIdx = 0;

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        if (byteIdx + 1 < rawBytes.Length)
                        {
                            ushort val = (ushort)(rawBytes[byteIdx] | (rawBytes[byteIdx + 1] << 8));
                            heights[y, x] = val / 65535.0f;
                            byteIdx += 2;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[RealMiningGISLoader] RAW heightmap not found at {fullRawPath}, initializing default flat terrain.");
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        heights[y, x] = 0.5f;
            }

            return heights;
        }

        private void SetupTerrainLayers()
        {
            if (terrainLayer != null)
            {
                terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };
            }
            else if (geotiffTexture != null)
            {
                TerrainLayer layer = new TerrainLayer();
                layer.name = "GeoTIFF_OrthoLayer";
                layer.diffuseTexture = geotiffTexture;
                layer.tileSize = new Vector2(terrainWidth, terrainLength);
                layer.tileOffset = Vector2.zero;
                terrainData.terrainLayers = new TerrainLayer[] { layer };
            }
        }

        private void SetupMaterial()
        {
            if (terrainMaterial != null)
            {
                if (geotiffTexture != null && terrainMaterial.HasProperty("_MainTex"))
                {
                    terrainMaterial.SetTexture("_MainTex", geotiffTexture);
                }
                if (terrainMaterial.HasProperty("_EnableContours"))
                {
                    terrainMaterial.SetFloat("_EnableContours", 1.0f);
                }
                if (terrainMaterial.HasProperty("_ContourInterval"))
                {
                    terrainMaterial.SetFloat("_ContourInterval", 10.0f);
                }
                if (terrainMaterial.HasProperty("_ContourOpacity"))
                {
                    terrainMaterial.SetFloat("_ContourOpacity", 0.85f);
                }
                terrainMaterial.SetFloat("_TextureBrightness", 1.0f);
                terrainMaterial.SetFloat("_TextureContrast", 0.0f);
                activeTerrain.materialTemplate = terrainMaterial;
            }
        }

        public bool ApplyGeoTIFFTexture(Texture2D newTexture)
        {
            if (newTexture == null) return false;
            geotiffTexture = newTexture;

            if (terrainData != null)
            {
                TerrainLayer[] layers = terrainData.terrainLayers;
                if (layers != null && layers.Length > 0 && layers[0] != null)
                {
                    layers[0].diffuseTexture = newTexture;
                    layers[0].tileSize = new Vector2(terrainWidth, terrainLength);
                    terrainData.terrainLayers = layers;
                }
                else
                {
                    TerrainLayer layer = new TerrainLayer
                    {
                        name = "GeoTIFF_OrthoLayer",
                        diffuseTexture = newTexture,
                        tileSize = new Vector2(terrainWidth, terrainLength),
                        tileOffset = Vector2.zero
                    };
                    terrainData.terrainLayers = new TerrainLayer[] { layer };
                }
            }

            if (terrainMaterial != null && terrainMaterial.HasProperty("_MainTex"))
            {
                terrainMaterial.SetTexture("_MainTex", newTexture);
            }

            if (activeTerrain != null)
            {
                activeTerrain.Flush();
            }

            return true;
        }

        public bool LoadAndApplyGeoTIFFFromFile(string filePath, out string message)
        {
            if (!File.Exists(filePath))
            {
                message = "File tidak ditemukan!";
                return false;
            }

            try
            {
                string destFolder = Path.Combine(Application.dataPath, "GeoTIFF_Source");
                if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(destFolder, fileName);

                if (!filePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(filePath, destPath, true);
                }

                byte[] fileBytes = File.ReadAllBytes(filePath);
                Texture2D newTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool loaded = newTex.LoadImage(fileBytes);

                if (loaded)
                {
                    newTex.name = Path.GetFileNameWithoutExtension(filePath);
                    newTex.wrapMode = TextureWrapMode.Clamp;
                    newTex.filterMode = FilterMode.Bilinear;
                    newTex.Apply();

                    ApplyGeoTIFFTexture(newTex);
                    message = $"Berhasil menerapkan citra: {fileName} ({newTex.width}x{newTex.height} px)";
                    return true;
                }
                else
                {
                    message = $"File {fileName} disalin ke Assets/GeoTIFF_Source/.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = $"Error saat membaca file: {ex.Message}";
                return false;
            }
        }

        public void OpenGeoTIFFFolderInExplorer()
        {
            string folderPath = Path.Combine(Application.dataPath, "GeoTIFF_Source");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            System.Diagnostics.Process.Start("explorer.exe", folderPath.Replace("/", "\\"));
        }
    }
}
