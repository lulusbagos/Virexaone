using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    /// <summary>
    /// Loads and instantiates authentic 3D GLB unit models at runtime using glTFast.
    /// Normalizes scale to real-world mining machine dimensions and aligns ground contact.
    /// </summary>
    public class FMSGlbModelLoader : MonoBehaviour
    {
        [Header("Model Settings")]
        public string glbFileName = "truck_empty_draco.glb";
        public float targetLengthMeters = 10.8f;
        public float targetWidthMeters = 5.8f;
        public float targetHeightMeters = 5.3f;
        public GameObject fallbackVisual;
        private static readonly Dictionary<string, Task<byte[]>> ModelBytesCache = new Dictionary<string, Task<byte[]>>();
        private GameObject loadedVisual;
        private int loadRevision;

        private static Task<byte[]> GetModelBytesAsync(string url)
        {
            lock (ModelBytesCache)
            {
                if (!ModelBytesCache.TryGetValue(url, out var pending))
                {
                    pending = DownloadModelBytesAsync(url);
                    ModelBytesCache[url] = pending;
                }
                return pending;
            }
        }

        private static async Task<byte[]> DownloadModelBytesAsync(string url)
        {
            try
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 30;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new Exception(request.error);
                    return request.downloadHandler.data;
                }
            }
            catch
            {
                lock (ModelBytesCache) ModelBytesCache.Remove(url);
                throw;
            }
        }

        private void Start()
        {
            _ = LoadModelAsync(++loadRevision);
        }

        public void SetModelVariant(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || glbFileName == fileName) return;
            glbFileName = fileName;
            if (isActiveAndEnabled) _ = LoadModelAsync(++loadRevision);
        }

        public void SetReferenceLength(float lengthMeters)
        {
            if (float.IsNaN(lengthMeters) || float.IsInfinity(lengthMeters) ||
                lengthMeters < 2f || lengthMeters > 40f || targetLengthMeters <= 0f ||
                Mathf.Abs(lengthMeters - targetLengthMeters) < 0.1f) return;
            float ratio = lengthMeters / targetLengthMeters;
            targetLengthMeters = lengthMeters;
            if (fallbackVisual != null) fallbackVisual.transform.localScale *= ratio;
            if (loadedVisual != null)
            {
                loadedVisual.transform.localScale *= ratio;
                loadedVisual.transform.localPosition *= ratio;
            }
            var collider = GetComponentInParent<BoxCollider>();
            if (collider != null)
            {
                collider.center *= ratio;
                collider.size *= ratio;
            }
        }

        private async Task LoadModelAsync(int revision)
        {
            try
            {
                string requestedFile = glbFileName;
                string assetPath = Application.streamingAssetsPath.TrimEnd('/', '\\') +
                    "/UnitModels/" + Uri.EscapeDataString(requestedFile);
                string url = assetPath.Contains("://") ? assetPath : new Uri(Path.GetFullPath(assetPath)).AbsoluteUri;
                byte[] bytes = await GetModelBytesAsync(url);
                if (this == null || revision != loadRevision) return;
                var deferAgent = new GLTFast.UninterruptedDeferAgent();
                var gltf = new GLTFast.GltfImport(null, deferAgent);
                
                bool success = await gltf.Load(bytes, new Uri(url));
                if (this == null || revision != loadRevision) return;
                if (success)
                {
                    GameObject glbRoot = new GameObject("GLB_" + Path.GetFileNameWithoutExtension(requestedFile));
                    glbRoot.transform.SetParent(transform, false);
                    
                    bool instantiated = await gltf.InstantiateMainSceneAsync(glbRoot.transform);
                    if (this == null || revision != loadRevision)
                    {
                        Destroy(glbRoot);
                        return;
                    }
                    if (instantiated)
                    {
                        Renderer[] renderers = glbRoot.GetComponentsInChildren<Renderer>();
                        if (renderers != null && renderers.Length > 0)
                        {
                            Bounds b = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++)
                            {
                                b.Encapsulate(renderers[i].bounds);
                            }

                            float rawLength = Mathf.Max(b.size.x, b.size.z);
                            float scaleFactor = rawLength > 0.001f ? targetLengthMeters / rawLength : 1f;

                            glbRoot.transform.localScale = Vector3.one * scaleFactor;
                            
                            Vector3 centerOffset = b.center - glbRoot.transform.position;
                            float groundY = b.min.y - glbRoot.transform.position.y;
                            glbRoot.transform.localPosition = new Vector3(-centerOffset.x * scaleFactor, -groundY * scaleFactor, -centerOffset.z * scaleFactor);

                            var unitCollider = GetComponentInParent<BoxCollider>();
                            if (unitCollider != null)
                            {
                                unitCollider.center = new Vector3(0f, b.size.y * scaleFactor * 0.5f, 0f);
                                unitCollider.size = b.size * scaleFactor;
                            }

                            // Update FMSUnitController references if present
                            var ctrl = GetComponentInParent<FMSUnitController>();
                            if (ctrl != null)
                            {
                                var foundWheels = new System.Collections.Generic.List<Transform>();
                                foreach (var t in glbRoot.GetComponentsInChildren<Transform>())
                                {
                                    string tName = t.name.ToLower();
                                    if (tName.Contains("wheel") || tName.Contains("tire") || tName.Contains("tyre") || tName.Contains("roda"))
                                    {
                                        foundWheels.Add(t);
                                    }
                                    else if (tName.Contains("cab") || tName.Contains("house") || tName.Contains("upper") || tName.Contains("revolve") || tName.Contains("turret"))
                                    {
                                        ctrl.cabTransform = t;
                                    }
                                    else if (tName.Contains("boom") || tName.Contains("mainarm"))
                                    {
                                        ctrl.boomTransform = t;
                                    }
                                    else if (tName.Contains("stick") || tName.Contains("dipper") || tName.Contains("forearm"))
                                    {
                                        ctrl.stickTransform = t;
                                    }
                                    else if (tName.Contains("bucket") || tName.Contains("shovel") || tName.Contains("scoop"))
                                    {
                                        ctrl.bucketTransform = t;
                                    }
                                    else if (tName.Contains("ore") || tName.Contains("coal") || tName.Contains("payload"))
                                    {
                                        ctrl.bucketOreTransform = t;
                                    }
                                    else if (tName.Contains("bed") || tName.Contains("dump") || tName.Contains("bak"))
                                    {
                                        ctrl.dumpBedTransform = t;
                                    }
                                }
                                if (foundWheels.Count > 0)
                                {
                                    ctrl.wheels = foundWheels.ToArray();
                                }
                            }

                            if (loadedVisual != null) Destroy(loadedVisual);
                            loadedVisual = glbRoot;
                            if (fallbackVisual != null) fallbackVisual.SetActive(false);

                            var lighting = GetComponentInParent<FMSUnitLighting>();
                            if (lighting != null)
                            {
                                lighting.BuildLightingRig();
                            }

                            Debug.Log($"[FMSGlbModelLoader] Loaded '{requestedFile}' (Scale: {scaleFactor:F3}x, Target: {targetLengthMeters}m)");
                        }
                        else Destroy(glbRoot);
                    }
                    else Destroy(glbRoot);
                }
                else
                {
                    Debug.LogWarning($"[FMSGlbModelLoader] glTFast failed to parse GLB data for: {requestedFile}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FMSGlbModelLoader] Error loading GLB '{glbFileName}': {ex.Message}");
            }
        }
    }
}
