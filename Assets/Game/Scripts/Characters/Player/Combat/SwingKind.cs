using System;

namespace SpaceGame.Characters
{
    /// <summary>
    /// Which of the player's attacks a press turned into. A flags enum so a listener can say
    /// "the sword ones" in one field — the choice is made from the body's context, not the press.
    /// </summary>
    [Flags]
    public enum SwingKind
    {
        None = 0,
        Slash = 1,
        JumpAttack = 2,
        Kick = 4,
    }
}
