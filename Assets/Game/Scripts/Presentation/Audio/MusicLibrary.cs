using System;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Every piece of music in the game, filed by what it is for.
    /// <para>
    /// One asset rather than a list of clips on each <see cref="MusicDirector"/>, because the menu
    /// scene and the world scene each have a director of their own and both should be drawing on
    /// the same score. Two lists would let the menu play a fight.
    /// </para>
    /// <para>
    /// Built by <c>Tools &gt; Eclipse &gt; Audio &gt; Build Music Library</c>, which reads the clip
    /// folder and files each clip by the mood its name starts with. Editing it by hand afterwards
    /// is fine — the tunables are the point of it being an asset — but a rebuild re-files the clips.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Music Library", fileName = "MusicLibrary")]
    public sealed class MusicLibrary : ScriptableObject
    {
        [Tooltip("One shelf per mood. A mood with no shelf, or a shelf with no clips, plays " +
                 "nothing — which is a legitimate way to leave a mood silent.")]
        [SerializeField] private MusicShelf[] shelves = Array.Empty<MusicShelf>();

        /// <summary>
        /// The shelf for a mood, or null when that mood has nothing to play. Callers are expected
        /// to cope with null rather than being handed an empty shelf that pretends to work.
        /// </summary>
        public MusicShelf Shelf(MusicMood mood)
        {
            if (shelves == null) return null;

            foreach (MusicShelf shelf in shelves)
            {
                if (shelf != null && shelf.Mood == mood && shelf.Playable.Length > 0)
                    return shelf;
            }

            return null;
        }
    }
}
