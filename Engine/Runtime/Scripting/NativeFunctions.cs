#nullable enable
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
                    throw new ArgumentException("get-global requires 1 argument: key symbol or string");

                string key = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("get-global key cannot be null or empty");

                return gameState.Get<object>(key) ?? false;
            }, "get-global"));

            interpreter.DefineGlobal(Symbol.FromString("set-global!"), new NativeProcedure(args =>
            {
                if (args.Count < 2)
                    throw new ArgumentException("set-global! requires 2 arguments: key symbol or string and value");

                string key = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("set-global! key cannot be null or empty");

                var val = args[1];
                gameState.Set(key, val);
                 return new None();
            }, "set-global!"));

            interpreter.DefineGlobal(Symbol.FromString("string-append"), new NativeProcedure(args =>
            {
                return string.Concat(args);
            }, "string-append"));

            interpreter.DefineGlobal(Symbol.FromString("number->string"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("number->string requires 1 argument");
                return args[0]?.ToString() ?? "";
            }, "number->string"));

            var rand = new Random();
            interpreter.DefineGlobal(Symbol.FromString("random-choice"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("random-choice requires 1 argument: a list of options");

                if (args[0] is List<object> list)
                {
                    if (list.Count == 0)
                        return null;
                    return list[rand.Next(list.Count)];
                }

                throw new ArgumentException("random-choice argument must be a list");
            }, "random-choice"));
        }
    }
}
