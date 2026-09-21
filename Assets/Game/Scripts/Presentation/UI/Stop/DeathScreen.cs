using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Gameplay;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// What a player whose light has gone out is offered: stand back up, start the run again, or
    /// leave it.
    ///
    /// <para>
    /// This is the listener <see cref="PlayerController.OnPlayerDeath"/> was raised for. Without
    /// one, dying froze the body, released the cursor and then did nothing at all — the run could
    /// not be left, restarted or resumed, and the only way out of the game was to kill it.
    /// </para>
    /// <para>
    /// It hides on <see cref="PlayerController.OnPlayerRevive"/> rather than on the click that asks
    /// for one, which is what that event exists for: the screen is closed by the standing up that
    /// actually happened.
    /// </para>
    /// <para>
    /// It deliberately does NOT stop time, unlike the pause screen. The player's own death
    /// animation is playing underneath it, and a world frozen on the frame of the killing blow
    /// would cut that off mid-swing. The world going on without the light in it is also the truer
    /// picture.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class DeathScreen : MonoBehaviour
    {
        [Tooltip("The line across the screen. Says what happened in the game's own terms — the " +
                 "lantern is the health bar, so this is what running out of it is called.")]
        [SerializeField] private string saying = "YOUR LIGHT IS OUT";

        [Tooltip("The word for standing back up where the run left off.")]
        [SerializeField] private string riseLabel = "Rise";

        [Tooltip("The word for starting the run again.")]
        [SerializeField] private string againLabel = "Again";

        [Tooltip("The word for going back to the main menu.")]
        [SerializeField] private string leaveLabel = "Leave";

        [SerializeField] private StopScreenStyle style = new();

        private PlayerController player;
        private RunExits exits;
        private PlayerRespawn respawn;
        private StopScreenView view;

        private readonly List<StopScreenChoice> choices = new();

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            exits = GetComponent<RunExits>();
            respawn = GetComponent<PlayerRespawn>();

            view = new GameObject("Death Screen").AddComponent<StopScreenView>();
            view.transform.SetParent(transform, false);
            view.Build(style);

            if (exits == null)
            {
                Debug.LogError("[DeathScreen] " + name + " has no RunExits beside it, so a dead " +
                               "player would be offered nothing that works.", this);
                return;
            }

            // First, because it is what a dead player nearly always wants: the run they were in,
            // continued. Starting over sits under it rather than in front of it.
            if (respawn == null)
                Debug.LogError("[DeathScreen] " + name + " has no PlayerRespawn beside it, so a " +
                               "dead player can only start the run again or leave it.", this);
            else
                choices.Add(new StopScreenChoice(riseLabel, respawn.Respawn));

            choices.Add(new StopScreenChoice(againLabel, exits.Restart));
            choices.Add(new StopScreenChoice(leaveLabel, exits.Leave));
        }

        private void OnEnable()
        {
            player.OnPlayerDeath += Show;
            player.OnPlayerRevive += Hide;

            // Catch up on a death that happened before this was listening — the same reason
            // PlayerController re-reads the health it subscribed to rather than trusting the event.
            if (player.IsDead) Show();
        }

        private void OnDisable()
        {
            player.OnPlayerDeath -= Show;
            player.OnPlayerRevive -= Hide;
        }

        private void Show()
        {
            if (choices.Count == 0) return;

            view.Show(saying, choices);
        }

        private void Hide() => view.Hide();
    }
}
