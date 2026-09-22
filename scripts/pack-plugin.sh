#!/usr/bin/env bash
# Build LewdHandbook and stage a host-ready Plugins/LewdHandbook drop (no Sdk.dll).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/src/LewdHandbook/LewdHandbook.csproj"
OUT_DIR="$ROOT/artifacts/LewdHandbook"
CONFIG="${1:-Release}"

dotnet build "$PROJ" -c "$CONFIG" --nologo

BUILD_OUT="$ROOT/src/LewdHandbook/bin/$CONFIG/net10.0"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

cp "$BUILD_OUT/LewdHandbook.dll" "$OUT_DIR/"
cp "$BUILD_OUT/plugin.json" "$OUT_DIR/"

if [[ -d "$BUILD_OUT/RulesetData" ]]; then
  cp -R "$BUILD_OUT/RulesetData" "$OUT_DIR/"
fi
if [[ -d "$BUILD_OUT/skills" ]]; then
  cp -R "$BUILD_OUT/skills" "$OUT_DIR/"
fi

# Belt-and-suspenders: never ship the Sdk beside the plugin.
rm -f "$OUT_DIR"/CampaignVault.PluginSdk.dll "$OUT_DIR"/CampaignVault.PluginSdk.pdb

if [[ -f "$OUT_DIR/CampaignVault.PluginSdk.dll" ]]; then
  echo "Refusing to ship CampaignVault.PluginSdk.dll" >&2
  exit 1
fi

echo "Staged plugin drop: $OUT_DIR"
find "$OUT_DIR" -type f | sort
