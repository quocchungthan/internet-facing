FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# ARG GH_NUGET_TOKEN
# RUN if [ -n "$GH_NUGET_TOKEN" ]; then \
#       dotnet nuget add source "https://nuget.pkg.github.com/quocchungthan/index.json" \
#       --name github --username "quocchungthan" --password "$GH_NUGET_TOKEN" --store-password-in-clear-text; \
#     fi

COPY Fences/Fences.csproj Fences/
RUN dotnet restore Fences/Fences.csproj

COPY Fences/ Fences/
COPY Farm/wwwroot/css/shared/ Farm/wwwroot/css/shared/
COPY Farm/wwwroot/favicon.ico Farm/wwwroot/favicon.ico
RUN dotnet publish Fences/Fences.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:80
ENV IdentityApp__IdentityDatabasePath=/var/lib/fences/identity.db
ENV IdentityApp__DataProtectionKeysPath=/var/lib/fences/keys
EXPOSE 80
VOLUME ["/var/lib/fences"]

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Fences.dll"]
