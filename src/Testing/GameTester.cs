using System;
using System.Collections.Generic;
using System.IO;
using Schemy;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing
{
    public static class GameTester
    {
        public static void ValidateContent()
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

        public static void SimulateMinimalFlow()
        {
            var gameState = new GameState();
            var sceneManager = new SceneManager(gameState);

            sceneManager.LoadScene("home");
            ExecuteNode(sceneManager, "敲门");
            ExecuteNode(sceneManager, "敲门");
            ExecuteNode(sceneManager, "敲门");

            var homeNode = FindNode(sceneManager.CurrentWorldNodes, "家");
            if (FindNode(homeNode!.Children, "进门") == null)
            {
                throw new InvalidOperationException("Expected '进门' to appear after knocking three times.");
            }

            ExecuteNode(sceneManager, "进门");
            if (sceneManager.CurrentSceneName != "office")
            {
                throw new InvalidOperationException($"Expected scene 'office', got '{sceneManager.CurrentSceneName}'.");
            }

            // Verify initial clock state
            if (sceneManager.CurrentClocks.Count == 0 || sceneManager.CurrentClocks[0].Label != "工作进度")
            {
                throw new InvalidOperationException("Expected '工作进度' clock to be present in office scene.");
            }
            if (sceneManager.CurrentClocks[0].Current != 0)
            {
                throw new InvalidOperationException($"Expected initial work clock to be 0, got {sceneManager.CurrentClocks[0].Current}.");
            }

            ExecuteNode(sceneManager, "写代码");
            ExecuteNode(sceneManager, "写代码");
            ExecuteNode(sceneManager, "写代码");

            // After 3 works, the rule "工资发放" should trigger, awarding 50 money and resetting the clock to 0
            var money = gameState.Get<int>("money");
            if (money != 100)
            {
                throw new InvalidOperationException($"Expected money to be 100 after receiving salary, got {money}.");
            }

            if (sceneManager.CurrentClocks[0].Current != 0)
            {
                throw new InvalidOperationException($"Expected work clock to reset to 0 after triggering salary rule, got {sceneManager.CurrentClocks[0].Current}.");
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

        private static GameNode? FindNode(List<GameNode> nodes, string name)
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

        public static void TestCapabilities()
        {
            Console.WriteLine("=== Schemy Capability Test ===");

            var interpreter = new Interpreter(
                new Interpreter.CreateSymbolTableDelegate[] { Builtins.CreateBuiltins },
                new ReadOnlyFileSystemAccessor()
            );

            // Register native function test-native-fn
            interpreter.DefineGlobal(Symbol.FromString("test-native-fn"), new NativeProcedure(args =>
            {
                var arg = args[0];
                Console.WriteLine($"[C# Native] Received argument. Type: {arg?.GetType().FullName}, Value: {arg}");
                return null;
            }, "test-native-fn"));

            // 1. Can lambda be saved as a variable?
            Console.WriteLine("\nTest 1: Can lambda be saved as a variable?");
            var r1 = interpreter.Evaluate(new StringReader(@"
                (define my-lambda (lambda () 42))
                my-lambda
            "));
            if (r1.Error != null)
            {
                Console.WriteLine($"Test 1 Failed: {r1.Error}");
            }
            else
            {
                Console.WriteLine($"Test 1 Success. Result type: {r1.Result?.GetType().FullName}");
            }

            // 2. Can lambda be put into a cons?
            Console.WriteLine("\nTest 2: Can lambda be put into a cons?");
            var r2 = interpreter.Evaluate(new StringReader(@"
                (cons (lambda () 42) '())
            "));
            if (r2.Error != null)
            {
                Console.WriteLine($"Test 2 Failed: {r2.Error}");
            }
            else
            {
                Console.WriteLine($"Test 2 Success. Result type: {r2.Result?.GetType().FullName}");
                if (r2.Result is List<object> list)
                {
                    Console.WriteLine($"List length: {list.Count}, Element 0 type: {list[0]?.GetType().FullName}");
                }
            }

            // 3. Can lambda be put into a quote structure?
            Console.WriteLine("\nTest 3: Can lambda be put into a quote structure?");
            var r3 = interpreter.Evaluate(new StringReader(@"
                '(lambda () 42)
            "));
            if (r3.Error != null)
            {
                Console.WriteLine($"Test 3 Failed: {r3.Error}");
            }
            else
            {
                Console.WriteLine($"Test 3 Success. Result type: {r3.Result?.GetType().FullName}");
                if (r3.Result is List<object> list)
                {
                    Console.WriteLine($"Quote list length: {list.Count}");
                    foreach (var item in list)
                    {
                        Console.WriteLine($"  Element: {item} (Type: {item?.GetType().FullName})");
                    }
                }
            }

            // 3b. Can lambda evaluated be put in a list using (list ...)?
            Console.WriteLine("\nTest 3b: Can lambda be put into a list using (list ...)?");
            var r3b = interpreter.Evaluate(new StringReader(@"
                (list (lambda () 42))
            "));
            if (r3b.Error != null)
            {
                Console.WriteLine($"Test 3b Failed: {r3b.Error}");
            }
            else
            {
                Console.WriteLine($"Test 3b Success. Result type: {r3b.Result?.GetType().FullName}");
                if (r3b.Result is List<object> list)
                {
                    Console.WriteLine($"List length: {list.Count}, Element 0 type: {list[0]?.GetType().FullName}");
                }
            }

            // 4. Can lambda be passed as native function parameter?
            Console.WriteLine("\nTest 4: Can lambda be passed as native function parameter?");
            var r4 = interpreter.Evaluate(new StringReader(@"
                (test-native-fn (lambda () 42))
            "));
            if (r4.Error != null)
            {
                Console.WriteLine($"Test 4 Failed: {r4.Error}");
            }
            else
            {
                Console.WriteLine($"Test 4 Success.");
            }

            // 5. Test varargs define syntax
            Console.WriteLine("\nTest 5: Test varargs define syntax");
            var r5 = interpreter.Evaluate(new StringReader(@"
                (define (test-varargs first . rest)
                  rest)
                (test-varargs 1 2 3)
            "));
            if (r5.Error != null)
            {
                Console.WriteLine($"Test 5 Failed: {r5.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5 Success. Result type: {r5.Result?.GetType().FullName}, value: {r5.Result}");
            }

            // Test 5b: lambda with symbol list parameter
            Console.WriteLine("\nTest 5b: lambda with symbol list parameter");
            var r5b = interpreter.Evaluate(new StringReader(@"
                (define test-varargs-b (lambda args args))
                (test-varargs-b 1 2 3)
            "));
            if (r5b.Error != null)
            {
                Console.WriteLine($"Test 5b Failed: {r5b.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5b Success. Result type: {r5b.Result?.GetType().FullName}, value: {r5b.Result}");
                if (r5b.Result is List<object> list5b)
                {
                    Console.WriteLine($"  Length: {list5b.Count}");
                    foreach (var x in list5b)
                    {
                        Console.WriteLine($"    Item: {x} (Type: {x?.GetType().FullName})");
                    }
                }
            }

            // Test 5c: define with single symbol parameter list
            Console.WriteLine("\nTest 5c: define with single symbol parameter list");
            var r5c = interpreter.Evaluate(new StringReader(@"
                (define (test-varargs-c args) args)
                (test-varargs-c 1 2 3)
            "));
            if (r5c.Error != null)
            {
                Console.WriteLine($"Test 5c Failed (as expected if it expects 1 arg): {r5c.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5c Success (unexpected if it expects 1 arg). Result type: {r5c.Result?.GetType().FullName}, value: {r5c.Result}");
            }

            // Test 5d: lambda with dot-varargs parameter list
            Console.WriteLine("\nTest 5d: lambda with dot-varargs parameter list");
            var r5d = interpreter.Evaluate(new StringReader(@"
                (define test-varargs-d (lambda (first . rest) rest))
                (test-varargs-d 1 2 3)
            "));
            if (r5d.Error != null)
            {
                Console.WriteLine($"Test 5d Failed: {r5d.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5d Success. Result type: {r5d.Result?.GetType().FullName}, value: {r5d.Result}");
            }

            // Test 5e: test node constructor implementation
            Console.WriteLine("\nTest 5e: test node constructor implementation");
            var r5e = interpreter.Evaluate(new StringReader(@"
                (define (cadr xs) (car (cdr xs)))
                (define (get-kwarg kwargs key default)
                  (if (null? kwargs)
                      default
                      (if (null? (cdr kwargs))
                          default
                          (if (equal? (car kwargs) key)
                              (cadr kwargs)
                              (get-kwarg (cdr (cdr kwargs)) key default)))))
                (define node
                  (lambda args
                    (let ((name (car args))
                          (kwargs (cdr args)))
                      (list 'node
                            name
                            (get-kwarg kwargs ':children '())
                            (get-kwarg kwargs ':effect #f)))))
                (node ""写代码"" ':effect (lambda () 42))
            "));
            if (r5e.Error != null)
            {
                Console.WriteLine($"Test 5e Failed: {r5e.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5e Success. Result type: {r5e.Result?.GetType().FullName}, value: {r5e.Result}");
                if (r5e.Result is List<object> list5e)
                {
                    Console.WriteLine($"  Length: {list5e.Count}");
                    for (int i = 0; i < list5e.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list5e[i]} (Type: {list5e[i]?.GetType().FullName})");
                    }
                }
            }
        }
    }
}
