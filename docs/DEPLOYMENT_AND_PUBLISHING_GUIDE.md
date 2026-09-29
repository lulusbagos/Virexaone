# Virexaone FMS - Panduan Publishing & Deployment

Dokumen ini memuat instruksi langkah demi langkah untuk mem-publish dan mendeploy ketiga modul Virexaone ke lingkungan produksi.

---

## 1. Backend Deployment (Windows Server / Linux)

### Build & Publish:
```powershell
cd "D:\4. PROJECT\20. Astha\Virexaone\backend"
dotnet publish -c Release -r win-x64 --self-contained false -o "..\publish\backend"
```

### Konfigurasi Service:
Pastikan file `appsettings.Local.json` di folder publish telah memuat kredensial database target:
```json
{
  "FmsSettings": {
    "DbHost": "172.16.1.96",
    "DbName": "DB_FMS",
    "DbUser": "postgres",
    "DbPassword": "YOUR_PASSWORD"
  },
  "CabinComms": {
    "Enabled": true,
    "DispatcherKey": "astha-production-dispatcher-key"
  }
}
```

### Menjalankan sebagai Windows Service:
```powershell
New-Service -Name "VirexaoneBackend" -BinaryPathName "D:\4. PROJECT\20. Astha\Virexaone\publish\backend\Virexaone.FMS.Backend.exe" -StartupType Automatic
Start-Service "VirexaoneBackend"
```

---

## 2. Mobile In-Cabin Publishing (Android APK / AAB)

### Build Debug / Release APK:
```powershell
cd "D:\4. PROJECT\20. Astha\Virexaone\Mobile"
flutter build apk --release
```
Output binary: `Mobile/build/app/outputs/flutter-apk/app-release.apk`

### Pemasangan ke Tablet Kabin via ADB:
```powershell
adb install -r "build/app/outputs/flutter-apk/app-release.apk"
```

---

## 3. Desktop Digital Twin 3D Publishing (Unity 6 Standalone / WebGL)

### Build Windows Standalone (.exe):
1. Buka Unity Hub dan tambahkan folder `D:\4. PROJECT\20. Astha\Virexaone\desktop`.
2. Masuk ke **File > Build Profiles / Build Settings**.
3. Pilih platform **Windows, Mac, Linux (x86_64)**.
4. Target Output: `D:\4. PROJECT\20. Astha\Virexaone\publish\desktop\Virexaone_FMS_3D.exe`.
5. Klik **Build**.

### Build WebGL (Browser Control Room):
1. Pilih platform **WebGL**.
2. Template: **VirexaOne WebGL Template**.
3. Target Output: `D:\4. PROJECT\20. Astha\Virexaone\publish\webgl\`.
