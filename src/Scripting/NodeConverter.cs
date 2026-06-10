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

            if (nodeExpr.Count != 5)
            {
                throw new InvalidOperationException($"Invalid node expression length (expected 5, got {nodeExpr.Count})");
            }

            if (!(nodeExpr[0] is Symbol sym && sym.AsString == "node"))
            {
                throw new InvalidOperationException($"Invalid node expression header (expected symbol 'node, got '{nodeExpr[0]}')");
            }

            if (!(nodeExpr[1] is string name))
            {
                throw new InvalidOperationException($"Invalid node name (expected string, got {nodeExpr[1]?.GetType().FullName ?? "null"})");
            }

            var clocksExpr = nodeExpr[2];
            var clocks = ParseClocks(clocksExpr);

            var childrenExpr = nodeExpr[3];
            if (!(childrenExpr is List<object>))
            {
                throw new InvalidOperationException($"Invalid node children (expected list, got {childrenExpr?.GetType().FullName ?? "null"})");
            }

            var effectExpr = nodeExpr[4];
            Action? effect = null;
            if (effectExpr is Procedure proc)
            {
                effect = () =>
                {
                    proc.Call(new List<object>());
                };
            }
            else if (effectExpr is bool b && b == false)
            {
                // false effect is valid, no action
            }
            else
            {
                throw new InvalidOperationException($"Invalid node effect (expected Procedure or false, got {effectExpr?.GetType().FullName ?? "null"})");
            }

            var children = ConvertList(childrenExpr, interpreter);

            var node = new GameNode
            {
                Name = name,
                Children = children,
                Effect = effect
            };
            node.Clocks.AddRange(clocks);
            return node;
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
