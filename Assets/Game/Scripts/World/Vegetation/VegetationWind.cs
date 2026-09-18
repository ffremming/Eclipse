using UnityEngine;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Drives the wind the imported plant shaders bend to.
    /// <para>
    /// Those shaders read global uniforms rather than per-material ones, so one of these somewhere
    /// in the scene moves every plant in it. Without it the uniforms are all zero, which is not
    /// merely still: the gust wavelength divides, so a missing driver leaves the shader dividing by
    /// zero. A scene with these plants needs one.
    /// </para>
    /// <para>
    /// Runs in edit mode as well, so a field looks in the scene view the way it will in play.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VegetationWind : MonoBehaviour
    {
        private static readonly int DirectionId = Shader.PropertyToID("_BugWarWindDir");
        private static readonly int StrengthId = Shader.PropertyToID("_BugWarWindStrength");
        private static readonly int GustId = Shader.PropertyToID("_BugWarWindGust");
        private static readonly int TimeId = Shader.PropertyToID("_BugWarWindTime");

        [Tooltip("Compass direction the wind blows towards, in degrees from +Z.")]
        [SerializeField, Range(0f, 360f)] private float directionDegrees = 135f;

        [Tooltip("How hard it blows between gusts.")]
        [SerializeField, Range(0f, 2f)] private float strength = 0.55f;

        [Tooltip("Metres between one gust and the next.")]
        [SerializeField, Min(1f)] private float gustWavelength = 26f;

        [Tooltip("How fast a gust travels over the ground, in metres per second.")]
        [SerializeField, Min(0f)] private float gustSpeed = 9f;

        [Tooltip("How much harder a gust blows than the wind between gusts.")]
        [SerializeField, Range(0f, 2f)] private float gustAmplitude = 0.45f;

        [Tooltip("Higher makes gusts rarer and sharper; 1 is a smooth swell.")]
        [SerializeField, Range(1f, 8f)] private float gustSharpness = 2.5f;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Update() => Apply();

        /// <summary>
        /// Seconds of wind. In the editor outside play mode nothing advances <see cref="Time.time"/>,
        /// so the editor's own clock keeps the scene view breathing while a field is being tuned.
        /// </summary>
        private static float Clock()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.time;
        }

        private void Apply()
        {
            float radians = directionDegrees * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));

            Shader.SetGlobalVector(DirectionId, new Vector4(direction.x, 0f, direction.z, radians));
            Shader.SetGlobalFloat(StrengthId, strength);
            Shader.SetGlobalVector(GustId, new Vector4(gustWavelength, gustSpeed, gustAmplitude, gustSharpness));

            // A clock of its own rather than _Time, so the wind can be paused or scrubbed without
            // the rest of the frame's timing changing with it.
            Shader.SetGlobalFloat(TimeId, Clock());
        }
    }
}
