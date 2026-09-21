using System;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// One thing a stopped player can do: a word, and what pressing it runs.
    /// </summary>
    public readonly struct StopScreenChoice
    {
        public StopScreenChoice(string label, Action chosen)
        {
            Label = label;
            Chosen = chosen;
        }

        /// <summary>What the button says. Shown as written.</summary>
        public string Label { get; }

        /// <summary>What it does.</summary>
        public Action Chosen { get; }
    }
}
