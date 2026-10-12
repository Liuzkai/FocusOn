#!/usr/bin/env bash
set -euo pipefail
focuson_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$focuson_root"
if [[ -x "$focuson_root/.tools/dotnet/dotnet" ]]; then
  focuson_sdk="$focuson_root/.tools/dotnet/dotnet"
  export DOTNET_ROOT="$focuson_root/.tools/dotnet"
else
  focuson_sdk="$(command -v dotnet)"
fi
export DOTNET_CLI_HOME="$focuson_root/.tools/cli-home"
export NUGET_PACKAGES="$focuson_root/.tools/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_NOLOGO=1
export FOCUSON_DOTNET="$focuson_sdk"
exec "$focuson_sdk" run --project tools/FocusOn.Build/FocusOn.Build.csproj -c Release --property:RestoreLockedMode=true -- "$focuson_root" "${1:-build}" "${2:--}" "${3:--}"
