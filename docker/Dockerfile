# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY *.slnx ./
COPY SmartParking.Domain/*.csproj SmartParking.Domain/
COPY SmartParking.Application/*.csproj SmartParking.Application/
COPY SmartParking.Infrastructure/*.csproj SmartParking.Infrastructure/
COPY SmartParking.Host/*.csproj SmartParking.Host/

# Restore dependencies
RUN dotnet restore SmartParking.Host/SmartParking.Host.csproj

# Copy everything else
COPY SmartParking.Domain/ SmartParking.Domain/
COPY SmartParking.Application/ SmartParking.Application/
COPY SmartParking.Infrastructure/ SmartParking.Infrastructure/
COPY SmartParking.Host/ SmartParking.Host/

# Build and publish
WORKDIR /src/SmartParking.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmartParking.Host.dll"]
