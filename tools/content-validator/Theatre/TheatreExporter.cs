using System;
using System.IO;
using Newtonsoft.Json;
using SSNoir.Core;
using SSNoir.Scripting;
using SSNoir.Theatre;

namespace SSNoir.Testing;

public static class TheatreExporter
{
    public static void Run(string expression, string output)
    {
        var state = new GameState();
        var interpreter = new SchemeInterpreter(state, new LocalScriptLoader());
        TheatreScene? scene = null;
        state.DialogueCenter.OnTheatreRequested += value =>
        {
            if (scene != null) throw new InvalidOperationException("预览表达式只能发起一场线绘演出");
            scene = value;
        };
        interpreter.Eval(expression);
        if (scene == null) throw new InvalidOperationException("表达式没有发起线绘演出");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonConvert.SerializeObject(scene, new Newtonsoft.Json.Converters.StringEnumConverter()));
        Console.WriteLine($"[theatre-export] {scene.Nodes.Count} objects -> {output}");
    }
}
