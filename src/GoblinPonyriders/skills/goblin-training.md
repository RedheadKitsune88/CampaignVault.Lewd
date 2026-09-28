---
name: goblin-training
description: Capture training phases, 8 roles, restraint presets, loyalty/punishment — maps to LewdHandbook binds
---

# Training & Roles

## Capture State

`goblin_state` action=`capture` stamps Capture State + Defiance ~7 + training `raid_camp` day 1.  
Immediately follow with LewdHandbook `lewd_bind` (collar + hobble + wrists + gag). One deliberate exploitable flaw per restraint set.

## Training phases

| phase | days | beat |
|-------|------|------|
| raid_camp | 1–3 | will-break: rut, gag service, hood when idle, beg windows |
| village | 4–7 | structured drills, parades, role seeding, naming ritual |
| assigned | 8+ | living in a catalog role |

Advance with `goblin_state` action=`train` (optional `phase` override).

**Tests (village entry):** cargo pony (load crawl) and riding pony (mock march) — CON saves; failures bias toward heavy_labor / breeder / cock_sleeve; both-pass opens riding_girl.

**Naming ritual:** public use + assigned name (`action=name`) + Clan Mark raise (`action=mark`). Naming auto-stamps locked `goblins.ear_notch_iron` via core `piercing`.

## Roles → Lewd presets

| roleId | field? | Lewd bind hint | Piercing kit |
|--------|--------|----------------|--------------|
| riding_girl | yes | collar+reins, bit, wrist-to-collar, upright/bent-seat; never quad in field | `goblins.septum_lead` (leash_ring) |
| domestic | no | collar+leash, light hobble | — |
| entertainer | no | jingle/quad harness, bells, muzzle + insert | `goblins.jingle_iron` both nipples (heavy+bell) |
| heavy_labor | no | yoke/beam, ankle chain, insert during work | — |
| slave_warrior | no | collar, light hobble, wrists to belt | — |
| pet_caster | no | entertainer off-duty; free wrists under guard to cast | — |
| breeder | no | stocks/stake, retention plugs, breeding muzzle | — |
| cock_sleeve | no | quad harness, anal hook chains, crawl transport | — |

Terror-release leaves locked `goblins.heavy_nose_ring` + rumor. Public `lewd_humiliate` severity ≥2 while captured raises Defiance +1.

Assign with `goblin_state` action=`role` roleId=...

## Defiance Clock (0–10)

High = sabotage/escape plotting; low = compliance. Lewd binding/imprint/forced climax while captured auto-nudges via plugin event hooks. Telegraph every goblin scene open.

## Clan Mark (1–5)

Stigma + command bias near clans. Does not replace Lewd lustbrands/imprints. Level 3+ WIS vs commands; 5 deep overlay (player rollback still applies).

## Punishment / loyalty (skill-driven)

Infractions → `defiance` up + harsher role or spiked insert via `lewd_bind`/`lewd_insert`.  
Loyalty tests (shaft-use, public begging, restraint application, display pose) — role variance in narration; success may lighten role toward domestic/riding_girl.

## State snapshot (every clan scene)

Open with sensory-only line: restraints, Defiance telegraph, Clan Mark level, role kit, assigned name, filth/arousal from Lewd pools — no OOC role labels in PC-facing prose.
