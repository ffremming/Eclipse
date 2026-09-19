using System.IO;
using SpaceGame.Gameplay;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the blades the enemies carry: the player's own weapon models, blackened, trailing a
    /// ribbon of pitch black and carrying an <see cref="Unlight"/> instead of a glow.
    /// <para>
    /// The same three models as <see cref="LightBladeBuilder"/>, on purpose. The player learns in
    /// the first minute what an axe's reach and timing are, and meeting that silhouette on an enemy
    /// says what is coming without a word of UI (<c>GDC-L1-FEEL-0004</c>). Making up a separate
    /// enemy weapon family would throw that away and teach the same lesson twice.
    /// </para>
    /// <para>
    /// These are props, not items. No <c>PickupableItem</c>, no inventory asset, no drop physics: an
    /// enemy's weapon is seated in its hand by <c>EnemyGear</c> and dies with it. What it does keep
    /// is an <see cref="ItemGrip"/>, because that is how any hand in this game — the player's or a
    /// creature's — is told where to close.
    /// </para>
    /// </summary>
    public static class DarkBladeBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Enemies/Weapons";
        private const string MaterialFolder = "Assets/Game/Art/Materials/Dark";

        private const string SlashShader = "SpaceGame/Dark/DarkSlash";
        private const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>
        /// The blade itself: not quite black, and rough rather than polished. A pure black,
        /// mirror-smooth blade picks up the sky as a highlight and reads as chrome; this one gives
        /// the eye a silhouette and nothing else, which is what leaves the streak doing the talking.
        /// </summary>
        private static readonly Color BladeColour = new Color(0.035f, 0.030f, 0.045f);
        private const float BladeSmoothness = 0.18f;

        // The dark an enemy carries, in the units Unlight documents — around thirteen times an
        // ordinary light's, because a negative colour goes through the sRGB curve's linear segment.
        // Set so a creature walking towards you puts the ground out ahead of it a little, and a
        // creature swinging at you takes the light off your own hands.
        private const float IdleStrength = 9f;
        private const float SwingStrength = 42f;
        private const float IdleRange = 3.5f;
        private const float SwingRange = 7f;

        /// <summary>
        /// How far past the blade's own tip the ribbon reaches, as a multiple of hilt-to-tip. Above
        /// 1 the streak overshoots the steel, which is what makes a swing read as bigger than the
        /// thing that threw it. Below the player's 1.6, whose anchors start at the wrist rather
        /// than at the hilt and so have a shorter span to multiply.
        /// </summary>
        private const float TrailOvershoot = 1.25f;

        /// <summary>
        /// Metres a second the tip must travel, in the creature's own frame, before the ribbon
        /// draws. Lower than the player's 4: a crumpy's arm is shorter and its swing slower, and at
        /// the player's threshold half of one would never register. The real gate is the strike —
        /// nothing draws at all outside the window a MeleeStrike opens — so this only has to tell
        /// the fast part of a swing from its windup.
        /// </summary>
        private const float SweepSpeed = 2.5f;

        [MenuItem("Tools/Eclipse/Items/Build Dark Blades")]
        private static void BuildAll()
        {
            Material slash = ItemBuilderKit.EnsureMaterial($"{MaterialFolder}/DarkSlash.mat", SlashShader);
            Material blade = ItemBuilderKit.EnsureMaterial($"{MaterialFolder}/DarkBlade.mat", LitShader,
                material =>
                {
                    material.SetColor("_BaseColor", BladeColour);
                    material.SetFloat("_Smoothness", BladeSmoothness);
                });

            if (slash == null || blade == null) return;

            // Trail lifetimes follow the light versions of the same models, so an enemy's streak
            // sweeps the same shape the player's does and the two can be told apart by what they do
            // to the frame rather than by their outline.
            Build("DarkKhopesh", BladeModels.Khopesh, slash, blade, trailLifetime: 0.24f);
            Build("DarkAxe", BladeModels.Axe, slash, blade, trailLifetime: 0.26f);

            AssetDatabase.SaveAssets();
            Debug.Log($"[DarkBlades] Built the khopesh and the axe into {PrefabFolder}.");
        }

        private static void Build(string name, BladeModel model, Material slash, Material blade,
                                  float trailLifetime)
        {
            GameObject root = new GameObject(name);
            MountedModel mounted = ModelMount.Mount(root.transform, model.Path, model.Length,
                                                    model.Handle, model.GripAlong);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = blade;

            DarkWeapon weapon = root.AddComponent<DarkWeapon>();
            ItemBuilderKit.Wire(weapon, "tipUnlight", BuildUnlight(mounted.Tip));
            ItemBuilderKit.WireFloat(weapon, "idleStrength", IdleStrength);
            ItemBuilderKit.WireFloat(weapon, "swingStrength", SwingStrength);
            ItemBuilderKit.WireFloat(weapon, "idleRange", IdleRange);
            ItemBuilderKit.WireFloat(weapon, "swingRange", SwingRange);

            Transform grip = AddGrip(root, model, mounted.GripPoint);
            AddSwingTrail(root, slash, grip, mounted.Tip, trailLifetime);

            Directory.CreateDirectory(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// The dark at the business end. Its Light's colour is an output — see <see cref="Unlight"/>
        /// — so it is created at zero and left for the component to write.
        /// </summary>
        private static Unlight BuildUnlight(Transform tip)
        {
            Light light = ItemBuilderKit.AddPointLight(tip, "Unlight", Color.black, IdleRange,
                                                       IdleStrength, LightShadows.None);

            Unlight unlight = light.gameObject.AddComponent<Unlight>();
            ItemBuilderKit.WireFloat(unlight, "strength", IdleStrength);
            ItemBuilderKit.WireFloat(unlight, "range", IdleRange);
            return unlight;
        }

        /// <summary>
        /// The streak the blade drags behind it. Its anchors are the two ends of the steel, which
        /// is the whole reason the trail rides the weapon rather than the creature: no bone sits at
        /// a blade's tip, and only the builder that mounted the model knows where that tip is.
        /// <para>
        /// The body frame and the strike are deliberately left empty. Neither exists yet — the
        /// creature that will swing this is only known once <c>EnemyGear</c> has put it in a hand —
        /// so <see cref="DarkSwingTrail"/> resolves both at runtime.
        /// </para>
        /// </summary>
        private static void AddSwingTrail(GameObject root, Material slash, Transform grip,
                                          Transform tip, float lifetime)
        {
            DarkSwingTrail trail = root.AddComponent<DarkSwingTrail>();
            ItemBuilderKit.Wire(trail, "baseAnchor", grip);
            ItemBuilderKit.Wire(trail, "tipAnchor", tip);
            ItemBuilderKit.Wire(trail, "material", slash);
            ItemBuilderKit.WireFloat(trail, "lengthScale", TrailOvershoot);
            ItemBuilderKit.WireFloat(trail, "lifetime", lifetime);
            ItemBuilderKit.WireFloat(trail, "sweepSpeed", SweepSpeed);
        }

        private static Transform AddGrip(GameObject root, BladeModel model, Vector3 gripPoint)
        {
            GameObject grip = new GameObject("Grip");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = gripPoint;

            ItemGrip itemGrip = root.AddComponent<ItemGrip>();
            ItemBuilderKit.Wire(itemGrip, "gripPoint", grip.transform);
            ItemBuilderKit.WireEnum(itemGrip, "holdStyle", (int)ItemGrip.HoldStyle.OneHanded);
            ItemBuilderKit.WireFloat(itemGrip, "holdSize", model.Length);

            SerializedObject fields = new SerializedObject(itemGrip);
            SerializedFields.SetVector3(fields, "rotationOffset", model.HoldRotation);
            fields.ApplyModifiedPropertiesWithoutUndo();

            return grip.transform;
        }
    }
}
