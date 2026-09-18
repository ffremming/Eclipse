// The single entry point for one thing hurting another.
//
// Damage is where "fighters acting on each other" actually lives, and routing every caller through
// one function is what keeps the rules — who has health, what a hit does to something that has
// none — in one place instead of in every weapon.
using UnityEngine;

namespace SpaceGame.Gameplay
{
    public static class Damage
    {
        /// <summary>
        /// Hurt <paramref name="target"/> for <paramref name="amount"/>, wherever this is called
        /// from. <paramref name="source"/> is whoever dealt it, for hit reactions and kill credit.
        /// </summary>
        public static void Apply(GameObject target, int amount, Transform source = null)
        {
            if (target == null || amount <= 0) return;

            HealthComponent health = target.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                health.Damage(amount, source);
                return;
            }

            // Not everything damageable owns a HealthComponent — destructible props implement
            // IDamageable directly.
            if (target.GetComponentInParent<IDamageable>() is { } damageable && damageable.Alive)
                damageable.Damage(amount);
        }

        /// <summary>Convenience for the common "I hit this collider" shape.</summary>
        public static void Apply(Component target, int amount, Transform source = null)
        {
            if (target != null) Apply(target.gameObject, amount, source);
        }
    }
}
