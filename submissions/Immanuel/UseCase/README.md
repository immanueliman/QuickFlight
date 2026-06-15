# Flight Status

A small full-stack feature for the SkyRoute support tool. A support agent types a
flight number and a date, the backend asks two flight data providers, normalises both
into one status model, and the UI shows the result with colour coding.

- **Backend** – .NET 9 Minimal API
- **Frontend** – Angular 19
- **Tests** – xUnit

## Layout

```
UseCase/
├── README.md
├── spec.md                 # data model + interfaces (written before the code)
├── FlightStatus.sln
├── FlightStatus.Api/        # minimal API, providers, normalisation, aggregator
├── FlightStatus.Tests/      # xUnit - normalisation + selection + endpoint tests
├── flight-status-ui/        # Angular app
├── prompts.md               # AI prompts used + what I kept/dropped
└── reflection.md            # self-review and trade-offs
```

## How to run

You need the **.NET 9 SDK** and **Node 18+** (built on Node 22).

### 1. Backend

```bash
cd FlightStatus.Api
dotnet run
```

It listens on `http://localhost:5279`. Quick check:

```
http://localhost:5279/flights/status?flightNumber=BA2490&date=2026-06-15
```

### 2. Frontend

In a second terminal:

```bash
cd flight-status-ui
npm install      # first time only
npm start
```

Open `http://localhost:4200`. The UI calls the API on port 5279 (CORS is already
allowed for `localhost:4200`).

### 3. Tests

```bash
dotnet test
```

## Try these flights (date 2026-06-15)

The stub providers are deterministic. Lookup is by flight number; the date is echoed
back but not used to pick the record.

| Flight   | What it shows                                                            |
|----------|--------------------------------------------------------------------------|
| `BA2490` | Both providers respond. QuickFlight's update is newer, so it wins → **OnTime**, minimal card (no gate/terminal). |
| `QF12`   | Both respond. AeroTrack's update is newer → **Delayed** with gate, terminal and delay reason. |
| `AA100`  | AeroTrack only → **Cancelled**.                                          |
| `DL55`   | AeroTrack only → **Diverted**.                                           |
| `LH400`  | QuickFlight only → **OnTime**.                                           |
| `OUTAGE` | AeroTrack throws (simulated outage); QuickFlight still answers → graceful fallback, logged as a warning. |
| `ZZ999`  | Nobody has it → **Unknown** with a message.                             |

## Architecture decisions

- **Provider abstraction.** `IFlightStatusProvider` has two stub implementations
  (`AeroTrackProvider`, `QuickFlightProvider`). They're registered in DI against the
  interface, so the endpoint never names a concrete type. Adding a third provider is
  one line in `Program.cs`.
- **Normalisation lives in `StatusMapper`.** Each provider speaks its own status
  vocabulary, so each calls the matching mapper method. The 15-minute OnTime/Delayed
  rule is shared. AeroTrack gives actual times so the delay is computed; QuickFlight
  only has a status word so it's mapped directly.
- **Selection in `FlightStatusService`.** It queries every provider, drops the ones
  that fail or have nothing, and keeps the result with the latest `lastUpdatedUtc`.
  One provider → that one. None → `Unknown` with a message.
- **Provider failures don't fail the request.** A throwing provider is caught and
  logged as a warning; the other provider's answer is still used.
- **Null fields are dropped from the JSON** (`WhenWritingNull`), so the AeroTrack-only
  fields (gate, terminal, delay reason) simply aren't there for QuickFlight results,
  and the UI shows them only when present.

See `spec.md` for the full data model and `reflection.md` for trade-offs and
assumptions.

## Assumptions

- "Within 15 minutes" is inclusive: a 15-minute delay is still **OnTime**, 16+ is
  **Delayed**.
- For AeroTrack, the computed delay is authoritative over its status word for
  OnTime vs Delayed, but an explicit `CANCELLED`/`DIVERTED` word always wins.
- All provider times are UTC.
- A provider "failing" means it throws; that's treated the same as no response.
- The stubs ignore the date for lookups (data is hardcoded) but echo it back in the
  result.

## Copilot usage

GitHub Copilot / chat was used throughout for scaffolding, the normalisation logic,
test cases and this documentation. Every significant prompt and whether I accepted or
changed the output is recorded in `prompts.md`.
