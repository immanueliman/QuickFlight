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
        string origin,
        string destination,
        DateTime? scheduledDepartureUtc,
        DateTime? actualDepartureUtc,
        DateTime? scheduledArrivalUtc,
        DateTime? actualArrivalUtc,
        string? departureTerminal,
        string? departureGate,
        string? delayReasonText,
        DateTime lastUpdatedUtc);

    private static readonly List<AeroTrackResponse> Data = new()
    {
        // both providers know this one - AeroTrack's update here is the OLDER one
        new("BA2490", "ON_TIME", "JFK", "LHR",
            new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 8, 34, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 45, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 50, 0, DateTimeKind.Utc),
            "2", "B12", null,
            new DateTime(2026, 6, 15, 9, 5, 0, DateTimeKind.Utc)),

        // delayed by 45 min on arrival, has a reason - AeroTrack is the NEWER one here
        new("QF12", "DELAYED", "SYD", "LAX",
            new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 7, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 9, 15, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            "4", "D2", "Late inbound aircraft",
            new DateTime(2026, 6, 15, 9, 10, 0, DateTimeKind.Utc)),

        new("AA100", "CANCELLED", "ORD", "MIA",
            new DateTime(2026, 6, 15, 6, 0, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            null,
            "1", null, "Crew shortage",
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc)),

        new("DL55", "DIVERTED", "ATL", "BOS",
            new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 5, 5, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            null,
            "3", "C9", "Diverted to BOS due to fog",
            new DateTime(2026, 6, 15, 7, 30, 0, DateTimeKind.Utc)),

        // shares the LHR->JFK route with QuickFlight's VS3 - so a route search returns
        // more than one flight
        new("BA112", "ON_TIME", "LHR", "JFK",
            new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 11, 8, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 5, 0, DateTimeKind.Utc),
            "5", "A1", null,
            new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc)),
    };

    public Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query, CancellationToken ct)
    {
        // Simulated outage so we can show graceful degradation + logging.
        if (string.Equals(query.FlightNumber, "OUTAGE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AeroTrack upstream timed out");

        IEnumerable<AeroTrackResponse> matches;
        if (query.IsByFlightNumber)
            matches = Data.Where(d => string.Equals(d.flightCode, query.FlightNumber, StringComparison.OrdinalIgnoreCase));
        else
            matches = Data.Where(d =>
                string.Equals(d.origin, query.FromCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.destination, query.ToCode, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<FlightStatusResult> results = matches
            .Select(raw => ToResult(raw, query.Date))
            .ToList();

        return Task.FromResult(results);
    }

    // Translate AeroTrack's raw shape into our unified model.
    private FlightStatusResult ToResult(AeroTrackResponse raw, DateOnly date)
    {
        var status = StatusMapper.FromAeroTrack(raw.currentStatus,
            raw.scheduledDepartureUtc, raw.actualDepartureUtc,
            raw.scheduledArrivalUtc, raw.actualArrivalUtc);

        return new FlightStatusResult
        {
            FlightNumber = raw.flightCode,
            Date = date.ToString("yyyy-MM-dd"),
            Status = status,
            OriginCode = raw.origin,
            DestinationCode = raw.destination,
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
    }
}
