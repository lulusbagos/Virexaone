using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    /// <summary>
    /// Real-time Two-Way Communication, Talkback Voice, & Dispatch Radio Messenger Engine.
    /// Completely standalone: Works seamlessly in Mobile Cabin and Control Room.
    /// Supports both Direct PTT (Push-To-Talk) and Request-To-Talk authorization modes.
    /// </summary>
    public class FMSFleetMessenger : MonoBehaviour
    {
        public static FMSFleetMessenger Instance { get; private set; }

        public enum MessagePriority
        {
            Normal,       // Info rutin / operasional
            Urgent,       // Pengalihan rute / percepat hauling
            Emergency     // Bahaya pit, cuaca ekstrem, blasting
        }

        [System.Serializable]
        public class ChatMessage
        {
            public string messageId;
            public string senderName;       // "Control Room (Dispatcher)" or "RD5104 (Budi Pratama)"
            public string senderRole;       // "DISPATCHER" / "OPERATOR"
            public string targetUnitId;     // "ALL" (Broadcast) or specific "RD5104"
            public string messageText;
            public MessagePriority priority;
            public string timestamp;
            public bool isRead;
            public bool isAcknowledged;    // Operator replied "10-4 Copy"
        }

        [Header("Live Message Log")]
        public List<ChatMessage> messageHistory = new List<ChatMessage>();
        public int unreadCabinMessagesCount = 0;

        [Header("Backend Cabin Comms")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public bool sendMessagesToBackend = true;
        public string lastBackendCommsStatus = "Belum tersambung";

        [Header("Live Talkback (Control Room -> Cabin Push-To-Talk)")]
        public bool isTalkbackActive = false;
        public string talkbackTargetUnit = "ALL";
        public float talkbackStartTime = 0f;
        public float talkbackDuration = 0f;

        [Header("Live Cabin Talkback (In-Cabin -> Control Room Push-To-Talk)")]
        public bool isCabinTalkbackActive = false;
        public string cabinTalkbackSourceUnit = "";
        public string cabinTalkbackOperatorName = "";
        public float cabinTalkbackStartTime = 0f;
        public float cabinTalkbackDuration = 0f;

        public enum InboundCommsPolicy
        {
            OpenDirect,          // Bebas langsung masuk (Direct Messages & Direct PTT)
            RequireAuthorization // Wajib Izin Dispatcher (Request-To-Talk queue)
        }

        public enum NotificationFilterLevel
        {
            AllMessagesAndVoice, // Semua pesan & PTT bersuara
            UrgentOnly,          // Hanya Urgent & Emergency
            Muted                // Hening (Visual Saja)
        }

        [Header("Control Room Communication Policy")]
        public InboundCommsPolicy commsPolicy = InboundCommsPolicy.OpenDirect;
        public NotificationFilterLevel notificationFilter = NotificationFilterLevel.AllMessagesAndVoice;
        public bool autoFollowCameraOnIncomingComms = true;

        [Header("Request-To-Talk Queue (Pending Authorization)")]
        public bool hasPendingTalkbackRequest = false;
        public string pendingRequestUnitId = "";
        public string pendingRequestOperatorName = "";
        public float pendingRequestTime = 0f;

        [System.Serializable]
        public class BackendCommsMessageDto
        {
            public string id;
            public string unit_name;
            public string sender_role;
            public string kind;
            public string body;
            public string priority;
            public string sent_at;
        }

        [System.Serializable]
        public class BackendCommsMessageListResponse
        {
            public string status;
            public List<BackendCommsMessageDto> data;
        }

        private HashSet<string> knownBackendMessageIds = new HashSet<string>();
        private bool isInitialCommsPollDone = false;
        private Coroutine commsPollCoroutine;

        // Recent Inbound Cabin Activity tracker
        public string lastActiveCabinUnit = "";
        public float lastActiveCabinTime = -999f;

        // Events & Callbacks
        public event Action<ChatMessage> OnNewMessageReceived;
        public event Action<bool, string> OnTalkbackStateChanged;
        public event Action<bool, string, string> OnCabinTalkbackStateChanged;
        public static Action<string> OnNotificationRequested;
        public static Action<string, string, string, bool, bool> OnInboundCabinCommsReceived; // unitName, senderRole, body, isVoice, isUrgent

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            SeedDefaultDemoMessages();
        }

        private void Start()
        {
            if (sendMessagesToBackend)
            {
                commsPollCoroutine = StartCoroutine(PollBackendCabinMessagesLoop());
            }
        }

        private void OnEnable()
        {
            if (sendMessagesToBackend && commsPollCoroutine == null)
            {
                commsPollCoroutine = StartCoroutine(PollBackendCabinMessagesLoop());
            }
        }

        private void OnDisable()
        {
            if (commsPollCoroutine != null)
            {
                StopCoroutine(commsPollCoroutine);
                commsPollCoroutine = null;
            }
        }

        private void Update()
        {
            if (isTalkbackActive)
            {
                talkbackDuration = Time.time - talkbackStartTime;
            }

            if (isCabinTalkbackActive)
            {
                cabinTalkbackDuration = Time.time - cabinTalkbackStartTime;
            }
        }

        private void SeedDefaultDemoMessages()
        {
            messageHistory.Add(new ChatMessage
            {
                messageId = "MSG-001",
                senderName = "Control Room (Rian)",
                senderRole = "DISPATCHER",
                targetUnitId = "ALL",
                messageText = "Selamat pagi seluruh armada Shift 1. Utamakan keselamatan kerja, periksa P2H sebelum beroperasi.",
                priority = MessagePriority.Normal,
                timestamp = DateTime.Now.AddMinutes(-45).ToString("HH:mm"),
                isRead = true,
                isAcknowledged = true
            });

            messageHistory.Add(new ChatMessage
            {
                messageId = "MSG-002",
                senderName = "Control Room (Rian)",
                senderRole = "DISPATCHER",
                targetUnitId = "RD5091",
                messageText = "RD5091, antrean di EX-201 mulai padat. Siapkan unit untuk pengalihan ke EX-204 setelah ritase ini.",
                priority = MessagePriority.Urgent,
                timestamp = DateTime.Now.AddMinutes(-8).ToString("HH:mm"),
                isRead = false,
                isAcknowledged = false
            });
            unreadCabinMessagesCount = 1;
        }

        /// <summary>
        /// Sends message from Control Room to a specific truck or ALL fleet
        /// </summary>
        public void SendFromControlRoom(string targetUnitId, string text, MessagePriority priority = MessagePriority.Normal)
        {
            if (string.IsNullOrEmpty(text)) return;

            ChatMessage msg = new ChatMessage
            {
                messageId = $"MSG-{UnityEngine.Random.Range(1000, 9999)}",
                senderName = "Control Room (Dispatcher)",
                senderRole = "DISPATCHER",
                targetUnitId = string.IsNullOrEmpty(targetUnitId) ? "ALL" : targetUnitId,
                messageText = text,
                priority = priority,
                timestamp = DateTime.Now.ToString("HH:mm"),
                isRead = false,
                isAcknowledged = false
            };

            messageHistory.Add(msg);
            unreadCabinMessagesCount++;
            OnNewMessageReceived?.Invoke(msg);

            // Audio Alert & Notification
            Mobile.OperatorAudioFeedbackManager.Instance?.PlayDispatchAlert();
            TriggerNotification($"📻 Pesan Radio terkirim ke {msg.targetUnitId}: \"{text}\"");

            if (sendMessagesToBackend)
            {
                StartCoroutine(PostDispatchMessageToBackend(msg));
            }
        }

        private IEnumerator PostDispatchMessageToBackend(ChatMessage msg)
        {
            string baseUrl = FMSDashboardUI.Instance != null && !string.IsNullOrWhiteSpace(FMSDashboardUI.Instance.apiBaseUrl)
                ? FMSDashboardUI.Instance.apiBaseUrl : backendBaseUrl;

            if (msg == null || string.IsNullOrWhiteSpace(baseUrl)) yield break;

            string safeText = (msg.messageText ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
            string unit = string.IsNullOrWhiteSpace(msg.targetUnitId) ? "ALL" : msg.targetUnitId.Trim();
            string priority = msg.priority == MessagePriority.Urgent || msg.priority == MessagePriority.Emergency
                ? "urgent" : "normal";
            string json = $"{{\"unit_name\":\"{unit}\",\"body\":\"{safeText}\",\"priority\":\"{priority}\"}}";

            using (var request = new UnityWebRequest(baseUrl.TrimEnd('/') + "/api/v1/comms/messages", "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                FMSApiSession.AuthorizeDispatcher(request);
                request.timeout = 8;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    lastBackendCommsStatus = $"Pesan terkirim ke backend ({unit})";
                }
                else
                {
                    lastBackendCommsStatus = $"Gagal kirim pesan backend: HTTP {request.responseCode}";
                    TriggerNotification($"⚠️ Pesan belum masuk ke mobile: {lastBackendCommsStatus}");
                }
            }
        }

        /// <summary>
        /// Sends message or quick acknowledgment from In-Cabin Operator to Control Room
        /// </summary>
        public void SendFromCabin(string unitId, string operatorName, string text, MessagePriority priority = MessagePriority.Normal)
        {
            if (string.IsNullOrEmpty(text)) return;

            ChatMessage msg = new ChatMessage
            {
                messageId = $"MSG-{UnityEngine.Random.Range(1000, 9999)}",
                senderName = $"{unitId} ({operatorName})",
                senderRole = "OPERATOR",
                targetUnitId = "CONTROL_ROOM",
                messageText = text,
                priority = priority,
                timestamp = DateTime.Now.ToString("HH:mm"),
                isRead = true,
                isAcknowledged = true
            };

            messageHistory.Add(msg);
            OnNewMessageReceived?.Invoke(msg);

            Mobile.OperatorAudioFeedbackManager.Instance?.PlaySuccessChime();
            TriggerNotification($"📩 Pesan dari Kabin {unitId}: \"{text}\"");
        }

        /// <summary>
        /// Starts live Voice Talkback transmission from Control Room to target unit (or ALL fleet)
        /// </summary>
        public void StartTalkback(string targetUnit = "ALL")
        {
            isTalkbackActive = true;
            talkbackTargetUnit = string.IsNullOrEmpty(targetUnit) ? "ALL" : targetUnit;
            talkbackStartTime = Time.time;
            talkbackDuration = 0f;

            OnTalkbackStateChanged?.Invoke(true, talkbackTargetUnit);
            Mobile.OperatorAudioFeedbackManager.Instance?.PlayDispatchAlert();
            TriggerNotification($"🎙️ [TALKBACK DISPATCH AKTIF] Transmisi suara live ke: {talkbackTargetUnit}...");
        }

        /// <summary>
        /// Ends live Voice Talkback transmission from Control Room and logs voice dispatch record
        /// </summary>
        public void StopTalkback()
        {
            if (!isTalkbackActive) return;

            isTalkbackActive = false;
            float finalDuration = talkbackDuration;

            OnTalkbackStateChanged?.Invoke(false, talkbackTargetUnit);
            Mobile.OperatorAudioFeedbackManager.Instance?.PlayButtonClick();

            string targetLabel = talkbackTargetUnit == "ALL" ? "Seluruh Armada (Broadcast)" : talkbackTargetUnit;
            SendFromControlRoom(talkbackTargetUnit, $"🎙️ [TRANSMISI SUARA RADIO ({finalDuration:F1}s)] ke {targetLabel}");
            TriggerNotification($"🎙️ [TALKBACK SELESAI] Transmisi suara {finalDuration:F1}s terkirim ke {talkbackTargetUnit}.");
        }

        /// <summary>
        /// Operator in Cabin starts transmitting live voice to Control Room (Direct PTT)
        /// </summary>
        public void StartCabinTalkback(string unitId, string operatorName)
        {
            isCabinTalkbackActive = true;
            cabinTalkbackSourceUnit = unitId;
            cabinTalkbackOperatorName = operatorName;
            cabinTalkbackStartTime = Time.time;
            cabinTalkbackDuration = 0f;

            hasPendingTalkbackRequest = false;
            OnCabinTalkbackStateChanged?.Invoke(true, unitId, operatorName);
            Mobile.OperatorAudioFeedbackManager.Instance?.PlaySuccessChime();
            TriggerNotification($"📞 [RADIO KABIN MASUK] {unitId} ({operatorName}) sedang berbicara live ke Ruang Kontrol...");
        }

        /// <summary>
        /// Operator in Cabin stops transmitting voice to Control Room
        /// </summary>
        public void StopCabinTalkback()
        {
            if (!isCabinTalkbackActive) return;

            isCabinTalkbackActive = false;
            float finalDuration = cabinTalkbackDuration;
            string unitId = cabinTalkbackSourceUnit;
            string opName = cabinTalkbackOperatorName;

            OnCabinTalkbackStateChanged?.Invoke(false, unitId, opName);
            Mobile.OperatorAudioFeedbackManager.Instance?.PlayButtonClick();

            SendFromCabin(unitId, opName, $"🎙️ [TRANSMISI SUARA KABIN ({finalDuration:F1}s)]");
            TriggerNotification($"📞 [RADIO KABIN SELESAI] Panggilan suara dari {unitId} ({finalDuration:F1}s) selesai.");
        }

        /// <summary>
        /// Operator in Cabin requests permission to talk (Buzzer / Request-to-Talk)
        /// </summary>
        public void RequestCabinTalkback(string unitId, string operatorName)
        {
            hasPendingTalkbackRequest = true;
            pendingRequestUnitId = unitId;
            pendingRequestOperatorName = operatorName;
            pendingRequestTime = Time.time;

            Mobile.OperatorAudioFeedbackManager.Instance?.PlayWarningBeep();
            TriggerNotification($"🔔 [PERMINTAAN BICARA] {unitId} ({operatorName}) meminta izin transmisi radio ke Ruang Kontrol.");
        }

        /// <summary>
        /// Control Room approves pending talkback request from Cabin
        /// </summary>
        public void ApproveCabinTalkback(string unitId)
        {
            if (hasPendingTalkbackRequest && pendingRequestUnitId.Equals(unitId, StringComparison.OrdinalIgnoreCase))
            {
                hasPendingTalkbackRequest = false;
                StartCabinTalkback(pendingRequestUnitId, pendingRequestOperatorName);
                TriggerNotification($"✅ [IZIN DIBERIKAN] Channel radio dibuka untuk {unitId}.");
            }
        }

        public void DismissTalkbackRequest()
        {
            hasPendingTalkbackRequest = false;
        }

        private void TriggerNotification(string message)
        {
            OnNotificationRequested?.Invoke(message);
        }

        public List<ChatMessage> GetMessagesForUnit(string unitId)
        {
            return messageHistory.FindAll(m => m.targetUnitId == "ALL" || m.targetUnitId.Equals(unitId, StringComparison.OrdinalIgnoreCase) || m.senderName.Contains(unitId));
        }

        public void MarkAllRead(string unitId)
        {
            unreadCabinMessagesCount = 0;
            foreach (var m in messageHistory)
            {
                if (m.targetUnitId == "ALL" || m.targetUnitId.Equals(unitId, StringComparison.OrdinalIgnoreCase))
                {
                    m.isRead = true;
                }
            }
        }

        public bool IsUnitRecentlyCommunicating(string unitId, float withinSeconds = 15f)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(lastActiveCabinUnit)) return false;
            return unitId.Equals(lastActiveCabinUnit, StringComparison.OrdinalIgnoreCase) && (Time.time - lastActiveCabinTime <= withinSeconds);
        }

        /// <summary>
        /// Periodically queries the backend comms endpoint for new messages and PTT from mobile cabin operators
        /// </summary>
        private IEnumerator PollBackendCabinMessagesLoop()
        {
            var waitInterval = new WaitForSeconds(1.0f);
            while (true)
            {
                yield return waitInterval;

                string baseUrl = FMSDashboardUI.Instance != null && !string.IsNullOrWhiteSpace(FMSDashboardUI.Instance.apiBaseUrl)
                    ? FMSDashboardUI.Instance.apiBaseUrl : backendBaseUrl;

                if (string.IsNullOrWhiteSpace(baseUrl)) continue;

                string url = baseUrl.TrimEnd('/') + "/api/v1/comms/messages?unit_name=ALL";
                using (var request = UnityWebRequest.Get(url))
                {
                    FMSApiSession.AuthorizeDispatcher(request);
                    request.timeout = 4;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string json = request.downloadHandler.text;
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            try
                            {
                                var resp = JsonUtility.FromJson<BackendCommsMessageListResponse>(json);
                                if (resp != null && resp.data != null)
                                {
                                    ProcessInboundBackendMessages(resp.data);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[FMSFleetMessenger] Parse error comms: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private void ProcessInboundBackendMessages(List<BackendCommsMessageDto> incomingList)
        {
            if (incomingList == null) return;

            if (!isInitialCommsPollDone)
            {
                // First poll: record initial database history so existing messages don't burst popups
                foreach (var item in incomingList)
                {
                    if (!string.IsNullOrEmpty(item.id))
                    {
                        knownBackendMessageIds.Add(item.id);
                    }
                }
                isInitialCommsPollDone = true;
                return;
            }

            foreach (var item in incomingList)
            {
                if (string.IsNullOrEmpty(item.id) || knownBackendMessageIds.Contains(item.id))
                    continue;

                knownBackendMessageIds.Add(item.id);

                // Process inbound messages from cabin / mobile operator
                bool isDispatcher = string.Equals(item.sender_role, "dispatcher", StringComparison.OrdinalIgnoreCase);
                if (!isDispatcher)
                {
                    string unit = string.IsNullOrWhiteSpace(item.unit_name) ? "CABIN" : item.unit_name.Trim();
                    bool isVoice = string.Equals(item.kind, "voice", StringComparison.OrdinalIgnoreCase) || 
                                   (item.body != null && (item.body.Contains("PTT") || item.body.Contains("Suara") || item.body.Contains("Radio") || item.body.Contains("🎙️")));
                    bool isUrgent = string.Equals(item.priority, "urgent", StringComparison.OrdinalIgnoreCase) || 
                                    string.Equals(item.priority, "emergency", StringComparison.OrdinalIgnoreCase);

                    lastActiveCabinUnit = unit;
                    lastActiveCabinTime = Time.time;

                    // If policy is RequireAuthorization for voice PTT
                    if (commsPolicy == InboundCommsPolicy.RequireAuthorization && isVoice)
                    {
                        RequestCabinTalkback(unit, $"{unit} (Operator)");
                        return;
                    }

                    if (isVoice)
                    {
                        StartCabinTalkback(unit, $"{unit} (Operator)");
                    }

                    ChatMessage chatMsg = new ChatMessage
                    {
                        messageId = item.id,
                        senderName = $"{unit} (Operator)",
                        senderRole = "OPERATOR",
                        targetUnitId = "CONTROL_ROOM",
                        messageText = item.body,
                        priority = isUrgent ? MessagePriority.Urgent : MessagePriority.Normal,
                        timestamp = DateTime.Now.ToString("HH:mm:ss"),
                        isRead = false,
                        isAcknowledged = false
                    };

                    messageHistory.Add(chatMsg);
                    unreadCabinMessagesCount++;
                    OnNewMessageReceived?.Invoke(chatMsg);

                    // Chime audio
                    Mobile.OperatorAudioFeedbackManager.Instance?.PlayWarningBeep();

                    // Fire global event
                    OnInboundCabinCommsReceived?.Invoke(unit, "OPERATOR", item.body, isVoice, isUrgent);

                    // Show visual Popup in Control Room Dashboard
                    if (FMSDashboardUI.Instance != null)
                    {
                        FMSDashboardUI.Instance.ShowCabinCommunicationPopup(unit, $"{unit} (Kabin)", item.body, isVoice, isUrgent);

                        if (autoFollowCameraOnIncomingComms)
                        {
                            var unitCtrl = FMSFleetManager.Instance?.GetUnitById(unit);
                            if (unitCtrl != null)
                            {
                                FMSFleetManager.Instance.SelectUnit(unitCtrl);
                                FMSCameraController.Instance?.SetFollowTarget(unitCtrl.transform);
                            }
                        }
                    }
                }
            }
        }
    }
}
