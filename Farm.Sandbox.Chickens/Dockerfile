FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates git tini tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1654 chickens \
    && useradd --uid 1654 --gid chickens --create-home --shell /usr/sbin/nologin chickens \
    && install --directory --owner=chickens --group=chickens \
        /workspace/repository \
        /workspace/artifacts \
        /workspace/state \
        /workspace/cache \
        /workspace/worktrees \
        /workspace/agent-overrides

WORKDIR /app

ENV DOTNET_CLI_HOME=/workspace/cache/dotnet \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    GIT_TERMINAL_PROMPT=0 \
    HOME=/workspace/cache/home \
    NUGET_PACKAGES=/workspace/cache/nuget

COPY --chown=chickens:chickens artifacts/farm-sandbox-chickens/linux-x64/ ./

USER 1654:1654

ENTRYPOINT ["dotnet", "Farm.Sandbox.Chickens.dll"]