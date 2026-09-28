---
name: lewd-encounter
description: lewd_encounter mode — adults only, player settings (narration / non-consent / hard limits), lewd_stance, turns, advance/climax, pools.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
  mode: lewd_encounter
---

# Lewd encounter

Engine State wins. Do not invent stance, limits, bindings, or pool values.

Companions: `lewd-bindings`, `lewd-tracks`, `lewd-catalog`.

## Adults only (hard rule)

Every character a lewd verb touches must have `lifeStage` `adult` or `elder`. `Unspecified` is refused, minors are always refused, and so is any character whose name, appearance, tags or features read as a minor. Set it when you create the character:

```json
{ "$type": "character_update", "characterId": "chars/mara", "lifeStage": "adult" }
```

A character recorded as `child` or `adolescent` can never be changed to adult. No campaign option changes any of this. Characters from before `lifeStage` existed are unset: a context line names the ones in play, so record their stage from the fiction (never guess adult for someone who reads as young).

## Player settings (the player owns these)

| Option | Values | Effect |
|--------|--------|--------|
| `lewdNarration` | `explicit` \| `suggestive` (default) \| `fade` | How you narrate. Mechanics always resolve in full; `fade` = before and after, never the act |
| `lewdNonConsent` | `off` (default) \| `not_against_pc` \| `on` | Whether acts a character doesn't want can resolve. `not_against_pc`: among NPCs only, never against the player's character. `on` is the grimdark setting: no consent refusals for anything a character merely doesn't want (brands, imprints, impregnation, forced climax as well as advances) |
| `lewdHardLimits` | comma list | Content the player never wants. Refused in every verb |

Change them only when the player asks, in its own commit, quoting them:

```json
{ "$type": "campaign_update", "systemOptions": { "lewdNarration": "fade" }, "playerRequest": "can we fade to black from now on?" }
```

## Setup

Campaign `EnabledModeIds` includes `lewd_encounter`. Only the player switches it on or off: `campaign_update` with `playerRequest`, in its own commit.

```json
{ "$type": "mode_transition", "action": "enter", "modeId": "lewd_encounter", "locationId": "locs/…", "participantIds": ["chars/a", "chars/b"] }
```

**One action per participant, on their own turn**: `lewd_advance`, `lewd_bind` or a `lewd_escape` attempt. The first participant listed starts. `mode_transition` `action: turn` hands the action to the next one and runs their start-of-turn rules (edging at max arousal, extended-edging overstimulation, climax incapacitation ending, timed hardening). Several steps can share one commit: `[lewd_advance a→b, mode_transition turn, lewd_advance b→a]`. Saves, stance, recovery dice and DM rulings (`lewd_bad_end`) are free. End: `action: exit` (resolves imprints once per track, clears incapacitation/edging; bindings stay).

## Stance — `lewd_stance` (in-fiction willingness)

The characters' own disposition, separate from the player's settings. Set NPCs' from the fiction; set the PC's from what the player says.

```json
{ "$type": "lewd_stance", "characterId": "chars/b", "stance": "selective", "allowedPartners": ["chars/a"], "kinks": ["rope"], "softLimits": ["fire"], "inhibition": 2 }
```

| Field | Notes |
|-------|-------|
| `stance` | `willing` (default) \| `selective` \| `unwilling` \| `revoked` — `revoked` is a hard stop for every lewd verb |
| `playerRequest` | The player's own words. Needed to make the player's character *more* willing (stance, more allowed partners, fewer hard limits); tightening never needs it |
| `scope` | `scene` (default in an encounter) \| `default` (lasting character Traits `lewd_encounter.*`) |
| `hardLimits` / `softLimits` / `kinks` | Personal. Soft ×0.5, kink ×1.5 stimulation |
| `inhibition` | Int/Wis/Cha modifier chosen at creation |

**What each stance does**

| Stance | Counts as wanted? | Effect on lewd verbs |
|--------|-------------------|----------------------|
| `willing` | Always | Advances, brands, imprints, impregnation and forced climax resolve without a policy check. The `willing`/`accept` flags on brand and imprint are honoured. |
| `selective` | Only from `allowedPartners` (an empty list means anyone) | Wanted from a listed partner, exactly like `willing`. From anyone else it is *unwanted*: it resolves only if `lewdNonConsent` allows it for that target. |
| `unwilling` | Never | Every act is *unwanted*: it resolves only if `lewdNonConsent` allows it (`off` refuses, `not_against_pc` allows NPC targets, `on` allows all). Imprint ticks then get their resist saves, and `accept` is refused. |
| `revoked` | Never | Hard stop for every lewd verb in every setting, including `lewdNonConsent=on`. Removing a brand is the only thing still allowed. |

**Default stance is `willing`, for NPCs and for the player's character alike.** A character with no stance set is treated as willing, so `lewdNonConsent=off` or `not_against_pc` only protects someone once a stance other than `willing` has been recorded (the DM for an NPC, the player through `playerRequest` for their PC). Hard limits (personal and the player's `lewdHardLimits`) refuse regardless of stance. A scene-scope stance overrides the character's default for that scene only; `allowedPartners`, hard/soft limits and kinks from both scopes are merged. Soft limits halve stimulation, kinks multiply it by 1.5, and `inhibition` is added to climax saves.

