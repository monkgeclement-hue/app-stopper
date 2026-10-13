using System.Text.Json;
using AppStopper.Core;

namespace AppStopper.WindowsClient;

internal sealed class TrackedRule
{
    public AppRule Rule { get; set; } = new();
    public UsageCycleState State { get; set; } = new();
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
        "AppStopper",
        "windows-state.json");

    internal List<TrackedRule> Load()
    {
        if (!File.Exists(_statePath))
            return [];

        var rules = JsonSerializer.Deserialize<List<TrackedRule>>(File.ReadAllText(_statePath), JsonOptions) ?? [];
        foreach (var trackedRule in rules)
            AppRuleValidator.EnsureValid(trackedRule.Rule);
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
