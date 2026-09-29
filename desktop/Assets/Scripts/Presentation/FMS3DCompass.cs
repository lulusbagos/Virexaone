using System;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMS3DCompass : MonoBehaviour
    {
        public static FMS3DCompass Instance { get; private set; }

        [Header("Render Texture Settings")]
        public int renderSize = 256;
        public RenderTexture CompassRenderTexture { get; private set; }

        private Camera compassCamera;
        private GameObject compassPivot;
        private GameObject compassRoot;
        private Light compassLight;

        private Material northMat;
        private Material southMat;
        private Material eastWestMat;
        private Material ringMat;
        private Material centerPinMat;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            Setup3DCompassSystem();
        }

        private void Setup3DCompassSystem()
        {
            // 1. Create Render Texture
            CompassRenderTexture = new RenderTexture(renderSize, renderSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_3D_Compass",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            CompassRenderTexture.Create();

            // 2. Create Isolated Root in far-off coordinates
            compassRoot = new GameObject("3D_Compass_Rig");
            compassRoot.transform.position = new Vector3(9999f, 9999f, 9999f);

            // 3. Create Compass Camera
            GameObject camObj = new GameObject("Compass_Camera");
            camObj.transform.SetParent(compassRoot.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 0f, -4.5f);
            camObj.transform.localRotation = Quaternion.identity;

            compassCamera = camObj.AddComponent<Camera>();
            compassCamera.clearFlags = CameraClearFlags.SolidColor;
            compassCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            compassCamera.cullingMask = ~0; // Render all in isolated rig
            compassCamera.fieldOfView = 36f;
            compassCamera.nearClipPlane = 0.1f;
            compassCamera.farClipPlane = 20f;
            compassCamera.targetTexture = CompassRenderTexture;
            compassCamera.depth = -100;
            compassCamera.allowHDR = false;
            compassCamera.allowMSAA = true;

            // 4. Create Studio Key Light (Point Light local to isolated rig at 9999, 9999, 9999)
            GameObject lightObj = new GameObject("Compass_KeyLight");
            lightObj.transform.SetParent(compassRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(2.5f, 3.5f, -3.5f);

            compassLight = lightObj.AddComponent<Light>();
            compassLight.type = LightType.Point;
            compassLight.range = 15f;
            compassLight.color = new Color(1.0f, 0.98f, 0.95f);
            compassLight.intensity = 2.2f;

            // Secondary Rim Light (Point Light local to isolated rig)
            GameObject rimLightObj = new GameObject("Compass_RimLight");
            rimLightObj.transform.SetParent(compassRoot.transform, false);
            rimLightObj.transform.localPosition = new Vector3(-2.5f, -2f, -2.5f);
            Light rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 15f;
            rimLight.color = new Color(0.0f, 0.85f, 1.0f);
            rimLight.intensity = 1.6f;

            // 5. Create Compass Materials
            InitMaterials();

            // 6. Build 3D Compass Geometry
            BuildCompassModel();
        }

        private void InitMaterials()
        {
            Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Sprites/Default");

            // North Arrow Material (Vibrant Red Metallic)
            northMat = new Material(standardShader)
            {
                color = new Color(0.95f, 0.18f, 0.18f, 1f)
            };
            if (northMat.HasProperty("_Metallic")) northMat.SetFloat("_Metallic", 0.65f);
            if (northMat.HasProperty("_Glossiness")) northMat.SetFloat("_Glossiness", 0.85f);
            if (northMat.HasProperty("_EmissionColor"))
            {
                northMat.EnableKeyword("_EMISSION");
                northMat.SetColor("_EmissionColor", new Color(0.35f, 0.05f, 0.05f));
            }

            // South Arrow Material (Silver / Platinum Metallic)
            southMat = new Material(standardShader)
            {
                color = new Color(0.85f, 0.90f, 0.95f, 1f)
            };
            if (southMat.HasProperty("_Metallic")) southMat.SetFloat("_Metallic", 0.75f);
            if (southMat.HasProperty("_Glossiness")) southMat.SetFloat("_Glossiness", 0.80f);

            // East / West Arrow Material (Cyber Cyan Metallic)
            eastWestMat = new Material(standardShader)
            {
                color = new Color(0.15f, 0.75f, 0.95f, 1f)
            };
            if (eastWestMat.HasProperty("_Metallic")) eastWestMat.SetFloat("_Metallic", 0.70f);
            if (eastWestMat.HasProperty("_Glossiness")) eastWestMat.SetFloat("_Glossiness", 0.85f);

            // Ring Material (Dark Obsidian with Cyan Edge)
            ringMat = new Material(standardShader)
            {
                color = new Color(0.12f, 0.16f, 0.22f, 1f)
            };
            if (ringMat.HasProperty("_Metallic")) ringMat.SetFloat("_Metallic", 0.85f);
            if (ringMat.HasProperty("_Glossiness")) ringMat.SetFloat("_Glossiness", 0.90f);

            // Center Pin Material (Bright Gold / Amber)
            centerPinMat = new Material(standardShader)
            {
                color = new Color(1.0f, 0.75f, 0.20f, 1f)
            };
            if (centerPinMat.HasProperty("_Metallic")) centerPinMat.SetFloat("_Metallic", 0.90f);
            if (centerPinMat.HasProperty("_Glossiness")) centerPinMat.SetFloat("_Glossiness", 0.95f);
        }

        private void BuildCompassModel()
        {
            compassPivot = new GameObject("Compass_Pivot");
            compassPivot.transform.SetParent(compassRoot.transform, false);
            compassPivot.transform.localPosition = Vector3.zero;

            // 1. North Arrow (3D Faceted Pyramidal Wedge pointing +Z)
            Create3DWedge(compassPivot.transform, "North_Arrow_3D", Vector3.forward, 1.45f, 0.38f, 0.18f, northMat);

            // 2. South Arrow (3D Faceted Pyramidal Wedge pointing -Z)
            Create3DWedge(compassPivot.transform, "South_Arrow_3D", Vector3.back, 1.25f, 0.32f, 0.15f, southMat);

            // 3. East Arrow (3D Faceted Pyramidal Wedge pointing +X)
            Create3DWedge(compassPivot.transform, "East_Arrow_3D", Vector3.right, 0.95f, 0.26f, 0.12f, eastWestMat);

            // 4. West Arrow (3D Faceted Pyramidal Wedge pointing -X)
            Create3DWedge(compassPivot.transform, "West_Arrow_3D", Vector3.left, 0.95f, 0.26f, 0.12f, eastWestMat);

            // 5. 3D Outer Gimbal Ring
            Create3DRing(compassPivot.transform, "Gimbal_Ring_3D", 1.55f, 0.05f, 36, ringMat);

            // 6. Center Pin (3D Sphere / Bevel)
            GameObject pinObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pinObj.name = "Center_Pin_3D";
            pinObj.transform.SetParent(compassPivot.transform, false);
            pinObj.transform.localPosition = Vector3.zero;
            pinObj.transform.localScale = new Vector3(0.32f, 0.24f, 0.32f);
            if (pinObj.TryGetComponent<Collider>(out var col)) Destroy(col);
            pinObj.GetComponent<MeshRenderer>().material = centerPinMat;
        }

        private void Create3DWedge(Transform parent, string name, Vector3 direction, float length, float width, float height, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.material = mat;

            Mesh mesh = new Mesh { name = name + "_Mesh" };

            // Rotate geometry so local +Z matches the direction
            Quaternion rot = Quaternion.FromToRotation(Vector3.forward, direction.normalized);

            Vector3 tip = rot * new Vector3(0f, 0f, length);
            Vector3 left = rot * new Vector3(-width * 0.5f, 0f, 0.1f * length);
            Vector3 right = rot * new Vector3(width * 0.5f, 0f, 0.1f * length);
            Vector3 top = rot * new Vector3(0f, height, 0.15f * length);
            Vector3 bottom = rot * new Vector3(0f, -height, 0.15f * length);
            Vector3 baseBack = rot * Vector3.zero;

            // 8 Facets for high-detail 3D gem / arrow look
            Vector3[] vertices = new Vector3[]
            {
                // Top-Left facet
                tip, left, top,
                // Top-Right facet
                tip, top, right,
                // Bottom-Left facet
                tip, bottom, left,
                // Bottom-Right facet
                tip, right, bottom,
                // Back-Top-Left facet
                baseBack, top, left,
                // Back-Top-Right facet
                baseBack, right, top,
                // Back-Bottom-Left facet
                baseBack, left, bottom,
                // Back-Bottom-Right facet
                baseBack, bottom, right
            };

            int[] triangles = new int[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8,
                9, 10, 11,
                12, 13, 14,
                15, 16, 17,
                18, 19, 20,
                21, 22, 23
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;
        }

        private void Create3DRing(Transform parent, string name, float radius, float thickness, int segments, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.material = mat;

            Mesh mesh = new Mesh { name = name + "_Mesh" };

            int vertCount = segments * 4;
            Vector3[] verts = new Vector3[vertCount];
            int[] tris = new int[segments * 24];

            float rOuter = radius;
            float rInner = radius - thickness * 2f;
            float h = thickness;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts[i * 4 + 0] = new Vector3(cos * rOuter, h, sin * rOuter);
                verts[i * 4 + 1] = new Vector3(cos * rInner, h, sin * rInner);
                verts[i * 4 + 2] = new Vector3(cos * rOuter, -h, sin * rOuter);
                verts[i * 4 + 3] = new Vector3(cos * rInner, -h, sin * rInner);
            }

            int triIdx = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                int o0 = i * 4 + 0;
                int i0 = i * 4 + 1;
                int o0_b = i * 4 + 2;
                int i0_b = i * 4 + 3;

                int o1 = next * 4 + 0;
                int i1 = next * 4 + 1;
                int o1_b = next * 4 + 2;
                int i1_b = next * 4 + 3;

                // Top Face
                tris[triIdx++] = o0; tris[triIdx++] = o1; tris[triIdx++] = i0;
                tris[triIdx++] = i0; tris[triIdx++] = o1; tris[triIdx++] = i1;

                // Outer Face
                tris[triIdx++] = o0; tris[triIdx++] = o0_b; tris[triIdx++] = o1;
                tris[triIdx++] = o1; tris[triIdx++] = o0_b; tris[triIdx++] = o1_b;

                // Inner Face
                tris[triIdx++] = i0; tris[triIdx++] = i1; tris[triIdx++] = i0_b;
                tris[triIdx++] = i1; tris[triIdx++] = i1_b; tris[triIdx++] = i0_b;

                // Bottom Face
                tris[triIdx++] = o0_b; tris[triIdx++] = i0_b; tris[triIdx++] = o1_b;
                tris[triIdx++] = i0_b; tris[triIdx++] = i1_b; tris[triIdx++] = o1_b;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;
        }

        private void LateUpdate()
        {
            if (compassPivot == null) return;

            // Synchronize 3D Compass rotation with the active camera's 3D orientation (Pitch & Yaw)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Invert camera rotation so looking from camera perspective gives True 3D North orientation
                Quaternion camRot = mainCam.transform.rotation;
                compassPivot.transform.rotation = Quaternion.Inverse(camRot);
            }
        }

        private void OnDestroy()
        {
            if (CompassRenderTexture != null)
            {
                CompassRenderTexture.Release();
                Destroy(CompassRenderTexture);
            }
            if (compassRoot != null)
            {
                Destroy(compassRoot);
            }
        }
    }
}
