using System.Globalization;
using System.Text.Json.Serialization;
using FlightStatus.Api.Models;
using FlightStatus.Api.Providers;
using FlightStatus.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Providers are registered against the interface - the endpoint never sees the
// concrete types. Adding a third provider is just one more line here.
builder.Services.AddSingleton<IFlightStatusProvider, AeroTrackProvider>();
builder.Services.AddSingleton<IFlightStatusProvider, QuickFlightProvider>();
builder.Services.AddSingleton<FlightStatusService>();

// enums as strings in the JSON, and skip null fields so the UI can hide them
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// let the Angular dev server call us
const string CorsPolicy = "ui";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.AllowAnyHeader().AllowAnyMethod().WithOrigins("http://localhost:4200")));

var app = builder.Build();
app.UseCors(CorsPolicy);

app.MapGet("/", () => "Flight Status API. Try /flights/status?flightNumber=BA2490&date=2026-06-15 "
    + "or /flights/search?from=LHR&to=JFK&date=2026-06-15");

// Look up a single known flight by number. Returns one result (Unknown if nobody has it).
app.MapGet("/flights/status", async (string? flightNumber, string? date,
    FlightStatusService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(flightNumber))
        return Results.BadRequest(new { error = "flightNumber is required" });

    if (!TryParseDate(date, out var parsedDate, out var dateError))
        return Results.BadRequest(new { error = dateError });

    var query = new FlightStatusQuery { FlightNumber = flightNumber.Trim(), Date = parsedDate };
    var matches = await service.GetStatusAsync(query, ct);

    if (matches.Count == 0)
        return Results.Ok(new FlightStatusResult
        {
            FlightNumber = flightNumber.Trim(),
            Date = parsedDate.ToString("yyyy-MM-dd"),
            Status = UnifiedStatus.Unknown,
            Message = "No flight data available from any provider."
        });

    return Results.Ok(matches[0]);
});

// Find flights by route when the agent doesn't have a flight number. Returns a list
// (possibly empty) of the matching flights' statuses.
app.MapGet("/flights/search", async (string? from, string? to, string? date,
    FlightStatusService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        return Results.BadRequest(new { error = "from and to airport codes are required" });

    if (!TryParseDate(date, out var parsedDate, out var dateError))
        return Results.BadRequest(new { error = dateError });

    var query = new FlightStatusQuery { FromCode = from.Trim(), ToCode = to.Trim(), Date = parsedDate };
    var matches = await service.GetStatusAsync(query, ct);

    return Results.Ok(matches);
});

app.Run();

// shared date validation for both endpoints
static bool TryParseDate(string? date, out DateOnly parsed, out string error)
{
    parsed = default;
    error = "";
    if (string.IsNullOrWhiteSpace(date)) { error = "date is required"; return false; }
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out parsed))
    {
        error = "date must be in yyyy-MM-dd format";
        return false;
    }
    return true;
}

// exposed so the test project (WebApplicationFactory) can boot the app
public partial class Program { }
