#nullable enable
using System;
using System.Collections.Generic;
using Schemy;
using SSNoir.Core;

namespace SSNoir.Scripting
{
    public static class NativeFunctions
    {
        /// <summary>同伴技能表：((violence 1) (knowledge 2) ...) 四项齐全，重复报错。</summary>
        public static Dictionary<string, int> ParseCompanionStats(object raw)
        {
            if (!(raw is List<object> rawStats))
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
            return stats;
        }

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
                    gameState.CurrentActionReport?.AddSupplement(
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
                    gameState.CurrentActionReport?.AddSupplement("冷静击穿：你的手在抖，身体先一步承受了代价。");
                return new None();
            }, "__spend-actor-composure!"));

            interpreter.DefineGlobal(Symbol.FromString("__apply-hangover!"), new NativeProcedure(args =>
            {
                gameState.Team.ApplyHangover();
                gameState.CurrentActionReport?.AddSupplement("酒劲会留到下一次城市骰池：一格会带宿醉降质。");
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
                var stats = ParseCompanionStats(args[2]);
                gameState.Team.RecruitCompanion(actorId, name, stats);
                gameState.NotificationCenter.Push($"{name}加入队伍", NotificationKind.Info);
                return new None();
            }, "__recruit-companion!"));

            // __summon-helper!（支援叫来的临时帮手）只能在交锋里用，所以注册在 SceneManager 那头。

            // ── 关系支援 ────────────────────────────────────
            interpreter.DefineGlobal(Symbol.FromString("__grant-support!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__grant-support! requires a support id");
                string id = args[0] as string ?? throw new ArgumentException("support id must be a string");
                gameState.Team.GrantSupport(id);
                return new None();
            }, "__grant-support!"));

            interpreter.DefineGlobal(Symbol.FromString("__has-support?"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__has-support? requires a support id");
                string id = args[0] as string ?? throw new ArgumentException("support id must be a string");
                return gameState.Team.HasSupport(id);
            }, "__has-support?"));

            interpreter.DefineGlobal(Symbol.FromString("__set-carried-support!"), new NativeProcedure(args =>
            {
                if (args.Count < 1) throw new ArgumentException("__set-carried-support! requires a support id");
                string id = args[0] as string ?? throw new ArgumentException("support id must be a string");
                gameState.Team.SetCarriedSupport(id);
                return new None();
            }, "__set-carried-support!"));

            interpreter.DefineGlobal(Symbol.FromString("__carried-support"), new NativeProcedure(args =>
            {
                string id = gameState.Team.CarriedSupport;
                return id.Length == 0 ? (object)false : id;
            }, "__carried-support"));

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

            interpreter.DefineGlobal(Symbol.FromString("__result-supplement!"), new NativeProcedure(args =>
            {
                if (args.Count < 1 || !(args[0] is string text) || string.IsNullOrWhiteSpace(text))
                    throw new ArgumentException("__result-supplement! requires 1 non-empty string");
                if (gameState.CurrentActionReport == null)
                    throw new InvalidOperationException("result-supplement! can only be used while executing an action");
                gameState.CurrentActionReport.AddSupplement(text);
                return new None();
            }, "__result-supplement!"));

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
            interpreter.DefineGlobal(Symbol.FromString("__play-video!"), new NativeProcedure(args =>
            {
                if (args.Count < 1 || !(args[0] is string tag) || string.IsNullOrWhiteSpace(tag))
                    throw new ArgumentException("__play-video! requires 1 argument: a non-empty tag string");
                if (gameState.CurrentActionReport == null)
                    return new None();
                gameState.CurrentActionReport.BlockingStorySteps.Add(BlockingStoryStep.ForVideo(tag));
                return new None();
            }, "__play-video!"));

            // 场景演出：道具的实时动画作为阻塞步骤（机位 → 播过渡 → 回来）。没有 ActionReport 时不播、不报错。
            interpreter.DefineGlobal(Symbol.FromString("__play-motion!"), new NativeProcedure(args =>
            {
                if (args.Count < 2 || !(args[0] is string prop) || !(args[1] is string state)
                    || string.IsNullOrWhiteSpace(prop) || string.IsNullOrWhiteSpace(state))
                    throw new ArgumentException("__play-motion! requires: prop-name state-name [camera-name]");
                string camera = args.Count >= 3 && args[2] is string c ? c : string.Empty;
                if (gameState.CurrentActionReport == null)
                    return new None();
                gameState.CurrentActionReport.BlockingStorySteps.Add(BlockingStoryStep.ForMotion(prop, state, camera));
                return new None();
            }, "__play-motion!"));

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

                var stage = parts.Count > 4 ? ParseStageCue(parts[4], who, speaker == "世界") : DialogueStageCue.None;

                lines.Add(new DialogueLine { Speaker = speaker, Text = text, VoiceId = voice, DwellSeconds = dwell, Stage = stage });
            }
            return new DialogueSequence(lines, allowsRemoteParticipants);
        }

        // 舞台指示以 (key value key value ...) 的平铺表到达；key 是 :pose 这类符号。
        // 未知键、非法档位都直接报错——内容里的拼写错误不能变成「没动」。
        private static readonly HashSet<string> LightStates = new HashSet<string> { "normal", "surge", "faint", "ember" };
        private static readonly HashSet<string> CurrentStates = new HashSet<string> { "still", "pulse", "racing" };
        private static readonly HashSet<string> LightEvents = new HashSet<string> { "flicker", "relight", "blackout" };
        private static readonly HashSet<string> MoveKinds = new HashSet<string> { "in", "back" };

        private static DialogueStageCue ParseStageCue(object raw, string who, bool isNarration)
        {
            if (!(raw is List<object> plist))
                throw new ArgumentException($"{who}: stage cue must be a flat key/value list");
            if (plist.Count == 0)
                return DialogueStageCue.None;

            // :other 是个分隔符，不带值：它后面的人物指示落到对方身上。
            int split = -1;
            for (int i = 0; i < plist.Count; i++)
            {
                if (plist[i] is string) continue;
                if (SchemeValue.AsId(plist[i]).TrimStart(':') != "other") continue;
                if (split >= 0) throw new ArgumentException($"{who}: :other can only appear once in a line");
                if (isNarration) throw new ArgumentException($"{who}: narration has no :other — nobody is speaking");
                split = i;
            }
            if (split < 0)
                return ParseStageCueWords(plist, 0, plist.Count, who, forOther: false, other: null);
            var other = ParseStageCueWords(plist, split + 1, plist.Count, who, forOther: true, other: null);
            return ParseStageCueWords(plist, 0, split, who, forOther: false, other: other);
        }

        private static DialogueStageCue ParseStageCueWords(
            List<object> plist, int start, int end, string who, bool forOther, DialogueStageCue? other)
        {
            if ((end - start) % 2 != 0)
                throw new ArgumentException($"{who}: stage cue has a key without a value");

            string? pose = null, move = null, light = null, current = null, screen = null;
            bool shake = false, flicker = false, relight = false, blackout = false, flash = false, inner = false;
            for (int i = start; i < end; i += 2)
            {
                string key = SchemeValue.AsId(plist[i]).TrimStart(':');
                object value = plist[i + 1];
                switch (key)
                {
                    case "pose":
                        pose = value as string;
                        if (string.IsNullOrWhiteSpace(pose))
                            throw new ArgumentException($"{who}: :pose must be a non-empty string");
                        break;
                    case "move":
                        move = SchemeValue.AsId(value);
                        if (!MoveKinds.Contains(move))
                            throw new ArgumentException($"{who}: :move must be in/back, got '{move}'");
                        break;
                    case "light":
                    {
                        string word = SchemeValue.AsId(value);
                        if (LightStates.Contains(word)) light = word;
                        else if (CurrentStates.Contains(word)) current = word;
                        else if (word == "flicker") flicker = true;
                        else if (word == "relight") relight = true;
                        else if (word == "blackout" && !forOther) blackout = true;
                        else throw new ArgumentException($"{who}: :light must be normal/surge/faint/ember, still/pulse/racing, or flicker/relight/blackout; got '{word}'");
                        break;
                    }
                    case "shake":
                        shake = !(value is bool b1) || b1;
                        break;
                    case "screen" when !forOther:
                    {
                        string word = SchemeValue.AsId(value);
                        if (word == "normal" || word == "negative") screen = word;
                        else if (word == "flash") flash = true;
                        else throw new ArgumentException($"{who}: :screen must be normal/negative/flash, got '{word}'");
                        break;
                    }
                    case "inner" when !forOther:
                        inner = !(value is bool b3) || b3;
                        break;
                    default:
                        throw new ArgumentException(forOther
                            ? $"{who}: after :other only pose/move/light/shake apply to the other person; got '{key}'"
                            : $"{who}: unknown stage cue key '{key}'");
                }
            }
            return new DialogueStageCue
            {
                Pose = pose, Move = move, Light = light, Current = current,
                Flicker = flicker, Relight = relight, Blackout = blackout, Shake = shake,
                Screen = screen, Flash = flash, Inner = inner, Other = other,
            };
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
