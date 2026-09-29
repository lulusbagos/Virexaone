using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    public class FMSMtcPanel : MonoBehaviour
    {
        [Serializable] private class MtcSnapshot
        {
            public string status;
            public string generated_at;
            public int gps_fresh_seconds;
            public MtcData data;
        }

        [Serializable] private class MtcData
        {
            public MtcAssignment[] assignments;
            public MtcUnit[] units;
        }

        [Serializable] private class MtcAssignment
        {
            public long dispatch_id;
            public long truck_id;
            public string truck_name;
            public long shovel_id;
            public string shovel_name;
            public string updated_at;
        }

        [Serializable] private class MtcUnit
        {
            public long unit_id;
            public string unit_name;
            public double easting;
            public double northing;
            public bool gps_valid;
            public double gps_age_seconds;
            public double speed_kmh;
            public string activity_name;
        }

        private class Fleet
        {
            public long Id;
            public string Name;
            public MtcUnit Shovel;
            public readonly List<Truck> Trucks = new List<Truck>();
        }

        private class Truck
        {
            public MtcAssignment Assignment;
            public MtcUnit Unit;
        }

        private struct GapAnimation
        {
            public float From;
            public float To;
            public float StartedAt;
        }

        public bool IsOpen { get; private set; }

        private MtcSnapshot snapshot;
        private readonly List<Fleet> fleets = new List<Fleet>();
        private readonly Dictionary<long, bool> freshCache = new Dictionary<long, bool>();
        private int freshCacheSecond = -1;
        private UnityWebRequest activeRequest;
        private Coroutine refreshLoop;
        private float nextRefreshAt;
        private string errorText;
        private string search = "";
        private bool freshOnly;
        private long selectedFleetId;
        private long selectedTruckId;
        private Vector2 scroll;
        private readonly Dictionary<(long, long), GapAnimation> gapAnimations =
            new Dictionary<(long, long), GapAnimation>();
        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle textStyle;
        private GUIStyle mutedStyle;
        private GUIStyle smallStyle;
        private GUIStyle numberStyle;
        private GUIStyle markerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;
        private GUIStyle warningStyle;

        private static readonly Color Ink = new Color(0.89f, 0.94f, 0.93f);
        private static readonly Color Muted = new Color(0.59f, 0.70f, 0.68f);
        private static readonly Color Teal = new Color(0.32f, 0.81f, 0.70f);
        private static readonly Color Amber = new Color(0.98f, 0.72f, 0.38f);
        private const float CorridorPixelsPerMeter = 0.9f;
        private const float GapTransitionSeconds = 0.8f;

        public void Open()
        {
            IsOpen = true;
            nextRefreshAt = 0f;
            gapAnimations.Clear();
            if (refreshLoop == null) refreshLoop = StartCoroutine(RefreshLoop());
        }

        public void Close()
        {
            IsOpen = false;
            if (activeRequest != null) activeRequest.Abort();
            if (refreshLoop != null) StopCoroutine(refreshLoop);
            activeRequest = null;
            refreshLoop = null;
        }

        private void OnDisable() { Close(); }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        private IEnumerator RefreshLoop()
        {
            while (IsOpen)
            {
                if (Time.realtimeSinceStartup >= nextRefreshAt)
                {
                    nextRefreshAt = Time.realtimeSinceStartup + 10f;
                    yield return FetchSnapshot();
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            refreshLoop = null;
        }

        private IEnumerator FetchSnapshot()
        {
            string baseUrl = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.apiBaseUrl : null;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                errorText = "Alamat backend belum tersedia.";
                yield break;
            }

            using (var request = UnityWebRequest.Get(baseUrl.TrimEnd('/') + "/api/v1/mtc/live"))
            {
                activeRequest = request;
                request.timeout = 8;
                FMSApiSession.Authorize(request);
                yield return request.SendWebRequest();
                if (!IsOpen) { activeRequest = null; yield break; }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    errorText = "Koneksi MTC terganggu. Periksa backend lalu tekan Segarkan.";
                    Debug.LogWarning("[FMSMtcPanel] MTC request failed: " + request.error);
                }
                else
                {
                    try
                    {
                        MtcSnapshot parsed = JsonUtility.FromJson<MtcSnapshot>(request.downloadHandler.text);
                        if (parsed == null || parsed.data == null) throw new FormatException("Respons MTC tidak lengkap.");
                        snapshot = parsed;
                        errorText = null;
                        BuildFleets();
                    }
                    catch (Exception ex)
                    {
                        errorText = "Respons MTC tidak dapat dibaca. Coba segarkan kembali.";
                        Debug.LogWarning("[FMSMtcPanel] Invalid MTC response: " + ex.Message);
                    }
                }
                activeRequest = null;
            }
        }

        private void BuildFleets()
        {
            fleets.Clear();
            freshCache.Clear();
            freshCacheSecond = -1;
            var units = new Dictionary<long, MtcUnit>();
            foreach (MtcUnit unit in snapshot.data.units ?? Array.Empty<MtcUnit>())
                if (unit != null) units[unit.unit_id] = unit;

            var latest = new Dictionary<long, MtcAssignment>();
            foreach (MtcAssignment assignment in snapshot.data.assignments ?? Array.Empty<MtcAssignment>())
            {
                if (assignment == null || assignment.truck_id <= 0 || assignment.shovel_id <= 0) continue;
                if (!latest.TryGetValue(assignment.truck_id, out MtcAssignment previous) ||
                    AssignmentTime(assignment) > AssignmentTime(previous) ||
                    AssignmentTime(assignment) == AssignmentTime(previous) && assignment.dispatch_id > previous.dispatch_id)
                    latest[assignment.truck_id] = assignment;
            }

            var byShovel = new Dictionary<long, Fleet>();
            foreach (MtcAssignment assignment in latest.Values)
            {
                if (!byShovel.TryGetValue(assignment.shovel_id, out Fleet fleet))
                {
                    units.TryGetValue(assignment.shovel_id, out MtcUnit shovel);
                    fleet = new Fleet { Id = assignment.shovel_id,
                        Name = string.IsNullOrEmpty(assignment.shovel_name) ? "EX " + assignment.shovel_id : assignment.shovel_name,
                        Shovel = shovel };
                    byShovel.Add(fleet.Id, fleet);
                }
                units.TryGetValue(assignment.truck_id, out MtcUnit truckUnit);
                fleet.Trucks.Add(new Truck { Assignment = assignment, Unit = truckUnit });
            }
            fleets.AddRange(byShovel.Values.OrderBy(fleet => fleet.Name, StringComparer.OrdinalIgnoreCase));
            foreach (Fleet fleet in fleets)
            {
                fleet.Trucks.Sort((a, b) => string.Compare(a.Assignment.truck_name, b.Assignment.truck_name,
                    StringComparison.OrdinalIgnoreCase));
            }
            if (selectedFleetId != 0 && !fleets.Exists(fleet => fleet.Id == selectedFleetId)) selectedFleetId = 0;
            if (selectedTruckId != 0 && !fleets.Any(fleet => fleet.Trucks.Any(truck => truck.Assignment.truck_id == selectedTruckId)))
                selectedTruckId = 0;
        }

        private static DateTimeOffset AssignmentTime(MtcAssignment assignment)
        {
            return DateTimeOffset.TryParse(assignment.updated_at, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset time) ? time : DateTimeOffset.MinValue;
        }

        private static bool AssignmentIsOld(MtcAssignment assignment)
        {
            return (DateTimeOffset.UtcNow - AssignmentTime(assignment)).TotalHours > 12;
        }

        private bool TrySnapshotAge(out double seconds)
        {
            seconds = 0;
            if (snapshot == null || !DateTimeOffset.TryParse(snapshot.generated_at,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset generated))
                return false;
            seconds = Math.Max(0, (DateTimeOffset.UtcNow - generated).TotalSeconds);
            return true;
        }

        private bool IsFresh(MtcUnit unit)
        {
            if (unit == null || snapshot == null) return false;
            int second = Mathf.FloorToInt(Time.realtimeSinceStartup);
            if (freshCacheSecond != second)
            {
                freshCache.Clear();
                freshCacheSecond = second;
            }
            if (freshCache.TryGetValue(unit.unit_id, out bool cached)) return cached;
            bool hasTime = TrySnapshotAge(out double elapsed);
            bool fresh = hasTime && unit.gps_valid && !double.IsNaN(unit.easting) && !double.IsNaN(unit.northing) &&
                !double.IsInfinity(unit.easting) && !double.IsInfinity(unit.northing) &&
                unit.gps_age_seconds >= 0 && unit.gps_age_seconds + elapsed <= Math.Max(1, snapshot.gps_fresh_seconds);
            freshCache[unit.unit_id] = fresh;
            return fresh;
        }

        private static double Distance(MtcUnit a, MtcUnit b)
        {
            double dx = a.easting - b.easting;
            double dy = a.northing - b.northing;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string Meters(double? value)
        {
            if (!value.HasValue) return "--";
            return value.Value < 1000 ? Math.Round(value.Value).ToString("N0", CultureInfo.InvariantCulture) + " m"
                : (value.Value / 1000).ToString("F2", CultureInfo.InvariantCulture) + " km";
        }

        private static string UnitName(Truck truck)
        {
            return !string.IsNullOrEmpty(truck.Assignment.truck_name) ? truck.Assignment.truck_name
                : truck.Unit != null && !string.IsNullOrEmpty(truck.Unit.unit_name) ? truck.Unit.unit_name : "Unit " + truck.Assignment.truck_id;
        }

        private void EnsureStyles()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            pixel.wrapMode = TextureWrapMode.Clamp;
            Font font = FMSDashboardUI.GetPoppinsFont();
            titleStyle = MakeStyle(font, 18, Ink, FontStyle.Bold);
            headerStyle = MakeStyle(font, 12, Ink, FontStyle.Bold);
            textStyle = MakeStyle(font, 12, Ink, FontStyle.Normal);
            mutedStyle = MakeStyle(font, 11, Muted, FontStyle.Normal);
            smallStyle = MakeStyle(font, 10, Muted, FontStyle.Normal);
            numberStyle = MakeStyle(font, 11, Ink, FontStyle.Bold);
            numberStyle.alignment = TextAnchor.MiddleCenter;
            markerStyle = MakeStyle(font, 10, new Color(0.04f, 0.17f, 0.16f), FontStyle.Bold);
            markerStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle = MakeStyle(font, 11, Ink, FontStyle.Bold);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            inputStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 12, normal = { textColor = Ink } };
            warningStyle = MakeStyle(font, 10, Amber, FontStyle.Normal);
        }

        private static GUIStyle MakeStyle(Font font, int size, Color color, FontStyle weight)
        {
            return new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight,
                normal = { textColor = color }, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private bool Button(Rect rect, string label, string tooltip = null)
        {
            Fill(rect, new Color(0.18f, 0.29f, 0.28f));
            return GUI.Button(rect, new GUIContent(label, tooltip), buttonStyle);
        }

        public void Draw(float screenWidth, float screenHeight)
        {
            if (!IsOpen) return;
            EnsureStyles();
            int oldDepth = GUI.depth;
            GUI.depth = -90;
            Fill(new Rect(0, 0, screenWidth, screenHeight), new Color(0.02f, 0.07f, 0.07f, 0.82f));

            float width = Mathf.Min(1140f, screenWidth - 24f);
            float height = Mathf.Min(780f, screenHeight - 32f);
            Rect panel = new Rect((screenWidth - width) * 0.5f, (screenHeight - height) * 0.5f, width, height);
            Fill(panel, new Color(0.07f, 0.13f, 0.14f));
            Fill(new Rect(panel.x, panel.y, width, 3f), Teal);
            GUI.Label(new Rect(panel.x + 20, panel.y + 13, width - 170, 24), "MTC  /  Fleet movement", titleStyle);
            GUI.Label(new Rect(panel.x + 20, panel.y + 38, width - 170, 18),
                "Fleet excavator | jarak antarunit dari GPS aktual", mutedStyle);
            if (Button(new Rect(panel.xMax - 48, panel.y + 17, 28, 28), "X", "Tutup MTC")) Close();
            if (Button(new Rect(panel.xMax - 153, panel.y + 17, 96, 28), "Buka web", "Buka MTC di jendela browser"))
            {
                string url = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.apiBaseUrl : "";
                if (!string.IsNullOrEmpty(url)) Application.OpenURL(url.TrimEnd('/') + "/mtc/");
            }
            Fill(new Rect(panel.x + 20, panel.y + 70, width - 40, 1), new Color(0.21f, 0.35f, 0.33f));

            DrawToolbar(panel);
            DrawBody(panel);
            GUI.depth = oldDepth;
        }

        private void DrawToolbar(Rect panel)
        {
            float x = panel.x + 20;
            float y = panel.y + 83;
            float space = panel.width - 40;
            bool compact = space < 730;
            float fleetWidth = compact ? space : 350;
            if (Button(new Rect(x, y, 55, 30), "Semua")) { selectedFleetId = 0; scroll = Vector2.zero; }
            if (Button(new Rect(x + 62, y, 28, 30), "<", "Fleet sebelumnya")) SelectFleet(-1);
            string selected = selectedFleetId == 0 ? "Semua fleet" : fleets.Find(fleet => fleet.Id == selectedFleetId)?.Name ?? "Semua fleet";
            GUI.Label(new Rect(x + 99, y, fleetWidth - 142, 30), selected, textStyle);
            if (Button(new Rect(x + fleetWidth - 35, y, 28, 30), ">", "Fleet berikutnya")) SelectFleet(1);

            if (!compact)
            {
                search = GUI.TextField(new Rect(x + 365, y, Mathf.Max(120, space - 620), 30), search, inputStyle);
                if (string.IsNullOrEmpty(search))
                    GUI.Label(new Rect(x + 375, y + 5, Mathf.Max(100, space - 640), 20), "Cari fleet atau unit", mutedStyle);
                freshOnly = GUI.Toggle(new Rect(panel.xMax - 235, y + 5, 116, 24), freshOnly, "GPS aktif", textStyle);
                if (Button(new Rect(panel.xMax - 103, y, 83, 30), "Segarkan")) nextRefreshAt = 0f;
            }
            else
            {
                float secondY = y + 39;
                search = GUI.TextField(new Rect(x, secondY, Mathf.Max(72, space - 204), 30), search, inputStyle);
                if (string.IsNullOrEmpty(search))
                    GUI.Label(new Rect(x + 9, secondY + 5, Mathf.Max(60, space - 222), 20), "Cari unit", mutedStyle);
                freshOnly = GUI.Toggle(new Rect(panel.xMax - 192, secondY + 5, 105, 24), freshOnly, "GPS aktif", textStyle);
                if (Button(new Rect(panel.xMax - 80, secondY, 60, 30), "Muat")) nextRefreshAt = 0f;
            }
        }

        private void SelectFleet(int step)
        {
            if (fleets.Count == 0) return;
            int index = fleets.FindIndex(fleet => fleet.Id == selectedFleetId);
            if (index < 0) index = step > 0 ? -1 : 0;
            index = (index + step + fleets.Count) % fleets.Count;
            selectedFleetId = fleets[index].Id;
            scroll = Vector2.zero;
        }

        private void DrawBody(Rect panel)
        {
            bool compact = panel.width < 770;
            float top = panel.y + (compact ? 168 : 132);
            float innerWidth = panel.width - 40;
            int shownFleetCount = 0, shownUnitCount = 0, freshCount = 0, oldAssignments = 0;
            foreach (Fleet fleet in fleets)
            {
                if (selectedFleetId != 0 && fleet.Id != selectedFleetId) continue;
                int visible = fleet.Trucks.Count(truck => Matches(truck, fleet));
                if (visible == 0) continue;
                shownFleetCount++;
                shownUnitCount += visible;
                freshCount += fleet.Trucks.Count(truck => Matches(truck, fleet) && IsFresh(truck.Unit));
                oldAssignments += fleet.Trucks.Count(truck => Matches(truck, fleet) && AssignmentIsOld(truck.Assignment));
            }
            bool hasSnapshotAge = TrySnapshotAge(out double snapshotAge);
            bool snapshotOld = hasSnapshotAge && snapshotAge > Math.Max(1, snapshot.gps_fresh_seconds);
            string state = !string.IsNullOrEmpty(errorText) ? "Koneksi terganggu" :
                snapshot == null ? "Menunggu data" : !hasSnapshotAge ? "Waktu data tidak tersedia" :
                snapshotOld ? "Data GPS lama" : snapshot.status == "success" ? "Terhubung" : "Data terbatas";
            if (hasSnapshotAge)
                state += " | sinkron " + Math.Floor(snapshotAge).ToString("N0", CultureInfo.InvariantCulture) + " dtk lalu";
            GUIStyle stateStyle = !string.IsNullOrEmpty(errorText) || snapshotOld || snapshot != null && !hasSnapshotAge
                ? warningStyle : mutedStyle;
            string summary = shownFleetCount + " fleet   /   " + shownUnitCount + " unit   /   " + freshCount + " GPS aktif";
            if (compact)
            {
                GUI.Label(new Rect(panel.x + 20, top, innerWidth, 22), summary, headerStyle);
                GUI.Label(new Rect(panel.x + 20, top + 22, innerWidth, 20),
                    state, stateStyle);
                top += 46;
            }
            else
            {
                GUI.Label(new Rect(panel.x + 20, top, innerWidth * 0.65f, 22), summary, headerStyle);
                GUI.Label(new Rect(panel.x + 20 + innerWidth * 0.65f, top, innerWidth * 0.35f, 22),
                    state, stateStyle);
                top += 30;
            }
            if (oldAssignments > 0)
            {
                GUI.Label(new Rect(panel.x + 20, top, innerWidth, 20),
                    oldAssignments + " assignment lebih dari 12 jam; verifikasi sebelum keputusan dispatch.", warningStyle);
                top += 24;
            }
            if (!string.IsNullOrEmpty(errorText))
            {
                GUI.Label(new Rect(panel.x + 20, top, innerWidth, 20), errorText, new GUIStyle(mutedStyle) { normal = { textColor = Amber } });
                top += 24;
            }
            float viewHeight = Mathf.Max(50, panel.yMax - top - 34);
            float contentWidth = Mathf.Max(100, innerWidth - 18);
            float contentHeight = 0;
            foreach (Fleet fleet in fleets)
                if ((selectedFleetId == 0 || fleet.Id == selectedFleetId) && fleet.Trucks.Exists(truck => Matches(truck, fleet)))
                    contentHeight += FleetHeight(fleet, contentWidth, compact) + 14;
            contentHeight = Mathf.Max(contentHeight, viewHeight);
            Rect viewport = new Rect(panel.x + 20, top, innerWidth, viewHeight);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, contentWidth, contentHeight));
            float y = 0;
            foreach (Fleet fleet in fleets)
            {
                if (selectedFleetId != 0 && fleet.Id != selectedFleetId) continue;
                if (!fleet.Trucks.Exists(truck => Matches(truck, fleet))) continue;
                DrawFleet(fleet, 0, y, contentWidth, compact);
                y += FleetHeight(fleet, contentWidth, compact) + 14;
            }
            if (shownFleetCount == 0)
                GUI.Label(new Rect(8, 12, contentWidth - 16, 48), snapshot == null ? "Memuat MTC..." : "Tidak ada fleet yang cocok dengan filter atau assignment belum tersedia.", textStyle);
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x + 20, panel.yMax - 27, innerWidth, 17),
                "Urutan koridor berdasarkan kedekatan ke EX; semua jarak adalah garis lurus GPS, bukan jarak jalan.", smallStyle);
        }

        private bool Matches(Truck truck, Fleet fleet)
        {
            if (freshOnly && !IsFresh(truck.Unit)) return false;
            string query = search.Trim();
            return query.Length == 0 || fleet.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                UnitName(truck).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private float FleetHeight(Fleet fleet, float width, bool compact)
        {
            float total = 239f;
            foreach (Truck truck in OrderedTrucks(fleet))
            {
                if (!Matches(truck, fleet)) continue;
                total += compact ? 66f : 54f;
                if (selectedTruckId == truck.Assignment.truck_id)
                    total += PairDetailHeight(fleet, width - 28f);
            }
            return total + 8f;
        }

        private List<Truck> OrderedTrucks(Fleet fleet)
        {
            bool shovelFresh = IsFresh(fleet.Shovel);
            return fleet.Trucks
                .OrderBy(truck => shovelFresh && IsFresh(truck.Unit) ? 0 : 1)
                .ThenByDescending(truck => shovelFresh && IsFresh(truck.Unit)
                    ? Distance(truck.Unit, fleet.Shovel) : 0)
                .ThenBy(UnitName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int PairColumns(float width)
        {
            return width >= 820f ? 3 : width >= 520f ? 2 : 1;
        }

        private static float PairDetailHeight(Fleet fleet, float width)
        {
            int pairs = Math.Max(0, fleet.Trucks.Count - 1);
            return 32f + Mathf.Max(1, Mathf.CeilToInt((float)pairs / PairColumns(width))) * 29f;
        }

        private float AnimatedGap(long fromUnitId, long toUnitId, float actualMeters)
        {
            var key = (fromUnitId, toUnitId);
            float now = Time.realtimeSinceStartup;
            if (!gapAnimations.TryGetValue(key, out GapAnimation state))
            {
                state = new GapAnimation { From = actualMeters, To = actualMeters, StartedAt = now };
            }
            float current = Mathf.Lerp(state.From, state.To,
                Mathf.Clamp01((now - state.StartedAt) / GapTransitionSeconds));
            if (Mathf.Abs(state.To - actualMeters) > 0.01f)
            {
                state.From = current;
                state.To = actualMeters;
                state.StartedAt = now;
            }
            gapAnimations[key] = state;
            return Mathf.Lerp(state.From, state.To,
                Mathf.Clamp01((now - state.StartedAt) / GapTransitionSeconds));
        }

        private void DrawFleet(Fleet fleet, float x, float y, float width, bool compact)
        {
            float height = FleetHeight(fleet, width, compact);
            Fill(new Rect(x, y, width, height), new Color(0.10f, 0.18f, 0.19f));
            Fill(new Rect(x, y, 3, height), Teal);
            if (selectedFleetId == 0 && Button(new Rect(x + width - 47f, y + 8f, 27f, 27f), ">",
                "Tampilkan hanya fleet " + fleet.Name))
            {
                selectedFleetId = fleet.Id;
                scroll = Vector2.zero;
            }
            GUI.Label(new Rect(x + 14, y + 7, width - 26, 23), fleet.Name, headerStyle);
            GUI.Label(new Rect(x + 14, y + 29, width - 26, 17),
                fleet.Trucks.Count + " unit ditugaskan   |   EX GPS " + (IsFresh(fleet.Shovel) ? "aktif" : "lama / tidak tersedia"), mutedStyle);

            List<Truck> ordered = OrderedTrucks(fleet);
            float laneY = y + 51f;
            Fill(new Rect(x + 12f, laneY, width - 24f, 155f), new Color(0.075f, 0.145f, 0.15f));
            GUI.Label(new Rect(x + 22f, laneY + 5f, 75f, 17f), "DISPOSAL", smallStyle);
            if (IsFresh(fleet.Shovel))
            {
                List<Truck> positioned = ordered.Where(truck => IsFresh(truck.Unit)).ToList();
                if (positioned.Count > 0)
                {
                    float laneWidth = width - 42f;
                    float[] gaps = new float[positioned.Count];
                    float totalMeters = 0f;
                    for (int i = 0; i < positioned.Count; i++)
                    {
                        MtcUnit next = i + 1 < positioned.Count ? positioned[i + 1].Unit : fleet.Shovel;
                        gaps[i] = AnimatedGap(positioned[i].Assignment.truck_id,
                            next.unit_id, (float)Distance(positioned[i].Unit, next));
                        totalMeters += gaps[i];
                    }
                    float scale = totalMeters > 0f
                        ? Mathf.Min(CorridorPixelsPerMeter, (laneWidth - 83f) / totalMeters)
                        : CorridorPixelsPerMeter;
                    float[] markerX = new float[positioned.Count + 1];
                    markerX[0] = laneWidth - 30f - totalMeters * scale;
                    for (int i = 0; i < positioned.Count; i++)
                        markerX[i + 1] = markerX[i] + gaps[i] * scale;
                    GUI.BeginGroup(new Rect(x + 20f, laneY + 5f, laneWidth, 125f));
                    Fill(new Rect(markerX[0], 54f, markerX[positioned.Count] - markerX[0], 2f),
                        new Color(0.31f, 0.45f, 0.43f));
                    for (int i = 0; i < positioned.Count; i++)
                    {
                        Truck truck = positioned[i];
                        int nearby = 0;
                        for (int previous = i - 1; previous >= 0 && markerX[i] - markerX[previous] < 22f; previous--)
                            nearby++;
                        float[] offsets = { 0f, -20f, 20f, -38f, 38f };
                        float markerY = 55f + offsets[nearby % offsets.Length];
                        Fill(new Rect(markerX[i], Mathf.Min(markerY, 55f), 1f, Mathf.Abs(markerY - 55f)), DividerColor());
                        Fill(new Rect(markerX[i] - 9f, markerY - 9f, 18f, 18f),
                            selectedTruckId == truck.Assignment.truck_id ? Amber : Teal);
                        GUI.Label(new Rect(markerX[i] - 9f, markerY - 9f, 18f, 18f),
                            new GUIContent((i + 1).ToString(), UnitName(truck) + " | " +
                                Meters(Distance(truck.Unit, fleet.Shovel)) + " ke " + fleet.Name), markerStyle);
                        MtcUnit next = i + 1 < positioned.Count ? positioned[i + 1].Unit : fleet.Shovel;
                        float gapWidth = markerX[i + 1] - markerX[i];
                        string gapLabel = Meters(Distance(truck.Unit, next));
                        float labelWidth = gapLabel.Length > 5 ? 60f : 40f;
                        if (gapWidth >= labelWidth)
                            GUI.Label(new Rect(markerX[i] + gapWidth * 0.5f - labelWidth * 0.5f,
                                105f, labelWidth, 18f), gapLabel, smallStyle);
                    }
                    float exX = markerX[positioned.Count];
                    float exY = exX - markerX[positioned.Count - 1] < 22f ? 83f : 55f;
                    Fill(new Rect(exX, Mathf.Min(exY, 55f), 1f, Mathf.Abs(exY - 55f)), DividerColor());
                    Fill(new Rect(exX - 10f, exY - 10f, 20f, 20f), Amber);
                    GUI.Label(new Rect(exX - 10f, exY - 10f, 20f, 20f), new GUIContent("EX", fleet.Name), markerStyle);
                    GUI.EndGroup();
                }
                else GUI.Label(new Rect(x + 22f, laneY + 25f, width - 44f, 24f),
                    "Belum ada posisi GPS unit yang segar.", mutedStyle);
            }
            else GUI.Label(new Rect(x + 22f, laneY + 25f, width - 44f, 24f),
                "GPS excavator lama. Urutan ke EX tidak dapat dihitung; jarak antarunit tetap tersedia di bawah.", mutedStyle);

            float tableY = y + 211f;
            Fill(new Rect(x + 12f, tableY, width - 24f, 1f), DividerColor());
            GUI.Label(new Rect(x + 14f, tableY + 3f, 170f, 20f), "UNIT", smallStyle);
            if (!compact)
            {
                GUI.Label(new Rect(x + 195f, tableY + 3f, 112f, 20f), "KE EX", smallStyle);
                GUI.Label(new Rect(x + 330f, tableY + 3f, width - 510f, 20f), "DARI UNIT SEBELUMNYA", smallStyle);
                GUI.Label(new Rect(x + width - 170f, tableY + 3f, 150f, 20f), "AKTIVITAS", smallStyle);
            }
            float rowY = y + 239f;
            for (int i = 0; i < ordered.Count; i++)
            {
                Truck truck = ordered[i];
                if (!Matches(truck, fleet)) continue;
                float rowHeight = compact ? 66f : 54f;
                bool selected = selectedTruckId == truck.Assignment.truck_id;
                if (selected) Fill(new Rect(x + 12f, rowY, width - 24f, rowHeight), new Color(0.13f, 0.27f, 0.26f));
                Fill(new Rect(x + 12f, rowY, width - 24f, 1f), DividerColor());
                if (GUI.Button(new Rect(x + 12f, rowY, width - 24f, rowHeight),
                    new GUIContent("", "Lihat jarak " + UnitName(truck) + " ke semua unit dalam fleet"), GUIStyle.none))
                {
                    selectedTruckId = selected ? 0 : truck.Assignment.truck_id;
                    selectedFleetId = fleet.Id;
                    scroll = Vector2.zero;
                }
                GUI.Label(new Rect(x + 14f, rowY + 7f, compact ? width * 0.51f : 170f, 20f),
                    (i + 1) + ". " + UnitName(truck), selected ? numberStyle : textStyle);
                bool oldAssignment = AssignmentIsOld(truck.Assignment);
                string activity = oldAssignment ? "Assignment lama" :
                    IsFresh(truck.Unit) ? (truck.Unit.activity_name ?? "GPS aktif") : "GPS lama / tidak ada";
                double? toExcavator = IsFresh(fleet.Shovel) && IsFresh(truck.Unit)
                    ? Distance(truck.Unit, fleet.Shovel) : null;
                float exX = compact ? x + width * 0.60f : x + 195f;
                GUI.Label(new Rect(exX, rowY + 7f, compact ? width * 0.33f : 110f, 21f), Meters(toExcavator), numberStyle);
                Truck previous = i > 0 && IsFresh(fleet.Shovel) ? ordered[i - 1] : null;
                string gapText = previous == null ? "--" : UnitName(previous) + "  " +
                    Meters(IsFresh(previous.Unit) && IsFresh(truck.Unit)
                        ? Distance(previous.Unit, truck.Unit) : (double?)null);
                if (compact)
                {
                    GUI.Label(new Rect(x + 14f, rowY + 34f, width - 28f, 20f),
                        "Dari unit sebelumnya: " + gapText, smallStyle);
                }
                else
                {
                    GUI.Label(new Rect(x + 330f, rowY + 7f, width - 510f, 21f), gapText, numberStyle);
                    GUI.Label(new Rect(x + width - 170f, rowY + 7f, 150f, 21f),
                        activity, oldAssignment ? warningStyle : smallStyle);
                }
                rowY += rowHeight;
                if (selectedTruckId == truck.Assignment.truck_id)
                {
                    DrawPairDetails(fleet, truck, x + 14f, rowY, width - 28f);
                    rowY += PairDetailHeight(fleet, width - 28f);
                }
            }
        }

        private static Color DividerColor() { return new Color(0.19f, 0.31f, 0.31f); }

        private void DrawPairDetails(Fleet fleet, Truck selected, float x, float y, float width)
        {
            GUI.Label(new Rect(x, y + 4f, width, 20f),
                "JARAK " + UnitName(selected) + " KE SETIAP UNIT", smallStyle);
            List<Truck> others = fleet.Trucks.Where(truck => truck != selected)
                .OrderBy(UnitName, StringComparer.OrdinalIgnoreCase).ToList();
            if (others.Count == 0)
            {
                GUI.Label(new Rect(x, y + 32f, width, 20f), "Tidak ada unit lain dalam fleet.", mutedStyle);
                return;
            }
            int columns = PairColumns(width);
            float cellWidth = width / columns;
            for (int i = 0; i < others.Count; i++)
            {
                Truck other = others[i];
                float cellX = x + (i % columns) * cellWidth;
                float cellY = y + 30f + (i / columns) * 29f;
                double? meters = IsFresh(selected.Unit) && IsFresh(other.Unit)
                    ? Distance(selected.Unit, other.Unit) : null;
                GUI.Label(new Rect(cellX, cellY, Mathf.Max(40f, cellWidth - 100f), 23f), UnitName(other), textStyle);
                GUI.Label(new Rect(cellX + cellWidth - 96f, cellY, 90f, 23f), Meters(meters), numberStyle);
            }
        }
    }
}
