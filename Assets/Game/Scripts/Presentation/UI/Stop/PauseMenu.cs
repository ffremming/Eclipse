using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SpaceGame.Characters;
using SpaceGame.Core;
using SpaceGame.Gameplay;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Stops the world and offers the player a way out of it, or back into it.
    ///
    /// <para>
    /// Before this existed there was no way out of a started game at all: the cursor was locked,
    /// the main menu's Quit was two scenes away, and nothing in the world listened for Escape.
    /// </para>
    /// <para>
    /// The mouse dial is here rather than on a settings screen because this is the only moment the
    /// game gives a player to change anything, and look speed is the one setting nobody else's
    /// default fits. <c>PlayerLook</c> multiplies its rig scale by
    /// <see cref="GameSettings.MouseSensitivity"/>, so a new value is in the player's hands the
    /// moment they go back in — which is also the only place they can judge it, since looking
    /// around is one of the things being paused stops.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PauseMenu : MonoBehaviour
    {
        [Tooltip("The line across the screen while the world is stopped.")]
        [SerializeField] private string saying = "PAUSED";

        [Tooltip("The word for going back into the run.")]
        [SerializeField] private string resumeLabel = "Resume";

        [Tooltip("The word for starting the run again from the top.")]
        [SerializeField] private string restartLabel = "Start Again";

        [Tooltip("The word for going back to the main menu.")]
        [SerializeField] private string leaveLabel = "Leave";

        [Tooltip("The caption over the mouse dial.")]
        [SerializeField] private string dialCaption = "Mouse Speed";

        [SerializeField] private StopScreenStyle style = new();

        private PlayerController player;
        private RunExits exits;
        private StopScreenView view;
        private Slider dial;

        private readonly List<StopScreenChoice> choices = new();

        /// <summary>
        /// This screen's own action, not one of <see cref="PlayerInputManager"/>'s.
        ///
        /// <para>
        /// Everything that component owns is switched off while the game is paused — that is what
        /// pausing does to a player — so an Escape living there could open this screen and never
        /// close it again. The action that undoes a stop cannot be one of the things the stop
        /// stops.
        /// </para>
        /// </summary>
        private InputAction stopKey;

        private bool paused;

        /// <summary>Whether the world is currently stopped by this screen.</summary>
        public bool Paused => paused;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            exits = GetComponent<RunExits>();

            view = new GameObject("Pause Screen").AddComponent<StopScreenView>();
            view.transform.SetParent(transform, false);
            view.Build(style);

            dial = view.AddDial(dialCaption, GameSettings.MinSensitivity,
                                GameSettings.MaxSensitivity, GameSettings.MouseSensitivity);
            dial.onValueChanged.AddListener(OnDialMoved);

            stopKey = new InputAction("Stop", InputActionType.Button);
            stopKey.AddBinding("<Keyboard>/escape").WithGroup("Keyboard&Mouse");
            stopKey.AddBinding("<Gamepad>/start").WithGroup("Gamepad");
            stopKey.performed += _ => Toggle();

            choices.Add(new StopScreenChoice(resumeLabel, Resume));

            if (exits == null)
            {
                Debug.LogError("[PauseMenu] " + name + " has no RunExits beside it, so a paused " +
                               "player can only go back in, never out.", this);
                return;
            }

            choices.Add(new StopScreenChoice(restartLabel, exits.Restart));
            choices.Add(new StopScreenChoice(leaveLabel, exits.Leave));
        }

        private void OnEnable() => stopKey.Enable();

        private void OnDisable()
        {
            stopKey.Disable();

            // A screen still up when this component goes away would leave the game stopped with
            // nothing left running that could start it again — the same reason the weapon wheel
            // closes itself on the way out.
            if (paused) Resume();
        }

        private void OnDestroy() => stopKey?.Dispose();

        /// <summary>
        /// Escape came down. A dead player is already being offered the same two exits by the
        /// death screen, so this stays out of the way rather than stacking a second screen over
        /// the first.
        /// </summary>
        private void Toggle()
        {
            if (player.IsDead) return;

            if (paused) Resume();
            else Pause();
        }

        private void Pause()
        {
            if (paused) return;
            paused = true;

            // Cutscene mode first, then the clock. Handing the body over cancels every action the
            // player was holding, and a weapon wheel that closes on the way down puts the time
            // scale back to what it was before it opened — which has to happen BEFORE this sets
            // zero, or the wheel's restore would quietly start the world again.
            player.EnterCutsceneMode();
            Time.timeScale = 0f;

            dial.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            view.Show(saying, choices);
        }

        private void Resume()
        {
            if (!paused) return;
            paused = false;

            view.Hide();
            GameSettings.Save();

            Time.timeScale = 1f;
            player.ExitCutsceneMode();
        }

        // Written straight through rather than held until Resume, so a player who drags this and
        // then leaves by any of the three words still keeps what they set.
        private void OnDialMoved(float value) => GameSettings.MouseSensitivity = value;
    }
}
