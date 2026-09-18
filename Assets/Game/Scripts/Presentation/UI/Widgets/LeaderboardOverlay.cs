using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// A board held open with Tab during play and pinned open by whichever screen ends the match.
    /// Subclasses say which columns and which rows; this owns the canvas, the key and the pin.
    ///
    /// Sorting order 1100: above MatchResultUI (1000), whose panel is a full-screen backdrop that
    /// would otherwise hide the table entirely when pinned. The result screen's headline and
    /// button are anchored to the top and bottom edges, so nothing actually overlaps the table.
    /// </summary>
    public abstract class LeaderboardOverlay : MonoBehaviour
    {
        private const int SortingOrder = 1100;

        private LeaderboardTable table;
        private bool pinned;
        private bool dirty = true;

        protected LeaderboardTable Table => table;

        protected abstract string CanvasName { get; }
        protected abstract int MaxRows { get; }
        protected abstract LeaderboardTable.Column[] Columns { get; }

        /// <summary>Fill the table from whatever the subclass watches. Called only while visible and dirty.</summary>
        protected abstract void Rebuild();

        protected static T Ensure<T>() where T : LeaderboardOverlay
        {
            T existing = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject(typeof(T).Name);
            return go.AddComponent<T>();
        }

        /// <summary>Keeps the final standings up without holding Tab.</summary>
        public void SetPinned(bool value)
        {
            pinned = value;
            dirty = true;
        }

        protected void MarkDirty() => dirty = true;

        private void Awake()
        {
            table = new LeaderboardTable(transform, CanvasName, SortingOrder, Columns, MaxRows);
            table.SetVisible(false);
        }

        private void Update()
        {
            bool held = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            bool shouldShow = pinned || held;

            table.SetVisible(shouldShow);

            // Rebuilt only while on screen: nobody can see a table that is hidden, and scores
            // change continuously in a match with bots respawning or racers mid-course.
            if (shouldShow && dirty)
            {
                Rebuild();
                dirty = false;
            }
        }
    }
}
