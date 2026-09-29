using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS
{
    public enum FtwStatus
    {
        FitToWork = 0,        // Boleh Bekerja (Hijau) - Jam Tidur >= 6.0 jam
        DalamPengawasan = 1,  // Dalam Pengawasan / Fatigue Alert (Kuning) - Jam Tidur 4.5 - 5.9 jam
        Unfit = 2             // Unfit / Wajib Istirahat (Merah) - Jam Tidur < 4.5 jam
    }

    [System.Serializable]
    public class FtwSaveraRecord
    {
        public string Unit;            // ID Unit (e.g. "DT5101", "EX-201", "DZ301")
        public string NIK;             // NIK Karyawan Operator (e.g. "NIK-882103")
        public string operator_name;   // Nama Operator
        public FtwStatus status_ftw;   // FitToWork, DalamPengawasan, Unfit
        public float jam_tidur;        // Total jam tidur (e.g. 7.5, 5.0, 4.0)
        public string warna;           // "Hijau", "Kuning", "Merah"
        public string hexColor;        // "#00FFA3", "#FFB800", "#FF4D4D"
        public string status_kerja;    // "Boleh Bekerja", "Dalam Pengawasan", "Wajib Istirahat"
        public string tensimeter;      // "120/80"
        public float suhu_tubuh;       // 36.5
        public int denyut_nadi;        // 74
        public string waktu_input;     // "05:45 WITA"
        public string catatan_medis;   // Catatan evaluasi dokter / pengawas K3
    }

    public class FMSFtwSaveraManager : MonoBehaviour
    {
        public static FMSFtwSaveraManager Instance { get; private set; }

        [Header("Tabel Database FTW SAVERA")]
        public Dictionary<string, FtwSaveraRecord> tbl_m_ftw_savera = new Dictionary<string, FtwSaveraRecord>(StringComparer.OrdinalIgnoreCase);

        [Header("Modal View State")]
        public bool isFtwModalOpen = false;
        public string searchFilter = "";
        public int statusFilterIndex = 0; // 0: Semua, 1: Fit, 2: Pengawasan, 3: Unfit
        private Vector2 tableScrollPos = Vector2.zero;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

        }

        public void InitializeDummyFtwDatabase()
        {
            tbl_m_ftw_savera.Clear();

            // Preset list of realistic operator names & base NIKs
            string[] operatorPool = new string[]
            {
                "Ahmad Supardi", "Bambang H.", "Candra Wijaya", "Dedi Kurniawan", "Eko Prasetyo",
                "Fajar Nugroho", "Gunawan S.", "Hendra Setiawan", "Indra Gunawan", "Joko Susilo",
                "Kurniawan D.", "Lukman Hakim", "Muhamad Rizki", "Nur Hidayat", "Oki Pratama",
                "Panji Asmoro", "Rian Hidayat", "Surya Saputra", "Teguh Santoso", "Wahyu Hidayat",
                "Yudi Permana", "Zainal Abidin", "Agus Salim", "Bayu Pratama", "Denny Sumargo"
            };

            // Generate realistic dummy entries for key unit IDs
            string[] sampleUnits = new string[]
            {
                "EX-201", "EX-202", "EX-203", "EX-204", "EX-205",
                "DT5101", "DT5102", "DT5103", "DT5104", "DT5105", "DT5106", "DT5107", "DT5108", "DT5109", "DT5110",
                "DT5111", "DT5112", "DT5113", "DT5114", "DT5115", "DT5116", "DT5117", "DT5118", "DT5119", "DT5120",
                "RD5091", "RD5092", "RD5093", "RD5094", "RD5095", "RD5133", "RD5134", "RD5135", "RD5136", "RD5137",
                "DZ301", "DZ302", "DZ303", "GD401", "GD402", "FT601", "FT602", "WL101", "WL102"
            };

            for (int i = 0; i < sampleUnits.Length; i++)
            {
                string uId = sampleUnits[i];
                GenerateRecordForUnit(uId, operatorPool[i % operatorPool.Length], i);
            }
        }

        private FtwSaveraRecord GenerateRecordForUnit(string uId, string opName, int seed)
        {
            int hash = Mathf.Abs(uId.GetHashCode() + seed * 37);
            int nikNum = 108000 + (hash % 8999);
            string nik = $"NIK-{nikNum}";

            // Determine Sleep Hours and FTW Status:
            // ~75% Fit (6.2 - 8.5 jam), ~20% Dalam Pengawasan (4.5 - 5.8 jam), ~5% Unfit (< 4.5 jam)
            int categoryRoll = hash % 100;
            float jamTidur;
            FtwStatus st;
            string warna;
            string hexCol;
            string stKerja;
            string tensi;
            string catatan;

            if (categoryRoll < 75)
            {
                // FIT TO WORK (Hijau)
                jamTidur = 6.0f + (hash % 26) * 0.1f; // 6.0 - 8.5 Jam
                st = FtwStatus.FitToWork;
                warna = "Hijau";
                hexCol = "#00FFA3";
                stKerja = "Boleh Bekerja (Fit)";
                tensi = $"{(115 + (hash % 10))}/{(75 + (hash % 8))}";
                catatan = "Kondisi fisik prima, tensi stabil & jam tidur terpenuhi.";
            }
            else if (categoryRoll < 93)
            {
                // DALAM PENGAWASAN (Kuning) - Fatigue Alert
                jamTidur = 4.5f + (hash % 14) * 0.1f; // 4.5 - 5.8 Jam
                st = FtwStatus.DalamPengawasan;
                warna = "Kuning";
                hexCol = "#FFB800";
                stKerja = "Dalam Pengawasan (Fatigue Warning)";
                tensi = $"{(125 + (hash % 12))}/{(82 + (hash % 8))}";
                catatan = $"Jam tidur {jamTidur:F1}j. Diberikan suplemen & wajib observasi kelelahan per 2 jam.";
            }
            else
            {
                // UNFIT (Merah) - Istirahat Wajib
                jamTidur = 3.2f + (hash % 12) * 0.1f; // 3.2 - 4.3 Jam
                st = FtwStatus.Unfit;
                warna = "Merah";
                hexCol = "#FF4D4D";
                stKerja = "Unfit (Wajib Istirahat / Evaluasi Medis)";
                tensi = $"{(138 + (hash % 15))}/{(90 + (hash % 8))}";
                catatan = $"Kurang tidur kritis ({jamTidur:F1}j). Dilarang mengoperasikan unit sebelum istirahat.";
            }

            int inMin = (hash % 45);
            string inTime = $"05:{inMin:D2} WITA";
            float temp = 36.2f + (hash % 7) * 0.1f;
            int pulse = 70 + (hash % 16);

            FtwSaveraRecord rec = new FtwSaveraRecord()
            {
                Unit = uId,
                NIK = nik,
                operator_name = opName,
                status_ftw = st,
                jam_tidur = jamTidur,
                warna = warna,
                hexColor = hexCol,
                status_kerja = stKerja,
                tensimeter = tensi,
                suhu_tubuh = temp,
                denyut_nadi = pulse,
                waktu_input = inTime,
                catatan_medis = catatan
            };

            tbl_m_ftw_savera[uId] = rec;
            return rec;
        }

        public FtwSaveraRecord GetFtwRecord(string unitId, string defaultOpName = "")
        {
            if (string.IsNullOrEmpty(unitId)) return null;

            if (tbl_m_ftw_savera.TryGetValue(unitId, out FtwSaveraRecord rec))
            {
                return rec;
            }

            return null;
        }

        /// <summary>
        /// Generates concise badge string for overhead 3D tag:
        /// e.g. "🟢 FIT 💤 7.5j" or "🟡 PENGAWASAN 💤 5.0j"
        /// </summary>
        public string GetFtwTagSnippet(string unitId, bool showSleep, string opName = "")
        {
            var rec = GetFtwRecord(unitId, opName);
            if (rec == null) return "";

            string badge;
            switch (rec.status_ftw)
            {
                case FtwStatus.FitToWork:
                    badge = "<color=#00FFA3>🟢 FIT</color>";
                    break;
                case FtwStatus.DalamPengawasan:
                    badge = "<color=#FFB800>🟡 WASPADA</color>";
                    break;
                case FtwStatus.Unfit:
                default:
                    badge = "<color=#FF4D4D>🔴 UNFIT</color>";
                    break;
            }

            if (showSleep)
            {
                string sleepColor = rec.status_ftw == FtwStatus.FitToWork ? "#00FFA3" : (rec.status_ftw == FtwStatus.DalamPengawasan ? "#FFB800" : "#FF6666");
                return $"{badge} <color={sleepColor}>💤 {rec.jam_tidur:F1}j</color>";
            }

            return badge;
        }

        public void GetFtwFleetSummary(out int totalUnits, out int totalFit, out int totalPengawasan, out int totalUnfit, out float avgSleepHours)
        {
            totalUnits = tbl_m_ftw_savera.Count;
            totalFit = 0;
            totalPengawasan = 0;
            totalUnfit = 0;
            float sumSleep = 0f;

            foreach (var kvp in tbl_m_ftw_savera)
            {
                var r = kvp.Value;
                sumSleep += r.jam_tidur;
                if (r.status_ftw == FtwStatus.FitToWork) totalFit++;
                else if (r.status_ftw == FtwStatus.DalamPengawasan) totalPengawasan++;
                else totalUnfit++;
            }

            avgSleepHours = totalUnits > 0 ? (sumSleep / totalUnits) : 7.0f;
        }

        // =========================================================================
        // FULL FTW & SAVERA OPERATOR FATIGUE MONITORING MODAL
        // =========================================================================
        public void DrawFtwSaveraModal(float screenW, float screenH, GUIStyle cardStyle, GUIStyle headerStyle, GUIStyle hintStyle, GUIStyle panelStyle, GUIStyle navBtnStyle, GUIStyle activeBtnStyle, Texture2D accentTex)
        {
            if (!isFtwModalOpen) return;

            float modalW = Mathf.Min(980f, screenW - 40f);
            float modalH = Mathf.Min(650f, screenH - 50f);
            float x = (screenW - modalW) / 2f;
            float y = (screenH - modalH) / 2f;

            // Semi-transparent backdrop
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none, cardStyle);

            // Modal Card Box
            GUI.Box(new Rect(x, y, modalW, modalH), GUIContent.none, cardStyle);
            if (accentTex != null)
            {
                GUI.DrawTexture(new Rect(x + 2, y + 2, modalW - 4, 2), accentTex);
            }

            // Top Header Title & Close Button
            GUI.Label(new Rect(x + 20, y + 14, modalW - 60, 24), 
                "🩺 <b>SISTEM KESELAMATAN & EVALUASI OPERATOR: <color=#00FFA3>SAVERA / FTW (FIT TO WORK)</color></b>", headerStyle);

            if (GUI.Button(new Rect(x + modalW - 36, y + 14, 24, 22), "✕", navBtnStyle))
            {
                isFtwModalOpen = false;
                return;
            }

            GUI.Label(new Rect(x + 20, y + 38, modalW - 40, 18), 
                "Data FTW belum terhubung ke sumber operasional.", hintStyle);

            if (tbl_m_ftw_savera.Count == 0)
            {
                GUI.Label(new Rect(x + 20, y + 100, modalW - 40, 32),
                    "Belum ada catatan FTW terverifikasi untuk ditampilkan.", hintStyle);
                return;
            }

            float curY = y + 62;

            // 4 KPI GAUGE CARDS (Fit, Pengawasan, Unfit, Rata-rata Tidur)
            GetFtwFleetSummary(out int totalU, out int fitCount, out int pengawasanCount, out int unfitCount, out float avgSleep);
            float kpiColW = (modalW - 58) / 4f;
            float kpiH = 70f;

            // KPI 1: Fit to Work (Boleh Bekerja)
            GUI.Box(new Rect(x + 20, curY, kpiColW, kpiH), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 28, curY + 6, kpiColW - 16, 16), "🟢 <b>FIT TO WORK (BOLEH KERJA)</b>", hintStyle);
            GUI.Label(new Rect(x + 28, curY + 24, kpiColW - 16, 24), $"<color=#00FFA3><size=17><b>{fitCount} Unit</b></size></color> ({((float)fitCount / Mathf.Max(1, totalU) * 100f):F0}%)", hintStyle);
            GUI.Label(new Rect(x + 28, curY + 48, kpiColW - 16, 16), "Jam Tidur ≥ 6.0 Jam (Normal)", hintStyle);

            // KPI 2: Dalam Pengawasan (Fatigue Warning)
            GUI.Box(new Rect(x + 26 + kpiColW, curY, kpiColW, kpiH), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 6, kpiColW - 16, 16), "🟡 <b>DALAM PENGAWASAN (FATIGUE)</b>", hintStyle);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 24, kpiColW - 16, 24), $"<color=#FFB800><size=17><b>{pengawasanCount} Unit</b></size></color> ({((float)pengawasanCount / Mathf.Max(1, totalU) * 100f):F0}%)", hintStyle);
            GUI.Label(new Rect(x + 34 + kpiColW, curY + 48, kpiColW - 16, 16), "Jam Tidur 4.5 - 5.9 Jam (Observasi)", hintStyle);

            // KPI 3: Unfit / Wajib Istirahat
            GUI.Box(new Rect(x + 32 + kpiColW * 2, curY, kpiColW, kpiH), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 6, kpiColW - 16, 16), "🔴 <b>UNFIT (WAJIB ISTIRAHAT)</b>", hintStyle);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 24, kpiColW - 16, 24), $"<color=#FF4D4D><size=17><b>{unfitCount} Unit</b></size></color> ({((float)unfitCount / Mathf.Max(1, totalU) * 100f):F0}%)", hintStyle);
            GUI.Label(new Rect(x + 40 + kpiColW * 2, curY + 48, kpiColW - 16, 16), "Jam Tidur < 4.5 Jam / Re-evaluasi", hintStyle);

            // KPI 4: Rata-rata Jam Tidur
            GUI.Box(new Rect(x + 38 + kpiColW * 3, curY, kpiColW, kpiH), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 6, kpiColW - 16, 16), "💤 <b>RATA-RATA JAM TIDUR</b>", hintStyle);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 24, kpiColW - 16, 24), $"<color=#00E5FF><size=17><b>{avgSleep:F1} Jam</b></size></color> / Operator", hintStyle);
            GUI.Label(new Rect(x + 46 + kpiColW * 3, curY + 48, kpiColW - 16, 16), "Target K3 Standar: ≥ 7.0 Jam", hintStyle);

            curY += kpiH + 10;

            // Search Filter & Category Tabs
            GUI.Label(new Rect(x + 20, curY + 4, 80, 20), "🔍 Cari Data:", hintStyle);
            searchFilter = GUI.TextField(new Rect(x + 100, curY + 2, 220, 24), searchFilter ?? "");

            // Status Filter Tabs
            float tabX = x + 340;
            string[] tabs = new string[] { "Semua", $"🟢 Fit ({fitCount})", $"🟡 Waspada ({pengawasanCount})", $"🔴 Unfit ({unfitCount})" };
            for (int t = 0; t < tabs.Length; t++)
            {
                bool isCur = statusFilterIndex == t;
                if (GUI.Button(new Rect(tabX, curY + 2, 110, 24), tabs[t], isCur ? activeBtnStyle : navBtnStyle))
                {
                    statusFilterIndex = t;
                }
                tabX += 116;
            }

            curY += 32;

            // Table Header Row
            float tableW = modalW - 40;
            float thH = 26f;
            GUI.Box(new Rect(x + 20, curY, tableW, thH), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 26, curY + 4, 30, 18), "<b>NO</b>", hintStyle);
            GUI.Label(new Rect(x + 60, curY + 4, 75, 18), "<b>UNIT</b>", hintStyle);
            GUI.Label(new Rect(x + 140, curY + 4, 95, 18), "<b>NIK</b>", hintStyle);
            GUI.Label(new Rect(x + 240, curY + 4, 120, 18), "<b>OPERATOR</b>", hintStyle);
            GUI.Label(new Rect(x + 365, curY + 4, 120, 18), "<b>STATUS FTW</b>", hintStyle);
            GUI.Label(new Rect(x + 490, curY + 4, 80, 18), "<b>JAM TIDUR</b>", hintStyle);
            GUI.Label(new Rect(x + 575, curY + 4, 65, 18), "<b>WARNA</b>", hintStyle);
            GUI.Label(new Rect(x + 645, curY + 4, 110, 18), "<b>TENSI / SUHU</b>", hintStyle);
            GUI.Label(new Rect(x + 760, curY + 4, 180, 18), "<b>AKSI KAMERA / CCTV</b>", hintStyle);

            curY += thH + 4;

            // Filter List
            List<FtwSaveraRecord> displayList = new List<FtwSaveraRecord>();
            string sLow = (searchFilter ?? "").ToLower().Trim();

            foreach (var kvp in tbl_m_ftw_savera)
            {
                var r = kvp.Value;
                if (statusFilterIndex == 1 && r.status_ftw != FtwStatus.FitToWork) continue;
                if (statusFilterIndex == 2 && r.status_ftw != FtwStatus.DalamPengawasan) continue;
                if (statusFilterIndex == 3 && r.status_ftw != FtwStatus.Unfit) continue;

                if (!string.IsNullOrEmpty(sLow))
                {
                    if (!r.Unit.ToLower().Contains(sLow) &&
                        !r.NIK.ToLower().Contains(sLow) &&
                        !r.operator_name.ToLower().Contains(sLow) &&
                        !r.status_kerja.ToLower().Contains(sLow))
                    {
                        continue;
                    }
                }
                displayList.Add(r);
            }

            // Scrollable Rows
            float listH = (y + modalH - 52) - curY;
            float totalContentH = displayList.Count * 36f + 10f;
            Rect scrollArea = new Rect(x + 20, curY, tableW, listH);
            Rect viewArea = new Rect(0, 0, tableW - 20, Mathf.Max(listH, totalContentH));

            tableScrollPos = GUI.BeginScrollView(scrollArea, tableScrollPos, viewArea);
            float rowY = 2f;

            for (int i = 0; i < displayList.Count; i++)
            {
                var row = displayList[i];
                if (row == null) continue;

                if (i % 2 == 0)
                {
                    GUI.Box(new Rect(0, rowY, viewArea.width, 32), GUIContent.none, panelStyle);
                }

                GUI.Label(new Rect(6, rowY + 6, 30, 18), $"#{i + 1}", hintStyle);
                GUI.Label(new Rect(40, rowY + 6, 75, 18), $"🚚 <b>{row.Unit}</b>", hintStyle);
                GUI.Label(new Rect(120, rowY + 6, 95, 18), $"<color=#00E5FF>{row.NIK}</color>", hintStyle);
                GUI.Label(new Rect(220, rowY + 6, 120, 18), row.operator_name, hintStyle);

                // Status FTW badge with color
                string stLabel = row.status_ftw switch
                {
                    FtwStatus.FitToWork => "<color=#00FFA3>🟢 FIT TO WORK</color>",
                    FtwStatus.DalamPengawasan => "<color=#FFB800>🟡 PENGAWASAN</color>",
                    _ => "<color=#FF4D4D>🔴 UNFIT / STOP</color>"
                };
                GUI.Label(new Rect(345, rowY + 6, 120, 18), stLabel, hintStyle);

                // Jam Tidur
                string sleepCol = row.jam_tidur >= 6.0f ? "#00FFA3" : (row.jam_tidur >= 4.5f ? "#FFB800" : "#FF4D4D");
                GUI.Label(new Rect(470, rowY + 6, 80, 18), $"<color={sleepCol}><b>💤 {row.jam_tidur:F1} Jam</b></color>", hintStyle);

                // Warna
                GUI.Label(new Rect(555, rowY + 6, 65, 18), $"<color={row.hexColor}><b>{row.warna}</b></color>", hintStyle);

                // Tensi & Suhu
                GUI.Label(new Rect(625, rowY + 6, 110, 18), $"{row.tensimeter} | {row.suhu_tubuh:F1}°C", hintStyle);

                // Actions (Fokus, Kabin, CCTV)
                if (GUI.Button(new Rect(740, rowY + 3, 50, 24), "🎯 3D", navBtnStyle))
                {
                    var unitCtrl = FMSFleetManager.Instance?.activeFleet.Find(u => u.unitId.Equals(row.Unit, StringComparison.OrdinalIgnoreCase));
                    if (unitCtrl != null)
                    {
                        FMSFleetManager.Instance.SelectUnit(unitCtrl);
                        FMSCameraController.Instance?.JumpTo(unitCtrl.transform.position, 120f);
                        isFtwModalOpen = false;
                    }
                }

                if (GUI.Button(new Rect(796, rowY + 3, 56, 24), "🪟 POV", navBtnStyle))
                {
                    var unitCtrl = FMSFleetManager.Instance?.activeFleet.Find(u => u.unitId.Equals(row.Unit, StringComparison.OrdinalIgnoreCase));
                    if (unitCtrl != null)
                    {
                        FMSFleetManager.Instance.SelectUnit(unitCtrl);
                        FMSCameraController.Instance?.SetFollowMode(CameraFollowMode.CockpitPOV, unitCtrl.transform);
                        isFtwModalOpen = false;
                    }
                }

                if (GUI.Button(new Rect(858, rowY + 3, 56, 24), "📹 Cam", activeBtnStyle))
                {
                    var unitCtrl = FMSFleetManager.Instance?.activeFleet.Find(u => u.unitId.Equals(row.Unit, StringComparison.OrdinalIgnoreCase));
                    if (unitCtrl != null)
                    {
                        FMSDashboardUI.Instance?.EnsureUnitCctvManager();
                        FMSUnitCctvManager.Instance?.OpenCctv(unitCtrl);
                        isFtwModalOpen = false;
                    }
                }

                rowY += 36f;
            }

            GUI.EndScrollView();

            // Footer
            float btmY = y + modalH - 42;
            GUI.Label(new Rect(x + 20, btmY + 6, 400, 20), 
                $"Total Terdaftar: <b>{displayList.Count} Operator</b> | Sinkronisasi K3 Savera Terhubung", hintStyle);

            if (GUI.Button(new Rect(x + modalW - 120, btmY, 100, 28), "Tutup [ESC]", activeBtnStyle))
            {
                isFtwModalOpen = false;
            }
        }
    }
}
