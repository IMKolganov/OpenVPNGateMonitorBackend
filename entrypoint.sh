#!/bin/sh

echo "[entrypoint] Current UID: $(id -u), GID: $(id -g)"
echo "[entrypoint] Fixing permissions on /app/GeoLite..."

chown -R app:app /app/GeoLite || echo "[entrypoint] chown failed (already owned?)"

echo "[entrypoint] Starting application..."
exec dotnet OpenVPNGateMonitor.dll