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

            if (nodeExpr.Count != 4)
            {
                throw new InvalidOperationException($"Invalid node expression length (expected 4, got {nodeExpr.Count})");
            }

            if (!(nodeExpr[0] is Symbol sym && sym.AsString == "node"))
            {
                throw new InvalidOperationException($"Invalid node expression header (expected symbol 'node, got '{nodeExpr[0]}')");
            }

            if (!(nodeExpr[1] is string name))
            {
                throw new InvalidOperationException($"Invalid node name (expected string, got {nodeExpr[1]?.GetType().FullName ?? "null"})");
            }

            var childrenExpr = nodeExpr[2];
            if (!(childrenExpr is List<object>))
            {
                throw new InvalidOperationException($"Invalid node children (expected list, got {childrenExpr?.GetType().FullName ?? "null"})");
            }

            var effectExpr = nodeExpr[3];
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

            return new GameNode
            {
                Name = name,
                Children = children,
                Effect = effect
            };
        }
    }
}
