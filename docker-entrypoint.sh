#!/bin/sh
# Том /data/filestorage часто смонтирован с владельцем root на хосте.
# Процесс работает от user app — без write на каталог File.OpenWrite даёт
# "Access to the path '.../guid' is denied." при рабочих download.
set -eu

STORAGE_PATH="${STORAGE_PATH:-/data/filestorage}"

mkdir -p "$STORAGE_PATH" /app/logs

if [ "$(id -u)" -eq 0 ]; then
  # Только сам каталог (не -R): иначе старт на большом архиве тормозит.
  chown app:app "$STORAGE_PATH" /app/logs || true
  # setpriv из util-linux (есть в aspnet:6.0 / Debian)
  exec setpriv --reuid=app --regid=app --init-groups -- "$@"
fi

exec "$@"
