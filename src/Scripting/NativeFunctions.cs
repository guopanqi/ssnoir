using System;
using System.Collections.Generic;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public static class NativeFunctions
    {
        public static void Register(Interpreter interpreter, GameState gameState)
        {
            interpreter.DefineGlobal(Symbol.FromString("get-global"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("get-global requires 1 argument: key symbol");

                var sym = args[0] as Symbol;
                if (sym == null)
                    throw new ArgumentException($"get-global argument must be a symbol, got {args[0]?.GetType().FullName}");

                return gameState.Get<object>(sym.AsString);
            }, "get-global"));

            interpreter.DefineGlobal(Symbol.FromString("set-global!"), new NativeProcedure(args =>
            {
                if (args.Count < 2)
                    throw new ArgumentException("set-global! requires 2 arguments: key symbol and value");

                var sym = args[0] as Symbol;
                if (sym == null)
                    throw new ArgumentException($"set-global! first argument must be a symbol, got {args[0]?.GetType().FullName}");

                var val = args[1];
                gameState.Set(sym.AsString, val);
                 return new None();
            }, "set-global!"));
        }
    }
}
