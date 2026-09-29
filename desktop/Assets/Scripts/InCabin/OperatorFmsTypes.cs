using System;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Core Enumerations and Data Types for Mobile In-Cabin Equipment Dashboard.
    /// Fully self-contained without requiring Desktop 3D GIS dependencies.
    /// </summary>
    public enum UnitState
    {
        Idle,
        TravellingToLoad,   // Menuju front muat (kosong)
        QueueingAtPit,      // Antre di loading face
        Loading,            // Sedang dimuat excavator/shovel
        Hauling,            // Hauling bermuatan menuju disposal
        QueueingAtDump,     // Antre di disposal/hopper
        Dumping,            // Proses buang muatan
        Maintenance,        // Perbaikan / Breakdown
        Offline             // Mati / Tidak aktif
    }

    public enum UnitType
    {
        HaulTruck,
        Excavator,
        Bulldozer,
        Grader,
        FuelTruck,
        WaterTruck,
        WheelLoader
    }

    [System.Serializable]
    public class MobileTelemetryPacket
    {
        public string unitId;
        public string operatorNik;
        public string operatorName;
        public UnitState state;
        public float speedKmh;
        public float payloadTons;
        public float fuelLevelPercent;
        public int completedTrips;
        public float latitude;
        public float longitude;
        public float altitude;
        public float compassHeading;
        public string assignedLoaderId;
        public string assignedDisposal;
        public string timestampIso;
    }
}
