# Virexaone FMS (Fleet Management System) - Konsep & Spesifikasi Operasional Tambang

Dokumen ini memuat seluruh cetak biru konsep operasional, bisnis proses, telemetri, dan logika dispatching untuk tambang terbuka batubara dan overburden (OB).

---

## 1. Siklus Kerja Hauling & Ritasi (Mining Cycle State Machine)

Setiap unit Hauler (Dump Truck) dan Loading Unit (Excavator / Shovel) beroperasi dalam siklus tertutup:

`	ext
[1. Traveling Empty] ──> [2. Queueing / Antri di Front] ──> [3. Spotting]
         ^                                                        │
         │                                                        v
[6. Return Empty] <── [5. Dumping di Disposal] <── [4. Hauling Loaded]
`

### Definisi Aktivitas Tambang:
1. **Traveling Empty (Activity ID 21)**: Unit bergerak dari Disposal / Workshop menuju Pit Front muat. Kecepatan rata-rata 25-40 km/jam.
2. **Queuing / Standby Front (Activity ID 22)**: Menunggu antrian di dekat radius Excavator (Call Point < 50m).
3. **Spotting & Loading (Activity ID 23)**: Truk memposisikan bak di bawah bucket excavator. Sensor payload mencatat pertambahan berat.
4. **Hauling Loaded (Activity ID 2)**: Truk bermuatan batubara/OB bergerak menuju Disposal / ROM Stockpile. Batas kecepatan aman maks 30 km/jam.
5. **Dumping (Activity ID 24)**: Manuver mundur dan penumpahan material di area disposal aktif.
6. **Delay / Breakdown / MTC (Activity ID 8 / 11)**: Status unit maintenance, refuel di fuel truck, atau operator rest.

---

## 2. Smart Dispatching System (LP - Linear Programming & Heuristic)

Sistem smart dispatch mengoptimalkan produktivitas alat gali-muat (Shovel) dan alat angkut (Truck):
- **Match Factor Optimization**: Menghitung rasio ideal  = \frac{N_{truck} \times T_{load}}{N_{shovel} \times T_{cycle}}$ agar antrian (queue time) truk dan waktu tunggu (idle time) excavator mendekati nol.
- **Dynamic Rerouting**: Jika terjadi bottleneck atau breakdown pada EX7001, sistem dispatch backend secara otomatis mengalihkan armada hauler terdekat ke EX7002 melalui notifikasi in-cabin Flutter.
- **Geofencing Event Trigger**: Deteksi otomatis masuk/keluar zona (Front, Disposal, Road Junction) berdasarkan koordinat UTM Easting/Northing.

---

## 3. Komunikasi Dua Arah & Push-To-Talk (In-Cabin Voice & Radio)

1. **Dispatcher Messaging**: Pengiriman instruksi teks darurat (*Emergency*), prioritas tinggi (*Urgent*), maupun broadcast ke seluruh armada kabin.
2. **Voice Synthesizer (Text-To-Speech)**: Membacakan perintah dispatch secara audio langsung di speaker kabin tanpa mengalihkan pandangan pengemudi.
3. **PTT Radio Talkback**: Streaming audio berlatensi rendah berbasis PCM WebSockets antara ruang kontrol (Control Room) dan kabin operator.

---

## 4. Telemetri & Sensor IoT Terintegrasi

| Parameter | Sumber Data | Ambang Batas / Satuan | Tindakan Sistem |
| :--- | :--- | :--- | :--- |
| **GPS Latency** | Modul GNSS RTK / NMEA | Update tiap 1-2 detik | Jika GPS > 20s: Visual projection dead-reckoning |
| **Speed Alert** | CANBus / GPS Speed | > 40 km/jam (Haul Road) | Peringatan audio kabin & catat audit overspeed |
| **Payload Ton** | Strut Pressure Sensor | Satuan Ton (e.g. 90-105 t) | Validasi beban overload / underload |
| **Fuel Level** | Sensor Tangki Bahan Bakar | Persentase % | Auto-schedule unit fuel truck refuel |
| **Tire Pressure/Temp** | Sensor TPMS | Bar / °C | Notifikasi peringatan dini panas ban (*Heat separation*) |
