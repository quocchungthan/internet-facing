FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Farm.Mind/Farm.Mind.csproj Farm.Mind/
COPY Farm.Auth/Farm.Auth.csproj Farm.Auth/
RUN dotnet restore Farm.Mind/Farm.Mind.csproj

COPY Farm.Mind/ Farm.Mind/
COPY Farm.Auth/ Farm.Auth/
RUN dotnet publish Farm.Mind/Farm.Mind.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish/ ./
ENTRYPOINT ["dotnet", "Farm.Mind.dll"]
