#!/bin/sh
# Держит процесс neolink.net запущенным и перезапускает его при изменении config.json:
# мост читает список камер только при старте.
set -eu

config=/config/config.json
mkdir -p /config

if [ ! -s "$config" ]; then
  printf '{\n  "webui": false,\n  "cameras": []\n}\n' > "$config"
fi

hash_config() {
  sha256sum "$config" 2>/dev/null | cut -d' ' -f1 || echo missing
}

while true; do
  dotnet /app/neolink.net.dll --config "$config" &
  app=$!
  current=$(hash_config)

  while kill -0 "$app" 2>/dev/null; do
    sleep 2
    updated=$(hash_config)
    if [ "$updated" != "$current" ]; then
      echo "config.json changed - restarting Reolink bridge" >&2
      current=$updated
      kill -TERM "$app" 2>/dev/null || true

      for _ in 1 2 3 4 5 6 7 8 9 10; do
        kill -0 "$app" 2>/dev/null || break
        sleep 1
      done

      kill -KILL "$app" 2>/dev/null || true
      break
    fi
  done

  wait "$app" 2>/dev/null || true
  sleep 1
done
