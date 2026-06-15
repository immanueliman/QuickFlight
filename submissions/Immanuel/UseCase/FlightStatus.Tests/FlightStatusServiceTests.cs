using System;
using System.Threading;
using System.Threading.Tasks;
using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;
using FlightStatus.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightStatus.Tests;

// Covers the aggregation/selection logic: newest update wins, single provider,
// nobody responds, and a provider blowing up.
public class FlightStatusServiceTests
{
    // small stub provider we can configure per test
    private class FakeProvider : IFlightStatusProvider
    {
        private readonly FlightStatusResult? _result;
        private readonly bool _throws;

        public FakeProvider(string name, FlightStatusResult? result, bool throws = false)
        {
            Name = name;
            _result = result;
            _throws = throws;
        }

        public string Name { get; }

        public Task<FlightStatusResult?> GetStatusAsync(string flightNumber, DateOnly date, CancellationToken ct)
        {
            if (_throws) throw new Exception("provider down");
            return Task.FromResult(_result);
        }
    }

    private static FlightStatusResult Result(string source, UnifiedStatus status, DateTime updated) => new()
    {
        FlightNumber = "BA2490",
        Status = status,
        Source = source,
        LastUpdatedUtc = updated
    };

    private static FlightStatusService Build(params IFlightStatusProvider[] providers) =>
        new(providers, NullLogger<FlightStatusService>.Instance);

    private static readonly DateOnly Date = new(2026, 6, 15);

    [Fact]
    public async Task Picks_the_provider_with_the_latest_update()
    {
        var older = new FakeProvider("AeroTrack",
            Result("AeroTrack", UnifiedStatus.Delayed, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));
        var newer = new FakeProvider("QuickFlight",
            Result("QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 30, 0, DateTimeKind.Utc)));

        var service = Build(older, newer);
        var result = await service.GetStatusAsync("BA2490", Date);

        Assert.Equal("QuickFlight", result.Source);
        Assert.Equal(UnifiedStatus.OnTime, result.Status);
    }

    [Fact]
    public async Task Order_of_registration_does_not_matter()
    {
        var newer = new FakeProvider("AeroTrack",
            Result("AeroTrack", UnifiedStatus.Delayed, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)));
        var older = new FakeProvider("QuickFlight",
            Result("QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));

        // newer registered first this time
        var result = await Build(newer, older).GetStatusAsync("BA2490", Date);

        Assert.Equal("AeroTrack", result.Source);
    }

    [Fact]
    public async Task Uses_the_only_provider_that_responds()
    {
        var empty = new FakeProvider("QuickFlight", null);
        var has = new FakeProvider("AeroTrack",
            Result("AeroTrack", UnifiedStatus.Cancelled, new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc)));

        var result = await Build(empty, has).GetStatusAsync("AA100", Date);

        Assert.Equal("AeroTrack", result.Source);
        Assert.Equal(UnifiedStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Returns_Unknown_with_message_when_nobody_responds()
    {
        var result = await Build(
            new FakeProvider("AeroTrack", null),
            new FakeProvider("QuickFlight", null))
            .GetStatusAsync("ZZ999", Date);

        Assert.Equal(UnifiedStatus.Unknown, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal("ZZ999", result.FlightNumber);
    }

    [Fact]
    public async Task A_failing_provider_does_not_break_the_lookup()
    {
        var broken = new FakeProvider("AeroTrack", null, throws: true);
        var ok = new FakeProvider("QuickFlight",
            Result("QuickFlight", UnifiedStatus.OnTime, new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc)));

        var result = await Build(broken, ok).GetStatusAsync("OUTAGE", Date);

        Assert.Equal("QuickFlight", result.Source);
        Assert.Equal(UnifiedStatus.OnTime, result.Status);
    }

    [Fact]
    public async Task Unknown_when_every_provider_fails()
    {
        var result = await Build(
            new FakeProvider("AeroTrack", null, throws: true),
            new FakeProvider("QuickFlight", null, throws: true))
            .GetStatusAsync("OUTAGE", Date);

        Assert.Equal(UnifiedStatus.Unknown, result.Status);
    }
}
