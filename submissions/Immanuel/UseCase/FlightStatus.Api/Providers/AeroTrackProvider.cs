using FlightStatus.Api.Models;

namespace FlightStatus.Api.Providers;

// Stub for the "AeroTrack" source. Verbose, uses its own field names, and carries
// actual times + gate/terminal/delay reason. Data is hardcoded and deterministic.
public class AeroTrackProvider : IFlightStatusProvider
{
    public string Name => "AeroTrack";

    // AeroTrack's raw shape - deliberately different naming from our unified model.
    private record AeroTrackResponse(
        string flightCode,
        string currentStatus,
        DateTime? scheduledDepartureUtc,
        DateTime? actualDepartureUtc,
        DateTime? scheduledArrivalUtc,
        DateTime? actualArrivalUtc,
        string? departureTerminal,
        string? departureGate,
        string? delayReasonText,
        DateTime lastUpdatedUtc);

    private static readonly Dictionary<string, AeroTrackResponse> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        // both providers know this one - AeroTrack's update here is the OLDER one
        ["BA2490"] = new("BA2490", "ON_TIME",
            new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 8, 34, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 45, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 50, 0, DateTimeKind.Utc),
            "2", "B12", null,
            new DateTime(2026, 6, 15, 9, 5, 0, DateTimeKind.Utc)),

        // delayed by 45 min on arrival, has a reason - AeroTrack is the NEWER one here
        ["QF12"] = new("QF12", "DELAYED",
            new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 7, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 15, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            "4", "D2", "Late inbound aircraft",
            new DateTime(2026, 6, 15, 9, 10, 0, DateTimeKind.Utc)),

        ["AA100"] = new("AA100", "CANCELLED",
            new DateTime(2026, 6, 15, 6, 0, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            null,
            "1", null, "Crew shortage",
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc)),

        ["DL55"] = new("DL55", "DIVERTED",
            new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 5, 5, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            null,
            "3", "C9", "Diverted to BOS due to fog",
            new DateTime(2026, 6, 15, 7, 30, 0, DateTimeKind.Utc)),
    };

    public Task<FlightStatusResult?> GetStatusAsync(string flightNumber, DateOnly date, CancellationToken ct)
    {
        // Simulated outage so we can show graceful degradation + logging.
        if (string.Equals(flightNumber, "OUTAGE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AeroTrack upstream timed out");

        if (!Data.TryGetValue(flightNumber, out var raw))
            return Task.FromResult<FlightStatusResult?>(null);

        var status = StatusMapper.FromAeroTrack(raw.currentStatus,
            raw.scheduledDepartureUtc, raw.actualDepartureUtc,
            raw.scheduledArrivalUtc, raw.actualArrivalUtc);

        var result = new FlightStatusResult
        {
            FlightNumber = raw.flightCode,
            Date = date.ToString("yyyy-MM-dd"),
            Status = status,
            ScheduledDeparture = raw.scheduledDepartureUtc,
            ActualDeparture = raw.actualDepartureUtc,
            ScheduledArrival = raw.scheduledArrivalUtc,
            ActualArrival = raw.actualArrivalUtc,
            Terminal = raw.departureTerminal,
            Gate = raw.departureGate,
            DelayReason = raw.delayReasonText,
            Source = Name,
            LastUpdatedUtc = raw.lastUpdatedUtc
        };

        return Task.FromResult<FlightStatusResult?>(result);
    }
}
