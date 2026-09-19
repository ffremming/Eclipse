using UnityEngine;
using SpaceGame.Castle;

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

        // The skybox properties the dawn moves. Named here rather than in DaylightProfile so that
        // every coupling to the CorruptSkybox shader lives in the one class that already owns the
        // agreement between the sky and the fog.
        private static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        private static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        private static readonly int HazeId = Shader.PropertyToID("_HazeColor");
        private static readonly int DiscId = Shader.PropertyToID("_DiscColor");
        private static readonly int RingIntensityId = Shader.PropertyToID("_RingIntensity");
        private static readonly int CoronaStrengthId = Shader.PropertyToID("_CoronaStrength");
        private static readonly int BloodSkyId = Shader.PropertyToID("_BloodSky");
        private static readonly int PallStrengthId = Shader.PropertyToID("_PallStrength");
        private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
        private static readonly int CloudBodyId = Shader.PropertyToID("_CloudBodyColor");
        private static readonly int CloudUndertoneId = Shader.PropertyToID("_CloudUndertoneStrength");

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

        [Header("Daylight")]
        [Tooltip("What the world becomes when the lighthouse is lit. Left empty, the world stays " +
                 "under the eclipse for good and DaylightBlend does nothing — which is the right " +
                 "state for a scene that has no castle in it.")]
        [SerializeField] private DaylightProfile daylight;

        [Tooltip("How much of the daylight has arrived, for previewing the end of the game in the " +
                 "editor. At runtime this is driven by Lightfall.")]
        [SerializeField, Range(0f, 1f)] private float daylightBlend;

        private Transform trackedLight;
        private float trackedReach;

        /// <summary>
        /// A runtime instance of the skybox, so the dawn does not write on the shared material
        /// asset. Without this a single playthrough permanently edits <c>CorruptSkybox.mat</c> —
        /// in the editor the change survives leaving play mode, and the world is then daylit the
        /// next time it is opened with no way to tell what did it.
        /// </summary>
        private Material skyInstance;
        private Material skySource;

        /// <summary>
        /// How much of the daylight is here: 0 under the eclipse, 1 once the lighthouse has
        /// finished lighting the world. Driven by <see cref="SpaceGame.Castle.Lightfall"/>.
        /// </summary>
        public float DaylightBlend
        {
            get => daylightBlend;
            set => daylightBlend = Mathf.Clamp01(value);
        }

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

#if UNITY_EDITOR
            // The dawn's skybox copy must never be what gets written into the scene. RenderSettings
            // .skybox is scene data, so a save taken while the copy is installed persists a
            // throwaway material — and the next session instances a copy OF THE COPY, which is how
            // "CorruptSkybox (dawn) (dawn)" appears and how the authored material quietly stops
            // being the one the world uses. Putting the real one back just before the write, and
            // letting the next frame re-apply the dawn, keeps the scene honest either way.
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= ReleaseSkyBeforeSave;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += ReleaseSkyBeforeSave;
#endif

            Apply();
        }

#if UNITY_EDITOR
        private void ReleaseSkyBeforeSave(UnityEngine.SceneManagement.Scene scene, string path) =>
            ReleaseSky();