## Verb visibility

`lewd_advance` and `lewd_bad_end` need the mode and are hidden from the default schema (`ModeId=lewd_encounter`); look them up with `type=`. Every other verb is in the default schema and works in or out of a scene (`lewd_bind`, `lewd_stance`, `lewd_pregnancy`, `lewd_vice`, `lewd_climax_check`, ...).

`lewd_turn_start`, `lewd_scene_end`, `lewd_rest` and `lewd_echo_check` are emitted by the plugin itself; the host refuses them if you send one.

## Who is in the scene

`mode_transition` `join` adds `participantIds` to the running scene (they must be recorded adults and act from the next round); `leave` removes them and the scene goes on; the last one out uses `exit`. The scene also ends by itself when a participant travels elsewhere, is interrupted by an encounter, is knocked to 0 HP, finishes a rest, or combat starts around them. The exit runs the usual wrap-up; bindings stay.

## `lewd_advance`

Needs the mode. Prefer resolved `stimulationAmount`/`hit`. Refused on revoked stance or any hard limit; an unwanted advance resolves only if `lewdNonConsent` allows it for that target.

```json
{ "$type": "lewd_advance", "actorId": "chars/a", "targetId": "chars/b", "kind": "martial", "stimulationAmount": 7, "stimulationType": "piercing", "tags": ["phallic"], "hit": true }
```

Dice source when `stimulationAmount` is omitted: `implementId` → `anatomyKey` → `stimulationDice`, then a held item marked as an implement, then the character's declared implement anatomy (`lewd-catalog`), then derived `hands`. The engine never guesses a receptive part (name it via `anatomyKey` when it does the work). A held weapon is never a toy. Finesse → Dex. Verbal/non-contact: history caps (`lewd-catalog`); no verbal climax until `had_physical` for modest-tier histories. Dirt/fluids: core `character_update`/`item_update`.

## `lewd_climax_check`

Only at the edge (edging or arousal at max) — otherwise refused. Works in or out of an encounter. d20 + Inhibition (minus brand tiers and unwilling imprint levels) vs DC 15; `d20: 0` rolls. `forceClimax` for effects that force one (`power_word_cum`).

```json
{ "$type": "lewd_climax_check", "targetId": "chars/b", "d20": 12 }
```

A climax incapacitates until the end of that participant's next turn. Climaxing again meanwhile: 2nd stunned, 3rd paralyzed, 4th+ +1 overstimulation (`Overstimulation N`), each adding a turn. Level ≥5 keeps edging; 6 marks bad-end.

Right after a climax (before that character's next turn) they may spend Recovery Dice: up to the proficiency bonus, each die + Con lowers arousal, and incapacitation lasts one round per die. With no dice left, an edging climax is a bad end.

```json
{ "$type": "lewd_recover", "characterId": "chars/b", "dice": 2 }
```

Climax save tally: successes and failures stay on the sheet until arousal drops below maximum (edging ends), across scenes.

While incapacitated by a climax, a free action each turn: `{ "$type": "lewd_recover", "characterId": "chars/b", "save": true }` is a Con save, DC 12 + climaxes in the last hour; success ends it. Every climax also asks for a Concentration check, DC 15 + climaxes in the past minute (the campaign clock has hour resolution, so "the past minute" means the same clock reading), unless the effect is a sexual advance.

The climax incapacitation is a real "cannot act" status: the host refuses that character's own actions (attacks, spells, item use, `lewd_advance`, `lewd_bind`, `lewd_escape`) until it ends. Saves, recovery, and anything done to them still work. It ends at the scene end, a completed rest, the Con save, or an hour of campaign time.

Saves the plugin rolls itself honour the handbook's conditions: intoxicated or nymphomanic gives disadvantage on Int/Wis/Cha saves, flustered on Wis/Cha. A nymphomanic or infatuated character's positive Inhibition Bonus counts as 0; that never makes them willing (stance and `lewdNonConsent` still decide).

## Pools

| Pool | Role |
|------|------|
| `arousal` | Reverse HP, starts at 0. Max = recovery die + Con at 1st level, + average + Con per level (re-derived on level-up). Long rest −½ **max**. Max ≤ 0 is a bad end |
| `numbing` | Absorbs stim (replace, no stack); cleared by a long rest |
| `recovery_dice` | One per level; die from `lewd_encounter.recovery_die` or the sexual history. Spend with `lewd_recover` after a climax or a short rest; all back on a long rest |

## Rest / time

A completed rest (not an interrupted one) runs `lewd_rest` on its own: vice withdrawal saves, brand hooks, imprint decay, pregnancy progress and rest save, arousal −½ max and numbing cleared (long), the recovery-dice window (short). Travel and elapsed minutes move vice and pregnancy clocks. If Rolls is null, emit the matching verb with `d20`.

## Do not

Invent State. Leave `lifeStage` unset on a character you mean to include. Change player settings without the player's words. Prose-only “tied up”. Auto-enter the mode on combat downed.
