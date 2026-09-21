// What the score does with a world that keeps changing its mind.
//
// Every one of these is a thing that is inaudible in a diff and obvious in play: a fight that keeps
// dropping the music because an enemy blinked behind a rock, a castle that stops sounding like a
// castle the moment it defends itself, a death sting that arrives under a fight that is still going
// on. The rule has memory in it, and memory is the part that goes wrong quietly.
//
// In Editor/ rather than beside the other EditMode tests: MoodSense lives in Assembly-CSharp, which
// SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Presentation;

namespace SpaceGame.EditorTools
{
    public class MusicMoodTests
    {
        private const float Hold = 10f;

        private static MoodSenses Calm() => new MoodSenses();

        private static MoodSenses Fight() => new MoodSenses { Threatened = true };

        private static MoodSenses Castle(bool threatened = false, bool holds = true) =>
            new MoodSenses
            {
                InZone = true,
                ZoneMood = MusicMood.Castle,
                ZoneHoldsThroughFights = holds,
                Threatened = threatened,
            };

        [Test]
        public void OpenGroundWithNothingHappeningIsExploration()
        {
            var sense = new MoodSense(Hold);

            Assert.AreEqual(MusicMood.Explore, sense.Read(Calm(), 0f));
        }

        [Test]
        public void SomethingHuntingThePlayerTakesTheMusic()
        {
            var sense = new MoodSense(Hold);

            Assert.AreEqual(MusicMood.Combat, sense.Read(Fight(), 0f));
        }

        [Test]
        public void TheFightKeepsTheMusicAfterTheLastEnemyStops()
        {
            var sense = new MoodSense(Hold);
            sense.Read(Fight(), 0f);

            Assert.AreEqual(MusicMood.Combat, sense.Read(Calm(), Hold - 0.1f),
                            "still inside the hold, so the score has not noticed the lull");
            Assert.AreEqual(MusicMood.Explore, sense.Read(Calm(), Hold + 0.1f),
                            "and lets go once the hold has run out");
        }

        [Test]
        public void EverySightingPushesTheHoldForwardRatherThanStartingItOver()
        {
            var sense = new MoodSense(Hold);
            sense.Read(Fight(), 0f);
            sense.Read(Fight(), 8f);

            Assert.AreEqual(MusicMood.Combat, sense.Read(Calm(), 17f),
                            "the hold runs from the last sighting, not the first");
        }

        [Test]
        public void ACastleKeepsItsOwnMusicThroughItsGarrison()
        {
            var sense = new MoodSense(Hold);

            Assert.AreEqual(MusicMood.Castle, sense.Read(Castle(threatened: true), 0f));
        }

        [Test]
        public void APlaceThatDoesNotHoldHandsOverToTheFight()
        {
            var sense = new MoodSense(Hold);

            Assert.AreEqual(MusicMood.Combat,
                            sense.Read(Castle(threatened: true, holds: false), 0f));
        }

        [Test]
        public void WalkingOutOfACastleMidFightFindsTheFightStillWaiting()
        {
            var sense = new MoodSense(Hold);
            sense.Read(Castle(threatened: true), 0f);

            Assert.AreEqual(MusicMood.Combat, sense.Read(Calm(), 1f),
                            "the hold was being kept up inside the castle even though the castle " +
                            "was what was playing");
        }

        [Test]
        public void DyingBeatsEverythingIncludingTheCastleStandingOverYou()
        {
            var sense = new MoodSense(Hold);
            MoodSenses dead = Castle(threatened: true);
            dead.PlayerDead = true;

            Assert.AreEqual(MusicMood.Death, sense.Read(dead, 0f));
        }

        [Test]
        public void StandingBackUpDoesNotWalkIntoTheFightTheDeathInterrupted()
        {
            var sense = new MoodSense(Hold);
            MoodSenses dead = Fight();
            dead.PlayerDead = true;

            sense.Read(dead, 0f);

            Assert.AreEqual(MusicMood.Explore, sense.Read(Calm(), 1f),
                            "death cleared the hold, so a revived player is not dropped straight " +
                            "back into combat music with nothing fighting them");
        }

        [Test]
        public void AZoneWithoutAFightIsSimplyWhatItSays()
        {
            var sense = new MoodSense(Hold);

            Assert.AreEqual(MusicMood.Castle, sense.Read(Castle(), 0f));
        }
    }
}
