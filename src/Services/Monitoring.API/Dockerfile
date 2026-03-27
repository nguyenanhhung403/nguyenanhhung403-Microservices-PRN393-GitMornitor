FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/Monitoring.API/Monitoring.API.csproj", "Services/Monitoring.API/"]
RUN dotnet restore "./Services/Monitoring.API/Monitoring.API.csproj"
COPY src/Services/Monitoring.API/ Services/Monitoring.API/
WORKDIR "/src/Services/Monitoring.API"
RUN dotnet build "./Monitoring.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Monitoring.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Monitoring.API.dll"]
