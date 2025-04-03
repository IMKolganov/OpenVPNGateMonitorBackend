#!/bin/sh

echo "[entrypoint] Current UID: $(id -u), GID: $(id -g)"
echo "[entrypoint] Fixing permissions on /app/GeoLite..."

chown -R app:app /app/GeoLite

echo "[entrypoint] Switching to user 'app' and starting application..."
exec su app -c "dotnet OpenVPNGateMonitor.dll"