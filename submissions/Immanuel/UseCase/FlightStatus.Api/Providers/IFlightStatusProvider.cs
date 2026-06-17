using FlightStatus.Api.Models;

namespace FlightStatus.Api.Providers;

// Every flight data source implements this. The endpoint only ever sees this
// interface - concrete providers are wired up in DI.
public interface IFlightStatusProvider
{
    string Name { get; }

    // Looks up by flight number or by route, depending on the query. Returns an empty
    // list when this provider has no match (a flight-number query yields 0 or 1; a
    // route query can yield several). May throw if the provider is "down" - the
    // aggregator handles that.
    Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query, CancellationToken ct);
}
