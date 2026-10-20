using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing
{
    public static class NodeIdentityChecks
    {
        public static void Run()
        {
            VerifySameNamesAndExecutionById();
            VerifyPlaceNavigationById();
            VerifyDuplicateIdsRejected();
            VerifyAnchorDefaultsAndOverrides();
            VerifyPhotoStudioClockGroups();
            Console.WriteLine("[test-node-identity] Node identity contracts passed.");
        }

        private static void VerifySameNamesAndExecutionById()
        {
            var manager = NewManager(new DictionaryScriptLoader(new Dictionary<string, string>
            {
                ["scenes/encounters/node-identity.scm"] = """
                    (define selected "")
                    (define (on-encounter-enter) #t)
                    (define (on-encounter-collapse) (list 'test))
                    (define (on-action) #t)
                    (define choose-a (lambda () (set! selected "a")))
                    (define choose-b (lambda () (set! selected "b")))
                    (define (get-render-data)
                      (node "identity-root" :name "测试场景" :children
                        (list
                          (node "group-id" :name "相同显示名" :children
                            (list (node "nested-id" :name "相同显示名")))
                          (node "action-a" :name "同名动作"
                            :resolve (instant (outcome choose-a)))
                          (node "action-b" :name "同名动作"
                            :resolve (instant (outcome choose-b))))))
                    """
            }));
            manager.LoadScene("node-identity");

            var root = manager.CurrentRootNode ?? throw new InvalidOperationException("Identity scene did not load.");
            Require(root.Name == "测试场景", "The root display name was not read from :name.");
            Require(FindById(root, "group-id").Children.Single().Name == "相同显示名",
                "Same display names across levels did not load.");
            Require(root.Children.Count(n => n.Name == "同名动作") == 2,
                "Same display names at one level did not load.");

            var selectedAction = FindById(root, "action-b");
            manager.ExecuteAction(selectedAction, new List<SlottedResource?>());
            Require((string?)manager.ActiveInterpreter.Eval("selected") == "b",
                "Executing the node selected by Id did not run the matching action.");
        }

        private static void VerifyPlaceNavigationById()
        {
            var manager = NewManager(new DictionaryScriptLoader(new Dictionary<string, string>
            {
                ["scenes/world/world.scm"] = """
                    (define entered "")
                    (define (on-enter-place id) (set! entered id))
                    (define (get-dossier) '())
                    (define (get-render-data)
                      (node "world-root" :name "世界" :children
                        (list (place "place-a" :name "同名地点")
                              (place "place-b" :name "同名地点"))))
                    """
            }));
            manager.LoadScene("world");
            Require(manager.CurrentRootNode!.Children.Count(n => n.Name == "同名地点") == 2,
                "Places with a shared display name did not load.");

            manager.EnterPlace("place-b");
            Require((string?)manager.ActiveInterpreter.Eval("entered") == "place-b",
                "Place navigation did not select the matching Id.");
        }

        private static void VerifyDuplicateIdsRejected()
        {
            AssertDuplicateRejected("duplicate-sibling", """
                (define (on-encounter-enter) #t)
                (define (on-encounter-collapse) (list 'test))
                (define (get-render-data)
                  (node "root" :children
                    (list (node "reused-id" :name "甲")
                          (node "reused-id" :name "乙"))))
                """);

            AssertDuplicateRejected("duplicate-nested", """
                (define (on-encounter-enter) #t)
                (define (on-encounter-collapse) (list 'test))
                (define (get-render-data)
                  (node "reused-id" :name "父" :children
                    (list (node "reused-id" :name "子"))))
                """);
        }

        private static void AssertDuplicateRejected(string sceneName, string source)
        {
            var manager = NewManager(new DictionaryScriptLoader(new Dictionary<string, string>
            {
                [$"scenes/encounters/{sceneName}.scm"] = source
            }));
            try
            {
                manager.LoadScene(sceneName);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("重复节点 ID", StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException($"SceneManager accepted duplicate IDs in {sceneName}.");
        }

        private static void VerifyAnchorDefaultsAndOverrides()
        {
            var manager = NewManager(new DictionaryScriptLoader(new Dictionary<string, string>
            {
                ["scenes/encounters/node-identity.scm"] = """
                    (define (on-encounter-enter) #t)
                    (define (on-encounter-collapse) (list 'test))
                    (define (on-action) #t)
                    (define (get-render-data) (node "identity-root" :children '()))
                    """
            }));
            manager.LoadScene("node-identity");
            var interpreter = manager.ActiveInterpreter;
            GameNode Convert(string expression) => NodeConverter.ConvertSingle(
                interpreter.Eval(expression), interpreter.RawInterpreter);

            var defaultAnchor = Convert("(node \"default-anchor-id\")");
            Require(defaultAnchor.Name == "default-anchor-id", "A node without :name did not default Name to Id.");
            Require(defaultAnchor.EffectiveAnchorName == "default-anchor-id",
                "The default AnchorName did not use Id.");

            var first = Convert("(node \"anchor-owner-a\" :anchor \"shared-anchor\")");
            var second = Convert("(node \"anchor-owner-b\" :anchor \"shared-anchor\")");
            Require(first.EffectiveAnchorName == second.EffectiveAnchorName,
                "Distinct IDs could not explicitly share an AnchorName.");
        }

        private static void VerifyPhotoStudioClockGroups()
        {
            var manager = NewManager(new LocalScriptLoader());
            manager.LoadScene("照相馆交涉");
            var interpreter = manager.ActiveInterpreter;

            for (int stageA = 0; stageA < 3; stageA++)
            for (int stageB = 0; stageB < 3; stageB++)
            {
                SetStage("paper", "equipment", stageA);
                SetStage("permission", "records", stageB);
                manager.Refresh();
                var root = manager.CurrentRootNode!;
                VerifyRoute("加工比对", "照相馆-加工组内进度", "A：辨认相纸", "A1：核对设备", stageA);
                VerifyRoute("订单追查", "照相馆-订单组内进度", "B：争取查账", "B1：核对订单", stageB);

                void VerifyRoute(string groupId, string clockNodeId, string firstClock, string secondClock, int stage)
                {
                    var group = root.Children.FirstOrDefault(n => n.Id == groupId);
                    if (stage == 2)
                    {
                        Require(group == null, $"Completed route {groupId} still exposes its action group.");
                        return;
                    }

                    Require(group != null, $"Active route {groupId} is missing its group.");
                    var actionNodes = group!.Children.Where(n => n.Resolve?.Type == ResolveType.Roll).ToList();
                    var noteNodes = group.Children.Where(n => n.Resolve?.Type == ResolveType.Note).ToList();
                    Require(actionNodes.Count == 2, $"Active route {groupId} must have two actions.");
                    Require(noteNodes.Count == 1 && noteNodes[0].Id == clockNodeId && noteNodes[0].Resolve!.Clock != null,
                        $"Active route {groupId} must have one clock annotation with stable Id {clockNodeId}.");

                    string expectedLabel = stage == 0 ? firstClock : secondClock;
                    var outerClock = root.Children.FirstOrDefault(n => n.Resolve?.Clock?.Label == expectedLabel)?.Resolve?.Clock;
                    var innerClock = noteNodes[0].Resolve!.Clock!;
                    Require(outerClock != null, $"Outer clock {expectedLabel} is missing.");
                    Require(outerClock!.Current == innerClock.Current && outerClock.Max == innerClock.Max
                        && outerClock.Label == innerClock.Label,
                        $"Outer and group clock readings diverge for {groupId}/{expectedLabel}.");
                }
            }

            void SetStage(string first, string second, int stage)
            {
                int firstValue = stage == 0 ? 2 : 6;
                int secondValue = stage == 0 ? 0 : stage == 1 ? 3 : 6;
                interpreter.Eval($"({first} 'set! {firstValue})");
                interpreter.Eval($"({second} 'set! {secondValue})");
            }
        }

        private static GameNode FindById(GameNode node, string id)
        {
            if (node.Id == id) return node;
            foreach (var child in node.Children)
            {
                try { return FindById(child, id); }
                catch (InvalidOperationException) { }
            }
            throw new InvalidOperationException($"Node Id not found: {id}");
        }

        private static SceneManager NewManager(IScriptLoader loader) => new(new GameState(), loader);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class DictionaryScriptLoader : IScriptLoader
        {
            private readonly IReadOnlyDictionary<string, string> _scripts;
            private readonly LocalScriptLoader _fallback = new();

            public DictionaryScriptLoader(IReadOnlyDictionary<string, string> scripts) => _scripts = scripts;

            public string LoadScriptText(string path) => _scripts.TryGetValue(path, out string? source)
                ? source
                : _fallback.LoadScriptText(path);
        }
    }
}
