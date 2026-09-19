using SpaceGame.Items;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// One wedge of the weapon wheel: the coloured segment, the item's name and its icon.
    ///
    /// <para>
    /// A plain class, not a component. It owns three child objects and one job — settling their
    /// colours on whatever the current state asks for — and the view that owns the wheel ticks it.
    /// </para>
    /// </summary>
    public sealed class WheelSlotView
    {
        /// <summary>The label may shrink to this fraction of its authored size before it is cut off.</summary>
        private const float MinLabelScale = 0.55f;

        /// <summary>How much of the wedge's chord the label may occupy. Leaves a margin to the neighbours.</summary>
        private const float LabelChordFill = 0.85f;

        private readonly WeaponWheelStyle style;
        private readonly Image wedge;
        private readonly Text label;
        private readonly Image icon;
        private readonly RectTransform wedgeRect;

        /// <summary>The middle of this wedge's band on the dial — where the label sits with no icon above it.</summary>
        private readonly Vector2 midpoint;

        private WheelEntry entry;
        private bool hovered;

        public WheelSlotView(RectTransform parent, WeaponWheelStyle style, Sprite ringSprite, Font font,
                             int index, int count)
        {
            this.style = style;

            float wedgeDegrees = 360f / count;
            float centreAngle = RadialSelection.CentreAngle(index, count);
            float midRadius = (style.outerRadius + style.innerRadius) * 0.5f;

            wedge = HudImage.Create(parent, "Slot " + index);
            wedge.sprite = ringSprite;
            wedge.type = Image.Type.Filled;
            wedge.fillMethod = Image.FillMethod.Radial360;
            wedge.fillOrigin = (int)Image.Origin360.Top;
            wedge.fillClockwise = true;
            wedge.fillAmount = Mathf.Max(0f, wedgeDegrees - style.gapDegrees) / 360f;

            wedgeRect = wedge.rectTransform;
            wedgeRect.sizeDelta = Vector2.one * style.outerRadius * 2f;

            // A fill starts at twelve o'clock and sweeps clockwise, and a positive z-rotation turns
            // counter-clockwise, hence the sign. Starting half a gap in from the wedge's edge keeps
            // the gap even on both sides.
            float startAngle = centreAngle - (wedgeDegrees - style.gapDegrees) * 0.5f;
            wedgeRect.localRotation = Quaternion.Euler(0f, 0f, -startAngle);

            midpoint = new Vector2(Mathf.Sin(centreAngle * Mathf.Deg2Rad),
                                   Mathf.Cos(centreAngle * Mathf.Deg2Rad)) * midRadius;

            icon = HudImage.Create(parent, "Icon " + index);
            icon.rectTransform.anchoredPosition = midpoint + Vector2.up * style.iconSize * 0.5f;
            icon.rectTransform.sizeDelta = Vector2.one * style.iconSize;
            icon.preserveAspect = true;

            // The chord, not the arc: it is the straight width the wedge really has at mid radius,
            // and a label wider than that pokes into its neighbour.
            float chord = 2f * midRadius * Mathf.Sin(wedgeDegrees * 0.5f * Mathf.Deg2Rad);

            label = new GameObject("Label " + index, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.raycastTarget = false;
            label.font = font;
            label.fontSize = style.labelFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.RoundToInt(style.labelFontSize * MinLabelScale);
            label.resizeTextMaxSize = style.labelFontSize;
            label.rectTransform.anchoredPosition = midpoint;
            label.rectTransform.sizeDelta = new Vector2(chord * LabelChordFill, style.labelFontSize * 2.4f);
        }

        public void SetEntry(WheelEntry newEntry)
        {
            entry = newEntry;
            label.text = entry.IsEmpty ? "Empty" : entry.Label;

            icon.sprite = entry.Icon;
            icon.enabled = entry.Icon != null && !entry.IsEmpty;

            // With an icon the label drops below it; without one it centres on the wedge.
            float drop = icon.enabled ? style.iconSize * 0.5f : 0f;
            label.rectTransform.anchoredPosition = midpoint - Vector2.up * drop;
        }

        public void SetHovered(bool isHovered)
        {
            hovered = isHovered && !entry.IsEmpty;
        }

        /// <summary>Jump straight to the current target with no easing. Used when the wheel opens.</summary>
        public void Snap()
        {
            Settle(1f);
        }

        /// <summary>Ease colour and size towards the current target.</summary>
        public void Tick(float deltaTime)
        {
            Settle(1f - Mathf.Exp(-style.settleSpeed * deltaTime));
        }

        private void Settle(float blend)
        {
            Color fill = entry.IsEmpty ? style.slotEmpty
                       : hovered ? style.slotHovered
                       : entry.IsEquipped ? style.slotEquipped
                       : style.slot;

            Color ink = entry.IsEmpty ? style.textEmpty
                      : hovered ? style.textHovered
                      : style.text;

            float scale = hovered ? style.hoverScale : 1f;

            wedge.color = Color.Lerp(wedge.color, fill, blend);
            label.color = Color.Lerp(label.color, ink, blend);
            wedgeRect.localScale = Vector3.one * Mathf.Lerp(wedgeRect.localScale.x, scale, blend);
        }
    }
}
