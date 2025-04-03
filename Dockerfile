# Define the TARGETARCH argument
ARG TARGETARCH

# Use the .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Check if the argument is passed
ARG TARGETARCH
RUN if [ -z "$TARGETARCH" ]; then echo "ERROR: TARGETARCH is not set!"; exit 1; fi
RUN echo "BUILD STAGE: TARGETARCH=${TARGETARCH}"

# Set the working directory
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["OpenVPNGateMonitor/OpenVPNGateMonitor.csproj", "OpenVPNGateMonitor/"]
WORKDIR /src/OpenVPNGateMonitor
RUN dotnet restore "OpenVPNGateMonitor.csproj"

# Copy the rest of the application source code
WORKDIR /src
COPY . .

# Publish the application (framework-dependent)
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN echo "Using build configuration: $BUILD_CONFIGURATION" && \
    dotnet publish "OpenVPNGateMonitor/OpenVPNGateMonitor.csproj" \
      -c $BUILD_CONFIGURATION \
      -o /app/publish

# Use the ASP.NET runtime for the final image (framework-dependent)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Use root initially to allow setting permissions
USER root

WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Don't switch to app here — entrypoint.sh will drop privileges
ENTRYPOINT ["/entrypoint.sh"]