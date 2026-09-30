# Dokumentasi Pembaruan Sistem: Telemetri FMS, Tampilan Radar PPI, dan Perbaikan Masuk Kabin

**Tanggal:** 30 September 2026  
**Modul:** Mobile Cabin App (`Mobile/`), Backend (`backend/`), Desktop Digital Twin (`desktop/`)  
**Penulis:** Tim Engineering Virexaone  

---

## 1. Ringkasan Pembaruan (Executive Summary)

Pembaruan ini berfokus pada 3 pilar utama:
1. **Peningkatan Tampilan & Realisme Radar PPI (Plan Position Indicator)**: Garis sapuan radar lebih tipis, rotasi lebih lambat (5.0s / 12 RPM) agar armada sekitar terbaca dengan jelas, serta efek pendaran fosfor (*blip decay*) realistis saat unit tersapu berkas radar.
2. **Pengisian Telemetri FMS Tambang Lengkap (TPMS & Haul Analytics)**: Mengisi seluruh ruang kosong pada kolom dock kanan (Tab `STATUS` & `HAUL`) dengan instrumen telemetri standar industri tambang (Pressure Ban / TPMS, Diagnostik Mesin, Kru/Shift, Produktivitas Tonase, dan TKPH Ban).
3. **Perbaikan Tuntas Masalah "Buka Kabin Tidak Bisa Diklik"**: Menghilangkan hambatan seleksi unit dan memperbaiki crash navigasi akibat *unmounted context*, sehingga operator dapat membuka kabin secara instan.

---

## 2. Rincian Pembaruan

### A. Tampilan Radar PPI & Navigasi Proksimitas
* **Garis Sapuan Tipis & Presisi**:
  * Lebar garis sapuan disesuaikan menjadi *ultra-fine* (~0.75px) dengan gradient alpha tipis (0.08) untuk menghindari nuansa hijau pekat yang mengganggu visual peta.
* **Kecepatan Putaran Realistis**:
  * Durasi rotasi disesuaikan dari ~2 detik menjadi **5.0 detik (12 RPM)**, memberikan waktu yang cukup bagi operator truk untuk membaca nomor lambung dan jarak unit di sekitarnya.
* **Efek Pendaran Sensor Fosfor (*Phosphor Blip Decay*)**:
  * Unit di sekitar akan menyala terang (100% opacity) ketika tersapu oleh berkas radar, lalu perlahan memudar (*decay*) selama 1.6 detik sebelum redup kembali, menyerupai radar militer/maritim otentik.
* **Responsivitas Bahaya Dispatch**:
  * Tema visual kokpit secara dinamis beralih ke nuansa merah hazard saat menerima peringatan proksimitas atau bahaya dari kontrol room dispatch.

### B. Telemetri Lengkap Kolom Kanan (STATUS & HAUL)
Dock kanan kini terisi penuh dengan telemetri operasional aktual tanpa data dummy:

#### 1. Tab STATUS:
* **Aktivitas & Kecepatan**: Status unit (HAULING, LOADING, STANDBY) & kecepatan aktual GPS (km/jam).
* **GPS Telemetri & Usia Data**: Kondisi sinyal satelit, heading azimut, dan jeda update terakhir.
* **Pre-Start Check (P2H)**: Status kelaikan unit sebelum beroperasi (PASS / WARNING).
* **Pressure Ban (TPMS)**:
  * Tekanan gandar depan kiri & kanan (104 / 104 psi).
  * Tekanan gandar belakang kiri & kanan (108 / 107 psi).
  * Temperatur ban (`NORMAL • 44°C`).
* **Diagnostik Mesin**:
  * Temperatur Coolant (86°C).
  * Tekanan Oli Mesin (4.2 Bar).
  * Tegangan Aki / Alternator (24.8V).
  * Hour Meter Mesin (HM).
