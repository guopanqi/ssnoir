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
            string? anchorName = null;
            string subtitle = string.Empty;
            bool disabled = false;
            bool isPlace = false;
            string carryItemId = string.Empty;
            List<ArrivalBeat> arrivals = new List<ArrivalBeat>();

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
                else if (kwStr == ":anchor")
                {
                    if (!(val is string parsedAnchor) || string.IsNullOrWhiteSpace(parsedAnchor))
                        throw new InvalidOperationException($"Node '{name}' :anchor must be a non-empty string.");
                    anchorName = parsedAnchor;
                }
                else if (kwStr == ":place")
                {
                    if (!(val is bool parsedPlace))
                        throw new InvalidOperationException("Node :place must be a boolean.");
                    isPlace = parsedPlace;
                }
                else if (kwStr == ":carry-item")
                {
                    if (!(val is string parsedCarry) || string.IsNullOrWhiteSpace(parsedCarry))
                        throw new InvalidOperationException("Node :carry-item must be a non-empty string.");
                    carryItemId = parsedCarry;
                }
                else if (kwStr == ":arrivals")
                {
                    arrivals = ParseArrivals(val, name);
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

            // 节点只有两种合法形状：容器（只有 :children）或动作（只有 :resolve）。
            // 二者互斥——客户端 IsContainer = (Resolve == null)，带 resolve 的节点其 children
            // 永远不会被导航或渲染。历史上这类矛盾节点能静默加载，子节点却神秘消失，
            // 极难排查；这里在加载期直接拦下，把哑错变成响错。
            if (resolve != null && children.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Node '{name}' has both :resolve and :children. A node is either a container " +
                    "(:children only) or an action (:resolve only); a resolve node's children are " +
                    "silently ignored. Move the children into a container, or drop the :resolve.");
            }

            // Place 是容器的一种，不是动作：它只有走进去这一种交互。
            if (isPlace && resolve != null)
            {
                throw new InvalidOperationException(
                    $"Place '{name}' 不能有 :resolve。地点只能被走进去，不能被执行。");
            }

            // 入场节拍属于「走进一个地点」这件事，挂在别处永远不会被触发。
            if (arrivals.Count > 0 && !isPlace)
            {
                throw new InvalidOperationException(
                    $"Node '{name}' 声明了 :arrivals 却不是 place。入场节拍只能挂在地点上——" +
                    "把这个容器改成 (place ...)，或者把节拍搬到它所在的地点上。");
            }

            var node = new GameNode
            {
                Name = name,
                IsPlace = isPlace,
                CarryItemId = carryItemId,
                Arrivals = arrivals,
                AnchorName = anchorName,
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

        // (arrival "id" effect) 的列表。ID 只在同一个地点内要求唯一——节点名已经全局
        // 唯一（见 SceneManager.AssertUniqueNodeNames），(地点名, ID) 就足够定位一拍。
        private static List<ArrivalBeat> ParseArrivals(object arrivalsExpr, string placeName)
        {
            var arrivals = new List<ArrivalBeat>();
            if (arrivalsExpr is bool noArrivals && noArrivals == false)
            {
                return arrivals;
            }

            if (!(arrivalsExpr is List<object> list))
            {
                throw new InvalidOperationException(
                    $"Place '{placeName}' :arrivals 必须是列表，得到 {arrivalsExpr?.GetType().FullName ?? "null"}。");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                if (!(item is List<object> expr) || expr.Count != 3
                    || !(expr[0] is Symbol header) || header.AsString != "arrival")
                {
                    throw new InvalidOperationException(
                        $"Place '{placeName}' 的入场节拍必须写成 (arrival \"标识\" 过程)。");
                }

                if (!(expr[1] is string id) || string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException(
                        $"Place '{placeName}' 的 arrival 标识必须是非空字符串。");
                }

                if (!(expr[2] is Procedure effect))
                {
                    throw new InvalidOperationException(
                        $"Place '{placeName}' 的 arrival \"{id}\" 的第二个参数必须是过程。");
                }

                if (!seen.Add(id))
                {
                    throw new InvalidOperationException(
                        $"Place '{placeName}' 里有重复的 arrival 标识 \"{id}\"。同一个地点内标识必须唯一。");
                }

                arrivals.Add(new ArrivalBeat { Id = id, Effect = () => effect.Call(new List<object>()) });
            }

            return arrivals;
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
            else if (typeStr == "note" && list.Count >= 3)
            {
                // (note 标题 正文 [钟])。三项都可空，但一条什么都不说的标注是内容错误，
                // 不是一个可以静默画成空白的状态。
                var title = list[1] as string ?? string.Empty;
                var text = list[2] as string ?? string.Empty;
                var noteClock = list.Count >= 4 ? ParseSingleClock(list[3]) : null;
                if (list.Count >= 4 && noteClock == null)
                    throw new InvalidOperationException("note resolve contains invalid clock data");
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text) && noteClock == null)
                    throw new InvalidOperationException("note resolve 至少要有标题、正文或一根钟。");

                return new GameResolve
                {
                    Type = ResolveType.Note,
                    NoteTitle = title,
                    NoteText = text,
                    Clock = noteClock
                };
            }
            else if (typeStr == "clock" && list.Count == 2)
            {
                // 钟卡就是「带读数的标注」。钟的 Label / Note 在这里摊平成标题与正文，
                // 绘制层从此只认一种形状，不必再分「钟卡」与「说明卡」两条路。
                var clock = ParseSingleClock(list[1])
                    ?? throw new InvalidOperationException("clock resolve contains invalid clock data");
                return new GameResolve
                {
                    Type = ResolveType.Note,
                    NoteTitle = clock.Label,
                    NoteText = clock.Note,
                    Clock = clock
                };
            }

            else if (typeStr == "clock")
            {
                throw new InvalidOperationException(
                    "clock resolve 必须且只能包含一根钟：每个 clock-node 对应一条单独的标注。");
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
                throw new InvalidOperationException($"Invalid {context}: expected procedure or (outcome title mode effect)");
            }

            if (list.Count != 4)
            {
                throw new InvalidOperationException($"Invalid {context}: outcome expects exactly 3 values after 'outcome (title mode effect), got {list.Count - 1}");
            }

            if (!(list[1] is string title))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome title must be a string");
            }

            if (!(list[2] is Symbol modeSym))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome mode must be 'light or 'heavy");
            }

            var mode = modeSym.AsString.ToLowerInvariant() switch
            {
                "light" => OutcomePresentationMode.Light,
                "heavy" => OutcomePresentationMode.Heavy,
                _ => throw new InvalidOperationException($"Invalid {context}: unknown outcome mode '{modeSym.AsString}', expected 'light or 'heavy")
            };

            if (!(list[3] is Procedure effectProc))
            {
                throw new InvalidOperationException($"Invalid {context}: outcome effect must be a procedure");
            }

            return new ActionOutcome
            {
                Presentation = new OutcomePresentation
                {
                    Title = title,
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

        // ── 物品容量 ─────────────────────────────────────────────────────
        // (item-capacity-table) 回一串 ("香烟" 5)。只有真的带上限的物品在里面。
        public static Dictionary<string, int> ConvertItemCapacities(object schemeVal)
        {
            var caps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (schemeVal is bool none && none == false)
                return caps;
            if (!(schemeVal is List<object> list))
                throw new InvalidOperationException(
                    $"(item-capacity-table) 必须返回列表，得到 {schemeVal?.GetType().FullName ?? "null"}。");

            foreach (var item in list)
            {
                if (!(item is List<object> pair) || pair.Count != 2 || !(pair[0] is string name))
                    throw new InvalidOperationException("物品容量表的每一项必须是 (\"物品名\" 上限)。");
                int cap = SchemeValue.ToInt(pair[1]);
                if (cap <= 0)
                    throw new InvalidOperationException($"物品 \"{name}\" 的容量必须大于零，得到 {cap}。");
                caps[name] = cap;
            }
            return caps;
        }

        // ── 卷宗 ─────────────────────────────────────────────────────────
        // (get-dossier) 回一串 (dossier "id" :kind … :log …)。字段全部必填由 Scheme 侧
        // 的 dossier 构造器保证，这里只做形状检查——它读到的东西已经规整过。
        public static List<DossierEntry> ConvertDossier(object schemeVal)
        {
            var entries = new List<DossierEntry>();
            if (schemeVal is bool none && none == false)
                return entries;

            if (!(schemeVal is List<object> list))
                throw new InvalidOperationException(
                    $"(get-dossier) 必须返回列表，得到 {schemeVal?.GetType().FullName ?? "null"}。");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var entry = ConvertDossierEntry(item);
                if (!seen.Add(entry.Id))
                    throw new InvalidOperationException(
                        $"卷宗里有重复的标识 \"{entry.Id}\"。每条故事线的标识必须唯一——它同时是存档键。");
                entries.Add(entry);
            }
            return entries;
        }

        private static DossierEntry ConvertDossierEntry(object item)
        {
            if (!(item is List<object> expr) || expr.Count < 2
                || !(expr[0] is Symbol header) || header.AsString != "dossier")
                throw new InvalidOperationException("卷宗条目必须由 (dossier \"标识\" …) 构造。");

            if (!(expr[1] is string id) || string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("卷宗条目的标识必须是非空字符串。");

            string kind = "委托";
            string status = "进行中";
            string now = string.Empty;
            string where = string.Empty;
            var clocks = new List<GameClock>();
            var log = new List<DossierLogEntry>();

            for (int i = 2; i < expr.Count; i += 2)
            {
                if (i + 1 >= expr.Count)
                    throw new InvalidOperationException($"卷宗条目 '{id}' 的关键字 {expr[i]} 少了值。");
                if (!(expr[i] is Symbol kw))
                    throw new InvalidOperationException($"卷宗条目 '{id}' 期待关键字符号，得到 {expr[i]}。");

                object val = expr[i + 1];
                switch (kw.AsString.ToLowerInvariant())
                {
                    case ":kind":   kind = SymbolOrString(val, id, ":kind"); break;
                    case ":status": status = SymbolOrString(val, id, ":status"); break;
                    case ":now":    now = val as string ?? string.Empty; break;
                    case ":where":  where = val as string ?? string.Empty; break;
                    case ":clocks": clocks = ParseClocks(val); break;
                    case ":log":    log = ParseJournal(val, id); break;
                    default:
                        throw new InvalidOperationException($"卷宗条目 '{id}' 收到未知关键字 {kw.AsString}。");
                }
            }

            return new DossierEntry
            {
                Id = id, Kind = kind, Status = status, Now = now, Where = where,
                Clocks = clocks, Log = log,
            };
        }

        private static string SymbolOrString(object val, string id, string field) =>
            val switch
            {
                Symbol s => s.AsString,
                string str => str,
                _ => throw new InvalidOperationException($"卷宗条目 '{id}' 的 {field} 必须是符号或字符串。")
            };

        private static List<DossierLogEntry> ParseJournal(object val, string id)
        {
            var log = new List<DossierLogEntry>();
            if (!(val is List<object> list))
                return log;

            foreach (var item in list)
            {
                if (!(item is List<object> expr) || expr.Count != 3
                    || !(expr[0] is Symbol header) || header.AsString != "journal-entry")
                    throw new InvalidOperationException(
                        $"卷宗条目 '{id}' 的履历必须是 (journal-entry 天 \"正文\") 的列表。");
                log.Add(new DossierLogEntry
                {
                    Day = SchemeValue.ToInt(expr[1]),
                    Text = expr[2] as string ?? string.Empty,
                });
            }
            return log;
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

            // 未知语义直接中断：手写的 clock tuple 拼错样式时，静默退回 progress 会让
            // 一根倒计时长成进度条，而这正是这套语义化要杜绝的事。
            string styleStr = expr[4] is Symbol s ? s.AsString : expr[4] as string ?? "";
            ClockStyle style = styleStr.ToLowerInvariant() switch
            {
                "gauge" => ClockStyle.Gauge,
                "countdown" => ClockStyle.Countdown,
                "readout" => ClockStyle.Readout,
                _ => throw new InvalidOperationException(
                    $"Clock '{label}' has unknown style '{styleStr}' (expected progress / countdown / readout).")
            };

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
