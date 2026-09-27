# CampaignVault.Lewd

Opt-in **Lewd Handbook** plugin for CampaignVault: adult `lewd_encounter` interaction mode for `ActiveSystem=dnd5e`, plus YAML data overlays and LLM-client skill sidecars. What is this **Lewd Handbook**? It is a homebrew extension for fifth edition D&D system; you can [read more](https://www.patreon.com/Miss_Mycelia/posts/sex-dungeons-5e-79225409) at it's creator's patreon.

> **Adult content.** See [NOTICE](NOTICE). Operator and table consent required. Full-trust DLL (same trust model as any CampaignVault code plugin).

This repository is intentionally **separate** from the main CampaignVault tree so adult mechanics and prose never land in core.

## Requirements

- .NET SDK that targets `net10.0`
- A CampaignVault host with engine version ≥ `0.7.0` (`minEngineVersion` in `plugin.json`)
- `CampaignVault.PluginSdk` **0.7.0**. From 0.6.0: `Character.LifeStage`, `IInteractionMode.ValidateEntry`, `playerOnly` campaign options, `mode_transition action=turn`. New in 0.7.0: `IPluginCampaignOptionsUpgrader`, `IContextTurn.Config` / `Time` / `LoadCharacterAsync`, `playerOnlyModeIds`, owner-managed pools (`ownerManaged` / `startsAt`), mode action budgets enforced by the host, and travel alongside a hard engagement whose target travels in the same commit. Restored from nuget.org.

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

See [EVENTS.md](EVENTS.md) for topics other plugins can subscribe to. Mode verbs `lewd_advance` / `lewd_bad_end` / `lewd_turn_start` use `ModeId=lewd_encounter` (hidden from the default commit_schema index; look up with `type=`). `lewd_bind` works anywhere.

Host-level tests run the plugin inside core's real dispatcher and need the core repo next to this one: `./scripts/test-host.sh`.

## Install into a host

```bash
./scripts/pack-plugin.sh
# Copies into artifacts/LewdHandbook/ — drop that folder under the host Plugins/ directory:
#
# Plugins/
#   LewdHandbook/
#     plugin.json
#     LewdHandbook.dll
#     RulesetData/   (optional)
#     skills/        (optional; host logs path only — point Claude/OpenCode at these files)
```

**Never** place `CampaignVault.PluginSdk.dll` in the plugin folder. The host already provides it.

### Enable the mode

On a `dnd5e` campaign, include `lewd_encounter` in `EnabledModeIds` (the manifest lists it in `playerOnlyModeIds`, so only a `campaign_update` quoting the player in `playerRequest`, alone in its commit, can switch it), then enter / `turn` / exit with core `mode_transition`. See **Player settings and consent** below.

### LLM skills (operator install)

Host does **not** inject skills. Point your LLM client at the plugin skill pack:

`Plugins/LewdHandbook/skills/` (or repo `src/LewdHandbook/skills/` while developing)

| File | Role |
|------|------|
| `lewd-encounter.md` | Adults-only rule, player settings, stance, turns, pools, advance/climax |
| `lewd-bindings.md` | Restraint anywhere: `lewd_bind` / `lewd_unbind` / `lewd_escape`, anchors, locks |
| `lewd-tracks.md` | Vice, imprint, brand, pregnancy, bad-end |
| `lewd-catalog.md` | Histories, anatomy/implements, spells/items/conditions |

## Plugin identity

| Field | Value |
|-------|-------|
| Manifest id | `com.campaignvault.lewd-handbook` |
| Mode id | `lewd_encounter` |
| Compatible systems | `dnd5e` |
| Custom `$type`s | `lewd_advance`, `lewd_climax_check`, `lewd_recover`, `lewd_bind`, `lewd_unbind`, `lewd_escape`, `lewd_stance`, `lewd_pregnancy`, `lewd_bad_end`, `lewd_apply_brand`, `lewd_imprint`, `lewd_decondition`, `lewd_vice`; plugin-emitted `lewd_turn_start`, `lewd_scene_end`, `lewd_rest`, `lewd_echo_check` |

## Layout

```
src/LewdHandbook/          plugin assembly + plugin.json + RulesetData + skills
tests/LewdHandbook.Tests/  unit tests against Sdk types
tests/LewdHandbook.HostTests/  plugin inside core's dispatcher (needs ../CampaignVault)
pack/                      install notes
scripts/                   plugin pack + host-test helpers
```

## Adults only

Every character a lewd verb touches — mode entry included — must have `lifeStage` `adult` or `elder` (core `Character.LifeStage`). `Unspecified` fails closed; `child`/`adolescent` always fail; a character whose name, appearance, visual tags or features read as a minor fails even when marked adult (a character's children, kid gloves or a nickname like "The Kid" don't count). Core refuses to change a recorded minor to adult. None of this is configurable.

Characters created before `lifeStage` existed are unset, and the plugin never infers a stage. In a campaign with `lewd_encounter` enabled, the context names party and involved characters still unset, so the DM records each from the fiction with `character_update`.

## Player settings and consent

Two separate layers, because the only human at the table is the player:

| Layer | Who sets it | Where | Controls |
|-------|-------------|-------|----------|
| **Player settings** | The human player | Campaign options `lewdNarration`, `lewdNonConsent`, `lewdHardLimits` (`playerOnly`) | How it's narrated, whether unwanted acts can resolve, content that's always refused |
| **Stance** | The model for NPCs, the player for their PC | `lewd_stance` → scene State or character Traits `lewd_encounter.*` | Whether a character wants a given act, and their own limits, kinks and Inhibition |

`lewdNarration` (`explicit` / `suggestive` / `fade`) only steers narration; mechanics always resolve in full. `lewdNonConsent`: `off` (default) refuses anything a character doesn't want; `not_against_pc` allows it among NPCs but never against the player's character; `on` allows it. Revoked stance and hard limits refuse in every setting.

Player-owned options can't change as a side effect of a story commit: `campaign_update` touching them must quote the player in `playerRequest` and be the only change in its commit.

```json
{
  "$type": "campaign_update",
  "systemOptions": { "lewdNonConsent": "not_against_pc", "lewdHardLimits": "pregnancy,vore" },
  "playerRequest": "NPCs can be captured, but never my character. No pregnancy or vore."
}
```

Migrating from `intimacyTone`: the host converts it at startup (`LewdCampaignOptionsUpgrader`) and never overwrites a key the player already set. `consensual` → `lewdNonConsent: off`; `grimdark` → `on`; `fade` → `on` plus `lewdNarration: fade` (fade no longer zeroes stimulation).

### Stance

```json
{ "$type": "lewd_stance", "characterId": "chars/mara", "scope": "default", "stance": "selective", "allowedPartners": ["chars/pc"], "kinks": ["rope"], "inhibition": 2 }
```

`scope: scene` (default inside an encounter) overrides for that scene only. Keys read by the handlers: `stance` / `consent`, `allowed_partners`, `hard_limits`, `soft_limits`, `kinks`, `inhibition`.
