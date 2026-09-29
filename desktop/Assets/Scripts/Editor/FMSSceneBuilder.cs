#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Virexa.FMS.Editor
{
    [InitializeOnLoad]
    public static class FMSSceneBuilder
    {
        static FMSSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                // Auto build on initial load if scene doesn't have terrain
                Terrain t = UnityEngine.Object.FindFirstObjectByType<Terrain>();
                if (t == null && SceneManager.GetActiveScene().name == "FMS_Mine_Main")
                {
                    BuildScene();
                }
            };
        }


        [MenuItem("FMS/Build Complete 3D Mine Scene (Tampilkan Peta GeoTIFF)", false, 1)]
        public static void BuildScene()
        {
            Debug.Log("[FMSSceneBuilder] Building 100% Real GeoTIFF 3D Terrain Scene...");

            // 1. Ensure Data Directories exist
            if (!Directory.Exists("Assets/Data")) Directory.CreateDirectory("Assets/Data");
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            // 2. Create or Update TerrainData Asset
            string tdataPath = "Assets/Data/RealMine_TerrainData.asset";
            TerrainData tData = AssetDatabase.LoadAssetAtPath<TerrainData>(tdataPath);
            if (tData == null)
            {
                tData = new TerrainData();
                AssetDatabase.CreateAsset(tData, tdataPath);
            }

            int res = 513;
            float width = 5451.56f;
            float length = 4086.95f;
            float heightRange = 245.44f; // 71.02m to 316.46m
            float minElev = 71.02f;

            tData.heightmapResolution = res;
            tData.size = new Vector3(width, heightRange, length);

            // Read 16-bit Raw LiDAR Heightmap
            string rawPath = "Assets/Textures/RealMine_Heightmap.raw";
            if (File.Exists(rawPath))
            {
                byte[] rawBytes = File.ReadAllBytes(rawPath);
                float[,] heights = new float[res, res];
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
                tData.SetHeights(0, 0, heights);
                Debug.Log("[FMSSceneBuilder] Real 16-bit LiDAR heights applied to TerrainData.");
            }

            // Assign TerrainLayer
            TerrainLayer tLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Materials/Layer_PitUnggul_TIF.terrainlayer");
            if (tLayer != null)
            {
                tData.terrainLayers = new TerrainLayer[] { tLayer };
            }

            EditorUtility.SetDirty(tData);

            // 3. Setup Scene GameObjects
            // Remove any old incomplete terrain objects
            GameObject oldTerrain = GameObject.Find("3D_Mine_Terrain");
            if (oldTerrain != null) UnityEngine.Object.DestroyImmediate(oldTerrain);

            GameObject oldReal = GameObject.Find("RealMining_Terrain_GIS");
            if (oldReal != null) UnityEngine.Object.DestroyImmediate(oldReal);

            // Create Native Unity Terrain
            GameObject terrainObj = Terrain.CreateTerrainGameObject(tData);
            terrainObj.name = "RealMining_Terrain_GIS";
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            terrain.heightmapPixelError = 1;
            terrain.basemapDistance = 25000;
            terrain.drawTreesAndFoliage = false;

            // Center Terrain at (0, 0)
            float halfW = width * 0.5f;
            float halfL = length * 0.5f;
            terrainObj.transform.position = new Vector3(-halfW, minElev, -halfL);

            // Assign Material
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_RealGIS_Terrain.mat");
            if (mat != null)
            {
                terrain.materialTemplate = mat;
            }

            // 4. Setup Lighting
            GameObject lightObj = GameObject.Find("Directional Light");
            if (lightObj == null)
            {
                lightObj = new GameObject("Directional Light");
                Light l = lightObj.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.3f;
                l.color = new Color(1f, 0.98f, 0.92f);
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 5. Setup Camera
            GameObject camObj = GameObject.Find("Main Camera");
            if (camObj == null)
            {
                camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            Camera cam = camObj.GetComponent<Camera>();
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 35000f;
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.Skybox;

            FMSCameraController camCtrl = camObj.GetComponent<FMSCameraController>();
            if (camCtrl == null) camCtrl = camObj.AddComponent<FMSCameraController>();
            camCtrl.pivotPoint = new Vector3(0f, 150f, 0f);

            camObj.transform.position = new Vector3(0f, 1800f, -2200f);
            camObj.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            // 6. Setup UI
            GameObject mgrObj = GameObject.Find("--- FMS_SYSTEM_MANAGERS ---");
            if (mgrObj == null)
            {
                mgrObj = new GameObject("--- FMS_SYSTEM_MANAGERS ---");
            }
            if (mgrObj.GetComponent<FMSDashboardUI>() == null)
            {
                mgrObj.AddComponent<FMSDashboardUI>();
            }

            // 7. Save Scene
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FMSSceneBuilder] SUCCESS! 3D GeoTIFF Terrain has been built and displayed in the scene!");
        }
    }
}
#endif
