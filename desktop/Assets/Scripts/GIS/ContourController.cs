using UnityEngine;

namespace Virexa.FMS
{
    public class ContourController : MonoBehaviour
    {
        public static ContourController Instance { get; private set; }

        [Header("Contour Settings")]
        [SerializeField] private bool _enableContours = false; // Default OFF for pure 4K GeoTIFF
        [SerializeField] private float _contourInterval = 25.0f; // 25m interval
        [SerializeField] private float _contourOpacity = 0.85f;
        [SerializeField] private Color _majorContourColor = new Color(1.0f, 0.78f, 0.15f, 0.95f);

        [Header("TIN Surface")]
        [SerializeField] private float _tinMode = 0f; // 0=Off, 1=Wireframe, 2=Solid CAD

        private Material _targetMaterial;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            FindTerrainMaterial();
            ApplySettings();
        }

        private void FindTerrainMaterial()
        {
            Terrain t = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            if (t != null && t.materialTemplate != null)
            {
                _targetMaterial = t.materialTemplate;
            }
        }

        public void ToggleContours()
        {
            FindTerrainMaterial();
            _enableContours = !_enableContours;
            ApplySettings();
        }

        public void ToggleTINMode()
        {
            FindTerrainMaterial();
            _tinMode = (_tinMode == 0f) ? 1f : ((_tinMode == 1f) ? 2f : 0f);
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (_targetMaterial == null) FindTerrainMaterial();
            if (_targetMaterial == null) return;

            if (_targetMaterial.HasProperty("_EnableContours"))
                _targetMaterial.SetFloat("_EnableContours", _enableContours ? 1.0f : 0.0f);

            if (_targetMaterial.HasProperty("_ContourInterval"))
                _targetMaterial.SetFloat("_ContourInterval", _contourInterval);

            if (_targetMaterial.HasProperty("_ContourOpacity"))
                _targetMaterial.SetFloat("_ContourOpacity", _contourOpacity);

            if (_targetMaterial.HasProperty("_MajorContourColor"))
                _targetMaterial.SetColor("_MajorContourColor", _majorContourColor);

            if (_targetMaterial.HasProperty("_TINMode"))
                _targetMaterial.SetFloat("_TINMode", _tinMode);
        }
    }
}
