# Virexaone FMS - Arsitektur Sistem & Cetak Biru (System Architecture Blueprint)

Virexaone adalah ekosistem Fleet Management System dan 3D Digital Twin pertambangan terpadu yang dibangun dengan arsitektur 3-Tier Enterprise:

`	ext
┌─────────────────────────────────────────────────────────────────────────┐
│                      VIREXAONE MONOREPO ECOSYSTEM                       │
└─────────────────────────────────────────────────────────────────────────┘
         │                                            │
         ▼                                            ▼
┌─────────────────────────┐                ┌─────────────────────────┐
│     FLUTTER IN-CABIN    │                │  UNITY 6 DIGITAL TWIN   │
│       (Mobile App)      │                │   (3D Desktop Client)   │
│  - Operator Navigation  │                │  - Real-Time GIS Pit    │
│  - Speed & Compass 3D   │                │  - Fleet 3D Tracking    │
│  - Smart Dispatch Alert │                │  - Heatmap & CCTV Cam   │
└───────────┬─────────────┘                └───────────┬─────────────┘
            │                                          │
            │ REST HTTP / JSON & WebSockets PTT Radio  │
            ▼                                          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    ASP.NET CORE 10.0 BACKEND SERVICE                    │
│    - /api/v1/fleet/live (Telemetri Real-time)                           │
│    - /api/v1/roads/network & /locations (GIS Spasial)                   │
│    - /api/v1/comms/messages & WebSocket TelemetryHub                    │
│    - GeoTransform: UTM EPSG 32650 <-> Unity 3D World Space              │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   POSTGRESQL 16 ENTERPRISE (DB_FMS)                     │
│  - Master Unit & Equipment Specs                                        │
│  - Live Telemetry Positions (GPS Easting, Northing, RL)                 │
│  - Haul Production, Cycle Time, & Geofence Logs                         │
└─────────────────────────────────────────────────────────────────────────┘
`

---

## 1. Modul In-Cabin Operator (Mobile/)
- **Teknologi**: Flutter 3.47 (Dart 3.13), Material 3 Dark FMS Theme.
- **Fitur Utama**:
  - CabinDashboardScreen: Tampilan instrumen kokpit kecepatan, ritasi, status muatan (*Payload*), dan status alat muat tujuan (*Assigned Shovel*).
  - Nav3dArrowPainter & GpsTrackPainter: Navigasi panah 3D dan plotting rute jalan tambang.
  - GpsVisualProjection: Estimasi interpolasi pergerakan halus (Dead-Reckoning) saat sinyal GPS terputus sesaat.
  - LiveCabinCommsService: PTT radio dua arah & pengiriman status breakdown/standby.

---

## 2. Modul Digital Twin 3D Control Room (desktop/)
- **Teknologi**: Unity 6 (6000.4.10f1), Universal Render Pipeline (URP), GLTFast Draco PBR.
- **Fitur Utama**:
  - RealMiningGISLoader: Rekonstruksi kontur topografi tambang aktual (5.45 km x 4.09 km) berbasis 16-bit LiDAR DEM & 4K GeoTIFF Orthophoto.
  - TerrainContourShader: Shader kustom anti-aliased contour line, TIN Wireframe CAD mode, dan Hypsometric Elevation Heatmap.
  - FMSFleetManager: Instansiasi model 3D kendaraan (CAT 777D/E, Komatsu HD785, PC2000 Shovel) dengan pergerakan lereng akurat dan rotasi heading real-time.
  - FMSUnitCctvManager: Integrasi multi-channel live streaming video kamera kabin dan blind-spot truk.

---

## 3. Modul Backend Core (ackend/)
- **Teknologi**: ASP.NET Core 10, C# 13, Npgsql Connection Pooling, SignalR.
- **Fitur Utama**:
  - High-throughput streaming (mendukung >10.000 concurrent connection).
  - Transformasi koordinat global ke lokal:
    \begin{aligned}
    X_{unity} &= \text{Easting} - 572728.3 \\
    Z_{unity} &= \text{Northing} - 113338.3 \\
    Y_{unity} &= \text{Elevation (RL)}
    \end{aligned}
