FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Berg/Berg.csproj", "Berg/"]
RUN dotnet restore "Berg/Berg.csproj"
COPY . .
WORKDIR "/src/Berg"
RUN dotnet build "Berg.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Berg.csproj" -c Release -o /app/publish

FROM base AS final
LABEL org.opencontainers.image.source=https://github.com/norelect/berg
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Berg.dll"]
