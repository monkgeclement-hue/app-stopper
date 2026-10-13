using System.Text.Json;
using MindGate.Core;

namespace MindGate.WindowsClient;

internal sealed class TrackedRule
{
    public AppRule Rule { get; set; } = new();
    public UsageCycleState State { get; set; } = new();
    public string Category { get; set; } = "Other";
}

internal sealed class LocalStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindGate",
        "windows-state.json");
    private readonly string _legacyStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AppStopper",
        "windows-state.json");

    internal List<TrackedRule> Load()
    {
        var sourcePath = File.Exists(_statePath)
            ? _statePath
            : File.Exists(_legacyStatePath)
                ? _legacyStatePath
                : null;
        if (sourcePath is null)
            return [];

        var rules = JsonSerializer.Deserialize<List<TrackedRule>>(File.ReadAllText(sourcePath), JsonOptions) ?? [];
        foreach (var trackedRule in rules)
        {
            if (string.IsNullOrWhiteSpace(trackedRule.Category))
                trackedRule.Category = "Other";
            AppRuleValidator.EnsureValid(trackedRule.Rule);
        }

        if (sourcePath == _legacyStatePath)
            Save(rules);
        return rules;
    }

    internal void Save(IEnumerable<TrackedRule> rules)
    {
        var directory = Path.GetDirectoryName(_statePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _statePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(rules, JsonOptions));
        File.Move(temporaryPath, _statePath, overwrite: true);
    }
}
