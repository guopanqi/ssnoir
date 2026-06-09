using System;
using System.Collections.Generic;
using System.IO;
using SSNoir.Core;
using SSNoir.Rendering;

namespace SSNoir
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--validate")
                {
                    ValidateContent();
                    return;
                }

                if (args.Length > 0 && args[0] == "--simulate")
                {
                    SimulateMinimalFlow();
                    return;
                }

                var gameState = new GameState();
                var sceneManager = new SceneManager(gameState);
                var renderer = new RaylibRenderer(sceneManager, gameState);
                renderer.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.Exit(1);
            }
        }

        private static void ValidateContent()
        {
            var scenesDir = Path.GetFullPath("scenes");
            if (!Directory.Exists(scenesDir))
            {
                throw new DirectoryNotFoundException($"Scenes directory not found: {scenesDir}");
            }

            foreach (var scenePath in Directory.GetFiles(scenesDir, "*.scm"))
            {
                var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                var gameState = new GameState();
                var sceneManager = new SceneManager(gameState);

                sceneManager.LoadScene(sceneName);

                if (sceneManager.CurrentWorldNodes.Count == 0)
                {
                    throw new InvalidDataException($"Scene '{sceneName}' produced an empty world.");
                }

                Console.WriteLine($"Validated scene '{sceneName}' with {sceneManager.CurrentWorldNodes.Count} root node(s).");
            }
        }

        private static void SimulateMinimalFlow()
        {
            var gameState = new GameState();
            var sceneManager = new SceneManager(gameState);

            sceneManager.LoadScene("home");
            ExecuteNode(sceneManager, "敲门");
            ExecuteNode(sceneManager, "敲门");
            ExecuteNode(sceneManager, "敲门");

            var homeNode = FindNode(sceneManager.CurrentWorldNodes, "家");
            if (FindNode(homeNode.Children, "进门") == null)
            {
                throw new InvalidOperationException("Expected '进门' to appear after knocking three times.");
            }

            ExecuteNode(sceneManager, "进门");
            if (sceneManager.CurrentSceneName != "office")
            {
                throw new InvalidOperationException($"Expected scene 'office', got '{sceneManager.CurrentSceneName}'.");
            }

            ExecuteNode(sceneManager, "写代码");
            ExecuteNode(sceneManager, "写代码");
            ExecuteNode(sceneManager, "写代码");

            var officeNode = FindNode(sceneManager.CurrentWorldNodes, "办公室");
            if (FindNode(officeNode.Children, "休息") == null)
            {
                throw new InvalidOperationException("Expected '休息' to appear after working three times.");
            }

            var money = gameState.Get<int>("money");
            if (money != 80)
            {
                throw new InvalidOperationException($"Expected money to be 80 after three work actions, got {money}.");
            }

            Console.WriteLine("Minimal flow simulation passed.");
        }

        private static void ExecuteNode(SceneManager sceneManager, string name)
        {
            var node = FindNode(sceneManager.CurrentWorldNodes, name);
            if (node == null)
            {
                throw new InvalidOperationException($"Node not found: {name}");
            }

            sceneManager.ExecuteEffect(node);
        }

        private static GameNode FindNode(List<GameNode> nodes, string name)
        {
            foreach (var node in nodes)
            {
                if (node.Name == name)
                {
                    return node;
                }

                var child = FindNode(node.Children, name);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
