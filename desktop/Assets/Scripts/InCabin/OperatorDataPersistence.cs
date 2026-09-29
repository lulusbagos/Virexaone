using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Offline-First Persistent Storage Engine for Mobile In-Cabin Dashboard.
    /// Stores completed trips, delays, and telemetry logs locally on Android storage (PlayerPrefs / JSON Cache)
    /// and automatically syncs to PostgreSQL Backend once network connection is restored.
    /// </summary>
    public class OperatorDataPersistence : MonoBehaviour
    {
        public static OperatorDataPersistence Instance { get; private set; }

        private const string PREF_KEY_UNSYNCED_TRIPS = "Virexa_Unsynced_Trips";
        private const string PREF_KEY_OPERATOR_NIK = "Virexa_Saved_NIK";
        private const string PREF_KEY_OPERATOR_NAME = "Virexa_Saved_Name";
        private const string PREF_KEY_SAVED_UNIT = "Virexa_Saved_Unit";

        [System.Serializable]
        public class UnsyncedTripRecord
        {
            public string tripId;
            public string unitId;
            public string operatorNik;
            public string loaderId;
            public string frontName;
            public string disposalName;
            public float tonnage;
            public string timestampIso;
            public bool isSynced;
        }

        [System.Serializable]
        private class TripRecordListWrapper
        {
            public List<UnsyncedTripRecord> records = new List<UnsyncedTripRecord>();
        }

        [Header("Local Buffer Status")]
        public List<UnsyncedTripRecord> pendingSyncTrips = new List<UnsyncedTripRecord>();
        public int totalLocalSavedTrips = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            LoadLocalBuffer();
        }

        public void SaveOperatorCredentials(string nik, string name, string unitId)
        {
            PlayerPrefs.SetString(PREF_KEY_OPERATOR_NIK, nik);
            PlayerPrefs.SetString(PREF_KEY_OPERATOR_NAME, name);
            PlayerPrefs.SetString(PREF_KEY_SAVED_UNIT, unitId);
            PlayerPrefs.Save();
        }

        public (string nik, string name, string unitId) GetSavedCredentials()
        {
            string nik = PlayerPrefs.GetString(PREF_KEY_OPERATOR_NIK, "OP-98241");
            string name = PlayerPrefs.GetString(PREF_KEY_OPERATOR_NAME, "Budi Pratama");
            string unitId = PlayerPrefs.GetString(PREF_KEY_SAVED_UNIT, "RD5104");
            return (nik, name, unitId);
        }

        public void RecordLocalTrip(string unitId, string nik, string loaderId, string frontName, string disposalName, float tonnage)
        {
            UnsyncedTripRecord trip = new UnsyncedTripRecord
            {
                tripId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                unitId = unitId,
                operatorNik = nik,
                loaderId = loaderId,
                frontName = frontName,
                disposalName = disposalName,
                tonnage = tonnage,
                timestampIso = DateTime.UtcNow.ToString("o"),
                isSynced = false
            };

            pendingSyncTrips.Add(trip);
            totalLocalSavedTrips++;
            PersistBuffer();
        }

        public void MarkTripsSynced()
        {
            pendingSyncTrips.Clear();
            PersistBuffer();
        }

        private void PersistBuffer()
        {
            TripRecordListWrapper wrapper = new TripRecordListWrapper { records = pendingSyncTrips };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(PREF_KEY_UNSYNCED_TRIPS, json);
            PlayerPrefs.Save();
        }

        private void LoadLocalBuffer()
        {
            if (PlayerPrefs.HasKey(PREF_KEY_UNSYNCED_TRIPS))
            {
                string json = PlayerPrefs.GetString(PREF_KEY_UNSYNCED_TRIPS, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        TripRecordListWrapper wrapper = JsonUtility.FromJson<TripRecordListWrapper>(json);
                        if (wrapper != null && wrapper.records != null)
                        {
                            pendingSyncTrips = wrapper.records;
                            totalLocalSavedTrips = pendingSyncTrips.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VirexaDataPersistence] Error loading local buffer: {ex.Message}");
                    }
                }
            }
        }
    }
}
