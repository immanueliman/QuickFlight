namespace FlightStatus.Api.Models;

// A lookup can be done two ways: by flight number, or by route (from + to airport
// codes). Either way a date is supplied. Providers inspect this to decide how to search.
public class FlightStatusQuery
{
    public string? FlightNumber { get; init; }
    public string? FromCode { get; init; }
    public string? ToCode { get; init; }
    public DateOnly Date { get; init; }

    public bool IsByFlightNumber => !string.IsNullOrWhiteSpace(FlightNumber);
    public bool IsByRoute => !string.IsNullOrWhiteSpace(FromCode) && !string.IsNullOrWhiteSpace(ToCode);
}
