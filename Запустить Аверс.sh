#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "Не найден .NET 8. Установите SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
  exit 1
fi
exec dotnet run --project src/Avers.Desktop/Avers.Desktop.csproj -c Release
