---
name: lewd-catalog
description: How to use RulesetData spells/conditions/items; prefer StatusEffects; key shipped spell ids.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Lewd RulesetData Catalog

Curated YAML under plugin `RulesetData/dnd5e/`. Host merges last-wins. Prefer these summaries over inventing handbook text.

## When to use

- Casting or referencing a shipped lewd spell.
- Applying sexual / bondage conditions.
- Looking up pool templates or implement stubs.
- Choosing StatusEffects vs inventing string flags.

## Layout

```text
RulesetData/dnd5e/
  pools/        arousal, numbing, recovery_dice
  conditions/   sexual + overstimulation
  feats/        sexual-history stubs
  spells/       priority lewd spells
  items/        finesse_implement (stub)
  classes/      (optional / sparse)
  backgrounds/  (optional / sparse)
```

Read `mechanicalSummary` / description fields from YAML when resolving a beat. Full prose stays in the operator-local handbook — do not paste OCR into commits.

## Prefer engine StatusEffects

For bound and sexual conditions, commit structured `status` / StatusEffects (name, modifiers, duration, recovery hint) rather than only narrating adjectives.

| Prefer StatusEffect / condition id | Instead of |
|------------------------------------|------------|
| `edging`, `overstimulation`, `denied`, `flustered`, `hyperaroused`, `infatuated`, `intoxicated`, `nymphomanic` | Free-text “they’re really turned on” with no State |
| Bind implies → `cuffed`, `hobbled`, `encased`, `gagged`, … | Prose-only “tied up” (see `lewd-bindings`) |

`lewd_bind` should keep `bindings[]` and StatusEffects aligned. Overstimulation is a **stacking** 1–6 condition (level 6 = Bad-Ended). When the engine stamps it, the effect **name** is `Overstimulation N` (space + level) and `conditionName` is `overstimulation`, so a long rest decrements one level. Do not also narrate a full clear.

Example:

```json
{
  "$type": "status",
  "characterId": "chars/bob",
  "action": "apply",
  "effect": {
    "name": "edging",
    "conditionName": "edging",
    "notes": "at max arousal start of turn"
  }
}
```

## Pools (shipped)

| Id | Use |
|----|-----|
| `arousal` | Current / max; Never recovery |
| `numbing` | Absorb stim; Never; replace don’t stack |
| `recovery_dice` | LongRest; faces from `Traits.recovery_die` |

## Conditions (shipped)

| Id | Role |
|----|------|
| `edging` | At max; climax saves; max stim on hits; half move or prone |
| `overstimulation` | Stacking exhaustion-like 1–6 |
| `bad_ended` | Permanent sexual defeat. Engine does not drain levels. See `lewd-bad-ending` |
| `denied` | Cannot climax; auto-succeed climax saves |
| `flustered` | Social stun-like |
| `hyperaroused` | Disadv resist indirect; martial adv vs them |
| `infatuated` | Charmed-like; willing to source; Inhibition benefit suppressed |
| `intoxicated` | Disadv Int/Wis/Cha saves |
| `nymphomanic` | Hyperaroused + intoxicated; willing; Inhibition ≤ 0; must pursue sex |
| `pregnant` | Str/Dex disadv, crit 18–20, rest poison, termination save. Verb: `lewd_pregnancy` |
| `lustbrand:<id>` | Per-brand curse. Name `Lustbrand: <Title>`. Verb: `lewd_apply_brand`. See `lewd-brands` |
| `imprint:<id>` | Level-3 fetish track. Name `Imprint: <Title>`. Verb: `lewd_imprint`. See `lewd-imprints-conditioning` |
| `hyperfertile` / `hypervirile` / `infertile` | Fertility modifiers. See `lewd-pregnancy` |

## Feats (sexual-history stubs shipped)

`virgin`, `willingly_celibate`, `modest_lover`, `experienced_kinkster`, `erotic_professional` — see `lewd-sexual-histories` for the full eight (including `devoted_partner`, `promiscuous`, `strictly_vanilla`).

## Items

Create live instances with `world_build` `items[]` + `definitionName` (copies category/tags/properties/equipZones/equipLayer once). Categories/zones are open strings (`Implement`, `Bondage`, `Jewelry`, custom `nipples`, …).

Contraceptive stubs: `condom`, `oil_of_impotence`, `potion_of_infertility`, `beads_of_prevention`. Pass the matching `contraceptive` on `lewd_pregnancy`; the engine does not consume charges.

| definitionName | category | zones / layer | Role |
|----------------|----------|---------------|------|
| `finesse_implement` | Implement | MainHand / Held | Artificial implement; finesse → Dex for stim bonus |
| `wooden_dildo` | Implement | MainHand / Held | Phallic 1d6 piercing |
| `glass_wand` | Implement | MainHand / Held | Finesse wand 1d4 piercing |
| `vibrating_wand` | Implement | MainHand / Held | Thunder 1d6 vibration |
| `flogger` | Implement | MainHand / Held | Impact 1d4 slashing |
| `paddle` | Implement | MainHand / Held | Impact 1d6 bludgeoning |
| `nipple_clamps` | Jewelry | nipples / Base | Worn clamps; 1d4 piercing on tug |
| `leather_cuffs` | Bondage | Wrists / Base | Cuffs; orientation required |
| `armbinder` | Bondage | Wrists+Hands / Base | Arms rear-bound |
| `spreader_bar` | Bondage | Legs / Base | Ankles apart; hobbled |
| `ball_gag` | Bondage | Face / Base | Gagged |
| `blindfold` | Bondage | Face / Outer | Blinded |
| `hood` | Clothing | Head / Outer | Blinded + gagged |
| `bitchsuit` | Bondage | Torso+Legs+Hands+Head / Base | Encased crawl-suit |
| `tar_bandages` | Bondage | Torso / Base | Hardenable wrap |
| `rope_coil` | Tool | MainHand / Held | Versatile tie |
| `*_pack` | Container | — | Starting kits (not equippable) |

Extend with campaign Items using Properties from `lewd-implements-anatomy`.

## Priority spells (shipped ids)

Use these ids when the ruleset_action / spell commit expects a definition name:

| Id | Snapshot |
|----|----------|
| `handjob` | Skilled/martial touch stim cantrip-tier |
| `lubricate` | Orifice/size safety for penetration |
| `vibration` | Natural implement magical; spellcasting ability; +thunder stim |
| `tentacle` | Conjure tentacle advance source |
| `numbing_sensation` | Apply numbing points |
| `heatwave` | Area/heat stim / hyperarousal pressure |
| `magecock` | Spectral implement; bonus-action move + advance |
| `instant_recovery` | Recovery / arousal relief |
| `word_of_safety` | Safeword; free from nonmagical bindings; end restrained/grappled |
| `incite_lust` | Drive lust / hyperarousal |
| `suppress_inhibition` | Crush Inhibition benefit |
| `stunning_orgasm` | Climax-adjacent control |
| `power_word_cum` | Forced climax → `lewd_climax_check` with `forceClimax: true` |

Example pairing:

```json
{
  "$type": "lewd_climax_check",
  "targetId": "chars/bob",
  "d20": 1,
  "forceClimax": true,
  "notes": "power_word_cum"
}
```

For `word_of_safety` triggers, follow with `lewd_unbind` on non-`hardened` entries and clear restrained/grappled StatusEffects.

## Do not

- Invent spell names that collide with shipped ids but change mechanics.
- Apply bondage conditions without updating `bindings[]`.
- Assume the catalog is the full handbook — it is curated priority content only.

Also see `lewd-vices` for addiction StatusEffects `vice_*` and yaml under `RulesetData/dnd5e/vices/`.
