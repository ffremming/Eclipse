using System;
using System.Collections.Generic;
using System.IO;
using SpaceGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Turns the folder of rendered clips into a <see cref="MusicLibrary"/>, and drops a player for
    /// it into whichever scene is open.
    /// <para>
    /// Built rather than wired by hand for the reason every other asset here is: the clips are
    /// re-cut from the raw takes from time to time, and a library wired by hand goes stale the
    /// first time one of them is renamed — silently, because a missing clip is a mood that plays
    /// nothing rather than an error. A clip's mood is its filename's first word, so the cut and the
    /// wiring cannot disagree.
    /// </para>
    /// <para>
    /// It also fixes the import settings. Unity's default decompresses a whole clip into memory at
    /// load, which for a minute of music is megabytes each and fifteen of them at once; music is
    /// streamed instead, which is what streaming is for.
    /// </para>
    /// </summary>
    public static class MusicBuilder
    {
        private const string ClipFolder = "Assets/Game/Art/Audio/Music";
        private const string LibraryPath = "Assets/Game/Data/MusicLibrary.asset";

        /// <summary>
        /// How each mood is played. The numbers the score is tuned with live here rather than only
        /// in the asset because a rebuild would otherwise wipe whatever had been tuned — so the
        /// tuning is what is under version control and the asset is its output.
        /// </summary>
        private static readonly MoodTuning[] Tunings =
        {
            // The menu is one piece under a still screen. It fades up slowly and never stops.
            new MoodTuning(MusicMood.Menu, fade: 5f, rest: 0f, once: false),

            // The island. It rests between pieces, so the next one is heard as music starting
            // rather than as the score still going (GDC-L1-AUDIO-0005) — and so a fight has some
            // quiet to arrive out of.
            new MoodTuning(MusicMood.Explore, fade: 4f, rest: 20f, once: false),

            // The one place the score does not stop for breath, and the one that has to arrive
            // before the player has finished turning round.
            new MoodTuning(MusicMood.Combat, fade: 1.6f, rest: 0f, once: false),

            // The heaviest thing in the game and the slowest to arrive: a castle should come up
            // under the player rather than start.
            new MoodTuning(MusicMood.Castle, fade: 6f, rest: 8f, once: false),

            // One toll, and then the quiet the death screen is read in.
            new MoodTuning(MusicMood.Death, fade: 1f, rest: 0f, once: true),
        };

        [MenuItem("Tools/Eclipse/Audio/Build Music Library")]
        public static void BuildLibrary()
        {
            Dictionary<MusicMood, List<AudioClip>> filed = FileClipsByMood();

            MusicLibrary library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MusicLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var serialized = new SerializedObject(library);
            SerializedProperty shelves = serialized.FindProperty("shelves");
            shelves.arraySize = 0;

            int total = 0;
            foreach (MoodTuning tuning in Tunings)
            {
                List<AudioClip> clips = filed[tuning.Mood];
                if (clips.Count == 0)
                {
                    Debug.LogWarning($"[Music] Nothing is filed under {tuning.Mood}, so that mood " +
                                     "will be silent.");
                    continue;
                }

                // Alphabetical, which for these names is stable — the shuffle decides the order
                // they are heard in, so this only has to stop the array reshuffling itself in the
                // diff every time the library is rebuilt.
                clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

                shelves.arraySize++;
                tuning.WriteInto(shelves.GetArrayElementAtIndex(shelves.arraySize - 1), clips);
                total += clips.Count;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Music] Library built: {shelves.arraySize} moods, {total} clips.", library);
        }

        [MenuItem("Tools/Eclipse/Audio/Add Music To Open Scene")]
        public static void AddToOpenScene()
        {
            MusicLibrary library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError("[Music] There is no library yet — build it first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();

            var existing = UnityEngine.Object.FindFirstObjectByType<MusicDirector>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log($"[Music] {scene.name} already has a director.", existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // The menu is one screen and one mood; the world opens calm and is then steered by what
            // is happening in it.
            bool isMenu = scene.name.Contains("Menu");

            var host = new GameObject("Music");
            MusicDirector director = host.AddComponent<MusicDirector>();
            ItemBuilderKit.Wire(director, "library", library);
            ItemBuilderKit.WireEnum(director, "startMood",
                                    (int)(isMenu ? MusicMood.Menu : MusicMood.Explore));

            if (!isMenu) host.AddComponent<WorldMusic>();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = host;

            Debug.Log($"[Music] Added a director to {scene.name}, opening on " +
                      $"{(isMenu ? MusicMood.Menu : MusicMood.Explore)}.", host);
        }

        // ------------------------------------------------------------------
        // Parts
        // ------------------------------------------------------------------

        private static Dictionary<MusicMood, List<AudioClip>> FileClipsByMood()
        {
            var filed = new Dictionary<MusicMood, List<AudioClip>>();
            foreach (MoodTuning tuning in Tunings) filed[tuning.Mood] = new List<AudioClip>();

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                if (!TryReadMood(Path.GetFileNameWithoutExtension(path), out MusicMood mood) ||
                    !filed.ContainsKey(mood))
                {
                    Debug.LogWarning($"[Music] {Path.GetFileName(path)} does not begin with the " +
                                     "name of a mood, so nothing would ever play it. Rename it " +
                                     "Mood_Something.");
                    continue;
                }

                Stream(path);
                filed[mood].Add(clip);
            }

            return filed;
        }

        private static bool TryReadMood(string fileName, out MusicMood mood)
        {
            int underscore = fileName.IndexOf('_');
            string head = underscore > 0 ? fileName[..underscore] : fileName;

            return Enum.TryParse(head, ignoreCase: true, out mood);
        }

        /// <summary>
        /// Kept compressed in memory and decoded as it plays, rather than decompressed at load or
        /// streamed off disk.
        /// <para>
        /// Decompressing would be the whole score as raw PCM — thirteen minutes of it, well over a
        /// hundred megabytes — to have two clips audible at a time. Streaming, which is what music
        /// this long would normally use, does not work here: a streamed clip reports its data as
        /// Unloaded, refuses an explicit <c>LoadAudioData</c>, and <c>AudioSource.Play</c> then does
        /// nothing at all, silently. Compressed-in-memory is fifteen Vorbis files resident, which
        /// for a mono score of this length is a handful of megabytes.
        /// </para>
        /// <para>
        /// Force To Mono is deliberately left OFF even though every clip is mono already. Unity
        /// pairs it with a peak normalise that cannot be turned off from script, and that would
        /// flatten the one thing the cut was careful about: exploration sits several LU under a
        /// fight so that a fight arriving is louder rather than merely different
        /// (<c>GDC-L1-AUDIO-0005</c>). Normalising every clip to the same ceiling would throw the
        /// whole staging away.
        /// </para>
        /// </summary>
        private static void Stream(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.CompressedInMemory &&
                settings.compressionFormat == AudioCompressionFormat.Vorbis &&
                settings.preloadAudioData &&
                !importer.forceToMono)
                return;

            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;

            importer.SaveAndReimport();
        }

        /// <summary>How one mood is played, as the numbers the score was tuned with.</summary>
        private readonly struct MoodTuning
        {
            public readonly MusicMood Mood;

            private readonly float fade;
            private readonly float rest;
            private readonly bool once;

            public MoodTuning(MusicMood mood, float fade, float rest, bool once)
            {
                Mood = mood;
                this.fade = fade;
                this.rest = rest;
                this.once = once;
            }

            /// <summary>
            /// Fills in one shelf of the library. Written through the serialized property rather
            /// than through setters, so nothing in <see cref="MusicShelf"/> gains a public setter
            /// purely because a builder wanted to reach it.
            /// </summary>
            public void WriteInto(SerializedProperty shelf, List<AudioClip> clips)
            {
                shelf.FindPropertyRelative("mood").enumValueIndex = (int)Mood;
                shelf.FindPropertyRelative("gain").floatValue = 1f;
                shelf.FindPropertyRelative("fadeSeconds").floatValue = fade;
                shelf.FindPropertyRelative("restSeconds").floatValue = rest;
                shelf.FindPropertyRelative("playsOnce").boolValue = once;

                SerializedProperty list = shelf.FindPropertyRelative("clips");
                list.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }
    }
}
