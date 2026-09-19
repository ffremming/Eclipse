using System;
using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>The shape of one lash. Serialized on the weapon so it is tuned in the Inspector.</summary>
    [Serializable]
    public sealed class WhipLashTuning
    {
        [Header("Reach")]
        [Tooltip("Rope paid out while the chain hangs at the holder's side, in metres.")]
        [SerializeField] private float restLength = 1.4f;

        [Tooltip("Rope paid out at the far end of a lash, in metres. This is the weapon's reach.")]
        [SerializeField] private float fullLength = 4.5f;

        [Tooltip("How much of the full length is already out at the top of the wind-up, 0 to 1. " +
                 "The rest is paid out during the lash itself, which is what makes it extend.")]
        [SerializeField] private float windUpLengthShare = 0.6f;

        [Header("Path")]
        [Tooltip("Where the orb is drawn to during the wind-up, relative to the holder: x is to " +
                 "their right, y up, z forward. Behind and above, so the lash comes over the shoulder.")]
        [SerializeField] private Vector3 windUpPoint = new Vector3(0.25f, 1.5f, -0.5f);

        [Tooltip("How high above the holder's feet the orb is drawn to at the end of the lash, in " +
                 "metres.")]
        [SerializeField] private float strikeHeight = 0.55f;

        [Header("Timing")]
        [Tooltip("Relative length of the wind-up. Only the proportions matter: the three shares are " +
                 "scaled to fill the weapon's swing duration, so the chain and the light cannot " +
                 "disagree about how long a swing is.")]
        [SerializeField] private float windUpShare = 0.2f;

        [Tooltip("Relative length of the lash itself. Shorter is a harder crack.")]
        [SerializeField] private float extendShare = 0.3f;

        [Tooltip("Relative length of the recovery, while the chain is reeled back in.")]
        [SerializeField] private float recoverShare = 0.5f;

        [Header("Hand")]
        [Tooltip("How hard the orb is drawn to the wind-up point, per second.")]
        [SerializeField] private float windUpPull = 14f;

        [Tooltip("How hard the orb is drawn along the lash, per second. Lower lets the orb lag the " +
                 "hand and the chain bow behind it; higher is a stiffer, faster lash.")]
        [SerializeField] private float lashPull = 60f;

        public float RestLength => Mathf.Max(0.05f, restLength);
        public float FullLength => Mathf.Max(RestLength, fullLength);
        public float WindUpLength => Mathf.Lerp(RestLength, FullLength, Mathf.Clamp01(windUpLengthShare));
        public Vector3 WindUpPoint => windUpPoint;
        public float StrikeHeight => strikeHeight;
        public float WindUpShare => Mathf.Max(0.01f, windUpShare);
        public float ExtendShare => Mathf.Max(0.01f, extendShare);
        public float RecoverShare => Mathf.Max(0.01f, recoverShare);
        public float WindUpPull => Mathf.Max(0f, windUpPull);
        public float LashPull => Mathf.Max(0f, lashPull);
    }
}
