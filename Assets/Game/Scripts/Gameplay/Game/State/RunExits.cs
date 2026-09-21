using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceGame.Core;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// The two ways out of a run: start it again, or leave it.
    ///
    /// <para>
    /// One component rather than a pair of buttons that each know how to load a scene, because both
    /// screens that stop the game — dying and pausing — offer the same two exits, and because both
    /// have the same thing to get right on the way out: <c>Time.timeScale</c> survives a scene load.
    /// A run restarted from a paused game would otherwise come back frozen, with nothing left in the
    /// new scene that remembers it was ever paused.
    /// </para>
    /// <para>
    /// A restart reloads the world rather than reviving the body. Eclipse is one run against one
    /// world — the lighthouse is lit once — so starting again means the camps standing back up and
    /// the keys back where they were, which is a scene load and nothing else. Standing back up in
    /// the run you were already in is the other thing entirely, and belongs to
    /// <c>PlayerRespawn</c>: it keeps the world exactly as the death left it. These two are why a
    /// death screen offers both.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunExits : MonoBehaviour
    {
        [Tooltip("The scene leaving a run goes back to. The same Main Menu the game starts at.")]
        [SerializeField] private SceneReference menuScene;

        /// <summary>Loads the world again from the top.</summary>
        public void Restart()
        {
            ReleaseTime();

            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        /// <summary>Goes back to the main menu, keeping whatever the player changed in settings.</summary>
        public void Leave()
        {
            if (menuScene == null || string.IsNullOrEmpty(menuScene.SceneName))
            {
                Debug.LogError("[RunExits] " + name + " has no menu scene assigned, so there is " +
                               "nowhere to leave to. The run stays open.", this);
                return;
            }

            ReleaseTime();
            GameSettings.Save();

            SceneManager.LoadScene(menuScene.SceneName, LoadSceneMode.Single);
        }

        // Nothing in the scene being loaded sets this, and a zero carried across the load is a
        // game that comes up already stopped.
        private static void ReleaseTime() => Time.timeScale = 1f;
    }
}
