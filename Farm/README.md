# Goals
> Gathering all the tools that suppose my backbone "daily software work"

## Shared data access

`Farm.Data` can be distributed as a NuGet package independently of this
application. See [the package documentation](../Farm.Data/README.md) for
building, downloading, consuming, and managing the shared database schema.

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
