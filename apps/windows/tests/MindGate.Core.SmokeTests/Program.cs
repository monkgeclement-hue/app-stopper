using System.Text.Json;
using MindGate.Core;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

var fixture = Path.Combine(AppContext.BaseDirectory, "app-rule.daily.example.json");
var rule = JsonSerializer.Deserialize<AppRule>(File.ReadAllText(fixture))
    ?? throw new InvalidOperationException("Could not read the shared daily rule example.");
Check(AppRuleValidator.Validate(rule).Count == 0, "Shared daily rule example should validate.");

var start = new DateTimeOffset(2026, 10, 12, 2, 0, 0, TimeSpan.Zero); // 04:00 in Africa/Johannesburg
var warningUpdate = UsageBudgetEngine.Consume(rule, null, 2_700, start);
Check(warningUpdate.NewlyReachedWarningThresholdsSeconds.SequenceEqual([1_800, 1_200]),
    "A long usage interval should emit each crossed warning once, in order.");
Check(!warningUpdate.IsBlocked, "The rule should remain available before the allowance is exhausted.");

var blockUpdate = UsageBudgetEngine.Consume(rule, warningUpdate.State, 900, start.AddSeconds(900));
Check(blockUpdate.IsBlocked, "The rule should block at the allowance boundary.");
Check(blockUpdate.NewlyReachedWarningThresholdsSeconds.SequenceEqual([600]),
    "The final warning should fire once when its remaining-time threshold is crossed.");
Check(blockUpdate.State.UsedSeconds == rule.AllowanceSeconds, "Usage should be capped at the allowance.");
Check(blockUpdate.NextResetAtUtc == new DateTimeOffset(2026, 10, 13, 2, 0, 0, TimeSpan.Zero),
    "The daily reset should use the configured local time zone.");

var afterReset = UsageBudgetEngine.Consume(rule, blockUpdate.State, 1, blockUpdate.NextResetAtUtc!.Value);
Check(afterReset.CycleReset && afterReset.State.UsedSeconds == 1 && !afterReset.IsBlocked,
    "A new daily cycle should clear the old usage and block.");

var cooldownRule = rule with
{
    ResetPolicy = new ResetPolicy { Mode = "cooldown", CooldownSeconds = 3_600 },
    AllowanceSeconds = 60,
    WarningThresholdsSeconds = [30]
};
Check(AppRuleValidator.Validate(cooldownRule).Count == 0, "A valid cooldown rule should validate.");
var cooldownBlock = UsageBudgetEngine.Consume(cooldownRule, null, 60, start);
Check(cooldownBlock.IsBlocked && cooldownBlock.NextResetAtUtc == start.AddHours(1),
    "A cooldown should start when the allowance is exhausted.");
var cooldownReset = UsageBudgetEngine.Consume(cooldownRule, cooldownBlock.State, 5, start.AddHours(1));
Check(cooldownReset.CycleReset && cooldownReset.State.UsedSeconds == 5 && !cooldownReset.IsBlocked,
    "A completed cooldown should start a fresh usage cycle.");

Console.WriteLine("All MindGate.Core smoke checks passed.");
