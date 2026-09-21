// The order a mood's clips come out in, with no Unity in it.
//
// A shuffle bag rather than "pick one at random": random picking plays the same clip twice in a row
// often enough to be noticed, and leaves a clip unheard for a whole run often enough to be a waste
// of the recording. A bag deals every clip once before reshuffling, so a player who stays in one
// mood long enough hears all of it, and never hears one twice while another is still waiting.
//
// The only thing the reshuffle has to be careful about is the seam: a fresh bag whose first clip is
// the one that just finished would repeat it across the boundary, which is the exact thing the bag
// exists to prevent. So the first card is swapped away when that happens.
using System;

namespace SpaceGame.Presentation
{
    public sealed class MusicPlaylist
    {
        private readonly int count;
        private readonly Random shuffler;
        private readonly int[] bag;

        private int dealt;
        private int last = -1;

        /// <param name="count">How many clips the shelf holds.</param>
        /// <param name="shuffler">Where the order comes from. Seeded by the caller so a test can
        /// ask for the same run twice.</param>
        public MusicPlaylist(int count, Random shuffler)
        {
            this.count = Math.Max(count, 0);
            this.shuffler = shuffler ?? new Random();

            bag = new int[this.count];
            dealt = this.count; // empty, so the first Next fills it
        }

        /// <summary>
        /// The next clip to play, or -1 when there are none at all. Never the same index twice
        /// running while the shelf holds more than one clip.
        /// </summary>
        public int Next()
        {
            if (count == 0) return -1;
            if (count == 1) return last = 0;

            if (dealt >= count) Refill();

            return last = bag[dealt++];
        }

        private void Refill()
        {
            for (int i = 0; i < count; i++) bag[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int j = shuffler.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            // Only possible across a refill, and only against the clip the previous bag ended on.
            if (bag[0] == last) (bag[0], bag[count - 1]) = (bag[count - 1], bag[0]);

            dealt = 0;
        }
    }
}
