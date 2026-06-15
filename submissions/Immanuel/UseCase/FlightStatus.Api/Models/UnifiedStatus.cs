namespace FlightStatus.Api.Models;

// The single status vocabulary both providers get mapped onto.
public enum UnifiedStatus
{
    OnTime,
    Delayed,
    Cancelled,
    Diverted,
    Unknown
}
