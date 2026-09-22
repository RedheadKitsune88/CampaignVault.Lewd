#!/usr/bin/env bash
# Pack CampaignVault.PluginSdk from a sibling CampaignVault checkout into ./local-packages.
# Safe to delete once the package is on nuget.org and the local-packages source is removed from nuget.config.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK_PROJ="${CAMPAIGNVAULT_ROOT:-$ROOT/../CampaignVault}/src/CampaignVault.PluginSdk/CampaignVault.PluginSdk.csproj"
OUT="$ROOT/local-packages"

if [[ ! -f "$SDK_PROJ" ]]; then
  echo "PluginSdk project not found at: $SDK_PROJ" >&2
  echo "Set CAMPAIGNVAULT_ROOT to your CampaignVault checkout." >&2
  exit 1
fi

mkdir -p "$OUT"
dotnet pack "$SDK_PROJ" -c Release -o "$OUT" --nologo
echo "Packed into $OUT"
ls -la "$OUT"/*.nupkg
