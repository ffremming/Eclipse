using System.IO;
using SpaceGame.World;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Puts the corrupted atmosphere into the built world: the skybox, the fog driver, and the ash
    /// drifting through it.
    /// <para>
    /// Split out of <see cref="NatureWorldBuilder"/> because the two answer different questions.
    /// That one decides where the land is; this one decides what it feels like to stand on it, and
    /// the look is retuned far more often than the terrain is.
    /// </para>
    /// <para>
    /// The scene is an output, so nothing here is hand-placed. The MATERIALS are not — they are
    /// assets, they survive a rebuild, and they are where the look is actually tuned. This tool
    /// creates them only when they are missing and never overwrites an existing one, so a rebuild
    /// cannot discard a tuning pass.
    /// </para>
    /// </summary>
    public static class AtmosphereSetup
    {
        private const string MaterialFolder = "Assets/Game/Art/Materials/Environment";
        private const string SkyMaterialPath = MaterialFolder + "/CorruptSkybox.mat";
        private const string AshMaterialPath = MaterialFolder + "/AshMote.mat";

        private const string SkyShader = "SpaceGame/CorruptSkybox";
        private const string AshShader = "SpaceGame/AshMote";

        /// <summary>How far the ash volume reaches around the camera, in metres.</summary>
        private const float AshVolumeRadius = 60f;

        /// <summary>Height of the ash volume, in metres.</summary>
        private const float AshVolumeHeight = 45f;

        /// <summary>Motes alive at once. High enough to read as air, low enough to stay cheap.</summary>
        private const int AshCount = 900;

        private const float AshSizeMin = 0.03f;
        private const float AshSizeMax = 0.14f;
        private const float AshDriftSpeed = 0.6f;
        private const float AshLifetime = 14f;

        /// <summary>
        /// What is left of the sun's light with the disc in the way: nothing at all.
        /// <para>
        /// Zero, not merely low. The eclipse is total, so the directional light contributes no
        /// light to the world — everything the player can see is the ambient trilight and whatever
        /// they are carrying, which is the point of the game. A sun with even a little intensity in
        /// it lights every surface on the island evenly and quietly undoes that.
        /// </para>
        /// <para>
        /// The colour is kept, and is not dead weight: it is the colour the light is still set to,
        /// so the moment <see cref="SpaceGame.Castle.Lightfall"/> starts raising the intensity the
        /// first light to reach the ground is the eclipse's red rather than a neutral white that
        /// belongs to no sky. <c>WorldAtmosphere</c> reads both off the light itself as the
        /// blend's starting point.
        /// </para>
        /// </summary>
        private static readonly Color SunColour = new Color(0.50f, 0.26f, 0.22f);
        private const float SunIntensity = 0f;

        public static void Build(Transform sun)
        {
            Material sky = EnsureMaterial(SkyMaterialPath, SkyShader);
            Material ash = EnsureMaterial(AshMaterialPath, AshShader);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun != null ? sun.GetComponent<Light>() : null;

            // Unity's own fog is left off. HeightFog replaces it — an exponential-with-height
            // integration reads as a valley filling up, which flat distance fog cannot do.
            RenderSettings.fog = false;

            DimTheSun(sun);
            BuildDriver(sun);
            BuildAsh(ash);
        }

        /// <summary>
        /// Loads a material, creating it from its shader the first time.
        /// <para>
        /// Never overwrites: once the material exists it is the authored artefact and this tool has
        /// no business touching it. That is what lets the world be rebuilt after the look has been
        /// tuned without losing the tuning.
        /// </para>
        /// </summary>
        private static Material EnsureMaterial(string path, string shaderName)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Atmosphere] Shader '{shaderName}' not found. " +
                               "The world will build without its atmosphere.");
                return null;
            }

            Directory.CreateDirectory(MaterialFolder);
            Material created = new Material(shader);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void DimTheSun(Transform sun)
        {
            if (sun == null) return;

            Light light = sun.GetComponent<Light>();
            if (light == null) return;

            light.color = SunColour;
            light.intensity = SunIntensity;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// The component that writes the fog, sky and mote-lighting globals.
        /// <para>
        /// Without it those uniforms stay at zero, and HeightFog divides by one of them — so the
        /// failure is not "no fog" but a screen washed to fog colour. This object is what makes the
        /// atmosphere shaders work at all.
        /// </para>
        /// </summary>
        private static void BuildDriver(Transform sun)
        {
            GameObject driver = new GameObject("Atmosphere");
            WorldAtmosphere atmosphere = driver.AddComponent<WorldAtmosphere>();

            // Point the sky's occluded glow at the same direction the light comes from, through the
            // serialized field rather than by copying the rotation, so moving the sun later moves
            // the glow with it.
            SerializedObject serialized = new SerializedObject(atmosphere);
            serialized.FindProperty("sunDirection").objectReferenceValue = sun;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The ash. A slab of slow motes around the camera, simulated in world space so flying
        /// through them feels like passing through air that was already there rather than dragging
        /// a cloud along.
        /// </summary>
        private static void BuildAsh(Material material)
        {
            if (material == null) return;

            GameObject ashObject = new GameObject("Ash");
            ParticleSystem system = ashObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = AshLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(AshDriftSpeed * 0.3f, AshDriftSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(AshSizeMin, AshSizeMax);
            main.startColor = Color.white;
            main.maxParticles = AshCount;
            main.gravityModifier = 0.01f;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = AshCount / AshLifetime;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(AshVolumeRadius * 2f, AshVolumeHeight, AshVolumeRadius * 2f);

            // Noise rather than velocity: ash should wander, not stream. A constant velocity reads
            // as wind, and wind implies weather, which implies a world that still works.
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;

            ParticleSystemRenderer renderer = ashObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
