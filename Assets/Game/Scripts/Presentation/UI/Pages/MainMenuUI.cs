using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceGame.Core;
using SpaceGame.Presentation;

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
    /// Loads the game scene, behind a loading screen that stays up until it is genuinely playable.
    /// </summary>
    public void EnterWorld()
    {
        if (gameScene == null || string.IsNullOrEmpty(gameScene.SceneName))
        {
            Debug.LogError("[MainMenu] No game scene is assigned, so there is nowhere to start.", this);
            return;
        }

        LoadingScreenUI.ShowUntilReady(gameScene.SceneName);

        SceneManager.LoadScene(gameScene.SceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
