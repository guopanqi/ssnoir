#nullable enable
using System;
using System.Collections.Generic;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public static class NodeConverter
    {
        public static List<GameNode> ConvertList(object schemeVal, Interpreter interpreter)
        {
            var nodes = new List<GameNode>();
            if (schemeVal is List<object> list)
            {
                foreach (var item in list)
                {
                    var node = ConvertSingle(item, interpreter);
                    nodes.Add(node);
                }
                return nodes;
            }

            throw new InvalidOperationException($"Expected a node list, got {schemeVal?.GetType().FullName ?? "null"}");
        }

        public static GameNode ConvertSingle(object item, Interpreter interpreter)
        {
            if (!(item is List<object> nodeExpr))
            {
                throw new InvalidOperationException($"Invalid node expression (must be a list): {item?.GetType().FullName ?? "null"}");
            }

            if (nodeExpr.Count < 2)
            {
                throw new InvalidOperationException($"Invalid node expression length (expected at least 2, got {nodeExpr.Count})");
            }

            if (!(nodeExpr[0] is Symbol sym && sym.AsString == "node"))
            {
                throw new InvalidOperationException($"Invalid node expression header (expected symbol 'node, got '{nodeExpr[0]}')");
            }

            if (!(nodeExpr[1] is string name))
            {
                throw new InvalidOperationException($"Invalid node name (expected string, got {nodeExpr[1]?.GetType().FullName ?? "null"})");
            }

            List<GameClock> clocks = new List<GameClock>();
            List<GameNode> children = new List<GameNode>();
            List<ActionCost> requires = new List<ActionCost>();
            GameResolve? resolve = null;

            for (int i = 2; i < nodeExpr.Count; i += 2)
            {
                if (i + 1 >= nodeExpr.Count)
                {
                    throw new InvalidOperationException($"Missing value for keyword {nodeExpr[i]}");
                }

                if (!(nodeExpr[i] is Symbol kw))
                {
                    throw new InvalidOperationException($"Expected keyword symbol at index {i}, got {nodeExpr[i]}");
                }

                string kwStr = kw.AsString.ToLowerInvariant();
                object val = nodeExpr[i + 1];

                if (kwStr == ":clocks")
                {
                    clocks = ParseClocks(val);
                }
                else if (kwStr == ":children")
                {
                    children = ConvertList(val, interpreter);
                }
                else if (kwStr == ":requires")
                {
                    requires = ParseRequires(val);
                }
                else if (kwStr == ":resolve")
                {
                    resolve = ParseResolve(val, interpreter);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown node keyword {kwStr}");
                }
            }

            var node = new GameNode
            {
                Name = name,
                Children = children,
                Requires = requires,
                Resolve = resolve
            };
            node.Clocks.AddRange(clocks);
            return node;
        }

        private static List<ActionCost> ParseRequires(object requiresExpr)
        {
            var requires = new List<ActionCost>();
            if (requiresExpr is bool b && b == false)
            {
                return requires; // #f means no requirements
            }

            if (requiresExpr is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> costExpr && costExpr.Count > 0)
                    {
                        if (costExpr[0] is Symbol costSym)
                        {
                            var typeStr = costSym.AsString.ToLowerInvariant();
                            if (typeStr == "die")
                            {
                                requires.Add(new ActionCost { Type = "die" });
                            }
                            else if (typeStr == "item" && costExpr.Count >= 3)
                            {
                                var itemName = costExpr[1] as string ?? "Unknown";
                                int qty = 0;
                                if (costExpr[2] is double d) qty = (int)d;
                                else if (costExpr[2] is long l) qty = (int)l;
                                else if (costExpr[2] is int i) qty = i;

                                requires.Add(new ActionCost { Type = "item", ItemName = itemName, Qty = qty });
                            }
                        }
                    }
                }
            }
            return requires;
        }

        private static GameResolve? ParseResolve(object resolveExpr, Interpreter interpreter)
        {
            if (resolveExpr is bool b && b == false)
            {
                return null;
            }

            if (!(resolveExpr is List<object> list) || list.Count == 0)
            {
                throw new InvalidOperationException("Invalid resolve expression: must be a non-empty list");
            }

            if (!(list[0] is Symbol typeSym))
            {
                throw new InvalidOperationException($"Invalid resolve type: expected symbol, got {list[0]}");
            }

            var typeStr = typeSym.AsString.ToLowerInvariant();
            if (typeStr == "instant" && list.Count >= 2)
            {
                var effectProc = list[1] as Procedure;
                return new GameResolve
                {
                    Type = ResolveType.Instant,
                    Effect = effectProc != null ? () => effectProc.Call(new List<object>()) : null
                };
            }
            else if (typeStr == "roll" && list.Count >= 5)
            {
                string skillName = string.Empty;
                if (list[1] is Symbol sSym) skillName = sSym.AsString;
                else if (list[1] is string sStr) skillName = sStr;

                Func<List<DifficultyModifierInfo>>? getModifiers = null;
                int failIndex = 2;

                // Check if 3rd element is a difficulty modifier callback (Procedure)
                if (list.Count >= 6 && list[2] is Procedure modProc)
                {
                    getModifiers = () =>
                    {
                        var result = modProc.Call(new List<object>());
                        return ParseDifficultyModifiers(result);
                    };
                    failIndex = 3;
                }

                var failProc = list[failIndex] as Procedure;
                var neutralProc = list[failIndex + 1] as Procedure;
                var successProc = list[failIndex + 2] as Procedure;

                return new GameResolve
                {
                    Type = ResolveType.Roll,
                    SkillName = skillName,
                    GetDifficultyModifiers = getModifiers,
                    OnFail = failProc != null ? () => failProc.Call(new List<object>()) : null,
                    OnNeutral = neutralProc != null ? () => neutralProc.Call(new List<object>()) : null,
                    OnSuccess = successProc != null ? () => successProc.Call(new List<object>()) : null
                };
            }
            else if (typeStr == "observe" && list.Count >= 2)
            {
                var text = list[1] as string ?? string.Empty;
                return new GameResolve
                {
                    Type = ResolveType.Observe,
                    ObserveText = text
                };
            }

            throw new InvalidOperationException($"Unknown resolve type or invalid argument count: {typeStr}");
        }

        private static List<DifficultyModifierInfo> ParseDifficultyModifiers(object modifiersExpr)
        {
            var modifiers = new List<DifficultyModifierInfo>();
            if (modifiersExpr is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> modExpr && modExpr.Count >= 3)
                    {
                        if (modExpr[0] is Symbol sym && sym.AsString == "modifier")
                        {
                            int value = 0;
                            if (modExpr[1] is double d) value = (int)d;
                            else if (modExpr[1] is long l) value = (int)l;
                            else if (modExpr[1] is int i) value = i;

                            var reason = modExpr[2] as string ?? string.Empty;

                            modifiers.Add(new DifficultyModifierInfo
                            {
                                Value = value,
                                Reason = reason
                            });
                        }
                    }
                }
            }
            return modifiers;
        }

        private static List<GameClock> ParseClocks(object clocksExpr)
        {
            var clocks = new List<GameClock>();
            if (clocksExpr is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> clockExpr && clockExpr.Count >= 5)
                    {
                        if (clockExpr[0] is Symbol sym && sym.AsString == "clock")
                        {
                            var label = clockExpr[1] as string ?? "Unknown";

                            int current = 0;
                            if (clockExpr[2] is double d1) current = (int)d1;
                            else if (clockExpr[2] is long l1) current = (int)l1;
                            else if (clockExpr[2] is int i1) current = i1;

                            int max = 1;
                            if (clockExpr[3] is double d2) max = (int)d2;
                            else if (clockExpr[3] is long l2) max = (int)l2;
                            else if (clockExpr[3] is int i2) max = i2;

                            ClockStyle style = ClockStyle.Segments;
                            if (clockExpr[4] is Symbol styleSym)
                            {
                                var styleStr = styleSym.AsString.ToLowerInvariant();
                                if (styleStr == "segments") style = ClockStyle.Segments;
                                else if (styleStr == "countdown") style = ClockStyle.Countdown;
                                else if (styleStr == "pie") style = ClockStyle.Pie;
                            }
                            else if (clockExpr[4] is string styleStr)
                            {
                                var s = styleStr.ToLowerInvariant();
                                if (s == "segments") style = ClockStyle.Segments;
                                else if (s == "countdown") style = ClockStyle.Countdown;
                                else if (s == "pie") style = ClockStyle.Pie;
                            }

                            clocks.Add(new GameClock
                            {
                                Label = label,
                                Current = current,
                                Max = max,
                                Style = style
                            });
                        }
                    }
                }
            }
            return clocks;
        }
    }
}
