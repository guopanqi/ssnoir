#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class TeamState
    {
        public int MaxHealth { get; set; } = 8;
        
        private int _health = 8;
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

        public int MaxSupplies { get; set; } = 6;
        
        private int _supplies = 3;
        public int Supplies
        {
            get => _supplies;
            set
            {
                _supplies = Math.Clamp(value, 0, MaxSupplies);
                OnTeamChanged?.Invoke();
            }
        }

        public List<ActorState> Actors { get; } = new List<ActorState>();

        public event Action? OnTeamChanged;

        public ActorState? FindActor(string actorId)
        {
            return Actors.Find(a => a.Id.Equals(actorId, StringComparison.OrdinalIgnoreCase));
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
            const int MaxStatLevel = 6;
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
                if (actor.Stress >= 6)
                {
                    actor.Status = "away";
                }
            }
            else if (actor.Role == "protagonist")
            {
                int curStress = actor.Stress;
                if (curStress < 6)
                {
                    actor.Stress = Math.Min(6, curStress + amount);
                    int overflow = (curStress + amount) - 6;
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
                if (actor.Role == "companion" && actor.Stress < 6 && actor.Status == "away")
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
            // 1. Consume 1 supply
            if (Supplies <= 0)
            {
                Health -= 1;
            }
            else
            {
                Supplies -= 1;
            }

            // 2. Reduce stress for everyone by 1, and recovery away status if stress reaches 0
            foreach (var actor in Actors)
            {
                actor.Stress = Math.Max(0, actor.Stress - 1);
                if (actor.Role == "companion" && actor.Stress == 0 && actor.Status == "away")
                {
                    actor.Status = "active";
                }
            }

            // 3. Roll action dice for active members
            RollActionDice(isInEncounter);
        }

        public TeamSaveData Serialize()
        {
            var data = new TeamSaveData
            {
                Health      = Health,
                Supplies    = Supplies,
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
            Supplies    = data.Supplies;
            GrowthLevel = data.GrowthLevel;
            foreach (var actorData in data.Actors)
            {
                var actor = FindActor(actorData.Id)
                    ?? throw new ArgumentException($"Save file references unknown actor '{actorData.Id}'.");
                if (actorData.Status != "active" && actorData.Status != "away")
                    throw new ArgumentException($"Actor '{actorData.Id}' has invalid status '{actorData.Status}' in save file.");
                actor.Status            = actorData.Status;
                actor.Stress            = actorData.Stress;
                actor.SpentGrowthPoints = actorData.SpentGrowthPoints;
                foreach (var kv in actorData.Stats)
                {
                    if (!actor.Stats.ContainsKey(kv.Key))
                        throw new ArgumentException($"Save file contains unknown stat '{kv.Key}' for actor '{actorData.Id}'.");
                    actor.Stats[kv.Key] = kv.Value;
                }
                actor.ActionDice.Clear(); // re-rolled after load
            }
            OnTeamChanged?.Invoke();
        }

        public void RollActionDice(bool isInEncounter)
        {
            var rand = new Random();
            foreach (var actor in Actors)
            {
                actor.ActionDice.Clear();
                if (actor.Status == "active")
                {
                    if (isInEncounter && actor.Role == "companion")
                    {
                        continue;
                    }

                    actor.ActionDice.Add(rand.Next(1, 7));
                    actor.ActionDice.Add(rand.Next(1, 7));
                }
            }
            OnTeamChanged?.Invoke();
        }
    }
}
