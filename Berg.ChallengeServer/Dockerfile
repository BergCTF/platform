FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Berg.ChallengeServer/Berg.ChallengeServer.csproj", "Berg.ChallengeServer/"]
RUN dotnet restore "Berg.ChallengeServer/Berg.ChallengeServer.csproj"
COPY . .
WORKDIR "/src/Berg.ChallengeServer"
RUN dotnet build "Berg.ChallengeServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Berg.ChallengeServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Berg.ChallengeServer.dll"]
