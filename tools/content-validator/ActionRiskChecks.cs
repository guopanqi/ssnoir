#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing
{
    public static class ActionRiskChecks
    {
        private static void Assert(bool valid, string message)
        {
            if (!valid) throw new Exception("[action-risk] " + message);
        }

        private static GameNode Find(GameNode node, string id)
        {
            if (node.Id == id) return node;
            foreach (var child in node.Children)
            {
                try { return Find(child, id); }
                catch (InvalidOperationException) { }
            }
            throw new InvalidOperationException("Missing node: " + id);
        }

        public static void Run()
        {
            var clock = new GameClock
            {
                Label = "夜莺的恐惧", Current = 2, Max = 3,
                Style = ClockStyle.Pressure, BeneficialDirection = BeneficialDirection.Decrease
            };
            var root = new GameNode { Id = "root", Children = new List<GameNode>
            {
                new GameNode { Id = "clock", Clocks = { clock } },
                new GameNode { Id = "action", Risk = new ActionRisk { Level = ActionRiskLevel.Risky },
                    RiskTarget = new ActionTarget { Kind = ActionTargetKind.Clock, Id = "夜莺的恐惧" } }
            }};
            ActionRiskContract.Validate(root, Array.Empty<GameNode>());
            root.Children[1].RiskTarget = new ActionTarget { Kind = ActionTargetKind.Clock, Id = "缺失钟" };
            bool rejected = false;
            try { ActionRiskContract.Validate(root, Array.Empty<GameNode>()); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "missing Clock target not rejected");

            var rain = new SceneManager(new GameState(), new LocalScriptLoader());
            rain.LoadScene("雨夜同行");
            var shortcut = Find(rain.CurrentRootNode!, "穿过暗巷");
            Assert(shortcut.Risk?.Level == ActionRiskLevel.Risky &&
                   shortcut.RiskTarget?.Id == "夜莺的恐惧", "rainy alley preview mismatched");
            var safe = Find(rain.CurrentRootNode!, "沿街灯走");
            Assert(safe.Risk?.Level == ActionRiskLevel.Safe &&
                   safe.ProgressTarget?.Id == "回到住处", "lit street preview mismatched");

            var interpreter = rain.ActiveInterpreter;
            void Reject(string expr)
            {
                try { NodeConverter.ConvertSingle(interpreter.Eval(expr), interpreter.RawInterpreter); }
                catch (InvalidOperationException) { return; }
                throw new Exception("Invalid risk declaration accepted: " + expr);
            }
            Reject("(node \"bad\" :risk '陌生)");
            Reject("(node \"invalid\" :risk-target (list 'whatever \"x\"))");
            Reject("(node \"bad-placement\" :risk '稳妥)");

            var payment = NodeConverter.ConvertSingle(interpreter.Eval(
                "(node \"pay\" :progress-target (list 'clock \"夜莺的恐惧\") :resolve (instant (outcome (lambda () #t))))"), interpreter.RawInterpreter);
            Assert(payment.Risk == null && payment.ProgressTarget?.Id == "夜莺的恐惧",
                "instant payment must declare its effect without a roll risk");
            root.Children[1] = payment;
            ActionRiskContract.Validate(root, Array.Empty<GameNode>());
            payment.ProgressTarget = new ActionTarget { Kind = ActionTargetKind.Clock, Id = "缺失钟" };
            rejected = false;
            try { ActionRiskContract.Validate(root, Array.Empty<GameNode>()); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "instant missing Clock target not rejected");

            var tavern = new SceneManager(new GameState(), new LocalScriptLoader());
            tavern.LoadScene("醉客互相叫骂");
            var persuade = Find(tavern.CurrentRootNode!, "西边这桌：劝");
            var threaten = Find(tavern.CurrentRootNode!, "西边这桌：威胁");
            Assert(persuade.Risk?.Level == ActionRiskLevel.Safe
                   && threaten.Risk?.Level == ActionRiskLevel.Risky, "tavern choices are not distinct");
            int before = Convert.ToInt32(tavern.ActiveInterpreter.Eval("(西边 'current)"));
            persuade.Resolve!.FailOutcome!.Effect!.Invoke();
            int after = Convert.ToInt32(tavern.ActiveInterpreter.Eval("(西边 'current)"));
            Assert(after == before, "safe failed persuasion escalates the scene");
            threaten.Resolve!.SuccessOutcome!.Effect!.Invoke();
            after = Convert.ToInt32(tavern.ActiveInterpreter.Eval("(西边 'current)"));
            Assert(after == before - 3, "risky success must be faster than safe success");
            Console.WriteLine("[test-action-risk] all checks passed");
        }
    }
}
