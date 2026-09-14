# Shed

Shed is the migrated file-management service from `piggy-farm/car/ValinaAspNet`.
The migration preserves the Razor Pages, Identity authentication, file APIs, access-key sync endpoints, SQLite EF migrations, and legacy tests.

> Only for personal large files with limited SSD VPS.

## Local validation

```powershell
dotnet restore .\Shed\Shed.csproj
dotnet build .\Shed\Shed.csproj --configuration Release --no-restore
dotnet test .\Shed.Tests\Shed.Tests.csproj --configuration Release --no-restore
```

The app uses SQLite files under `/app/data` in the container and stores managed files under `/app/data/prodstorage`. Local development can override `ConnectionStrings` and `Storage:RootPath` with environment variables or user secrets.

Shed consumes the shared Farm CSS from `Farm/wwwroot/css/shared`. `Shed/Shed.csproj` links `variables.css`, `base.css`, `tokens.css`, and `storage.css` from that directory and copies them into Shed's runtime output and publish output. Farm owns these shared CSS sources; update them there rather than adding separate Shed copies.

The `OpensourceLab.FileStorage` contract project is vendored locally at `Shed/OpensourceLab.FileStorage`. Shed references this project directly, including its generated protobuf and gRPC contracts, so local and deployment builds do not require the private GitHub NuGet package feed.

## Deployment assumptions

The manual or `sub/storage` deployment workflow builds from the repository root with `Shed` as the Docker context, transfers the image to the VPS, and invokes `deployment/deploy-shed.sh` with the shared `deployment/deploy-nginx-service.sh` script.

The wrapper assumes:

- Public domain: `storage.eldervibe.dev` (`sub/storage` -> `storage.eldervibe.dev`)
- Container and upstream port: `8080`
- Container name: `storage`
- VPS env file: `/etc/storage/storage.env`
- Persistent host directory: `/srv/storage/data`, mounted at `/app/data`
- Required runtime configuration is supplied through `/etc/storage/storage.env`, especially `IdentityBridge__BaseUrl`, `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, and `Smtp__DefaultFrom`.
- `CERTBOT_EMAIL` is supplied as a repository/environment secret or variable for the shared deployment script.

The production JSON intentionally contains no credentials. The workflow requires the existing `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`, and `VPS_KNOWN_HOSTS` secrets.
