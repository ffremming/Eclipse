// What an enemy is doing right now.
//
// Four states and no more. There is deliberately no Investigate and no Search: these enemies attack
// the moment they see you, so there is never a "something was here" to go and look at.
namespace SpaceGame.Enemies
{
    public enum EnemyState
    {
        /// <summary>Drifting between points inside its camp, unaware of anyone.</summary>
        Wander,

        /// <summary>Closing on the target.</summary>
        Chase,

        /// <summary>In range, standing its ground and swinging.</summary>
        Attack,

        /// <summary>Walking back to camp, having lost the target or run out its leash.</summary>
        ReturnHome,
    }
}
