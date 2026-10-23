FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency restore
COPY PiggyFarm.slnx ./
COPY Farm/Farm.csproj Farm/
COPY Farm.Data/Farm.Data.csproj Farm.Data/
COPY Farm.Auth/Farm.Auth.csproj Farm.Auth/
RUN dotnet restore Farm/Farm.csproj

# Copy C# sources and compiled frontend assets
COPY Farm/ Farm/
COPY Farm.Data/ Farm.Data/
COPY Farm.Auth/ Farm.Auth/
RUN dotnet publish Farm/Farm.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish/ ./
ENTRYPOINT ["dotnet", "Farm.dll"]