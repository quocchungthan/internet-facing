# syntax=docker/dockerfile:1
# Build context is the repository root (internet-note): docker build -f Farm.Sync/Dockerfile .
FROM node:22-bookworm-slim AS reader
WORKDIR /app/reader
COPY Farm.Sync/reader/package*.json ./
RUN npm ci --omit=dev
COPY Farm.Sync/reader/ ./

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
# Farm.Data is a project reference, so the worker always compiles against the schema in this branch.
COPY Farm.Data/Farm.Data.csproj Farm.Data/
COPY Farm.Sync/Farm.Sync.csproj Farm.Sync/
RUN dotnet restore Farm.Sync/Farm.Sync.csproj
COPY Farm.Data/ Farm.Data/
COPY Farm.Sync/*.cs Farm.Sync/
RUN dotnet publish Farm.Sync/Farm.Sync.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble
RUN apt-get update -qq && apt-get install -y --no-install-recommends -qq libgssapi-krb5-2 \
    && apt-get clean
WORKDIR /app
COPY --from=reader /usr/local/bin/node /usr/local/bin/node
COPY --from=reader /app/reader ./reader
COPY --from=build /app/publish ./
RUN mkdir /state && chown app:app /state
USER app
ENV SYNC_STATE_PATH=/state/checkpoint.json
ENTRYPOINT ["dotnet", "AffineSync.dll"]
