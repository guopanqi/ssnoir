using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;
using SSNoir.Theatre;

namespace SSNoir.Testing;

public static class TheatreChecks
{
    public static void Run()
    {
        var state = new GameState(); var interpreter = new SchemeInterpreter(state, new LocalScriptLoader());
        TheatreScene? captured = null; state.DialogueCenter.OnTheatreRequested += scene => captured = scene;
        interpreter.LoadFile("scripts/theatre/路灯下.scm"); interpreter.Eval("(路灯下-试演!)");
        var sample = captured ?? throw new Exception("sample did not request theatre");
        Check(sample.Nodes.Count > 20 && sample.Commands.Count() > 50, "sample must generate geometry and a real nested program");
        state.CurrentActionReport = new ActionReport(); captured = null; interpreter.Eval("(路灯下-试演!)");
        Check(captured == null && state.CurrentActionReport.BlockingStorySteps.Count == 1 && state.CurrentActionReport.BlockingStorySteps[0].Kind == BlockingStoryStepKind.Theatre, "action theatre queues instead of broadcasting");
        state.CurrentActionReport = null;
        var session = new TheatreSession(sample); int sounds = 0; session.SoundRequested += _ => sounds++; session.Start();
        for (int i = 0; i < 1000 && !session.IsComplete; i++) { session.Tick(.3f); if (session.Line != null) { session.Advance(); session.Advance(); } }
        Check(session.IsComplete && sounds > 2, "sample executes to completion");
        System.IO.Directory.CreateDirectory(".cache/theatre-check"); System.IO.File.WriteAllText(".cache/theatre-check/scene.json", Newtonsoft.Json.JsonConvert.SerializeObject(sample));
        const string sceneExpression = "(theatre-scene 1600 900 \"#08090F\" (list (theatre-group \"g\" \"\" 0 0)))";
        TheatreScene Build(string program) => TheatreParser.Parse(interpreter.Eval(sceneExpression), interpreter.Eval(program));
        TheatreSession Start(string program, float enter = 0, float exit = 0) { var s = new TheatreSession(Build(program), enter, exit); s.Start(); return s; }
        var coarse = Start("(theatre-sequence (theatre-tween \"g\" 'x 10 1) (theatre-tween \"g\" 'x 20 1))"); coarse.Tick(1.5f);
        var fine = Start("(theatre-sequence (theatre-tween \"g\" 'x 10 1) (theatre-tween \"g\" 'x 20 1))"); for (int i=0;i<150;i++) fine.Tick(.01f);
        Near(coarse.Objects["g"][TheatreProperty.X], 15, "sequence carries frame overshoot"); Near(fine.Objects["g"][TheatreProperty.X], 15, "small frames produce same state");
        coarse.Pause(true); coarse.Tick(50); Near(coarse.Time, 1.5f, "pause freezes unified clock"); coarse.Pause(false); coarse.Tick(.5f); Check(coarse.IsComplete, "resumes to completion");
        var nested = Start("(theatre-parallel (theatre-sequence (theatre-wait .3) (theatre-tween \"g\" 'x 10 .7)) (theatre-tween \"g\" 'y 20 2))");
        nested.Tick(1); Near(nested.Objects["g"][TheatreProperty.X], 10, "nested delayed action completed"); Near(nested.Objects["g"][TheatreProperty.Y],10,"parallel still advances"); nested.Tick(1); Check(nested.IsComplete,"parallel waits for every branch");
        var during = Start("(theatre-sequence (theatre-during (theatre-say \"尼尔\" \"四个字啊\") (theatre-loop (theatre-animate \"g\" 'brightness '((0 0) (.5 1) (1 0))))) (theatre-tween \"g\" 'x 10 1))");
        during.Tick(2.25f); Near(during.Objects["g"][TheatreProperty.Brightness], .5f, "background loop advances during dialogue wait");
        during.Advance(); Check(during.Line == null, "completed text click ends dialogue scope"); during.Tick(.5f);
        Near(during.Objects["g"][TheatreProperty.Brightness], .5f,"scope cancellation stops background animation"); Near(during.Objects["g"][TheatreProperty.X],5,"foreground proceeds after scope");
        var dialogue = Start("(theatre-sequence (theatre-say \"尼尔\" \"四个字啊\") (theatre-wait 1))"); dialogue.Advance(); Check(dialogue.VisibleCharacters == 4,"first click reveals text"); dialogue.Advance(); Check(dialogue.Line == null,"second click advances");
        var timed = Start("(theatre-sequence (theatre-parallel (theatre-caption-for \"尼尔\" \"四个字啊\" 1 \"#F0CF8A\") (theatre-tween \"g\" 'x 10 .5)) (theatre-wait 1) (theatre-clear-caption) (theatre-wait 1))");
        timed.Tick(.25f); timed.Advance(); Near(timed.Objects["g"][TheatreProperty.X],5,"timed subtitle permits concurrent animation"); timed.Pause(true); timed.Tick(5); Near(timed.CaptionTime,.25f,"subtitle fades share pause clock"); timed.Pause(false); timed.Tick(1); Check(timed.Caption!=null,"subtitle persists between timed actions"); timed.Tick(1); Check(timed.Caption==null,"clear subtitle is explicit");
        var audio = StartAudio(Build("(theatre-during (theatre-caption-for \"尼尔\" \"一句话\" 1 \"#FFFFFF\") (theatre-sequence (theatre-sound \"电流\" \"a\" #t .5 0) (theatre-volume \"电流\" .1 .5) (theatre-loop (theatre-wait 1))))"));
        audio.Session.Tick(.25f); Near(audio.Session.Sounds["电流"].Volume,.3f,"volume animation executes in pure player"); audio.Session.Tick(2);
        Check(audio.Session.IsComplete && audio.Events.SequenceEqual(new[]{TheatreCommandKind.Sound,TheatreCommandKind.StopSound}),"scope completion releases audio exactly once");
        var cancelled = StartAudio(Build("(theatre-sequence (theatre-sound \"电流\" \"a\" #t 1 0) (theatre-say \"尼尔\" \"等着\"))")); cancelled.Session.Stop(); cancelled.Session.Stop(); cancelled.Session.Tick(10);
        Check(cancelled.Session.Phase==TheatrePhase.Cancelled && cancelled.Session.Caption==null && cancelled.Events.Count==2,"cancel is idempotent and cleans caption/audio");
        var oneShot = StartAudio(Build("(theatre-during (theatre-wait .5) (theatre-sequence (theatre-sound \"脚步\" \"a\" #f 1 0) (theatre-wait 10)))"));
        oneShot.Session.Tick(1); Check(oneShot.Events.Count==2 && oneShot.Session.Playbacks.Count==0, "scope cancels one-shot audio too");
        var finished = StartAudio(Build("(theatre-sequence (theatre-sound \"脚步\" \"a\" #f 1 0) (theatre-wait 1))"));
        finished.Session.SoundFinished(finished.Session.Playbacks.Keys.Single()); finished.Session.Tick(1);
        Check(finished.Events.Count == 1 && finished.Session.Playbacks.Count == 0, "natural audio completion is not stopped a second time");
        var smooth = Start("(theatre-tween \"g\" 'x 10 1 'smooth)"); smooth.Tick(.25f);
        Near(smooth.Objects["g"][TheatreProperty.X], 1.5625f, "smooth tween eases motion at the beginning");
        var fade = Start("(theatre-wait 1)",.5f,.5f); fade.Tick(.25f); Near(fade.Fade,.5f,"enter transition uses session clock"); fade.Tick(1.5f); Check(fade.Phase==TheatrePhase.Exiting,"coarse dt enters exit without losing time"); Near(fade.Fade,.5f,"exit consumes same dt"); fade.Pause(true); fade.Tick(5); Near(fade.Fade,.5f,"exit fade pauses too"); fade.Pause(false); fade.Tick(.25f); Check(fade.IsComplete,"exit finishes");
        var repeat = Start("(theatre-repeat 3 (theatre-sequence (theatre-tween \"g\" 'x 10 .5) (theatre-tween \"g\" 'x 0 .5)))"); repeat.Tick(2.75f); Near(repeat.Objects["g"][TheatreProperty.X],5,"repeat captures new start values per iteration"); repeat.Tick(.25f); Check(repeat.IsComplete,"finite repeat completes");
        Reject(()=>Build("(theatre-parallel (theatre-tween \"g\" 'x 1 1) (theatre-sequence (theatre-wait 1) (theatre-tween \"g\" 'x 2 1)))"),"nested concurrent property ownership");
        Reject(()=>Build("(theatre-parallel (theatre-say \"尼尔\" \"一句\") (theatre-say \"夜莺\" \"另一句\"))"),"concurrent captions");
        Reject(()=>Build("(theatre-parallel (theatre-sound \"s\" \"a\" #t 1 0) (theatre-volume \"s\" 0 1))"), "concurrent audio lifecycle and volume ownership");
        Reject(()=>Build("(theatre-loop (theatre-wait 1))"),"unbounded root loop");
        Reject(()=>Build("(theatre-during (theatre-say \"尼尔\" \"一句\") (theatre-loop (theatre-clear-caption)))"),"background caption mutation");
        Reject(()=>Build("(theatre-repeat 2 (theatre-clear-caption))"),"zero-time repeat");
        Reject(()=>Build("(theatre-tween \"missing\" 'x 1 1)"),"unknown object");
        Reject(()=>Build("(theatre-tween \"g\" 'opacity 2 1)"),"opacity bounds");
        Reject(()=>Build("(theatre-sound \"s\" \"../outside\" #f 1 0)"),"asset path");
        Console.WriteLine($"[theatre] {sample.Nodes.Count} objects; nested composition, scoped loops/audio, volume, input, pause/cancel and transitions passed.");
    }
    private static (TheatreSession Session,List<TheatreCommandKind> Events) StartAudio(TheatreScene scene)
    { var s=new TheatreSession(scene);var events=new List<TheatreCommandKind>();s.SoundRequested+=c=>events.Add(c.Command.Kind);s.Start();return(s,events); }
    private static void Near(float actual,float expected,string label)=>Check(Math.Abs(actual-expected)<.002f,label+$" ({actual} != {expected})");
    private static void Check(bool condition,string label){if(!condition)throw new Exception("theatre check: "+label);}
    private static void Reject(Action action,string label){try{action();}catch(ArgumentException){return;}throw new Exception("theatre accepted invalid "+label);}
}
