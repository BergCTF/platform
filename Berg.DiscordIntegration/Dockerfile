FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Berg.DiscordIntegration/Berg.DiscordIntegration.csproj", "Berg.DiscordIntegration/"]
RUN dotnet restore "Berg.DiscordIntegration/Berg.DiscordIntegration.csproj"
COPY . .
WORKDIR "/src/Berg.DiscordIntegration"
RUN dotnet build "Berg.DiscordIntegration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Berg.DiscordIntegration.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Berg.DiscordIntegration.dll"]
