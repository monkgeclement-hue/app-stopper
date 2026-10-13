using System.Globalization;

namespace AppStopper.Core;

/// <summary>
/// Pure policy logic. A platform adapter supplies only foreground-use seconds and applies the returned block state.
/// </summary>
public static class UsageBudgetEngine
{
    public static UsageUpdate Consume(AppRule rule, UsageCycleState? current, int foregroundSeconds, DateTimeOffset now)
    {
        AppRuleValidator.EnsureValid(rule);
        if (foregroundSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(foregroundSeconds), "Usage cannot be negative.");

        current ??= new UsageCycleState();
        var state = NormalizeCycle(rule.ResetPolicy, current, now, out var reset);

        if (!rule.Enabled)
            return BuildUpdate(rule, state, [], now, reset);

        var remainingBefore = Math.Max(0, rule.AllowanceSeconds - state.UsedSeconds);
        var usedAfter = (int)Math.Min(rule.AllowanceSeconds, (long)state.UsedSeconds + foregroundSeconds);
        var remainingAfter = rule.AllowanceSeconds - usedAfter;

        var warned = (state.WarnedThresholdsSeconds ?? []).ToHashSet();
        var newlyReached = rule.WarningThresholdsSeconds
            .Where(threshold => remainingBefore > threshold && remainingAfter <= threshold && warned.Add(threshold))
            .OrderByDescending(threshold => threshold)
            .ToArray();

        DateTimeOffset? exhaustedAt = state.ExhaustedAtUtc;
        if (usedAfter >= rule.AllowanceSeconds && exhaustedAt is null)
            exhaustedAt = now.ToUniversalTime();

        state = state with
        {
            UsedSeconds = usedAfter,
            WarnedThresholdsSeconds = warned.Order().ToArray(),
            ExhaustedAtUtc = exhaustedAt
        };

        return BuildUpdate(rule, state, newlyReached, now, reset);
    }

    private static UsageCycleState NormalizeCycle(
        ResetPolicy policy,
        UsageCycleState state,
        DateTimeOffset now,
        out bool reset)
    {
        reset = false;
        var utcNow = now.ToUniversalTime();

        if (policy.Mode == "daily")
        {
            if (state.CycleEndUtc is not null && utcNow < state.CycleEndUtc.Value)
                return state;

            var (start, end) = GetDailyWindow(policy, utcNow);
            reset = state.CycleStartUtc is not null;
            return new UsageCycleState { CycleStartUtc = start, CycleEndUtc = end };
        }

        if (state.ExhaustedAtUtc is not null && utcNow >= state.ExhaustedAtUtc.Value.AddSeconds(policy.CooldownSeconds!.Value))
        {
            reset = true;
            return new UsageCycleState { CycleStartUtc = utcNow };
        }

        return state.CycleStartUtc is null ? state with { CycleStartUtc = utcNow } : state;
    }

    private static UsageUpdate BuildUpdate(
        AppRule rule,
        UsageCycleState state,
        int[] warnings,
        DateTimeOffset now,
        bool reset)
    {
        DateTimeOffset? nextReset;
        bool blocked;

        if (rule.ResetPolicy.Mode == "daily")
        {
            nextReset = state.CycleEndUtc;
            blocked = rule.Enabled && state.UsedSeconds >= rule.AllowanceSeconds &&
                (nextReset is null || now.ToUniversalTime() < nextReset.Value);
        }
        else
        {
            nextReset = state.ExhaustedAtUtc?.AddSeconds(rule.ResetPolicy.CooldownSeconds!.Value);
            blocked = rule.Enabled && state.UsedSeconds >= rule.AllowanceSeconds &&
                nextReset is not null && now.ToUniversalTime() < nextReset.Value;
        }

        return new UsageUpdate
        {
            State = state,
            NewlyReachedWarningThresholdsSeconds = warnings,
            IsBlocked = blocked,
            NextResetAtUtc = nextReset,
            CycleReset = reset
        };
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetDailyWindow(ResetPolicy policy, DateTimeOffset utcNow)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZone!);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, zone);
        var resetTime = TimeOnly.ParseExact(policy.LocalTime!, "HH:mm", CultureInfo.InvariantCulture);
        var today = localNow.Date;
        var todayBoundary = ResolveBoundary(today, resetTime, zone);

        if (utcNow >= todayBoundary)
            return (todayBoundary, ResolveBoundary(today.AddDays(1), resetTime, zone));

        return (ResolveBoundary(today.AddDays(-1), resetTime, zone), todayBoundary);
    }

    private static DateTimeOffset ResolveBoundary(DateTime date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);

        // A reset configured inside a daylight-saving gap moves to the first valid local minute.
        for (var minute = 0; zone.IsInvalidTime(local) && minute < 180; minute++)
            local = local.AddMinutes(1);

        if (zone.IsInvalidTime(local))
            throw new InvalidOperationException("Could not resolve the daily reset time in the selected time zone.");

        // For a repeated local time, choose the earlier instant so the reset occurs only once.
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
