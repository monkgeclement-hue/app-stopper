using System.Text.Json.Serialization;

namespace MindGate.Core;

public sealed record AppRule
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "";

    [JsonPropertyName("rule_id")]
    public string RuleId { get; init; } = "";

    [JsonPropertyName("target")]
    public AppTarget Target { get; init; } = new();

    [JsonPropertyName("allowance_seconds")]
    public int AllowanceSeconds { get; init; }

    [JsonPropertyName("warning_thresholds_seconds")]
    public int[] WarningThresholdsSeconds { get; init; } = [];

    [JsonPropertyName("reset_policy")]
    public ResetPolicy ResetPolicy { get; init; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
}

public sealed record AppTarget
{
    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";

    [JsonPropertyName("selection_ref")]
    public string SelectionRef { get; init; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "";
}

public sealed record ResetPolicy
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "";

    [JsonPropertyName("local_time")]
    public string? LocalTime { get; init; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; init; }

    [JsonPropertyName("cooldown_seconds")]
    public int? CooldownSeconds { get; init; }
}

/// <summary>Persist this value locally alongside the rule; it contains no app-selection identifier.</summary>
public sealed record UsageCycleState
{
    public int UsedSeconds { get; init; }
    public int[] WarnedThresholdsSeconds { get; init; } = [];
    public DateTimeOffset? ExhaustedAtUtc { get; init; }
    public DateTimeOffset? CycleStartUtc { get; init; }
    public DateTimeOffset? CycleEndUtc { get; init; }
}

public sealed record UsageUpdate
{
    public UsageCycleState State { get; init; } = new();
    public int[] NewlyReachedWarningThresholdsSeconds { get; init; } = [];
    public bool IsBlocked { get; init; }
    public DateTimeOffset? NextResetAtUtc { get; init; }
    public bool CycleReset { get; init; }
}
