#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class TeamState
    {
        public const int MinStatLevel = 0;
        public const int MaxStatLevel = 4;

        // 冷静三段阈值（见 docs/城市生活设计.md）：4–6 为缓冲，1–3 为失态，0 为失控。
        // 失态随机一个骰池位置 -1；失控再随机一个骰池位置 -2。0 点后继续花冷静会击穿为健康伤害。
        public const int MaxComposure = 6;
        public const int FaintThreshold = 3;
        public const int LossOfControlThreshold = 0;
        public const int HealthPenaltyThreshold = 2;

        public int MaxHealth { get; set; } = 5;

        private int _health = 5;
        public int Health
        {
            get => _health;
            set
            {
                _health = Math.Clamp(value, 0, MaxHealth);
                OnTeamChanged?.Invoke();
            }
        }

        public int GrowthLevel { get; set; } = 0;
        // 骰池位置稳定存在；身体状态附着在位置上，而非可变骰子列表下标。
        // 主角承担城市与交锋的主要行动，同伴只在城市中提供一枚额外行动骰。
        public const int ProtagonistActionSlotCount = 4;
        public const int CompanionActionSlotCount = 1;

        public static int GetActionSlotCount(string role) => role switch
        {
            "protagonist" => ProtagonistActionSlotCount,
            "companion" => CompanionActionSlotCount,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown actor role.")
        };
        /// <summary>本场是否登场：城市里全队都在，交锋里只有主角上场（同伴连骰子都不发）。
        /// 发骰和界面共用这一条规则，避免两边各判各的。</summary>
        public static bool IsOnStage(ActorState actor, bool isInEncounter)
            => actor.Status == "active" && !(isInEncounter && actor.Role == "companion");

        public int GetAvailableGrowthPoints(ActorState actor)
        {
            return Math.Max(0, GrowthLevel - actor.SpentGrowthPoints);
        }

        public List<ActorState> Actors { get; } = new List<ActorState>();

        public event Action? OnTeamChanged;

        public void ResetForNewGame()
        {
            MaxHealth = 5;
            _health = MaxHealth;
            GrowthLevel = 0;
            Actors.Clear();
            OnTeamChanged?.Invoke();
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
                Composure = MaxComposure,
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
            if (actor.Stats[statId] >= MaxStatLevel)
            {
                throw new InvalidOperationException($"Stat '{statId}' on actor '{actorId}' has reached the maximum level {MaxStatLevel}.");
            }
            if (GetAvailableGrowthPoints(actor) <= 0)
            {
                throw new InvalidOperationException($"Actor '{actorId}' has no available growth points.");
            }

            actor.Stats[statId]++;
            actor.SpentGrowthPoints++;
            OnTeamChanged?.Invoke();
        }

        // 花冷静（失败、交锋每回合自动流失）。协作者在 0 点失能离场；主角击穿
        // 0 点后溢出直接伤健康——"冷静挡不住子弹"之外的第二个健康受伤口。
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
                if (curComposure > 0)
                {
                    actor.Composure = Math.Max(0, curComposure - amount);
                    int overflow = amount - curComposure;
                    if (overflow > 0)
                    {
                        Health -= overflow;
                    }
                }
                else
                {
                    Health -= amount;
                }
            }
            UpdateComposureSlotStatuses(actor);
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
            if (actor.Role == "companion" && actor.Status == "away" && actor.Composure >= MaxComposure)
            {
                actor.Status = "active";
            }
            UpdateComposureSlotStatuses(actor);
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
                Health      = Health,
                GrowthLevel = GrowthLevel,
            };
            foreach (var actor in Actors)
            {
                data.Actors.Add(new ActorSaveData
                {
                    Id                = actor.Id,
                    Name              = actor.Name,
                    Role              = actor.Role,
                    Status            = actor.Status,
                    Composure         = actor.Composure,
                    HangoverSlotId     = actor.HangoverSlotId,
                    FaintSlotId        = actor.FaintSlotId,
                    LossOfControlSlotId = actor.LossOfControlSlotId,
                    PermanentDiePenaltyLabel = actor.PermanentDiePenaltyLabel,
                    PermanentDiePenalty = actor.PermanentDiePenalty,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats             = new Dictionary<string, int>(actor.Stats),
                });
            }
            return data;
        }

        public void ApplySaveData(TeamSaveData data)
        {
            Health      = data.Health;
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
                actor.FaintSlotId = actorData.FaintSlotId;
                actor.LossOfControlSlotId = actorData.LossOfControlSlotId;
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
                actor.ActionDice.Clear(); // re-rolled after load
                actor.ActionDiceSlotIds.Clear();
            }
            OnTeamChanged?.Invoke();
        }

        public void RollActionDice(bool isInEncounter, bool consumeHangover = true)
        {
            var rand = GameRandom.Instance;
            bool healthDicePenalty = Health <= HealthPenaltyThreshold;
            foreach (var actor in Actors)
            {
                bool applyHangover = !isInEncounter && consumeHangover && actor.HangoverSlotId != null;
                UpdateComposureSlotStatuses(actor);
                actor.ActionDice.Clear();
                actor.ActionDiceSlotIds.Clear();
                if (IsOnStage(actor, isInEncounter))
                {
                    int diceCount = GetActionSlotCount(actor.Role);
                    if (actor.Role == "protagonist")
                    {
                        if (healthDicePenalty)
                            diceCount -= 1;
                    }
                    for (int slotId = 0; slotId < diceCount; slotId++)
                    {
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
            if (actor.FaintSlotId != null) result.Add(new ActionSlotStatus { SlotId = actor.FaintSlotId.Value, Label = "失态", DiePenalty = -1 });
            if (actor.LossOfControlSlotId != null) result.Add(new ActionSlotStatus { SlotId = actor.LossOfControlSlotId.Value, Label = "失控", DiePenalty = -2 });
            return result;
        }

        public IReadOnlyList<ActionSlotStatus> GetPendingActionSlotStatuses(ActorState actor)
        {
            if (actor.HangoverSlotId == null) return Array.Empty<ActionSlotStatus>();
            return new[] { new ActionSlotStatus { SlotId = actor.HangoverSlotId.Value, Label = "宿醉", DiePenalty = -1 } };
        }

        private void UpdateComposureSlotStatuses(ActorState actor)
        {
            if (actor.Composure > FaintThreshold)
                actor.FaintSlotId = null;
            else if (actor.FaintSlotId == null)
                actor.FaintSlotId = GameRandom.Instance.Next(0, GetActionSlotCount(actor.Role));

            if (actor.Composure > LossOfControlThreshold)
                actor.LossOfControlSlotId = null;
            else if (actor.LossOfControlSlotId == null)
                actor.LossOfControlSlotId = PickUnusedSlot(actor.FaintSlotId, GetActionSlotCount(actor.Role));
        }

        private static int PickUnusedSlot(int? excluded, int actionSlotCount)
        {
            int pick = GameRandom.Instance.Next(0, excluded == null ? actionSlotCount : actionSlotCount - 1);
            return excluded != null && pick >= excluded.Value ? pick + 1 : pick;
        }

        private static int GetCurrentSlotPenalty(ActorState actor, int slotId)
        {
            int penalty = slotId == 0 ? actor.PermanentDiePenalty : 0;
            if (actor.FaintSlotId == slotId) penalty--;
            if (actor.LossOfControlSlotId == slotId) penalty -= 2;
            return penalty;
        }
    }
}
