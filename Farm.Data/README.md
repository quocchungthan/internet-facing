# Farm.Data

Reusable .NET 10 data access for the AuraFarming PostgreSQL database. The
package contains the `AuraFarming` EF Core context, entities, model mappings,
and migrations. It does not depend on the Farm web/business layer.

## Build and download

Build a package locally from the repository root:

```powershell
dotnet pack .\Farm.Data\Farm.Data.csproj --configuration Release --output .\artifacts\nuget -p:PackageVersion=1.0.0
```

The **Package Farm.Data** GitHub Actions workflow uploads a
`Farm.Data-nuget` artifact whenever relevant changes reach `main`. Automatic
builds use unique `1.0.0-ci.<run>.<attempt>` prerelease versions. Run the workflow
manually with an explicit version for a release package. Download and extract
the artifact from the workflow run; it is retained for 90 days.

This workflow publishes a downloadable build artifact, not a NuGet feed.
For long-term distribution and normal feed-based restores, publish the same
`.nupkg` to your organization's NuGet feed. Use a new version for each release,
and coordinate package versions with database schema changes.

## Consume from another solution

Use a .NET 10 application. Place the downloaded `.nupkg` in a local folder,
register that folder as a NuGet source, and install the exact package version:

```powershell
dotnet nuget add source C:\packages\farm-data --name FarmDataLocal
dotnet add .\YourApp\YourApp.csproj package Farm.Data --version 1.0.0
```

Use the actual version from the artifact if it is a prerelease build. CI and
other developers must also have access to the configured package source.
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
