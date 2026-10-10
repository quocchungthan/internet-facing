# Farm.Data

Reusable .NET 10 data access for the AuraFarming PostgreSQL database. The
package contains the `AuraFarming` EF Core context, entities, model mappings,
and migrations. It does not depend on the Farm web/business layer.

## Portfolio schema

The same `Farm.Data` package also contains the eight portfolio entities in
`Farm.Data.Entities` and `PortfolioDbContext`. This context uses the same
AuraFarming PostgreSQL connection, owns only the portfolio tables, and has
its own `__PortfolioMigrationsHistory` table. Register it with
`UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__PortfolioMigrationsHistory"))`.
It has no seed data or automatic migration on startup. `AvatarSvg` is stored
as PostgreSQL `text`; no avatar generation is included. Employment enums are
stored as strings, and dates use PostgreSQL `date`.

The designated migrator can generate/review a SQL script without connecting:

```powershell
dotnet ef migrations script --project Farm.Data --context PortfolioDbContext --output portfolio.sql
```

For an approved schema deployment only, supply `ConnectionStrings__AuraFarming`
through secret configuration and run:

```powershell
dotnet ef database update --project Farm.Data --context PortfolioDbContext
```

Portfolio migrations and snapshot live in `Migrations/Portfolio`; existing
AuraFarming migrations are unchanged. Applying the schema does not import
source sample data.

## Build and publish

Build a package locally from the repository root:

```powershell
dotnet pack .\Farm.Data\Farm.Data.csproj --configuration Release --output .\artifacts\nuget -p:PackageVersion=1.0.0
```

The **Package Farm.Data** GitHub Actions workflow publishes to the
`quocchungthan` GitHub Packages NuGet feed whenever relevant changes reach
`main`, using its built-in `GITHUB_TOKEN` with `packages: write` permission. Automatic
builds use unique `1.0.0-ci.<run>.<attempt>` prerelease versions. Run the workflow
manually with an explicit version for a release package. The workflow also
uploads a `Farm.Data-nuget` artifact, retained for 90 days.

Feed URL: `https://nuget.pkg.github.com/quocchungthan/index.json`.
Use a new version for each release; publishing an existing version fails.
Coordinate package versions with database schema changes.

## Consume from another solution

Use a .NET 10 application. GitHub's NuGet registry requires authentication,
including for public packages. Create a personal access token (classic) with
`read:packages` permission and access to the package. Supply it through the
`GITHUB_PACKAGES_TOKEN` environment variable, not source-controlled files.
Register the feed and install the exact published package version:

```powershell
dotnet nuget add source https://nuget.pkg.github.com/quocchungthan/index.json --name FarmDataGitHub --username YOUR_GITHUB_USERNAME --password $env:GITHUB_PACKAGES_TOKEN
dotnet add .\YourApp\YourApp.csproj package Farm.Data --version 1.0.0
```

The command above stores encrypted credentials in the local user NuGet
configuration on Windows. Do not commit credentials. On platforms without
NuGet password encryption, use a credential provider or a CI-secret-backed
NuGet environment credential instead of storing the password.

Use the actual published version if it is a prerelease build; `1.0.0` is only
available after a manual release run publishes it. CI and other developers
must also have access to the configured package source.
For consumption from another GitHub Actions repository, grant that repository
Actions access in the package settings and give its `GITHUB_TOKEN`
`packages: read` permission, or use an appropriately scoped token.
EF Core and the PostgreSQL provider are restored as transitive dependencies.

Register the context in the consuming application's `Program.cs`:

```csharp
using Farm.Data;
using Microsoft.EntityFrameworkCore;

var connectionString = builder.Configuration.GetConnectionString("AuraFarming")
    ?? throw new InvalidOperationException("AuraFarming connection string is required.");

builder.Services.AddDbContext<AuraFarming>(options =>
    options.UseNpgsql(connectionString));
```

Supply `ConnectionStrings__AuraFarming` through the consumer's secret
configuration. No credentials are included in the package. Each consumer
needs database network access and an account with appropriate permissions.

Inject `AuraFarming` into a scoped application service to write data:

```csharp
using Farm.Data;
using Farm.Data.Entities;

public sealed class BookmarkWriter(AuraFarming database)
{
    public async Task AddAsync(
        PlatformBookmark bookmark,
        CancellationToken cancellationToken = default)
    {
        database.PlatformBookmarks.Add(bookmark);
        await database.SaveChangesAsync(cancellationToken);
    }
}
```

Direct context access does not execute the Farm application's business
validation, authorization, or other business rules. Only use this package for
trusted writers that enforce the required rules themselves; otherwise write
through the application's API.

## Schema ownership

Keep migrations in this repository and apply them through one designated
deployment process. Consumers must not create competing migrations or
automatically migrate the shared database on startup. Migrations are included
in the package for the designated migrator to use. The design-time factory
is tooling support, not runtime connection configuration.
