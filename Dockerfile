# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ComplianceScannerPro.sln ./
COPY src/ComplianceScannerPro.Core/ComplianceScannerPro.Core.csproj src/ComplianceScannerPro.Core/
COPY src/ComplianceScannerPro.Infrastructure/ComplianceScannerPro.Infrastructure.csproj src/ComplianceScannerPro.Infrastructure/
COPY src/ComplianceScannerPro.Shared/ComplianceScannerPro.Shared.csproj src/ComplianceScannerPro.Shared/
COPY src/ComplianceScannerPro.Web/ComplianceScannerPro.Web.csproj src/ComplianceScannerPro.Web/

# Restore packages
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
WORKDIR /src/src/ComplianceScannerPro.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install required packages for Playwright
RUN apt-get update && apt-get install -y \
    wget \
    gnupg \
    libnss3 \
    libatk-bridge2.0-0 \
    libgtk-3-0 \
    libgdk-pixbuf2.0-0 \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create storage directory
RUN mkdir -p /app/storage/reports && chmod 755 /app/storage

# Set environment
ENV ASPNETCORE_URLS=http://+:80
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 80

ENTRYPOINT ["dotnet", "ComplianceScannerPro.Web.dll"]