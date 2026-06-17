using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FlightStatus.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlightStatus.Tests;

// End-to-end through the real endpoint + real stub providers. Mostly here to prove
// validation works and the wiring is correct.
public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    // the API serialises the status enum as a string, so the client needs to read it back the same way
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Missing_flightNumber_returns_400()
    {
        var resp = await _client.GetAsync("/flights/status?date=2026-06-15");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Missing_date_returns_400()
    {
        var resp = await _client.GetAsync("/flights/status?flightNumber=BA2490");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Bad_date_format_returns_400()
    {
        var resp = await _client.GetAsync("/flights/status?flightNumber=BA2490&date=15-06-2026");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Known_flight_returns_a_result()
    {
        var result = await _client.GetFromJsonAsync<FlightStatusResult>(
            "/flights/status?flightNumber=QF12&date=2026-06-15", Json);

        Assert.NotNull(result);
        // both know QF12 but AeroTrack's update is newer, so it wins with full detail
        Assert.Equal("AeroTrack", result!.Source);
        Assert.Equal(UnifiedStatus.Delayed, result.Status);
        Assert.False(string.IsNullOrEmpty(result.DelayReason));
    }

    [Fact]
    public async Task Unknown_flight_returns_Unknown_not_an_error()
    {
        var resp = await _client.GetAsync("/flights/status?flightNumber=ZZ999&date=2026-06-15");
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<FlightStatusResult>(Json);
        Assert.NotNull(result);
        Assert.Equal(UnifiedStatus.Unknown, result!.Status);
    }

    // --- route search ---

    [Fact]
    public async Task Search_without_from_or_to_returns_400()
    {
        var resp = await _client.GetAsync("/flights/search?to=JFK&date=2026-06-15");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Search_with_bad_date_returns_400()
    {
        var resp = await _client.GetAsync("/flights/search?from=LHR&to=JFK&date=nope");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Search_route_with_two_flights_returns_both()
    {
        // LHR->JFK is served by AeroTrack's BA112 and QuickFlight's VS3
        var results = await _client.GetFromJsonAsync<List<FlightStatusResult>>(
            "/flights/search?from=LHR&to=JFK&date=2026-06-15", Json);

        Assert.NotNull(results);
        Assert.Equal(2, results!.Count);
        Assert.Contains(results, r => r.FlightNumber == "BA112");
        Assert.Contains(results, r => r.FlightNumber == "VS3");
    }

    [Fact]
    public async Task Search_route_known_to_both_for_one_flight_dedupes()
    {
        // JFK->LHR is BA2490 in both providers - one card, QuickFlight (newer) wins
        var results = await _client.GetFromJsonAsync<List<FlightStatusResult>>(
            "/flights/search?from=JFK&to=LHR&date=2026-06-15", Json);

        Assert.NotNull(results);
        var single = Assert.Single(results!);
        Assert.Equal("BA2490", single.FlightNumber);
        Assert.Equal("QuickFlight", single.Source);
    }

    [Fact]
    public async Task Search_unknown_route_returns_empty_list()
    {
        var results = await _client.GetFromJsonAsync<List<FlightStatusResult>>(
            "/flights/search?from=AAA&to=BBB&date=2026-06-15", Json);

        Assert.NotNull(results);
        Assert.Empty(results!);
    }
}
