using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;
using SSNoir.Theatre;

namespace SSNoir.Testing;

// Stable parser/clock contracts, plus an actual execution of the sample's Scheme entry.
public static class TheatreChecks
{
    public static void Run()
    {
        var state = new GameState();
        var interpreter = new SchemeInterpreter(state, new LocalScriptLoader());
        TheatreScene? captured = null;
        state.DialogueCenter.OnTheatreRequested += scene => captured = scene;
        interpreter.LoadFile("scripts/theatre/路灯下.scm");
        interpreter.Eval("(路灯下-试演!)");
        var sample = captured ?? throw new Exception("sample did not request theatre");
        Check(sample.Nodes.Count > 20 && sample.Beats.Count > 25, "sample must actually generate scene and beats");
        foreach (var node in sample.Nodes.Where(n => n.Shape == TheatreShape.Image))
            Check(node.Light == "路灯", "sample actors must bind the lamp");
        state.CurrentActionReport = new ActionReport();
        captured = null;
        interpreter.Eval("(路灯下-试演!)");
        Check(captured == null && state.CurrentActionReport.BlockingStorySteps.Count == 1
            && state.CurrentActionReport.BlockingStorySteps[0].Kind == BlockingStoryStepKind.Theatre,
            "action-owned theatre must queue, not broadcast");
        state.CurrentActionReport = null;
        var session = new TheatreSession(sample); int sounds = 0;
        session.SoundRequested += _ => sounds++;
        session.Start();
        int iterations = 0;
        while (!session.IsComplete && iterations++ < 500)
        {
            session.Tick(0.7f);
            if (session.Line != null) { session.Advance(); session.Advance(); }
        }
        Check(session.IsComplete && sounds > 2, "sample must run through all beats and sound events");

        System.IO.Directory.CreateDirectory(".cache/theatre-check");
        System.IO.File.WriteAllText(".cache/theatre-check/scene.json", Newtonsoft.Json.JsonConvert.SerializeObject(sample));
        const string sceneExpression = "(theatre-scene 1600 900 \"#08090F\" (list (theatre-group \"g\" \"\" 0 0)))";
        TheatreScene Build(string beats) => TheatreParser.Parse(interpreter.Eval(sceneExpression), interpreter.Eval(beats));
        var scene = Build("(list (theatre-tween \"g\" 'x 10 1) (theatre-tween \"g\" 'x 20 1))");
        var coarse = new TheatreSession(scene); coarse.Start(); coarse.Tick(1.5f);
        Near(coarse.Objects["g"][TheatreProperty.X], 15f, "overshoot must carry across beats");
        var fine = new TheatreSession(scene); fine.Start();
        for (int i = 0; i < 150; i++) fine.Tick(0.01f);
        Near(fine.Objects["g"][TheatreProperty.X], 15f, "frame rate must not change animation");
        coarse.Pause(true); coarse.Tick(20); Near(coarse.Objects["g"][TheatreProperty.X], 15f, "pause freezes time");
        coarse.Pause(false); coarse.Tick(.5f); Check(coarse.IsComplete, "resume must finish");
        var dialogue = new TheatreSession(Build("(list (theatre-say \"尼尔\" \"四个字啊\") (theatre-tween \"g\" 'x 5 1))"));
        dialogue.Start(); dialogue.Advance(); Check(dialogue.Line != null && dialogue.VisibleCharacters == 4, "first click reveals text");
        dialogue.Advance(); Check(dialogue.Line == null, "second click advances dialogue");
        dialogue.Advance(); dialogue.Tick(.5f); Near(dialogue.Objects["g"][TheatreProperty.X], 2.5f, "click cannot skip timed beat");
        var timed = new TheatreSession(Build("(list (theatre-parallel (theatre-caption-for \"尼尔\" \"四个字啊\" 1 \"#F0CF8A\") (theatre-tween \"g\" 'x 10 .5)) (theatre-wait 1) (theatre-clear-caption) (theatre-wait 1))"));
        timed.Start(); timed.Tick(.25f); timed.Advance();
        Near(timed.Objects["g"][TheatreProperty.X], 5, "timed subtitle animates concurrently; clicks do not skip");
        timed.Pause(true); timed.Tick(5); Near(timed.CaptionTime, .25f, "caption fades pause with stage");
        timed.Pause(false); timed.Tick(1); Check(timed.Line == null && timed.Caption != null, "subtitle persists between lines");
        timed.Tick(1); Check(timed.Caption == null && !timed.IsComplete, "explicit clear removes caption");
        var zero = new TheatreSession(Build("(list (theatre-sound \"s\" \"a\" #f 1 0) (theatre-wait 1))"));
        int events = 0; zero.SoundRequested += _ => events++; zero.Start(); zero.Tick(0); zero.Tick(.5f);
        Check(events == 1 && zero.BeatIndex == 1, "zero-duration event fires once");
        var delayed = new TheatreSession(Build("(list (theatre-parallel (theatre-sound-after \"s\" \"a\" #f 1 0 .3) (theatre-wait 1)))"));
        int delayedEvents = 0; delayed.SoundRequested += _ => delayedEvents++; delayed.Start(); delayed.Tick(.2f);
        Check(delayedEvents == 0, "delayed sound does not fire at start"); delayed.Pause(true); delayed.Tick(1);
        Check(delayedEvents == 0, "paused delayed sound stays pending"); delayed.Pause(false); delayed.Tick(.5f); delayed.Tick(.3f);
        Check(delayedEvents == 1 && delayed.IsComplete, "delayed sound fires once across a coarse frame");
        Reject(() => Build("(list (theatre-parallel (theatre-tween \"g\" 'x 1 1) (theatre-tween \"g\" 'x 2 1)))"), "parallel conflict");
        Reject(() => Build("(list (theatre-image-to \"g\" \"Portraits/Neon/尼尔\"))"), "image replacement on a group");
        Reject(() => Build("(list (theatre-tween \"missing\" 'x 1 1))"), "unknown object");
        Reject(() => Build("(list (theatre-animate \"g\" 'brightness '((0 1) (0 0))))"), "nonincreasing key times");
        Reject(() => Build("(list (theatre-tween \"g\" 'opacity 2 1))"), "opacity range");
        Reject(() => Build("(list (theatre-stop-sound \"missing\"))"), "missing audio loop");
        Reject(() => Build("(list (theatre-parallel (theatre-say \"尼尔\" \"一句话\") (theatre-say \"夜莺\" \"另一句\")))"), "parallel captions");
        Reject(() => TheatreParser.Parse(interpreter.Eval("(theatre-scene 1600 900 \"#000000\" (list (theatre-group \"g\" \"\" 0 0) (theatre-group \"g\" \"\" 0 0)))"), interpreter.Eval("(list (theatre-wait 1))")), "duplicate object");
        Reject(() => Build("(list (theatre-sound \"s\" \"../outside\" #f 1 0))"), "asset traversal");
        Reject(() => TheatreParser.Parse(interpreter.Eval("(theatre-scene 1600 900 \"#000000\" (list (theatre-group \"child\" \"missing\" 0 0)))"), interpreter.Eval("(list (theatre-wait 1))")), "missing parent");
        Reject(() => TheatreParser.Parse(interpreter.Eval("(theatre-scene 1600 900 \"#000000\" (list (theatre-polygon \"p\" \"\" \"#FFFFFF\" '((0 0) (2 0) (1 0.5) (2 2) (0 2)))))"), interpreter.Eval("(list (theatre-wait 1))")), "concave polygon");
        var loop = new TheatreSession(Build("(list (theatre-sound \"rain\" \"a\" #t 1 0) (theatre-wait 1) (theatre-stop-sound \"rain\"))"));
        var loopEvents = new List<TheatreCommandKind>(); loop.SoundRequested += c => loopEvents.Add(c.Kind);
        loop.Start(); loop.Tick(1);
        Check(loop.IsComplete && loopEvents.SequenceEqual(new[] { TheatreCommandKind.Sound, TheatreCommandKind.StopSound }), "loop starts and stops exactly once");
        var swapScene = TheatreParser.Parse(interpreter.Eval("(theatre-scene 1600 900 \"#000000\" (list (theatre-image \"a\" \"\" \"first\" 10 20 100 200 \"\")))"), interpreter.Eval("(list (theatre-image-to \"a\" \"second\") (theatre-wait 1))"));
        var swap = new TheatreSession(swapScene); swap.Start(); swap.Tick(0);
        Check(swap.Objects["a"].Asset == "second" && swap.Objects["a"][TheatreProperty.X] == 10, "image replacement retains transform");
        foreach (var beat in sample.Beats)
            foreach (var command in beat.Commands)
                if (command.Kind == TheatreCommandKind.Say)
                    Check(EstimateCaptionRows(sample.Width, command.Text) <= 2, "caption exceeds two rows: " + command.Target);
        Console.WriteLine($"[theatre] Scheme sample: {sample.Nodes.Count} objects, {sample.Beats.Count} beats, {sounds} sound events; parser/clock contracts passed.");
    }
    // Mirrors the player's greedy wrap at base size: CJK advances a full 36.8-unit glyph,
    // narrow characters half of that, across 86% of the scene width. Authors split longer lines.
    private static int EstimateCaptionRows(float sceneWidth, string text)
    {
        float width = sceneWidth * 0.86f, used = 0f;
        int rows = 1;
        foreach (char c in text)
        {
            float glyph = c < 128 ? 18.4f : 36.8f;
            if (used + glyph > width && used > 0f) { rows++; used = 0f; }
            used += glyph;
        }
        return rows;
    }
    private static void Near(float actual, float expected, string label) => Check(Math.Abs(actual - expected) < 0.002f, label + $" ({actual} != {expected})");
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("theatre check: " + message); }
    private static void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { return; } throw new Exception("theatre accepted invalid " + message); }
}
