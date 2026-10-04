FROM node:22-bookworm-slim AS frontend-build
WORKDIR /src

# Copy package definitions for both React projects
COPY Farm.Microsoft/package.json Farm.Microsoft/
COPY Farm.HangingPost/package.json Farm.HangingPost/package-lock.json Farm.HangingPost/

# Build Farm.Microsoft first so its artifacts and types are available
WORKDIR /src/Farm.Microsoft
COPY Farm.Microsoft/ ./
RUN npm install && npm run build

# Build Farm.HangingPost shell with injected Farm.Microsoft tab
WORKDIR /src/Farm.HangingPost
COPY Farm.HangingPost/ ./
RUN npm install
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency restore
COPY PiggyFarm.slnx ./
COPY Farm/Farm.csproj Farm/
COPY Farm.Data/Farm.Data.csproj Farm.Data/
RUN dotnet restore Farm/Farm.csproj

# Copy C# sources and compiled frontend assets
COPY Farm/ Farm/
COPY Farm.Data/ Farm.Data/
COPY --from=frontend-build /src/Farm/wwwroot/hanging-post/ /src/Farm/wwwroot/hanging-post/
RUN dotnet publish Farm/Farm.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish/ ./
ENTRYPOINT ["dotnet", "Farm.dll"]