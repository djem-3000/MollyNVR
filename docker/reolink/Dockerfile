# Мост Reolink: проприетарный Baichuan (порт 9000) -> RTSP + HTTP-API.
#
# Используется готовый образ neolink.net (AGPL-3.0, https://github.com/borexola/neolink.net).
# Код проекта не линкуется с ним: это отдельный процесс, обмен по HTTP/RTSP.
# Сверху добавлен супервизор: при изменении /config/config.json процесс перезапускается
# и перечитывает список камер (сам мост конфиг на лету не перечитывает).

FROM ghcr.io/borexola/neolink.net:1.2.2 AS bridge

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=bridge /app/ ./
COPY docker/reolink/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8654 8655
ENTRYPOINT ["/entrypoint.sh"]
