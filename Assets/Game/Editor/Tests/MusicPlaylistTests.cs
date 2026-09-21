// That every clip gets played, and none of them twice in a row.
//
// Both halves matter for the same reason: the score is fifteen minutes cut out of two recordings of
// one instrument, so a clip that never comes up is a piece of the game nobody hears, and a clip
// that comes up twice running is the one thing that makes a hand-cut score sound like a short loop.
// The seam between one shuffle and the next is where naive bags get it wrong, so it is tested
// directly.
using System;
using NUnit.Framework;
using SpaceGame.Presentation;

namespace SpaceGame.EditorTools
{
    public class MusicPlaylistTests
    {
        private const int Seed = 4242;

        [Test]
        public void EmptyShelfHasNothingToPlay()
        {
            var playlist = new MusicPlaylist(0, new Random(Seed));

            Assert.AreEqual(-1, playlist.Next());
        }

        [Test]
        public void ASingleClipIsPlayedForeverWithoutComplaint()
        {
            var playlist = new MusicPlaylist(1, new Random(Seed));

            for (int i = 0; i < 5; i++) Assert.AreEqual(0, playlist.Next());
        }

        [Test]
        public void EveryClipIsPlayedOnceBeforeAnyIsPlayedTwice()
        {
            const int count = 6;
            var playlist = new MusicPlaylist(count, new Random(Seed));
            var seen = new bool[count];

            for (int i = 0; i < count; i++)
            {
                int index = playlist.Next();
                Assert.IsFalse(seen[index], "clip " + index + " came up twice in one pass");
                seen[index] = true;
            }
        }

        [Test]
        public void NoClipFollowsItself_IncludingAcrossTheReshuffle()
        {
            const int count = 4;

            // Every seed, not one lucky one: the seam is only wrong for the seeds whose fresh bag
            // happens to open on the clip the last one closed with.
            for (int seed = 0; seed < 200; seed++)
            {
                var playlist = new MusicPlaylist(count, new Random(seed));
                int previous = -1;

                for (int i = 0; i < count * 5; i++)
                {
                    int index = playlist.Next();
                    Assert.AreNotEqual(previous, index,
                                       $"seed {seed} played clip {index} twice running");
                    previous = index;
                }
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameRunTwice()
        {
            var first = new MusicPlaylist(5, new Random(Seed));
            var second = new MusicPlaylist(5, new Random(Seed));

            for (int i = 0; i < 12; i++) Assert.AreEqual(first.Next(), second.Next());
        }
    }
}
