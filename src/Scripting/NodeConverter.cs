using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (item is GameNode gameNode)
            {
                return gameNode;
            }

            if (item is List<object> nodeExpr)
            {
                // Check if it's a node list structure: ('node name children effect)
                if (nodeExpr.Count >= 4 && nodeExpr[0] is Symbol sym && sym.AsString == "node")
                {
                    var name = nodeExpr[1] as string;
                    var childrenExpr = nodeExpr[2];
                    var effectExpr = nodeExpr[3];

                    Debug.Assert(name != null, "GameNode name must be a string");

                    var children = ConvertList(childrenExpr, interpreter);

                    Action effect = null;
                    if (effectExpr != null && !(effectExpr is bool b && b == false))
                    {
                        if (effectExpr is Procedure proc)
                        {
                            effect = () =>
                            {
                                proc.Call(new List<object>());
                            };
                        }
                        else
                        {
                            Debug.Fail($"Effect for node '{name}' is not a Procedure (actual: {effectExpr.GetType().FullName})");
                        }
                    }

                    return new GameNode
                    {
                        Name = name,
                        Children = children,
                        Effect = effect
                    };
                }
            }

            throw new InvalidOperationException($"Invalid node expression: {item}");
        }
    }
}
