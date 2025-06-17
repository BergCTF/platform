FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Berg.Api/Berg.Api.csproj", "Berg.Api/"]
RUN dotnet restore "Berg.Api/Berg.Api.csproj"
COPY . .
WORKDIR "/src/Berg.Api"
RUN dotnet build "Berg.Api.csproj" -c Release -o /app/build
RUN dotnet test "Berg.Api.csproj"

FROM build AS publish
RUN dotnet publish "Berg.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ARG BERG_VERSION
ENV BERG_VERSION=$BERG_VERSION

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Berg.Api.dll"]
