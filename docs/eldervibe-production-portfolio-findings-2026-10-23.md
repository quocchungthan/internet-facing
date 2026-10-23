# ElderVibe production portfolio findings - 2026-10-23

## Findings

- The TGR Vietnam experience uses `EmploymentType=Fulltime`. `Farm.Sync/reader/portfolio.mjs` maps experiences through `mapExperience` and `propertyReader.oneOf`, which expects `FullTime` as defined by `Farm.Data/Entities/EmploymentEnums.cs`. The mismatched value is rejected, so this experience is not imported.
- The fourth active gig page has no `Kind`. `Farm.Sync/reader/export.mjs` silently omits it; the bodies of the other three gig pages match.
- Public `GET https://eldervibe.dev/api/portfolio/experiences` returns `200 []`; `GET https://eldervibe.dev/api/portfolio/gigs` returns `404`.
- The public portfolio API currently exposes 1 profile, 2 projects, 0 experiences, 10 skills, and 2 social links.
- The Farm controller on `sub/note` includes `GetGigs`; `origin/main` does not. The deployed Farm API therefore needs a revision containing that endpoint.
- The production image was created at `2026-10-23 04:25:56`.

## Recommended actions

1. Normalize the experience employment type to `FullTime`.
2. Add the required `Kind` property to the fourth active gig page.
3. Deploy the Farm API revision that includes `GetGigs`.

No screenshot was captured; this was kept docs-only, without changing the app or deployment.
