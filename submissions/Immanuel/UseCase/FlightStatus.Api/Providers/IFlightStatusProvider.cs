using FlightStatus.Api.Models;

namespace FlightStatus.Api.Providers;

// Every flight data source implements this. The endpoint only ever sees this
// interface - concrete providers are wired up in DI.
public interface IFlightStatusProvider
{
    string Name { get; }

    // Returns null when this provider has no record for the flight/date.
    // May throw if the provider is "down" - the aggregator handles that.
    Task<FlightStatusResult?> GetStatusAsync(string flightNumber, DateOnly date, CancellationToken ct);
}
