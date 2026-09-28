# CampaignVault.Goblins — Goblin Ponyriders

Optional high-stakes faction overlay for CampaignVault (`com.campaignvault.goblins`).

**Unified Clans** are an original take: goblin riding war-culture forged from revenge against tall-folk, after Cult of the Dragon–enhanced shamans went rogue. Living mounts are **Goblin Ponyriders** / Riding-Girls. This is **not** a port of any specific comic IP.

## Requires

- CampaignVault host ≥ 0.11.1 (PluginSdk 0.11.1)
- LewdHandbook installed for bondage / arousal / imprints / brands (this plugin listens to Lewd domain events by string topic)

## Install

```bash
./scripts/pack-goblins.sh          # stages artifacts/GoblinPonyriders/
# copy that folder to host Plugins/GoblinPonyriders/ and restart MCP
```

Never ship `CampaignVault.PluginSdk.dll` beside the drop.

## Enable

Player-owned campaign option:

```json
{ "$type": "campaign_update", "systemOptions": { "goblinClans": "on" }, "playerRequest": "enable goblin clans" }
```

**No auto world_build.** Dropping the plugin into an existing campaign does not create Unified Clans. With `goblinClans=on`, guidance nudges the LLM to `world_build` `factions/unified_clans` (or run `goblin_state` action=`seed_faction` once it is loaded to refresh lore metadata).

## What it owns vs Lewd

| Layer | Owner |
|-------|--------|
| Bindings, plugs, arousal, imprints, brands, willpower drain, soil | LewdHandbook |
| Defiance Clock, Clan Mark, Capture State, roles, training phases, faction lore | GoblinPonyriders |

Roles are **presets** that tell the DM which `lewd_bind` / `lewd_insert` kit to apply.

## Verb

`goblin_state` — actions: `seed_faction`, `capture`, `release`, `defiance`, `mark`, `role`, `train`, `name`, `status`.

## Skills

- `goblin-clans` — lore, doctrine, raids, terror-release, Clan Mark
- `goblin-training` — phases, 8 roles, Lewd bind hints, punishment/loyalty
