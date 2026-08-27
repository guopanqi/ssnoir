#nullable enable
using System;
using System.Collections.Generic;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public static class NativeFunctions
    {
        public static void Register(Interpreter interpreter, GameState gameState)
        {
            // --- New Native Bridge APIs ---
            interpreter.DefineGlobal(Symbol.FromString("__item-count"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__item-count requires 1 argument: item-id");
                string itemId = SchemeValue.AsId(args[0]);
                return gameState.Inventory.GetCount(itemId);
            }, "__item-count"));

            interpreter.DefineGlobal(Symbol.FromString("__set-item-count!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-item-count! requires 2 arguments: item-id and count");
                string itemId = SchemeValue.AsId(args[0]);
                int count = SchemeValue.ToInt(args[1]);
                if (count < 0) throw new ArgumentException("item count cannot be negative");
                int before = gameState.Inventory.GetCount(itemId);
                gameState.Inventory.SetCount(itemId, count);
                int delta = count - before;
                gameState.CurrentActionReport?.AddEffect(
                    ActionEffectKind.Item, itemId, delta,
                    delta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                return new None();
            }, "__set-item-count!"));

            interpreter.DefineGlobal(Symbol.FromString("__injury-severity"), new NativeProcedure(args =>
            {
                return gameState.Team.Injury.Severity;
            }, "__injury-severity"));

            // 档位以序号回给内容层，符号名在 engine.scm 侧映射——与 __relation-band-index 同一惯例。
            interpreter.DefineGlobal(Symbol.FromString("__injury-band-index"), new NativeProcedure(args =>
            {
                return (int)gameState.Team.Injury.Band;
            }, "__injury-band-index"));

            interpreter.DefineGlobal(Symbol.FromString("__hospitalization-pending?"), new NativeProcedure(args =>
            {
                if (args.Count != 0)
                    throw new ArgumentException("__hospitalization-pending? takes no arguments");
                return gameState.HasPendingHospitalization;
            }, "__hospitalization-pending?"));

            // 疤痕只读：内容层不能发疤，也不能抹疤——疤只由「倒下」这一件事产生（见 ScarSet）。
            // 不带参数是身上疤的总数，带部位名（"手"/"头"/"眼"/"脸"）是那一处的道数。
            interpreter.DefineGlobal(Symbol.FromString("__scar-count"), new NativeProcedure(args =>
            {
                if (args.Count == 0) return gameState.Team.Scars.Count;
                if (args[0] is not string part)
                    throw new ArgumentException("__scar-count takes an optional body part string");
                return gameState.Team.Scars.CountAt(part);
            }, "__scar-count"));

            interpreter.DefineGlobal(Symbol.FromString("__injure!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__injure! requires 1 argument: amount");
                int amount = SchemeValue.ToInt(args[0]);
                if (amount <= 0) throw new ArgumentException("injury amount must be positive");
                var injury = gameState.Team.Injury;
                int before = injury.Severity;
                gameState.Team.Injure(amount);
                // 倒下会立刻完成医疗数值结算并把伤势归零；结果条仍应报告这一下把刻度顶满，
                // 不能因最终数值变小而谎报成“伤势恢复”。
                int reportedDelta = before + amount >= Injury.MaxSeverity
                    ? Injury.MaxSeverity - before
                    : injury.Severity - before;
                ReportInjuryChange(gameState, reportedDelta);
                if (injury.Severity > before)
                    gameState.CurrentActionReport?.AddNote(
                        injury.Band == InjuryBand.Severe
                            ? $"{injury.Part}上的伤进了重伤：少一颗行动骰。"
                            : before == 0
                                ? $"伤在{injury.Part}上：{injury.SkillName}判定 {injury.SkillPenalty}。"
                                : $"{injury.Part}上的伤又重了：{injury.SkillName}判定 {injury.SkillPenalty}。");
                return new None();
            }, "__injure!"));

            interpreter.DefineGlobal(Symbol.FromString("__heal-injury!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__heal-injury! requires 1 argument: amount");
                int amount = SchemeValue.ToInt(args[0]);
                if (amount <= 0) throw new ArgumentException("heal amount must be positive");
                int before = gameState.Team.Injury.Severity;
                gameState.Team.HealInjury(amount);
                ReportInjuryChange(gameState, gameState.Team.Injury.Severity - before);
                return new None();
            }, "__heal-injury!"));

            interpreter.DefineGlobal(Symbol.FromString("__fail-game!"), new NativeProcedure(args =>
            {
                if (args.Count != 2 || !(args[0] is string title) || !(args[1] is string description))
                    throw new ArgumentException("__fail-game! requires title and description strings");
                gameState.FailGame(title, description);
                return new None();
            }, "__fail-game!"));

            interpreter.DefineGlobal(Symbol.FromString("__register-rest-block!"), new NativeProcedure(args =>
            {
                if (args.Count != 4 || args[0] is not string id || args[1] is not string reason
                    || args[2] is not string locationName || args[3] is not string targetNodeName)
                    throw new ArgumentException("__register-rest-block! requires id, reason, location, and target node strings");
                gameState.RegisterRestBlocker(id, reason, locationName, targetNodeName);
                return new None();
            }, "__register-rest-block!"));

            interpreter.DefineGlobal(Symbol.FromString("__release-rest-block!"), new NativeProcedure(args =>
            {
                if (args.Count != 1 || args[0] is not string id)
                    throw new ArgumentException("__release-rest-block! requires one id string");
                gameState.ReleaseRestBlocker(id);
                return new None();
            }, "__release-rest-block!"));

            interpreter.DefineGlobal(Symbol.FromString("__clear-rest-blockers!"), new NativeProcedure(args =>
            {
                if (args.Count != 0)
                    throw new ArgumentException("__clear-rest-blockers! takes no arguments");
                gameState.ClearRestBlockers();
                return new None();
            }, "__clear-rest-blockers!"));

            // 声望档位：读 relation:<faction> 的当前整数值，按 RelationScale 折算成档位序号（0..5）。
            interpreter.DefineGlobal(Symbol.FromString("__relation-band-index"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__relation-band-index requires 1 argument: faction");
                string faction = SchemeValue.AsId(args[0]);
                return RelationScale.BandIndex(gameState.Get<int>("relation:" + faction));
            }, "__relation-band-index"));

            interpreter.DefineGlobal(Symbol.FromString("__change-faction-relation!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__change-faction-relation! requires 2 arguments: faction and delta");
                string faction = SchemeValue.AsId(args[0]);
                if (Array.IndexOf(GameState.Circles, faction) < 0)
                    throw new ArgumentException($"unknown circle '{faction}'");
                int requestedDelta = SchemeValue.ToInt(args[1]);
                string key = "relation:" + faction;
                int before = gameState.Get<int>(key);
                int after = Math.Clamp(before + requestedDelta, RelationScale.Min, RelationScale.Max);
                gameState.Set(key, after);
                int actualDelta = after - before;
                gameState.CurrentActionReport?.AddEffect(
                    ActionEffectKind.Relation, faction + "声誉", actualDelta,
                    actualDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                return new None();
            }, "__change-faction-relation!"));

            interpreter.DefineGlobal(Symbol.FromString("__growth-level"), new NativeProcedure(args =>
            {
                return gameState.Team.GrowthLevel;
            }, "__growth-level"));

            interpreter.DefineGlobal(Symbol.FromString("__set-growth-level!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__set-growth-level! requires 1 argument");
                int n = SchemeValue.ToInt(args[0]);
                if (n < 0) throw new ArgumentException("growth level cannot be negative");
                int before = gameState.Team.GrowthLevel;
                gameState.Team.GrowthLevel = n;
                int delta = n - before;
                gameState.CurrentActionReport?.AddEffect(
                    ActionEffectKind.Growth, "成长", delta,
                    delta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                return new None();
            }, "__set-growth-level!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-composure"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__actor-composure requires 1 argument: actor-id");
                string actorId = SchemeValue.AsId(args[0]);
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                return actor.Composure;
            }, "__actor-composure"));

            interpreter.DefineGlobal(Symbol.FromString("__set-actor-composure!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-actor-composure! requires 2 arguments: actor-id and composure");
                string actorId = SchemeValue.AsId(args[0]);
                int n = SchemeValue.ToInt(args[1]);
                if (n < 0) throw new ArgumentException("composure cannot be negative");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                int composureBefore = actor.Composure;
                int injuryBefore = gameState.Team.Injury.Severity;
                gameState.Team.SetActorComposureSafe(actorId, n);
                int composureDelta = actor.Composure - composureBefore;
                string composureLabel = gameState.CurrentContext?.ActorId == actorId ? "冷静" : actor.Name + "冷静";
                gameState.CurrentActionReport?.AddEffect(
                    ActionEffectKind.Composure, composureLabel, composureDelta,
                    composureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                int injuryDelta = gameState.Team.Injury.Severity - injuryBefore;
                ReportInjuryChange(gameState, injuryDelta < 0 && injuryBefore > 0
                    ? Injury.MaxSeverity - injuryBefore
                    : injuryDelta);
                return new None();
            }, "__set-actor-composure!"));

            interpreter.DefineGlobal(Symbol.FromString("__spend-actor-composure!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__spend-actor-composure! requires 2 arguments: actor-id and amount");
                string actorId = SchemeValue.AsId(args[0]);
                int amount = SchemeValue.ToInt(args[1]);
                if (amount < 0) throw new ArgumentException("composure spend cannot be negative");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");

                int composureBefore = actor.Composure;
                int injuryBefore = gameState.Team.Injury.Severity;
                gameState.Team.SpendComposure(actorId, amount);
                int composureDelta = actor.Composure - composureBefore;
                string composureLabel = gameState.CurrentContext?.ActorId == actorId ? "冷静" : actor.Name + "冷静";
                gameState.CurrentActionReport?.AddEffect(
                    ActionEffectKind.Composure, composureLabel, composureDelta,
                    composureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);

                int injuryDelta = gameState.Team.Injury.Severity - injuryBefore;
                bool collapsed = injuryDelta < 0 && injuryBefore > 0;
                ReportInjuryChange(gameState, collapsed
                    ? Injury.MaxSeverity - injuryBefore
                    : injuryDelta);
                if (injuryDelta > 0 || collapsed)
                    gameState.CurrentActionReport?.AddNote("冷静击穿：你的手在抖，身体先一步承受了代价。");
                return new None();
            }, "__spend-actor-composure!"));

            interpreter.DefineGlobal(Symbol.FromString("__apply-hangover!"), new NativeProcedure(args =>
            {
                gameState.Team.ApplyHangover();
                gameState.CurrentActionReport?.AddNote("酒劲会留到下一次城市骰池：一格会带宿醉降质。");
                return new None();
            }, "__apply-hangover!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-status"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__actor-status requires 1 argument: actor-id");
                string actorId = SchemeValue.AsId(args[0]);
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                return Symbol.FromString(actor.Status);
            }, "__actor-status"));

            interpreter.DefineGlobal(Symbol.FromString("__set-actor-status!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__set-actor-status! requires 2 arguments: actor-id and status");
                string actorId = SchemeValue.AsId(args[0]);
                string status = SchemeValue.AsId(args[1]);
                if (status != "active" && status != "away") throw new ArgumentException("status must be 'active or 'away");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                actor.Status = status;
                // Trigger OnTeamChanged
                gameState.Team.SpendComposure(actorId, 0);
                return new None();
            }, "__set-actor-status!"));

            interpreter.DefineGlobal(Symbol.FromString("__actor-stat"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__actor-stat requires 2 arguments: actor-id and stat-name");
                string actorId = SchemeValue.AsId(args[0]);
                string statName = SchemeValue.AsId(args[1]);
                
                string normalizedStat = statName.ToLowerInvariant();
                if (normalizedStat != "violence" && normalizedStat != "knowledge" && normalizedStat != "sharpness" && normalizedStat != "social")
                    throw new ArgumentException("statName must be violence/knowledge/sharpness/social");
                var actor = gameState.Team.FindActor(actorId);
                if (actor == null) throw new ArgumentException($"actor '{actorId}' not found");
                if (!actor.Stats.TryGetValue(normalizedStat, out var val))
                    throw new InvalidOperationException($"actor '{actorId}' is missing required stat '{normalizedStat}'");
                return val;
            }, "__actor-stat"));

            interpreter.DefineGlobal(Symbol.FromString("__recruit-companion!"), new NativeProcedure(args =>
            {
                if (args.Count < 3)
                    throw new ArgumentException("__recruit-companion! requires id, name, and stats alist");
                string actorId = SchemeValue.AsId(args[0]);
                string name = args[1] as string
                    ?? throw new ArgumentException("companion name must be a string");
                if (!(args[2] is List<object> rawStats))
                    throw new ArgumentException("companion stats must be an alist");

                var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (object rawEntry in rawStats)
                {
                    if (!(rawEntry is List<object> entry) || entry.Count != 2)
                        throw new ArgumentException("each companion stat entry must contain id and value");
                    string statId = SchemeValue.AsId(entry[0]);
                    if (stats.ContainsKey(statId))
                        throw new ArgumentException($"duplicate companion stat '{statId}'");
                    stats.Add(statId, SchemeValue.ToInt(entry[1]));
                }

                gameState.Team.RecruitCompanion(actorId, name, stats);
                gameState.NotificationCenter.Push($"{name}加入队伍", NotificationKind.Info);
                return new None();
            }, "__recruit-companion!"));

            interpreter.DefineGlobal(Symbol.FromString("__dismiss-companion!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__dismiss-companion! requires actor id");
                string actorId = SchemeValue.AsId(args[0]);
                var actor = gameState.Team.FindActor(actorId)
                    ?? throw new InvalidOperationException($"Actor '{actorId}' is not in the team.");
                string name = actor.Name;
                gameState.Team.DismissCompanion(actorId);
                gameState.NotificationCenter.Push($"{name}离开队伍", NotificationKind.Info);
                return new None();
            }, "__dismiss-companion!"));

            // 定制某个人物的骰池：几颗骰，以及是否恒定点数（0 = 正常掷骰）。
            interpreter.DefineGlobal(Symbol.FromString("__set-actor-die-profile!"), new NativeProcedure(args =>
            {
                if (args.Count < 4)
                    throw new ArgumentException("__set-actor-die-profile! requires actor id, slot count, fixed value (0 = roll), and label");
                string actorId = SchemeValue.AsId(args[0]);
                int slotCount = SchemeValue.ToInt(args[1]);
                int fixedValue = SchemeValue.ToInt(args[2]);
                string label = args[3] as string
                    ?? throw new ArgumentException("fixed die label must be a string");
                gameState.Team.SetActorDieProfile(actorId, slotCount,
                    fixedValue == 0 ? (int?)null : fixedValue, label);
                return new None();
            }, "__set-actor-die-profile!"));

            interpreter.DefineGlobal(Symbol.FromString("__has-companion?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__has-companion? requires actor id");
                string actorId = SchemeValue.AsId(args[0]);
                var actor = gameState.Team.FindActor(actorId);
                return actor != null && actor.Role == "companion";
            }, "__has-companion?"));

            interpreter.DefineGlobal(Symbol.FromString("__set-actor-permanent-die-penalty!"), new NativeProcedure(args =>
            {
                if (args.Count < 3)
                    throw new ArgumentException("__set-actor-permanent-die-penalty! requires actor id, label, and penalty");
                string actorId = SchemeValue.AsId(args[0]);
                string label = args[1] as string
                    ?? throw new ArgumentException("permanent die penalty label must be a string");
                int penalty = SchemeValue.ToInt(args[2]);
                if (penalty > 0 || penalty < -2)
                    throw new ArgumentOutOfRangeException(nameof(args), "permanent die penalty must be between -2 and 0");
                if (penalty != 0 && string.IsNullOrWhiteSpace(label))
                    throw new ArgumentException("permanent die penalty requires a non-empty label");
                var actor = gameState.Team.FindActor(actorId)
                    ?? throw new ArgumentException($"actor '{actorId}' not found");
                actor.PermanentDiePenaltyLabel = penalty == 0 ? string.Empty : label;
                actor.PermanentDiePenalty = penalty;
                return new None();
            }, "__set-actor-permanent-die-penalty!"));

            interpreter.DefineGlobal(Symbol.FromString("__current-actor"), new NativeProcedure(args =>
            {
                if (gameState.CurrentContext == null)
                    throw new InvalidOperationException(
                        "__current-actor is only valid during ExecuteAction. An encounter-result callback " +
                        "or a turn-end rule runs outside any action — address the actor explicitly there, " +
                        "e.g. (spend-actor-composure! 'player n) / (restore-actor-composure! 'player n).");
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

            interpreter.DefineGlobal(Symbol.FromString("__result-note!"), new NativeProcedure(args =>
            {
                if (args.Count < 1 || !(args[0] is string text) || string.IsNullOrWhiteSpace(text))
                    throw new ArgumentException("__result-note! requires 1 non-empty string");
                if (gameState.CurrentActionReport == null)
                    throw new InvalidOperationException("result-note! can only be used while executing an action");
                gameState.CurrentActionReport.AddNote(text);
                return new None();
            }, "__result-note!"));

            interpreter.DefineGlobal(Symbol.FromString("__record-clock-effect!"), new NativeProcedure(args =>
            {
                if (args.Count < 2 || !(args[0] is string label))
                    throw new ArgumentException("__record-clock-effect! requires label and delta");
                int delta = Convert.ToInt32(args[1]);
                gameState.CurrentActionReport?.AddClockEffect(label, delta);
                return new None();
            }, "__record-clock-effect!"));

            interpreter.DefineGlobal(Symbol.FromString("__play-narration!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__play-narration! requires 1 argument: id");
                if (!(args[0] is string id)) throw new ArgumentException("__play-narration! id must be a string");
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.NarrationIds.Add(id);
                else
                    gameState.NarrationCenter.Play(id);
                return new None();
            }, "__play-narration!"));

            interpreter.DefineGlobal(Symbol.FromString("__spotlight!"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("__spotlight! requires 2 arguments: title subtitle");
                if (!(args[0] is string title)) throw new ArgumentException("__spotlight! title must be a string");
                if (!(args[1] is string subtitle)) throw new ArgumentException("__spotlight! subtitle must be a string");
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.BlockingStorySteps.Add(
                        BlockingStoryStep.ForSpotlight(new SpotlightCard { Title = title, Subtitle = subtitle }));
                else
                    gameState.SpotlightCenter.Show(title, subtitle);
                return new None();
            }, "__spotlight!"));

            // 阻塞对话:动作内排入有序剧情步骤(adopt 前播放),动作外即时广播。
            interpreter.DefineGlobal(Symbol.FromString("__play-dialogue!"), new NativeProcedure(args =>
            {
                var sequence = ParseDialogueSequence(args, "__play-dialogue!");
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.BlockingStorySteps.Add(BlockingStoryStep.ForDialogue(sequence));
                else
                    gameState.DialogueCenter.RequestDialogue(sequence);
                return new None();
            }, "__play-dialogue!"));

            // 非阻塞插话:动作内延迟到 adopt 之后释放,动作外即时广播。
            interpreter.DefineGlobal(Symbol.FromString("__play-banter!"), new NativeProcedure(args =>
            {
                var sequence = ParseDialogueSequence(args, "__play-banter!");
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.Banter.Add(sequence);
                else
                    gameState.DialogueCenter.RequestBanter(sequence);
                return new None();
            }, "__play-banter!"));

            // 阻塞对话(场外):允许未在场的说话人以临时侧边卡为锚点。
            interpreter.DefineGlobal(Symbol.FromString("__play-remote-dialogue!"), new NativeProcedure(args =>
            {
                var sequence = ParseDialogueSequence(args, "__play-remote-dialogue!", allowsRemoteParticipants: true);
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.BlockingStorySteps.Add(BlockingStoryStep.ForDialogue(sequence));
                else
                    gameState.DialogueCenter.RequestDialogue(sequence);
                return new None();
            }, "__play-remote-dialogue!"));

            // 显式场外插话:允许未在场的说话人以临时侧边卡为锚点。
            // 不复用普通 banter 的静默兜底，保留后者对内容拼写/节点配置的严格校验。
            interpreter.DefineGlobal(Symbol.FromString("__play-remote-banter!"), new NativeProcedure(args =>
            {
                var sequence = ParseDialogueSequence(args, "__play-remote-banter!", allowsRemoteParticipants: true);
                if (gameState.CurrentActionReport != null)
                    gameState.CurrentActionReport.Banter.Add(sequence);
                else
                    gameState.DialogueCenter.RequestBanter(sequence);
                return new None();
            }, "__play-remote-banter!"));

            // 命名动画:v1 仅携带 tag,作为有序阻塞剧情步骤(前端占位播放)。
            // 交锋入场钩子也可调用；Debug 直载没有 ActionReport 时不播放、不报错。
            interpreter.DefineGlobal(Symbol.FromString("__play-animation!"), new NativeProcedure(args =>
            {
                if (args.Count < 1 || !(args[0] is string tag) || string.IsNullOrWhiteSpace(tag))
                    throw new ArgumentException("__play-animation! requires 1 argument: a non-empty tag string");
                if (gameState.CurrentActionReport == null)
                    return new None();
                gameState.CurrentActionReport.BlockingStorySteps.Add(BlockingStoryStep.ForAnimation(tag));
                return new None();
            }, "__play-animation!"));

            // --- Existing Native Procedures ---
            interpreter.DefineGlobal(Symbol.FromString("get-global"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("get-global requires 1 argument: key symbol or string");

                string key = SchemeValue.AsId(args[0]);
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("get-global key cannot be null or empty");

                return gameState.Get<object>(key) ?? false;
            }, "get-global"));

            interpreter.DefineGlobal(Symbol.FromString("set-global!"), new NativeProcedure(args =>
            {
                if (args.Count < 2)
                    throw new ArgumentException("set-global! requires 2 arguments: key symbol or string and value");

                string key = SchemeValue.AsId(args[0]);
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
                int divisor = SchemeValue.ToInt(args[1]);
                if (divisor == 0) throw new DivideByZeroException("quotient divisor cannot be zero");
                return SchemeValue.ToInt(args[0]) / divisor;
            }, "quotient"));

            interpreter.DefineGlobal(Symbol.FromString("remainder"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("remainder requires 2 arguments");
                int divisor = SchemeValue.ToInt(args[1]);
                if (divisor == 0) throw new DivideByZeroException("remainder divisor cannot be zero");
                return SchemeValue.ToInt(args[0]) % divisor;
            }, "remainder"));

            interpreter.DefineGlobal(Symbol.FromString("modulo"), new NativeProcedure(args =>
            {
                if (args.Count < 2) throw new ArgumentException("modulo requires 2 arguments");
                int dividend = SchemeValue.ToInt(args[0]);
                int divisor = SchemeValue.ToInt(args[1]);
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
                return SchemeValue.ToInt(args[0]) == 0;
            }, "zero?"));

            interpreter.DefineGlobal(Symbol.FromString("even?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("even? requires 1 argument");
                return SchemeValue.ToInt(args[0]) % 2 == 0;
            }, "even?"));

            interpreter.DefineGlobal(Symbol.FromString("odd?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("odd? requires 1 argument");
                return SchemeValue.ToInt(args[0]) % 2 != 0;
            }, "odd?"));

            var rand = GameRandom.Instance;
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

        // 把 Scheme 端 (list (line speaker text [voice] [dwell]) ...) 解析成 DialogueSequence。
        // 内容/配置错误一律直接抛出,尽早暴露。
        private static DialogueSequence ParseDialogueSequence(
            IList<object> args,
            string who,
            bool allowsRemoteParticipants = false)
        {
            if (args.Count < 1 || !(args[0] is List<object> rawLines))
                throw new ArgumentException($"{who} requires a list of lines: (line speaker text ...)");
            if (rawLines.Count == 0)
                throw new ArgumentException($"{who} requires at least one line");

            var lines = new List<DialogueLine>(rawLines.Count);
            foreach (var entry in rawLines)
            {
                if (!(entry is List<object> parts) || parts.Count < 2)
                    throw new ArgumentException($"{who}: each line must be (line speaker text [voice] [dwell])");
                if (!(parts[0] is string speaker) || string.IsNullOrWhiteSpace(speaker))
                    throw new ArgumentException($"{who}: line speaker must be a non-empty string");
                if (!(parts[1] is string text))
                    throw new ArgumentException($"{who}: line text must be a string");

                string? voice = parts.Count > 2 && parts[2] is string v && v.Length > 0 ? v : null;
                float dwell = parts.Count > 3 ? Convert.ToSingle(parts[3]) : 0f;
                if (dwell < 0f)
                    throw new ArgumentException($"{who}: dwell seconds cannot be negative");

                lines.Add(new DialogueLine { Speaker = speaker, Text = text, VoiceId = voice, DwellSeconds = dwell });
            }
            return new DialogueSequence(lines, allowsRemoteParticipants);
        }

        // 伤势的正负与其他资源相反：刻度涨上去是坏事，所以 tone 反着挂。
        private static void ReportInjuryChange(GameState gameState, int delta)
        {
            if (delta == 0) return;
            gameState.CurrentActionReport?.AddEffect(
                ActionEffectKind.Injury, "伤势", delta,
                delta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
        }
    }
}
