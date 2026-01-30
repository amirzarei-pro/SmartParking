# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Sln
COPY . .

# Restore Packages
RUN dotnet restore "UniSmartParking.slnx"

# Publish SmartParking.Host
RUN dotnet publish "SmartParking.Host/SmartParking.Host.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Defualt Port
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SmartParking.Host.dll"]
