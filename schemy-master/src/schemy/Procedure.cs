// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Schemy
{
    public interface ICallable
    {
        object Call(List<object> args);
    }

    /// <summary>
    /// A procedure implemented in Scheme.
    /// </summary>
    public class Procedure : ICallable
    {
        private readonly LambdaParams parameters;
        private readonly object body;
        private readonly Environment env;

        public Procedure(LambdaParams parameters, object body, Environment env)
        {
            this.parameters = parameters;
            this.body = body;
            this.env = env;
        }

        public object Body => this.body;
        public LambdaParams Parameters => this.parameters;
        public Environment Env => this.env;

        /// <remarks>
        /// Under normal invocation this method is bypassed by the tail-call loop in
        /// <see cref="Interpreter.EvaluateExpression"/>. It is used for macro expansion
        /// and any other caller that needs a direct call.
        /// </remarks>
        public object Call(List<object> args)
        {
            return Interpreter.EvaluateExpression(
                this.body,
                Environment.FromVariablesAndValues(this.parameters, args, this.env));
        }

        public override string ToString()
        {
            object paramRepr;
            if (!parameters.IsVariadic)
            {
                paramRepr = parameters.Required.Cast<object>().ToList();
            }
            else if (parameters.Required.Count == 0)
            {
                paramRepr = parameters.Rest;
            }
            else
            {
                var list = parameters.Required.Cast<object>().ToList();
                list.Add(Symbol.DOT);
                list.Add(parameters.Rest);
                paramRepr = list;
            }

            var form = new List<object> { Symbol.LAMBDA, paramRepr, this.body };
            return Utils.PrintExpr(form);
        }
    }

    /// <summary>
    /// A procedure implemented in .NET.
    /// </summary>
    public class NativeProcedure : ICallable
    {
        private readonly Func<List<object>, object> func;
        private readonly string name;

        public NativeProcedure(Func<List<object>, object> func, string name = null)
        {
            this.func = func;
            this.name = name;
        }

        public object Call(List<object> args) => this.func(args);

        public static NativeProcedure Create<T1, T2>(Func<T1, T2> func, string name = null)
        {
            return new NativeProcedure(args =>
            {
                Utils.CheckArity(args, 1);
                return func(Utils.ConvertType<T1>(args[0]));
            }, name);
        }

        public static NativeProcedure Create<T1, T2, T3>(Func<T1, T2, T3> func, string name = null)
        {
            return new NativeProcedure(args =>
            {
                Utils.CheckArity(args, 2);
                return func(Utils.ConvertType<T1>(args[0]), Utils.ConvertType<T2>(args[1]));
            }, name);
        }

        public static NativeProcedure Create<T1, T2, T3, T4>(Func<T1, T2, T3, T4> func, string name = null)
        {
            return new NativeProcedure(args =>
            {
                Utils.CheckArity(args, 3);
                return func(
                    Utils.ConvertType<T1>(args[0]),
                    Utils.ConvertType<T2>(args[1]),
                    Utils.ConvertType<T3>(args[2]));
            }, name);
        }

        public static NativeProcedure Create<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5> func, string name = null)
        {
            return new NativeProcedure(args =>
            {
                Utils.CheckArity(args, 4);
                return func(
                    Utils.ConvertType<T1>(args[0]),
                    Utils.ConvertType<T2>(args[1]),
                    Utils.ConvertType<T3>(args[2]),
                    Utils.ConvertType<T4>(args[3]));
            }, name);
        }

        public static NativeProcedure Create<T1>(Func<T1> func, string name = null)
        {
            return new NativeProcedure(args =>
            {
                Utils.CheckArity(args, 0);
                return func();
            }, name);
        }

        public override string ToString() =>
            string.Format("#<NativeProcedure:{0}>", string.IsNullOrEmpty(this.name) ? "noname" : this.name);
    }
}
