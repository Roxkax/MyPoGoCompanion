# AI provider and spending cap

The .NET `IAiClient` gives the app's chat and weekly researcher a shared text generation interface. Callers supply a task purpose and complexity. Simple requests use the pinned GPT-5 nano snapshot and standard requests use the pinned GPT-5 mini snapshot. Both use the OpenAI Responses API with response storage disabled. The provider reports input, cached input, and output tokens, which the app prices at the [nano](https://developers.openai.com/api/docs/models/gpt-5-nano) and [mini](https://developers.openai.com/api/docs/models/gpt-5-mini) standard rates. The ledger stores model, counts, cost, purpose, and optional user or job IDs. Prompts, replies, and provider error bodies are excluded from the ledger.

## Configure an environment

- In an API development checkout, add `Ai:OpenAiApiKey` in .NET user secrets. On GitHub, set the `OPENAI_API_KEY` secret in the intended `qa` or `prod` environment. Use a dedicated OpenAI project key for each deployment you enable. Leaving the key unset disables AI calls.
- The API defaults to a **$5 USD monthly cap** per deployment database. Optionally set the GitHub environment variable `AI_MONTHLY_CAP_USD` (for example `5.00`) or the host setting `Ai__MonthlyCapUsd`. Set it to `0` to pause all requests. When the researcher is deployed, register the same AI services in its host and give it the same database and cap as that environment's API.
- Set quotas in the OpenAI project for any other applications using the same account. The app cap applies to calls through `IAiClient` using the configured database and prices.

## Check spend and pauses

Sign in as an admin and call `GET /admin/ai/budget`. The response shows the cap, settled cost, pending and uncertain reservations, the next UTC reset, and whether the provider is configured. The pause flags indicate whether the minimum output request for each model fits. Larger requests can reach the cap earlier. A budget refusal logs a warning and returns the reset time to its caller.

Before sending a request, the app reserves the most it could cost at the selected model's context limit and requested output limit. PostgreSQL serializes that step across callers. Once OpenAI responds with valid usage, the app replaces the reservation with the actual priced usage, rounded up to the nearest millionth of a dollar. A definitive rejection releases the reservation. If a timeout, cancellation, interrupted process, or invalid response leaves the charge uncertain, the reservation stays in the UTC month when the call started. The new month's cap becomes available at midnight UTC on the first day.
