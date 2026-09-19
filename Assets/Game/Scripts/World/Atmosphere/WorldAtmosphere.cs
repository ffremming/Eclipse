using UnityEngine;

namespace SpaceGame.World
{
    /// <summary>
    /// The one place the world's atmosphere is tuned.
    /// <para>
    /// <see cref="SpaceGame"/>'s height fog, corrupt skybox and ash motes all read global shader
    /// uniforms rather than per-material ones, so one of these somewhere in the scene drives every
    /// one of them at once. That is deliberate: fog colour and the skybox's horizon haze have to be
    /// the same colour or distant terrain ends at a visible curtain with a different sky behind it,
    /// and the only way to guarantee they agree is to have a single owner write both.
    /// </para>
    /// <para>
    /// Without one of these the fog uniforms are all zero, which is not "no fog" — HeightFog
    /// divides by the falloff, so a missing driver leaves the shader dividing by zero. A scene with
    /// this atmosphere needs one, the same way a scene with the imported plants needs a
    /// <c>VegetationWind</c>.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WorldAtmosphere : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_AirFogColor");
        private static readonly int FogDensityId = Shader.PropertyToID("_AirFogDensity");
        private static readonly int FogBaseHeightId = Shader.PropertyToID("_AirFogBaseHeight");
        private static readonly int FogFalloffId = Shader.PropertyToID("_AirFogFalloff");
        private static readonly int FogStartId = Shader.PropertyToID("_AirFogStart");
        private static readonly int SunDirId = Shader.PropertyToID("_CorruptSunDir");
        private static readonly int PlayerLightId = Shader.PropertyToID("_PlayerLightPos");

        [Header("Fog")]
        [Tooltip("Must match the skybox's haze colour, or distant terrain ends at a visible seam.")]
        [SerializeField] private Color fogColor = new Color(0.135f, 0.128f, 0.120f);

        [Tooltip("Extinction per metre at the base height. Small numbers: 0.01 is already thick. " +
                 "This is a hard cap on how far ANY light can be seen — at 0.016 a torch is 80% " +
                 "swallowed by 100 metres no matter how bright it is, so raise it only once the " +
                 "lighting reads at the distance you want.")]
        [SerializeField] private float fogDensity = 0.0045f;

        [Tooltip("World height at which the fog reaches its full density.")]
        [SerializeField] private float fogBaseHeight = 6f;

        [Tooltip("Reciprocal scale height. Higher values keep the fog in a thinner layer.")]
        [SerializeField] private float fogFalloff = 0.035f;

        [Tooltip("Metres of clear air in front of the eye before fog starts to accumulate.")]
        [SerializeField] private float fogStart = 12f;

        [Header("Ambient")]
        [Tooltip("Light that arrives from nowhere. Keep this very low — the point of the world is " +
                 "that the player's own light is the only light worth having.")]
        [SerializeField] private Color ambientSky = new Color(0.035f, 0.037f, 0.044f);
        [SerializeField] private Color ambientEquator = new Color(0.022f, 0.021f, 0.020f);
        [SerializeField] private Color ambientGround = new Color(0.010f, 0.009f, 0.009f);

        [Header("Sky")]
        [Tooltip("Where the occluded glow sits in the sky. Rotating this transform moves it. " +
                 "Leave empty to use this object's own forward.")]
        [SerializeField] private Transform sunDirection;

        [Header("The player's light")]
        [Tooltip("What the ash motes catch their rim from. Assigned at runtime by the carried " +
                 "light; this field is for previewing the effect in the editor.")]
        [SerializeField] private Transform playerLight;

        [Tooltip("How far the motes react to the player's light, in metres.")]
        [SerializeField] private float playerLightReach = 14f;

        private Transform trackedLight;
        private float trackedReach;

        /// <summary>
        /// Points the mote-lighting at a light source, or clears it when <paramref name="light"/>
        /// is null.
        /// <para>
        /// Called by whatever the player is currently carrying rather than discovered by a search,
        /// so putting the lantern away stops the motes reacting on the same frame instead of on
        /// whichever frame the next search happens to run.
        /// </para>
        /// </summary>
        public void TrackLight(Transform light, float reach)
        {
            trackedLight = light;
            trackedReach = reach;
        }

        private void OnEnable()
        {
            trackedLight = playerLight;
            trackedReach = playerLightReach;
            Apply();
        }

        private void OnDisable()
        {
            // Leave the motes unlit rather than frozen around a light that is no longer driven.
            Shader.SetGlobalVector(PlayerLightId, Vector4.zero);
        }

        private void LateUpdate()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // So the inspector sliders move the world while it is being tuned, which is the only
            // way a look like this converges.
            if (isActiveAndEnabled)
            {
                trackedReach = playerLightReach;
                Apply();
            }
        }
#endif

        /// <summary>Writes every global and the scene's ambient in one place.</summary>
        private void Apply()
        {
            Shader.SetGlobalColor(FogColorId, fogColor);
            Shader.SetGlobalFloat(FogDensityId, fogDensity);
            Shader.SetGlobalFloat(FogBaseHeightId, fogBaseHeight);

            // Guarded because HeightFog divides by this. A zero here is a division by zero in the
            // shader, which shows up as the whole screen turning to fog colour rather than as an
            // error anyone can trace back to this field.
            Shader.SetGlobalFloat(FogFalloffId, Mathf.Max(fogFalloff, 1e-4f));
            Shader.SetGlobalFloat(FogStartId, Mathf.Max(fogStart, 0f));

            Vector3 sunward = sunDirection != null ? -sunDirection.forward : -transform.forward;
            Shader.SetGlobalVector(SunDirId, sunward.normalized);

            Vector4 light = trackedLight != null
                ? new Vector4(trackedLight.position.x, trackedLight.position.y,
                              trackedLight.position.z, Mathf.Max(trackedReach, 0f))
                : Vector4.zero;
            Shader.SetGlobalVector(PlayerLightId, light);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
        }
    }
}
