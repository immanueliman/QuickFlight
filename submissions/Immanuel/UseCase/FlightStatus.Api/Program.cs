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

app.MapGet("/", () => "Flight Status API. Try /flights/status?flightNumber=BA2490&date=2026-06-15");

app.MapGet("/flights/status", async (string? flightNumber, string? date,
    FlightStatusService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(flightNumber))
        return Results.BadRequest(new { error = "flightNumber is required" });

    if (string.IsNullOrWhiteSpace(date))
        return Results.BadRequest(new { error = "date is required" });

    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsedDate))
        return Results.BadRequest(new { error = "date must be in yyyy-MM-dd format" });

    var result = await service.GetStatusAsync(flightNumber.Trim(), parsedDate, ct);
    return Results.Ok(result);
});

app.Run();

// exposed so the test project (WebApplicationFactory) can boot the app
public partial class Program { }
