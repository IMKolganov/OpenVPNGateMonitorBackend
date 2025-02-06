﻿# Use the ARM64 SDK image for building the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set the working directory
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["OpenVPNGateMonitor/OpenVPNGateMonitor.csproj", "OpenVPNGateMonitor/"]
WORKDIR /src/OpenVPNGateMonitor
RUN dotnet restore "OpenVPNGateMonitor.csproj"

# Copy the rest of the application source code
WORKDIR /src
COPY . .

# Build the application
ARG BUILD_CONFIGURATION=Debug
RUN dotnet build "OpenVPNGateMonitor/OpenVPNGateMonitor.csproj" -c $BUILD_CONFIGURATION -o /app/build --runtime linux-arm64 --self-contained false

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Debug
RUN dotnet publish "OpenVPNGateMonitor/OpenVPNGateMonitor.csproj" -c $BUILD_CONFIGURATION -o /app/publish --runtime linux-arm64 --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install curl & vsdbg for debugging
RUN apt-get update && apt-get install -y curl unzip && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

# Set up a non-root user for security
RUN if ! id -u app > /dev/null 2>&1; then useradd -m app; fi
RUN mkdir -p /app && chown -R app:app /app

# Switch to the non-root user
USER app

# Set the working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=publish /app/publish .

# Specify the entry point for the application
ENTRYPOINT ["dotnet", "OpenVPNGateMonitor.dll"]