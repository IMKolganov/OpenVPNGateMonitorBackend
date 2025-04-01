# Base build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release

RUN if [ -z "$TARGETARCH" ]; then echo "ERROR: TARGETARCH is not set!"; exit 1; fi
RUN echo "Building for runtime: linux-${TARGETARCH}, configuration: ${BUILD_CONFIGURATION}"

WORKDIR /src

COPY ["OpenVPNGateMonitor/OpenVPNGateMonitor.csproj", "OpenVPNGateMonitor/"]
WORKDIR /src/OpenVPNGateMonitor
RUN dotnet restore "OpenVPNGateMonitor.csproj"

WORKDIR /src
COPY . .

# Use dotnet publish with -r (runtime) argument for cross-targeting
RUN dotnet publish "OpenVPNGateMonitor/OpenVPNGateMonitor.csproj" \
      -c $BUILD_CONFIGURATION \
      -r linux-${TARGETARCH} \
      --self-contained false \
      -o /app/publish

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
USER app
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenVPNGateMonitor.dll"]
