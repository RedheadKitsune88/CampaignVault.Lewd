---
name: lewd-bad-ending
description: Bad ending status - consequences, lewd_bad_end verb and its parameters. Engine state is source of truth. 
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Bad-Ending

The engine is the source of truth for `bad_ended`. You choose the consequence on a **later** `take_turn`, never inside the commit that set the flag. This plugin does not drain a class level.

## Triggers

| Trigger | Engine |
|---------|--------|
| Overstimulation 6 | Marks `overstim` |
| Climax while edging and `recovery_dice.Current` ≤ 0 | Marks `empty_recovery` |
| `recovery_dice` pool absent | Does not mark. Do not treat a missing pool as zero |
| Arousal pool exists and `Max` ≤ 0 | Marks `arousal_max` |
| HP ≤ 0 | Does not mark. Emit `lewd_bad_end` only if there is no rescue |
| Pregnancy `noEscape` after impregnation | Marks `capture_impreg` |
| `lewd_bad_end` | Marks `defeat` or `explicit` |

Dropping overstimulation does not clear the flag. Do not set `scene_end`.

## Record verb

```json
{ "$type": "lewd_bad_end", "targetId": "chars/bob", "reason": "defeat", "noEscape": true, "consequence": "slave" }
```

`reason` is `defeat` or `explicit`. `noEscape` must be true. Fails if consent is `revoked` or `intimacyTone` is `consensual`. `fade` records and nudges a non-graphic fade. `grimdark` records.

`consequence` is stored, not applied (except the imprint request, which is also only stored):

- `level_drain` (handbook default, optional): narrate the lost features. There is no drain verb. Do not emit `level_up`. Negative `xp_grant` does not drop a level. A later `character_update` may patch `systemStats.level` / `classLevels`; the plugin will not check it.
- `imprint`: pass `imprintTrack` (`wanton`, `training`, `breeding`, `ordeal`, `cruelty`) and `imprintJump` (usually 3). This commit only stores the jump. The next imprint write applies it. See `lewd-imprints-conditioning`.
- `slave`, `seedbed`, `curse`, `class_change`, `narrated`: `event` plus narration. Curse may also be `status`. Do not invent a class-swap verb.
- `lustbrand`: store it on this commit. On a later `take_turn`, emit `lewd_apply_brand`. Do not apply the brand inside the marking commit.
- `vice`: pass `viceId`. Stored as `bad_end_vice_id`. First later `lewd_vice` or the rest observer applies addicted at base DC — see `lewd-vices`.

Defiled Hero and Mark of the Beast are not shipped. Narrate only.
