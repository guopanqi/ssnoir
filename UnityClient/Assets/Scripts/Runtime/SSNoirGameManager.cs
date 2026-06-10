using UnityEngine;
using SSNoir.Core;

namespace SSNoir
{
    public class SSNoirGameManager : MonoBehaviour
    {
        private GameState _gameState;
        private SceneManager _sceneManager;
        private UnityScriptLoader _scriptLoader;

        public GameState GameState => _gameState;
        public SceneManager SceneManager => _sceneManager;

        private void Start()
        {
            // 1. Initialize Game State
            _gameState = new GameState();

            // 2. Initialize Unity specific script loader
            _scriptLoader = new UnityScriptLoader();

            // 3. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);

            // 4. Listen to scene loads
            _sceneManager.OnSceneLoaded += () => {
                Debug.Log($"[SSNoir] Scene Loaded: {_sceneManager.CurrentSceneName}");
            };
            _sceneManager.OnWorldRefreshed += () => {
                Debug.Log($"[SSNoir] World Refreshed! Current nodes count: {_sceneManager.CurrentWorldNodes.Count}");
            };

            // 5. Load starting location from gameState
            string startingLocation = _gameState.Get<string>("location", "home");
            _sceneManager.LoadScene(startingLocation);

            Debug.Log("[SSNoir] Core Engine initialized successfully in UnityClient.");
        }
    }
}
