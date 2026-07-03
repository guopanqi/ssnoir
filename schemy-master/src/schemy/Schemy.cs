// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Schemy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Reflection;

    public class Interpreter
    {
        private readonly Environment environment;
        private readonly Dictionary<Symbol, Procedure> macroTable;
        private readonly IFileSystemAccessor fsAccessor;
        private TextWriter output;

        public delegate IDictionary<Symbol, object> CreateSymbolTableDelegate(Interpreter interpreter);

        /// <summary>
        /// Initializes a new instance of the <see cref="Interpreter"/> class.
        /// </summary>
        /// <param name="environmentInitializers">Additional environment initializers run after builtins.</param>
        /// <param name="fsAccessor">File system accessor used by <c>load</c>. Defaults to disabled.</param>
        /// <param name="output">Writer for <c>display</c>/<c>write</c>/<c>newline</c>. Defaults to <see cref="Console.Out"/>.</param>
        public Interpreter(
            IEnumerable<CreateSymbolTableDelegate> environmentInitializers = null,
            IFileSystemAccessor fsAccessor = null,
            TextWriter output = null)
        {
            this.fsAccessor = fsAccessor ?? new DisabledFileSystemAccessor();
            this.output = output;
            this.environment = Environment.CreateEmpty();
            this.macroTable = new Dictionary<Symbol, Procedure>();

            environmentInitializers = environmentInitializers ?? new List<CreateSymbolTableDelegate>();
            environmentInitializers = new CreateSymbolTableDelegate[] { Builtins.CreateBuiltins }.Concat(environmentInitializers);

            foreach (var initializer in environmentInitializers)
                this.environment = new Environment(initializer(this), this.environment);

            foreach (var reader in GetInitializeReaders())
            {
                var result = this.Evaluate(reader);
                if (result.Error != null)
                    throw new InvalidOperationException(
                        "Interpreter initialization failed: " + result.Error.Message, result.Error);
            }
        }

        /// <summary>The output writer used by display/write/newline. Defaults to Console.Out.</summary>
        public TextWriter Output
        {
            get { return this.output ?? Console.Out; }
            set { this.output = value; }
        }

        public IFileSystemAccessor FileSystemAccessor => this.fsAccessor;
        public Environment Environment => this.environment;

        private IEnumerable<TextReader> GetInitializeReaders()
        {
            // yield return is illegal inside try/catch, so we collect into a list first.
            var readers = new List<TextReader>();

            // Load the embedded init.ss. Try both resource name conventions:
            // old-style csproj uses <LogicalName>init.ss</LogicalName> → "init.ss"
            // SDK-style default → "Schemy.init.ss"
            var asm = typeof(Interpreter).Assembly;
            foreach (var resourceName in new[] { "init.ss", "Schemy.init.ss" })
            {
                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        readers.Add(new StringReader(new StreamReader(stream).ReadToEnd()));
                        break;
                    }
                }
            }

            // Optional host-level .init.ss next to the dll.
            // Skipped gracefully when Assembly.Location is unavailable (Unity IL2CPP).
            try
            {
                var loc = asm.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    if (dir != null)
                    {
                        var hostInit = Path.Combine(dir, ".init.ss");
                        if (File.Exists(hostInit))
                            readers.Add(new StringReader(File.ReadAllText(hostInit)));
                    }
                }
            }
            catch { /* Unity IL2CPP may throw on Assembly.Location */ }

            return readers;
        }

        /// <summary>Evaluate all expressions in <paramref name="input"/> and return the last result.</summary>
        public EvaluationResult Evaluate(TextReader input)
        {
            var port = new InPort(input);
            object res = null;
            while (true)
            {
                try
                {
                    var expr = Expand(Read(port), environment, macroTable, true);
                    if (Symbol.EOF.Equals(expr))
                        return new EvaluationResult(null, res);
                    res = EvaluateExpression(expr, environment);
                }
                catch (Exception e)
                {
                    return new EvaluationResult(e, null);
                }
            }
        }

        /// <summary>Evaluate a single string expression.</summary>
        public EvaluationResult Evaluate(string expression)
        {
            return Evaluate(new StringReader(expression));
        }

        /// <summary>Read-Eval-Print loop.</summary>
        public void REPL(TextReader input, TextWriter output, string prompt = null, string[] headers = null)
        {
            var port = new InPort(input);

            if (headers != null)
                foreach (var line in headers) output.WriteLine(line);

            object res = null;
            while (true)
            {
                try
                {
                    if (!string.IsNullOrEmpty(prompt) && output != null) output.Write(prompt);
                    var expr = Expand(Read(port), environment, macroTable, true);
                    if (Symbol.EOF.Equals(expr)) return;
                    res = EvaluateExpression(expr, environment);
                    if (output != null) output.WriteLine(Utils.PrintExpr(res));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        /// <summary>Bind a symbol in the global environment.</summary>
        public void DefineGlobal(Symbol sym, object val)
        {
            this.environment[sym] = val;
        }

        public static object Read(InPort port)
        {
            Func<object, object> readAhead = null;
            readAhead = token =>
            {
                if (object.Equals(token, Symbol.EOF))
                    throw new SyntaxError("unexpected EOF");

                if (!(token is string))
                    throw new SyntaxError("unexpected token: " + token);

                string tok = (string)token;
                if (tok == "(")
                {
                    var L = new List<object>();
                    while (true)
                    {
                        token = port.NextToken();
                        if (token is string && (string)token == ")")
                            return L;
                        L.Add(readAhead(token));
                    }
                }
                else if (tok == ")")
                {
                    throw new SyntaxError("unexpected )");
                }
                else
                {
                    Symbol quote;
                    if (Symbol.QuotesMap.TryGetValue(tok, out quote))
                        return new List<object> { quote, Read(port) };
                    return ParseAtom(tok);
                }
            };

            var token1 = port.NextToken();
            return Symbol.EOF.Equals(token1) ? Symbol.EOF : readAhead(token1);
        }

        public static object Expand(object expression, Environment env, Dictionary<Symbol, Procedure> macroTable, bool isTopLevel = true)
        {
            Procedure procedure = null;
            Func<object, bool, object> expand = null;
            expand = (x, topLevel) =>
            {
                if (!(x is List<object>)) return x;

                var xs = (List<object>)x;
                Utils.CheckSyntax(xs, xs.Count > 0);

                if (Symbol.QUOTE.Equals(xs[0]))
                {
                    Utils.CheckSyntax(xs, xs.Count == 2);
                    return xs;
                }
                else if (Symbol.IF.Equals(xs[0]))
                {
                    if (xs.Count == 3) xs.Add(None.Instance);
                    Utils.CheckSyntax(xs, xs.Count == 4);
                    return xs.Select(e => expand(e, false)).ToList();
                }
                else if (Symbol.SET.Equals(xs[0]))
                {
                    Utils.CheckSyntax(xs, xs.Count == 3);
                    Utils.CheckSyntax(xs, xs[1] is Symbol, "can only set! a symbol");
                    return new List<object> { Symbol.SET, xs[1], expand(xs[2], false) };
                }
                else if (Symbol.DEFINE.Equals(xs[0]) || Symbol.DEFINE_MACRO.Equals(xs[0]))
                {
                    Utils.CheckSyntax(xs, xs.Count >= 3);
                    Symbol def = (Symbol)xs[0];
                    object v = xs[1];
                    var body = xs.Skip(2).ToList();
                    if (v is List<object>)
                    {
                        // (define (f a b . rest) body) → (define f (lambda (a b . rest) body))
                        var args = (List<object>)v;
                        Utils.CheckSyntax(xs, args.Count > 0);
                        var f = args[0];
                        var @params = args.Skip(1).ToList();
                        return expand(new List<object> { def, f, Enumerable.Concat(new object[] { Symbol.LAMBDA, @params }, body).ToList() }, false);
                    }
                    else
                    {
                        Utils.CheckSyntax(xs, xs.Count == 3);
                        Utils.CheckSyntax(xs, v is Symbol);
                        var expr = expand(xs[2], false);
                        if (Symbol.DEFINE_MACRO.Equals(def))
                        {
                            Utils.CheckSyntax(xs, topLevel, "define-macro is only allowed at the top level");
                            var proc = EvaluateExpression(expr, env);
                            Utils.CheckSyntax(xs, proc is Procedure, "macro must be a procedure");
                            macroTable[(Symbol)v] = (Procedure)proc;
                            return None.Instance;
                        }
                        return new List<object> { Symbol.DEFINE, v, expr };
                    }
                }
                else if (Symbol.BEGIN.Equals(xs[0]))
                {
                    if (xs.Count == 1) return None.Instance;
                    return xs.Select(e => expand(e, topLevel)).ToList();
                }
                else if (Symbol.LAMBDA.Equals(xs[0]))
                {
                    Utils.CheckSyntax(xs, xs.Count >= 3);
                    var vars = xs[1];
                    ValidateLambdaParams(xs, vars);

                    object body = xs.Count == 3
                        ? xs[2]
                        : Enumerable.Concat(new[] { Symbol.BEGIN }, xs.Skip(2)).ToList();

                    return new List<object> { Symbol.LAMBDA, vars, expand(body, false) };
                }
                else if (Symbol.QUASIQUOTE.Equals(xs[0]))
                {
                    Utils.CheckSyntax(xs, xs.Count == 2);
                    return ExpandQuasiquote(xs[1]);
                }
                else if (xs[0] is Symbol && macroTable.TryGetValue((Symbol)xs[0], out procedure))
                {
                    return expand(procedure.Call(xs.Skip(1).ToList()), topLevel);
                }
                else
                {
                    return xs.Select(p => expand(p, false)).ToList();
                }
            };

            return expand(expression, isTopLevel);
        }

        private static void ValidateLambdaParams(object context, object vars)
        {
            if (vars is Symbol) return; // (lambda args body) — variadic capture

            Utils.CheckSyntax(context, vars is List<object>, "illegal lambda argument");
            var varList = (List<object>)vars;
            int dotIdx = varList.FindIndex(v => Symbol.DOT.Equals(v));
            if (dotIdx >= 0)
            {
                Utils.CheckSyntax(context, dotIdx == varList.Count - 2,
                    "dot must be followed by exactly one rest symbol");
                Utils.CheckSyntax(context,
                    varList.Take(dotIdx).All(v => v is Symbol) && varList[dotIdx + 1] is Symbol,
                    "lambda parameters must be symbols");
            }
            else
            {
                Utils.CheckSyntax(context, varList.All(v => v is Symbol), "lambda parameters must be symbols");
            }
        }

        public static object EvaluateExpression(object expr, Environment env)
        {
            while (true)
            {
                if (expr is Symbol)
                    return env[(Symbol)expr];

                if (!(expr is List<object>))
                    return expr; // constant literal

                var exprList = (List<object>)expr;

                if (Symbol.QUOTE.Equals(exprList[0]))
                {
                    return exprList[1];
                }
                else if (Symbol.IF.Equals(exprList[0]))
                {
                    expr = Utils.IsTruthy(EvaluateExpression(exprList[1], env))
                        ? exprList[2]
                        : exprList[3];
                }
                else if (Symbol.DEFINE.Equals(exprList[0]))
                {
                    var variable = (Symbol)exprList[1];
                    env[variable] = EvaluateExpression(exprList[2], env);
                    return None.Instance;
                }
                else if (Symbol.SET.Equals(exprList[0]))
                {
                    var sym = (Symbol)exprList[1];
                    var containing = env.TryFindContainingEnv(sym);
                    if (containing == null) throw new KeyNotFoundException("Symbol not defined: " + sym);
                    containing[sym] = EvaluateExpression(exprList[2], env);
                    return None.Instance;
                }
                else if (Symbol.LAMBDA.Equals(exprList[0]))
                {
                    var rawParams = exprList[1];
                    LambdaParams parameters;
                    if (rawParams is Symbol)
                    {
                        parameters = LambdaParams.RestOnly((Symbol)rawParams);
                    }
                    else
                    {
                        var paramList = (List<object>)rawParams;
                        int dotIdx = paramList.FindIndex(p => Symbol.DOT.Equals(p));
                        if (dotIdx >= 0)
                        {
                            var required = paramList.Take(dotIdx).Cast<Symbol>().ToList();
                            parameters = LambdaParams.WithRest(required, (Symbol)paramList[dotIdx + 1]);
                        }
                        else
                        {
                            parameters = LambdaParams.Fixed(paramList.Cast<Symbol>().ToList());
                        }
                    }
                    return new Procedure(parameters, exprList[2], env);
                }
                else if (Symbol.BEGIN.Equals(exprList[0]))
                {
                    for (int i = 1; i < exprList.Count - 1; i++)
                        EvaluateExpression(exprList[i], env);
                    expr = exprList[exprList.Count - 1]; // tail call
                }
                else
                {
                    var rawProc = EvaluateExpression(exprList[0], env);
                    if (!(rawProc is ICallable))
                        throw new InvalidCastException(string.Format("Object is not callable: {0}", rawProc));

                    var args = exprList.Skip(1).Select(a => EvaluateExpression(a, env)).ToList();
                    if (rawProc is Procedure)
                    {
                        var proc = (Procedure)rawProc;
                        expr = proc.Body;
                        env = Environment.FromVariablesAndValues(proc.Parameters, args, proc.Env);
                    }
                    else
                    {
                        return ((ICallable)rawProc).Call(args);
                    }
                }
            }
        }

        private static bool IsPair(object x) =>
            x is List<object> && ((List<object>)x).Count > 0;

        private static object ExpandQuasiquote(object x)
        {
            if (!IsPair(x)) return new List<object> { Symbol.QUOTE, x };
            var xs = (List<object>)x;
            Utils.CheckSyntax(xs, !Symbol.UNQUOTE_SPLICING.Equals(xs[0]), "Cannot splice");
            if (Symbol.UNQUOTE.Equals(xs[0]))
            {
                Utils.CheckSyntax(xs, xs.Count == 2);
                return xs[1];
            }
            else if (IsPair(xs[0]) && Symbol.UNQUOTE_SPLICING.Equals(((List<object>)xs[0])[0]))
            {
                var x0 = (List<object>)xs[0];
                Utils.CheckSyntax(x0, x0.Count == 2);
                return new List<object> { Symbol.APPEND, x0[1], ExpandQuasiquote(xs.Skip(1).ToList()) };
            }
            else
            {
                return new List<object> { Symbol.CONS, ExpandQuasiquote(xs[0]), ExpandQuasiquote(xs.Skip(1).ToList()) };
            }
        }

        private static object ParseAtom(string token)
        {
            int intVal;
            double floatVal;
            if (token == "#t") return true;
            if (token == "#f") return false;
            if (token.Length > 0 && token[0] == '"')
                return ParseStringLiteral(token);
            if (int.TryParse(token, out intVal)) return intVal;
            if (double.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out floatVal))
                return floatVal;
            return Symbol.FromString(token);
        }

        private static string ParseStringLiteral(string token)
        {
            var value = new System.Text.StringBuilder(token.Length - 2);
            for (int i = 1; i < token.Length - 1; i++)
            {
                var ch = token[i];
                if (ch != '\\')
                {
                    value.Append(ch);
                    continue;
                }

                Utils.CheckSyntax(token, i + 1 < token.Length - 1, "unterminated string escape");
                var escaped = token[++i];
                switch (escaped)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    default:
                        throw new SyntaxError("unsupported string escape: \\" + escaped);
                }
            }

            return value.ToString();
        }

        public struct EvaluationResult
        {
            private readonly Exception error;
            private readonly object result;

            public EvaluationResult(Exception error, object result) : this()
            {
                this.error = error;
                this.result = result;
            }

            public Exception Error => this.error;
            public object Result => this.result;
        }

        public class InPort
        {
            private const string tokenizer = @"^\s*(,@|[('`,)]|""(?:[\\].|[^\\""])*""|;.*|[^\s('""`,;)]*)(.*)";
            private System.IO.TextReader file;
            private string line;

            public InPort(System.IO.TextReader file)
            {
                this.file = file;
                this.line = string.Empty;
            }

            public object NextToken()
            {
                while (true)
                {
                    if (this.line == string.Empty)
                        this.line = this.file.ReadLine();

                    if (this.line == string.Empty) continue;
                    if (this.line == null) return Symbol.EOF;

                    var res = Regex.Match(this.line, tokenizer);
                    var token = res.Groups[1].Value;
                    this.line = res.Groups[2].Value;

                    if (string.IsNullOrEmpty(token))
                    {
                        var tmp = this.line;
                        this.line = string.Empty;
                        if (tmp.Trim() != string.Empty)
                            Utils.CheckSyntax(tmp, false, "unexpected syntax");
                    }

                    if (!string.IsNullOrEmpty(token) && !token.StartsWith(";"))
                        return token;
                }
            }
        }
    }
}
