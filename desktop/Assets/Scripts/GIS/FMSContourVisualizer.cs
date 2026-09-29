using UnityEngine;

namespace Virexa.FMS
{
    public enum MapDisplayMode
    {
        OrthophotoOnly,
        OrthophotoWithContours,
        WireframeMode
    }

    public class FMSContourVisualizer : MonoBehaviour
    {
        public static FMSContourVisualizer Instance { get; private set; }

        [Header("Terrain Textures")]
        [SerializeField] private Texture2D orthoTexture;
        [SerializeField] private Texture2D contourTexture;
        [SerializeField] private Renderer terrainRenderer;

        public MapDisplayMode currentMode = MapDisplayMode.OrthophotoOnly;
        private Material terrainMat;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitMaterial();
        }

        private void InitMaterial()
        {
            if (terrainRenderer == null)
            {
                terrainRenderer = GetComponent<Renderer>();
            }

            if (terrainRenderer != null)
            {
                terrainMat = terrainRenderer.material;
                if (orthoTexture != null)
                {
                    terrainMat.mainTexture = orthoTexture;
                }
            }
        }

        public void SetDisplayMode(MapDisplayMode mode)
        {
            currentMode = mode;
            if (terrainMat == null && terrainRenderer != null)
            {
                terrainMat = terrainRenderer.material;
            }

            if (terrainMat == null) return;

            switch (mode)
            {
                case MapDisplayMode.OrthophotoOnly:
                    if (orthoTexture != null) terrainMat.mainTexture = orthoTexture;
                    terrainMat.color = Color.white;
                    break;

                case MapDisplayMode.OrthophotoWithContours:
                    // In runtime, we can blend or set combined texture
                    if (orthoTexture != null) terrainMat.mainTexture = orthoTexture;
                    terrainMat.color = Color.white;
                    break;

                case MapDisplayMode.WireframeMode:
                    if (contourTexture != null) terrainMat.mainTexture = contourTexture;
                    terrainMat.color = new Color(0.3f, 0.8f, 0.4f); // Topographic green tint
                    break;
            }
        }

        public void ToggleContours()
        {
            if (currentMode == MapDisplayMode.OrthophotoWithContours)
            {
                SetDisplayMode(MapDisplayMode.OrthophotoOnly);
            }
            else if (currentMode == MapDisplayMode.OrthophotoOnly)
            {
                SetDisplayMode(MapDisplayMode.WireframeMode);
            }
            else
            {
                SetDisplayMode(MapDisplayMode.OrthophotoWithContours);
            }
        }
    }
}
