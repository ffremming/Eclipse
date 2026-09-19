// The four evasive moves the dash button can produce.
//
// The order matters: it is the DodgeIndex the Goblin controller's Any-State transitions compare
// against, so renumbering here silently repoints every dodge in the animator.
namespace SpaceGame.Characters
{
    public enum DodgeMove
    {
        Dodge = 0,
        DodgeBack = 1,
        Roll = 2,
        Slide = 3,
    }
}
