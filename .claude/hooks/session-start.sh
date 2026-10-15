#!/usr/bin/env bash
# SessionStart-хук Claude Code: в облачной сессии ставит .NET 10 SDK и восстанавливает пакеты,
# чтобы Claude сразу мог выполнить dotnet build / test / format.
# На ПК разработчика (не облако) ничего не делает.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"

# AVALONIA_TELEMETRY_OPTOUT: сборка Avalonia иначе стучится на свой сервер телеметрии, в облаке он закрыт.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 AVALONIA_TELEMETRY_OPTOUT=1

has_sdk10() { command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; }

if ! has_sdk10; then
  echo "Ставлю .NET 10 SDK…" >&2
  SUDO=""; [ "$(id -u)" -ne 0 ] && SUDO="sudo"
  # Ubuntu 24.04+ отдаёт .NET 10 из своего архива; builds.dotnet.microsoft.com в облаке закрыт.
  if command -v apt-get >/dev/null 2>&1; then
    $SUDO apt-get update -qq >&2 || true
    DEBIAN_FRONTEND=noninteractive $SUDO apt-get install -y -qq dotnet-sdk-10.0 >&2
  fi
fi

if ! has_sdk10; then
  echo "Не удалось поставить .NET 10 SDK: сборка и тесты в этой сессии недоступны." >&2
  exit 0
fi

# Пакеты NuGet кешируются вместе с контейнером, повторный запуск быстрый.
if [ -f Avers.sln ]; then
  dotnet restore Avers.sln --nologo -v q >&2 || echo "dotnet restore завершился с ошибкой" >&2
fi

echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 AVALONIA_TELEMETRY_OPTOUT=1" >> "${CLAUDE_ENV_FILE:-/dev/null}"
echo ".NET готов: $(dotnet --version)" >&2
