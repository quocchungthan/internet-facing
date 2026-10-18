# Goals
> Gathering all the tools that suppose my backbone "daily software work"

## Shared data access

`Farm.Data` can be distributed as a NuGet package independently of this
application. See [the package documentation](../Farm.Data/README.md) for
building, downloading, consuming, and managing the shared database schema.

## Usage and savings dashboard

The Dashboard tab (`/tracker`, including the `/brick` and `/bricks` aliases)
preserves the existing activity tracker, navigation and top-bar sign-in.
An additional analytics block below the tracker shows signed-in users
cost/fees, estimated savings, input/output tokens, elapsed time per task,
payload bytes and a per-task breakdown. The entire analytics block is
absent while signed out or loading the session; no extra sign-in panel
is added to the Dashboard.

`GET /api/dashboard?days=7|30|90` currently returns **explicitly labeled demo
data**, never actual bills or measured savings. The reference cost and fees
are fictional. Audience data is not mocked: the owner receives the Fences
audience endpoint for the configured Farm OIDC client. API failures
are displayed as errors, not replaced with demo data. Responses are private
to the authenticated session and use `Cache-Control: no-store`.

The owner additionally receives read-only user growth and sign-in analytics.
`Dashboard:OwnerSubjects` contains allowed immutable Fences subjects
(`github:<GitHub numeric ID>`). The default identifies the requested GitHub
owner, resolved from the GitHub API. Override with
`Dashboard__OwnerSubjects__0` as needed. Username/email display values do not
grant access, and the API omits the audience payload for every other user.
There are no account-management controls.

The browser reads the owner-only Fences `/api/audience?clientId=...&days=...`
endpoint using the existing Fences SSO cookie. Fences independently authorizes
the owner through `IdentityApp:AudienceOwnerSubjects` (defaulting to the same
owner subject); keep both owner allowlists consistent. Its credentialed CORS
policy allows configured origins and origins of explicitly registered OIDC
redirects. An expired Fences session, missing deployment or blocked cookies
shows an error, never fictional users. Counts are unique observed users of
this client, not the entire SSO population. Tracking begins with the new
Fences deployment; first/last observed authorization days are not original
registration dates, exact login counts or online status.

**Live data already available:** the separate MCP bandwidth section reads
`/api/memory/usage` from Farm.Mind. It covers only instrumented MCP message
bodies, not all AI traffic, tokens or provider billing. A failed request is
shown as a live-data error, not fabricated traffic.

**Path to real analytics:** replace the demo projection in
`DashboardController` with authenticated, subject-scoped persisted task
events. Instrument connected clients/services with a unique task/event ID,
model/provider, UTC start/end, status, provider-reported input/output/cached
tokens, measured input/output bytes and currency. Deduplicate retried events;
record pricing versions and distinguish invoiced costs from estimates and
fixed-price subscriptions. Task elapsed time is not human labor time saved.
Record successful sign-ins at Fences for first/last sign-in and growth;
page views are not sign-ins, and sign-ins are not online presence.
Keep prompts, responses and credentials out of telemetry. Update the API's
source metadata and UI labels together when a metric becomes live, allowing
measured, estimated and unavailable metrics rather than treating missing
values as zero. Automatic external AI usage collection is not implemented;
audience observations are persisted by Fences separately from task demos.

## Raw portfolio API

Farm registers `PortfolioDbContext` using its existing AuraFarming connection
resolution (`ConnectionStrings__AuraFarming` / `POSTGRES_*`) and separate migration
history. Deploy the portfolio migration explicitly as documented in
`Farm.Data/README.md`; Farm does not automatically apply it.

Public GET-only routes under `/api/portfolio`:

| Collection | Single row |
| --- | --- |
| `profiles` | `profiles/{id}` |
| `projects` | `projects/{id}` |
| `skills` | `skills/{id}` |
| `experiences` | `experiences/{id}` |
| `social-links` | `social-links/{id}` |
| `profile-projects` | `profile-projects/{profileId}/{projectId}` |
| `profile-skills` | `profile-skills/{profileId}/{skillId}` |
| `project-skills` | `project-skills/{projectId}/{skillId}` |

Collections return flat arrays ordered by their primary key, with `page=1`
and `pageSize=50` defaults. Valid pages are 1–10000 and sizes 1–100. Invalid
paging/nonpositive keys return 400; missing rows return 404 and empty pages
return `[]`. There are no write routes, joins, counts, or assembled reports.
Responses exclude private `ContactEmail` and all navigation properties.
Enums use MVC's existing numeric JSON convention (database storage is string).
The existing public API tracing/rate limiting applies, including 429 responses.
Only data intended for public publication should be placed in these tables.
Stored SVG is returned as data, never generated or rendered by this API.
