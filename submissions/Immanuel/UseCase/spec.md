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
