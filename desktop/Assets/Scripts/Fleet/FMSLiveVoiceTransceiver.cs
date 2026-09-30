using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    /// <summary>
    /// High-Performance Zero-Delay Real-Time Bidirectional Voice PTT Transceiver for Unity Control Room.
    /// Completely FREE & Standalone: Uses Unity native Microphone + 16kHz PCM16 WebSocket Audio Streaming + OnAudioFilterRead.
    /// Seamlessly communicates with Flutter In-Cabin mobile operators with ultra-low latency.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FMSLiveVoiceTransceiver : MonoBehaviour
    {
        public static FMSLiveVoiceTransceiver Instance { get; private set; }

        public const int SAMPLE_RATE = 16000;
        public const int CHANNELS = 1;

        [Header("Connection State")]
        public bool isConnected = false;
        public bool isTransmitting = false; // Dispatcher speaking (Mic -> Mobile)
        public bool isReceivingVoice = false; // Mobile Operator speaking (Mobile -> Unity)
        public string currentReceivingUnit = "";
        public string currentReceivingRole = "";
        public string connectionStatus = "Menghubungkan radio...";

        [Header("Audio Devices & Gain")]
        public string selectedMicrophoneDevice = null;
        [Range(0.5f, 4.0f)] public float microphoneGain = 1.5f;
        [Range(0.1f, 3.0f)] public float speakerVolume = 1.2f;
        public float liveMicVolumeLevel = 0f; // 0.0 to 1.0 VU meter

        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private Coroutine connectionLoopCoroutine;
        private AudioSource audioSource;
        private AudioClip recordingClip;
        private int lastSamplePosition = 0;
        private string activeMicrophoneName = null;

        // Thread-safe queues for cross-thread dispatching & low-latency audio DSP
        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<float> audioPlaybackQueue = new ConcurrentQueue<float>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // 2D Stereo
            audioSource.volume = 1f;

            // Generate an infinite silent dummy carrier clip so OnAudioFilterRead continuously runs
            if (audioSource.clip == null)
            {
                audioSource.clip = AudioClip.Create("LiveRadioSilentCarrier", SAMPLE_RATE * 2, 1, SAMPLE_RATE, false);
                float[] dummy = new float[SAMPLE_RATE * 2];
                audioSource.clip.SetData(dummy, 0);
            }
            if (!audioSource.isPlaying) audioSource.Play();
        }

        private void Start()
        {
            RefreshMicrophoneDevices();
            connectionLoopCoroutine = StartCoroutine(ConnectionLifecycleLoop());
        }

        public void RefreshMicrophoneDevices()
        {
            if (Microphone.devices.Length > 0)
            {
                selectedMicrophoneDevice = Microphone.devices[0];
                Debug.Log($"[FMSLiveVoiceTransceiver] Default Microphone Selected: '{selectedMicrophoneDevice}'");
            }
            else
            {
                selectedMicrophoneDevice = null;
                Debug.LogWarning("[FMSLiveVoiceTransceiver] No physical microphone detected on system.");
            }
        }

        private void OnEnable()
        {
            if (!audioSource.isPlaying) audioSource.Play();
            if (connectionLoopCoroutine == null)
            {
                connectionLoopCoroutine = StartCoroutine(ConnectionLifecycleLoop());
            }
        }

        private void OnDisable()
        {
            StopTransmitting();
            DisconnectWebSocket();
            if (connectionLoopCoroutine != null)
            {
                StopCoroutine(connectionLoopCoroutine);
                connectionLoopCoroutine = null;
            }
        }

        private void Update()
        {
            // 1. Dispatch thread-safe actions on the Unity Main Thread
            while (mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FMSLiveVoiceTransceiver] Main thread action error: {ex.Message}");
                }
            }

            // 2. Process outgoing live microphone audio when transmitting
            if (isTransmitting && recordingClip != null)
            {
                ProcessOutgoingMicrophoneAudio();
            }

            // 3. Keep silent carrier running for DSP filter read
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private float resamplePos = 0f;
        private float currentSample = 0f;
        private float nextSample = 0f;

        /// <summary>
        /// DSP Audio Filter Read: Real-time, stutter-free streaming audio playback with linear sample-rate resampling
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float vol = speakerVolume;
            int outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            float step = (float)SAMPLE_RATE / outRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                while (resamplePos >= 1.0f)
                {
                    resamplePos -= 1.0f;
                    currentSample = nextSample;
                    if (!audioPlaybackQueue.TryDequeue(out nextSample))
                    {
                        nextSample = 0f;
                    }
                }

                float sample = Mathf.Lerp(currentSample, nextSample, resamplePos) * vol;
                resamplePos += step;

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }

        /// <summary>
        /// Starts live Microphone Push-To-Talk voice transmission to Mobile Cabin operators
        /// </summary>
        public void StartTransmitting(string targetUnit = "ALL")
        {
            if (isTransmitting) return;

            isTransmitting = true;
            lastSamplePosition = 0;

            activeMicrophoneName = !string.IsNullOrEmpty(selectedMicrophoneDevice)
                ? selectedMicrophoneDevice
                : (Microphone.devices.Length > 0 ? Microphone.devices[0] : null);

            try
            {
                if (Microphone.devices.Length > 0 || activeMicrophoneName != null)
                {
                    recordingClip = Microphone.Start(activeMicrophoneName, true, 20, SAMPLE_RATE);
                    Debug.Log($"[FMSLiveVoiceTransceiver] Microphone recording started on '{activeMicrophoneName}' (16kHz)");
                }
                else
                {
                    Debug.LogWarning("[FMSLiveVoiceTransceiver] No physical mic detected; transmitting digital audio carrier frame.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FMSLiveVoiceTransceiver] Microphone.Start warning: {ex.Message}");
            }

            // Send PTT Start control frame over WebSocket
            SendTextMessage("{\"type\":\"ptt_start\"}");

            Mobile.OperatorAudioFeedbackManager.Instance?.PlayDispatchAlert();
            Debug.Log($"[FMSLiveVoiceTransceiver] Started live PTT radio broadcast to: {targetUnit}");
        }

        /// <summary>
        /// Stops live Microphone Push-To-Talk voice transmission
        /// </summary>
        public void StopTransmitting()
        {
            if (!isTransmitting) return;

            isTransmitting = false;
            liveMicVolumeLevel = 0f;

            try
            {
                if (Microphone.IsRecording(activeMicrophoneName))
                {
                    Microphone.End(activeMicrophoneName);
                }
            }
            catch { }

            recordingClip = null;

            // Send PTT Stop control frame over WebSocket
            SendTextMessage("{\"type\":\"ptt_stop\"}");

            Mobile.OperatorAudioFeedbackManager.Instance?.PlayButtonClick();
            Debug.Log("[FMSLiveVoiceTransceiver] Stopped live PTT radio broadcast.");
        }

        private void ProcessOutgoingMicrophoneAudio()
        {
            if (recordingClip == null) return;

            int currentPos = Microphone.GetPosition(activeMicrophoneName);
            if (currentPos < 0 || currentPos == lastSamplePosition) return;

            int sampleCount = currentPos >= lastSamplePosition
                ? currentPos - lastSamplePosition
                : (recordingClip.samples - lastSamplePosition) + currentPos;

            if (sampleCount <= 0) return;

            float[] floatBuffer = new float[sampleCount];
            recordingClip.GetData(floatBuffer, lastSamplePosition);
            lastSamplePosition = currentPos;

            // Compute live VU-meter amplitude
            float sumSquares = 0f;
            for (int i = 0; i < floatBuffer.Length; i++)
            {
                float val = floatBuffer[i] * microphoneGain;
                sumSquares += val * val;
            }
            liveMicVolumeLevel = Mathf.Clamp01(Mathf.Sqrt(sumSquares / floatBuffer.Length) * 3.5f);

            // Convert Float[-1.0, 1.0] to 16-bit PCM Little-Endian Bytes
            byte[] pcmBytes = new byte[floatBuffer.Length * 2];
            for (int i = 0; i < floatBuffer.Length; i++)
            {
                float sample = Mathf.Clamp(floatBuffer[i] * microphoneGain, -1.0f, 1.0f);
                short pcmSample = (short)(sample * 32767f);
                pcmBytes[i * 2] = (byte)(pcmSample & 0xFF);
                pcmBytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            // Stream PCM16 bytes over WebSocket directly to backend & mobile cabin
            SendBinaryData(pcmBytes);
        }

        private IEnumerator ConnectionLifecycleLoop()
        {
            var waitInterval = new WaitForSeconds(3.0f);

            while (true)
            {
                if (webSocket == null || webSocket.State != WebSocketState.Open)
                {
                    yield return ConnectWebSocketCoroutine();
                }

                yield return waitInterval;
            }
        }

        private IEnumerator ConnectWebSocketCoroutine()
        {
            string baseUrl = FMSDashboardUI.Instance != null && !string.IsNullOrWhiteSpace(FMSDashboardUI.Instance.apiBaseUrl)
                ? FMSDashboardUI.Instance.apiBaseUrl : "http://127.0.0.1:8000";

            string ticketUrl = $"{baseUrl.TrimEnd('/')}/api/v1/comms/live-ticket";
            string ticketJsonPayload = "{\"unit_name\":\"ALL\"}";

            using (UnityWebRequest req = new UnityWebRequest(ticketUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(ticketJsonPayload);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                FMSApiSession.AuthorizeDispatcher(req);
                req.timeout = 6;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    connectionStatus = "Gagal meminta tiket radio";
                    isConnected = false;
                    yield break;
                }

                string ticket = null;
                try
                {
                    string resText = req.downloadHandler.text;
                    TicketResponseDto ticketObj = JsonUtility.FromJson<TicketResponseDto>(resText);
                    ticket = ticketObj?.ticket;
                }
                catch { }

                if (string.IsNullOrEmpty(ticket))
                {
                    connectionStatus = "Tiket radio kosong";
                    yield break;
                }

                // Connect WebSocket
                string wsScheme = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
                string host = baseUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/');
                string wsUrl = $"{wsScheme}://{host}/api/v1/comms/live?ticket={UnityWebRequest.EscapeURL(ticket)}";

                cts = new CancellationTokenSource();
                webSocket = new ClientWebSocket();

                Task connectTask = null;
                try
                {
                    connectTask = webSocket.ConnectAsync(new Uri(wsUrl), cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FMSLiveVoiceTransceiver] ConnectAsync exception: {ex.Message}");
                    isConnected = false;
                    yield break;
                }

                while (!connectTask.IsCompleted)
                {
                    yield return null;
                }

                if (connectTask.IsFaulted)
                {
                    Debug.LogWarning($"[FMSLiveVoiceTransceiver] WebSocket connection failed: {connectTask.Exception?.GetBaseException()?.Message}");
                    isConnected = false;
                    yield break;
                }

                if (webSocket.State == WebSocketState.Open)
                {
                    isConnected = true;
                    connectionStatus = "📻 Radio PTT Live Terhubung (16kHz Zero-Delay)";
                    Debug.Log($"[FMSLiveVoiceTransceiver] SUCCESS! Connected to Live Cabin Radio WebSocket at {wsUrl}");

                    // Start background receive loop
                    _ = Task.Run(ReceiveWebSocketFramesLoop, cts.Token);
                }
                else
                {
                    isConnected = false;
                    connectionStatus = "Gagal menyambung WebSocket radio";
                }
            }
        }

        private async Task ReceiveWebSocketFramesLoop()
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (webSocket != null && webSocket.State == WebSocketState.Open && cts != null && !cts.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                    {
                        string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleIncomingTextCommand(text);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0 && result.Count % 2 == 0)
                    {
                        // Convert 16-bit PCM Little-Endian bytes to Floats and enqueue for DSP
                        int sampleCount = result.Count / 2;
                        for (int i = 0; i < sampleCount; i++)
                        {
                            short pcmSample = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                            audioPlaybackQueue.Enqueue(pcmSample / 32768.0f);
                        }

                        // Cap playback queue to avoid latency accumulation (max 1.5 seconds)
                        while (audioPlaybackQueue.Count > SAMPLE_RATE * 1.5f)
                        {
                            audioPlaybackQueue.TryDequeue(out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[FMSLiveVoiceTransceiver] WebSocket loop ended: {ex.Message}");
            }
            finally
            {
                mainThreadActions.Enqueue(() =>
                {
                    isConnected = false;
                    isReceivingVoice = false;
                });
            }
        }

        private void HandleIncomingTextCommand(string json)
        {
            try
            {
                WsControlDto dto = JsonUtility.FromJson<WsControlDto>(json);
                if (dto == null) return;

                if (dto.type == "voice_start" && dto.sender_role != "dispatcher")
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        isReceivingVoice = true;
                        currentReceivingUnit = dto.unit_name;
                        currentReceivingRole = dto.sender_role;

                        FMSFleetMessenger.Instance?.StartCabinTalkback(dto.unit_name, $"Operator {dto.unit_name}");
                        if (FMSDashboardUI.Instance != null)
                        {
                            FMSDashboardUI.Instance.ShowCabinCommunicationPopup(dto.unit_name, $"Operator {dto.unit_name}", "Transmisi Suara PTT Kabin Aktif (16kHz Live)", true, false);
                        }
                    });
                }
                else if (dto.type == "voice_stop")
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        isReceivingVoice = false;
                        FMSFleetMessenger.Instance?.StopCabinTalkback();
                    });
                }
                else if (dto.type == "ready")
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        isConnected = true;
                        connectionStatus = "📻 Radio PTT Live Terhubung (16kHz Zero-Delay)";
                    });
                }
                else if (dto.type == "message")
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        if (FMSFleetMessenger.Instance != null)
                        {
                            FMSFleetMessenger.Instance.RefreshMessagesFromBackend();
                        }
                    });
                }
            }
            catch { }
        }

        private async void SendTextMessage(string text)
        {
            if (webSocket == null || webSocket.State != WebSocketState.Open) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
        }

        private async void SendBinaryData(byte[] data)
        {
            if (webSocket == null || webSocket.State != WebSocketState.Open) return;
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch { }
        }

        private void DisconnectWebSocket()
        {
            try
            {
                cts?.Cancel();
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                webSocket?.Dispose();
                webSocket = null;
            }
            catch { }
            isConnected = false;
            isTransmitting = false;
            isReceivingVoice = false;
        }

        [System.Serializable]
        private class TicketResponseDto
        {
            public string ticket;
            public int expires_in_seconds;
        }

        [System.Serializable]
        private class WsControlDto
        {
            public string type;
            public string unit_name;
            public string sender_role;
        }
    }
}
