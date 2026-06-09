using System;
using System.IO;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public class SchemeInterpreter
    {
        private readonly Interpreter _interpreter;

        public SchemeInterpreter(GameState gameState)
        {
            _interpreter = new Interpreter(
                new Interpreter.CreateSymbolTableDelegate[] { Builtins.CreateBuiltins },
                new ReadOnlyFileSystemAccessor()
            );

            // Register our bridge functions
            NativeFunctions.Register(_interpreter, gameState);

            // Load standard library
            LoadFile("scripts/stdlib.scm");
        }

        public void LoadFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Scheme script not found: {fullPath}");
            }

            using (var reader = File.OpenText(fullPath))
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
