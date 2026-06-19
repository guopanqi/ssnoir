#nullable enable
using System;
using System.Collections.Generic;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public static class NativeFunctions
    {
        private static int ConvertToInt(object value)
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is long l) return (int)l;
            return Convert.ToInt32(value);
        }

        public static void Register(Interpreter interpreter, GameState gameState)
        {
            // --- New Native Bridge APIs ---
            interpreter.DefineGlobal(Symbol.FromString("__item-count"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__item-count requires 1 argument: item-id");
                string itemId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                return gameState.Inventory.GetCount(itemId);
            }, "__item-count"));

            interpreter.DefineGlobal(Symbol.FromString("__set-item-count!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-item-count! requires 2 arguments: item-id and count");
                string itemId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                int count = ConvertToInt(args[1]);
                if (count < 0) throw new ArgumentException("item count cannot be negative");
                gameState.Inventory.SetCount(itemId, count);
                return new None();
            }, "__set-item-count!"));

            interpreter.DefineGlobal(Symbol.FromString("__party-health"), new NativeProcedure(args =>
            {
                return gameState.Team.Health;
            }, "__party-health"));

            interpreter.DefineGlobal(Symbol.FromString("__set-party-health!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__set-party-health! requires 1 argument");
                int n = ConvertToInt(args[0]);
                gameState.Team.Health = Math.Clamp(n, 0, gameState.Team.MaxHealth);
                return new None();
            }, "__set-party-health!"));

            interpreter.DefineGlobal(Symbol.FromString("__party-supplies"), new NativeProcedure(args =>
            {
                return gameState.Team.Supplies;
            }, "__party-supplies"));

            interpreter.DefineGlobal(Symbol.FromString("__set-party-supplies!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__set-party-supplies! requires 1 argument");
                int n = ConvertToInt(args[0]);
                gameState.Team.Supplies = Math.Clamp(n, 0, gameState.Team.MaxSupplies);
                return new None();
            }, "__set-party-supplies!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-stress"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__actor-stress requires 1 argument: actor-id");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                return actor.Stress;
            }, "__actor-stress"));

            interpreter.DefineGlobal(Symbol.FromString("__set-actor-stress!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-actor-stress! requires 2 arguments: actor-id and stress");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                int n = ConvertToInt(args[1]);
                if (n < 0) throw new ArgumentException("stress cannot be negative");
                gameState.Team.SetActorStressSafe(actorId, n);
                return new None();
            }, "__set-actor-stress!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-status"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__actor-status requires 1 argument: actor-id");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                return Symbol.FromString(actor.Status);
            }, "__actor-status"));

            interpreter.DefineGlobal(Symbol.FromString("__set-actor-status!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-actor-status! requires 2 arguments: actor-id and status");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                string status = args[1] is Symbol symStatus ? symStatus.AsString : args[1]?.ToString() ?? "";
                if (status != "active" && status != "away") throw new ArgumentException("status must be 'active or 'away");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                actor.Status = status;
                // Trigger OnTeamChanged
                gameState.Team.ApplyStress(actorId, 0);
                return new None();
            }, "__set-actor-status!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-stat"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__actor-stat requires 2 arguments: actor-id and stat-name");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                string statName = args[1] is Symbol symStat ? symStat.AsString : args[1]?.ToString() ?? "";
                
                string normalizedStat = statName.ToLowerInvariant();
                if (normalizedStat != "violence" && normalizedStat != "knowledge" && normalizedStat != "sharpness" && normalizedStat != "coding")
                    throw new ArgumentException("statName must be violence/knowledge/sharpness/coding");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                return actor.Stats.TryGetValue(normalizedStat, out var val) ? val : 1;
            }, "__actor-stat"));

            interpreter.DefineGlobal(Symbol.FromString("__current-actor"), new NativeProcedure(args =>
            {
                if (gameState.CurrentContext == null) throw new InvalidOperationException("__current-actor called without action context");
                return Symbol.FromString(gameState.CurrentContext.ActorId);
            }, "__current-actor"));

            interpreter.DefineGlobal(Symbol.FromString("__game-mode"), new NativeProcedure(args =>
            {
                string mode = gameState.CurrentContext?.Mode ?? "world";
                return Symbol.FromString(mode);
            }, "__game-mode"));

            interpreter.DefineGlobal(Symbol.FromString("__notify!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__notify! requires 1 argument: text");
                string text = args[0]?.ToString() ?? "";
                gameState.NotificationCenter.Push(text, NotificationKind.Info);
                return new None();
            }, "__notify!"));

            interpreter.DefineGlobal(Symbol.FromString("__spotlight!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__spotlight! requires 2 arguments: title subtitle");
                if (!(args[0] is string title)) throw new ArgumentException("__spotlight! title must be a string");
                if (!(args[1] is string subtitle)) throw new ArgumentException("__spotlight! subtitle must be a string");
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.Spotlights.Add(new SpotlightCard { Title = title, Subtitle = subtitle });
                else
                    gameState.SpotlightCenter.Show(title, subtitle);
                return new None();
            }, "__spotlight!"));

            interpreter.DefineGlobal(Symbol.FromString("__upgrade-actor-stat!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__upgrade-actor-stat! requires 2 arguments: actor-id and stat-id");
                string actorId = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                string statId = args[1] is Symbol symStat ? symStat.AsString : args[1]?.ToString() ?? "";
                gameState.Team.UpgradeActorStat(actorId, statId);
                return new None();
            }, "__upgrade-actor-stat!"));

            // --- Existing Native Procedures ---
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

            interpreter.DefineGlobal(Symbol.FromString("quotient"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("quotient requires 2 arguments");
                int divisor = ConvertToInt(args[1]);
                if (divisor == 0) throw new DivideByZeroException("quotient divisor cannot be zero");
                return ConvertToInt(args[0]) / divisor;
            }, "quotient"));

            interpreter.DefineGlobal(Symbol.FromString("remainder"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("remainder requires 2 arguments");
                int divisor = ConvertToInt(args[1]);
                if (divisor == 0) throw new DivideByZeroException("remainder divisor cannot be zero");
                return ConvertToInt(args[0]) % divisor;
            }, "remainder"));

            interpreter.DefineGlobal(Symbol.FromString("modulo"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("modulo requires 2 arguments");
                int dividend = ConvertToInt(args[0]);
                int divisor = ConvertToInt(args[1]);
                if (divisor == 0) throw new DivideByZeroException("modulo divisor cannot be zero");
                int result = dividend % divisor;
                if ((result < 0 && divisor > 0) || (result > 0 && divisor < 0))
                {
                    result += divisor;
                }
                return result;
            }, "modulo"));

            interpreter.DefineGlobal(Symbol.FromString("zero?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("zero? requires 1 argument");
                return ConvertToInt(args[0]) == 0;
            }, "zero?"));

            interpreter.DefineGlobal(Symbol.FromString("even?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("even? requires 1 argument");
                return ConvertToInt(args[0]) % 2 == 0;
            }, "even?"));

            interpreter.DefineGlobal(Symbol.FromString("odd?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("odd? requires 1 argument");
                return ConvertToInt(args[0]) % 2 != 0;
            }, "odd?"));

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