#endif

        private void OnDisable()
        {
            // Leave the motes unlit rather than frozen around a light that is no longer driven.
            Shader.SetGlobalVector(PlayerLightId, Vector4.zero);

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= ReleaseSkyBeforeSave;
#endif

            ReleaseSky();
        }

        /// <summary>
        /// Hands the scene its own skybox back and throws the dawn's copy away.
        /// <para>
        /// Called when the atmosphere is switched off, when the dawn returns to zero, and in the
        /// editor immediately before a scene is saved. All three are the same requirement: the
        /// authored <c>CorruptSkybox.mat</c> is what the scene must reference, and the copy exists
        /// only for as long as something is actually mid-dawn.
        /// </para>
        /// </summary>
        private void ReleaseSky()
        {
            if (skyInstance == null) return;

            if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = skySource;

            if (Application.isPlaying) Destroy(skyInstance);
            else DestroyImmediate(skyInstance);

            skyInstance = null;
            skySource = null;
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
            // The eclipse end of every value is the field above; the daylight end is the profile.
            // Blending here rather than in Lightfall is what keeps this class the single owner its
            // summary describes — two writers would mean the fog and the sky could be mid-dawn by
            // different amounts on the same frame, which is exactly the seam this class exists to
            // prevent.
            float dawn = daylight != null ? daylightBlend : 0f;

            Color litFogColor = dawn > 0f ? Color.Lerp(fogColor, daylight.FogColor, dawn) : fogColor;

            Shader.SetGlobalColor(FogColorId, litFogColor);
            Shader.SetGlobalFloat(FogDensityId, Blend(fogDensity, daylight != null ? daylight.FogDensity : 0f, dawn));
            Shader.SetGlobalFloat(FogBaseHeightId, Blend(fogBaseHeight, daylight != null ? daylight.FogBaseHeight : 0f, dawn));

            // Guarded because HeightFog divides by this. A zero here is a division by zero in the
            // shader, which shows up as the whole screen turning to fog colour rather than as an
            // error anyone can trace back to this field.
            Shader.SetGlobalFloat(FogFalloffId,
                Mathf.Max(Blend(fogFalloff, daylight != null ? daylight.FogFalloff : 0f, dawn), 1e-4f));
            Shader.SetGlobalFloat(FogStartId,
                Mathf.Max(Blend(fogStart, daylight != null ? daylight.FogStart : 0f, dawn), 0f));

            Vector3 sunward = sunDirection != null ? -sunDirection.forward : -transform.forward;
            Shader.SetGlobalVector(SunDirId, sunward.normalized);

            Vector4 light = trackedLight != null
                ? new Vector4(trackedLight.position.x, trackedLight.position.y,
                              trackedLight.position.z, Mathf.Max(trackedReach, 0f))
                : Vector4.zero;
            Shader.SetGlobalVector(PlayerLightId, light);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            if (dawn > 0f)
            {
                RenderSettings.ambientSkyColor = Color.Lerp(ambientSky, daylight.AmbientSky, dawn);
                RenderSettings.ambientEquatorColor = Color.Lerp(ambientEquator, daylight.AmbientEquator, dawn);
                RenderSettings.ambientGroundColor = Color.Lerp(ambientGround, daylight.AmbientGround, dawn);
                ApplySun(dawn);
                ApplySky(dawn, litFogColor);
            }
            else
            {
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;

                // Nothing is mid-dawn, so neither the sky copy nor the raised sun has any reason
                // to exist. Both have to be put back, not just the sky: the sun is a Light in the
                // scene and whatever the last dawn wrote to it STAYS there, so previewing the end
                // of the game in the editor left the world permanently daylit — a sun at 2.1 in a
                // scene whose blend reads zero, with nothing to say what had raised it.
                RestoreSun();
                ReleaseSky();
            }
        }

        private static float Blend(float eclipse, float day, float dawn) =>
            dawn > 0f ? Mathf.Lerp(eclipse, day, dawn) : eclipse;

        /// <summary>
        /// Brings the sun up. Its eclipse colour and intensity are read from the light itself the
        /// first time rather than stored here, so <c>AtmosphereSetup</c> stays the one place that
        /// decides how dim the occluded sun is.
        /// </summary>
        private void ApplySun(float dawn)
        {
            Light sun = sunDirection != null ? sunDirection.GetComponent<Light>() : null;
            if (sun == null) return;

            if (!sunCaptured)
            {
                eclipseSunColour = sun.color;
                eclipseSunIntensity = sun.intensity;
                sunCaptured = true;
            }

            sun.color = Color.Lerp(eclipseSunColour, daylight.SunColour, dawn);
            sun.intensity = Mathf.Lerp(eclipseSunIntensity, daylight.SunIntensity, dawn);
        }

        /// <summary>
        /// Puts the sun back to the eclipse values the first dawn captured, and forgets them.
        /// <para>
        /// Forgetting matters as much as restoring. The capture is the light's own state before
        /// any dawn touched it, so holding on to it across a restore would mean a later retune of
        /// the sun in the Inspector was overwritten by a number captured before the change.
        /// </para>
        /// </summary>
        private void RestoreSun()
        {
            if (!sunCaptured) return;

            Light sun = sunDirection != null ? sunDirection.GetComponent<Light>() : null;
            if (sun != null)
            {
                sun.color = eclipseSunColour;
                sun.intensity = eclipseSunIntensity;
            }

            sunCaptured = false;
        }

        /// <summary>
        /// Ends the eclipse in the sky, on a private copy of the skybox material.
        /// <para>
        /// The haze is assigned the fog colour rather than lerped separately, which makes the
        /// agreement this class exists to guarantee structural instead of a matter of two profiles
        /// being filled in consistently. It was previously only ever true because both were tuned
        /// by hand to the same value.
        /// </para>
        /// </summary>
        private void ApplySky(float dawn, Color fogNow)
        {
            Material sky = SkyInstance();
            if (sky == null) return;

            sky.SetColor(ZenithId, Color.Lerp(skySource.GetColor(ZenithId), daylight.Zenith, dawn));
            sky.SetColor(HorizonId, Color.Lerp(skySource.GetColor(HorizonId), daylight.Horizon, dawn));
            sky.SetColor(HazeId, fogNow);
            sky.SetColor(DiscId, Color.Lerp(skySource.GetColor(DiscId), daylight.Disc, dawn));

            sky.SetFloat(RingIntensityId, Mathf.Lerp(skySource.GetFloat(RingIntensityId), daylight.RingIntensity, dawn));
            sky.SetFloat(CoronaStrengthId, Mathf.Lerp(skySource.GetFloat(CoronaStrengthId), daylight.CoronaStrength, dawn));
            sky.SetFloat(BloodSkyId, Mathf.Lerp(skySource.GetFloat(BloodSkyId), daylight.BloodSky, dawn));
            sky.SetFloat(PallStrengthId, Mathf.Lerp(skySource.GetFloat(PallStrengthId), daylight.PallStrength, dawn));
            sky.SetFloat(CloudCoverageId, Mathf.Lerp(skySource.GetFloat(CloudCoverageId), daylight.CloudCoverage, dawn));
            sky.SetColor(CloudBodyId, Color.Lerp(skySource.GetColor(CloudBodyId), daylight.CloudBody, dawn));
            sky.SetFloat(CloudUndertoneId,
                Mathf.Lerp(skySource.GetFloat(CloudUndertoneId), daylight.CloudUndertoneStrength, dawn));
        }

        /// <summary>
        /// The private copy of the scene's skybox, made on first use, along with the untouched
        /// original the blend reads its eclipse end from.
        /// </summary>
        private Material SkyInstance()
        {
            Material current = RenderSettings.skybox;
            if (current == null) return null;

            // Remade when the scene's skybox is swapped out from under us, so this does not go on
            // writing to a copy of a material nothing is rendering any more.
            if (skyInstance != null && current == skyInstance) return skyInstance;

            skySource = current;
            skyInstance = new Material(current) { name = current.name + " (dawn)" };
            RenderSettings.skybox = skyInstance;
            return skyInstance;
        }

        private bool sunCaptured;
        private Color eclipseSunColour;
        private float eclipseSunIntensity;
    }
}
