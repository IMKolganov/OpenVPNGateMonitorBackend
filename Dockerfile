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

## Get the host user ID and group ID via build args
ARG HOST_UID=1000
ARG HOST_GID=1000

# Check if the group exists before creating it
RUN getent group app || groupadd -g $HOST_GID app

# Check if the user exists before creating it
RUN id -u app &>/dev/null || useradd -m -u $HOST_UID -g app app

# Install vsdbg debugger for Rider
RUN mkdir -p /vsdbg && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

# Switch to the 'app' user
USER app

# Set the working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=publish /app/publish .

# Specify the entry point for the application
ENTRYPOINT ["dotnet", "OpenVPNGateMonitor.dll"]