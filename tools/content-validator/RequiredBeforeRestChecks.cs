using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSNoir.Core;

namespace SSNoir.Testing;

public static class RequiredBeforeRestChecks
{
    private static void Check(bool ok, string text)
    {
        if (!ok) throw new Exception("[required-before-rest] " + text);
        Console.WriteLine("[required-before-rest] OK: " + text);
    }
    private static GameNode Find(GameNode node, string id)
    {
        if (node.Id == id) return node;
        foreach (var child in node.Children)
        {
            try { return Find(child, id); } catch (KeyNotFoundException) { }
        }
        throw new KeyNotFoundException(id);
    }
    private static void Throws(Action action, string text)
    {
        try { action(); } catch (Exception) { Console.WriteLine("[required-before-rest] OK: " + text); return; }
        throw new Exception("[required-before-rest] expected rejection: " + text);
    }
    public static void Run()
    {
        var state = new GameState(); var scenes = new SceneManager(state, new LocalScriptLoader());
        scenes.LoadScene("world");
        scenes.ActiveInterpreter.Eval(@"
          (define pending-a #t) (define pending-b #t) (define next-day-ran? #f)
          (set! turn-rules '()) (set! turn-start-rules '())
          (define-turn-rule ""测试换日"" (lambda () #t) (lambda () (set! next-day-ran? #t)))
          (define (get-render-data)
            (container ""世界"" (list
              (place ""家"" :children (list (node ""睡觉"" :disabled (rest-blocked?)
                :resolve (instant (lambda () (end-turn!))))))
              (place ""事件地点"" :children (append
                (if pending-a (list (node ""事件甲"" :required-before-rest ""先处理甲""
                  :resolve (instant (lambda () (set! pending-a #f))))) '())
                (if pending-b (list (node ""事件乙"" :required-before-rest ""先处理乙""
                  :resolve (instant (lambda () (set! pending-b #f))))) '()))))))");
        scenes.RebuildRenderTree();
        Check(state.RestBlockers.Count == 2 && state.RestBlockers.All(b => b.LocationName == "事件地点"),
            "扫描全部地点，自动取得位置与节点身份");
        Check(Find(scenes.CurrentRootNode!, "睡觉").Disabled, "同一份快照立即禁用休息");
        Throws(() => scenes.EndTurn(), "C# 直接换日也不能绕过");
        Throws(() => scenes.ActiveInterpreter.Eval("(end-turn!)"), "Scheme 直接换日不能绕过");
        Check((bool)scenes.ActiveInterpreter.Eval("(not next-day-ran?)"), "拒绝换日前未执行日终副作用");
        scenes.ExecuteAction(Find(scenes.CurrentRootNode!, "事件甲"), new List<SlottedResource?>());
        Check(state.RestBlockers.Count == 1 && state.RestBlockers[0].TargetNodeId == "事件乙",
            "节点消失即解除，只影响自己的事件");
        scenes.ExecuteAction(Find(scenes.CurrentRootNode!, "事件乙"), new List<SlottedResource?>());
        Check(state.RestBlockers.Count == 0 && !Find(scenes.CurrentRootNode!, "睡觉").Disabled,
            "最后一个节点消失，休息立即恢复");
        scenes.EndTurn();
        Check((bool)scenes.ActiveInterpreter.Eval("next-day-ran?"), "解除后正常执行换日");
        scenes.ActiveInterpreter.Eval("(begin (set! pending-a #t) (set! next-day-ran? #f))");
        Throws(() => scenes.EndTurn(), "直接调用前重新读取最新剧情状态，不沿用旧快照");
        Check(!(bool)scenes.ActiveInterpreter.Eval("next-day-ran?"), "新出现的事件同样在副作用前拦截");
        scenes.ActiveInterpreter.Eval("(set! pending-a #f)"); scenes.EndTurn();


        var parser = scenes.ActiveInterpreter;
        Throws(() => { SSNoir.Scripting.NodeConverter.ConvertSingle(
            parser.Eval("(node \"坏字段\" :required-before-rest #t)"), parser.RawInterpreter); },
            "字段必须是非空字符串");
        var disabled = new GameNode { Id = "禁用事件", Disabled = true, RequiredBeforeRest = "处理", Resolve = Find(scenes.CurrentRootNode!, "睡觉").Resolve };
        var place = new GameNode { Id = "测试地点", IsPlace = true, Children = new() { disabled } };
        var root = new GameNode { Id = "测试世界", Children = new() { place } };
        Throws(() => RequiredBeforeRest.Collect(root, true), "必处理节点不能被禁用");
        disabled.Disabled = false;
        Throws(() => RequiredBeforeRest.Collect(root, false), "Scenario不能声明世界休息条件");
        place.IsPlace = false;
        Throws(() => RequiredBeforeRest.Collect(root, true), "必须有可进入的世界地点");

        var real = new SceneManager(new GameState(), new LocalScriptLoader()); real.LoadScene("world");
        Check(real.GameState.RestBlockers.Single().TargetNodeId == "旅馆清晨", "新档不调用同步函数也能得到开场事件");
        string path = Path.Combine(Path.GetTempPath(), "ssnoir-required-node-save.json");
        try
        {
            real.SaveGame(path);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(path);
            Check(restored.GameState.RestBlockers.Single().TargetNodeId == "旅馆清晨", "读档只恢复剧情，节点树重建必须事项");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        real.StartScenario("醉客互相叫骂");
        Check(real.GameState.RestBlockers.Count == 0, "进入Scenario不携带世界休息条件");
        real.EndTurn();
        Check(real.CurrentSceneName == "醉客互相叫骂", "世界必须事项不阻止Scenario结束回合");
        real.EndScenario();
        Check(real.GameState.RestBlockers.Single().TargetNodeId == "旅馆清晨", "返回世界自动恢复尚未处理的节点声明");
        var chapter2 = new SceneManager(new GameState(), new LocalScriptLoader()); chapter2.LoadScene("world");
        chapter2.ActiveInterpreter.Eval("(debug-enter-chapter2!)"); chapter2.RebuildRenderTree();
        chapter2.EndTurn(); chapter2.EndTurn();
        Check(chapter2.GameState.RestBlockers.Any(b => b.TargetNodeId == "赴晚宴"), "旧第二章晚宴按节点声明自动成为必须事项");
        var phaseB = new SceneManager(new GameState(), new LocalScriptLoader()); phaseB.LoadScene("world");
        phaseB.ActiveInterpreter.Eval("(debug-enter-chapter2-phase-b!)"); phaseB.RebuildRenderTree();
        Check(phaseB.CurrentRootNode != null, "旧第二章 Phase B 完整世界树可构建");
        Console.WriteLine("[required-before-rest] 全部通过。");
    }
}
