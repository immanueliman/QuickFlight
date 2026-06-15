# Reflection

The brief says to run the evaluator and fix gaps before the timed round. I haven't had
the evaluator's remarks back yet, so this is a self-review against the eight dimensions
plus the trade-offs I made and what I'd change with more time.

## Self-review against the dimensions

- **Analyze** – Assumptions are written in `spec.md` and `README.md` before the code,
  and `spec.md` was committed first. The 15-minute boundary, tie-break and
  "failure = throw" calls are all stated.
- **Architect** – `IFlightStatusProvider` + DI, endpoint has no concrete provider
  types. A third provider is one registration line.
- **Design** – Data model and interface are in `spec.md`, committed before any code.
- **Develop** – Builds and runs; all the demo flights behave as documented.
- **Test** – 25 xUnit tests covering normalisation, the selection logic and endpoint
  validation. The 15-minute boundary and the provider-throws path are both covered.
- **Deploy** – Two `dotnet`/`npm` commands from a clean clone, documented in the README.
- **Operate** – A failing provider is caught and logged as a warning; the request still
  succeeds off the other provider. Info logs show which provider won.
- **Document** – README, spec, prompts and this file are all present.

## Trade-offs I made on purpose

- **Stubs key on flight number, not date.** The data is hardcoded, so threading the
  date through the lookup would add branching for no real benefit. The date is still
  validated and echoed back. If this mattered I'd key the dictionary on
  `(flightNumber, date)`.
- **Delay reason passes through for cancelled/diverted too**, not just delayed. It read
  as useful context (e.g. "Crew shortage", "Diverted to BOS due to fog") and the UI
  only shows it when present.
- **Providers normalise themselves** rather than returning raw DTOs to a central
  normaliser. It keeps each provider's vocabulary knowledge next to its data, and the
  shared rule still lives in one place (`StatusMapper`). The alternative central
  approach would need a common raw shape, which the two providers don't have.

## What I'd do with more time

- Move the API base URL and CORS origin into Angular environment files / config instead
  of a constant.
- Add a couple of Angular component tests (the spec only requires backend test coverage,
  so I prioritised that).
- A `ResultStatus`/`ProblemDetails`-style error contract instead of an ad-hoc
  `{ error: "..." }` body, and timeouts/retries on the provider calls for a real system.
- Run the providers concurrently with `Task.WhenAll`; right now they're awaited in
  sequence, which is fine for two fast stubs but wouldn't be for real network calls.
