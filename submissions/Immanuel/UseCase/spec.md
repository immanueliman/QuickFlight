# Flight Status - Spec

Written before any code. This is the data model and the provider interface I'm going
to build against. If something here turns out wrong while coding I'll update it and
note it in reflection.md.

## Unified status

```
OnTime     -> departing/arrived within 15 minutes of schedule
Delayed    -> departure or arrival pushed beyond 15 minutes
Cancelled  -> flight will not operate
Diverted   -> flight landed at a different airport
Unknown    -> provider returned nothing usable
```

The 15 minute threshold is the rule for OnTime vs Delayed. AeroTrack gives actual
times so we can actually compute the delay. QuickFlight only gives scheduled times,
so for QuickFlight we fall back to mapping its status word.

## Unified result model (what the API returns)

`FlightStatusResult`

| field              | type            | notes                                              |
|--------------------|-----------------|----------------------------------------------------|
| flightNumber       | string          | echoed back from the request                       |
| date               | string (date)   | yyyy-MM-dd                                          |
| status             | UnifiedStatus   | enum, serialised as string                         |
| scheduledDeparture | DateTime?       | UTC                                                 |
| actualDeparture    | DateTime?       | UTC, AeroTrack only                                |
| scheduledArrival   | DateTime?       | UTC                                                 |
| actualArrival      | DateTime?       | UTC, AeroTrack only                                |
| terminal           | string?         | AeroTrack only, hidden when null                   |
| gate               | string?         | AeroTrack only, hidden when null                   |
| delayReason        | string?         | AeroTrack only, present when delayed                |
| source             | string?         | which provider the result came from                |
| lastUpdatedUtc     | DateTime?       | used to pick the winner when both respond           |
| message            | string?         | filled for Unknown / nothing-found cases            |

Nullable fields that come back null are dropped from the JSON so the UI can just
check "is it there". That covers the "shown when present, hidden when absent" rule.

## Provider abstraction

```csharp
public interface IFlightStatusProvider
{
    string Name { get; }
    Task<FlightStatusResult?> GetStatusAsync(string flightNumber, DateOnly date, CancellationToken ct);
}
```

- Returns `null` when the provider has no record for that flight/date.
- May throw if the provider "fails" - the aggregator catches that and treats the
  provider as not responding (and logs it).
- Each provider does its own normalisation internally because each one speaks a
  different status vocabulary. The shared mapping helper lives in `StatusMapper`.

Two concrete stubs, both deterministic (hardcoded responses):
- `AeroTrackProvider`  - verbose, own field names, has actual times + gate/terminal/reason
- `QuickFlightProvider` - minimal, scheduled times + a status word only

## Selection logic (aggregator)

1. Ask every registered provider.
2. Drop the ones that threw (log them) or returned null.
3. If two+ results -> keep the one with the latest `lastUpdatedUtc`.
4. If exactly one -> use it.
5. If none -> return `Unknown` with a message.

## Provider vocab mapping

AeroTrack words: `ON_TIME`, `DELAYED`, `CANCELLED`, `DIVERTED`, `LANDED`.
QuickFlight words: `On Schedule`, `Late`, `Cancelled`, `Diverted`.

Mapping rule (same function for both, vocab passed in):
- cancelled word    -> Cancelled
- diverted word     -> Diverted
- if actual + scheduled times present -> compute biggest delay, >15 min = Delayed else OnTime
- else map the word: on-time/landed/on schedule -> OnTime, delayed/late -> Delayed
- anything else / no word -> Unknown

## Endpoint

```
GET /flights/status?flightNumber={code}&date={yyyy-MM-dd}
```

- `flightNumber` and `date` both required -> 400 if missing.
- bad date format -> 400.
- otherwise 200 with a `FlightStatusResult` (status can be Unknown).

---

## Enhancement: search by route (added after the first cut)

Some agents don't have the flight number but know the airports. A lookup can now be
done by flight number **or** by route (from + to airport codes). The original
flight-number behaviour is unchanged.

### Query object

The provider interface now takes one query object instead of loose params:

```csharp
public class FlightStatusQuery
{
    public string? FlightNumber { get; init; }
    public string? FromCode { get; init; }
    public string? ToCode { get; init; }
    public DateOnly Date { get; init; }
    public bool IsByFlightNumber => !string.IsNullOrWhiteSpace(FlightNumber);
    public bool IsByRoute => !string.IsNullOrWhiteSpace(FromCode) && !string.IsNullOrWhiteSpace(ToCode);
}

public interface IFlightStatusProvider
{
    string Name { get; }
    Task<IReadOnlyList<FlightStatusResult>> GetStatusAsync(FlightStatusQuery query, CancellationToken ct);
}
```

A provider returns an empty list for "no match", 0..1 for a flight-number lookup, and
0..N for a route lookup.

### New result fields

`FlightStatusResult` gains `originCode` and `destinationCode` (IATA airport codes,
e.g. `LHR`, `JFK`). Used for route search and shown on the card.

### Generalised selection

The aggregator now returns a **list**: it gathers every provider's matches, groups by
flight number, and keeps the freshest provider's version of each flight (the same
latest-`lastUpdatedUtc` rule as before). A flight-number lookup is just the special
case of one group.

### Endpoints

```
GET /flights/status?flightNumber={code}&date={yyyy-MM-dd}   (unchanged - single object)
GET /flights/search?from={IATA}&to={IATA}&date={yyyy-MM-dd}  (new - array of results)
```

- `/flights/search`: `from`, `to`, `date` required -> 400 if missing/bad.
- Returns a JSON array of matching flights (empty array if none on that route).
- Kept as a separate endpoint so the existing single-object response is untouched.

### Assumptions

- Airport codes are 3-letter IATA, matched case-insensitively.
- If a route matches several flights, all are returned (sorted by scheduled departure).
- The two endpoints are separate rather than one endpoint returning different shapes,
  so the original `/flights/status` contract doesn't change.
