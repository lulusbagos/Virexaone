# Virexa In-Cabin & Control Room System Specification (v2.0)

## 1. Overview
Aplikasi **Virexa One** terbagi atas:
1. **Desktop Control Room (Unity 3D GIS)**: Pusat pemantauan armada tambang real-time, visualisasi terrain GeoTIFF, jaringan jalan hauling, dispatching, pelacakan CCTV unit, dan komunikasi dua arah.
2. **In-Cabin Mobile Dashboard (Flutter)**: Tablet/Mobile dashboard cockpit di dalam kabin operator dump truck dan excavator.
3. **FMS Backend Gateway (ASP.NET Core REST & WebSocket)**: Sinkronisasi telemetri GPS live, basis data rute/jalan tambang, dan server komunikasi pesan/PTT.

---

## 2. Fitur & Pembaruan Sistem (Terbaru)

### A. GPS Navigation & Motion Buffer (Mobile & Control Room)
- **Smooth Motion & Heading Buffer**: Menggunakan peredam interpolasi kuadratik (`Curves.easeOutQuad`) dan *shortest-angle wrapping* `(target - current + 540) % 360 - 180` pada koordinat Easting/Northing serta rotasi azimuth/bearing, mencegah patah (*jitter/snapping*) antar-detik sinyal GPS.
- **Geometri Jalan Hauling Nyata**: Peta Jejak GPS merender segmen nyata dari database `/api/v1/roads/network` (koordinat start/end dan lebar jalan aktual).
- **Badge Telemetri Unit Sekitar**: Menampilkan lingkaran blip radar lengkap dengan **Nama Unit (EX/HD/Support)** dan **Jarak Meter Real-Time** (mengatasi label kosong `' m'`).

### B. Komunikasi Dua Arah & Notifikasi PTT Kabin
- **Interactive Popup Alert**: Setiap pesan teks atau suara (PTT/mik) yang dikirim dari operator mobile langsung memicu *popup card* di Control Room dengan tombol aksi 1-klik:
  - `🎯 FOKUS / IKUTI UNIT (3D)`: Kamera melompat dan melacak unit 3D.
  - `📻 BALAS RADIO DISPATCH`: Membuka modal pesan dua arah.
  - `🎙️ TALKBACK SEKARANG`: Berbicara balik secara langsung.
- **Live Voice Waveform Overlay**: Menampilkan bilah status transmisi merah berdenyut `[📞 RADIO KABIN MASUK]` saat operator menekan mik.
- **Audio Chime Alert**: Memainkan suara peringatan di Control Room saat pesan/suara masuk.

### C. Smart Command Palette & Search (Control Room 3D)
- **Tombol Pencarian Top Navigation Bar**: `[🔍 Cari Unit / Lokasi (Ctrl+F)]` langsung di header atas.
- **Fokus & Auto-Enter**: Mendukung shortcut `Ctrl+F` / `Ctrl+K` / `F3`, pengetikan langsung nomor lambung (misal: `5048`, `RD5084`), dan tombol `[Enter]` untuk eksekusi instan.
- **Fix Font Rendering**: Menggunakan textfield IMGUI yang bersih dari error *TextEditor NullReferenceException*.
