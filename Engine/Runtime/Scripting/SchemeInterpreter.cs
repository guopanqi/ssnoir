#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public class DummyFileSystemAccessor : IFileSystemAccessor
    {
        public Stream OpenRead(string path)
        {
            return new MemoryStream();
        }

        public Stream OpenWrite(string path)
        {
            return new MemoryStream();
        }
    }

    public class SchemeInterpreter
    {
        private readonly Interpreter _interpreter;
        private readonly IScriptLoader _loader;

        public SchemeInterpreter(GameState gameState, IScriptLoader loader)
        {
            _loader = loader;
            _interpreter = CreateInterpreter();

            // Register our bridge functions
            NativeFunctions.Register(_interpreter, gameState);

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

        private static Interpreter CreateInterpreter()
        {
#if UNITY_5_3_OR_NEWER
            return CreateUnitySafeInterpreter();
#else
            return new Interpreter(
                new Interpreter.CreateSymbolTableDelegate[] { Builtins.CreateBuiltins },
                new DummyFileSystemAccessor()
            );
#endif
        }

#if UNITY_5_3_OR_NEWER
        private static Interpreter CreateUnitySafeInterpreter()
        {
            // Schemy 1.0.0 assumes Assembly.GetEntryAssembly() is non-null while
            // loading its optional ".init.ss". Unity returns null there, so we
            // construct the same core state and load only Schemy's embedded init.ss.
            //
            // This keeps the engine package usable inside Unity without modifying
            // the upstream schemy.dll. If more Unity-specific Schemy issues show up,
            // TODO: fork Schemy and patch the internals directly; that will be more
            // stable than maintaining reflection-based initialization here.
            var interpreter = (Interpreter)FormatterServices.GetUninitializedObject(typeof(Interpreter));
            var environment = Schemy.Environment.CreateEmpty();
            var macroTable = new Dictionary<Symbol, Procedure>();

            var initializers = new Interpreter.CreateSymbolTableDelegate[]
            {
                Builtins.CreateBuiltins
            };

            foreach (var initializer in initializers)
            {
                environment = new Schemy.Environment(initializer(interpreter), environment);
            }

            SetPrivateField(interpreter, "fsAccessor", new DummyFileSystemAccessor());
            SetPrivateField(interpreter, "environment", environment);
            SetPrivateField(interpreter, "macroTable", macroTable);

            using (var stream = typeof(Interpreter).Assembly.GetManifestResourceStream("init.ss"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Schemy embedded resource 'init.ss' was not found.");
                }

                using (var reader = new StreamReader(stream))
                {
                    var result = interpreter.Evaluate(reader);
                    if (result.Error != null)
                    {
                        throw new Exception($"Error loading Schemy init.ss: {result.Error}", result.Error);
                    }
                }
            }

            return interpreter;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
#endif
    }
}
