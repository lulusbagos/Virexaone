# Virexaone FMS - GIS Spatial & 3D Digital Twin

Dokumen ini menjelaskan alur pemrosesan data spasial geografis tambang dari format raw drone / satelit (GeoTIFF / DEM) hingga dirender secara interaktif di Unity 6 dan Flutter Mobile.

---

## 1. Parameter Spasial Pertambangan (Pit Unggul / Astha Site)

- **Sistem Proyeksi**: Universal Transverse Mercator (UTM) Zone 50N (EPSG: 32650)
- **Geographic Datum**: WGS 84 (Latitude: 1.022302° N, Longitude: 117.657014° E)
- **Pusat Referensi Unity (0, 0, 0)**:
  - Reference Easting: `572728.30 m`
  - Reference Northing: `113338.30 m`
  - Reference Elevation (Datum RL): `71.00 m ASL`
- **Dimensi Lapangan**:
  - Lebar Timur - Barat (X): `5.451,56 meter`
  - Panjang Utara - Selatan (Z): `4.086,95 meter`
  - Ketinggian Terendah - Tertinggi (Y): `71,02 m - 316,46 m ASL` (Rentang Vertikal: ~245,44 m)

---

## 2. Lapisan Visual GIS (Multi-Layer Rendering)

1. **4K High-Res Orthophoto Texture Layer**:
   - Sumber file: `geotiff_ortho_4k.jpg` / `PitUnggul_RealOrtho.png`.
   - Warna alami permukaan tambang (*Natural Surface*) tanpa artificial blowout.

2. **Screen-Space Anti-Aliased Contour Lines**:
   - Interval kontur minor: `10 meter`.
   - Interval kontur mayor (*Index Contour*): `50 meter`.
   - Dynamic Density Fade-out: Pada lereng yang sangat curam atau zoom-out tinggi, garis kontur otomatis meluruh halus (*fade to 0*) untuk mencegah garis putih bertumpuk.

3. **Triangulated Irregular Network (TIN) Mode**:
   - Wireframe TIN Mesh (ukuran grid 12m) dengan warna cyan / yellow.
   - Solid CAD Surface Mode: Rendering visual bergaya Surpac / Micromine untuk analisa desain jenjang pit (*Pit bench & berm*).

4. **Hypsometric DEM Elevation Heatmap**:
   - Gradien warna elevasi: Biru (Elevasi Rendah/Dasar Pit) ➔ Hijau ➔ Kuning ➔ Merah (Puncak Disposal/Highwall).

5. **Jaringan Jalan Tambang & Waypoint Vector**:
   - Poliline jalan 3D dengan elevasi offset `+0.6 meter` di atas medan jalan.
   - Papan nama penunjuk arah dinamis (*Billboard Markers*) yang selalu menghadap kamera.
