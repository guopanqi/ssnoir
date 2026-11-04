using SSNoir.Core;
using SSNoir.Scripting;
using System;
using System.Linq;

namespace SSNoir.Testing;

internal static class WorldContentAwarenessChecks
{
    public static void Run()
    {
        static GameNode N(string id, params GameNode[] children) => new() { Id = id, Name = id, Children = children.ToList() };
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception(message); Console.WriteLine("[world-content] OK: " + message); }
        static bool Marked(WorldContentView v, string id) => v.MarkedNodes.Contains(id);
        var tracker = new WorldContentAwareness();
        var root = N("world", N("A", N("old")));
        tracker.Observe(root);
        Check(tracker.Capture(root).MarkedNodes.Count == 0, "初始世界建立基线");
        root = N("world", N("A", N("old")), N("B", N("group", N("action"))));
        tracker.Observe(root);
        var view = tracker.Capture(root);
        Check(Marked(view, "B") && !Marked(view, "group") && !Marked(view, "action"), "整棵新增子树只标入口");
        tracker.Acknowledge(view, new[] { "B" });
        Check(!Marked(tracker.Capture(root), "B"), "进入新增地点不制造后代标记");
        root.Children[1].Children[0].Children.Add(N("later"));
        tracker.Observe(root);
        view = tracker.Capture(root);
        Check(Marked(view, "later") && Marked(view, "group") && Marked(view, "B"), "已登记子树中的后续新增向上汇总");
        tracker.Acknowledge(view, new[] { "B", "group" });
        Check(Marked(tracker.Capture(root), "B"), "确认祖先不会吞掉未查看的深层行动");
        tracker.Acknowledge(view, new[] { "later" });
        Check(!Marked(tracker.Capture(root), "B"), "最后一个子节点确认后祖先自然清除");
        var old = root.Children[0].Children[0];
        old.NoticeVersion = "ready"; tracker.Observe(root); var ready = tracker.Capture(root);
        old.NoticeVersion = "finished"; tracker.Observe(root);
        tracker.Acknowledge(ready, new[] { "old" });
        Check(Marked(tracker.Capture(root), "old"), "旧快照不能确认新版本");
        var repainted = tracker.Capture(root, ready.Revisions);
        tracker.Acknowledge(repainted, new[] { "old" });
        Check(Marked(tracker.Capture(root), "old"), "刷新旧快照也不能偷换版本");
        var saved = tracker.Save(); var copy = new WorldContentAwareness(); copy.Load(saved); copy.Observe(root);
        Check(Marked(copy.Capture(root), "old"), "未读状态跨存档恢复");
        copy.Acknowledge(copy.Capture(root), new[] { "old" });
        old.Subtitle = "剩余两天"; old.Disabled = true; old.Requires.Add(new ActionCost { Type = "item", ItemId = "金钱", Qty = 5 });
        copy.Observe(root);
        Check(copy.Capture(root).MarkedNodes.Count == 0, "普通描述、禁用和成本变化不产生提醒");
        old.NoticeVersion = "another"; copy.Observe(root); root.Children[0].Children.Clear(); copy.Observe(root);
        Check(!Marked(copy.Capture(root), "A"), "节点消失不会留下幽灵标记");
        root.Children[0].Children.Add(old); copy.Observe(root);
        Check(!Marked(copy.Capture(root), "old"), "原样重现不当作新节点");
        var legacy = new WorldContentAwareness(); legacy.Load(null); legacy.Observe(root);
        Check(legacy.Capture(root).MarkedNodes.Count == 0, "旧档恢复建立基线");
        var state = new GameState(); var scene = new SceneManager(state, new LocalScriptLoader()); scene.LoadScene("world");
        scene.ActiveInterpreter.Eval("(define awareness-fixture (node \"fixture\" :notice-version \"ready\"))");
        var node = NodeConverter.ConvertSingle(scene.ActiveInterpreter.Eval("awareness-fixture"), scene.ActiveInterpreter.RawInterpreter);
        Check(node.NoticeVersion == "ready", "Scheme版本声明进入节点");
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssnoir-awareness-" + Guid.NewGuid() + ".json");
        try
        {
            var data = new SaveData { WorldContent = saved, WorldData = new System.Collections.Generic.List<object>() };
            SaveManager.Write(path, data);
            var roundtrip = SaveManager.Read(path);
            var restored = new WorldContentAwareness(); restored.Load(roundtrip.WorldContent); restored.Observe(root);
            Check(Marked(restored.Capture(root), "old"), "JSON存档保存独立阅读状态");
        }
        finally { System.IO.File.Delete(path); }
        Console.WriteLine("[world-content] All checks passed.");
    }
}
