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

Point Claude / OpenCode / etc. at `Plugins/LewdHandbook/skills/` (`lewd-encounter.md` and siblings).

## Switching `intimacyTone` (consent style)

Key: `SystemOptions["intimacyTone"]` — values `consensual` (default if missing) | `fade` | `grimdark`.

1. **Check:** MCP `get_config` → `systemOptions.intimacyTone`.
2. **Add / change** (merge; host PluginSdk **0.1.2+**):

```json
{
  "$type": "campaign_update",
  "systemOptions": { "intimacyTone": "grimdark" }
}
```

Often combined with enabling the mode:

```json
{
  "$type": "campaign_update",
  "enabledModeIds": ["lewd_encounter"],
  "systemOptions": { "intimacyTone": "consensual" }
}
```

3. Confirm with `get_config` again. Per-character `consent` / `hard_limits` stay on participant State (see root README).

## Checklist

- [ ] `CampaignVault.PluginSdk.dll` is **absent** from the drop
- [ ] Host engine ≥ `minEngineVersion` in `plugin.json` (`0.2.0`)
- [ ] Host build includes PluginSdk **0.1.2+** if you need `campaign_update.systemOptions`
- [ ] Campaign `ActiveSystem` is `dnd5e`
- [ ] `EnabledModeIds` includes `lewd_encounter`
- [ ] `intimacyTone` present in `get_config` → `systemOptions` (or accept default consensual)
- [ ] LLM client pointed at `skills/` if using skill sidecars
- [ ] Table has consented to adult content (see root `NOTICE`)

## Uninstall

1. Delete `Plugins/LewdHandbook/`
2. Remove `lewd_encounter` from each campaign's `EnabledModeIds`
3. Restart the host
