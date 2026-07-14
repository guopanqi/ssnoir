#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class TeamState
    {
        public const int MinStatLevel = 0;
        public const int MaxStatLevel = 4;
        public const int MaxStress = 4;
        public const int StressPenaltyThreshold = 2;
        public const int HealthPenaltyThreshold = 2;

        public int MaxHealth { get; set; } = 5;

        public static int GetStressRollModifier(int stress)
        {
            if (stress < 0 || stress > MaxStress)
                throw new ArgumentOutOfRangeException(nameof(stress), $"Stress must be between 0 and {MaxStress}.");
            return stress >= StressPenaltyThreshold ? -1 : 0;
        }

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

        public int GetAvailableGrowthPoints(ActorState actor)
        {
            return Math.Max(0, GrowthLevel - actor.SpentGrowthPoints);
        }

        public int MaxSatiety { get; set; } = 5;

        private int _satiety = 3;
        // 饱腹：每天睡觉 −1，归零后开始扣健康（见 EndTurn）。吃食物恢复。
        public int Satiety
        {
            get => _satiety;
            set
            {
                _satiety = Math.Clamp(value, 0, MaxSatiety);
                OnTeamChanged?.Invoke();
            }
        }

        public List<ActorState> Actors { get; } = new List<ActorState>();

        public event Action? OnTeamChanged;

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
                Stress = 0,
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

        public void ApplyStress(string actorId, int amount)
        {
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }

            if (actor.Role == "companion")
            {
                int nextStress = actor.Stress + amount;
                actor.Stress = nextStress;
                if (actor.Stress >= MaxStress)
                {
                    actor.Status = "away";
                }
            }
            else if (actor.Role == "protagonist")
            {
                int curStress = actor.Stress;
                if (curStress < MaxStress)
                {
                    actor.Stress = Math.Min(MaxStress, curStress + amount);
                    int overflow = (curStress + amount) - MaxStress;
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
            OnTeamChanged?.Invoke();
        }

        public void SetActorStressSafe(string actorId, int newStress)
        {
            var actor = FindActor(actorId);
            if (actor == null)
            {
                throw new ArgumentException($"Actor with id '{actorId}' not found in team.");
            }

            int curStress = actor.Stress;
            int delta = newStress - curStress;
            if (delta > 0)
            {
                ApplyStress(actorId, delta);
            }
            else
            {
                actor.Stress = newStress;
                if (actor.Role == "companion" && actor.Stress < MaxStress && actor.Status == "away")
                {
                    if (actor.Stress == 0)
                    {
                        actor.Status = "active";
                    }
                }
                OnTeamChanged?.Invoke();
            }
        }

        public void EndTurn(bool isInEncounter)
        {
            // 1. 饱腹 −1；已经饿到 0 则扣健康
            if (Satiety <= 0)
            {
                Health -= 1;
            }
            else
            {
                Satiety -= 1;
            }

            // 2. Stress is content-driven (sleep, shelter, events), not a universal
            // turn-end effect. EndTurn only advances mandatory systemic state.

            // 3. Roll action dice for active members
            RollActionDice(isInEncounter);
        }

        public TeamSaveData Serialize()
        {
            var data = new TeamSaveData
            {
                Health      = Health,
                Satiety     = Satiety,
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
                    Stress            = actor.Stress,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats             = new Dictionary<string, int>(actor.Stats),
                });
            }
            return data;
        }

        public void ApplySaveData(TeamSaveData data)
        {
            Health      = data.Health;
            Satiety     = data.Satiety;
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
                actor.Stress            = actorData.Stress;
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
            }
            OnTeamChanged?.Invoke();
        }

        public void RollActionDice(bool isInEncounter)
        {
            var rand = GameRandom.Instance;
            bool healthDicePenalty = Health <= HealthPenaltyThreshold;
            foreach (var actor in Actors)
            {
                actor.ActionDice.Clear();
                if (actor.Status == "active")
                {
                    if (isInEncounter && actor.Role == "companion")
                    {
                        continue;
                    }

                    int diceCount = actor.Role == "protagonist" ? 3 : 1;
                    if (healthDicePenalty && actor.Role == "protagonist")
                        diceCount -= 1;
                    for (int i = 0; i < diceCount; i++)
                        actor.ActionDice.Add(rand.Next(1, 7));
                }
            }
            OnTeamChanged?.Invoke();
        }
    }
}
