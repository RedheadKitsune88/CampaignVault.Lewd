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

`lewd_bind` should keep `bindings[]` and StatusEffects aligned. Overstimulation is a **stacking** 1–6 condition (level 6 = Bad-Ended).

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
| `denied` | Cannot climax; auto-succeed climax saves |
| `flustered` | Social stun-like |
| `hyperaroused` | Disadv resist indirect; martial adv vs them |
| `infatuated` | Charmed-like; willing to source; Inhibition benefit suppressed |
| `intoxicated` | Disadv Int/Wis/Cha saves |
| `nymphomanic` | Hyperaroused + intoxicated; willing; Inhibition ≤ 0; must pursue sex |

## Feats (sexual-history stubs shipped)

`virgin`, `willingly_celibate`, `modest_lover`, `experienced_kinkster`, `erotic_professional` — see `lewd-sexual-histories` for the full eight (including `devoted_partner`, `promiscuous`, `strictly_vanilla`).

## Items

| Id | Role |
|----|------|
| `finesse_implement` | Artificial implement; finesse → Dex for stim bonus |

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
