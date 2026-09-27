#!/usr/bin/env bash
# Host-level tests: runs the plugin inside the real CampaignVault dispatcher. Needs ../CampaignVault checked out.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
if [ ! -d "$here/../CampaignVault/src/CampaignVault" ]; then
  echo "CampaignVault core not found at $here/../CampaignVault; skipping host tests." >&2
  exit 0
fi
dotnet test "$here/tests/LewdHandbook.HostTests/LewdHandbook.HostTests.csproj" "$@"
