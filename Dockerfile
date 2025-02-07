# Use the ARM64 SDK image for building the app
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
RUN echo "Using build configuration: $BUILD_CONFIGURATION" && \
    dotnet publish "OpenVPNGateMonitor/OpenVPNGateMonitor.csproj" -c $BUILD_CONFIGURATION -o /app/publish --runtime linux-arm64 --self-contained false

# Use the ASP.NET runtime for the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Install curl (optional, if needed for debugging or HTTP requests)
RUN apt-get update && apt-get install -y curl

# Switch to the 'app' user
USER app

# Set the working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=publish /app/publish .

# Specify the entry point for the application
ENTRYPOINT ["dotnet", "OpenVPNGateMonitor.dll"]