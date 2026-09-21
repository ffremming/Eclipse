// What the music is currently about.
//
// Five moods and no more, one per thing the game can be doing that the score should answer:
// sitting at the menu, walking the island, being fought, standing in a castle, and having gone out.
// There is deliberately no "tension" or "near miss" — nothing in Eclipse reports either, and a mood
// nothing can raise is a shelf of clips nobody ever hears.
namespace SpaceGame.Presentation
{
    public enum MusicMood
    {
        /// <summary>The main menu, before a run exists.</summary>
        Menu,

        /// <summary>The island, with nothing hunting you. The game's resting state.</summary>
        Explore,

        /// <summary>Something is closing on you or swinging at you.</summary>
        Combat,

        /// <summary>Inside the grounds of a castle. The heaviest thing the score does.</summary>
        Castle,

        /// <summary>The light has gone out. One clip, once, and then quiet.</summary>
        Death,
    }
}
