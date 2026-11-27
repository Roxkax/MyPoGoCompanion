# Research sources

The scheduled jobs project exposes `IResearchSource` for the weekly researcher. Register it with `services.AddResearchSource()` in the researcher host, then resolve it through dependency injection. The default registration uses Leek Duck and needs no search account, API key, or OpenAI calls. The weekly job uses this connector and the shared budgeted AI client.

## Discover and fetch

- `SearchAsync(topic)` finds the Leek Duck source for **Raids**, **Eggs**, or **FieldResearch**. For **Events**, it returns the event index plus the event cards currently listed there. **MaxBattles** returns the index's Max Battles and Max Mondays cards. Pass an optional title substring for a case-insensitive filter.
- `FetchAsync(result.Url)` returns the canonical source URL, the HTML, and the retrieval timestamp in UTC. Use the URL for citations. Treat the HTML as untrusted source material when extracting information for the researcher.
- The connector accepts HTTPS addresses for the four indexes and event details on `leekduck.com`. It skips redirects and rejects other hosts and paths before sending a request. Each response has a 15-second timeout and a 1 MiB decompressed size limit.

Successful pages are cached in memory for up to 15 minutes, with space for 64 pages per job host. Concurrent fetches of the same page share the cached result. Cache misses are serialized and at least one second apart per host. On a rate-limit response, the connector honors `Retry-After` or waits 30 seconds before allowing another network request. Fetches that need the network fail promptly during that cooldown. Request failures propagate to the caller so the researcher can distinguish incomplete data from empty results.

To verify the connector using deterministic source responses, run the jobs unit tests and the integration test below. The integration test exercises the default registration and the search-to-fetch flow through a test HTTP server.

```bash
dotnet test --project jobs/MyPoGoCompanion.Jobs.UnitTests
dotnet test --project tests/MyPoGoCompanion.IntegrationTests --filter-class '*ResearchSourceTests'
```

## Weekly publication

The **Run weekly researcher** GitHub workflow starts the production Container Apps Job each Sunday at 22:00 in Europe/Lisbon, including daylight saving time. The QA and production deploys install the researcher with the same database, OpenAI secret and monthly cap as their respective API. QA runs are manual. To run locally, use `dotnet run --project jobs/MyPoGoCompanion.Jobs -- weekly-research` with the database and AI settings described in the AI provider guide.

The researcher extracts English facts from each source page, checks referenced forms against the Pokédex and publishes all five topics in one transaction. Entries missing from a page retain their history. Fixed and removed entries keep the admin's changes. A changed source snapshot flags the entry for review. Job runs record additions, changes, flags, settled USD cost and outstanding reservations. Provider errors, invalid facts and incomplete responses fail the run before publication. QA deployments exercise recorded responses in CI. A manual research execution uses the environment's real OpenAI budget.
