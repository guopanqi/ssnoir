using System;
using System.Reflection;
using Raylib_cs;
using SSNoir.Core;
using SSNoir.Rendering;
using SSNoir.Testing;

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
                    GameTester.ValidateContent();
                    return;
                }

                if (args.Length > 0 && args[0] == "--list-fonts")
                {
                    var methods = typeof(Raylib).GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("LoadFont") || method.Name.Contains("MeasureText"))
                        {
                            Console.WriteLine($"Method: {method.ReturnType.Name} {method.Name}({string.Join(", ", Array.ConvertAll(method.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"))})");
                        }
                    }
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-capabilities")
                {
                    GameTester.TestCapabilities();
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-saveload")
                {
                    GameTester.TestSaveLoad();
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-arc")
                {
                    GameTester.TestGrowthArc();
                    return;
                }

                SaveManager.DefaultSavePath = "save.json";

                var gameState = new GameState();
                var loader = new LocalScriptLoader();
                var sceneManager = new SceneManager(gameState, loader);
                var renderer = new RaylibRenderer(sceneManager, gameState);
                renderer.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                System.Environment.Exit(1);
            }
        }
    }
}
