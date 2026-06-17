namespace FlightStatus.Api.Models;

// Unified result returned by the API. The AeroTrack-only fields (terminal, gate,
// delayReason) stay null when a provider can't supply them - they get dropped from
// the JSON so the UI just checks whether they're there.
public class FlightStatusResult
{
    public string FlightNumber { get; set; } = "";
    public string Date { get; set; } = "";

    public UnifiedStatus Status { get; set; } = UnifiedStatus.Unknown;

    // Route - origin/destination airport codes (IATA). Used for route search and
    // shown on the card so the agent can confirm the right flight.
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }

    public DateTime? ScheduledDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }

    public string? Terminal { get; set; }
    public string? Gate { get; set; }
    public string? DelayReason { get; set; }

    // Which provider this came from, and when that provider last updated it.
    public string? Source { get; set; }
    public DateTime? LastUpdatedUtc { get; set; }

    // Used for the Unknown / nothing-found case.
    public string? Message { get; set; }
}
