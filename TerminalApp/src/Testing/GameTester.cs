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
                scenesDir = Path.GetFullPath(Path.Combine("..", "Content", "scenes"));
            }
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath(Path.Combine("Content", "scenes"));
            }
            if (!Directory.Exists(scenesDir))
            {
                throw new DirectoryNotFoundException("Scenes directory not found under standard content paths.");
            }

            foreach (var scenePath in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(scenesDir, scenePath);
                var sceneName = Path.Combine(Path.GetDirectoryName(relativePath) ?? "", Path.GetFileNameWithoutExtension(relativePath)).Replace('\\', '/');

                if (sceneName == "world/home" || sceneName == "world/office" || sceneName == "world/club" || sceneName == "world/board" || sceneName == "world/merchant" || sceneName == "world/test")
                {
                    continue;
                }

                var gameState = new GameState();
                var sceneManager = new SceneManager(gameState, new LocalScriptLoader());

                try
                {
                    sceneManager.LoadScene(sceneName);
                }
                catch (Exception ex)
                {
                    string targetFile = scenePath;
                    var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"Error loading Scheme script '([^']+)'");
                    if (match.Success)
                    {
                        string relPath = match.Groups[1].Value;
                        string possiblePath = Path.Combine(Path.GetDirectoryName(scenesDir) ?? "", relPath);
                        if (File.Exists(possiblePath))
                        {
                            targetFile = possiblePath;
                        }
                        else
                        {
                            possiblePath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(scenesDir) ?? "") ?? "", relPath);
                            if (File.Exists(possiblePath))
                            {
                                targetFile = possiblePath;
                            }
                        }
                    }
                    CheckParenthesesDiagnostics(targetFile);
                    throw;
                }

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
            var sceneManager = new SceneManager(gameState, new LocalScriptLoader());

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

            // Knocking is no longer required, home now directly contains interior nodes

            // Verify initial clock state for work
            var workClock = sceneManager.CurrentClocks.Find(c => c.Label == "工作进度");
            if (workClock == null)
            {
                throw new InvalidOperationException("Expected '工作进度' clock to be present in office scene.");
            }
            if (workClock.Current != 0)
            {
                throw new InvalidOperationException($"Expected initial work clock to be 0, got {workClock.Current}.");
            }

            ExecuteNode(sceneManager, "写代码");
            ExecuteNode(sceneManager, "写代码");

            // After 2 works, the rule "工资发放" should trigger, awarding 50 money and resetting the clock to 0
            var money = gameState.Get<int>("money");
            if (money != 100)
            {
                throw new InvalidOperationException($"Expected money to be 100 after receiving salary, got {money}.");
            }

            if (workClock.Current != 0)
            {
                throw new InvalidOperationException($"Expected work clock to reset to 0 after triggering salary rule, got {workClock.Current}.");
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
            ExecuteActionWithDefaults(sceneManager, slasherSuppress);
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
            ExecuteActionWithDefaults(sceneManager, gunnerKill);
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
                    ExecuteActionWithDefaults(sceneManager, killAction);
                }
                else
                {
                    // Fallback to suppress if kill action isn't found
                    var suppressAction = FindNode(enemyNode.Children, "压制");
                    if (suppressAction != null)
                    {
                        ExecuteActionWithDefaults(sceneManager, suppressAction);
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

            // We should be back at world scene
            if (sceneManager.CurrentSceneName != "world")
            {
                throw new InvalidOperationException($"Expected to return to 'world' scene after escaping, got '{sceneManager.CurrentSceneName}'");
            }

            Console.WriteLine("Minimal flow simulation passed.");
        }

        private static void ExecuteActionWithDefaults(SceneManager sceneManager, GameNode node)
        {
            var slots = new List<SlottedResource?>();
            if (node.Requires != null)
            {
                foreach (var req in node.Requires)
                {
                    if (req.Type == "die")
                    {
                        slots.Add(new SlottedResource { Type = "die", Value = 6 });
                    }
                    else if (req.Type == "item")
                    {
                        slots.Add(new SlottedResource { Type = "item", ItemName = req.ItemName, Value = req.Qty });
                    }
                }
            }
            sceneManager.ExecuteAction(node, slots);
        }

        private static void ExecuteNode(SceneManager sceneManager, string name)
        {
            var node = FindNode(sceneManager.CurrentWorldNodes, name);
            if (node == null)
            {
                throw new InvalidOperationException($"Node not found: {name}");
            }

            ExecuteActionWithDefaults(sceneManager, node);
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

            // Test 9: Check whether raw Schemy and project stdlib expose `and` and `or`
            Console.WriteLine("\nTest 9: Test `and` and `or` availability");
            
            // Raw Schemy test for 'and'
            var r9RawAnd = interpreter.Evaluate(new StringReader("(and #t #t)"));
            if (r9RawAnd.Error != null)
            {
                Console.WriteLine($"Test 9 Raw Schemy 'and' Failed (expected): {r9RawAnd.Error.GetType().FullName}: {r9RawAnd.Error.Message}");
            }
            else
            {
                Console.WriteLine($"Test 9 Raw Schemy 'and' Success. Result: {r9RawAnd.Result}");
            }

            // Raw Schemy test for 'or'
            var r9RawOr = interpreter.Evaluate(new StringReader("(or #f #t)"));
            if (r9RawOr.Error != null)
            {
                Console.WriteLine($"Test 9 Raw Schemy 'or' Failed (expected): {r9RawOr.Error.GetType().FullName}: {r9RawOr.Error.Message}");
            }
            else
            {
                Console.WriteLine($"Test 9 Raw Schemy 'or' Success. Result: {r9RawOr.Result}");
            }

            var projectInterpreter = new SchemeInterpreter(new GameState(), new LocalScriptLoader());
            
            // Define a helper to run project test case and assert/print result
            Action<string, object> runProjectTest = (expression, expected) =>
            {
                try
                {
                    var result = projectInterpreter.Eval(expression);
                    bool match = Equals(result, expected);
                    Console.WriteLine($"  {expression} => {result} (Expected: {expected}) - {(match ? "PASS" : "FAIL")}");
                    if (!match)
                    {
                        throw new InvalidOperationException($"Test failed for: {expression}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {expression} => ERROR: {ex.GetType().FullName}: {ex.Message} - FAIL");
                    throw;
                }
            };

            Console.WriteLine("Testing 'and' and 'or' under project stdlib:");
            runProjectTest("(and)", true);
            runProjectTest("(and #t)", true);
            runProjectTest("(and #f)", false);
            runProjectTest("(and #t #t)", true);
            runProjectTest("(and #t #f)", false);
            runProjectTest("(and #t #t #f)", false);
            runProjectTest("(and #t #t #t)", true);

            runProjectTest("(or)", false);
            runProjectTest("(or #f)", false);
            runProjectTest("(or #t)", true);
            runProjectTest("(or #f #f)", false);
            runProjectTest("(or #f #t)", true);
            runProjectTest("(or #f #f #t)", true);
            runProjectTest("(or #f #f #f)", false);

            runProjectTest("(and (or #f #t) #t)", true);
            runProjectTest("(and (or #f #f) #t)", false);
            runProjectTest("(or (and #t #f) #t)", true);
            runProjectTest("(or (and #t #f) (and #f #t))", false);
            Console.WriteLine("Test 9 Project stdlib 'and'/'or' Completed Successfully.");

            // Test 10: Check availability of common Scheme symbols and keywords
            Console.WriteLine("\nTest 10: Unified Scheme Feature / Symbol Matrix");

            var symbolsToTest = new string[]
            {
                "+", "-", "*", "/", "=", "<", ">", "<=", ">=", "abs", "modulo", "remainder", "quotient", "even?", "odd?", "zero?",
                "eq?", "eqv?", "equal?",
                "null?", "pair?", "list?", "number?", "string?", "symbol?", "procedure?", "boolean?",
                "cons", "car", "cdr", "cadr", "caddr", "cadddr", "list", "length", "append", "reverse", "member", "assoc",
                "map", "filter", "apply",
                "string-append", "number->string", "display", "newline", "error", "not", "and", "or"
            };

            var expressionsToTest = new string[]
            {
                "(let ((x 1)) x)",
                "(let* ((x 1) (y (+ x 1))) y)",
                "(begin 1 2)",
                "(if #t 1 2)",
                "(cond (#f 1) (#t 2) (else 3))",
                "(case 2 ((1) 'one) ((2) 'two) (else 'other))",
                "((lambda () 42))",
                "(begin (define test-val 99) test-val)"
            };

            Console.WriteLine(string.Format("{0,-18} | {1,-15} | {2,-30}", "Symbol/Expr", "Raw Schemy", "Project Interpreter (with stdlib)"));
            Console.WriteLine(new string('-', 75));

            foreach (var sym in symbolsToTest)
            {
                // Test raw
                var rawResult = interpreter.Evaluate(new StringReader(sym));
                string rawStatus = rawResult.Error != null ? "NO" : "YES";

                // Test project
                string projStatus;
                try
                {
                    var result = projectInterpreter.Eval(sym);
                    projStatus = "YES";
                }
                catch
                {
                    projStatus = "NO";
                }

                Console.WriteLine(string.Format("{0,-18} | {1,-15} | {2,-30}", sym, rawStatus, projStatus));
            }

            Console.WriteLine("\nSyntax & Special Forms Expression Check:");
            Console.WriteLine(new string('-', 75));

            foreach (var expr in expressionsToTest)
            {
                // Test raw
                var rawResult = interpreter.Evaluate(new StringReader(expr));
                string rawStatus = rawResult.Error != null ? "FAIL" : "PASS";

                // Test project
                string projStatus;
                try
                {
                    projectInterpreter.Eval(expr);
                    projStatus = "PASS";
                }
                catch
                {
                    projStatus = "FAIL";
                }

                Console.WriteLine(string.Format("{0,-45} | {1,-10} | {2,-10}", expr, rawStatus, projStatus));
            }
        }

        private static void CheckParenthesesDiagnostics(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                string code = File.ReadAllText(filePath);
                var stack = new Stack<(int line, int col)>();
                string[] lines = code.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
                
                bool inString = false;
                bool escape = false;
                
                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    string line = lines[lineIdx];
                    escape = false;
                    for (int colIdx = 0; colIdx < line.Length; colIdx++)
                    {
                        char c = line[colIdx];
                        if (inString)
                        {
                            if (escape)
                            {
                                escape = false;
                            }
                            else if (c == '\\')
                            {
                                escape = true;
                            }
                            else if (c == '"')
                            {
                                inString = false;
                            }
                            continue;
                        }
                        
                        if (c == ';')
                        {
                            break;
                        }
                        
                        if (c == '"')
                        {
                            inString = true;
                            escape = false;
                            continue;
                        }
                        
                        if (c == '(')
                        {
                            stack.Push((lineIdx + 1, colIdx + 1));
                        }
                        else if (c == ')')
                        {
                            if (stack.Count == 0)
                            {
                                Console.Error.WriteLine($"\n[DIAGNOSTICS] Parenthesis mismatch in {filePath}:");
                                Console.Error.WriteLine($"  Extra closing parenthesis ')' found at Line {lineIdx + 1}, Col {colIdx + 1}.");
                                Console.Error.WriteLine($"  Line content: {line.Trim()}");
                                return;
                            }
                            stack.Pop();
                        }
                    }
                }
                
                if (stack.Count > 0)
                {
                    var unmatched = stack.Pop();
                    Console.Error.WriteLine($"\n[DIAGNOSTICS] Parenthesis mismatch in {filePath}:");
                    Console.Error.WriteLine($"  Unclosed opening parenthesis '(' started at Line {unmatched.line}, Col {unmatched.col}.");
                    Console.Error.WriteLine($"  Line content: {lines[unmatched.line - 1].Trim()}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DIAGNOSTICS] Failed to perform parenthesis checks: {ex.Message}");
            }
        }
    }
}
