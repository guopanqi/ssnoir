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

            // Verify initial state: 0 trash
            if (FindNode(sceneManager.CurrentWorldNodes, "清理垃圾") != null)
            {
                throw new InvalidOperationException("Expected no '清理垃圾' nodes initially.");
            }

            // Kick the bin twice
            ExecuteNode(sceneManager, "踢垃圾桶");
            ExecuteNode(sceneManager, "踢垃圾桶");

            // Verify 2 trash cards are present
            int trashCount = CountNodes(sceneManager.CurrentWorldNodes, "清理垃圾");
            if (trashCount != 2)
            {
                throw new InvalidOperationException($"Expected 2 '清理垃圾' nodes after kicking twice, got {trashCount}");
            }

            // Clean one trash card
            ExecuteNode(sceneManager, "清理垃圾");

            // Verify 1 trash card remains
            trashCount = CountNodes(sceneManager.CurrentWorldNodes, "清理垃圾");
            if (trashCount != 1)
            {
                throw new InvalidOperationException($"Expected 1 '清理垃圾' node remaining, got {trashCount}");
            }

            // Clean the last trash card
            ExecuteNode(sceneManager, "清理垃圾");

            // Verify 0 trash cards remain
            if (FindNode(sceneManager.CurrentWorldNodes, "清理垃圾") != null)
            {
                throw new InvalidOperationException("Expected all '清理垃圾' nodes to be cleaned.");
            }

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

            // ── Combat Scene Simulation ──────────────────
            gameState.Set("location", "combat");
            
            // Initial health
            var initialHealth = gameState.Get<int>("health");
            if (initialHealth != 100)
            {
                throw new InvalidOperationException($"Expected initial health to be 100, got {initialHealth}");
            }

            // Initial clocks (Exit clock + Spawn clock + 2 * 2 enemy clocks = 6 clocks)
            if (sceneManager.CurrentClocks.Count != 6)
            {
                throw new InvalidOperationException($"Expected 6 clocks, got {sceneManager.CurrentClocks.Count}");
            }

            // Clocks should be: "逃脱", "增援", and "HP"/"A" for enemies
            if (sceneManager.CurrentClocks[0].Label != "逃脱" ||
                sceneManager.CurrentClocks[1].Label != "增援" ||
                sceneManager.CurrentClocks[2].Label != "HP" ||
                sceneManager.CurrentClocks[3].Label != "A" ||
                sceneManager.CurrentClocks[4].Label != "HP" ||
                sceneManager.CurrentClocks[5].Label != "A")
            {
                throw new InvalidOperationException("Clocks label mismatch in combat scene");
            }

            // Attack the "持刀者" (suppress it)
            var slasherNode = FindNode(sceneManager.CurrentWorldNodes, "持刀者");
            if (slasherNode == null)
            {
                throw new InvalidOperationException("Expected '持刀者' enemy node to be present.");
            }
            var slasherSuppress = FindNode(slasherNode.Children, "压制");
            if (slasherSuppress == null)
            {
                throw new InvalidOperationException("Expected '压制' action to be present under '持刀者'.");
            }
            sceneManager.ExecuteEffect(slasherSuppress);
            sceneManager.EndTurn();

            // Since we ended the turn, all live enemies' clocks should have ticked.
            // Spawn clock (idx 1), Slasher's atk (idx 3), Gunner's atk (idx 5) ticked to 1.
            if (sceneManager.CurrentClocks[1].Current != 1 || 
                sceneManager.CurrentClocks[3].Current != 1 || 
                sceneManager.CurrentClocks[5].Current != 1)
            {
                throw new InvalidOperationException($"Expected clocks to tick to 1, got {sceneManager.CurrentClocks[1].Current}, {sceneManager.CurrentClocks[3].Current}, {sceneManager.CurrentClocks[5].Current}");
            }

            // Let's hit "持枪手" with "击倒" (eliminate! decreases HP by 2, so it dies)
            // But since this is Turn 2, the spawn clock ticks to 2/2 -> spawns a new random enemy!
            var gunnerNode = FindNode(sceneManager.CurrentWorldNodes, "持枪手");
            if (gunnerNode == null)
            {
                throw new InvalidOperationException("Expected '持枪手' enemy node to be present.");
            }
            var gunnerKill = FindNode(gunnerNode.Children, "击倒");
            if (gunnerKill == null)
            {
                throw new InvalidOperationException("Expected '击倒' action to be present under '持枪手'.");
            }
            sceneManager.ExecuteEffect(gunnerKill);
            sceneManager.EndTurn();

            // 4 clocks should still remain: "逃脱", "增援", and Slasher's HP and atk clocks (reinforcement has not triggered yet as spawn-clock max is 3)
            if (sceneManager.CurrentClocks.Count != 4)
            {
                throw new InvalidOperationException($"Expected 4 clocks after killing Gunner, got {sceneManager.CurrentClocks.Count}");
            }

            // Keep attacking any remaining enemy until all are eliminated
            while (true)
            {
                GameNode? enemyNode = null;
                var battleNode = FindNode(sceneManager.CurrentWorldNodes, "仓库");
                if (battleNode != null)
                {
                    foreach (var child in battleNode.Children)
                    {
                        if (child.Name != "冲向出口" && child.Name != "战场")
                        {
                            enemyNode = child;
                            break;
                        }
                    }
                }

                if (enemyNode == null)
                {
                    break;
                }

                var killAction = FindNode(enemyNode.Children, "击倒");
                if (killAction != null)
                {
                    sceneManager.ExecuteEffect(killAction);
                }
                else
                {
                    // Fallback to suppress if kill action isn't found
                    var suppressAction = FindNode(enemyNode.Children, "压制");
                    if (suppressAction != null)
                    {
                        sceneManager.ExecuteEffect(suppressAction);
                    }
                    else
                    {
                        break;
                    }
                }

                sceneManager.EndTurn();
            }

            // Verify all enemies are dead (only Exit clock and Spawn clock remain = 2 clocks)
            if (sceneManager.CurrentClocks.Count != 2 || 
                sceneManager.CurrentClocks[0].Label != "逃脱" ||
                sceneManager.CurrentClocks[1].Label != "增援")
            {
                foreach (var c in sceneManager.CurrentClocks)
                {
                    Console.WriteLine($"[TEST CLOCK DETECTED] Label: {c.Label}, Current: {c.Current}, Max: {c.Max}");
                }
                throw new InvalidOperationException($"Expected only Exit and Spawn clocks to remain, got {sceneManager.CurrentClocks.Count}");
            }

            // Tick escape clock 12 times to escape (new max is 12)
            for (int i = 0; i < 12; i++)
            {
                ExecuteNode(sceneManager, "冲向出口");
            }

            // We should be back at home scene
            if (sceneManager.CurrentSceneName != "home")
            {
                throw new InvalidOperationException($"Expected to return to 'home' scene after escaping, got '{sceneManager.CurrentSceneName}'");
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
                if (node.Name.StartsWith(name))
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

        private static int CountNodes(List<GameNode> nodes, string name)
        {
            int count = 0;
            foreach (var node in nodes)
            {
                if (node.Name.StartsWith(name))
                {
                    count++;
                }
                count += CountNodes(node.Children, name);
            }
            return count;
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

            interpreter.DefineGlobal(Symbol.FromString("string-append"), new NativeProcedure(args =>
            {
                return string.Concat(args);
            }, "string-append"));

            interpreter.DefineGlobal(Symbol.FromString("number->string"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("number->string requires 1 argument");
                return args[0]?.ToString() ?? "";
            }, "number->string"));

            var rand = new Random();
            interpreter.DefineGlobal(Symbol.FromString("random-choice"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("random-choice requires 1 argument: a list of options");

                if (args[0] is List<object> list)
                {
                    if (list.Count == 0)
                        return null;
                    return list[rand.Next(list.Count)];
                }

                throw new ArgumentException("random-choice argument must be a list");
            }, "random-choice"));

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

            // Test 6: Test map, apply append, and make-enemy closure
            Console.WriteLine("\nTest 6: Test map, apply append, and make-enemy closure");
            var r6 = interpreter.Evaluate(new StringReader(@"
                (define (make-clock label max)
                  (let ((current 0))
                    (lambda (msg)
                      (cond
                        ((equal? msg 'tick!)       (set! current (+ current 1)))
                        ((equal? msg 'reset!)      (set! current 0))
                        ((equal? msg 'full?)       (>= current max))
                        ((equal? msg 'render-data) (list 'clock label current max))
                        (else #f)))))

                (define (make-enemy type hp-max atk-max dmg)
                  (let ((hp hp-max)
                        (atk-clock (make-clock type atk-max)))
                    (let ((suppress! (lambda ()
                                       (set! hp (- hp 1))
                                       (atk-clock 'reset!)))
                          (eliminate! (lambda ()
                                        (set! hp (- hp 2)))))
                      (lambda (msg)
                        (cond
                          ((equal? msg 'type)      type)
                          ((equal? msg 'hp)        hp)
                          ((equal? msg 'dead?)     (<= hp 0))
                          ((equal? msg 'tick-atk!) (atk-clock 'tick!))
                          ((equal? msg 'atk-full?) (atk-clock 'full?))
                          ((equal? msg 'reset-atk!)(atk-clock 'reset!))
                          ((equal? msg 'render-data)
                           (list
                             (atk-clock 'render-data)
                             (list 'node type
                                   (list
                                     (list 'node ""压制"" '() (lambda () (suppress!)))
                                     (list 'node ""击倒"" '() (lambda () (eliminate!))))
                                   #f)))
                          (else #f))))))

                (define enemy (make-enemy ""持刀者"" 2 2 1))
                (enemy 'render-data)
            "));
            if (r6.Error != null)
            {
                Console.WriteLine($"Test 6 Failed: {r6.Error}");
            }
            else
            {
                Console.WriteLine($"Test 6 Success. Result type: {r6.Result?.GetType().FullName}, value: {r6.Result}");
                if (r6.Result is List<object> list6)
                {
                    Console.WriteLine($"  Length: {list6.Count}");
                    for (int i = 0; i < list6.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list6[i]} (Type: {list6[i]?.GetType().FullName})");
                    }
                }
            }

            // Test 7: Test map and apply builtins
            Console.WriteLine("\nTest 7: Test map and apply builtins");
            var r7 = interpreter.Evaluate(new StringReader(@"
                (list (map (lambda (x) (+ x 1)) '(1 2 3))
                      (apply + '(1 2 3))
                      (apply append '((1 2) (3 4))))
            "));
            if (r7.Error != null)
            {
                Console.WriteLine($"Test 7 Failed: {r7.Error}");
            }
            else
            {
                Console.WriteLine($"Test 7 Success. Result: {r7.Result}");
                if (r7.Result is List<object> list7)
                {
                    Console.WriteLine($"  Length: {list7.Count}");
                    for (int i = 0; i < list7.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list7[i]} (Type: {list7[i]?.GetType().FullName})");
                    }
                }
            }

            // Test 8: Test string-append and number->string
            Console.WriteLine("\nTest 8: Test string-append and number->string");
            var r8 = interpreter.Evaluate(new StringReader(@"
                (string-append ""HP: "" (number->string 3))
            "));
            if (r8.Error != null)
            {
                Console.WriteLine($"Test 8 Failed: {r8.Error}");
            }
            else
            {
                Console.WriteLine($"Test 8 Success. Result: {r8.Result}");
            }
        }
    }
}
