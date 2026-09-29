using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    public class FMSModulePanel : MonoBehaviour
    {
        [Serializable] private class CatalogResponse
        {
            public string status;
            public int count;
            public MenuItem[] data;
        }

        [Serializable] private class MenuItem
        {
            public string code;
            public string parent_code;
            public string title;
            public string module_code;
            public string document_section;
            public string readiness;
            public bool is_visible;
            public bool is_enabled;
            public int display_order;
        }

        public bool IsOpen { get; private set; }
        public Action OpenMtc;
        public Action OpenMapEditor;

        private CatalogResponse catalog;
        private UnityWebRequest activeRequest;
        private string errorText;
        private string selectedGroup = "FLEET";
        private string search = "";
        private Vector2 listScroll;
        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle textStyle;
        private GUIStyle mutedStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;

        private static readonly Color Background = new Color(0.055f, 0.10f, 0.11f);
        private static readonly Color Surface = new Color(0.09f, 0.15f, 0.16f);
        private static readonly Color Divider = new Color(0.21f, 0.31f, 0.31f);
        private static readonly Color Ink = new Color(0.92f, 0.96f, 0.95f);
        private static readonly Color Muted = new Color(0.59f, 0.70f, 0.69f);
        private static readonly Color Teal = new Color(0.32f, 0.81f, 0.70f);
        private static readonly Color Amber = new Color(0.98f, 0.72f, 0.38f);

        public void Open()
        {
            IsOpen = true;
            if (catalog == null) StartCoroutine(FetchCatalog());
        }

        public void Close()
        {
            IsOpen = false;
            if (activeRequest != null) activeRequest.Abort();
            activeRequest = null;
        }

        private void OnDisable() { Close(); }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        private IEnumerator FetchCatalog()
        {
            if (activeRequest != null) yield break;
            string baseUrl = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.apiBaseUrl : null;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                errorText = "Alamat backend belum tersedia.";
                yield break;
            }

            using (var request = UnityWebRequest.Get(baseUrl.TrimEnd('/') + "/api/v1/fms/catalog"))
            {
                activeRequest = request;
                request.timeout = 12;
                FMSApiSession.Authorize(request);
                yield return request.SendWebRequest();
                if (!IsOpen) { activeRequest = null; yield break; }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    errorText = request.responseCode == 404
                        ? "Katalog FMS belum tersedia di server. Perbarui backend lalu segarkan."
                        : "Katalog FMS belum dapat diakses. Periksa koneksi atau akses server.";
                }
                else
                {
                    try
                    {
                        CatalogResponse parsed = JsonUtility.FromJson<CatalogResponse>(request.downloadHandler.text);
                        if (parsed == null || parsed.status != "success" || parsed.data == null)
                            throw new FormatException("Katalog FMS tidak lengkap.");
                        catalog = parsed;
                        errorText = null;
                        if (!parsed.data.Any(item => item != null && item.code == selectedGroup && string.IsNullOrEmpty(item.parent_code)))
                            selectedGroup = parsed.data.FirstOrDefault(item => item != null && string.IsNullOrEmpty(item.parent_code))?.code ?? "";
                    }
                    catch (Exception ex)
                    {
                        errorText = "Respons katalog tidak dapat dibaca.";
                        Debug.LogWarning("[FMSModulePanel] " + ex.Message);
                    }
                }
                activeRequest = null;
            }
        }

        private void EnsureStyles()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            Font font = FMSDashboardUI.GetPoppinsFont();
            titleStyle = Label(font, 19, Ink, FontStyle.Bold);
            sectionStyle = Label(font, 12, Ink, FontStyle.Bold);
            textStyle = Label(font, 12, Ink, FontStyle.Normal);
            mutedStyle = Label(font, 11, Muted, FontStyle.Normal);
            statusStyle = Label(font, 10, Amber, FontStyle.Bold);
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink }, hover = { textColor = Ink }
            };
            inputStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 12, normal = { textColor = Ink } };
        }

        private static GUIStyle Label(Font font, int size, Color color, FontStyle weight)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, fontStyle = weight,
                normal = { textColor = color }, alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private bool Button(Rect rect, string label, string tooltip)
        {
            Fill(rect, Surface);
            return GUI.Button(rect, new GUIContent(label, tooltip), buttonStyle);
        }

        public void Draw(float screenWidth, float screenHeight)
        {
            if (!IsOpen) return;
            EnsureStyles();
            int oldDepth = GUI.depth;
            GUI.depth = -85;
            Fill(new Rect(0, 0, screenWidth, screenHeight), new Color(0.02f, 0.06f, 0.07f, 0.82f));

            float width = Mathf.Min(960f, screenWidth - 16f);
            float height = Mathf.Min(720f, screenHeight - 20f);
            Rect panel = new Rect((screenWidth - width) * 0.5f, (screenHeight - height) * 0.5f, width, height);
            Fill(panel, Background);
            Fill(new Rect(panel.x, panel.y, width, 3f), Teal);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 12f, width - 160f, 28f), "FMS  /  Modul operasi", titleStyle);
            if (Button(new Rect(panel.xMax - 48f, panel.y + 14f, 28f, 28f), "X", "Tutup panel FMS")) Close();
            if (Button(new Rect(panel.xMax - 92f, panel.y + 14f, 28f, 28f), "↻", "Segarkan katalog FMS") && activeRequest == null)
                StartCoroutine(FetchCatalog());

            float summaryY = panel.y + 50f;
            string summary = catalog == null ? "Menunggu katalog" : catalog.count + " modul terdaftar";
            GUI.Label(new Rect(panel.x + 20f, summaryY, width - 40f, 20f), summary, mutedStyle);
            Fill(new Rect(panel.x + 20f, panel.y + 78f, width - 40f, 1f), Divider);

            float actionY = panel.y + 92f;
            if (Button(new Rect(panel.x + 20f, actionY, Mathf.Min(180f, width - 40f), 34f), "MTC fleet", "Buka pergerakan fleet excavator"))
            {
                Close();
                OpenMtc?.Invoke();
            }
            bool actionsSideBySide = width >= 620f;
            if (Button(new Rect(panel.x + (actionsSideBySide ? 210f : 20f),
                actionY + (actionsSideBySide ? 0f : 40f), 160f, 34f),
                "Editor peta", "Gambar draft jalan dan area di peta"))
            {
                Close();
                OpenMapEditor?.Invoke();
            }
            if (width >= 620f)
                GUI.Label(new Rect(panel.x + 380f, actionY + 7f, width - 400f, 20f), "Draft Astha  /  bukan data Hexagon", mutedStyle);

            float bodyY = actionY + (actionsSideBySide ? 52f : 92f);
            if (!string.IsNullOrEmpty(errorText))
            {
                GUI.Label(new Rect(panel.x + 20f, bodyY, width - 40f, 38f), errorText,
                    new GUIStyle(statusStyle) { wordWrap = true, clipping = TextClipping.Overflow });
                bodyY += 48f;
            }
            if (catalog == null)
            {
                GUI.depth = oldDepth;
                return;
            }

            bool compact = width < 650f;
            MenuItem[] groups = catalog.data.Where(item => item != null && string.IsNullOrEmpty(item.parent_code))
                .OrderBy(item => item.display_order).ToArray();
            if (compact)
            {
                int selectedIndex = Array.FindIndex(groups, item => item.code == selectedGroup);
                if (Button(new Rect(panel.x + 20f, bodyY, 30f, 30f), "<", "Kelompok sebelumnya") && groups.Length > 0)
                    selectedGroup = groups[(selectedIndex + groups.Length - 1) % groups.Length].code;
                MenuItem selected = groups.FirstOrDefault(item => item.code == selectedGroup);
                GUI.Label(new Rect(panel.x + 58f, bodyY, width - 116f, 30f), selected?.title ?? "Modul FMS", sectionStyle);
                if (Button(new Rect(panel.xMax - 50f, bodyY, 30f, 30f), ">", "Kelompok berikutnya") && groups.Length > 0)
                    selectedGroup = groups[(selectedIndex + 1) % groups.Length].code;
                bodyY += 42f;
            }
            else
            {
                float sidebarWidth = 220f;
                Fill(new Rect(panel.x + 20f, bodyY, sidebarWidth, Mathf.Max(0f, panel.yMax - bodyY - 20f)), Surface);
                float groupY = bodyY + 10f;
                foreach (MenuItem group in groups)
                {
                    bool selected = group.code == selectedGroup;
                    if (selected) Fill(new Rect(panel.x + 20f, groupY, 3f, 34f), Teal);
                    if (GUI.Button(new Rect(panel.x + 29f, groupY, sidebarWidth - 18f, 34f), group.title,
                        selected ? sectionStyle : textStyle))
                    {
                        selectedGroup = group.code;
                        listScroll = Vector2.zero;
                    }
                    groupY += 42f;
                }
            }

            float listX = compact ? panel.x + 20f : panel.x + 256f;
            float listWidth = compact ? width - 40f : width - 276f;
            float listTop = compact ? bodyY : bodyY;
            search = GUI.TextField(new Rect(listX, listTop, listWidth, 31f), search, inputStyle);
            if (string.IsNullOrEmpty(search))
                GUI.Label(new Rect(listX + 8f, listTop + 5f, listWidth - 16f, 21f), "Cari modul", mutedStyle);
            listTop += 43f;

            MenuItem[] items = catalog.data.Where(item => item != null && item.parent_code == selectedGroup &&
                (string.IsNullOrWhiteSpace(search) || item.title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 item.code.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(item => item.display_order).ToArray();
            float viewportHeight = Mathf.Max(0f, panel.yMax - listTop - 22f);
            float contentWidth = Mathf.Max(120f, listWidth - 18f);
            listScroll = GUI.BeginScrollView(new Rect(listX, listTop, listWidth, viewportHeight), listScroll,
                new Rect(0f, 0f, contentWidth, Mathf.Max(viewportHeight, items.Length * 53f)));
            for (int i = 0; i < items.Length; i++)
            {
                MenuItem item = items[i];
                float rowY = i * 53f;
                Fill(new Rect(0f, rowY + 51f, contentWidth, 1f), Divider);
                GUI.Label(new Rect(5f, rowY + 4f, contentWidth - 105f, 22f), item.title, textStyle);
                string state = item.readiness == "existing_readonly" ? "BACA-SAJA" : "RENCANA";
                GUI.Label(new Rect(contentWidth - 97f, rowY + 4f, 92f, 20f), state, statusStyle);
                string detail = string.IsNullOrEmpty(item.document_section) ? item.code :
                    item.code + "  ·  " + item.document_section;
                GUI.Label(new Rect(5f, rowY + 27f, contentWidth - 10f, 18f), detail, mutedStyle);
            }
            GUI.EndScrollView();
            GUI.depth = oldDepth;
        }
    }
}
