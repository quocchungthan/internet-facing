FROM node:22-bookworm-slim AS hanging-post-build
WORKDIR /src/Farm.HangingPost

COPY Farm.HangingPost/package.json Farm.HangingPost/package-lock.json ./
RUN npm ci

COPY Farm.HangingPost/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Farm/Farm.csproj Farm/
RUN dotnet restore Farm/Farm.csproj

COPY Farm/ Farm/
COPY --from=hanging-post-build /src/Farm/wwwroot/hanging-post/ /src/Farm/wwwroot/hanging-post/
RUN dotnet publish Farm/Farm.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish/ ./
ENTRYPOINT ["dotnet", "Farm.dll"]