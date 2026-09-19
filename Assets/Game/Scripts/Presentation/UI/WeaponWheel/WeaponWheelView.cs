using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Draws the weapon wheel: a ring of wedges, a hub that names the item under the pointer, and a
    /// dot that shows where the pointer is.
    ///
    /// <para>
    /// Built from code at runtime by the component that owns the wheel, so there is no prefab or
    /// scene to keep in step with it. It decides nothing: it is told which wedge is hovered and where
    /// the pointer is, and it draws that. It runs on unscaled time because the game is slowed while
    /// the wheel is up and a UI that slowed with it would crawl.
    /// </para>
    /// </summary>
    public sealed class WeaponWheelView : MonoBehaviour
    {
        /// <summary>Alpha below which a fading-out wheel is switched off rather than drawn invisibly.</summary>
        private const float HiddenAlpha = 0.01f;

        private WeaponWheelStyle style;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform dial;
        private Text hubText;
        private RectTransform pointerDot;
        private Sprite ringSprite;
        private Sprite discSprite;

        private readonly List<WheelSlotView> slots = new();
        private readonly List<WheelEntry> entries = new();

        private float targetAlpha;
        private int hoveredIndex = -1;

        /// <summary>Builds the wheel's objects. Call once, before the first <see cref="Show"/>.</summary>
        public void Build(WeaponWheelStyle wheelStyle, int slotCount)
        {
            style = wheelStyle;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = style.sortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = style.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // Deliberately no GraphicRaycaster: the wheel is looked at, never clicked, and must not
            // swallow or steal a click from anything else.
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            ringSprite = RingSprite.Create(style.innerRadius / style.outerRadius);
            discSprite = RingSprite.Create(0f);

            var backdrop = HudImage.Create(transform, "Backdrop", style.backdrop);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = Vector2.zero;
            backdrop.rectTransform.offsetMax = Vector2.zero;

            dial = new GameObject("Dial", typeof(RectTransform)).GetComponent<RectTransform>();
            dial.SetParent(transform, false);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < slotCount; i++)
                slots.Add(new WheelSlotView(dial, style, ringSprite, font, i, slotCount));

            BuildHub(font);

            var dot = HudImage.Create(dial, "Pointer", style.text);
            dot.sprite = discSprite;
            pointerDot = dot.rectTransform;
            pointerDot.sizeDelta = Vector2.one * style.pointerDotSize;

            gameObject.SetActive(false);
        }

        /// <summary>Fade the wheel in, showing <paramref name="wheelEntries"/>, one per wedge.</summary>
        public void Show(IReadOnlyList<WheelEntry> wheelEntries)
        {
            entries.Clear();
            entries.AddRange(wheelEntries);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SetEntry(i < entries.Count ? entries[i] : new WheelEntry(null, null, true, false));
                slots[i].SetHovered(false);
                slots[i].Snap();
            }

            hoveredIndex = -1;
            RefreshHub();
            pointerDot.anchoredPosition = Vector2.zero;

            gameObject.SetActive(true);
            targetAlpha = 1f;
        }

        public void Hide()
        {
            targetAlpha = 0f;
        }

        /// <summary>Tell the wheel which wedge the pointer is over and where the pointer is.</summary>
        public void SetPointer(Vector2 pointer, int hovered)
        {
            pointerDot.anchoredPosition = pointer;

            if (hovered == hoveredIndex) return;
            hoveredIndex = hovered;

            for (int i = 0; i < slots.Count; i++)
                slots[i].SetHovered(i == hoveredIndex);

            RefreshHub();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, style.fadeSpeed * dt);

            if (targetAlpha <= 0f && group.alpha <= HiddenAlpha)
            {
                group.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < slots.Count; i++)
                slots[i].Tick(dt);
        }

        private void OnDestroy()
        {
            RingSprite.Destroy(ringSprite);
            RingSprite.Destroy(discSprite);
        }

        /// <summary>
        /// The hub names what a release would equip. With the pointer still in the middle that is
        /// nothing new, so it names what is already in hand rather than sitting blank.
        /// </summary>
        private void RefreshHub()
        {
            int shown = hoveredIndex >= 0 && hoveredIndex < entries.Count && !entries[hoveredIndex].IsEmpty
                ? hoveredIndex
                : entries.FindIndex(entry => entry.IsEquipped);

            hubText.text = shown >= 0 ? entries[shown].Label : string.Empty;
        }

        private void BuildHub(Font font)
        {
            var hub = HudImage.Create(dial, "Hub", style.hub);
            hub.sprite = discSprite;
            hub.rectTransform.sizeDelta = Vector2.one * (style.innerRadius - style.hubGap) * 2f;

            hubText = new GameObject("Hub Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            hubText.transform.SetParent(dial, false);
            hubText.raycastTarget = false;
            hubText.font = font;
            hubText.fontSize = style.hubFontSize;
            hubText.color = style.text;
            hubText.alignment = TextAnchor.MiddleCenter;
            hubText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hubText.verticalOverflow = VerticalWrapMode.Truncate;
            hubText.rectTransform.sizeDelta = Vector2.one * style.innerRadius * 1.5f;
        }
    }
}
