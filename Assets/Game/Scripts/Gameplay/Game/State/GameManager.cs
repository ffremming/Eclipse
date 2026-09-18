using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using SpaceGame.Core;

namespace SpaceGame.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, Won }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        public float GameTimer { get; private set; } = 0f;

        public event Action<GameState> OnStateChanged;
    
        [SerializeField] private SceneReference onWinScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
                GameTimer += Time.deltaTime;
        }

        public void SetState(GameState state)
        {
            CurrentState = state;
            OnStateChanged?.Invoke(state);
        }

        /// <summary>
        /// Ends the run and moves to the win scene.
        /// </summary>
        public void WinGame()
        {
            if (onWinScene == null || string.IsNullOrEmpty(onWinScene.SceneName))
            {
                Debug.LogError("[GameManager] Nothing is assigned to onWinScene, so winning has " +
                               "nowhere to go. The run stays open.", this);
                return;
            }

            SetState(GameState.Won);

            SceneManager.LoadScene(onWinScene.SceneName, LoadSceneMode.Single);
        }
    }
}
