using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace Virexa.FMS
{
    public enum CctvCameraChannel
    {
        CabinDriver = 0,     // Cabin internal CCTV camera (Driver & In-Cab AI / Savera)
        FrontDashcam = 1,    // Front Onboard Dashcam looking at haul road
        RearBackup = 2,      // Rear reverse camera looking at tail & dump
        ElevatedMast = 3     // Elevated 360 mast pole camera above unit
    }

    public enum CctvStreamSource
    {
        ApiVideoStream,      // Live Video Stream from API (HLS / MP4 / WebRTC / HTTP)
        ApiSnapshotStream,   // Live JPEG Snapshot sequence from REST API
        DigitalTwin3D        // 3D Real-time Digital Twin Camera Feed (Fallback)
    }

    public class FMSUnitCctvManager : MonoBehaviour
    {
        public static FMSUnitCctvManager Instance { get; private set; }

        [Header("CCTV State")]
        public bool isCctvOpen = false;
        public FMSUnitController targetUnit;
        public CctvCameraChannel currentChannel = CctvCameraChannel.CabinDriver;
        public CctvStreamSource currentStreamSource = CctvStreamSource.ApiVideoStream;
        public bool isNightVision = false;
        public bool showUrlConfigPanel = false;

        [Header("CCTV API & Stream Configuration")]
        public string cctvApiBaseUrl = "http://127.0.0.1:8000/api/cctv";
        private string currentSiteId = "astha";
        public string customStreamUrl = "";
        public string activeStreamUrl = "";
        public string streamStatusMessage = "Menghubungkan ke API CCTV...";
        public bool isStreamConnected = false;
        public float streamFps = 30f;
        public float streamBitrateMbps = 4.5f;

        [Header("Render Texture")]
        public int textureWidth = 720;
        public int textureHeight = 405;
        public RenderTexture CctvRenderTexture { get; private set; }
        public Texture2D snapshotTexture;

        private Camera cctvCamera;
        private GameObject cctvRig;
        private Light cctvIrLight;
        private VideoPlayer videoPlayer;
        private Coroutine snapshotPollCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            string initialSite = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.siteId : "astha";
            string initialBackend = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.apiBaseUrl : "";
            SwitchSite(initialSite, initialBackend);

            SetupCctvSystem();
        }

        private void SetupCctvSystem()
        {
            // 1. Create Render Texture (16:9 widescreen HD)
            CctvRenderTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_Unit_CCTV_Feed",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            CctvRenderTexture.Create();

            // 2. Setup VideoPlayer for live API streaming (MP4 / HLS / HTTP)
            GameObject vpObj = new GameObject("CCTV_VideoPlayer");
            vpObj.transform.SetParent(transform, false);
            videoPlayer = vpObj.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = CctvRenderTexture;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // Mute audio for mining telemetry
            videoPlayer.errorReceived += OnVideoPlayerError;
            videoPlayer.prepareCompleted += OnVideoPlayerPrepared;

            // 3. Setup 3D In-Cab / Onboard Camera (Digital Twin fallback)
            cctvRig = new GameObject("CCTV_Unit_Tracker_Rig");
            cctvRig.transform.SetParent(transform);

            GameObject camObj = new GameObject("CCTV_Camera");
            camObj.transform.SetParent(cctvRig.transform, false);

            cctvCamera = camObj.AddComponent<Camera>();
            cctvCamera.targetTexture = CctvRenderTexture;
            cctvCamera.fieldOfView = 65f;
            cctvCamera.nearClipPlane = 0.05f; // Extremely low near clip to prevent mesh polygon clipping
            cctvCamera.farClipPlane = 4000f;
            cctvCamera.clearFlags = CameraClearFlags.Skybox;
            cctvCamera.enabled = false;

            // IR Night Vision Spotlight attached to CCTV
            GameObject lightObj = new GameObject("CCTV_IR_Light");
            lightObj.transform.SetParent(camObj.transform, false);
            cctvIrLight = lightObj.AddComponent<Light>();
            cctvIrLight.type = LightType.Spot;
            cctvIrLight.range = 90f;
            cctvIrLight.spotAngle = 75f;
            cctvIrLight.intensity = 1.9f;
            cctvIrLight.color = new Color(0.85f, 0.95f, 1.0f);
            cctvIrLight.enabled = false;
        }

        public void OpenCctv(FMSUnitController unit, CctvCameraChannel channel = CctvCameraChannel.CabinDriver)
        {
            if (unit == null) return;
            targetUnit = unit;
            currentChannel = channel;
            isCctvOpen = true;

            // Resolve target stream URL
            activeStreamUrl = ResolveStreamUrl(unit.unitId, channel);
            ConnectToStream(activeStreamUrl);

            FMSDashboardUI.Instance?.ShowNotification($"📹 Membuka CCTV Kabin Unit: {unit.unitId} ({unit.operatorName})");
        }

        public string ResolveStreamUrl(string unitId, CctvCameraChannel ch)
        {
            if (!string.IsNullOrEmpty(customStreamUrl))
            {
                return customStreamUrl.Replace("{unitId}", unitId).Replace("{channel}", GetChannelCode(ch));
            }

            string chCode = GetChannelCode(ch);
            return $"{cctvApiBaseUrl}/{unitId}/{chCode}.mp4";
        }

        public string GetChannelCode(CctvCameraChannel ch) => ch switch
        {
            CctvCameraChannel.CabinDriver => "cabin",
            CctvCameraChannel.FrontDashcam => "front",
            CctvCameraChannel.RearBackup => "rear",
            CctvCameraChannel.ElevatedMast => "mast",
            _ => "cabin"
        };

        public void ConnectToStream(string url)
        {
            activeStreamUrl = url;
            streamStatusMessage = $"Menghubungkan ke CCTV API: {url}...";
            isStreamConnected = false;

            if (snapshotPollCoroutine != null)
            {
                StopCoroutine(snapshotPollCoroutine);
                snapshotPollCoroutine = null;
            }

            if (!string.IsNullOrEmpty(url) && (url.EndsWith(".jpg") || url.EndsWith(".jpeg") || url.Contains("/snapshot") || url.Contains("/mjpeg")))
            {
                // Snapshot / MJPEG frame stream mode
                currentStreamSource = CctvStreamSource.ApiSnapshotStream;
                if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
                if (cctvCamera != null) cctvCamera.enabled = false;
                snapshotPollCoroutine = StartCoroutine(PollApiSnapshotFrames(url));
            }
            else if (!string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("file://") || url.StartsWith("rtsp://")))
            {
                // Video stream mode via VideoPlayer
                currentStreamSource = CctvStreamSource.ApiVideoStream;
                if (cctvCamera != null) cctvCamera.enabled = false;

                try
                {
                    videoPlayer.source = VideoSource.Url;
                    videoPlayer.url = url;
                    videoPlayer.Prepare();
                    streamStatusMessage = "Menginisialisasi decoder video stream...";
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FMS CCTV] VideoPlayer exception: {ex.Message}. Falling back to Digital Twin 3D view.");
                    FallbackToDigitalTwin();
                }
            }
            else
            {
                FallbackToDigitalTwin();
            }
        }

        private void OnVideoPlayerPrepared(VideoPlayer vp)
        {
            vp.Play();
            isStreamConnected = true;
            streamStatusMessage = "● LIVE STREAMING DARI CCTV KABIN (API OK)";
            FMSDashboardUI.Instance?.ShowNotification("📹 CCTV Video Stream Terhubung.");
        }

        private void OnVideoPlayerError(VideoPlayer vp, string message)
        {
            Debug.LogWarning($"[FMS CCTV] Stream offline or error: {message}. Mengaktifkan Digital Twin 3D Camera Feed.");
            FallbackToDigitalTwin();
        }

        public void FallbackToDigitalTwin()
        {
            currentStreamSource = CctvStreamSource.DigitalTwin3D;
            isStreamConnected = true;
            streamStatusMessage = "● SENSOR DIGITAL TWIN LIVE (3D ONBOARD SENSOR)";

            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            if (cctvCamera != null)
            {
                cctvCamera.enabled = true;
                UpdateCameraPosition(instant: true);
            }
        }

        private IEnumerator PollApiSnapshotFrames(string url)
        {
            while (isCctvOpen && currentStreamSource == CctvStreamSource.ApiSnapshotStream)
            {
                using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
                {
                    uwr.timeout = 4;
                    yield return uwr.SendWebRequest();

                    if (uwr.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                        if (tex != null)
                        {
                            if (snapshotTexture != null) DestroyImmediate(snapshotTexture);
                            snapshotTexture = tex;
                            Graphics.Blit(snapshotTexture, CctvRenderTexture);
                            isStreamConnected = true;
                            streamStatusMessage = "● LIVE SNAPSHOT CCTV KABIN (API OK)";
                        }
                    }
                    else
                    {
                        streamStatusMessage = $"⚠️ Gagal memuat snapshot ({uwr.error}). Mencoba lagi...";
                    }
                }
                yield return new WaitForSecondsRealtime(0.2f); // 5 FPS snapshot polling
            }
        }

        public void SaveCustomUrl(string newUrl)
        {
            customStreamUrl = newUrl;
            PlayerPrefs.SetString("Virexa_CctvCustomStream_" + currentSiteId, customStreamUrl);
            PlayerPrefs.Save();
            if (targetUnit != null)
            {
                ConnectToStream(ResolveStreamUrl(targetUnit.unitId, currentChannel));
            }
        }

        public void SaveBaseApiUrl(string newBaseUrl)
        {
            cctvApiBaseUrl = newBaseUrl;
            PlayerPrefs.SetString("Virexa_CctvApiUrl_" + currentSiteId, cctvApiBaseUrl);
            PlayerPrefs.Save();
            if (targetUnit != null)
            {
                ConnectToStream(ResolveStreamUrl(targetUnit.unitId, currentChannel));
            }
        }

        public void CloseCctv()
        {
            isCctvOpen = false;
            if (snapshotPollCoroutine != null)
            {
                StopCoroutine(snapshotPollCoroutine);
                snapshotPollCoroutine = null;
            }
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            if (cctvCamera != null) cctvCamera.enabled = false;
            if (cctvIrLight != null) cctvIrLight.enabled = false;
        }

        public void SwitchSite(string site, string backendUrl)
        {
            string nextSite = string.IsNullOrWhiteSpace(site) ? "astha" : site.Trim();
            if (currentSiteId != nextSite) CloseCctv();
            currentSiteId = nextSite;
            string fallbackBase = string.IsNullOrWhiteSpace(backendUrl)
                ? "" : backendUrl.TrimEnd('/') + "/api/cctv";
            cctvApiBaseUrl = PlayerPrefs.GetString("Virexa_CctvApiUrl_" + currentSiteId,
                currentSiteId == "astha" ? PlayerPrefs.GetString("Virexa_CctvApiUrl", fallbackBase) : fallbackBase);
            customStreamUrl = PlayerPrefs.GetString("Virexa_CctvCustomStream_" + currentSiteId,
                currentSiteId == "astha" ? PlayerPrefs.GetString("Virexa_CctvCustomStream", "") : "");
        }

        public void SetChannel(CctvCameraChannel channel)
        {
            currentChannel = channel;
            if (targetUnit != null)
            {
                activeStreamUrl = ResolveStreamUrl(targetUnit.unitId, channel);
                ConnectToStream(activeStreamUrl);
            }
            FMSDashboardUI.Instance?.ShowNotification($"📹 CCTV Saluran: {GetChannelName(channel)}");
        }

        public void ToggleNightVision()
        {
            isNightVision = !isNightVision;
            if (cctvIrLight != null) cctvIrLight.enabled = isNightVision;
            FMSDashboardUI.Instance?.ShowNotification(isNightVision ? "🌙 Mode CCTV Night-Vision / IR Aktif" : "☀️ Mode CCTV Normal");
        }

        private void LateUpdate()
        {
            if (!isCctvOpen || targetUnit == null || !targetUnit.gameObject.activeInHierarchy)
            {
                if (isCctvOpen && (targetUnit == null || !targetUnit.gameObject.activeInHierarchy))
                {
                    CloseCctv();
                }
                return;
            }

            if (currentStreamSource == CctvStreamSource.DigitalTwin3D)
            {
                UpdateCameraPosition(instant: false);
            }
        }

        private void UpdateCameraPosition(bool instant)
        {
            if (targetUnit == null || cctvCamera == null) return;

            Transform t = targetUnit.transform;
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            float dt = Time.unscaledDeltaTime;

            switch (currentChannel)
            {
                case CctvCameraChannel.CabinDriver:
                    // Mounted on windshield looking inside directly at driver seat & dashboard
                    Vector3 cabinOffset = targetUnit.unitType == UnitType.Excavator 
                        ? new Vector3(-0.95f, 4.35f, 1.35f) 
                        : new Vector3(-0.75f, 4.30f, 2.20f);
                    targetPos = t.TransformPoint(cabinOffset);
                    targetRot = t.rotation * Quaternion.Euler(14f, 28f, 0f);
                    cctvCamera.fieldOfView = 62f;
                    break;

                case CctvCameraChannel.FrontDashcam:
                    // Mounted on front windshield center looking forward at road (clean, no mesh clipping)
                    Vector3 frontOffset = targetUnit.unitType == UnitType.Excavator 
                        ? new Vector3(-1.1f, 4.6f, 2.4f) 
                        : new Vector3(-0.85f, 4.45f, 3.2f);
                    targetPos = t.TransformPoint(frontOffset);
                    targetRot = t.rotation * Quaternion.Euler(6f, 0f, 0f);
                    cctvCamera.fieldOfView = 72f;
                    break;

                case CctvCameraChannel.RearBackup:
                    // Mounted high at rear tail looking down backwards
                    Vector3 rearOffset = targetUnit.unitType == UnitType.Excavator 
                        ? new Vector3(0f, 4.5f, -4.5f) 
                        : new Vector3(0f, 4.2f, -5.5f);
                    targetPos = t.TransformPoint(rearOffset);
                    targetRot = t.rotation * Quaternion.Euler(20f, 180f, 0f);
                    cctvCamera.fieldOfView = 75f;
                    break;

                case CctvCameraChannel.ElevatedMast:
                default:
                    // High mast pole 12m up and 14m behind tracking the whole machine
                    Vector3 mastOffset = new Vector3(0f, 12.0f, -14.0f);
                    targetPos = t.position + t.TransformDirection(mastOffset);
                    Vector3 lookPoint = t.position + Vector3.up * 2.5f;
                    targetRot = Quaternion.LookRotation(lookPoint - targetPos, Vector3.up);
                    cctvCamera.fieldOfView = 60f;
                    break;
            }

            if (instant)
            {
                cctvCamera.transform.position = targetPos;
                cctvCamera.transform.rotation = targetRot;
            }
            else
            {
                cctvCamera.transform.position = Vector3.Lerp(cctvCamera.transform.position, targetPos, dt * 16f);
                cctvCamera.transform.rotation = Quaternion.Slerp(cctvCamera.transform.rotation, targetRot, dt * 16f);
            }
        }

        public string GetChannelName(CctvCameraChannel ch) => ch switch
        {
            CctvCameraChannel.CabinDriver => "CH-01 [Kabin Supir & AI Fatigue]",
            CctvCameraChannel.FrontDashcam => "CH-02 [Dashcam Depan Jalan]",
            CctvCameraChannel.RearBackup => "CH-03 [Kamera Belakang/Dump]",
            CctvCameraChannel.ElevatedMast => "CH-04 [Mast 360 Orbit]",
            _ => "CH-01 [Kabin Supir]"
        };

        private void OnDestroy()
        {
            if (CctvRenderTexture != null)
            {
                CctvRenderTexture.Release();
                DestroyImmediate(CctvRenderTexture);
            }
            if (snapshotTexture != null)
            {
                DestroyImmediate(snapshotTexture);
            }
        }
    }
}
