using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// A pooled table on its own overlay canvas: a header row and up to <c>maxRows</c> rows of
    /// text cells laid out on shared column geometry, so header and rows always line up.
    /// Built in code, in the minigame screens' colours, for the same reason those screens are:
    /// the arena and the flying world carry no UI GameObjects, so a scene-authored panel would
    /// have to be duplicated per scene. Extracted from <c>MatchLeaderboardUI</c> when the race
    /// board became the second table.
    /// </summary>
    public sealed class LeaderboardTable
    {
        public readonly struct Column
        {
            public readonly string Header;
            public readonly float MinX;
            public readonly float MaxX;
            public readonly TextAlignmentOptions Alignment;

            public Column(string header, float minX, float maxX, TextAlignmentOptions alignment)
            {
                Header = header;
                MinX = minX;
                MaxX = maxX;
                Alignment = alignment;
            }
        }

        // Shared with MinigameConfigUI and MatchResultUI so the boards read as one set.
        public static readonly Color Backdrop = new(0.02f, 0.03f, 0.06f, 0.88f);
        public static readonly Color Accent = new(0.239f, 0.549f, 0.949f, 1f);
        public static readonly Color Muted = new(0.62f, 0.70f, 0.82f, 1f);
        public static readonly Color RowTint = new(1f, 1f, 1f, 0.04f);
        public static readonly Color LocalRowTint = new(0.239f, 0.549f, 0.949f, 0.20f);

        public const float RowHeight = 34f;
        public const float PanelWidth = 760f;
        public const float HeaderHeight = 64f;
        private const float PanelPadding = 24f;
        private const int HeaderFontSize = 24;
        private const int RowFontSize = 22;

        private readonly struct Row
        {
            public readonly GameObject Host;
            public readonly Image Background;
            public readonly TextMeshProUGUI[] Cells;

            public Row(GameObject host, Image background, TextMeshProUGUI[] cells)
            {
                Host = host;
                Background = background;
                Cells = cells;
            }
        }

        private readonly IReadOnlyList<Column> columns;
        private readonly List<Row> rows = new();
        private readonly RectTransform panelRect;

        public GameObject Panel { get; }
        public int MaxRows => rows.Count;

        public LeaderboardTable(Transform owner, string canvasName, int sortingOrder,
                                IReadOnlyList<Column> columns, int maxRows)
        {
            this.columns = columns;

            var canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(owner, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Panel = CreateChild("Panel", canvasGo.transform, out panelRect);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, HeaderHeight + RowHeight * 4f + PanelPadding);
            Panel.AddComponent<Image>().color = Backdrop;

            GameObject headerGo = CreateChild("Header", Panel.transform, out RectTransform headerRect);
            Stretch(headerRect, 0f, HeaderHeight);
            BuildCells(headerGo.transform, header: true);

            CreateChild("Rows", Panel.transform, out RectTransform containerRect);
            Stretch(containerRect, -HeaderHeight, RowHeight * maxRows);

            // Pooled up front rather than instantiated per rebuild: a full arena is 16 entities and
            // the table is rebuilt on every death.
            for (int i = 0; i < maxRows; i++)
                rows.Add(BuildRow(i, containerRect));
        }

        public void SetVisible(bool visible)
        {
            if (Panel.activeSelf != visible) Panel.SetActive(visible);
        }

        /// <summary>Sizes the backdrop to <paramref name="shown"/> rows and hides the rest.</summary>
        public void BeginRows(int shown)
        {
            shown = Mathf.Clamp(shown, 0, rows.Count);
            panelRect.sizeDelta = new Vector2(PanelWidth, HeaderHeight + RowHeight * Mathf.Max(1, shown) + PanelPadding);
            for (int i = shown; i < rows.Count; i++) rows[i].Host.SetActive(false);
        }

        /// <summary>Fills row <paramref name="index"/>; <paramref name="cells"/> is one string per column.</summary>
        public void SetRow(int index, bool isLocal, IReadOnlyList<string> cells)
        {
            Row row = rows[index];
            row.Host.SetActive(true);
            row.Background.color = isLocal ? LocalRowTint : RowTint;

            Color text = isLocal ? Color.white : Muted;
            for (int c = 0; c < row.Cells.Length; c++)
            {
                row.Cells[c].text = c < cells.Count ? cells[c] : string.Empty;
                row.Cells[c].color = text;
            }
        }

        private Row BuildRow(int index, RectTransform container)
        {
            GameObject rowGo = CreateChild($"Row{index:00}", container, out RectTransform rowRect);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            rowRect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, RowHeight - 3f);

            var background = rowGo.AddComponent<Image>();
            background.color = RowTint;

            TextMeshProUGUI[] cells = BuildCells(rowGo.transform, header: false);
            rowGo.SetActive(false);
            return new Row(rowGo, background, cells);
        }

        private TextMeshProUGUI[] BuildCells(Transform parent, bool header)
        {
            var cells = new TextMeshProUGUI[columns.Count];
            for (int c = 0; c < columns.Count; c++)
            {
                Column column = columns[c];
                cells[c] = Cell(parent, column.Header, header ? column.Header : string.Empty,
                                header ? Accent : Color.white, header ? HeaderFontSize : RowFontSize,
                                column.MinX, column.MaxX, column.Alignment);
            }

            return cells;
        }

        private static TextMeshProUGUI Cell(Transform parent, string name, string text, Color color,
                                            int fontSize, float anchorMinX, float anchorMaxX,
                                            TextAlignmentOptions alignment)
        {
            GameObject go = CreateChild(name, parent, out RectTransform rect);
            rect.anchorMin = new Vector2(anchorMinX, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            return label;
        }

        // Top-anchored strip inside the panel, inset by the padding on both sides.
        private static void Stretch(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(PanelPadding, 0f);
            rect.offsetMax = new Vector2(-PanelPadding, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static GameObject CreateChild(string name, Transform parent, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();
            return go;
        }
    }
}
