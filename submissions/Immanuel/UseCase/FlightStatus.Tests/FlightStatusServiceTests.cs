using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;
using FlightStatus.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightStatus.Tests;

// Covers the aggregation/selection logic: newest update wins per flight, single
// provider, nobody responds, a provider blowing up, and route multi-match.
public class FlightStatusServiceTests
{
    // small stub provider we can configure per test
    private class FakeProvider : IFlightStatusProvider
    {
        private readonly IReadOnlyList<FlightStatusResult> _results;
        private readonly bool _throws;

        public FakeProvider(string name, IReadOnlyList<FlightStatusResult>? results, bool throws = false)
        {
            Name = name;
            _results = results ?? Array.Empty<FlightStatusResult>();
            _throws = throws;
        }

        public string Name { get; }

        public Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query, CancellationToken ct)
        {
            if (_throws) throw new Exception("provider down");
            return Task.FromResult(_results);
        }
    }

    private static FlightStatusResult Result(string flight, string source, UnifiedStatus status, DateTime updated) => new()
    {
        FlightNumber = flight,
        Status = status,
        Source = source,
        LastUpdatedUtc = updated
    };

    // a provider that returns a single result
    private static FakeProvider One(string name, FlightStatusResult result) => new(name, new[] { result });

    private static FlightStatusService Build(params IFlightStatusProvider[] providers) =>
        new(providers, NullLogger<FlightStatusService>.Instance);

    private static FlightStatusQuery ByFlight(string flight) =>
        new() { FlightNumber = flight, Date = new DateOnly(2026, 6, 15) };

    private static FlightStatusQuery ByRoute(string from, string to) =>
        new() { FromCode = from, ToCode = to, Date = new DateOnly(2026, 6, 15) };

    [Fact]
    public async Task Picks_the_provider_with_the_latest_update()
    {
        var older = One("AeroTrack",
            Result("BA2490", "AeroTrack", UnifiedStatus.Delayed, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));
        var newer = One("QuickFlight",
            Result("BA2490", "QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 30, 0, DateTimeKind.Utc)));

        var result = await Build(older, newer).GetStatusAsync(ByFlight("BA2490"));

        var single = Assert.Single(result);
        Assert.Equal("QuickFlight", single.Source);
        Assert.Equal(UnifiedStatus.OnTime, single.Status);
    }

    [Fact]
    public async Task Order_of_registration_does_not_matter()
    {
        var newer = One("AeroTrack",
            Result("BA2490", "AeroTrack", UnifiedStatus.Delayed, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)));
        var older = One("QuickFlight",
            Result("BA2490", "QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));

        // newer registered first this time
        var result = await Build(newer, older).GetStatusAsync(ByFlight("BA2490"));

        Assert.Equal("AeroTrack", Assert.Single(result).Source);
    }

    [Fact]
    public async Task Uses_the_only_provider_that_responds()
    {
        var empty = new FakeProvider("QuickFlight", null);
        var has = One("AeroTrack",
            Result("AA100", "AeroTrack", UnifiedStatus.Cancelled, new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc)));

        var result = await Build(empty, has).GetStatusAsync(ByFlight("AA100"));

        var single = Assert.Single(result);
        Assert.Equal("AeroTrack", single.Source);
        Assert.Equal(UnifiedStatus.Cancelled, single.Status);
    }

    [Fact]
    public async Task Returns_empty_when_nobody_responds()
    {
        var result = await Build(
            new FakeProvider("AeroTrack", null),
            new FakeProvider("QuickFlight", null))
            .GetStatusAsync(ByFlight("ZZ999"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_failing_provider_does_not_break_the_lookup()
    {
        var broken = new FakeProvider("AeroTrack", null, throws: true);
        var ok = One("QuickFlight",
            Result("OUTAGE", "QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));

        var result = await Build(broken, ok).GetStatusAsync(ByFlight("OUTAGE"));

        Assert.Equal("QuickFlight", Assert.Single(result).Source);
    }

    [Fact]
    public async Task Empty_when_every_provider_fails()
    {
        var result = await Build(
            new FakeProvider("AeroTrack", null, throws: true),
            new FakeProvider("QuickFlight", null, throws: true))
            .GetStatusAsync(ByFlight("OUTAGE"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task Route_search_returns_every_distinct_flight()
    {
        var a = One("AeroTrack",
            Result("BA112", "AeroTrack", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)));
        var b = One("QuickFlight",
            Result("VS3", "QuickFlight", UnifiedStatus.Delayed, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)));

        var result = await Build(a, b).GetStatusAsync(ByRoute("LHR", "JFK"));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.FlightNumber == "BA112");
        Assert.Contains(result, r => r.FlightNumber == "VS3");
    }

    [Fact]
    public async Task Route_search_dedupes_the_same_flight_across_providers()
    {
        // both providers return BA2490 for this route - should collapse to one, freshest wins
        var older = One("AeroTrack",
            Result("BA2490", "AeroTrack", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));
        var newer = One("QuickFlight",
            Result("BA2490", "QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 30, 0, DateTimeKind.Utc)));

        var result = await Build(older, newer).GetStatusAsync(ByRoute("JFK", "LHR"));

        var single = Assert.Single(result);
        Assert.Equal("QuickFlight", single.Source);
    }
}
