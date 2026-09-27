---
name: lewd-catalog
description: RulesetData spells/conditions/items; sexual_history; anatomy/implements for lewd_advance.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Catalog

Plugin `RulesetData/dnd5e/` merges last-wins. Prefer StatusEffects over free-text adjectives. Prefer YAML ids over invented spells.

## Histories

`Traits["lewd_encounter.sexual_history"]` + `Traits["lewd_encounter.recovery_die"]` (+ optional `lewd_encounter.implement_proficiencies`). Mode-prefixed so NPC cards only show them in an active `lewd_encounter`.

| Id | Die | Notes |
|----|-----|-------|
| `virgin` | d6 | No natural pleasure proficiency; 1 artificial; verbal cap |
| `willingly_celibate` | d6 | No artificial; verbal cap |
| `strictly_vanilla` | d6 | Repro natural only; ≤2 artificial; verbal cap |
| `modest_lover` | d8 | ≤4 artificial; verbal cap |
| `devoted_partner` | d8 | ≤4 artificial; modest vs strangers |
| `promiscuous` | d8 | ≤6 artificial; full verbal |
| `experienced_kinkster` | d10 | ≤8 artificial; full verbal |
| `erotic_professional` | d12 | ≤8 artificial; full verbal |

Verbal/non-contact stim: modest-tier max +1–2 / no climax until `had_physical`. Engine enforces cap. Recovery dice spend on climax/short rest; long rest regain + arousal −½ max.

## Anatomy / implements

Natural body parts are Traits `lewd_encounter.anatomy.<slot>` (shown on an NPC card only inside a scene). Value: `die=1d8;tags=phallic,natural;size=medium;finesse=false;role=implement|receptive|both`, every part optional. `role` defaults to the slot's role; an unknown slot is an implement. `none` declares the part absent.

| Slot | Role | Declared by |
|------|------|-------------|
| `hands` | implement, finesse, 1d4 | derived — every humanoid |
| `mouth` | both, 1d4 | derived — every humanoid |
| `ass` | receptive | derived — every humanoid |
| `cock` | implement, 1d8 | **you declare** (aliases: dick, penis) |
| `pussy` | receptive | **you declare** (aliases: vagina) |
| `breasts` | receptive | **you declare** (aliases: tits, chest) |
| `tail` | implement, 1d4 | you declare, when they have one |

- The plugin derives `hands`, `mouth` and `ass` for anyone with no entry, and writes them (marked `source=default`) onto characters that already have a lewd profile (anatomy, history, stance, limits, kinks). Overwrite one to customise it, or set `none` when they lack it.
- Bodies differ, so **you** declare `cock`, `pussy` and `breasts` (`none` when absent) plus anything unusual (`tail`, tentacles, a second cock). On entering `lewd_encounter` the engine lists every adult participant that has not answered; declare before narrating anatomy-dependent acts.
- Non-humanoid or shapeless body (slime, construct, beast): set `lewd_encounter.anatomy.plan=custom` and declare every part yourself; nothing is derived.
- The engine never *guesses* a receptive part (`pussy`, `breasts`, `ass`) as the dice source. Name it with `anatomyKey` when it does the work in the act.

Artificial implements: Item Properties `damageDice`/`damageType`/`implementTags`/`finesse`.

`lewd_advance` resolve order: `implementId` → `anatomyKey` → explicit amount/dice → a held item marked as an implement → the character's declared implement parts (pure implements before the mouth; catalogue order) → derived `hands`. Finesse → Dex.

## Conditions / pools

Pools: `arousal`, `numbing`, `recovery_dice`. Overstim name `Overstimulation N`, `conditionName` `overstimulation` (long rest −1). Also: `edging`, `denied`, `flustered`, `hyperaroused`, `infatuated`, `intoxicated`, `nymphomanic`, `pregnant`, lustbrand/imprint/vice stamps (see `lewd-tracks`).

## Items (definitionName)

Implements: `finesse_implement`, `wooden_dildo`, `glass_wand`, `vibrating_wand`, `flogger`, `paddle`. Bondage: `leather_cuffs`, `armbinder`, `spreader_bar`, `ball_gag`, `blindfold`, `hood`, `bitchsuit`, `tar_bandages`, `rope_coil`. Jewelry: `nipple_clamps`. Contraceptives: `condom`, `oil_of_impotence`, `potion_of_infertility`, `beads_of_prevention`. Spawn via `world_build` + `definitionName`.

## Spells (priority ids)

`handjob`, `lubricate`, `vibration`, `tentacle`, `numbing_sensation`, `heatwave`, `magecock`, `instant_recovery`, `word_of_safety` (+`lewd_unbind` non-hardened), `incite_lust`, `suppress_inhibition`, `stunning_orgasm`, `power_word_cum` → `lewd_climax_check` `forceClimax`.

## Do not

Collide shipped spell ids with different mechanics. Bondage StatusEffects without `bindings[]`. Treat this as the full handbook — priority curated only.
