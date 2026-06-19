// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Schemy
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Tracks the state of an interpreter or a procedure. Supports lexical scoping.
    /// </summary>
    public class Environment
    {
        private readonly IDictionary<Symbol, object> store;
        private readonly Environment outer;

        public Environment(IDictionary<Symbol, object> env, Environment outer)
        {
            this.store = env;
            this.outer = outer;
        }

        public static Environment CreateEmpty() =>
            new Environment(new Dictionary<Symbol, object>(), null);

        public static Environment FromVariablesAndValues(LambdaParams parameters, List<object> values, Environment outer)
        {
            var dict = new Dictionary<Symbol, object>();

            if (parameters.IsVariadic)
            {
                if (values.Count < parameters.Required.Count)
                    throw new SyntaxError(string.Format(
                        "Too few arguments. Expecting at least {0}, got {1}.",
                        parameters.Required.Count, values.Count));

                for (int i = 0; i < parameters.Required.Count; i++)
                    dict[parameters.Required[i]] = values[i];

                dict[parameters.Rest] = values.Skip(parameters.Required.Count).ToList();
            }
            else
            {
                if (values.Count != parameters.Required.Count)
                    throw new SyntaxError(string.Format(
                        "Unexpected number of arguments. Expecting {0}, got {1}.",
                        parameters.Required.Count, values.Count));

                for (int i = 0; i < values.Count; i++)
                    dict[parameters.Required[i]] = values[i];
            }

            return new Environment(dict, outer);
        }

        public bool TryGetValue(Symbol sym, out object val)
        {
            var env = this.TryFindContainingEnv(sym);
            if (env != null)
            {
                val = env.store[sym];
                return true;
            }
            val = null;
            return false;
        }

        public Environment TryFindContainingEnv(Symbol sym)
        {
            object val;
            if (this.store.TryGetValue(sym, out val)) return this;
            if (this.outer != null) return this.outer.TryFindContainingEnv(sym);
            return null;
        }

        public object this[Symbol sym]
        {
            get
            {
                object val;
                if (this.TryGetValue(sym, out val)) return val;
                throw new KeyNotFoundException(string.Format("Symbol not defined: {0}", sym));
            }
            set { this.store[sym] = value; }
        }
    }
}
