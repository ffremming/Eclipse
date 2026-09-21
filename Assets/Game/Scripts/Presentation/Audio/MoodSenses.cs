// What the world looks like to the score right now, as plain data.
//
// Same shape and same reason as EnemySenses: everything that would need an Editor to gather stays
// on the far side of this struct, so the rule that turns it into a mood is a plain class the
// EditMode suite can reach.
namespace SpaceGame.Presentation
{
    public struct MoodSenses
    {
        /// <summary>The player's light has gone out and they have not stood back up.</summary>
        public bool PlayerDead;

        /// <summary>Something is chasing the player or swinging at them, close enough to matter.</summary>
        public bool Threatened;

        /// <summary>The player is standing inside a <see cref="MusicZone"/>.</summary>
        public bool InZone;

        /// <summary>What that zone asks for. Meaningless without <see cref="InZone"/>.</summary>
        public MusicMood ZoneMood;

        /// <summary>
        /// The zone keeps its own music through a fight rather than handing over to combat. What
        /// makes a castle sound like a castle even while its garrison is on you.
        /// </summary>
        public bool ZoneHoldsThroughFights;
    }
}