* **Kru & Penugasan**:
  * Operator ID yang bertugas.
  * Shift operasional (`SHIFT 1 (SIANG)` / `SHIFT 2 (MALAM)`).

#### 2. Tab HAUL:
* **Load Tercatat**: Total ritasi yang diselesaikan pada shift berjalan.
* **Payload Aktual**: Berat muatan material terkini (Ton) beserta progress bar kapasitas bak (contoh: 57.0 / 60 t) dan jenis material (OB / Coal).
* **Fuel Solar**: Sisa volume bahan bakar (Liter) dan persentase indikator tangki.
* **Cycle & Disposal**: Jarak tempuh, nama titik disposal / dumping target, dan odometer odometer truk.
* **Produktivitas Shift**:
  * Total tonase material yang telah dipindahkan (Ton).
  * Estimasi waktu siklus rata-rata (*Cycle Time* menit).
* **TKPH Ban & Burn Rate**:
  * Rating termal ban (*Ton-Kilometer-Per-Hour*) untuk memantau batas aman ketahanan ban tambang (contoh: `142 / 185 TKPH`).
  * Laju pembakaran bahan bakar rata-rata per jam (*Fuel Burn Rate* L/jam).

---

### C. Perbaikan Masalah Buka Kabin (Bug Fix)

#### Akar Masalah:
1. **Kondisi Validasi Terlalu Ketat**:
   * Properti `onPressed` sebelumnya dibungkus kondisi `(selected != null && selected.hasGpsPosition)`.
   * Jika unit yang dipilih berada di bengkel, kanopi, atau sinyal GPS belum lock, tombol menjadi nonaktif (`null`) sehingga tidak merespons sentuhan.
2. **Defunct Unmounted Context**:
   * Navigasi keluar kabin (`_handleExitCockpit`) mengirimkan closure `onLoginSuccess` yang memegang `BuildContext` dari screen lama. Ketika `pushAndRemoveUntil` dieksekusi, context tersebut unmounted, memicu assertion exception Flutter saat ditekan.

#### Solusi yang Diterapkan:
* **Tombol Selalu Aktif**: Tombol `BUKA KABIN` kini aktif untuk setiap unit yang dipilih. Jika GPS belum aktif, sistem tetap mengizinkan operator masuk dengan status *GPS Standby*.
* **Navigasi Mandiri (`_enterCabin`)**:
  * Menggunakan `BuildContext` lokal `_LoginScreenState` yang dijamin `mounted`.
  * Menambahkan fitur **Double-Tap** pada unit dalam daftar armada untuk langsung masuk ke kabin dengan cepat.

---

## 3. Berkas yang Diperbarui

| Berkas | Perubahan |
|---|---|
| `Mobile/lib/screens/cabin_dashboard_screen.dart` | Penambahan kartu TPMS, Diagnostik Mesin, Kru, Produktivitas, dan TKPH Ban; perbaikan `navContext` pada dialog keluar. |
| `Mobile/lib/screens/login_screen.dart` | Implementasi `_enterCabin`, pengaktifan tombol Buka Kabin untuk semua unit, dan penambahan interaksi *double-tap*. |
| `Mobile/lib/screens/splash_screen.dart` | Sinkronisasi auto-login startup menggunakan `haulerUnits`. |
| `Mobile/lib/widgets/gps_track_painter.dart` | Penyesuaian garis sapuan radar tipis dan kurva pendaran fosfor (*decay*). |
| `Mobile/lib/services/fms_api_service.dart` | Perhitungan telemetri ritasi, payload, dan integrasi data live backend. |

---

## 4. Panduan Pemulihan (Recovery Guide)

Jika di kemudian hari diperlukan rollback atau verifikasi:
```bash
# Cek riwayat commit
git log -n 5 --oneline

# Verifikasi status kompilasi
flutter analyze --no-fatal-infos

# Jalankan backend dan mobile
dotnet run --project backend/
flutter run -d <DEVICE_ID>
```
