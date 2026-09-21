// Which mood the score should be in, and nothing else.
//
// The whole of the music's decision-making, with no Unity in it — EnemyBrain's shape, for the same
// reason: a choice made inside an Update is a choice nothing can test, and this one has a piece of
// state (the combat hold) that is exactly the kind of thing that goes wrong unnoticed.
//
// The rule that shapes the rest: combat is a DEADLINE, not a flag. An enemy that loses sight of the
// player for half a second, or dies while its neighbour is still closing, must not drop the music
// back to exploration and then snatch it up again — that flutter is the failure mode of every naive
// combat-music system, and it is far more noticeable than being a few seconds late to calm down
// (GDC-L1-AUDIO-0003: the failure is a jarring cut, and it is audible immediately).
using UnityEngine;

namespace SpaceGame.Presentation
{
    public sealed class MoodSense
    {
        private readonly float combatHoldSeconds;

        private float combatUntil = float.NegativeInfinity;

        /// <param name="combatHoldSeconds">How long the music stays in a fight after the last
        /// thing that was fighting stops.</param>
        public MoodSense(float combatHoldSeconds) =>
            this.combatHoldSeconds = Mathf.Max(combatHoldSeconds, 0f);

        /// <summary>True while the fight is still holding the score, whether or not it is over.</summary>
        public bool InFight(float time) => time < combatUntil;

        /// <summary>
        /// What the music should be at <paramref name="time"/>. Call once per sense, however often
        /// that is — the hold is a timestamp, so a caller that ticks four times a second and one
        /// that ticks sixty get the same answer.
        /// </summary>
        public MusicMood Read(in MoodSenses senses, float time)
        {
            if (senses.Threatened) combatUntil = time + combatHoldSeconds;

            // Before everything, including the fight that caused it. A player watching their own
            // death animation is not in a fight any more, whatever is still standing over them.
            if (senses.PlayerDead)
            {
                combatUntil = float.NegativeInfinity;
                return MusicMood.Death;
            }

            // The castle wins. Its garrison is most of the fighting in the game, so letting combat
            // take the music inside the walls would mean the castles — the one place the score is
            // asked to be heaviest — almost never sounded like themselves.
            if (senses.InZone && senses.ZoneHoldsThroughFights) return senses.ZoneMood;

            if (time < combatUntil) return MusicMood.Combat;

            return senses.InZone ? senses.ZoneMood : MusicMood.Explore;
        }
    }
}
