#!/bin/bash
set -euo pipefail

if [ -n "${PORT:-}" ]; then
  export WEBSOCKIFY_PORT="${PORT}"
else
  export WEBSOCKIFY_PORT=6080
fi

# Patch supervisor config with dynamic port if needed
if [ "${WEBSOCKIFY_PORT}" != "6080" ]; then
  sed -i "s/6080/${WEBSOCKIFY_PORT}/g" /etc/supervisor/conf.d/supervisord.conf
fi

exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
