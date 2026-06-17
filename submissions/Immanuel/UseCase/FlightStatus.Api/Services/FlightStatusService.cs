using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;

namespace FlightStatus.Api.Services;

// Asks every registered provider, drops the ones that fail, then collapses the
// combined results: one card per distinct flight, keeping the freshest provider's
// version (latest lastUpdatedUtc). Works the same for a single-flight lookup (one
// card out) and a route search (several cards out).
public class FlightStatusService
{
    private readonly IEnumerable<IFlightStatusProvider> _providers;
    private readonly ILogger<FlightStatusService> _logger;

    public FlightStatusService(IEnumerable<IFlightStatusProvider> providers,
        ILogger<FlightStatusService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query,
        CancellationToken ct = default)
    {
        var all = new List<FlightStatusResult>();

        foreach (var provider in _providers)
        {
            try
            {
                var results = await provider.GetStatusAsync(query, ct);
                all.AddRange(results);
                _logger.LogInformation("{Provider} returned {Count} result(s)", provider.Name, results.Count);
            }
            catch (Exception ex)
            {
                // a provider being down should not fail the whole lookup
                _logger.LogWarning(ex, "{Provider} failed for query {Query}", provider.Name, Describe(query));
            }
        }

        // one card per flight - if both providers returned the same flight, keep the
        // one with the newest lastUpdatedUtc. Ordered by departure for a stable display.
        var deduped = all
            .GroupBy(r => r.FlightNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedUtc ?? DateTime.MinValue).First())
            .OrderBy(r => r.ScheduledDeparture ?? DateTime.MaxValue)
            .ToList();

        _logger.LogInformation("Query {Query} -> {Count} flight(s)", Describe(query), deduped.Count);
        return deduped;
    }

    private static string Describe(FlightStatusQuery q) =>
        q.IsByFlightNumber ? $"flight {q.FlightNumber}" : $"route {q.FromCode}->{q.ToCode}";
}
