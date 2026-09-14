FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG GH_NUGET_TOKEN
RUN if [ -n "$GH_NUGET_TOKEN" ]; then \
      dotnet nuget add source "https://nuget.pkg.github.com/quocchungthan/index.json" \
      --name github --username "quocchungthan" --password "$GH_NUGET_TOKEN" --store-password-in-clear-text; \
    fi

COPY Fences/Fences.csproj Fences/
RUN dotnet restore Fences/Fences.csproj

COPY Fences/ Fences/
COPY Farm/wwwroot/css/shared/ Farm/wwwroot/css/shared/
RUN dotnet publish Fences/Fences.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Fences.dll"]
