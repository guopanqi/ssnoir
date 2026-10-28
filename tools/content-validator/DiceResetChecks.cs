using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SSNoir.Core;

namespace SSNoir.Testing
{
    public static class DiceResetChecks
    {
        public static void Run()
        {
            VerifyTeamRollReplacesTheWholeHand();
            VerifySceneManagerRefreshScenarioRoundAndCityRest();
            VerifyRevisionIsNotSaved();
            Console.WriteLine("[test-dice-reset] Dice hand revision contracts passed.");
        }

        private static void VerifyTeamRollReplacesTheWholeHand()
        {
            var state = NewFixedDieState();
            var team = state.Team;
            var actor = Player(state);
            long initialRevision = actor.ActionDiceRevision;

            team.RollActionDice(isInScenario: false);
            Require(actor.ActionDiceRevision == initialRevision + 1,
                "A newly issued city hand must increment its revision once.");
            Require(actor.ActionDice.SequenceEqual(new[] { 4, 4, 4, 4 }),
                "The fixed-die fixture must issue four identical dice.");
            Require(actor.ActionDiceSlotIds.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "A new hand must issue every available slot.");

            team.ConsumeAvailableActionDice(actor.Id, 1);
            long afterConsume = actor.ActionDiceRevision;
            Require(afterConsume == initialRevision + 1 && actor.ActionDice.Count == 3,
                "Spending a die must not increment the hand revision.");

            team.RollActionDice(isInScenario: false);
            Require(actor.ActionDiceRevision == afterConsume + 1,
                "A second hand with identical values must still increment the revision.");
            Require(actor.ActionDice.SequenceEqual(new[] { 4, 4, 4, 4 })
                && actor.ActionDiceSlotIds.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "A new hand must replace remaining dice and restore every available slot.");

            team.Injury.Restore(5, "手");
            long beforeInjuredRoll = actor.ActionDiceRevision;
            team.RollActionDice(isInScenario: false);
            Require(actor.ActionDiceRevision == beforeInjuredRoll + 1,
                "An injured actor's newly issued hand must increment once.");
            Require(actor.IssuedInjuryDieLoss == 2
                && actor.ActionDice.SequenceEqual(new[] { 4, 4 })
                && actor.ActionDiceSlotIds.SequenceEqual(new[] { 0, 1 }),
                "Injury-blocked slots must not receive dice in the new hand.");
        }

        private static void VerifySceneManagerRefreshScenarioRoundAndCityRest()
        {
            var loader = new FixtureScriptLoader();
            var state = NewFixedDieState();
            var manager = new SceneManager(state, loader);
            var actor = Player(state);
            manager.LoadScene("world");

            long cityRevision = actor.ActionDiceRevision;
            AssertSnapshotRevision(manager, actor, cityRevision, 4);
            manager.Refresh();
            AssertSnapshotRevision(manager, actor, cityRevision, 4);

            state.Team.ConsumeAvailableActionDice(actor.Id, 1);
            manager.Refresh();
            AssertSnapshotRevision(manager, actor, cityRevision, 3);
            int[] stashedCityDice = actor.ActionDice.ToArray();
            int[] stashedCitySlots = actor.ActionDiceSlotIds.ToArray();

            manager.StartScenario("dice-reset-fixture");
            long scenarioRevision = actor.ActionDiceRevision;
            Require(scenarioRevision == cityRevision + 1,
                "Entering an scenario must issue exactly one new hand.");
            manager.EndScenario();
            Require(actor.ActionDiceRevision == scenarioRevision,
                "Returning to the city must restore its stashed hand without incrementing revision.");
            Require(actor.ActionDice.SequenceEqual(stashedCityDice)
                && actor.ActionDiceSlotIds.SequenceEqual(stashedCitySlots),
                $"Returning to the city must restore the remaining old hand; got [{string.Join(",", actor.ActionDice)}] in slots [{string.Join(",", actor.ActionDiceSlotIds)}].");

            manager.EndTurn();
            long afterCityRest = actor.ActionDiceRevision;
            Require(afterCityRest == scenarioRevision + 1,
                "A city rest/new day must issue exactly one new hand.");
            AssertSnapshotRevision(manager, actor, afterCityRest, 4);

            manager.StartScenario("dice-reset-fixture");
            long beforeRound = actor.ActionDiceRevision;
            manager.EndTurn();
            Require(actor.ActionDiceRevision == beforeRound + 1,
                "The SceneManager scenario EndTurn facade must issue exactly one new hand.");
            Require(actor.ActionDice.SequenceEqual(new[] { 4, 4, 4, 4 })
                && actor.ActionDiceSlotIds.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "The scenario round must replace the full hand, including unspent dice.");
        }

        private static void VerifyRevisionIsNotSaved()
        {
            PropertyInfo? savedRevision = typeof(ActorSaveData).GetProperty(nameof(ActorState.ActionDiceRevision));
            Require(savedRevision == null, "ActionDiceRevision must remain runtime-only and absent from ActorSaveData.");
        }

        private static GameState NewFixedDieState()
        {
            GameRandom.Reseed(42);
            var state = new GameState();
            var actor = Player(state);
            actor.FixedDieValue = 4;
            actor.ActionSlotCount = 4;
            actor.Composure = actor.MaxComposure;
            return state;
        }

        private static ActorState Player(GameState state) => state.Team.FindActor("player")
            ?? throw new InvalidOperationException("The fixed-die fixture requires a protagonist.");

        private static void AssertSnapshotRevision(SceneManager manager, ActorState actor, long expectedRevision, int expectedDice)
        {
            var snapshot = manager.LatestSnapshot.Actors.Single(a => a.Id == actor.Id);
            Require(snapshot.ActionDiceRevision == expectedRevision,
                "PresentationSnapshot did not preserve the actor's current dice revision.");
            Require(snapshot.ActionDice.Count == expectedDice,
                $"Expected {expectedDice} dice in the snapshot, got {snapshot.ActionDice.Count}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FixtureScriptLoader : IScriptLoader
        {
            private readonly Dictionary<string, string> _scripts = new()
            {
                ["scenes/world/world.scm"] = """
                    (define (get-render-data) (node "world-root" :name "世界" :children '()))
                    (define (get-dossier) '())
                    (define (on-enter-place id) #t)
                    (define (on-turn-end) #t)
                    (define (on-turn-start) #t)
                    """,
                ["scenes/scenarios/dice-reset-fixture.scm"] = """
                    (define (get-render-data) (node "scenario-root" :name "测试Scenario" :children '()))
                    (define (on-scenario-enter) #t)
                    (define (on-scenario-collapse) (list 'test))
                    (define (on-turn-end) #t)
                    (define (on-action) #t)
                    (define (scenario-action-nodes) '())
                    """
            };
            private readonly LocalScriptLoader _fallback = new();

            public string LoadScriptText(string path) => _scripts.TryGetValue(path, out string? source)
                ? source
                : _fallback.LoadScriptText(path);
        }
    }
}
