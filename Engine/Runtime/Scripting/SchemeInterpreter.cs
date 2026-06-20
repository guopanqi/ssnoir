#nullable enable
using System;
using System.IO;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    // Blocks (load "...") from Scheme — scripts must use (load-file "...") instead.
    public class DummyFileSystemAccessor : IFileSystemAccessor
    {
        public Stream OpenRead(string path)
        {
            throw new NotSupportedException($"Direct file access is disabled. Use (load-file \"{path}\") instead.");
        }

        public Stream OpenWrite(string path)
        {
            throw new NotSupportedException("File write access is disabled.");
        }
    }

    public class SchemeInterpreter
    {
        private readonly Interpreter _interpreter;
        private readonly IScriptLoader _loader;

        public SchemeInterpreter(GameState gameState, IScriptLoader loader)
        {
            _loader = loader;
            _interpreter = new Interpreter(fsAccessor: new DummyFileSystemAccessor());

            // Register our bridge functions
            NativeFunctions.Register(_interpreter, gameState);

            // Register load-file bridge
            _interpreter.DefineGlobal(
                Symbol.FromString("load-file"),
                new NativeProcedure(args =>
                {
                    if (args.Count < 1)
                        throw new ArgumentException("load-file requires 1 argument (relative path)");
                    string path = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                    string fullPath = path;
                    if (!path.StartsWith("scenes/") && !path.StartsWith("scripts/"))
                    {
                        fullPath = "scenes/" + path;
                    }
                    LoadFile(fullPath);
                    return new None();
                }, "load-file")
            );

            // Load standard library and engine definitions
            LoadFile("scripts/stdlib.scm");
            LoadFile("scripts/engine.scm");
        }

        public void LoadFile(string relativePath)
        {
            string scriptContent = _loader.LoadScriptText(relativePath);
            using (var reader = new StringReader(scriptContent))
            {
                var result = _interpreter.Evaluate(reader);
                if (result.Error != null)
                {
                    throw new Exception($"Error loading Scheme script '{relativePath}': {result.Error}", result.Error);
                }
            }
        }

        public object Eval(string code)
        {
            using (var reader = new StringReader(code))
            {
                var result = _interpreter.Evaluate(reader);
                if (result.Error != null)
                {
                    throw new Exception($"Error evaluating Scheme code: {result.Error}", result.Error);
                }
                return result.Result;
            }
        }

        public Interpreter RawInterpreter => _interpreter;
    }
}
