# AI prompts

These are the prompts I leaned on Copilot / chat for, roughly in order, with a note on
what I kept and what I changed. I didn't log trivial autocomplete (imports, getters,
obvious one-liners) - only the prompts that actually shaped a decision.

---

**1. "Read this brief and list the assumptions I should pin down before writing any
code - status thresholds, tie-breaks, what counts as a provider failure."**

Used the answer to write `spec.md`. Kept most of it. The one I made a firm call on
myself: whether exactly 15 minutes is OnTime or Delayed - I went inclusive (15 =
OnTime) and wrote a test for that boundary.

**2. "Design a provider abstraction for two flight data sources that normalise to one
status model, injected via DI in a .NET minimal API. Keep the endpoint free of
concrete types."**

This gave me `IFlightStatusProvider` returning the unified model and registering both
stubs against the interface. Accepted the shape. I added `Name` to the interface
because the aggregator needs it for logging and for the `source` field.

**3. "Write a normaliser that maps two different provider status vocabularies onto one
enum, using a 15-minute rule, where one provider has actual times and the other
doesn't."**

First draft put a separate copy of the delay maths in each provider. I rejected that
and pulled it into one shared `StatusMapper` with a `BiggestDelayMinutes` helper, so
the rule lives in exactly one place.

**4. "Generate deterministic stub data for AeroTrack (verbose, own field names, gate /
terminal / delay reason) and QuickFlight (minimal). Make sure at least one flight is
known to both so the lastUpdatedUtc tie-break actually gets exercised."**

Kept the dataset idea. I tuned the timestamps by hand so `BA2490` is won by QuickFlight
and `QF12` by AeroTrack - that way the demo shows both the minimal and the full card.
I also added the `OUTAGE` flight myself to show graceful degradation.

**5. "Write xUnit tests for the normalisation rules and the provider selection logic -
cover the 15-minute boundary, cancelled/diverted, latest-update-wins, single provider,
nobody responds, and a provider that throws."**

Took these mostly as-is. Copilot suggested an exactly-15-minutes case which I'd
overlooked - kept it. Added the registration-order test myself so the tie-break isn't
accidentally depending on list order.

**6. "Suggest an endpoint integration test using WebApplicationFactory."**

First attempt failed at runtime: the API serialises the enum as a string but the test
client deserialised with default options. Copilot's fix was to give the test client a
`JsonStringEnumConverter`, which I applied.

**7. "Build an Angular standalone component: search form (flight number + date),
colour-coded result card, AeroTrack-only fields hidden when absent, error state."**

Used the structure. I trimmed the styling down - the first version was heavier than
this feature needs. Colour mapping (green/amber/red/grey) is done with a small
`statusClass()` method rather than inline so it's easy to read.

**8. "Draft the README and reflection."**

Generated a first pass, then rewrote the assumptions and trade-offs in my own words so
they match what the code actually does.
