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
            List<string> tags = new List<string>();
            List<GameNode> children = new List<GameNode>();
            List<ActionCost> requires = new List<ActionCost>();
            GameResolve? resolve = null;
            string subtitle = string.Empty;
            bool disabled = false;

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
                else if (kwStr == ":tags")
                {
                    tags = ParseTags(val);
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
                else if (kwStr == ":subtitle")
                {
                    subtitle = val as string ?? string.Empty;
                }
                else if (kwStr == ":disabled")
                {
                    if (!(val is bool parsedDisabled))
                        throw new InvalidOperationException("Node :disabled must be a boolean.");
                    disabled = parsedDisabled;
                }
                else
                {
                    throw new InvalidOperationException($"Unknown node keyword {kwStr}");
                }
            }

            var node = new GameNode
            {
                Name = name,
                Subtitle = subtitle,
                Disabled = disabled,
                Tags = tags,
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
                        if (costExpr[0] is Symbol firstSym)
                        {
                            var firstStr = firstSym.AsString.ToLowerInvariant();
                            if (firstStr == "die" && costExpr.Count >= 1)
                            {
                                requires.Add(new ActionCost { Type = "die" });
                            }
                            else if (firstStr == "item" && costExpr.Count >= 3)
                            {
                                string itemId = costExpr[1] is Symbol s ? s.AsString : costExpr[1]?.ToString() ?? "Unknown";
                                int qty = SchemeValue.ToInt(costExpr[2]);
                                requires.Add(new ActionCost { Type = "item", ItemId = itemId, Qty = qty });
                            }
                        }
                    }
                }
            }
            return requires;
        }

        private static List<string> ParseTags(object tagsExpr)
        {
            var tags = new List<string>();
            if (tagsExpr is bool b && b == false)
            {
                return tags;
            }

            if (!(tagsExpr is List<object> list))
            {
                throw new InvalidOperationException($"Invalid node tags: expected list, got {tagsExpr?.GetType().FullName ?? "null"}");
            }

            foreach (var item in list)
            {
                if (item is string str)
                {
                    tags.Add(str);
                }
                else if (item is Symbol sym)
                {
                    tags.Add(sym.AsString);
                }
                else
                {
                    throw new InvalidOperationException($"Invalid node tag value: expected string or symbol, got {item?.GetType().FullName ?? "null"}");
                }
            }

            return tags;
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
                var outcome = ParseActionOutcome(list[1], "instant effect");
                return new GameResolve
                {
                    Type = ResolveType.Instant,
                    Outcome = outcome
                };
            }
            else if ((typeStr == "roll" || typeStr == "recovery-roll") && list.Count >= 5)
            {
                string skillName = string.Empty;
                if (list[1] is Symbol sSym) skillName = sSym.AsString;
                else if (list[1] is string sStr) skillName = sStr;

                var modifiers = new List<DifficultyModifierInfo>();
                int failIndex = 2;

                // Check if 3rd element is a difficulty modifier callback (Procedure).
                // Modifier functions are read-only render/roll metadata, so they are
                // resolved when the world node is materialized.
                if (list.Count >= 6 && list[2] is Procedure modProc)
                {
                    var result = modProc.Call(new List<object>());
                    modifiers = ParseDifficultyModifiers(result);
                    failIndex = 3;
                }

                var failOutcome = ParseActionOutcome(list[failIndex], "roll fail branch");
                var neutralOutcome = ParseActionOutcome(list[failIndex + 1], "roll neutral branch");
                var successOutcome = ParseActionOutcome(list[failIndex + 2], "roll success branch");

                return new GameResolve
                {
                    Type = ResolveType.Roll,
                    SkillName = skillName,
                    IgnoresStressPenalty = typeStr == "recovery-roll",
                    DifficultyModifiers = modifiers,
                    FailOutcome = failOutcome,
                    NeutralOutcome = neutralOutcome,
                    SuccessOutcome = successOutcome
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
            else if (typeStr == "clock" && list.Count >= 2)
            {
                return new GameResolve
                {
                    Type = ResolveType.Clock,
                    Clock = ParseSingleClock(list[1])
                };
            }

            throw new InvalidOperationException($"Unknown resolve type or invalid argument count: {typeStr}");
        }

        private static ActionOutcome ParseActionOutcome(object expr, string context)
        {
            if (expr is Procedure proc)
            {
                return new ActionOutcome
                {
                    Effect = () => proc.Call(new List<object>())
                };
            }

            if (!(expr is List<object> list) || list.Count == 0)
            {
                throw new InvalidOperationException($"Invalid {context}: expected procedure or outcome list, got {expr?.GetType().FullName ?? "null"}");
            }

            if (!(list[0] is Symbol header) || header.AsString != "outcome")
            {
                throw new InvalidOperationException($"Invalid {context}: expected procedure or (outcome title subtitle mode effect)");
            }

            if (list.Count != 5)
            {
                throw new InvalidOperationException($"Invalid {context}: outcome expects exactly 4 values after 'outcome (title subtitle mode effect), got {list.Count - 1}");
            }

            if (!(list[1] is string title))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome title must be a string");
            }

            if (!(list[2] is string subtitle))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome subtitle must be a string");
            }

            if (!(list[3] is Symbol modeSym))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome mode must be 'light or 'heavy");
            }

            var mode = modeSym.AsString.ToLowerInvariant() switch
            {
                "light" => OutcomePresentationMode.Light,
                "heavy" => OutcomePresentationMode.Heavy,
                _ => throw new InvalidOperationException($"Invalid {context}: unknown outcome mode '{modeSym.AsString}', expected 'light or 'heavy")
            };

            if (!(list[4] is Procedure effectProc))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome effect must be a procedure");
            }

            return new ActionOutcome
            {
                Presentation = new OutcomePresentation
                {
                    Title = title,
                    Subtitle = subtitle,
                    Mode = mode
                },
                Effect = () => effectProc.Call(new List<object>())
            };
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
                    var clock = ParseSingleClock(item);
                    if (clock != null)
                        clocks.Add(clock);
                }
            }
            return clocks;
        }

        private static GameClock? ParseSingleClock(object clockExpr)
        {
            if (!(clockExpr is List<object> expr) || expr.Count < 5)
                return null;
            if (!(expr[0] is Symbol sym && sym.AsString == "clock"))
                return null;

            var label = expr[1] as string ?? "Unknown";
            int current = SchemeValue.ToInt(expr[2]);
            int max = SchemeValue.ToInt(expr[3]);

            ClockStyle style = ClockStyle.Segments;
            string styleStr = expr[4] is Symbol s ? s.AsString : expr[4] as string ?? "";
            if (styleStr.Equals("countdown", StringComparison.OrdinalIgnoreCase)) style = ClockStyle.Countdown;
            else if (styleStr.Equals("pie", StringComparison.OrdinalIgnoreCase)) style = ClockStyle.Pie;

            string note = string.Empty;
            if (expr.Count >= 6)
            {
                if (!(expr[5] is string parsedNote))
                    throw new InvalidOperationException("Clock note must be a string.");
                note = parsedNote;
            }

            return new GameClock { Label = label, Note = note, Current = current, Max = max, Style = style };
        }
    }
}
