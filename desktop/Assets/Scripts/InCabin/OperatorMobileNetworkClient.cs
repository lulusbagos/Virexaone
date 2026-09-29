using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Handles network synchronization between Mobile In-Cabin Dashboard and Virexa FMS Backend (ASP.NET Core REST API).
    /// </summary>
    public class OperatorMobileNetworkClient : MonoBehaviour
    {
        public static OperatorMobileNetworkClient Instance { get; private set; }

        [Header("Backend Connection Settings")]
        public string apiBaseUrl = "http://127.0.0.1:8000";
        public bool isConnected = false;
        public float syncIntervalSeconds = 2.0f;

        [Header("Available Units Cache")]
        public List<string> availableUnitIds = new List<string>();
        public List<LiveUnitData> liveFleetData = new List<LiveUnitData>();

        [System.Serializable]
        public class LiveUnitData
        {
            public string unit_id;
            public string unit_model;
            public string unit_type;
            public string operator_name;
            public string status;
            public float speed_kmh;
            public float fuel_percent;
            public float payload_tons;
            public float latitude;
            public float longitude;
            public float elevation;
            public string assigned_loader_id;
            public string assigned_front_name;
            public string assigned_disposal_name;
        }

        [System.Serializable]
        private class FleetApiResponse
        {
            public string status;
            public int count;
            public List<LiveUnitData> data;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            // Populate initial default units if empty
            if (availableUnitIds.Count == 0)
            {
                for (int i = 1; i <= 18; i++)
                {
                    availableUnitIds.Add($"RD{5000 + i}");
                }
            }
        }

        private void Start()
        {
            StartCoroutine(SyncLoop());
        }

        private IEnumerator SyncLoop()
        {
            while (true)
            {
                yield return StartCoroutine(FetchLiveFleet());
                yield return new WaitForSeconds(syncIntervalSeconds);
            }
        }

        public IEnumerator FetchLiveFleet()
        {
            string configuredUrl = Virexa.FMS.FMSDashboardUI.Instance != null
                ? Virexa.FMS.FMSDashboardUI.Instance.apiBaseUrl : apiBaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                isConnected = false;
                yield break;
            }
            string url = $"{configuredUrl.TrimEnd('/')}/api/v1/fleet/live";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                Virexa.FMS.FMSApiSession.Authorize(req);
                req.timeout = 2;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    isConnected = true;
                    try
                    {
                        string json = req.downloadHandler.text;
                        FleetApiResponse resp = JsonUtility.FromJson<FleetApiResponse>(json);
                        if (resp != null && resp.data != null)
                        {
                            liveFleetData = resp.data;
                            availableUnitIds.Clear();
                            foreach (var u in resp.data)
                            {
                                if (!string.IsNullOrEmpty(u.unit_id))
                                {
                                    availableUnitIds.Add(u.unit_id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VirexaMobile] JSON parse error: {ex.Message}");
                    }
                }
                else
                {
                    isConnected = false;
                }
            }
        }
    }
}
