using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing;

internal static class InlineContainerChecks
{
    public static void Run()
    {
        static void Check(bool ok, string text)
        { if (!ok) throw new Exception(text); Console.WriteLine("[inline] OK: " + text); }
        var s = new SceneManager(new GameState(), new LocalScriptLoader()); s.LoadScene("world");
        GameNode Parse(string expression) => NodeConverter.ConvertSingle(s.ActiveInterpreter.Eval(expression), s.ActiveInterpreter.RawInterpreter);
        void Invalid(string expression)
        {
            bool rejected = false;
            try { Parse(expression); } catch { rejected = true; }
            Check(rejected, "拒绝非法展示声明：" + expression);
        }
        var page = Parse("(node \"page\" :children (list (instant-action \"child\" (lambda () #t))))");
        Check(page.IsContainer && page.Display == ContainerDisplay.Page, "旧容器默认仍是子页");
        var inline = Parse("(node \"group\" :display 'inline :children (list (instant-action \"child\" (lambda () #t))))");
        Check(inline.IsContainer && inline.IsInlineGroup && inline.Children.Single().Id == "child", "展开模式保持容器和子节点身份");
        Invalid("(node \"bad\" :display 'unknown)");
        Invalid("(node \"bad\" :display 'inline :resolve (instant (lambda () #t)))");
        Invalid("(node \"bad\" :display 'inline)");
        Invalid("(node \"bad\" :place #t :display 'inline :children (list (instant-action \"c\" (lambda () #t))))");
        var nested = new GameNode { Id = "outer", Display = ContainerDisplay.Inline, Children = new()
        { new GameNode { Id = "inner", Display = ContainerDisplay.Inline, Children = new() { new GameNode { Id = "a" } } } } };
        Check(nested.FindInlineParent("a")?.Id == "inner" && nested.ContainsNode("a"), "聚焦和Anchor继承能找到最近展开组");
        s.ActiveInterpreter.Eval("(define inline-test-clock (make-clock \"共享工作\" 4 'gauge)) (define (get-render-data) (node \"世界\" :children (list (place \"现场\" :children (list (node \"组\" :display 'inline :clocks (list (inline-test-clock 'render-data)) :children (list (node \"推进\" :progress-target (list 'clock \"共享工作\") :resolve (instant (outcome (lambda () (inline-test-clock 'advance! 1))))))))))))");
        s.Refresh();
        var group = s.CurrentRootNode!.Children.Single().Children.Single();
        var action = group.Children.Single();
        s.ExecuteAction(action, new List<SlottedResource?>());
        Check(s.CurrentRootNode.Children.Single().Children.Single().Clocks.Single().Current == 1,
            "展开组中的行动走正常执行入口，更新父级共享Clock");
        Console.WriteLine("[inline] All checks passed.");
    }
}
