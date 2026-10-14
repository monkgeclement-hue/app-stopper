using System.Text.Json;

namespace MindGate.WindowsClient;

internal sealed class UsageTrackingPreferences
{
    public bool IgnoreUsageWhenIdle { get; set; }
    public int IdleThresholdMinutes { get; set; } = 5;
}

internal sealed class UsageTrackingPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _preferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindGate",
        "windows-preferences.json");

    internal UsageTrackingPreferences Load()
    {
        if (!File.Exists(_preferencesPath))
            return new UsageTrackingPreferences();

        var preferences = JsonSerializer.Deserialize<UsageTrackingPreferences>(
            File.ReadAllText(_preferencesPath), JsonOptions) ?? new UsageTrackingPreferences();
        preferences.IdleThresholdMinutes = Math.Clamp(preferences.IdleThresholdMinutes, 1, 60);
        return preferences;
    }

    internal void Save(UsageTrackingPreferences preferences)
    {
        preferences.IdleThresholdMinutes = Math.Clamp(preferences.IdleThresholdMinutes, 1, 60);
        var directory = Path.GetDirectoryName(_preferencesPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _preferencesPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, JsonOptions));
        File.Move(temporaryPath, _preferencesPath, overwrite: true);
    }
}
