# CampaignVault.Lewd

Opt-in **Lewd Handbook** plugin for CampaignVault: adult `lewd_encounter` interaction mode for `ActiveSystem=dnd5e`, plus YAML data overlays and LLM-client skill sidecars. What is this **Lewd Handbook**? It is a homebrew extension for fifth edition D&D system; you can by Miss Mycelia: [Lewd Handbook 3](https://www.patreon.com/Miss_Mycelia/posts/lewd-handbook-3-124739374) (earlier: [Sex Dungeons 5e](https://www.patreon.com/Miss_Mycelia/posts/sex-dungeons-5e-79225409)) on their patreon.

> **Adult content.** See [NOTICE](NOTICE). Operator and table consent required. Full-trust DLL (same trust model as any CampaignVault code plugin).

This repository is intentionally **separate** from the main CampaignVault tree so adult mechanics and prose never land in core.

## Requirements

- .NET SDK that targets `net10.0`
- A CampaignVault host with engine version ≥ `0.15.0` (`minEngineVersion` in `plugin.json`)
- `CampaignVault.PluginSdk` **0.15.0** (resolved from nuget.org). Adds body piercings (`piercing` / `PiercingMark` / `core.pierced.v1`). 0.11.x covers dirt, roll modifiers, willpower, and climax-related host surfaces.

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

See [EVENTS.md](EVENTS.md) for topics other plugins can subscribe to. Only the verbs that need an active scene (`lewd_advance`, `lewd_bad_end` and the engine-only scene verbs) use `ModeId=lewd_encounter`, so they are absent from the default commit_schema index (look up with `type=`). Everything else (`lewd_stance`, `lewd_bind`/`unbind`/`escape`, `lewd_apply_brand`, `lewd_imprint`, `lewd_pregnancy`, `lewd_vice`, `lewd_decondition`, `lewd_recover`, `lewd_climax_check`, `lewd_insert`, `lewd_remove`, `lewd_cleanup`) stays in the default schema and works outside a scene, anywhere the plugin applies. The engine-only verbs are refused if the model sends them.

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

### Optional: Goblin Ponyriders (CampaignVault.Goblins)

Separate plugin in `src/GoblinPonyriders/` — Unified Clans faction overlay (Defiance Clock, Clan Mark, capture/training roles). Pack with `./scripts/pack-goblins.sh` → `artifacts/GoblinPonyriders/`. Enable player-owned `goblinClans=on`. Bondage stays on LewdHandbook; see `src/GoblinPonyriders/README.md`.

### Scenes, joining and interruptions

`mode_transition` `join` / `leave` change who is in a running scene (a joiner passes the same adults-only check and acts from the next round). The plugin listens to core's `traveled`, `encounter_interrupted`, `character_downed`, `combat_started` and `rested` events and exits the scene when one touches a participant. Only the campaign systems in `plugin.json` `systems` (`dnd5e`) run the plugin at all.

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
| **Player settings** | The human player | Campaign options including `lewdFluids`, `lewdInsertedToys`, `lewdLeaks`, consent/narration (`playerOnly`) | Narration, non-consent policy, hard limits, mood buffs, and optional engine sexual dirt via core `soil` |
| **Stance** | The model for NPCs, the player for their PC | `lewd_stance` → scene State or character Traits `lewd_encounter.*` | Whether a character wants a given act, and their own limits, kinks and Inhibition |

`lewdNarration` (`explicit` / `suggestive` / `fade`) only steers narration; mechanics always resolve in full. `lewdNonConsent`: `off` (default) refuses anything a character doesn't want; `not_against_pc` allows it among NPCs but never against the player's character; `on` is the **grimdark switch**: nothing a character merely doesn't want is refused, for advances, brands, imprints, impregnation and forced climax alike. Revoked stance, hard limits and the adults-only rule refuse in every setting.

The same rules apply to every verb, not only `lewd_advance`: a `willing` flag on a brand or imprint can withhold consent but never grant it (the stance decides), and the player's character only becomes more willing, gains allowed partners or loses hard limits through `lewd_stance` with `playerRequest` quoting the player.

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

#### What each stance does

| Stance | Counts as wanted? | Effect on lewd verbs |
|--------|-------------------|----------------------|
| `willing` | Always | Advances, brands, imprints, impregnation and forced climax resolve without a policy check. The `willing`/`accept` flags on brand and imprint are honoured. |
| `selective` | Only from `allowedPartners` (an empty list means anyone) | Wanted from a listed partner, exactly like `willing`. From anyone else it is *unwanted*: it resolves only if `lewdNonConsent` allows it for that target. |
| `unwilling` | Never | Every act is *unwanted*: it resolves only if `lewdNonConsent` allows it (`off` refuses, `not_against_pc` allows NPC targets, `on` allows all). Imprint ticks then get their resist saves, and `accept` is refused. |
| `revoked` | Never | Hard stop for every lewd verb in every setting, including `lewdNonConsent=on`. Removing a brand is the only thing still allowed. |

**Default stance is `willing`, for NPCs and for the player's character alike.** A character with no stance set is treated as willing, so `lewdNonConsent=off` or `not_against_pc` only protects someone once a stance other than `willing` has been recorded (the DM for an NPC, the player through `playerRequest` for their PC). Hard limits (personal and the player's `lewdHardLimits`) refuse regardless of stance. A scene-scope stance overrides the character's default for that scene only; `allowedPartners`, hard/soft limits and kinks from both scopes are merged. Soft limits halve stimulation, kinks multiply it by 1.5, and `inhibition` is added to climax saves.
