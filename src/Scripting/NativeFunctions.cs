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

            interpreter.DefineGlobal(Symbol.FromString("node"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("node requires at least 1 argument: name string");

                var name = args[0] as string;
                if (name == null)
                    throw new ArgumentException($"node name must be a string, got {args[0]?.GetType().FullName}");

                var children = new List<GameNode>();
                Action effect = null;

                for (var i = 1; i < args.Count; i += 2)
                {
                    if (i + 1 >= args.Count)
                        throw new ArgumentException($"node '{name}' has a keyword without a value");

                    var key = args[i] as Symbol;
                    if (key == null)
                        throw new ArgumentException($"node '{name}' argument {i} must be a keyword symbol");

                    var value = args[i + 1];
                    switch (key.AsString)
                    {
                        case ":children":
                            children = NodeConverter.ConvertList(value, interpreter);
                            break;
                        case ":effect":
                            if (value is bool b && b == false)
                            {
                                break;
                            }

                            if (value is Procedure proc)
                            {
                                effect = () => proc.Call(new List<object>());
                                break;
                            }

                            throw new ArgumentException($"node '{name}' :effect must be a procedure or #f");
                        default:
                            throw new ArgumentException($"node '{name}' has unknown keyword '{key.AsString}'");
                    }
                }

                return new GameNode
                {
                    Name = name,
                    Children = children,
                    Effect = effect
                };
            }, "node"));
        }
    }
}
