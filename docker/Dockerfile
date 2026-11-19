# Build the UI (React + Vite)
FROM node:22-alpine AS ui-build
WORKDIR /ui
COPY ui/package*.json ./
RUN npm ci
COPY ui/ ./
RUN npm run build

# Build the backend (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet publish src/MollyNvr.Api/MollyNvr.Api.csproj -c Release -o /app/publish

# go2rtc media engine (a single static binary)
FROM alpine:3.22 AS go2rtc
ARG GO2RTC_VERSION=v1.9.14
RUN apk add --no-cache ca-certificates curl \
    && curl -fsSL -o /go2rtc \
        "https://github.com/AlexxIT/go2rtc/releases/download/${GO2RTC_VERSION}/go2rtc_linux_amd64" \
    && chmod +x /go2rtc

# FFmpeg (BtbN static builds: NVENC/NVDEC inside)
FROM alpine:3.22 AS ffmpeg
ARG FFMPEG_URL=https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz
RUN apk add --no-cache ca-certificates curl xz \
    && curl -fsSL -o /tmp/ffmpeg.tar.xz "${FFMPEG_URL}" \
    && tar -xJf /tmp/ffmpeg.tar.xz -C /tmp \
    && mv /tmp/ffmpeg-master-latest-linux64-gpl/bin/ffmpeg /ffmpeg \
    && chmod +x /ffmpeg

# Runtime: API + UI static files + go2rtc + ffmpeg in one container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=go2rtc /go2rtc /usr/local/bin/go2rtc
COPY --from=ffmpeg /ffmpeg /usr/local/bin/ffmpeg
COPY --from=api-build /app/publish ./
COPY --from=ui-build /ui/dist ./wwwroot
EXPOSE 9080
ENV ASPNETCORE_URLS=http://+:9080
ENTRYPOINT ["dotnet", "MollyNvr.Api.dll"]
