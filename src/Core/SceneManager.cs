using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SSNoir.Scripting;

namespace SSNoir.Core
{
    public class SceneManager
    {
        private readonly GameState _gameState;
        private SchemeInterpreter _interpreter;
        private string _currentSceneName;

        public event Action OnSceneLoaded;
        public event Action OnWorldRefreshed;

        public List<GameNode> CurrentWorldNodes { get; private set; } = new List<GameNode>();
        public string CurrentSceneName => _currentSceneName;

        public SceneManager(GameState gameState)
        {
            _gameState = gameState;
            _gameState.OnStateChanged += HandleGlobalStateChanged;
        }

        private void HandleGlobalStateChanged()
        {
            var loc = _gameState.Get<string>("location");
            if (loc != _currentSceneName)
            {
                LoadScene(loc);
            }
        }

        public void LoadScene(string sceneName)
        {
            _currentSceneName = sceneName;
            
            // Re-create interpreter to fully discard old local state
            _interpreter = new SchemeInterpreter(_gameState);
            
            var relativePath = $"scenes/{sceneName}.scm";
            _interpreter.LoadFile(relativePath);

            OnSceneLoaded?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            Debug.Assert(_interpreter != null, "Interpreter must not be null when refreshing scene");
            
            var rawWorld = _interpreter.Eval("(get-world)");
            CurrentWorldNodes = NodeConverter.ConvertList(rawWorld, _interpreter.RawInterpreter);
            
            OnWorldRefreshed?.Invoke();
        }

        public void ExecuteEffect(GameNode node)
        {
            Debug.Assert(node.HasEffect, "Cannot execute effect on a node that has no effect");

            // Execute the action (calls the scheme procedure)
            node.Effect.Invoke();

            // Run reactive rules
            _interpreter.Eval("(on-action)");

            // Refresh the node tree
            Refresh();
        }
    }
}
