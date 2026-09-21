using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// The screen the game stops on: a veil over the world, a line saying what happened, and the
    /// words for what can be done about it.
    ///
    /// <para>
    /// One view for both stops the game has — dying and pausing — because they are the same screen
    /// with a different title and a different set of words. Built from code at runtime by whichever
    /// component owns it, the same as the lantern, the wheel and the key banner, so there is no
    /// prefab or scene to keep in step with it.
    /// </para>
    /// <para>
    /// It decides nothing. It is told what to say, what to offer, and it runs its fade on unscaled
    /// time because the thing it comes up over has usually set <c>Time.timeScale</c> to zero.
    /// </para>
    /// </summary>
    public sealed class StopScreenView : MonoBehaviour
    {
        private StopScreenStyle style;
        private CanvasGroup group;
        private HudLabel title;

        private readonly List<Button> buttons = new();
        private readonly List<HudLabel> buttonLabels = new();
        private readonly List<StopScreenChoice> offered = new();

        private RectTransform choiceColumn;
        private bool showing;

        /// <summary>Builds the screen's objects, hidden. Call once, before anything else.</summary>
        public void Build(StopScreenStyle screenStyle)
        {
            style = screenStyle;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = style.sortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = style.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // The one canvas in this game whose whole point is being clicked, so unlike every other
            // code-built element here it needs a raycaster — and something to raycast with.
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            group = gameObject.AddComponent<CanvasGroup>();

            BuildVeil();
            title = HudLabel.Create(BuildFrame("Title", style.titleHeight,
                                               new Vector2(style.referenceResolution.x,
                                                           style.titleFontSize * 2f)),
                                    style.titleFontSize, style.titleInk, style.shadow,
                                    style.shadowOffset);

            choiceColumn = BuildFrame("Choices", 0f, Vector2.zero);

            Hide();
        }

        /// <summary>
        /// Puts the screen up, saying <paramref name="saying"/> and offering
        /// <paramref name="choices"/> in the order given.
        /// </summary>
        public void Show(string saying, IReadOnlyList<StopScreenChoice> choices)
        {
            title.Say(saying);
            Offer(choices);

            showing = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            group.alpha = 0f;
        }

        /// <summary>
        /// Adds a captioned dial above the choices and hands it back for the owner to wire up.
        /// Call once, after <see cref="Build"/>; a screen with nothing to tune never calls it.
        ///
        /// <para>
        /// The slider comes back rather than a setting going in, so this view keeps knowing nothing
        /// about what it is adjusting — the same as the choices, which are labels and callbacks.
        /// </para>
        /// </summary>
        public Slider AddDial(string caption, float minimum, float maximum, float value)
        {
            RectTransform captionFrame = BuildFrame(
                "Dial Caption", style.dialHeight + style.dialCaptionGap,
                new Vector2(style.dialSize.x, style.dialFontSize * 2f));

            HudLabel.Create(captionFrame, style.dialFontSize, style.choiceInk, style.shadow,
                            style.shadowOffset).Say(caption);

            RectTransform barFrame = BuildFrame("Dial", style.dialHeight, style.dialSize);

            return HudDial.Create(barFrame, minimum, maximum, value, style.dialTrack,
                                  style.dialFill, style.dialHandle, style.dialHandleWidth);
        }

        /// <summary>Takes the screen down. Safe to call when it is already down.</summary>
        public void Hide()
        {
            showing = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Unscaled, because a paused game has no scaled time left to fade on — and because a
        /// death screen that faded at the world's speed would arrive at whatever rate the weapon
        /// wheel had last left behind.
        /// </summary>
        private void Update()
        {
            if (!showing || group.alpha >= 1f) return;

            group.alpha = style.fadeSeconds <= 0f
                ? 1f
                : Mathf.Min(1f, group.alpha + Time.unscaledDeltaTime / style.fadeSeconds);
        }

        /// <summary>
        /// The world scene has no EventSystem of its own — nothing in it was ever clicked — so the
        /// first screen to want one brings it. Parented to this view, so it leaves with the player
        /// rather than outliving the scene it was made for.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var events = new GameObject("Event System", typeof(EventSystem))
                         .GetComponent<EventSystem>();
            events.transform.SetParent(transform, false);

            // The project is Input System only, so the old StandaloneInputModule would raycast
            // nothing at all. AssignDefaultActions gives the module its own point/click actions,
            // which is what lets it work while every action the player owns is switched off.
            events.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void BuildVeil()
        {
            Image veil = HudImage.Create(transform, "Veil", style.veil);

            // Against HudImage's default: the veil is what stops a click reaching the world behind
            // it, which is the whole reason a stopped game is safe to click on.
            veil.raycastTarget = true;

            var rect = (RectTransform)veil.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private RectTransform BuildFrame(string name, float height, Vector2 size)
        {
            var frame = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(transform, false);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = size;
            frame.anchoredPosition = new Vector2(0f, height);
            return frame;
        }

        /// <summary>
        /// Lays the words out, growing the column of buttons if this stop offers more than the last
        /// one did. Buttons are kept and relabelled rather than rebuilt, so the screen can come and
        /// go for a whole session without making garbage.
        /// </summary>
        private void Offer(IReadOnlyList<StopScreenChoice> choices)
        {
            offered.Clear();
            offered.AddRange(choices);

            while (buttons.Count < offered.Count) BuildButton(buttons.Count);

            for (int i = 0; i < buttons.Count; i++)
            {
                bool used = i < offered.Count;
                buttons[i].gameObject.SetActive(used);

                if (used) buttonLabels[i].Say(offered[i].Label);
            }
        }

        private void BuildButton(int index)
        {
            // White, not the resting colour: a Button tints its target graphic by multiplying the
            // colour it finds there, so the fill has to live in the ColorBlock or hovering would
            // land on the square of it.
            Image face = HudImage.Create(choiceColumn, "Choice " + (index + 1), Color.white);
            face.raycastTarget = true;

            var rect = (RectTransform)face.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = style.choiceSize;
            rect.anchoredPosition = new Vector2(
                0f, style.choiceTop - index * (style.choiceSize.y + style.choiceSpacing));

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.colors = ButtonColours();

            // The index is captured into a local: a listener closing over the loop's own variable
            // would run whichever choice happened to be last.
            int slot = index;
            button.onClick.AddListener(() => Choose(slot));

            var labelFrame = new GameObject("Label", typeof(RectTransform))
                             .GetComponent<RectTransform>();
            labelFrame.SetParent(rect, false);
            labelFrame.anchorMin = Vector2.zero;
            labelFrame.anchorMax = Vector2.one;
            labelFrame.offsetMin = Vector2.zero;
            labelFrame.offsetMax = Vector2.zero;

            HudLabel label = HudLabel.Create(labelFrame, style.choiceFontSize, style.choiceInk,
                                             style.shadow, style.shadowOffset);

            buttons.Add(button);
            buttonLabels.Add(label);
        }

        private ColorBlock ButtonColours()
        {
            ColorBlock colours = ColorBlock.defaultColorBlock;
            colours.normalColor = style.choiceFill;
            colours.highlightedColor = style.choiceHoverFill;
            colours.pressedColor = style.choiceHoverFill;

            // Selected is the resting colour rather than the highlight: a button keeps selection
            // after it is clicked, and a restart that failed to happen would otherwise leave one
            // word lit for no reason.
            colours.selectedColor = style.choiceFill;
            colours.disabledColor = style.choiceFill;
            colours.fadeDuration = 0.1f;
            return colours;
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= offered.Count) return;

            offered[index].Chosen?.Invoke();
        }
    }
}
