---
name: lewd-imprints-conditioning
description: Gainable fetish tracks (wanton, training, breeding, ordeal, cruelty), resist saves, decondition, bad-end jump.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Imprints / Conditioning

The engine is the source of truth for imprint tracks. Do not invent an 8-category fetish list. Do not add a vice or addiction track here.

## Tracks

| Id | Captures |
|----|----------|
| `wanton` | Public, multi, promiscuous exposure |
| `training` | Obedience, petplay, bitchsuit. Not its own category |
| `breeding` | Repro-tagged climaxes, seedbed identity |
| `ordeal` | Forced climax, denial, incapacitated overstim, bad-end lean |
| `cruelty` | Inflicting pain or ruin |

Each track is `id:points:level:origin:day` in trait `imprints`, joined by `|`. Levels: 3 points → 1, 7 → 2, 12 → 3. Origin is `willing` or `unwilling`.

## When the engine ticks

- `lewd_imprint` always, unless a hard limit blocks it or an unwilling save negates it.
- Physical `lewd_advance` when tags match a track. Verbal / non-contact does not tick.
- Cruelty tags tick the actor. The other tracks tick the target.
- `lewd_bind` of a bitchsuit ticks `training`.
- A climax while already incapacitated, or a forced climax, suggests `ordeal`.
- Hard-limit tags never imprint. `intimacyTone=consensual` skips unwilling auto-ticks.

Willing exposure (consent `willing`, or `willing=true` on the verb) ticks with no save. Unwilling exposure needs a WIS or INT save, actor's choice. DC is 12 + current level + `dcMod`. `abilityMod` is on the roll. Success negates the tick. If Rolls is missing, the beat only suggests the verb — it does not tick.

A later willing tick does not convert origin. `accept=true` converts an existing track to willing when consent is not `unwilling` or `revoked`.

## Payloads

Willing: kink tag, stim ×1.5 on that tag. Level 2+ : narrate advantage 1/rest on aligned seduction or training checks. Intrusive thought token only at level 3.

Unwilling: same kink tag (not a soft limit — the body betrays them) plus `intrusive_thoughts` token `imprint:<id>` from level 1. Inhibition drops by the sum of unwilling levels (`imprint_inhib`). Narrate disadvantage on non-aligned actions while that category is in the scene. The engine does not apply that disadvantage to host rolls. No buffs until `accept`.

Level 3 stamps StatusEffect `imprint:<id>` (`Imprint: <Title>`, Manual). Feat stubs: `wanton` → `wanton_whore`, `training` and `ordeal` → `edge_puppet`. Training level 3 is an obey-save vs the owner, DC 15; the engine does not roll it. Cruelty level 3 adds +1 stimulation when that bearer inflicts pain or the target is overstimulated.

## Verbs

```json
{ "$type": "lewd_imprint", "targetId": "chars/bob", "category": "training", "source": "training", "willing": false, "ability": "wis", "d20": 8, "abilityMod": 1 }
```

`source`: `training` | `wanton` | `bad_end` | `cruelty` | `exposure`. `delta` is 1–3 (default 1). Does not require `lewd_encounter`, but hard limits are read from the participant when one is present.

```json
{ "$type": "lewd_decondition", "targetId": "chars/bob", "category": "ordeal", "method": "therapy", "d20": 14, "wisMod": 2 }
```

`method`: `therapy` | `aftercare` | `rest`. WIS save DC 10 + level. Unwilling origin is DC +2 unless `method=therapy`. `aftercare`, or trait `sexual_history` containing devoted, grants advantage (`d20Other` or a second roll). Success drops 1–3 points (margin 0–4 / 5–9 / 10+). `rest` is DC 14 and drops 1. Level 3 unwilling does not drop on rest — therapy, or several successful rests narrated as a month of avoidance. Aligned exposure since the last tick cancels that rest's drop.

Refuse decondition of `breeding` while Brand of Fertility is live, and `training` while Brand of Obedience is live.

## Bad-end jump

`lewd_bad_end` `consequence=imprint` only stores the jump. The next imprint write **or** a rest/status observer applies `bad_end_imprint_track` at `bad_end_imprint_jump` (cap 3), sets origin from `bad_end_imprint_origin`, and clears the pending keys. Do not emit a drain. Do not emit `lewd_vice`. Omit `abilityMod` / `wisMod` to read the sheet.
