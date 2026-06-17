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
        string from,
        string to,
        DateTime? departure,
        DateTime? arrival,
        DateTime updated);

    private static readonly List<QuickFlightResponse> Data = new()
    {
        // both providers know this - QuickFlight's update is NEWER, so it wins and the
        // card comes back without gate/terminal
        new("BA2490", "On Schedule", "JFK", "LHR",
            new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 45, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 20, 0, DateTimeKind.Utc)),

        // both know QF12 but QuickFlight's update is OLDER than AeroTrack's, so AeroTrack wins
        new("QF12", "Late", "SYD", "LAX",
            new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 15, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)),

        // only QuickFlight knows this one
        new("LH400", "On Schedule", "FRA", "JFK",
            new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 11, 15, 0, DateTimeKind.Utc)),

        // AeroTrack goes down for this one (see OUTAGE), QuickFlight still answers
        new("OUTAGE", "On Schedule", "DXB", "LHR",
            new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc)),

        // shares the LHR->JFK route with AeroTrack's BA112
        new("VS3", "Late", "LHR", "JFK",
            new DateTime(2026, 6, 15, 13, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 16, 45, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc)),
    };

    public Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query, CancellationToken ct)
    {
        IEnumerable<QuickFlightResponse> matches;
        if (query.IsByFlightNumber)
            matches = Data.Where(d => string.Equals(d.flight, query.FlightNumber, StringComparison.OrdinalIgnoreCase));
        else
            matches = Data.Where(d =>
                string.Equals(d.from, query.FromCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.to, query.ToCode, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<FlightStatusResult> results = matches
            .Select(raw => ToResult(raw, query.Date))
            .ToList();

        return Task.FromResult(results);
    }

    private FlightStatusResult ToResult(QuickFlightResponse raw, DateOnly date) => new()
    {
        FlightNumber = raw.flight,
        Date = date.ToString("yyyy-MM-dd"),
        Status = StatusMapper.FromQuickFlight(raw.state),
        OriginCode = raw.from,
        DestinationCode = raw.to,
        ScheduledDeparture = raw.departure,
        ScheduledArrival = raw.arrival,
        // no actual times, gate, terminal or delay reason from this provider
        Source = Name,
        LastUpdatedUtc = raw.updated
    };
}
