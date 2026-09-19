using System.Collections.Generic;
using SpaceGame.Castle;
using SpaceGame.Enemies;
using SpaceGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Stands the camps of <see cref="EnemyCampPlacement"/> up in the open scene.
    /// <para>
    /// Run as part of building the world, and available on its own so the camps can be replaced in
    /// a scene that already exists — the world build throws the scene away and takes any hand edits
    /// with it, which is too blunt a tool for moving a camp twenty metres.
    /// </para>
    /// <para>
    /// The camps sit under one root of their own rather than under the terrain. The terrain's
    /// NavMeshSurface bakes what is parented to it, and a creature baked into the navmesh is a
    /// permanent hole in the ground that every other creature walks around.
    /// </para>
    /// </summary>
    public static class EnemyCampBuilder
    {
        private const string RootName = "EnemyCamps";

        /// <summary>
        /// Ground for a camp that is not standing on any: a prefab's creatures are laid out flat
        /// around its origin, and each one's NavMeshAgent drops it onto the navmesh where the
        /// prefab is finally dropped.
        /// </summary>
        private static readonly System.Func<Vector3, Vector3> Flat = point => point;

        /// <summary>
        /// How far a creature may be moved to reach the navmesh, in metres. Generous enough to step
        /// off a boulder the ground ray landed on, tight enough that it cannot cross a camp.
        /// </summary>
        private const float NavMeshSnap = 4f;

        [MenuItem("Tools/Eclipse/World/Place Enemy Camps")]
        private static void PlaceInOpenScene()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("[EnemyCamps] No terrain in the open scene.");
                return;
            }

            Build(terrain, NatureWorldBuilder.Seed);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        /// <summary>
        /// Builds the camp PREFAB — both creatures around one <see cref="EnemyBase"/>, ready to
        /// drag anywhere — from the roster in <see cref="EnemyCampPlacement.Mixed"/>.
        /// <para>
        /// Laid out flat around the origin, because a prefab has no ground: each creature's
        /// NavMeshAgent puts it down on the navmesh wherever the camp is finally dropped. On very
        /// uneven ground the outer ring will want nudging, which is what dropping a camp by hand
        /// is for.
        /// </para>
        /// </summary>
        [MenuItem("Tools/Eclipse/Enemies/Build Dark Camp Prefab")]
        private static void BuildPrefab()
        {
            EnemyCamp camp = EnemyCampPlacement.Mixed;
            var root = new GameObject(camp.Name);

            if (Populate(root.transform, camp, NatureWorldBuilder.Seed, Flat))
            {
                PrefabUtility.SaveAsPrefabAsset(root, $"{EnemyPrefabs.Folder}/{camp.Name}.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnemyCamps] Built {EnemyPrefabs.Folder}/{camp.Name}.prefab: " +
                          $"{camp.Population} creatures round one camp.");
            }

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Builds every camp, replacing any this has built before. Idempotent on purpose: the world
        /// build calls it, and so does the menu item, and running it twice must not leave the
        /// island with two of everything.
        /// </summary>
        public static void Build(Terrain terrain, int seed)
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);

            int placed = 0;
            foreach (EnemyCamp camp in EnemyCampPlacement.All)
                placed += BuildCamp(root.transform, camp, terrain, seed) ? camp.Population : 0;

            Debug.Log($"[EnemyCamps] Placed {placed} creatures in {EnemyCampPlacement.All.Length} camps.");
        }

        private static bool BuildCamp(Transform root, EnemyCamp camp, Terrain terrain, int seed)
        {
            if (!CampSite.TryFind(camp.Centre, EnemyCampPlacement.SearchRadius,
                                  point => Sample(terrain, point),
                                  EnemyCampPlacement.MinHeight, EnemyCampPlacement.MaxSteepness,
                                  out Vector2 site))
            {
                Debug.LogWarning($"[EnemyCamps] {camp.Name} found no standable ground within " +
                                 $"{EnemyCampPlacement.SearchRadius} m of {camp.Centre}, so it is " +
                                 "not in the world. Move its centre inland.");
                return false;
            }

            var campObject = new GameObject(camp.Name);
            campObject.transform.SetParent(root, false);

            Vector3 centre = new Vector3(site.x, 0f, site.y);
            centre.y = TerrainGround.Surface(terrain, centre);
            campObject.transform.position = centre;

            return Populate(campObject.transform, camp, seed, world => OnGround(world, terrain));
        }

        /// <summary>
        /// Puts a camp's creatures around it: the ring, the roster, who they call home.
        /// <para>
        /// Shared by the camps in the world and by <see cref="BuildPrefab"/>, so a camp dropped
        /// from the prefab is laid out exactly like one the island was built with. The only thing
        /// that differs between the two is <paramref name="ground"/> — what a creature is standing
        /// on — which is the terrain out in the world and nothing at all inside a prefab.
        /// </para>
        /// </summary>
        private static bool Populate(Transform campObject, EnemyCamp camp, int seed,
                                     System.Func<Vector3, Vector3> ground)
        {
            var home = campObject.gameObject.AddComponent<EnemyBase>();
            ItemBuilderKit.WireFloat(home, "wanderRadius", EnemyCampPlacement.WanderRadius);
            ItemBuilderKit.WireFloat(home, "defendRadius", EnemyCampPlacement.DefendRadius);

            IReadOnlyList<Vector2> spots = GarrisonLayout.Ring(
                camp.Population, EnemyCampPlacement.RingInner, EnemyCampPlacement.RingOuter,
                EnemyCampPlacement.RingJitter, seed + camp.SeedOffset);

            IReadOnlyList<CampMember> members = camp.Members;
            for (int index = 0; index < members.Count; index++)
            {
                GameObject prefab = EnemyPrefabs.Load(members[index].Species);
                if (prefab == null) return false;

                Stand(campObject, prefab, home, spots[index], ground, members[index].Name);
            }

            return true;
        }

        /// <summary>
        /// The terrain under a point, then the navmesh. A creature that is not on the navmesh does
        /// not walk, does not chase and never joins the fight — and from a distance looks exactly
        /// like one that is simply idle, which is the worst way for this to fail.
        /// </summary>
        private static Vector3 OnGround(Vector3 world, Terrain terrain)
        {
            world.y = TerrainGround.Surface(terrain, world);

            if (NavMesh.SamplePosition(world, out NavMeshHit onMesh, NavMeshSnap, NavMesh.AllAreas))
                return onMesh.position;

            Debug.LogWarning($"[EnemyCamps] a creature is more than {NavMeshSnap} m from the navmesh " +
                             $"at {world:F0} and will not move. Bake the navmesh, or move its camp.");
            return world;
        }

        private static void Stand(Transform camp, GameObject prefab, EnemyBase home, Vector2 spot,
                                  System.Func<Vector3, Vector3> ground, string name)
        {
            Vector3 world = ground(camp.position + new Vector3(spot.x, 0f, spot.y));

            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, camp);
            body.name = name;

            // Facing outwards from the middle of camp, so the player is met by a creature looking
            // their way from whichever side they came. A camp all facing one direction reads as a
            // row of statues.
            Vector3 outward = world - camp.position;
            Quaternion facing = outward.sqrMagnitude < 1e-4f
                ? Quaternion.identity
                : Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z).normalized, Vector3.up);

            body.transform.SetPositionAndRotation(world, facing);
            ItemBuilderKit.Wire(body.GetComponent<EnemyAgent>(), "home", home);
        }

        /// <summary>The ground at a point, in the terms <see cref="CampSite"/> judges it by.</summary>
        private static Ground Sample(Terrain terrain, Vector2 point)
        {
            Vector3 world = new Vector3(point.x, 0f, point.y);
            float height = terrain.SampleHeight(world) + terrain.transform.position.y;

            // Steepness is asked of the terrain in its own 0..1 coordinates, which is what its
            // heightmap is indexed by — world metres here would sample the corner of the island for
            // every camp and report it perfectly flat.
            Vector3 local = world - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float steepness = terrain.terrainData.GetSteepness(
                Mathf.Clamp01(local.x / size.x), Mathf.Clamp01(local.z / size.z));

            return new Ground(height, steepness);
        }
    }
}
