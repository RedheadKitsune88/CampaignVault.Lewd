---
name: goblin-clans
description: Unified Clans faction lore, raids, terror-release, Clan Mark, speech — Goblin Ponyriders overlay
---

# Unified Clans (Goblin Ponyriders)

Optional high-stakes faction plugin (`com.campaignvault.goblins`). Requires LewdHandbook for bondage/arousal/imprints. Enable with player-owned `goblinClans=on`.

## Lore (original)

Scattered goblin clans unified after tall-folk raids and slave hunts. The **Cult of the Dragon** magically enhanced several goblin shamans as a terror weapon — sharper minds, shared doctrine — then **lost control**. The shamans turned that gift into the **Unified Clans**: revenge culture, living mounts (**Goblin Ponyriders**), alive capture, and marked warnings. No gore doctrine; terror and stock over corpses.

## Faction seed

Id: `factions/unified_clans` · Type: MilitaryOrder · Theme metadata: `goblin_ponyriders`.

**Installing or enabling this plugin does not create the faction.** Existing campaigns need an explicit `world_build` (guidance nudges when `goblinClans=on`). When the faction is already loaded, `goblin_state` action=`seed_faction` refreshes lore/metadata; when missing it fails with the world_build payload.

## Gates

- `goblinClans` off → verbs inert (except status/seed messaging).
- Honour LewdHandbook `lewdNonConsent`, revoked stance, age gate, `lewdHardLimits` + `goblinHardLimits`.
- Adults only.

## Core verbs

`$type: goblin_state` with `action`:

| action | purpose |
|--------|---------|
| seed_faction | refresh Unified Clans when loaded; else fail with world_build hint |
| capture | Capture State + Defiance Clock + training day 1 |
| release | end capture; `terrorRelease=true` for male/warning release + rumor |
| defiance | adjust Defiance Clock 0–10 (`delta` or `absolute`) |
| mark | Clan Mark 1–5 stigma/command overlay |
| role | assign catalog role; prints Lewd bind hint |
| train | advance raid_camp → village → assigned |
| name | humiliating assigned name / tattoo label |
| status | snapshot traits |

## Doctrine (play)

- **Alive capture** priority on fertile targets; nets/bolas/grapple before lethal.
- **Riding-Girl mounts** in every scout/raid party (upright/bent-seat only in field).
- **Terror-release**: stripped/minimally bound messengers; seed rumors.
- **Clan Mark**: slow visible tattoo overlay; stacks with Lewd imprints/brands, does not replace them.
- **Speech**: broken Common, vicious taunts; no modern kink labels until witnessed in-fiction.
- **Bondage**: always `lewd_bind` / `lewd_insert` — roles are presets, not a second engine.

## Creatures / items

Creatures: Goblin Ponyrider, Goblin Clan Shaman, Riding-Girl Mount.  
Items: Clan Iron Collar, Riding Saddle Harness, Clan Jingle Harness.
