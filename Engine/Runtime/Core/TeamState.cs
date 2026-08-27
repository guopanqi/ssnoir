#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class TeamState
    {
        public const int MinStatLevel = -1;
        public const int MaxStatLevel = 4;

        // 冷静是纯缓冲，本身没有档位效果：花到 0 之前不产生任何惩罚，
        // 0 之后消耗一比一转成伤势——它是身体前面的一层垫子，垫子用完，代价照原价打在身上。
        // 失态/失控两档已删除——它们和轻伤/重伤同构（都是"随机挑一个东西 −1"），
        // 让玩家要学两套同样的惩罚语言。机械效果现在只由伤势一处承担。
        //
        // 5 点，而且**睡觉只回 2 点**：这条轴要跨天，不能一觉抹平。
        // 一天四颗骰，顺的一天大约掉 2（一次坏结果），糟的一天掉 4 以上；
        // 于是平稳的日子刚好打平，连着硬干就一天比一天低，必须主动花一颗骰去散步
        // 或者花钱喝一杯把它拉回来——"恢复"这件事因此才存在。
        //
        // 代价的刻度（内容层照这个写，别再全部写 1）：
        //   中结果的小磨损 1 ／ 坏结果 2 ／ 高风险工作与交锋里重的那种 3
        //   交锋每回合自动流失 1（见 SceneManager.EncounterTurnComposureCost）
        // 一比一意味着这些数字就是击穿之后的伤害，卡上写几点就是几点——不要再往中间塞换算率。
        // 交锋因此有一条可以数出来的时间表：满冷静五个免费回合，之后每回合 1 点伤，第 11 回合倒下。
        public const int MaxComposure = 5;

        /// <summary>队伍唯一的身体轴，取代旧的健康血条。规则与档位见 <see cref="Core.Injury"/>。</summary>
        public Injury Injury { get; } = new Injury();

        /// <summary>倒下留下的永久疤痕。治不好、不进伤势刻度，规则见 <see cref="Core.ScarSet"/>。</summary>
        public ScarSet Scars { get; } = new ScarSet();

        /// <summary>伤势撞到倒下线，等待 GameState 结算送医（扣钱、作废当天骰子）。</summary>
        public bool PendingCollapse { get; private set; }

        public int GrowthLevel { get; set; } = 0;
        // 骰池位置稳定存在；身体状态附着在位置上，而非可变骰子列表下标。
        // 主角承担城市与交锋的主要行动，同伴只在城市中提供一枚额外行动骰。
        public const int ProtagonistActionSlotCount = 4;
        public const int CompanionActionSlotCount = 1;
        public const int CompanionComposurePerActionSlot = 2;

        /// <summary>入队时的默认骰位数。个别人物可以带自己的数字，见 ActorState.ActionSlotCount。</summary>
        public static int GetDefaultActionSlotCount(string role) => role switch
        {
            "protagonist" => ProtagonistActionSlotCount,
            "companion" => CompanionActionSlotCount,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown actor role.")
        };

        /// <summary>本场是否登场：在队里、还站得住，就发骰子。交锋与城市用同一条规则——
        /// 谁能出手由「他在不在队里」决定，不由场景类型决定。要限制某一场的阵容，
        /// 就在那一场里决定谁入队，而不是在发骰的地方加分支。</summary>
        public static bool IsOnStage(ActorState actor, bool isInEncounter)
            => actor.Status == "active";

        public int GetAvailableGrowthPoints(ActorState actor)
        {
            return Math.Max(0, GrowthLevel - actor.SpentGrowthPoints);
        }

        /// <summary>能力从当前等级升一级的成长点费用：-1→0 与 0→1 都花 1，之后依次花 2、3、4。</summary>
        public static int GetStatUpgradeCost(int currentLevel)
        {
            if (currentLevel < MinStatLevel || currentLevel > MaxStatLevel)
                throw new ArgumentOutOfRangeException(nameof(currentLevel),
                    $"Stat level must be between {MinStatLevel} and {MaxStatLevel}.");
            return currentLevel >= MaxStatLevel ? 0 : Math.Max(1, currentLevel + 1);
        }

        public List<ActorState> Actors { get; } = new List<ActorState>();

        public event Action? OnTeamChanged;

        public void ResetForNewGame()
        {
            Injury.Reset();
            Scars.Clear();
            PendingCollapse = false;
            GrowthLevel = 0;
            Actors.Clear();
            OnTeamChanged?.Invoke();
        }

        // ── 伤势 ────────────────────────────────────────
        // 受伤只有一个入口：身上没伤时随机落一处，已有伤则加深同一处。撞到倒下线时只
        // 竖起 PendingCollapse，实际的送医结算（钱、关系、作废骰子）由 GameState 完成——
        // TeamState 不认识钱包和声望。
        public void Injure(int amount)
        {
            AggravateInjury(amount);
            OnTeamChanged?.Invoke();
        }

        public void HealInjury(int amount)
        {
            Injury.Heal(amount);
            OnTeamChanged?.Invoke();
        }

        private void AggravateInjury(int amount)
        {
            if (Injury.Aggravate(amount))
                PendingCollapse = true;
        }

        /// <summary>
        /// 倒下：当天剩余骰子作废，伤势与冷静归零，并在当时受伤的那个部位永久留下一道疤。
        /// 疤是这件事唯一带不走的代价——钱能再赚，伤能养好，这一条跟到结局。
        /// 由 GameState 在扣完治疗费后调用，day 用于把这道疤钉在世界日历上。
        /// </summary>
        public ScarRecord ResolveCollapse(int day)
        {
            PendingCollapse = false;
            var scar = Scars.Add(Injury.Part, day);
            Injury.ResolveCollapse();
            var player = FindActor("player") ?? throw new InvalidOperationException("Protagonist is missing from the team.");
            player.ActionDice.Clear();
            player.ActionDiceSlotIds.Clear();
            // 送到医院后身体伤势和冷静都清零；能带出医院的永久代价只有这道疤。
            player.Composure = 0;
            OnTeamChanged?.Invoke();
            return scar;
        }

        public ActorState? FindActor(string actorId)
        {
            return Actors.Find(a => a.Id.Equals(actorId, StringComparison.OrdinalIgnoreCase));
        }

        public ActorState RecruitCompanion(string actorId, string name, IReadOnlyDictionary<string, int> stats)
        {
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Companion id and name must be non-empty.");
            if (FindActor(actorId) != null)
                throw new InvalidOperationException($"Actor '{actorId}' is already in the team.");

            string[] requiredStats = { "violence", "knowledge", "sharpness", "social" };
            if (stats.Count != requiredStats.Length)
                throw new ArgumentException("Companion stats must define exactly violence, knowledge, sharpness, and social.");

            var actor = new ActorState
            {
                Id = actorId,
                Name = name,
                Role = "companion",
                Status = "active",
                ActionSlotCount = CompanionActionSlotCount,
                Composure = CompanionActionSlotCount * CompanionComposurePerActionSlot,
            };
            foreach (string statId in requiredStats)
            {
                if (!stats.TryGetValue(statId, out int value))
                    throw new ArgumentException($"Companion stats are missing '{statId}'.");
                if (value < MinStatLevel || value > MaxStatLevel)
                    throw new ArgumentOutOfRangeException(nameof(stats),
                        $"Companion stat '{statId}' must be between {MinStatLevel} and {MaxStatLevel}.");
                actor.Stats[statId] = value;
            }

            Actors.Add(actor);
            OnTeamChanged?.Invoke();
            return actor;
        }

        /// <summary>
        /// 城市里新的一天：同伴的冷静回满，昨天走掉的今天回来；返回真的被接回来的人的名字。
        ///
        /// 同伴的冷静和主角的不是一回事。主角那条必须跨天——它是这个游戏里"恢复"的载体
        /// （见 <see cref="MaxComposure"/> 的注释）。同伴那条是"这个人今天还能陪你多久"，
        /// 和行动骰一样按天重发：她掉到 0 就是今天不奉陪了，睡一觉明天照旧。
        /// 让它跨天等于要玩家去照顾一个自己控制不了的人的状态条，而那条状态条
        /// 没有任何可以投入的动作。
        ///
        /// 交锋的回合之间不走这里：一场之内耗光了就是耗光了。
        /// </summary>
        public List<string> BeginCityDay()
        {
            var returned = new List<string>();
            foreach (var actor in Actors)
            {
                if (actor.Role != "companion") continue;
                if (actor.Status == "away") returned.Add(actor.Name);
                actor.Composure = actor.MaxComposure;
                actor.Status = "active";
            }
            if (returned.Count > 0)
                OnTeamChanged?.Invoke();
            return returned;
        }

        /// <summary>让同伴离队。一场交锋临时请来的人（弗兰克、林）必须在结算时走这里，
        /// 否则他们的骰子会跟着玩家回到城市——存档时的 HasDefaultDieProfile 校验会抓住这种泄漏。</summary>
        public void DismissCompanion(string actorId)
        {
            var actor = FindActor(actorId)
                ?? throw new InvalidOperationException($"Actor '{actorId}' is not in the team.");
            if (actor.Role != "companion")
                throw new InvalidOperationException($"Actor '{actorId}' is not a companion and cannot leave the team.");
            Actors.Remove(actor);
            OnTeamChanged?.Invoke();
        }

        /// <summary>给某个人物定制骰池：几颗骰、是否恒定点数。恒定点数必须带一个可见标签。</summary>
        public void SetActorDieProfile(string actorId, int slotCount, int? fixedDieValue, string fixedDieLabel)
        {
            var actor = FindActor(actorId)
                ?? throw new InvalidOperationException($"Actor '{actorId}' is not in the team.");
            if (slotCount < 1 || slotCount > ProtagonistActionSlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotCount),
                    $"Action slot count must be between 1 and {ProtagonistActionSlotCount}.");
            if (fixedDieValue != null && (fixedDieValue < 1 || fixedDieValue > 6))
                throw new ArgumentOutOfRangeException(nameof(fixedDieValue), "Fixed die value must be between 1 and 6.");
            if (fixedDieValue != null && string.IsNullOrWhiteSpace(fixedDieLabel))
                throw new ArgumentException("A fixed die must carry a visible label.", nameof(fixedDieLabel));
            bool wasAtFullComposure = actor.Composure == actor.MaxComposure;
            actor.ActionSlotCount = slotCount;
            actor.Composure = wasAtFullComposure ? actor.MaxComposure : actor.Composure;
            actor.FixedDieValue = fixedDieValue;
            actor.FixedDieLabel = fixedDieValue == null ? string.Empty : fixedDieLabel;
            OnTeamChanged?.Invoke();
        }

        public void UpgradeActorStat(string actorId, string statId)
        {
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }
            if (actor.Status != "active")
            {
                throw new InvalidOperationException($"Actor '{actorId}' is not active and cannot upgrade stats.");
            }
            if (!actor.Stats.ContainsKey(statId))
            {
                throw new ArgumentException($"Stat '{statId}' not found on actor '{actorId}'.");
            }
            int currentLevel = actor.Stats[statId];
            if (currentLevel >= MaxStatLevel)
            {
                throw new InvalidOperationException($"Stat '{statId}' on actor '{actorId}' has reached the maximum level {MaxStatLevel}.");
            }
            int cost = GetStatUpgradeCost(currentLevel);
            if (GetAvailableGrowthPoints(actor) < cost)
            {
                throw new InvalidOperationException(
                    $"Actor '{actorId}' needs {cost} growth points to upgrade stat '{statId}'.");
            }

            actor.Stats[statId]++;
            actor.SpentGrowthPoints += cost;
            OnTeamChanged?.Invoke();
        }

        // 花冷静（失败、交锋每回合自动流失）。协作者在 0 点失能离场；主角击穿
        // 0 点后溢出一比一转成伤势——"冷静挡不住子弹"之外的第二个受伤口。
        public void SpendComposure(string actorId, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Composure spend cannot be negative.");
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }

            if (actor.Role == "companion")
            {
                actor.Composure -= amount;
                if (actor.Composure <= 0)
                {
                    actor.Status = "away";
                }
            }
            else if (actor.Role == "protagonist")
            {
                int curComposure = actor.Composure;
                int overflow;
                if (curComposure > 0)
                {
                    actor.Composure = Math.Max(0, curComposure - amount);
                    overflow = amount - curComposure;
                }
                else
                {
                    overflow = amount;
                }
                // 一比一，没有换算率。冷静是身体前面的一层垫子：垫子用完，代价照原价打在身上。
                // 这里曾经折半（补偿冷静上限提高而伤势没跟着放大），那条规则不可加
                // ——在 0 点上挨两次 1 比挨一次 2 更疼——而且让卡面上那个数字不再等于伤害。
                if (overflow > 0)
                    AggravateInjury(overflow);
            }
            OnTeamChanged?.Invoke();
        }

        // 恢复冷静（睡觉、城市恢复动词、烟）。只在满值时把离场协作者接回来。
        public void RestoreComposure(string actorId, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Composure restoration cannot be negative.");
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }

            actor.Composure += amount;
            // 缓过一口气就回来。这里曾要求回满才归队，那在上限 2 的时候等于"喝一杯就回来"，
            // 上限提高之后等于"永远回不来"——同一行代码换了两个意思，所以改成不依赖上限的写法。
            // 真正把人接回来的常规路径是 BeginCityDay：睡一觉，明天照旧。
            if (actor.Role == "companion" && actor.Status == "away" && actor.Composure > 0)
            {
                actor.Status = "active";
            }
            OnTeamChanged?.Invoke();
        }

        public void ApplyHangover()
        {
            // 酒的延期成本固定在一个骰池位置上：今天先看见“宿醉”标签，下一次城市掷骰才兑现。
            var player = FindActor("player") ?? throw new InvalidOperationException("Protagonist is missing from the team.");
            player.HangoverSlotId = 0;
            OnTeamChanged?.Invoke();
        }

        public void SetActorComposureSafe(string actorId, int newComposure)
        {
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }

            int delta = newComposure - actor.Composure;
            if (delta < 0)
            {
                SpendComposure(actorId, -delta);
            }
            else
            {
                RestoreComposure(actorId, delta);
            }
        }

        public TeamSaveData Serialize()
        {
            var data = new TeamSaveData
            {
                InjurySeverity = Injury.Severity,
                InjuryPart     = Injury.Part,
                Scars          = Scars.Serialize(),
                GrowthLevel    = GrowthLevel,
                HasSavedActionDice = true,
            };
            foreach (var actor in Actors)
            {
                // 定制骰池只属于交锋临时请来的人。它出现在存档里，说明某场交锋结算时
                // 忘了让人离队——这是内容错误，当场中断，不要把它写进城市。
                if (!actor.HasDefaultDieProfile)
                    throw new InvalidOperationException(
                        $"Actor '{actor.Id}' still carries an encounter-only die profile; the encounter must dismiss them before returning to the city.");
                data.Actors.Add(new ActorSaveData
                {
                    Id                = actor.Id,
                    Name              = actor.Name,
                    Role              = actor.Role,
                    Status            = actor.Status,
                    Composure         = actor.Composure,
                    HangoverSlotId     = actor.HangoverSlotId,
                    PermanentDiePenaltyLabel = actor.PermanentDiePenaltyLabel,
                    PermanentDiePenalty = actor.PermanentDiePenalty,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats             = new Dictionary<string, int>(actor.Stats),
                    ActionDice        = new List<int>(actor.ActionDice),
                    ActionDiceSlotIds = new List<int>(actor.ActionDiceSlotIds),
                });
            }
            return data;
        }

        public void ApplySaveData(TeamSaveData data)
        {
            Injury.Restore(data.InjurySeverity, data.InjuryPart);
            Scars.Restore(data.Scars);
            PendingCollapse = false;
            GrowthLevel = data.GrowthLevel;

            var savedActorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var actorData in data.Actors)
                savedActorIds.Add(actorData.Id);
            Actors.RemoveAll(actor => actor.Role == "companion" && !savedActorIds.Contains(actor.Id));

            foreach (var actorData in data.Actors)
            {
                var actor = FindActor(actorData.Id);
                if (actor == null)
                {
                    if (actorData.Role != "companion")
                        throw new ArgumentException($"Save file references unknown non-companion actor '{actorData.Id}'.");
                    actor = RecruitCompanion(actorData.Id, actorData.Name, actorData.Stats);
                }
                if (!actor.Role.Equals(actorData.Role, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Actor '{actorData.Id}' role does not match the save file.");
                if (actorData.Status != "active" && actorData.Status != "away")
                    throw new ArgumentException($"Actor '{actorData.Id}' has invalid status '{actorData.Status}' in save file.");
                actor.Name              = actorData.Name;
                actor.Status            = actorData.Status;
                actor.Composure         = actorData.Composure;
                actor.HangoverSlotId = actorData.HangoverSlotId;
                if (actorData.PermanentDiePenalty > 0 || actorData.PermanentDiePenalty < -2)
                    throw new ArgumentOutOfRangeException(nameof(data),
                        $"Actor '{actorData.Id}' permanent die penalty must be between -2 and 0.");
                if (actorData.PermanentDiePenalty != 0 && string.IsNullOrWhiteSpace(actorData.PermanentDiePenaltyLabel))
                    throw new ArgumentException($"Actor '{actorData.Id}' permanent die penalty requires a label.");
                actor.PermanentDiePenaltyLabel = actorData.PermanentDiePenaltyLabel ?? string.Empty;
                actor.PermanentDiePenalty = actorData.PermanentDiePenalty;
                actor.SpentGrowthPoints = actorData.SpentGrowthPoints;
                foreach (var kv in actorData.Stats)
                {
                    if (!actor.Stats.ContainsKey(kv.Key))
                        throw new ArgumentException($"Save file contains unknown stat '{kv.Key}' for actor '{actorData.Id}'.");
                    if (kv.Value < MinStatLevel || kv.Value > MaxStatLevel)
                        throw new ArgumentOutOfRangeException(nameof(data),
                            $"Save file stat '{kv.Key}' for actor '{actorData.Id}' must be between {MinStatLevel} and {MaxStatLevel}.");
                    actor.Stats[kv.Key] = kv.Value;
                }
                actor.ActionDice.Clear();
                actor.ActionDiceSlotIds.Clear();
                if (data.HasSavedActionDice)
                {
                    if (actorData.ActionDice.Count != actorData.ActionDiceSlotIds.Count)
                        throw new ArgumentException($"Actor '{actorData.Id}' saved dice and slot lists must have equal length.");

                    int availableSlotCount = actor.Status == "active"
                        ? actor.ActionSlotCount - (actor.Role == "protagonist" && Injury.CostsActionDie ? 1 : 0)
                        : 0;
                    var seenSlots = new HashSet<int>();
                    for (int i = 0; i < actorData.ActionDice.Count; i++)
                    {
                        int die = actorData.ActionDice[i];
                        int slotId = actorData.ActionDiceSlotIds[i];
                        if (die < 1 || die > 6)
                            throw new ArgumentOutOfRangeException(nameof(data),
                                $"Actor '{actorData.Id}' saved die value must be between 1 and 6.");
                        if (slotId < 0 || slotId >= availableSlotCount)
                            throw new ArgumentOutOfRangeException(nameof(data),
                                $"Actor '{actorData.Id}' saved action slot {slotId} is not available.");
                        if (!seenSlots.Add(slotId))
                            throw new ArgumentException($"Actor '{actorData.Id}' save data repeats action slot {slotId}.");
                        actor.ActionDice.Add(die);
                        actor.ActionDiceSlotIds.Add(slotId);
                    }
                }
            }
            OnTeamChanged?.Invoke();
        }

        /// <summary>
        /// 把当前骰池原样抄一份。用于「离开世界去打一场交锋」——世界骰池是当天的，
        /// 交锋结束回来时要还回去，而不是重掷（见 SceneManager 的骰池暂存）。
        /// </summary>
        public Dictionary<string, (List<int> Dice, List<int> SlotIds)> CaptureActionDice()
        {
            var result = new Dictionary<string, (List<int>, List<int>)>();
            foreach (var actor in Actors)
                result[actor.Id] = (new List<int>(actor.ActionDice), new List<int>(actor.ActionDiceSlotIds));
            return result;
        }

        /// <summary>把 CaptureActionDice 抄下的骰池放回去。名单里没有的行动者一律清空骰池。</summary>
        public void RestoreActionDice(Dictionary<string, (List<int> Dice, List<int> SlotIds)> captured)
        {
            foreach (var actor in Actors)
            {
                actor.ActionDice.Clear();
                actor.ActionDiceSlotIds.Clear();
                if (!captured.TryGetValue(actor.Id, out var saved))
                    continue;
                actor.ActionDice.AddRange(saved.Dice);
                actor.ActionDiceSlotIds.AddRange(saved.SlotIds);
            }
            OnTeamChanged?.Invoke();
        }

        public void RollActionDice(bool isInEncounter, bool consumeHangover = true)
        {
            var rand = GameRandom.Instance;
            bool injuryDicePenalty = Injury.CostsActionDie;
            foreach (var actor in Actors)
            {
                bool applyHangover = !isInEncounter && consumeHangover && actor.HangoverSlotId != null;
                actor.ActionDice.Clear();
                actor.ActionDiceSlotIds.Clear();
                if (IsOnStage(actor, isInEncounter))
                {
                    int diceCount = actor.ActionSlotCount;
                    if (actor.Role == "protagonist")
                    {
                        if (injuryDicePenalty)
                            diceCount -= 1;
                    }
                    for (int slotId = 0; slotId < diceCount; slotId++)
                    {
                        if (actor.FixedDieValue != null)
                        {
                            // 恒定骰点不受任何降质影响：机器不会宿醉，也不会手抖。
                            actor.ActionDice.Add(actor.FixedDieValue.Value);
                            actor.ActionDiceSlotIds.Add(slotId);
                            continue;
                        }
                        int penalty = GetCurrentSlotPenalty(actor, slotId);
                        if (applyHangover && actor.HangoverSlotId == slotId)
                            penalty--;
                        actor.ActionDice.Add(Math.Max(1, rand.Next(1, 7) + penalty));
                        actor.ActionDiceSlotIds.Add(slotId);
                    }
                }
                if (applyHangover)
                    actor.HangoverSlotId = null;
            }
            OnTeamChanged?.Invoke();
        }

        public IReadOnlyList<ActionSlotStatus> GetActiveActionSlotStatuses(ActorState actor)
        {
            var result = new List<ActionSlotStatus>();
            if (actor.PermanentDiePenalty != 0)
                result.Add(new ActionSlotStatus { SlotId = 0, Label = actor.PermanentDiePenaltyLabel, DiePenalty = actor.PermanentDiePenalty });
            if (actor.FixedDieValue != null)
            {
                for (int slotId = 0; slotId < actor.ActionSlotCount; slotId++)
                    result.Add(new ActionSlotStatus
                    {
                        SlotId = slotId,
                        Label = actor.FixedDieLabel,
                        FixedDieValue = actor.FixedDieValue.Value
                    });
            }
            return result;
        }

        public IReadOnlyList<ActionSlotStatus> GetPendingActionSlotStatuses(ActorState actor)
        {
            if (actor.HangoverSlotId == null) return Array.Empty<ActionSlotStatus>();
            return new[] { new ActionSlotStatus { SlotId = actor.HangoverSlotId.Value, Label = "宿醉", DiePenalty = -1 } };
        }

        // 骰池位置上现在只剩人物经历造成的永久损伤（乔的残疾），临时降质走 pending 的宿醉。
        private static int GetCurrentSlotPenalty(ActorState actor, int slotId)
            => slotId == 0 ? actor.PermanentDiePenalty : 0;
    }
}
