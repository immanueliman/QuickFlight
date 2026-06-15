using FlightStatus.Api.Models;

namespace FlightStatus.Api.Providers;

// Stub for the "QuickFlight" source. Minimal - a status word and scheduled times
// only. No gate, terminal or delay reason. Faster but thinner.
public class QuickFlightProvider : IFlightStatusProvider
{
    public string Name => "QuickFlight";

    private record QuickFlightResponse(
        string flight,
        string state,
        DateTime? departure,
        DateTime? arrival,
        DateTime updated);

    private static readonly Dictionary<string, QuickFlightResponse> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        // both providers know this - QuickFlight's update is NEWER, so it wins and the
        // card comes back without gate/terminal
        ["BA2490"] = new("BA2490", "On Schedule",
            new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 45, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 20, 0, DateTimeKind.Utc)),

        // both know QF12 but QuickFlight's update is OLDER than AeroTrack's, so AeroTrack wins
        ["QF12"] = new("QF12", "Late",
            new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 15, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)),

        // only QuickFlight knows this one
        ["LH400"] = new("LH400", "On Schedule",
            new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 11, 15, 0, DateTimeKind.Utc)),

        // AeroTrack goes down for this one (see OUTAGE), QuickFlight still answers
        ["OUTAGE"] = new("OUTAGE", "On Schedule",
            new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc)),
    };

    public Task<FlightStatusResult?> GetStatusAsync(string flightNumber, DateOnly date, CancellationToken ct)
    {
        if (!Data.TryGetValue(flightNumber, out var raw))
            return Task.FromResult<FlightStatusResult?>(null);

        var result = new FlightStatusResult
        {
            FlightNumber = raw.flight,
            Date = date.ToString("yyyy-MM-dd"),
            Status = StatusMapper.FromQuickFlight(raw.state),
            ScheduledDeparture = raw.departure,
            ScheduledArrival = raw.arrival,
            // no actual times, gate, terminal or delay reason from this provider
            Source = Name,
            LastUpdatedUtc = raw.updated
        };

        return Task.FromResult<FlightStatusResult?>(result);
    }
}
