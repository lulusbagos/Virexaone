using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    public class FMSMapEditorPanel : MonoBehaviour
    {
        [Serializable] private class MapPoint
        {
            public double easting;
            public double northing;
            public double elevation;
        }

        [Serializable] private class MapDraft
        {
            public string id;
            public string code;
            public string name;
            public string feature_type;
            public string shape_kind;
            public MapPoint[] points;
            public double width_m;
            public string color_hex;
            public string updated_at;
        }

        [Serializable] private class MapList
        {
            public string status;
            public MapDraft[] data;
        }

        [Serializable] private class ApiError
        {
            public string error;
        }

        public bool IsOpen { get; private set; }

        private readonly List<MapPoint> points = new List<MapPoint>();
        private readonly List<GameObject> lines = new List<GameObject>();
        private MapDraft[] saved = Array.Empty<MapDraft>();
        private MapDraft selected;
        private UnityWebRequest activeRequest;
        private Material lineMaterial;
        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle mutedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle fieldStyle;
        private Vector2 bodyScroll;
        private Vector2 listScroll;
        private Collider[] terrainSurfaces = Array.Empty<Collider>();
        private Action pendingAction;
        private string pendingMessage;
        private string pendingButton;
        private string code = "";
        private string nameText = "";
        private string widthText = "12";
        private string editorKey = "";
        private string featureType = "road";
        private string shapeKind = "line";
        private string colorHex = "#4FD0B4";
        private string message = "";
        private bool isDrawing = true;

        private static readonly Color Surface = new Color(0.06f, 0.12f, 0.13f);
        private static readonly Color Ink = new Color(0.92f, 0.96f, 0.95f);
        private static readonly Color Teal = new Color(0.31f, 0.81f, 0.70f);
        private static readonly Color Muted = new Color(0.61f, 0.70f, 0.69f);

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            saved = Array.Empty<MapDraft>();
            terrainSurfaces = Array.Empty<Collider>();
            RefreshLines();
            StartCoroutine(LoadDrafts());
        }

        public void RequestClose()
        {
            if (pendingAction != null) { pendingAction = null; return; }
            RequestAction(Close, "Perubahan yang belum disimpan akan hilang.", "Buang & tutup");
        }

        public void Close()
        {
            IsOpen = false;
            StopAllCoroutines();
            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
            }
            activeRequest = null;
            editorKey = "";
            pendingAction = null;
            NewDraft();
            ClearLines();
        }

        private bool HasUnsavedChanges()
        {
            if (selected == null)
                return points.Count > 0 || !string.IsNullOrWhiteSpace(code) ||
                    !string.IsNullOrWhiteSpace(nameText);
            if (code != selected.code || nameText != selected.name ||
                featureType != selected.feature_type || shapeKind != selected.shape_kind ||
                colorHex != selected.color_hex || points.Count != (selected.points?.Length ?? 0))
                return true;
            if (featureType == "road" && (!double.TryParse(widthText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double width) || Math.Abs(width - selected.width_m) > 0.001))
                return true;
            for (int i = 0; i < points.Count; i++)
            {
                MapPoint a = points[i], b = selected.points[i];
                if (a.easting != b.easting || a.northing != b.northing || a.elevation != b.elevation)
                    return true;
            }
            return false;
        }

        private void RequestAction(Action action, string prompt, string confirmLabel, bool alwaysConfirm = false)
        {
            if (activeRequest != null || pendingAction != null) return;
            if (!alwaysConfirm && !HasUnsavedChanges()) { action(); return; }
            pendingAction = action;
            pendingMessage = prompt;
            pendingButton = confirmLabel;
        }

        private void ConfirmAction()
        {
            Action action = pendingAction;
            pendingAction = null;
            action?.Invoke();
        }

        private void OnDisable() { Close(); }

        private void OnDestroy()
        {
            ClearLines();
            if (lineMaterial != null) Destroy(lineMaterial);
            if (pixel != null) Destroy(pixel);
        }

        private void EnsureStyles()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            Font font = FMSDashboardUI.GetPoppinsFont();
            titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = Ink } };
            labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 11,
                normal = { textColor = Ink } };
            mutedStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 10,
                wordWrap = true, normal = { textColor = Muted } };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 11,
                normal = { textColor = Ink }, hover = { textColor = Ink } };
            fieldStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 11,
                normal = { textColor = Ink } };
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null) lineMaterial = new Material(shader);
        }

        private void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = before;
        }

        private bool Button(Rect rect, string text, string tooltip = null)
        {
            return GUI.Button(rect, new GUIContent(text, tooltip), buttonStyle);
        }

        public void Draw(float screenWidth, float screenHeight)
        {
            if (!IsOpen) return;
            bool stylesReady = pixel != null;
            EnsureStyles();
            if (!stylesReady) RefreshLines();
            int oldDepth = GUI.depth;
            bool oldEnabled = GUI.enabled;
            GUI.depth = -95;
            float panelWidth = Mathf.Min(318f, screenWidth - 20f);
            Rect panel = new Rect(12f, 55f, panelWidth, Mathf.Max(100f, screenHeight - 68f));
            Fill(panel, Surface);
            Fill(new Rect(panel.x, panel.y, 3f, panel.height), Teal);
            GUI.enabled = oldEnabled && activeRequest == null && pendingAction == null;
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 78f, 26f),
                "FMS  /  Editor peta", titleStyle);
            if (Button(new Rect(panel.xMax - 63f, panel.y + 11f, 22f, 23f), "↻", "Segarkan draft"))
                RequestAction(() => { NewDraft(); StartCoroutine(LoadDrafts()); },
                    "Perubahan yang belum disimpan akan hilang.", "Muat ulang");
            if (Button(new Rect(panel.xMax - 35f, panel.y + 11f, 22f, 23f), "X", "Tutup editor"))
                RequestClose();
            if (!IsOpen)
            {
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
                return;
            }

            Rect body = new Rect(panel.x + 14f, panel.y + 45f, panel.width - 28f,
                Mathf.Max(45f, panel.height - 57f));
            float w = body.width - 16f;
            float contentHeight = 700f + (featureType == "road" ? 28f : 0f) +
                (selected != null ? 31f : 0f);
            bodyScroll = GUI.BeginScrollView(body, bodyScroll,
                new Rect(0f, 0f, body.width - 2f, contentHeight));
            float x = 0f;
            float y = 0f;
            GUI.Label(new Rect(x, y, w, 17f), "Jenis objek", labelStyle);
            y += 20f;
            string[] types = { "road", "loading", "front", "disposal", "stockpile", "note" };
            string[] labels = { "Jalan", "Loading", "Front", "Disposal", "Stockpile", "Teks" };
            float buttonWidth = (w - 8f) / 3f;
            for (int i = 0; i < types.Length; i++)
            {
                Rect rect = new Rect(x + (i % 3) * (buttonWidth + 4f), y + (i / 3) * 31f, buttonWidth, 27f);
                if (featureType == types[i]) Fill(rect, new Color(0.14f, 0.39f, 0.35f));
                if (Button(rect, labels[i], "Pilih " + labels[i]) && featureType != types[i])
                {
                    string type = types[i];
                    RequestAction(() => SelectType(type),
                        "Perubahan yang belum disimpan akan hilang.", "Ganti jenis");
                }
            }
            y += 67f;
            GUI.Label(new Rect(x, y, w, 17f), "Bentuk", labelStyle);
            y += 20f;
            if (featureType == "road" || featureType == "note")
                GUI.Label(new Rect(x, y, w, 25f), featureType == "road" ? "Garis jalan" : "Titik label", mutedStyle);
            else
            {
                string[] kinds = { "rectangle", "circle", "polygon" };
                string[] kindLabels = { "Kotak", "Lingkaran", "Poligon" };
                for (int i = 0; i < kinds.Length; i++)
                {
                    Rect rect = new Rect(x + i * (buttonWidth + 4f), y, buttonWidth, 27f);
                    if (shapeKind == kinds[i]) Fill(rect, new Color(0.14f, 0.39f, 0.35f));
                    if (Button(rect, kindLabels[i], "Gambar " + kindLabels[i]) && shapeKind != kinds[i])
                    {
                        string kind = kinds[i];
                        RequestAction(() => SelectShape(kind),
                            "Perubahan yang belum disimpan akan hilang.", "Ganti bentuk");
                    }
                }
            }
            y += 34f;

            GUI.Label(new Rect(x, y, w, 17f), "Kode", labelStyle);
            code = GUI.TextField(new Rect(x + 82f, y, w - 82f, 23f), code, 40, fieldStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 17f), "Nama", labelStyle);
            nameText = GUI.TextField(new Rect(x + 82f, y, w - 82f, 23f), nameText, 100, fieldStyle);
            y += 28f;
            if (featureType == "road")
            {
                GUI.Label(new Rect(x, y, w, 17f), "Lebar (m)", labelStyle);
                widthText = GUI.TextField(new Rect(x + 82f, y, 70f, 23f), widthText, 8, fieldStyle);
                y += 28f;
            }
            GUI.Label(new Rect(x, y, w, 17f), "Warna", labelStyle);
            string[] colors = { "#4FD0B4", "#F0B25D", "#65A9E5", "#E78B83" };
            for (int i = 0; i < colors.Length; i++)
            {
                Rect swatch = new Rect(x + 82f + i * 34f, y, 27f, 21f);
                if (ColorUtility.TryParseHtmlString(colors[i], out Color tint)) Fill(swatch, tint);
                if (GUI.Button(swatch, new GUIContent("", colors[i]), GUIStyle.none))
                {
                    colorHex = colors[i];
                    RefreshLines();
                }
                if (colorHex == colors[i]) Fill(new Rect(swatch.x, swatch.yMax + 1f, 27f, 2f), Ink);
            }
            y += 34f;
            float third = (w - 8f) / 3f;
            if (Button(new Rect(x, y, third, 28f), "Baru", "Mulai objek baru"))
                RequestAction(NewDraft, "Perubahan yang belum disimpan akan hilang.", "Mulai baru");
            if (Button(new Rect(x + third + 4f, y, third, 28f), "Undo", "Hapus titik terakhir") && points.Count > 0)
            {
                points.RemoveAt(points.Count - 1);
                isDrawing = true;
                RefreshLines();
            }
            bool fixedComplete = (shapeKind == "text" && points.Count == 1) ||
                (shapeKind == "rectangle" || shapeKind == "circle") && points.Count == 2;
            if (Button(new Rect(x + 2f * (third + 4f), y, third, 28f),
                fixedComplete ? "Ubah titik" : isDrawing ? "Selesai" : "Lanjut",
                fixedComplete ? "Pilih ulang titik terakhir" : "Akhiri atau lanjutkan menggambar"))
            {
                if (fixedComplete)
                {
                    points.RemoveAt(points.Count - 1);
                    isDrawing = true;
                    RefreshLines();
                }
                else isDrawing = !isDrawing;
            }
            y += 35f;
            GUI.Label(new Rect(x, y, w, 17f), "Titik: " + points.Count + "  |  " +
                (selected == null ? "Draft baru" : "Edit " + selected.code), mutedStyle);
            y += 24f;
            GUI.Label(new Rect(x, y, w, 17f), "Kunci editor", labelStyle);
            editorKey = GUI.PasswordField(new Rect(x, y + 20f, w, 24f), editorKey, '*', fieldStyle);
            y += 50f;
            if (Button(new Rect(x, y, w * 0.62f - 3f, 31f), "Simpan draft",
                "Simpan hanya ke tabel peta Astha"))
                StartCoroutine(SaveDraft());
            if (Button(new Rect(x + w * 0.62f + 3f, y, w * 0.38f - 3f, 31f),
                "Batal", "Tutup editor tanpa menyimpan")) RequestClose();
            y += 37f;
            if (selected != null && Button(new Rect(x, y, w, 25f), "Arsipkan draft",
                "Sembunyikan draft dari daftar"))
                RequestAction(() => StartCoroutine(RetireDraft()),
                    "Arsipkan draft tersimpan? Perubahan lokal akan hilang.", "Arsipkan", true);
            if (selected != null) y += 31f;
            GUI.Label(new Rect(x, y, w, 40f), message, mutedStyle);
            y += 43f;
            GUI.Label(new Rect(x, y, w, 18f), "Draft tersimpan", labelStyle);
            y += 20f;
            float listHeight = Mathf.Clamp(body.height - y - 12f, 90f, 180f);
            listScroll = GUI.BeginScrollView(new Rect(x, y, w, listHeight), listScroll,
                new Rect(0f, 0f, w - 16f, Mathf.Max(listHeight, saved.Length * 31f)));
            for (int i = 0; i < saved.Length; i++)
            {
                MapDraft item = saved[i];
                if (Button(new Rect(0f, i * 31f, w - 18f, 27f), item.name + "  |  " + item.feature_type,
                    "Pilih draft " + item.code))
                    RequestAction(() => SelectDraft(item),
                        "Perubahan yang belum disimpan akan hilang.", "Buka draft");
            }
            GUI.EndScrollView();
            GUI.EndScrollView();

            GUI.enabled = oldEnabled;
            if (!IsOpen)
            {
                GUI.depth = oldDepth;
                return;
            }

            DrawTextLabels(panel);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 &&
                !panel.Contains(current.mousePosition) && current.mousePosition.y > 44f &&
                isDrawing && activeRequest == null && pendingAction == null)
            {
                PickPoint(current.mousePosition);
                current.Use();
            }
            if (pendingAction != null) DrawConfirmation(screenWidth, screenHeight);
            GUI.depth = oldDepth;
        }

        private void DrawConfirmation(float screenWidth, float screenHeight)
        {
            Fill(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0.01f, 0.04f, 0.04f, 0.72f));
            float width = Mathf.Min(390f, screenWidth - 32f);
            Rect modal = new Rect((screenWidth - width) * 0.5f,
                (screenHeight - 136f) * 0.5f, width, 136f);
            Fill(modal, Surface);
            Fill(new Rect(modal.x, modal.y, width, 3f), Teal);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 15f, width - 36f, 25f),
                "Konfirmasi perubahan", titleStyle);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 48f, width - 36f, 35f),
                pendingMessage, mutedStyle);
            float half = (width - 42f) * 0.5f;
            if (Button(new Rect(modal.x + 18f, modal.y + 93f, half, 30f),
                "Lanjut edit", "Kembali ke editor")) pendingAction = null;
            if (Button(new Rect(modal.x + 24f + half, modal.y + 93f, half, 30f),
                pendingButton, "Konfirmasi tindakan")) ConfirmAction();
        }

        private void SelectType(string type)
        {
            if (featureType == type) return;
            featureType = type;
            shapeKind = type == "road" ? "line" : type == "note" ? "text" : "polygon";
            NewDraft();
        }

        private void SelectShape(string kind)
        {
            if (shapeKind == kind) return;
            shapeKind = kind;
            NewDraft();
        }

        private void NewDraft()
        {
            selected = null;
            points.Clear();
            code = "";
            nameText = "";
            isDrawing = true;
            message = "";
            RefreshLines();
        }

        private void SelectDraft(MapDraft item)
        {
            selected = item;
            code = item.code;
            nameText = item.name;
            featureType = item.feature_type;
            shapeKind = item.shape_kind;
            widthText = item.width_m.ToString("0.##", CultureInfo.InvariantCulture);
            colorHex = item.color_hex;
            points.Clear();
            if (item.points != null) points.AddRange(item.points);
            isDrawing = false;
            message = "Draft dipilih. Ubah titik dengan Undo lalu klik peta.";
            RefreshLines();
        }

        private void PickPoint(Vector2 guiMouse)
        {
            if (Camera.main == null) { message = "Kamera peta belum tersedia."; return; }
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(guiMouse.x, Screen.height - guiMouse.y));
            if (TryTerrainHit(ray, 50000f, out RaycastHit terrainHit))
            {
                AddPoint(terrainHit.point);
                return;
            }
            message = "Permukaan terrain belum tersedia di titik ini.";
        }

        private bool TryTerrainHit(Ray ray, float maxDistance, out RaycastHit nearest)
        {
            nearest = default;
            if (terrainSurfaces.Length == 0)
            {
                var surfaces = new List<Collider>(FindObjectsByType<TerrainCollider>(FindObjectsSortMode.None));
                MineTerrainLoader terrain = FindFirstObjectByType<MineTerrainLoader>();
                if (terrain != null && terrain.TryGetComponent(out MeshCollider mesh)) surfaces.Add(mesh);
                terrainSurfaces = surfaces.ToArray();
            }
            bool found = false;
            foreach (Collider surface in terrainSurfaces)
            {
                if (surface == null || !surface.enabled ||
                    !surface.Raycast(ray, out RaycastHit hit, maxDistance)) continue;
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        private bool AddPoint(Vector3 world)
        {
            UTMCoordinate utm = GeoCoordinateConverter.UnityToUTM(world);
            if (!GeoCoordinateConverter.IsInsideMappedTerrain(utm.Easting, utm.Northing)) return false;
            if (shapeKind == "text" && points.Count >= 1 ||
                (shapeKind == "rectangle" || shapeKind == "circle") && points.Count >= 2) return true;
            points.Add(new MapPoint { easting = utm.Easting, northing = utm.Northing,
                elevation = utm.Elevation });
            if (shapeKind == "text" || (shapeKind == "rectangle" || shapeKind == "circle") && points.Count == 2)
                isDrawing = false;
            message = points.Count + " titik terpilih.";
            RefreshLines();
            return true;
        }

        private string BaseUrl
        {
            get
            {
                string url = FMSDashboardUI.Instance?.apiBaseUrl;
                return string.IsNullOrWhiteSpace(url) ? "" :
                    url.TrimEnd('/') + "/api/v1/fms/map-drafts";
            }
        }

        private IEnumerator LoadDrafts()
        {
            if (activeRequest != null) yield break;
            if (string.IsNullOrEmpty(BaseUrl)) { message = "Alamat backend belum tersedia."; yield break; }
            message = "Memuat draft peta...";
            using (var request = UnityWebRequest.Get(BaseUrl))
            {
                activeRequest = request;
                request.timeout = 12;
                FMSApiSession.Authorize(request);
                yield return request.SendWebRequest();
                if (IsOpen && request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        MapList result = JsonUtility.FromJson<MapList>(request.downloadHandler.text);
                        if (result == null || result.status != "success" || result.data == null)
                            throw new FormatException("Daftar draft tidak lengkap.");
                        saved = result.data;
                        message = saved.Length + " draft peta tersedia.";
                        RefreshLines();
                    }
                    catch (Exception ex)
                    {
                        message = "Respons draft tidak dapat dibaca.";
                        Debug.LogWarning("[FMSMapEditorPanel] " + ex.Message);
                    }
                }
                else if (IsOpen) message = "Draft belum dapat dimuat. Periksa versi backend.";
                activeRequest = null;
            }
        }

        private IEnumerator SaveDraft()
        {
            int minimum = shapeKind == "text" ? 1 : shapeKind == "line" ? 2 : shapeKind == "polygon" ? 3 : 2;
            if (points.Count < minimum || (shapeKind == "circle" || shapeKind == "rectangle") && points.Count != 2)
            { message = "Titik belum cukup untuk bentuk ini."; yield break; }
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(nameText))
            { message = "Isi kode dan nama objek."; yield break; }
            if (string.IsNullOrEmpty(editorKey)) { message = "Masukkan kunci editor dari administrator."; yield break; }
            if (string.IsNullOrEmpty(BaseUrl)) { message = "Alamat backend belum tersedia."; yield break; }
            if (featureType == "road" && !double.TryParse(widthText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out _)) { message = "Lebar jalan harus angka meter."; yield break; }
            message = selected == null ? "Menyimpan draft..." : "Memperbarui draft...";
            var payload = new MapDraft
            {
                code = code.Trim(), name = nameText.Trim(), feature_type = featureType,
                shape_kind = shapeKind, points = points.ToArray(), color_hex = colorHex,
                width_m = featureType == "road" ? double.Parse(widthText, CultureInfo.InvariantCulture) : 0,
                updated_at = selected?.updated_at ?? ""
            };
            string url = BaseUrl + (selected == null ? "" : "/" + selected.id);
            using (var request = new UnityWebRequest(url, selected == null ? "POST" : "PUT"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-FMS-Editor-Key", editorKey);
                request.timeout = 15;
                FMSApiSession.Authorize(request);
                yield return request.SendWebRequest();
                if (IsOpen)
                {
                    message = request.result == UnityWebRequest.Result.Success
                        ? "Draft tersimpan di tabel Astha." : request.responseCode == 403
                        ? "Akses tulis ditolak. Periksa konfigurasi dan kunci editor."
                        : request.responseCode == 409 ? DescribeConflict(request.downloadHandler.text)
                        : request.responseCode == 400 ? DescribeApiError(request.downloadHandler.text)
                        : "Koneksi gagal saat menyimpan draft.";
                }
                activeRequest = null;
            }
            if (IsOpen && message.StartsWith("Draft tersimpan", StringComparison.Ordinal))
            {
                NewDraft();
                StartCoroutine(LoadDrafts());
            }
        }

        private IEnumerator RetireDraft()
        {
            if (selected == null || string.IsNullOrEmpty(editorKey))
            { message = "Pilih draft dan masukkan kunci editor."; yield break; }
            string url = BaseUrl + "/" + selected.id + "?revision=" +
                UnityWebRequest.EscapeURL(selected.updated_at);
            message = "Mengarsipkan draft...";
            using (var request = UnityWebRequest.Delete(url))
            {
                activeRequest = request;
                request.SetRequestHeader("X-FMS-Editor-Key", editorKey);
                FMSApiSession.Authorize(request);
                yield return request.SendWebRequest();
                if (IsOpen) message = request.result == UnityWebRequest.Result.Success
                    ? "Draft diarsipkan." : request.responseCode == 409
                    ? "Draft berubah di server. Segarkan sebelum mengarsipkan."
                    : request.responseCode == 403 ? "Akses tulis ditolak. Periksa kunci editor."
                    : "Koneksi gagal saat mengarsipkan draft.";
                activeRequest = null;
            }
            if (IsOpen && message == "Draft diarsipkan")
            {
                NewDraft();
                StartCoroutine(LoadDrafts());
            }
        }

        private static string DescribeApiError(string json)
        {
            ApiError parsed = null;
            try { parsed = JsonUtility.FromJson<ApiError>(json); }
            catch (Exception) { }
            switch (parsed?.error)
            {
                case "invalid_metadata": return "Kode, nama, atau warna tidak valid.";
                case "invalid_points": return "Jumlah titik belum sesuai bentuk.";
                case "invalid_geometry": return "Bentuk memiliki titik berulang atau ukuran terlalu kecil.";
                case "invalid_feature_shape": return "Jenis objek dan bentuk tidak cocok.";
                case "invalid_road_width": return "Lebar jalan harus 2-100 meter.";
                case "point_outside_site": return "Titik berada di luar area site.";
                case "invalid_radius": return "Radius lingkaran harus 1-5.000 meter.";
                case "code_already_exists": return "Kode sudah digunakan di site ini.";
                case "site_missing_or_invalid_shape": return "Site belum siap atau bentuk tidak valid.";
                default: return "Draft ditolak server. Periksa bentuk dan isian.";
            }
        }

        private static string DescribeConflict(string json)
        {
            try
            {
                if (JsonUtility.FromJson<ApiError>(json)?.error == "code_already_exists")
                    return "Kode sudah digunakan. Pilih kode lain.";
            }
            catch (Exception) { }
            return "Draft berubah di server. Segarkan sebelum menyimpan.";
        }

        private void ClearLines()
        {
            foreach (GameObject line in lines) if (line != null) Destroy(line);
            lines.Clear();
        }

        private void RefreshLines()
        {
            ClearLines();
            if (!IsOpen || lineMaterial == null) return;
            foreach (MapDraft item in saved)
                if (item != null && item.id != selected?.id)
                    DrawOutline(item.points, item.shape_kind, item.color_hex, false);
            DrawOutline(points.ToArray(), shapeKind, colorHex, true);
        }

        private void DrawOutline(MapPoint[] source, string kind, string tint, bool active)
        {
            if (source == null || source.Length == 0 || kind == "text") return;
            var outline = new List<MapPoint>(source);
            if (kind == "rectangle" && source.Length == 2)
            {
                MapPoint a = source[0], b = source[1];
                outline = new List<MapPoint> { a,
                    new MapPoint { easting = b.easting, northing = a.northing, elevation = a.elevation }, b,
                    new MapPoint { easting = a.easting, northing = b.northing, elevation = b.elevation } };
            }
            else if (kind == "circle" && source.Length == 2)
            {
                MapPoint a = source[0], b = source[1];
                double radius = Math.Sqrt(Math.Pow(b.easting - a.easting, 2) + Math.Pow(b.northing - a.northing, 2));
                outline.Clear();
                for (int i = 0; i < 48; i++)
                    outline.Add(new MapPoint { easting = a.easting + radius * Math.Cos(2 * Math.PI * i / 48),
                        northing = a.northing + radius * Math.Sin(2 * Math.PI * i / 48), elevation = a.elevation });
            }
            if (outline.Count < 2) return;
            bool closed = kind != "line" && source.Length >= (kind == "polygon" ? 3 : 2);
            GameObject go = new GameObject("Astha map draft outline");
            go.transform.SetParent(transform, false);
            LineRenderer renderer = go.AddComponent<LineRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.useWorldSpace = true;
            renderer.widthMultiplier = active ? 2f : 1.25f;
            renderer.numCapVertices = 2;
            var worldPoints = new List<Vector3>();
            int segments = outline.Count - 1 + (closed ? 1 : 0);
            for (int i = 0; i < segments; i++)
            {
                MapPoint a = outline[i];
                MapPoint b = outline[(i + 1) % outline.Count];
                double length = Math.Sqrt(Math.Pow(b.easting - a.easting, 2) +
                    Math.Pow(b.northing - a.northing, 2));
                int steps = Mathf.Clamp(Mathf.CeilToInt((float)(length / 10)), 1, 50);
                for (int step = 0; step < steps; step++)
                {
                    double t = (double)step / steps;
                    worldPoints.Add(GroundPoint(a.easting + (b.easting - a.easting) * t,
                        a.northing + (b.northing - a.northing) * t,
                        a.elevation + (b.elevation - a.elevation) * t));
                }
            }
            worldPoints.Add(closed ? worldPoints[0] : GroundPoint(outline[outline.Count - 1].easting,
                outline[outline.Count - 1].northing, outline[outline.Count - 1].elevation));
            renderer.positionCount = worldPoints.Count;
            Color outlineColor = ColorUtility.TryParseHtmlString(tint, out Color color) ? color : Teal;
            outlineColor.a = active ? 0.95f : 0.45f;
            renderer.startColor = renderer.endColor = outlineColor;
            renderer.SetPositions(worldPoints.ToArray());
            lines.Add(go);
        }

        private Vector3 GroundPoint(double easting, double northing, double elevation)
        {
            Vector3 world = GeoCoordinateConverter.UTMToUnity(easting, northing, elevation);
            Ray ray = new Ray(new Vector3(world.x, 1000f, world.z), Vector3.down);
            if (TryTerrainHit(ray, 2000f, out RaycastHit hit))
                world.y = hit.point.y;
            return world + Vector3.up * 0.35f;
        }

        private void DrawTextLabels(Rect panel)
        {
            if (Camera.main == null) return;
            foreach (MapDraft item in saved)
                if (item != null && item.id != selected?.id && item.shape_kind == "text" &&
                    item.points != null && item.points.Length > 0)
                    DrawTextLabel(item.points[0], item.name, panel);
            if (shapeKind == "text" && points.Count > 0)
                DrawTextLabel(points[0], nameText, panel);
        }

        private void DrawTextLabel(MapPoint point, string label, Rect panel)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            Vector3 pos = GeoCoordinateConverter.UTMToUnity(point.easting, point.northing, point.elevation + 5);
            Vector3 screen = Camera.main.WorldToScreenPoint(pos);
            if (screen.z <= 0) return;
            Rect textRect = new Rect(screen.x - 80f, Screen.height - screen.y - 16f, 160f, 24f);
            if (!textRect.Overlaps(panel) && textRect.y >= 44f && textRect.yMax <= Screen.height)
            {
                Fill(textRect, new Color(0.02f, 0.08f, 0.09f, 0.78f));
                GUI.Label(new Rect(textRect.x + 5f, textRect.y, textRect.width - 10f,
                    textRect.height), label, labelStyle);
            }
        }
    }
}
