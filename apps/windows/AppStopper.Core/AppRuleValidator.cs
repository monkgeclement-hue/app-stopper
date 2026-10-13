using System.Globalization;

namespace AppStopper.Core;

public static class AppRuleValidator
{
    public static IReadOnlyList<string> Validate(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var errors = new List<string>();

        if (rule.SchemaVersion != "1.0")
            errors.Add("schema_version must be 1.0.");
        if (!Guid.TryParseExact(rule.RuleId, "D", out _))
            errors.Add("rule_id must be a UUID in D format.");
        if (rule.Target is null)
        {
            errors.Add("target is required.");
        }
        else
        {
            if (rule.Target.Platform is not ("android" or "ios" or "ipados" or "windows"))
                errors.Add("target.platform is not supported.");
            if (string.IsNullOrWhiteSpace(rule.Target.SelectionRef))
                errors.Add("target.selection_ref is required.");
            if (string.IsNullOrWhiteSpace(rule.Target.DisplayName))
                errors.Add("target.display_name is required.");
        }

        if (rule.AllowanceSeconds is < 1 or > 86_400)
            errors.Add("allowance_seconds must be between 1 and 86400.");
        if (rule.WarningThresholdsSeconds is null || rule.WarningThresholdsSeconds.Length == 0)
        {
            errors.Add("At least one warning threshold is required.");
        }
        else
        {
            if (rule.WarningThresholdsSeconds.Any(value => value < 1 || value > rule.AllowanceSeconds))
                errors.Add("Every warning threshold must be positive and no greater than the allowance.");
            if (rule.WarningThresholdsSeconds.Distinct().Count() != rule.WarningThresholdsSeconds.Length)
                errors.Add("Warning thresholds must be unique.");
        }

        if (rule.ResetPolicy is null)
        {
            errors.Add("reset_policy is required.");
        }
        else if (rule.ResetPolicy.Mode == "daily")
        {
            if (rule.ResetPolicy.LocalTime is null ||
                !TimeOnly.TryParseExact(rule.ResetPolicy.LocalTime, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                errors.Add("daily reset_policy.local_time must use HH:mm format.");

            if (string.IsNullOrWhiteSpace(rule.ResetPolicy.TimeZone))
            {
                errors.Add("daily reset_policy.time_zone is required.");
            }
            else
            {
                try
                {
                    _ = TimeZoneInfo.FindSystemTimeZoneById(rule.ResetPolicy.TimeZone);
                }
                catch (TimeZoneNotFoundException)
                {
                    errors.Add("daily reset_policy.time_zone is not recognized on this device.");
                }
                catch (InvalidTimeZoneException)
                {
                    errors.Add("daily reset_policy.time_zone is invalid on this device.");
                }
            }

            if (rule.ResetPolicy.CooldownSeconds is not null)
                errors.Add("daily reset_policy must not include cooldown_seconds.");
        }
        else if (rule.ResetPolicy.Mode == "cooldown")
        {
            if (rule.ResetPolicy.CooldownSeconds is not (>= 1 and <= 2_592_000))
                errors.Add("cooldown_seconds must be between 1 and 2592000.");
            if (rule.ResetPolicy.LocalTime is not null || rule.ResetPolicy.TimeZone is not null)
                errors.Add("cooldown reset_policy must not include local_time or time_zone.");
        }
        else
        {
            errors.Add("reset_policy.mode must be daily or cooldown.");
        }

        return errors;
    }

    public static void EnsureValid(AppRule rule)
    {
        var errors = Validate(rule);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(rule));
    }
}
