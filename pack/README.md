# Packaging / install

## Host drop layout

Copy `artifacts/LewdHandbook/` (from `../scripts/pack-plugin.sh`) into the CampaignVault host:

```
<host>/Plugins/LewdHandbook/
  plugin.json
  LewdHandbook.dll
  RulesetData/     # optional YAML overlays
  skills/          # optional LLM-client sidecars (not loaded by the host)
```

Restart the MCP host after installing or updating.

Point Claude / OpenCode / etc. at `Plugins/LewdHandbook/skills/` (`lewd-encounter.md` and siblings). Requires PluginSdk **0.1.1**+ types used by the assembly; set campaign `SystemOptions.intimacyTone` (`consensual` | `fade` | `grimdark`).

## Checklist

- [ ] `CampaignVault.PluginSdk.dll` is **absent** from the drop
- [ ] Host engine ≥ `minEngineVersion` in `plugin.json` (`0.2.0`)
- [ ] Campaign `ActiveSystem` is `dnd5e`
- [ ] `EnabledModeIds` includes `lewd_encounter`
- [ ] `intimacyTone` set appropriately for the table
- [ ] LLM client pointed at `skills/` if using skill sidecars
- [ ] Table has consented to adult content (see root `NOTICE`)

## Uninstall

1. Delete `Plugins/LewdHandbook/`
2. Remove `lewd_encounter` from each campaign's `EnabledModeIds`
3. Restart the host
