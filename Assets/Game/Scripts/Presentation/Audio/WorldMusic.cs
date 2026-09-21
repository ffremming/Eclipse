using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Enemies;
using SpaceGame.Gameplay;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// What the world tells the score. Reads the three things that can change the music — the
    /// player being dead, something hunting them, and where they are standing — and hands the
    /// answer to <see cref="MoodSense"/>, which decides.
    /// <para>
    /// Nothing is decided here on purpose. The rule has a piece of memory in it (a fight holds the
    /// music for a few seconds after it ends) and memory is the part that goes wrong quietly, so it
    /// lives in a plain class the EditMode suite can drive; this component only gathers.
    /// </para>
    /// <para>
    /// Sensed on an interval rather than every frame. The answer can only change when someone walks
    /// a few metres or an enemy changes its mind, and four times a second is far below what anyone
    /// notices — where it matters, the fade is seconds long anyway. The same poll is what finds the
    /// player, which is how this survives the player dying and coming back as a new body.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MusicDirector))]
    public sealed class WorldMusic : MonoBehaviour
    {
        [Tooltip("How close a creature that is hunting the player has to be for the music to call " +
                 "it a fight. Wider than the enemies' attack range on purpose: the fight starts " +
                 "when they set off towards you, not when they arrive.")]
        [Min(0f)]
        [SerializeField] private float threatRadius = 45f;

        [Tooltip("Seconds the music stays in a fight after the last thing fighting stops. Long " +
                 "enough to ride out an enemy losing sight of the player behind a rock, which is " +
                 "otherwise a score that flickers between two moods every few seconds.")]
        [Min(0f)]
        [SerializeField] private float combatHoldSeconds = 10f;

        [Tooltip("Seconds between readings of the world.")]
        [Min(0.02f)]
        [SerializeField] private float senseInterval = 0.25f;

        private MusicDirector director;
        private MoodSense sense;

        private Transform player;
        private PlayerController controller;
        private float nextSense;

        private void Awake()
        {
            director = GetComponent<MusicDirector>();
            sense = new MoodSense(combatHoldSeconds);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextSense) return;
            nextSense = Time.unscaledTime + senseInterval;

            FindPlayer();
            if (player == null) return;

            director.Play(sense.Read(Read(), Time.unscaledTime));
        }

        private MoodSenses Read()
        {
            MusicZone zone = MusicZone.At(player.position);

            return new MoodSenses
            {
                PlayerDead = controller != null && controller.IsDead,
                Threatened = EnemyAlert.AnyHunting(player.position, threatRadius),
                InZone = zone != null,
                ZoneMood = zone != null ? zone.Mood : MusicMood.Explore,
                ZoneHoldsThroughFights = zone != null && zone.HoldsThroughFights,
            };
        }

        // By tag, like everything else here that needs the player: the body is destroyed and
        // rebuilt between deaths, and a reference cached once does not survive it.
        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;

            GameObject found = GameObject.FindGameObjectWithTag(SpawnClearance.PlayerTag);
            player = found != null ? found.transform : null;
            controller = found != null ? found.GetComponent<PlayerController>() : null;
        }
    }
}
