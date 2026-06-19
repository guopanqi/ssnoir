// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Schemy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class Builtins
    {
        public static IDictionary<Symbol, object> CreateBuiltins(Interpreter interpreter)
        {
            var b = new Dictionary<Symbol, object>();

            // ── Arithmetic ────────────────────────────────────────────────────────────
            b[Symbol.FromString("+")] = new NativeProcedure(Utils.MakeVariadic(Add), "+");
            b[Symbol.FromString("-")] = new NativeProcedure(Utils.MakeVariadic(Minus), "-");
            b[Symbol.FromString("*")] = new NativeProcedure(Utils.MakeVariadic(Multiply), "*");
            b[Symbol.FromString("/")] = new NativeProcedure(Utils.MakeVariadic(Divide), "/");
            b[Symbol.FromString("modulo")]    = NativeProcedure.Create<int, int, int>((a, n) => ((a % n) + n) % n, "modulo");
            b[Symbol.FromString("remainder")] = NativeProcedure.Create<int, int, int>((a, n) => a % n, "remainder");
            b[Symbol.FromString("quotient")]  = NativeProcedure.Create<int, int, int>((a, n) => a / n, "quotient");
            b[Symbol.FromString("abs")] = NativeProcedure.Create<object, object>(
                x => x is int ? (object)Math.Abs((int)x) : Math.Abs(Convert.ToDouble(x)), "abs");
            b[Symbol.FromString("min")] = new NativeProcedure(args => {
                if (args.Count == 0) throw new SyntaxError("min: requires at least 1 argument");
                return args.Aggregate((a, x) => Convert.ToDouble(a) <= Convert.ToDouble(x) ? a : x);
            }, "min");
            b[Symbol.FromString("max")] = new NativeProcedure(args => {
                if (args.Count == 0) throw new SyntaxError("max: requires at least 1 argument");
                return args.Aggregate((a, x) => Convert.ToDouble(a) >= Convert.ToDouble(x) ? a : x);
            }, "max");

            // ── Comparison ────────────────────────────────────────────────────────────
            b[Symbol.FromString("=")]  = NativeProcedure.Create<double, double, bool>((x, y) => x == y, "=");
            b[Symbol.FromString("<")]  = NativeProcedure.Create<double, double, bool>((x, y) => x < y, "<");
            b[Symbol.FromString("<=")]  = NativeProcedure.Create<double, double, bool>((x, y) => x <= y, "<=");
            b[Symbol.FromString(">")]  = NativeProcedure.Create<double, double, bool>((x, y) => x > y, ">");
            b[Symbol.FromString(">=")]  = NativeProcedure.Create<double, double, bool>((x, y) => x >= y, ">=");

            // ── Equality ──────────────────────────────────────────────────────────────
            b[Symbol.FromString("eq?")]    = NativeProcedure.Create<object, object, bool>(
                (x, y) => object.ReferenceEquals(x, y), "eq?");
            b[Symbol.FromString("eqv?")]   = NativeProcedure.Create<object, object, bool>(
                (x, y) => object.Equals(x, y), "eqv?");
            b[Symbol.FromString("equal?")] = NativeProcedure.Create<object, object, bool>(EqualImpl, "equal?");

            // ── Type predicates ───────────────────────────────────────────────────────
            b[Symbol.FromString("boolean?")]  = NativeProcedure.Create<object, bool>(x => x is bool, "boolean?");
            b[Symbol.FromString("number?")]   = NativeProcedure.Create<object, bool>(x => x is int || x is double, "number?");
            b[Symbol.FromString("num?")]      = b[Symbol.FromString("number?")]; // backward-compat alias
            b[Symbol.FromString("string?")]   = NativeProcedure.Create<object, bool>(x => x is string, "string?");
            b[Symbol.FromString("symbol?")]   = NativeProcedure.Create<object, bool>(x => x is Symbol, "symbol?");
            b[Symbol.FromString("list?")]     = NativeProcedure.Create<object, bool>(x => x is List<object>, "list?");
            b[Symbol.FromString("pair?")]     = NativeProcedure.Create<object, bool>(
                x => x is List<object> && ((List<object>)x).Count > 0, "pair?");
            b[Symbol.FromString("procedure?")] = NativeProcedure.Create<object, bool>(x => x is ICallable, "procedure?");
            b[Symbol.FromString("null?")]     = NativeProcedure.Create<object, bool>(
                x => x is List<object> && ((List<object>)x).Count == 0, "null?");

            // ── Boolean ───────────────────────────────────────────────────────────────
            // Only #f is false in Scheme — (not 5) => #f, (not #f) => #t
            b[Symbol.FromString("not")] = NativeProcedure.Create<object, bool>(x => !Utils.IsTruthy(x), "not");

            // ── List primitives ───────────────────────────────────────────────────────
            b[Symbol.FromString("list")]     = new NativeProcedure(args => args, "list");
            b[Symbol.FromString("cons")]     = NativeProcedure.Create<object, List<object>, List<object>>(
                (x, ys) => Enumerable.Concat(new[] { x }, ys).ToList(), "cons");
            b[Symbol.FromString("car")]      = NativeProcedure.Create<List<object>, object>(ls => ls[0], "car");
            b[Symbol.FromString("cdr")]      = NativeProcedure.Create<List<object>, List<object>>(
                ls => ls.Skip(1).ToList(), "cdr");
            b[Symbol.FromString("length")]   = NativeProcedure.Create<List<object>, int>(ls => ls.Count, "length");
            b[Symbol.FromString("list-ref")] = NativeProcedure.Create<List<object>, int, object>((ls, i) => ls[i], "list-ref");
            b[Symbol.FromString("reverse")]  = NativeProcedure.Create<List<object>, List<object>>(
                ls => ls.AsEnumerable().Reverse().ToList(), "reverse");

            // append is variadic: (append '(1 2) '(3 4) '(5 6)) => (1 2 3 4 5 6)
            b[Symbol.APPEND] = new NativeProcedure(args => {
                var result = new List<object>();
                foreach (var arg in args)
                    result.AddRange(Utils.ConvertType<List<object>>(arg));
                return result;
            }, "append");

            // apply supports (apply f a1 a2 ... list)
            b[Symbol.FromString("apply")] = new NativeProcedure(args => {
                if (args.Count < 2) throw new SyntaxError("apply: requires at least 2 arguments");
                var proc = Utils.ConvertType<ICallable>(args[0]);
                var last = Utils.ConvertType<List<object>>(args[args.Count - 1]);
                var callArgs = args.Skip(1).Take(args.Count - 2).Concat(last).ToList();
                return proc.Call(callArgs);
            }, "apply");

            // map supports multiple lists: (map f '(1 2) '(10 20)) => (11 22)
            b[Symbol.FromString("map")] = new NativeProcedure(args => {
                if (args.Count < 2) throw new SyntaxError("map: requires at least 2 arguments");
                var proc = Utils.ConvertType<ICallable>(args[0]);
                var lists = args.Skip(1).Select(a => Utils.ConvertType<List<object>>(a)).ToList();
                int len = lists[0].Count;
                if (lists.Any(l => l.Count != len))
                    throw new SyntaxError("map: all lists must have the same length");
                var result = new List<object>(len);
                for (int i = 0; i < len; i++)
                    result.Add(proc.Call(lists.Select(l => l[i]).ToList()));
                return result;
            }, "map");

            // ── Other builtins ────────────────────────────────────────────────────────
            b[Symbol.FromString("range")]  = new NativeProcedure(RangeImpl, "range");
            b[Symbol.FromString("assert")] = new NativeProcedure(AssertImpl, "assert");
            b[Symbol.FromString("load")]   = NativeProcedure.Create<string, None>(
                filename => LoadImpl(interpreter, filename), "load");
            b[Symbol.FromString("null")]   = NativeProcedure.Create<object>(() => (object)null, "null");

            // error: signal an error from Scheme code
            b[Symbol.FromString("error")] = new NativeProcedure(args => {
                if (args.Count == 0) throw new Exception("error");
                var msg = args[0] is string ? (string)args[0] : Utils.PrintExpr(args[0]);
                if (args.Count > 1)
                    msg += " " + string.Join(" ", args.Skip(1).Select(a => Utils.PrintExpr(a)));
                throw new Exception(msg);
            }, "error");

            // ── Output ────────────────────────────────────────────────────────────────
            // These write to interpreter.Output (defaults to Console.Out).
            // In Unity, set interpreter.Output = your own TextWriter.
            b[Symbol.FromString("display")] = new NativeProcedure(args => {
                Utils.CheckArity(args, 1, 2); // optional port arg accepted but ignored
                var text = args[0] is string ? (string)args[0] : Utils.PrintExpr(args[0]);
                interpreter.Output.Write(text);
                return None.Instance;
            }, "display");
            b[Symbol.FromString("write")] = new NativeProcedure(args => {
                Utils.CheckArity(args, 1, 2);
                interpreter.Output.Write(Utils.PrintExpr(args[0]));
                return None.Instance;
            }, "write");
            b[Symbol.FromString("newline")] = new NativeProcedure(args => {
                interpreter.Output.WriteLine();
                return None.Instance;
            }, "newline");

            return b;
        }

        #region Implementations

        private static List<object> RangeImpl(List<object> args)
        {
            Utils.CheckSyntax(args, args.Count >= 1 && args.Count <= 3);
            foreach (var item in args) Utils.CheckSyntax(args, item is int, "range: items must be integers");

            int start, end, step;
            if (args.Count == 1) { start = 0; end = (int)args[0]; step = 1; }
            else if (args.Count == 2) { start = (int)args[0]; end = (int)args[1]; step = 1; }
            else { start = (int)args[0]; end = (int)args[1]; step = (int)args[2]; }

            if (start < end) Utils.CheckSyntax(args, step > 0, "range: step must move toward end");
            if (start > end) Utils.CheckSyntax(args, step < 0, "range: step must move toward end");

            var res = new List<object>();
            if (start <= end) for (int i = start; i < end; i += step) res.Add(i);
            else for (int i = start; i > end; i += step) res.Add(i);
            return res;
        }

        private static None AssertImpl(List<object> args)
        {
            Utils.CheckArity(args, 1, 2);
            string msg = "Assertion failed";
            msg += args.Count > 1 ? ": " + Utils.ConvertType<string>(args[1]) : string.Empty;
            if (!Utils.IsTruthy(args[0])) throw new AssertionFailedError(msg);
            return None.Instance;
        }

        private static None LoadImpl(Interpreter interpreter, string filename)
        {
            using (var reader = new StreamReader(interpreter.FileSystemAccessor.OpenRead(filename)))
            {
                var result = interpreter.Evaluate(reader);
                if (result.Error != null)
                    throw new Exception(
                        string.Format("load \"{0}\": {1}", filename, result.Error.Message),
                        result.Error);
            }
            return None.Instance;
        }

        public static bool EqualImpl(object x, object y)
        {
            if (object.Equals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x is IList<object> && y is IList<object>)
            {
                var x2 = (IList<object>)x;
                var y2 = (IList<object>)y;
                if (x2.Count != y2.Count) return false;
                return Enumerable.Zip(x2, y2, Tuple.Create).All(p => EqualImpl(p.Item1, p.Item2));
            }
            return false;
        }

        private static object Add(object x, object y)
        {
            if (x is int && y is int) return (int)x + (int)y;
            return Convert.ToDouble(x) + Convert.ToDouble(y);
        }

        private static object Minus(object x, object y)
        {
            if (x is int && y is int) return (int)x - (int)y;
            return Convert.ToDouble(x) - Convert.ToDouble(y);
        }

        private static object Multiply(object x, object y)
        {
            if (x is int && y is int) return (int)x * (int)y;
            return Convert.ToDouble(x) * Convert.ToDouble(y);
        }

        private static object Divide(object x, object y)
        {
            if (x is int && y is int) return (int)x / (int)y;
            return Convert.ToDouble(x) / Convert.ToDouble(y);
        }

        #endregion
    }
}
