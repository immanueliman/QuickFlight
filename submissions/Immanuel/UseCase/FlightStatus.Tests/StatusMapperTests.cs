using System;
using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;

namespace FlightStatus.Tests;

// Covers the normalisation rules - the 15 minute boundary, cancelled/diverted and
// the vocab mapping for both providers.
public class StatusMapperTests
{
    private static readonly DateTime SchedDep = new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SchedArr = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AeroTrack_within_15_minutes_is_OnTime()
    {
        var status = StatusMapper.FromAeroTrack("ON_TIME",
            SchedDep, SchedDep.AddMinutes(10),
            SchedArr, SchedArr.AddMinutes(12));

        Assert.Equal(UnifiedStatus.OnTime, status);
    }

    [Fact]
    public void AeroTrack_exactly_15_minutes_is_still_OnTime()
    {
        // "within 15 minutes" - 15 should not tip it over
        var status = StatusMapper.FromAeroTrack("ON_TIME",
            SchedDep, SchedDep.AddMinutes(15), null, null);

        Assert.Equal(UnifiedStatus.OnTime, status);
    }

    [Fact]
    public void AeroTrack_beyond_15_minutes_is_Delayed()
    {
        var status = StatusMapper.FromAeroTrack("ON_TIME",
            SchedDep, SchedDep.AddMinutes(40), null, null);

        Assert.Equal(UnifiedStatus.Delayed, status);
    }

    [Fact]
    public void AeroTrack_arrival_delay_counts_even_if_departure_was_fine()
    {
        var status = StatusMapper.FromAeroTrack("ON_TIME",
            SchedDep, SchedDep.AddMinutes(5),
            SchedArr, SchedArr.AddMinutes(50));

        Assert.Equal(UnifiedStatus.Delayed, status);
    }

    [Fact]
    public void AeroTrack_cancelled_word_wins_over_times()
    {
        var status = StatusMapper.FromAeroTrack("CANCELLED", null, null, null, null);
        Assert.Equal(UnifiedStatus.Cancelled, status);
    }

    [Fact]
    public void AeroTrack_diverted_word_is_Diverted()
    {
        var status = StatusMapper.FromAeroTrack("DIVERTED",
            SchedDep, SchedDep.AddMinutes(2), null, null);
        Assert.Equal(UnifiedStatus.Diverted, status);
    }

    [Fact]
    public void AeroTrack_no_times_and_unknown_word_is_Unknown()
    {
        var status = StatusMapper.FromAeroTrack("???", null, null, null, null);
        Assert.Equal(UnifiedStatus.Unknown, status);
    }

    [Theory]
    [InlineData("On Schedule", UnifiedStatus.OnTime)]
    [InlineData("on time", UnifiedStatus.OnTime)]
    [InlineData("Late", UnifiedStatus.Delayed)]
    [InlineData("Cancelled", UnifiedStatus.Cancelled)]
    [InlineData("Diverted", UnifiedStatus.Diverted)]
    [InlineData("gibberish", UnifiedStatus.Unknown)]
    [InlineData("", UnifiedStatus.Unknown)]
    public void QuickFlight_maps_its_vocab(string word, UnifiedStatus expected)
    {
        Assert.Equal(expected, StatusMapper.FromQuickFlight(word));
    }
}
