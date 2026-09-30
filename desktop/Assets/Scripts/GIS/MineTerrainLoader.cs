using System;
using System.IO;
using UnityEngine;

namespace Virexa.FMS
{
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class MineTerrainLoader : MonoBehaviour
    {
        public static MineTerrainLoader Instance { get; private set; }

        [Header("Terrain Dimensions")]
        public float terrainWidthM = 5537.12f;
        public float terrainHeightM = 4545.46f;
        public int gridCols = 280;
        public int gridRows = 230;
        public float verticalExaggeration = 1.0f;

        [Header("Assets")]
        public Texture2D geotiffTexture;
        public TextAsset elevationBytesAsset;

        [Header("Components")]
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        public Material terrainMaterial;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildTerrain();
        }

        private void Start()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                BuildTerrain();
            }
        }

        public void BuildTerrain()
        {
            EnsureComponents();
            LoadAssetsIfNull();

            // 1. Read Elevation Data
            float[,] elevations = ReadElevationData();

            // 2. Generate 3D Mesh
            Mesh terrainMesh = GenerateMesh(elevations);
            meshFilter.sharedMesh = terrainMesh;
            meshCollider.sharedMesh = terrainMesh;

            // 3. Setup Material & Texture
            SetupMaterial();

            Debug.Log($"[MineTerrainLoader] Terrain generated: {terrainMesh.vertexCount} vertices, {terrainMesh.triangles.Length / 3} triangles, Texture: {(geotiffTexture != null ? geotiffTexture.name : "null")}");
        }

        private void EnsureComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            meshRenderer = GetComponent<Renderer>() as MeshRenderer;
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        private void LoadAssetsIfNull()
        {
            if (geotiffTexture == null)
            {
#if UNITY_EDITOR
                geotiffTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PitUnggul_TIF_Texture.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PitUnggul_RealOrtho.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/geotiff_ortho_4k.jpg");
#endif
                if (geotiffTexture == null)
                {
                    geotiffTexture = Resources.Load<Texture2D>("Textures/geotiff_ortho_4k");
                }
            }

            if (elevationBytesAsset == null)
            {
#if UNITY_EDITOR
                elevationBytesAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/terrain_elevation.bytes");
#endif
                if (elevationBytesAsset == null)
                {
                    elevationBytesAsset = Resources.Load<TextAsset>("Data/terrain_elevation");
                }
            }
        }

        private float[,] ReadElevationData()
        {
            float[,] elev = new float[gridRows, gridCols];

            byte[] rawBytes = null;
            if (elevationBytesAsset != null)
            {
                rawBytes = elevationBytesAsset.bytes;
            }
            else
            {
                string path = Path.Combine(Application.dataPath, "Data", "terrain_elevation.bytes");
                if (File.Exists(path)) rawBytes = File.ReadAllBytes(path);
            }

            if (rawBytes != null && rawBytes.Length >= gridRows * gridCols * 4)
            {
                int offset = 0;
                for (int r = 0; r < gridRows; r++)
                {
                    for (int c = 0; c < gridCols; c++)
                    {
                        elev[r, c] = BitConverter.ToSingle(rawBytes, offset);
                        offset += 4;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[MineTerrainLoader] Using default baseline elevation 150m.");
                for (int r = 0; r < gridRows; r++)
                {
                    for (int c = 0; c < gridCols; c++)
                    {
                        elev[r, c] = 150f;
                    }
                }
            }

            return elev;
        }

        private Mesh GenerateMesh(float[,] elevations)
        {
            Mesh mesh = new Mesh();
            mesh.name = "MineTerrainMesh_Runtime";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int totalVertices = gridRows * gridCols;
            Vector3[] vertices = new Vector3[totalVertices];
            Vector2[] uvs = new Vector2[totalVertices];

            float minX = (float)(570144.2313292557 - GeoCoordinateConverter.ORIGIN_UTM_X); // ~ -2584.07m
            float minZ = (float)(110653.65266003287 - GeoCoordinateConverter.ORIGIN_UTM_Y); // ~ -2684.65m

            float stepX = terrainWidthM / (gridCols - 1);
            float stepZ = terrainHeightM / (gridRows - 1);

            int vIdx = 0;
            for (int r = 0; r < gridRows; r++)
            {
                // In Unity, Z=minZ is South, Z=maxZ is North
                // In LiDAR array, row 0 is North (top), row gridRows-1 is South (bottom)
                float z = minZ + (gridRows - 1 - r) * stepZ;
                float v = (float)(gridRows - 1 - r) / (gridRows - 1);

                for (int c = 0; c < gridCols; c++)
                {
                    float x = minX + c * stepX;
                    float u = (float)c / (gridCols - 1);
                    float y = elevations[r, c] * verticalExaggeration;

                    vertices[vIdx] = new Vector3(x, y, z);
                    uvs[vIdx] = new Vector2(u, v);
                    vIdx++;
                }
            }

            // Generate Triangles (Clockwise winding for upward facing normals)
            int numQuads = (gridRows - 1) * (gridCols - 1);
            int[] triangles = new int[numQuads * 6];
            int tIdx = 0;

            for (int r = 0; r < gridRows - 1; r++)
            {
                for (int c = 0; c < gridCols - 1; c++)
                {
                    int tl = r * gridCols + c;
                    int tr = r * gridCols + (c + 1);
                    int bl = (r + 1) * gridCols + c;
                    int br = (r + 1) * gridCols + (c + 1);

                    // Quad Triangle 1: tl -> tr -> br (Clockwise from top)
                    triangles[tIdx++] = tl;
                    triangles[tIdx++] = tr;
                    triangles[tIdx++] = br;

                    // Quad Triangle 2: tl -> br -> bl (Clockwise from top)
                    triangles[tIdx++] = tl;
                    triangles[tIdx++] = br;
                    triangles[tIdx++] = bl;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void SetupMaterial()
        {
            if (terrainMaterial == null)
            {
#if UNITY_EDITOR
                terrainMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_RealGIS_Terrain.mat");
#endif
                if (terrainMaterial == null)
                {
                    Shader shader = Shader.Find("Virexa/TerrainContourShader")
                                 ?? Shader.Find("Virexa/GeoTIFF_Terrain_DoubleSided") 
                                 ?? Shader.Find("Standard");

                    terrainMaterial = new Material(shader);
                    terrainMaterial.name = "Mat_RealGIS_Terrain";
                }
            }

            if (geotiffTexture != null)
            {
                terrainMaterial.mainTexture = geotiffTexture;
            }

            meshRenderer.sharedMaterial = terrainMaterial;
        }
    }
}
