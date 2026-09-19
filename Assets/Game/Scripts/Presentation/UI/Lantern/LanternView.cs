using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Draws the lantern that shows how much light the player has: a glass body that fills from the
    /// bottom with light, under a cap and a handle, with a glow that spreads from it as it fills.
    ///
    /// <para>
    /// Built from code at runtime by the component that owns it, so there is no prefab or scene to
    /// keep in step with it. It decides nothing: it is told how full the lantern is and draws that,
    /// and it runs on unscaled time so it does not crawl while the weapon wheel slows the game.
    /// </para>
    /// <para>
    /// Every part is placed as a fraction of the lantern's own rectangle, so resizing the lantern in
    /// its style scales the whole drawing with it.
    /// </para>
    /// </summary>
    public sealed class LanternView : MonoBehaviour
    {
        // The lantern's proportions, as fractions of its height (Y) and width (X). They describe the
        // shape of the drawing, not a tuning: change them and it is a different lantern.
        private const float BaseTop = 0.10f;
        private const float GlassTop = 0.70f;
        private const float CapTop = 0.80f;
        private const float BaseInset = 0.02f;
        private const float CapInset = 0.06f;
        private const float GlassInset = 0.12f;
        private const float BarWidth = 0.05f;
        private const float HandleInset = 0.25f;
        /// <summary>Thickness of the bright line along the top of the light, as a fraction of the lantern's height.</summary>
        private const float SurfaceThickness = 0.012f;

        /// <summary>How many lantern widths across the glow reaches when the lantern is full.</summary>
        private const float HaloWidths = 3f;

        /// <summary>How many glass widths across the light at the heart of the fill is.</summary>
        private const float CoreWidths = 1.6f;

        private LanternStyle style;
        private RectTransform lantern;
        private RectTransform fill;
        private RectTransform core;
        private Image fillImage;
        private Image surfaceImage;
        private Image coreImage;
        private Image haloImage;
        private Sprite ringSprite;
        private Sprite glowSprite;

        private Vector2 restingPosition;
        private float target;
        private float shown;
        private float swell;
        private float jolt;

        /// <summary>Builds the lantern's objects. Call once, before anything else.</summary>
        public void Build(LanternStyle lanternStyle)
        {
            style = lanternStyle;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = style.sortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = style.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            ringSprite = RingSprite.Create(0.9f);
            glowSprite = GlowSprite.Create();

            lantern = new GameObject("Lantern", typeof(RectTransform)).GetComponent<RectTransform>();
            lantern.SetParent(transform, false);
            lantern.anchorMin = lantern.anchorMax = lantern.pivot = new Vector2(1f, 0f);
            lantern.sizeDelta = new Vector2(style.width, style.height);
            restingPosition = new Vector2(-style.margin.x, style.margin.y);
            lantern.anchoredPosition = restingPosition;

            BuildHalo();
            BuildHandle();
            BuildGlass();
            BuildFrame();
        }

        /// <summary>How full the lantern is being asked to be, 0 to 1. It eases towards this.</summary>
        public void SetLevel(float fraction) => target = Mathf.Clamp01(fraction);

        /// <summary>Light was taken back: the lantern swells for a moment.</summary>
        public void Gain() => swell = 1f;

        /// <summary>Light was lost: the lantern is jolted for a moment.</summary>
        public void Lose() => jolt = 1f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            shown = Mathf.MoveTowards(shown, target, style.settleSpeed * dt);
            float decay = Mathf.Exp(-style.reactionDecay * dt);
            swell *= decay;
            jolt *= decay;

            lantern.localScale = Vector3.one * (1f + swell * style.gainSwell);
            lantern.anchoredPosition = restingPosition + Random.insideUnitCircle * (jolt * style.lossJolt);

            ApplyLevel(shown, Brightness(shown));
        }

        private void OnDestroy()
        {
            RingSprite.Destroy(ringSprite);
            RingSprite.Destroy(glowSprite);
        }

        // Gutters only once it is running low, and never to nothing: a lantern that blinks out
        // completely cannot be told from an empty one, and empty means dead.
        private float Brightness(float level)
        {
            if (level >= style.flickerBelow || level <= 0f) return 1f;

            float noise = Mathf.PerlinNoise(Time.unscaledTime * style.flickerSpeed, 0f);
            return 1f - style.flickerDepth * noise;
        }

        private void ApplyLevel(float level, float brightness)
        {
            fill.anchorMax = new Vector2(1f, level);

            // The glow at the heart of the fill sits at its middle and grows with it, so a nearly
            // empty lantern is a small ember at the bottom, not a glow hanging in empty glass.
            core.anchorMin = core.anchorMax = new Vector2(0.5f, level * 0.5f);
            core.sizeDelta = Vector2.one * (style.width * (1f - 2f * GlassInset) * CoreWidths * level);

            fillImage.color = Dim(style.light, brightness);
            surfaceImage.color = Dim(style.surface, brightness);
            coreImage.color = Dim(style.core, brightness * level);
            haloImage.color = Dim(style.halo, brightness * level);

            surfaceImage.enabled = level > 0f;
        }

        private static Color Dim(Color colour, float factor)
        {
            colour.a *= factor;
            return colour;
        }

        private void BuildHalo()
        {
            haloImage = HudImage.Create(lantern, "Halo");
            haloImage.sprite = glowSprite;

            var rect = haloImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, (BaseTop + GlassTop) * 0.5f);
            rect.sizeDelta = Vector2.one * (style.width * HaloWidths);
        }

        // The ring is twice the handle's height and centred on its bottom edge, and the mask keeps
        // only the top half: what shows is an arch that rises out of the cap.
        private void BuildHandle()
        {
            RectTransform area = NewArea("Handle", HandleInset, CapTop, 1f - HandleInset, 1f);
            area.gameObject.AddComponent<RectMask2D>();

            var ring = HudImage.Create(area, "Handle Ring", style.frame);
            ring.sprite = ringSprite;
            Stretch(ring.rectTransform, 0f, -1f, 1f, 1f);
        }

        // Masked so the fill and the glow at its heart are cut off at the glass, however big they grow.
        private void BuildGlass()
        {
            RectTransform glass = NewArea("Glass", GlassInset, BaseTop, 1f - GlassInset, GlassTop);
            glass.gameObject.AddComponent<RectMask2D>();
            Stretch(HudImage.Create(glass, "Glass Back", style.glass).rectTransform, 0f, 0f, 1f, 1f);

            fillImage = HudImage.Create(glass, "Light", style.light);
            fill = fillImage.rectTransform;
            Stretch(fill, 0f, 0f, 1f, 0f);

            // A fixed-thickness strip riding the top of the fill, however full it is.
            surfaceImage = HudImage.Create(fill, "Surface", style.surface);
            var surface = surfaceImage.rectTransform;
            surface.anchorMin = Vector2.one;
            surface.anchorMax = Vector2.one;
            surface.pivot = new Vector2(1f, 1f);
            surface.sizeDelta = new Vector2(style.width * (1f - 2f * GlassInset), style.height * SurfaceThickness);
            surface.anchoredPosition = Vector2.zero;

            coreImage = HudImage.Create(glass, "Core");
            coreImage.sprite = glowSprite;
            core = coreImage.rectTransform;
        }

        private void BuildFrame()
        {
            NewBar("Base", BaseInset, 0f, 1f - BaseInset, BaseTop);
            NewBar("Cap", CapInset, GlassTop, 1f - CapInset, CapTop);
            NewBar("Left Bar", GlassInset, BaseTop, GlassInset + BarWidth, GlassTop);
            NewBar("Right Bar", 1f - GlassInset - BarWidth, BaseTop, 1f - GlassInset, GlassTop);
        }

        private void NewBar(string name, float xMin, float yMin, float xMax, float yMax)
        {
            var bar = HudImage.Create(lantern, name, style.frame);
            Stretch(bar.rectTransform, xMin, yMin, xMax, yMax);
        }

        private RectTransform NewArea(string name, float xMin, float yMin, float xMax, float yMax)
        {
            var area = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(lantern, false);
            Stretch(area, xMin, yMin, xMax, yMax);
            return area;
        }

        private static void Stretch(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
