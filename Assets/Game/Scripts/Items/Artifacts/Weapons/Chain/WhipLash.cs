using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>
    /// The timeline of one lash: wind back, throw forward, reel in.
    /// <para>
    /// It owns only what the hand does. It says how much rope is paid out and where the orb is being
    /// drawn to; it never touches a link. The chain's shape, and the crack when it runs out of
    /// rope, come from <see cref="ChainRope"/>. Keeping the two apart is what lets the same rope
    /// hang, swing and get dragged on the ground with no lash running at all.
    /// </para>
    /// </summary>
    public sealed class WhipLash
    {
        public enum Phase
        {
            Idle,
            WindUp,
            Extend,
            Recover
        }

        private readonly WhipLashTuning tuning;

        private Vector3 forward = Vector3.forward;
        private float elapsed;
        private float windUpEnd;
        private float extendEnd;
        private float total;
        private float progress;

        public WhipLash(WhipLashTuning tuning)
        {
            this.tuning = tuning;
        }

        public Phase Current { get; private set; }

        public bool Active => Current != Phase.Idle;

        /// <summary>The way the current lash was thrown: level, unit length.</summary>
        public Vector3 Direction => forward;

        /// <summary>
        /// Start a lash along <paramref name="direction"/> (level, unit length), taking
        /// <paramref name="duration"/> seconds in all.
        /// </summary>
        public void Begin(Vector3 direction, float duration)
        {
            forward = direction;
            elapsed = 0f;

            float shares = tuning.WindUpShare + tuning.ExtendShare + tuning.RecoverShare;
            total = duration;
            windUpEnd = duration * tuning.WindUpShare / shares;
            extendEnd = duration * (tuning.WindUpShare + tuning.ExtendShare) / shares;

            Locate();
        }

        public void Advance(float dt)
        {
            if (!Active) return;

            elapsed += dt;
            Locate();
        }

        /// <summary>How much rope is out right now, in metres.</summary>
        public float PaidOutLength
        {
            get
            {
                switch (Current)
                {
                    case Phase.WindUp:
                        return Mathf.Lerp(tuning.RestLength, tuning.WindUpLength, Mathf.SmoothStep(0f, 1f, progress));
                    case Phase.Extend:
                        return Mathf.Lerp(tuning.WindUpLength, tuning.FullLength, progress);
                    case Phase.Recover:
                        return Mathf.Lerp(tuning.FullLength, tuning.RestLength, Mathf.SmoothStep(0f, 1f, progress));
                    default:
                        return tuning.RestLength;
                }
            }
        }

        /// <summary>
        /// Where the hand is drawing the orb, given where the holder stands. Recovery and idle draw
        /// it nowhere: the orb is let go, and the chain being reeled in is what brings it home.
        /// </summary>
        public TipPull TipPullFor(Vector3 holder, float dt)
        {
            switch (Current)
            {
                case Phase.WindUp:
                    return new TipPull(WindUpPointFor(holder), Fraction(tuning.WindUpPull, dt));
                case Phase.Extend:
                    Vector3 strike = holder + forward * tuning.FullLength + Vector3.up * tuning.StrikeHeight;
                    return new TipPull(Vector3.Lerp(WindUpPointFor(holder), strike, progress),
                                       Fraction(tuning.LashPull, dt));
                default:
                    return default;
            }
        }

        private Vector3 WindUpPointFor(Vector3 holder)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 offset = tuning.WindUpPoint;
            return holder + right * offset.x + Vector3.up * offset.y + forward * offset.z;
        }

        /// <summary>
        /// The share of the gap one step closes, for a pull of <paramref name="rate"/> per second.
        /// Exponential rather than <c>rate * dt</c>, so a hard pull never overshoots the target.
        /// </summary>
        private static float Fraction(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);

        private void Locate()
        {
            if (elapsed >= total)
            {
                Current = Phase.Idle;
                progress = 0f;
            }
            else if (elapsed < windUpEnd)
            {
                Current = Phase.WindUp;
                progress = elapsed / windUpEnd;
            }
            else if (elapsed < extendEnd)
            {
                Current = Phase.Extend;
                progress = (elapsed - windUpEnd) / (extendEnd - windUpEnd);
            }
            else
            {
                Current = Phase.Recover;
                progress = (elapsed - extendEnd) / (total - extendEnd);
            }
        }
    }
}
