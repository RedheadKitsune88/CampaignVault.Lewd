---
name: lewd-pregnancy
description: Pregnancy and fertility — lewd_pregnancy verb, contraceptives, rest poison, term progress. Engine state is source of truth.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Pregnancy

Adult opt-in. Persist pregnancy on the **character** (`SystemStats.Traits`), not only encounter scratch. The handler mirrors onto the participant when `lewd_encounter` is active.

## When to emit `lewd_pregnancy`

After potentially reproductive sex, a fertility spell, a crit against a pregnant creature, or birth/termination. Host `RestChange` rolls rest poison while pregnant when Rolls is available; emit `action=rest` only as the fallback. Do not tick pregnancy from `lewd_advance`.

```json
{
  "$type": "lewd_pregnancy",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "action": "impregnate",
  "kind": "traditional",
  "d20": 14,
  "secondD20": 18,
  "actorConModifier": 2,
  "targetConModifier": 1,
  "targetProficiency": 2,
  "contraceptive": "none"
}
```

| `action` | Meaning |
|----------|---------|
| `impregnate` | Traditional Con check or nontraditional Con save |
| `advance` | Move `pregnancy_progress` (0–100). Omit `progressDelta` to use the default |
| `rest` | Fallback when host `RestChange` had no Rolls. DC 15 Con. Failure stamps Poisoned (1d4 hours — you remove it). Omit `targetConModifier` to use sheet Con |
| `termination_save` | Crit against a pregnant creature. `dc` = damage taken |
| `terminate` | Birth, spell, or a failed termination save you already resolved |

Traditional DC = 10 + target Con mod + target proficiency. Condom forces DC 25. Oil, beads, or infertile (either party) **fail the check** and still commit. Beads also block nontraditional. Pregnancy Ward: set `autoSucceedSave` (and infertile for checks).

Nontraditional: you set `dc`. Unwanted saves add `inhibitionBonus` only under `intimacyTone: grimdark`. Hyperfertile target or hypervirile source: pass `secondD20` (disadvantage). Hyperfertile on a traditional check: pass `secondD20` (advantage).

`force: true` is Power Word Pregnant: halfway progress (50) if not pregnant; doubles `pregnancy_offspring` if already pregnant. Still fail-closed on hard limits (`pregnancy`, `impregnation`, `breeding`, `repro`) and on unwilling targets unless grimdark.

## State

| Trait | Values |
|-------|--------|
| `pregnant` | `true` / `false` |
| `pregnancy_progress` | 0–100 |
| `pregnancy_type` | `traditional` \| `nontraditional` |
| `pregnancy_source` | impregnator id |
| `pregnancy_offspring` | int |

Default term step: traditional +1 per `advance` (long rest), nontraditional +25. At 100 the engine nudges you to narrate birth and `terminate`. It does not birth by itself.

## Effects you still adjudicate

StatusEffect `pregnant` is the reminder. You apply disadvantage on Str/Dex saves, crit range 18–20, and the rest/termination verbs. Do not drain levels on `noEscape`; that flag only sets `bad_ended`. Consequence is a later commit — see `lewd-bad-ending`.

Fertility conditions: `hyperfertile`, `infertile`, `hypervirile`. Stamp them (or set the matching trait) when a spell or item says so. Items `condom`, `oil_of_impotence`, `potion_of_infertility`, `beads_of_prevention` are catalog stubs — you track duration and bead charges.
