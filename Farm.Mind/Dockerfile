FROM node:22-bookworm-slim AS note-reader
WORKDIR /reader
COPY Farm.Mind/Notes/reader/package*.json ./
RUN npm ci --omit=dev
COPY Farm.Mind/Notes/reader/decode.mjs ./

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
COPY --from=note-reader /usr/local/bin/node /usr/local/bin/node
COPY --from=note-reader /reader/ ./Notes/reader/
ENTRYPOINT ["dotnet", "Farm.Mind.dll"]
