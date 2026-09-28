#nullable enable
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SSNoir.Core;

namespace SSNoir.StagePreview;

public static class StageExporter
{
    public static void Run(string expression, string output)
    {
        if (!expression.TrimStart().StartsWith('('))
            throw new ArgumentException("演出入口必须是 Scheme 表达式，例如 (baines 'debug-play-street!)");

        var state = new GameState();
        var scenes = new SceneManager(state, new LocalScriptLoader());
        scenes.LoadScene("world");
        StoryStageSequence? captured = null;
        state.DialogueCenter.OnStageRequested += sequence =>
        {
            if (captured != null) throw new InvalidOperationException("演出入口触发了多场舞台演出，请指定单场入口");
            captured = sequence;
        };
        scenes.ActiveInterpreter.Eval(expression);
        if (captured == null) throw new InvalidOperationException("演出入口没有触发 play-stage!");

        var path = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            expression,
            beats = captured.Beats
        }, Formatting.Indented, new StringEnumConverter()));
        Console.WriteLine(path);
    }
}
