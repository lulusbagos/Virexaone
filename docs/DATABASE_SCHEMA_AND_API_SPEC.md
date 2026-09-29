# Virexaone FMS - Spesifikasi API & Skema Database

## 1. RESTful API Endpoints (`/api/v1`)

| Method | Endpoint | Deskripsi | Respons Utama |
| :--- | :--- | :--- | :--- |
| `GET` | `/health` | Pemeriksaan kesehatan layanan & database | `{"status": "healthy"}` |
| `GET` | `/api/v1/ops/health` | Status operasional tambang & pool koneksi | `{"status": "healthy", "site": "astha"}` |
| `GET` | `/api/v1/fleet/live` | Data posisi & telemetri unit bergerak real-time | `List<FleetUnit>` (GPS, speed, heading, payload, status) |
| `GET` | `/api/v1/fleet/summary` | Ringkasan armada (Total, Hauling, Loading, Idle, MTC) | `FleetSummary` |
| `GET` | `/api/v1/dispatch/active` | Daftar assignment & antrian dispatch aktif | `List<DispatchAssignment>` |
| `GET` | `/api/v1/locations/all` | Data titik lokasi tambang (Disposal, Front, Callpoints) | `List<LocationItem>` |
| `GET` | `/api/v1/roads/network` | Poliline jaringan jalan tambang (Nodes & Waypoints) | `List<RoadItem>` |
| `GET` | `/api/v1/comms/messages` | Riwayat pesan instruksi kabin per unit | `List<CommsMessage>` |
| `POST`| `/api/v1/comms/messages` | Kirim instruksi dispatch / pesan radio ke kabin | `{"status": "sent", "message_id": "..."}` |
| `GET` | `/api/v1/weather/current`| Kondisi cuaca lokal tambang (Suhu, Hujan, Angin) | `WeatherData` |

---

## 2. Struktur Entitas Database Utama (`DB_FMS`)

### Tabel: `t_fms_equipment` (Master Armada Tambang)
- `equipment_id` (BIGINT, PK): ID unik peralatan tambang.
- `unit_name` (VARCHAR): Kode nomor lambung unit (contoh: RD5099, EX7001).
- `equipment_type_id` (INT): Kategori alat (Dump Truck, Excavator Shovel, Dozer, Grader, Fuel Truck).
- `site_id` (VARCHAR): Site pertambangan aktif (astha).
- `is_active` (BOOLEAN): Status keaktifan unit.

### Tabel: `t_fms_telemetry_live` (Posisi Real-Time)
- `equipment_id` (BIGINT, FK): Relasi ke armada.
- `latitude`, `longitude` (DOUBLE PRECISION): Koordinat geografis WGS84.
- `easting`, `northing` (DOUBLE PRECISION): Koordinat proyeksi UTM EPSG 32650.
- `elevation` (FLOAT): Ketinggian RL (Reduced Level / Meter di atas permukaan laut).
- `speed_kmh` (FLOAT): Kecepatan laju unit saat ini.
- `heading_deg` (FLOAT): Arah hadap kendaraan (0° - 360° True North).
- `last_heard` (TIMESTAMPTZ): Timestamp penerimaan data telemetri terakhir.

### Tabel: `t_fms_haul_records` (Catatan Ritasi & Produksi)
- `haul_id` (BIGINT, PK): ID unik siklus ritasi.
- `hauler_unit_id` (BIGINT, FK): Truk pengangkut.
- `shovel_unit_id` (BIGINT, FK): Excavator pemuat.
- `source_location_id` (BIGINT): Pit Loading Point.
- `dest_location_id` (BIGINT): Disposal / Dumping Point.
- `payload_tons` (FLOAT): Berat muatan tercatat.
- `cycle_start_time`, `load_time`, `dump_time`, `cycle_end_time` (TIMESTAMPTZ).
