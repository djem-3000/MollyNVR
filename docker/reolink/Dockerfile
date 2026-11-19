# Reolink bridge: proprietary Baichuan (port 9000) -> RTSP + HTTP API.
#
# Uses the ready-made neolink.net image (AGPL-3.0, https://github.com/borexola/neolink.net).
# The project code is not linked with it: it is a separate process communicating over HTTP/RTSP.
# A supervisor is added on top: when /config/config.json changes, the process restarts
# and re-reads the camera list (the bridge itself does not reload the config on the fly).

FROM ghcr.io/borexola/neolink.net:1.2.2 AS bridge

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=bridge /app/ ./
COPY docker/reolink/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8654 8655
ENTRYPOINT ["/entrypoint.sh"]
