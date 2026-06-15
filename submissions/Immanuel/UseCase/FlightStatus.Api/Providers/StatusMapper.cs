using FlightStatus.Api.Models;

namespace FlightStatus.Api.Providers;

// Normalisation lives here. Both providers speak their own status vocabulary so each
// one calls into the matching method. The 15-minute OnTime/Delayed rule is shared.
public static class StatusMapper
{
    public const int OnTimeThresholdMinutes = 15;

    // AeroTrack words: ON_TIME / DELAYED / CANCELLED / DIVERTED / LANDED.
    // It gives actual times too, so when we have them we compute the real delay
    // instead of trusting the word.
    public static UnifiedStatus FromAeroTrack(string rawStatus,
        DateTime? schedDep, DateTime? actualDep, DateTime? schedArr, DateTime? actualArr)
    {
        var s = (rawStatus ?? "").Trim().ToUpperInvariant();

        if (s == "CANCELLED") return UnifiedStatus.Cancelled;
        if (s == "DIVERTED") return UnifiedStatus.Diverted;

        var delay = BiggestDelayMinutes(schedDep, actualDep, schedArr, actualArr);
        if (delay.HasValue)
            return delay.Value > OnTimeThresholdMinutes ? UnifiedStatus.Delayed : UnifiedStatus.OnTime;

        // no usable times - fall back to the reported word
        return s switch
        {
            "ON_TIME" or "LANDED" => UnifiedStatus.OnTime,
            "DELAYED" => UnifiedStatus.Delayed,
            _ => UnifiedStatus.Unknown
        };
    }

    // QuickFlight words: On Schedule / Late / Cancelled / Diverted. Scheduled times
    // only, so there's nothing to compute - we just map the word.
    public static UnifiedStatus FromQuickFlight(string rawState)
    {
        var s = (rawState ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "cancelled" or "canceled" => UnifiedStatus.Cancelled,
            "diverted" => UnifiedStatus.Diverted,
            "on schedule" or "on time" => UnifiedStatus.OnTime,
            "late" or "delayed" => UnifiedStatus.Delayed,
            _ => UnifiedStatus.Unknown
        };
    }

    // Biggest delay across departure and arrival, in minutes. Negative = early.
    // Null when we don't have a scheduled+actual pair to compare.
    private static int? BiggestDelayMinutes(DateTime? schedDep, DateTime? actualDep,
        DateTime? schedArr, DateTime? actualArr)
    {
        int? max = null;

        if (schedDep.HasValue && actualDep.HasValue)
            max = (int)(actualDep.Value - schedDep.Value).TotalMinutes;

        if (schedArr.HasValue && actualArr.HasValue)
        {
            var arrDelay = (int)(actualArr.Value - schedArr.Value).TotalMinutes;
            if (max == null || arrDelay > max) max = arrDelay;
        }

        return max;
    }
}
