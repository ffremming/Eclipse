using System.Collections.Generic;
using System.IO;
using SpaceGame.Castle;
using SpaceGame.Enemies;
using SpaceGame.Gameplay;
using SpaceGame.Items;
using SpaceGame.Vegetation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Stands the two castles on the island and wires each one up: its garrison, its locked doors,
    /// the beacon at the top of its tower and what lighting that beacon does to the world.
    /// <para>
    /// The scene is an output, the same as the rest of the world — see
    /// <see cref="NatureWorldBuilder"/>. Everything here is rebuilt from these numbers, so a castle
    /// is moved by editing <see cref="CastlePlacement"/> rather than by dragging it, and the hill
    /// under it moves with it.
    /// </para>
    /// <para>
    /// The two castles are the same three components with different numbers, which is what makes
    /// the first one a promise about the second: the player learns the whole shape of it —
    /// unlock, fight through, climb, strike the core, watch the light come — on a small castle
    /// that lights its own grounds, then does it again on one that lights the island
    /// (<c>GDC-L1-LEVEL-0004</c>).
    /// </para>
    /// </summary>
    public static class CastleBuilder
    {
        private const string ModelFolder = "Assets/Game/Art/Models/World/Castle";
        private const string AnchorPath = ModelFolder + "/CastleAnchors.json";
        private const string DaylightPath = "Assets/Game/Data/DaylightProfile.asset";

        /// <summary>
        /// The material a door leaf wears. A copy of the castle's stone that has its emission
        /// keyword ON — the keyword is a property of the MATERIAL, so a leaf left on the shared
        /// stone can be handed all the emission colour in the world and will not show a photon of
        /// it. Its own emission is black, so a door nothing is driving looks exactly like the wall
        /// beside it; <see cref="EntranceSignal"/> lights it through a property block.
        /// </summary>
        private const string DoorLeafMaterialPath =
            "Assets/Game/Art/Materials/World/Castle_DoorLeaf.mat";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// How far into the shelf the castle is sunk, in metres. The outer wall's lowest course
        /// starts a little below the model's own zero, so this only has to bury the difference —
        /// enough that the wall meets the ground with no gap, not so much that the gateway's sill
        /// rises above the courtyard.
        /// </summary>
        private const float Embed = 0.15f;

        /// <summary>
        /// Metres above the deck the orb of light forms at, and metres across it grows to.
        /// <para>
        /// Sized and hung so the orb takes the top of the tower without swallowing the player
        /// standing on the deck under it: its underside settles a couple of metres above their
        /// head. Inside it there is nothing to see — the orb is additive and single-sided, so a
        /// camera within it is a camera looking at the back of a surface that is not drawn.
        /// </para>
        /// </summary>
        private const float OrbHeight = 15f;
        private const float OrbDiameter = 24f;

        /// <summary>
        /// Metres out from the keep's door the player is set down when the light has come, and
        /// metres out the key they earned is left lying. The key is nearer, so it is between them
        /// and the castle rather than behind them.
        /// </summary>
        private const float ReturnStandoff = 6f;
        private const float RewardStandoff = 3.2f;

        /// <summary>
        /// How high off the shelf the key is dropped, in metres. It falls the rest of the way on
        /// its own drop physics, which is what settles it onto the actual ground rather than onto
        /// the flat the shelf is assumed to be.
        /// </summary>
        private const float RewardDropHeight = 1.2f;

        /// <summary>
        /// Half the side of the square the first castle lights. The brief is a 50 m square, and a
        /// point light is round, so its radius has to reach the corners rather than the edges.
        /// </summary>
        private const float LocalLightRadius = 25f * 1.415f;

        [MenuItem("Tools/Eclipse/World/Build Castles")]
        private static void BuildIntoOpenScene()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[Castle] No terrain in the open scene to stand a castle on.");
                return;
            }

            Vector3 size = terrain.terrainData.size;
            Build(terrain, size.x, size.y, NatureWorldSeed);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Mirrors <see cref="NatureWorldBuilder"/>'s seed, so building castles on their own puts
        /// the garrison where a full world build would have.
        /// </summary>
        private const int NatureWorldSeed = 20260918;

        public static void Build(Terrain terrain, float sizeMetres, float heightMetres, int seed)
        {
            Anchors anchors = LoadAnchors();
            if (anchors == null) return;

            Flatten(terrain, sizeMetres, seed);
            GiveAtmosphereItsDawn(EnsureDaylightProfile());

            InventoryItem wallKey = KeyBuilder.Load(KeyBuilder.WallKeyName);
            InventoryItem towerKey = KeyBuilder.Load(KeyBuilder.TowerKeyName);

            // The keep alone pays out the wall key, which is what gets the player through the full
            // castle's outer gate. The full castle pays out nothing: there is nothing after it.
            Raise(CastlePlacement.Keep, "CastleKeep", terrain, sizeMetres, heightMetres, seed,
                  anchors, towerKey, outerGateKey: null,
                  reward: wallKey, reach: Lightfall.Reach.Local);

            Raise(CastlePlacement.Full, "CastleFull", terrain, sizeMetres, heightMetres, seed,
                  anchors, towerKey, outerGateKey: wallKey,
                  reward: null, reach: Lightfall.Reach.World);

            ClearTheGrounds();
            Rebake(terrain);
        }

        /// <summary>
        /// Stamps the castles' shelves into the terrain that is already there.
        /// <para>
        /// Run on every castle build, including the one inside a full world build where
        /// <see cref="NatureWorldBuilder"/> has already stamped them. That is deliberate and it is
        /// free: a shelf's height comes from the NOISE at that spot, not from whatever the
        /// heightmap currently says, so applying it twice lands on the same number. The alternative
        /// — a flag saying "the terrain is already done" — is the sort of thing that is passed
        /// wrongly once and leaves a castle buried to its battlements with no clue why.
        /// </para>
        /// <para>
        /// The splat map is NOT recomputed here, so a castle built on its own leaves the shelf
        /// painted for the slope the land used to have. A full world build paints it correctly,
        /// because there the splat is derived after the heights.
        /// </para>
        /// </summary>
        private static void Flatten(Terrain terrain, float sizeMetres, int seed)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;

            data.SetHeights(0, 0, TerrainShape.Heights(resolution, sizeMetres, seed,
                                                       CastlePlacement.Sites(sizeMetres, seed)));
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// Hands the scene's atmosphere the profile it blends towards. WorldAtmosphere is the one
        /// owner of fog, ambient and sky — Lightfall only tells it how far through the dawn the
        /// world is — so the profile belongs there rather than on either castle.
        /// </summary>
        private static void GiveAtmosphereItsDawn(DaylightProfile daylight)
        {
            var atmosphere = Object.FindFirstObjectByType<SpaceGame.World.WorldAtmosphere>();
            if (atmosphere == null)
            {
                Debug.LogWarning("[Castle] The scene has no WorldAtmosphere, so lighting the " +
                                 "lighthouse will not lift the dark.");
                return;
            }

            ItemBuilderKit.Wire(atmosphere, "daylight", daylight);
        }

        /// <summary>Builds one castle, replacing any previous copy of it.</summary>
        private static void Raise(CastleStand stand, string modelName, Terrain terrain,
                                  float sizeMetres, float heightMetres, int seed, Anchors anchors,
                                  InventoryItem towerKey, InventoryItem outerGateKey,
                                  InventoryItem reward, Lightfall.Reach reach)
        {
            GameObject model = LoadModel(modelName);
            if (model == null) return;

            Replace(stand.Name);

            var root = new GameObject(stand.Name);

            // Parented to the terrain because its NavMeshSurface collects its own children — a
            // castle standing beside it is geometry the garrison's navmesh would not know about, so
            // they would walk through its walls.
            root.transform.SetParent(terrain.transform, worldPositionStays: false);
            root.transform.SetPositionAndRotation(
                stand.GroundPosition(sizeMetres, heightMetres, seed) + Vector3.down * Embed,
                stand.Rotation());

            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Structure";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            Transform deck = Anchor(root.transform, "TowerDeck", anchors.towerDeck);
            Transform beaconAt = Anchor(root.transform, "BeaconCore", anchors.towerBeacon);
            Transform returnTo = ReturnStand(root.transform, anchors.keepEntrance);
            Transform rewardAt = RewardStand(root.transform, anchors.keepEntrance);

            TowerBeacon beacon = BuildBeacon(beaconAt);
            Lightfall lightfall = BuildLightfall(root.transform, deck, reach);

            BuildEntrance(root.transform, body, "KeepEntrance", anchors.keepEntrance, towerKey,
                          KeyedEntrance.Answer.Carry, deck);

            if (outerGateKey != null)
                BuildEntrance(root.transform, body, "OuterGate", anchors.outerGate, outerGateKey,
                              KeyedEntrance.Answer.Swing, destination: null);

            BuildGarrison(root.transform, stand, terrain, seed, towerKey);

            var encounter = root.AddComponent<CastleEncounter>();
            ItemBuilderKit.Wire(encounter, "beacon", beacon);
            ItemBuilderKit.Wire(encounter, "lightfall", lightfall);
            ItemBuilderKit.Wire(encounter, "returnTo", returnTo);
            ItemBuilderKit.Wire(encounter, "rewardAnchor", rewardAt);

            if (reward != null)
            {
                ItemBuilderKit.Wire(encounter, "reward", reward);
                ItemBuilderKit.Wire(encounter, "rewardPrefab", KeyBuilder.LoadPrefab(reward.name));
            }

            Debug.Log($"[Castle] Raised {stand.Name} at {root.transform.position} " +
                      $"with a garrison of {stand.Garrison.Count}.");
        }

        // ------------------------------------------------------------------
        // Parts
        // ------------------------------------------------------------------

        /// <summary>
        /// The core the player strikes.
        /// <para>
        /// Its collider is SOLID, and it has to be. It was a trigger, so that the player could
        /// stand on the deck without bumping into it — but <c>MeleeStrike</c> sweeps with
        /// <c>QueryTriggerInteraction.Ignore</c>, so the swing looked straight through it and the
        /// beacon could not be struck at all. A player could hit it as long as they liked and
        /// nothing in the game would ever happen.
        /// </para>
        /// </summary>
        private static TowerBeacon BuildBeacon(Transform at)
        {
            var collider = at.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = 1.1f;
            collider.height = 2.4f;

            var light = at.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 28f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            var beacon = at.gameObject.AddComponent<TowerBeacon>();
            ItemBuilderKit.Wire(beacon, "core", light);
            return beacon;
        }

        private static Lightfall BuildLightfall(Transform root, Transform deck,
                                                Lightfall.Reach reach)
        {
            var anchor = new GameObject("OrbAnchor");
            anchor.transform.SetParent(root, false);
            anchor.transform.position = deck.position + Vector3.up * OrbHeight;

            GameObject orb = AssetDatabase.LoadAssetAtPath<GameObject>(
                                 LightfallOrbBuilder.PrefabPath)
                             ?? LightfallOrbBuilder.Build();

            var lightfall = root.gameObject.AddComponent<Lightfall>();
            ItemBuilderKit.WireEnum(lightfall, "reach", (int)reach);
            ItemBuilderKit.Wire(lightfall, "orbAnchor", anchor.transform);
            ItemBuilderKit.Wire(lightfall, "orbPrefab", orb);
            ItemBuilderKit.WireFloat(lightfall, "orbScale", OrbDiameter);
            ItemBuilderKit.WireFloat(lightfall, "localRadius", LocalLightRadius);
            return lightfall;
        }

        /// <summary>
        /// Where the player is set down when the castle is finished: out in front of the keep's
        /// door, turned to face it, so the first thing they see is the tower with the orb over it.
        /// </summary>
        private static Transform ReturnStand(Transform root, Vector3 keepEntrance)
        {
            Transform stand = Anchor(root, "ReturnStand", GroundOutside(keepEntrance, ReturnStandoff));

            // Facing back down the castle's own +Z, which is the axis the doors are laid out on and
            // so the direction the player is standing away from the keep along.
            stand.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            return stand;
        }

        /// <summary>Where the key the castle pays out is dropped: between the player and the door.</summary>
        private static Transform RewardStand(Transform root, Vector3 keepEntrance)
            => Anchor(root, "RewardStand",
                      GroundOutside(keepEntrance, RewardStandoff) + Vector3.up * RewardDropHeight);

        /// <summary>
        /// A point on the shelf <paramref name="standoff"/> metres out from the keep's door. The
        /// anchor's own height is the middle of the doorway, so the ground is the <see cref="Embed"/>
        /// the whole castle is sunk by rather than the anchor's Y.
        /// </summary>
        private static Vector3 GroundOutside(Vector3 keepEntrance, float standoff)
            => new Vector3(keepEntrance.x, Embed, keepEntrance.z + standoff);

        /// <summary>
        /// A door: the leaf that is already in the model, plus the lock that holds it and the light
        /// that is the only thing telling the player it is locked — see <see cref="EntranceSignal"/>.
        /// </summary>
        private static void BuildEntrance(Transform root, GameObject body, string name,
                                          Vector3 local, InventoryItem key,
                                          KeyedEntrance.Answer answer, Transform destination)
        {
            var door = new GameObject(name);
            door.transform.SetParent(root, false);
            door.transform.localPosition = local;

            var entrance = door.AddComponent<KeyedEntrance>();
            ItemBuilderKit.Wire(entrance, "requiredKey", key);
            ItemBuilderKit.WireEnum(entrance, "answer", (int)answer);

            Transform leaf = FindLeaf(body, name);
            if (leaf != null)
            {
                ItemBuilderKit.Wire(entrance, "leaf", leaf);
                WireVector(entrance, "hingePivot", HingePivot(leaf, root));
            }

            if (destination != null) ItemBuilderKit.Wire(entrance, "destination", destination);

            // The interaction has to be reachable by the Interactor's ray, which stops at the
            // first solid thing it meets. A trigger set proud of the door's face is what the ray
            // resolves to — see Interactor.ResolveAlongRay: a trigger answers only when it carries
            // the interactable itself, which this one does.
            var reach = door.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.size = new Vector3(3.6f, 4.2f, 1.2f);

            var lampObject = new GameObject("Lock");
            lampObject.transform.SetParent(door.transform, false);
            lampObject.transform.localPosition = new Vector3(0f, 0f, -0.9f);

            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.shadows = LightShadows.None;

            var signal = lampObject.AddComponent<EntranceSignal>();
            ItemBuilderKit.Wire(signal, "entrance", entrance);

            // The leaf as well as the lamp, so the thing that lights up is the DOOR rather than a
            // lantern hanging in front of one. It matters most at the moment the highlight is for:
            // a lamp brightening is ambiguous about which of the things it is lighting the player
            // is being offered, and the door glowing is not.
            if (leaf != null) ItemBuilderKit.Wire(signal, "glow", GlowingLeaf(leaf));
        }

        /// <summary>
        /// The door leaf's renderer, moved off the castle's shared stone and onto a material that
        /// can emit — see <see cref="DoorLeafMaterialPath"/>. Null when the leaf has no renderer,
        /// which leaves the lamp as the only signal rather than failing the build.
        /// </summary>
        private static Renderer GlowingLeaf(Transform leaf)
        {
            var renderer = leaf.GetComponent<Renderer>();
            if (renderer == null) return null;

            // The stone's own base colour, so a door is the same black as the wall it is set into
            // until something lights it. Reading it rather than restating it means re-texturing the
            // castle does not leave its doors the colour the castle used to be.
            Material stone = renderer.sharedMaterial;
            Color baseColour = stone != null && stone.HasProperty(BaseColorId)
                ? stone.GetColor(BaseColorId)
                : Color.black;

            Directory.CreateDirectory(Path.GetDirectoryName(DoorLeafMaterialPath));
            renderer.sharedMaterial = ItemBuilderKit.EnsureLitMaterial(
                DoorLeafMaterialPath, baseColour, Color.black);

            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        /// <summary>
        /// Where a door leaf is hinged, in its parent's space: down one vertical edge.
        /// <para>
        /// Told to the door rather than built as a parent object, because the leaf lives inside an
        /// imported model instance and Unity refuses to reparent anything inside a prefab
        /// instance. Giving <see cref="KeyedEntrance"/> a pivot to turn about needs no hierarchy
        /// surgery and leaves the imported model exactly as it was imported.
        /// </para>
        /// <para>
        /// The edge comes from the leaf's own bounds rather than a width constant, so widening the
        /// gateway in the exporter moves the hinge with it.
        /// </para>
        /// </summary>
        private static Vector3 HingePivot(Transform leaf, Transform root)
        {
            var renderer = leaf.GetComponent<Renderer>();
            if (renderer == null) return Vector3.zero;

            // The leaf's width runs along the castle's own X, the axis the exporter lays every
            // door out on.
            float halfWidth = Mathf.Abs(Vector3.Dot(renderer.bounds.extents, root.right));
            Vector3 edge = renderer.bounds.center - root.right * halfWidth;

            return leaf.parent != null ? leaf.parent.InverseTransformPoint(edge) : edge;
        }

        /// <summary>A serialized Vector3 field, which <c>ItemBuilderKit</c> has no writer for.</summary>
        private static void WireVector(Object target, string field, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The door leaf inside the imported model, by the name the exporter gave it. Null when the
        /// model has no leaf there, which is the outer gate's case in the keep-only castle.
        /// </summary>
        private static Transform FindLeaf(GameObject body, string entranceName)
        {
            string wanted = entranceName == "OuterGate" ? "Door_OuterGate" : "Door_KeepEntrance";

            foreach (Transform child in body.GetComponentsInChildren<Transform>(true))
                if (child.name == wanted) return child;

            return null;
        }

        /// <summary>
        /// The garrison, dropped onto whatever the ground under each layout point turns out to be.
        /// <para>
        /// Raycast rather than placed at the shelf height: the shelf is flat, but the castle's own
        /// steps and foundations stand on it, and a creature spawned inside a plinth never finds
        /// the navmesh.
        /// </para>
        /// <para>
        /// Who is in it comes from the stand's roster, the same shape the island's camps use, so a
        /// castle and a camp are populated by one idea of what a group of enemies is.
        /// </para>
        /// </summary>
        private static void BuildGarrison(Transform root, CastleStand stand, Terrain terrain,
                                          int seed, InventoryItem towerKey)
        {
            IReadOnlyList<CampMember> garrison = stand.Garrison;

            var campObject = new GameObject("Garrison");
            campObject.transform.SetParent(root, false);

            var camp = campObject.AddComponent<EnemyBase>();
            ItemBuilderKit.WireFloat(camp, "wanderRadius", stand.GarrisonOuter);
            ItemBuilderKit.WireFloat(camp, "defendRadius", stand.GarrisonOuter + 6f);

            IReadOnlyList<Vector2> spots = GarrisonLayout.Ring(
                garrison.Count, stand.GarrisonInner, stand.GarrisonOuter, jitter: 1.5f,
                seed: seed + stand.SeedOffset);

            // Which of them is holding the key. Chosen, not rolled — see GarrisonLayout.Bearers.
            var bearers = new HashSet<int>(GarrisonLayout.Bearers(
                spots.Count, stand.TowerKeyBearers, seed + stand.SeedOffset));

            for (int index = 0; index < spots.Count; index++)
            {
                GameObject prefab = EnemyPrefabs.Load(garrison[index].Species);
                if (prefab == null)
                {
                    Debug.LogError($"[Castle] {stand.Name} is standing empty.");
                    return;
                }

                Vector3 local = new Vector3(spots[index].x, 0f, spots[index].y);
                Vector3 world = root.TransformPoint(local);
                world.y = TerrainGround.Under(terrain, world);

                var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, campObject.transform);
                body.name = garrison[index].Name;
                body.transform.SetPositionAndRotation(
                    world, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                ItemBuilderKit.Wire(body.GetComponent<EnemyAgent>(), "home", camp);

                if (!bearers.Contains(index)) continue;

                var loot = body.AddComponent<EnemyLoot>();
                ItemBuilderKit.Wire(loot, "carried", towerKey);
                ItemBuilderKit.Wire(loot, "health", body.GetComponent<HealthComponent>());
            }
        }

        // ------------------------------------------------------------------
        // Assets and scene plumbing
        // ------------------------------------------------------------------

        private static Transform Anchor(Transform root, string name, Vector3 local)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = local;
            return anchor.transform;
        }

        private static GameObject LoadModel(string name)
        {
            string path = $"{ModelFolder}/{name}.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
            {
                Debug.LogError($"[Castle] No model at {path}. Run castle_export.py first.");
                return null;
            }

            EnsureColliders(path);
            return model;
        }

        /// <summary>
        /// Turns on mesh colliders for an imported castle. Without them the castle is scenery the
        /// player walks straight through, and — because the garrison's navmesh is built from
        /// colliders — a courtyard with no walls in it as far as they are concerned.
        /// </summary>
        private static void EnsureColliders(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.addCollider) return;

            importer.addCollider = true;

            // Nothing in the castle is animated and nothing reads its hierarchy, so the rig is
            // dead weight on a 125,000-vertex mesh.
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;

            importer.SaveAndReimport();
        }

        private static DaylightProfile EnsureDaylightProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DaylightProfile>(DaylightPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(DaylightPath));
            var created = ScriptableObject.CreateInstance<DaylightProfile>();
            AssetDatabase.CreateAsset(created, DaylightPath);
            return created;
        }

        /// <summary>Drops a previous copy of this castle, so building twice does not stack them.</summary>
        private static void Replace(string name)
        {
            foreach (GameObject root in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root != null && root.name == name && root.GetComponent<CastleEncounter>() != null)
                    Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Keeps the castles' grounds bare, and replants the island around them.
        /// <para>
        /// Two things make this necessary and neither is optional. The planting ray looks for
        /// whatever is under a point, and a castle is under a great many points — so without a
        /// clearing the castle comes up wearing a forest, with trees on its battlements and its
        /// tower roofs. And the plants already in the scene were dropped onto the land as it was
        /// BEFORE the shelf was stamped, so every one of them over the new hill is now buried in
        /// it or standing in the air above it.
        /// </para>
        /// <para>
        /// The clearing is the plateau rather than the castle's own footprint, so there is a bare
        /// apron round the walls. A castle with trees growing tight against its gate cannot be
        /// seen from anywhere, which throws away the one thing it is placed on a hill to do.
        /// </para>
        /// </summary>
        private static void ClearTheGrounds()
        {
            var field = Object.FindFirstObjectByType<VegetationField>();
            if (field == null)
            {
                Debug.LogWarning("[Castle] No VegetationField in the scene, so the castles' " +
                                 "grounds cannot be cleared.");
                return;
            }

            var clearings = new List<VegetationField.Clearing>();
            foreach (CastleStand stand in CastlePlacement.All)
                clearings.Add(new VegetationField.Clearing(stand.Centre, stand.PlateauRadius));

            field.SetClearings(clearings);
            EditorUtility.SetDirty(field);

            VegetationBaker.Bake(field);
        }

        /// <summary>
        /// Rebuilds the navmesh, now that there are walls on it. Without this the garrison paths
        /// through the castle as though it were not there.
        /// </summary>
        private static void Rebake(Terrain terrain)
        {
            var surface = terrain.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[Castle] The terrain has no NavMeshSurface, so the garrison has " +
                                 "nothing to walk on.");
                return;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The gameplay points on the castle, written by <c>castle_export.py</c> straight out of
        /// the model. Read from the model rather than typed here, because every one of them is a
        /// place on a mesh — the deck the player is put down on, the core they strike, the gateway
        /// a door hangs in — and a number copied out of Blender by hand goes stale the first time
        /// anyone moves the tower.
        /// </summary>
        private sealed class Anchors
        {
#pragma warning disable CS0649 // assigned by JsonUtility
            public Vector3 outerGate;
            public Vector3 keepEntrance;
            public Vector3 towerDeck;
            public Vector3 towerBeacon;
#pragma warning restore CS0649
        }

        private static Anchors LoadAnchors()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AnchorPath);
            if (asset == null)
            {
                Debug.LogError($"[Castle] No anchors at {AnchorPath}. Run castle_export.py first.");
                return null;
            }

            return JsonUtility.FromJson<Anchors>(asset.text);
        }
    }
}
