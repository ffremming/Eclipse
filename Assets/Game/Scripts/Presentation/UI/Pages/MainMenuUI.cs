using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceGame.Core;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private SceneReference gameScene;

    [Tooltip("The prefab every menu entry is cloned from, so the screens this menu opens carry its " +
             "hover animation and sounds.")]
    [SerializeField] private GameObject menuButtonPrefab;

    /// <summary>The scene every route into the game loads.</summary>
    public string GameSceneName => gameScene != null ? gameScene.SceneName : null;

    /// <summary>
    /// The menu's own button, lent to the screens it opens. They are built at runtime and have no
    /// Inspector of their own, so this is where the reference lives.
    /// </summary>
    public GameObject MenuButtonPrefab => menuButtonPrefab;

    /// <summary>
    /// Front-menu entry: start a fight. Bound by name from MainMenu.unity; do not rename.
    /// </summary>
    public void StartGame() => EnterWorld();

    /// <summary>
    /// Front-menu entry: leave the game. Bound by name from MainMenu.unity; do not rename.
    /// </summary>
    public void QuitGame()
    {
        GameSettings.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Loads the game scene.
    /// </summary>
    public void EnterWorld()
    {
        if (gameScene == null || string.IsNullOrEmpty(gameScene.SceneName))
        {
            Debug.LogError("[MainMenu] No game scene is assigned, so there is nowhere to start.", this);
            return;
        }

        SceneManager.LoadScene(gameScene.SceneName, LoadSceneMode.Single);
    }
}
