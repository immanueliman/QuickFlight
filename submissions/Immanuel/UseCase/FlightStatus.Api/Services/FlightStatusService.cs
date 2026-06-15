using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;

namespace FlightStatus.Api.Services;

// Asks every registered provider, drops the ones that fail or have nothing, and
// picks the freshest answer (latest lastUpdatedUtc). Falls back to Unknown when
// nobody can help.
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

    public async Task<FlightStatusResult> GetStatusAsync(string flightNumber, DateOnly date,
        CancellationToken ct = default)
    {
        var results = new List<FlightStatusResult>();

        foreach (var provider in _providers)
        {
            try
            {
                var r = await provider.GetStatusAsync(flightNumber, date, ct);
                if (r != null)
                {
                    results.Add(r);
                    _logger.LogInformation("{Provider} returned {Status} for {Flight}",
                        provider.Name, r.Status, flightNumber);
                }
                else
                {
                    _logger.LogInformation("{Provider} had no record for {Flight}",
                        provider.Name, flightNumber);
                }
            }
            catch (Exception ex)
            {
                // a provider being down should not fail the whole lookup
                _logger.LogWarning(ex, "{Provider} failed for {Flight}", provider.Name, flightNumber);
            }
        }

        if (results.Count == 0)
        {
            _logger.LogInformation("No provider had data for {Flight} on {Date}", flightNumber, date);
            return new FlightStatusResult
            {
                FlightNumber = flightNumber,
                Date = date.ToString("yyyy-MM-dd"),
                Status = UnifiedStatus.Unknown,
                Message = "No flight data available from any provider."
            };
        }

        // both (or more) responded -> newest lastUpdatedUtc wins
        var winner = results
            .OrderByDescending(r => r.LastUpdatedUtc ?? DateTime.MinValue)
            .First();

        if (results.Count > 1)
            _logger.LogInformation("Picked {Provider} for {Flight} (latest update)",
                winner.Source, flightNumber);

        return winner;
    }
}
