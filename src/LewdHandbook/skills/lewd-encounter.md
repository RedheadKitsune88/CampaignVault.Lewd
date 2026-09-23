---
name: lewd-encounter
description: Master skill for CampaignVault lewd_encounter mode — enter/exit, consent State, advance/climax/bind verbs, pools.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
  mode: lewd_encounter
---

# Lewd Encounter (`lewd_encounter`)

Adult opt-in interaction mode. **Engine State is source of truth.** Never invent consent, limits, bindings, or pool values that contradict participant / character State.

Companion skills: `lewd-intimacy-tone`, `lewd-implements-anatomy`, `lewd-bindings`, `lewd-sexual-histories`, `lewd-pregnancy`, `lewd-brands`, `lewd-imprints-conditioning`, `lewd-vices`, `lewd-bad-ending`, `lewd-catalog`.

## When to use

- Campaign wants explicit arousal / stimulation / climax / bondage resolution under `ActiveSystem=dnd5e`.
- Entering or running a lewd scene after table consent and plugin install.
- Any commit that would change arousal, numbing, climax counters, or restraint graph.

## Prerequisites

1. Plugin installed under host `Plugins/LewdHandbook/` (`plugin.json` + `LewdHandbook.dll` + optional `RulesetData/` + `skills/`).
2. Campaign `EnabledModeIds` includes `lewd_encounter`.
3. Campaign option `intimacyTone` set (`consensual` | `fade` | `grimdark`) — see `lewd-intimacy-tone`.
4. Characters have pools `arousal`, `numbing`, `recovery_dice` (RulesetData templates) and Traits for sexual history / anatomy when relevant.

**Install ≠ scene consent.** Enabling the plugin or mode does not make any character willing. Set per-participant `consent` on enter (or before the first advance).

## Enter / exit

Enter via core `mode_transition`:

```json
{
  "$type": "mode_transition",
  "action": "enter",
  "modeId": "lewd_encounter",
  "locationId": "locs/...",
  "participantIds": ["chars/alice", "chars/bob"]
}
```

On enter, ensure each participant State includes at least:

| Key | Values / shape |
|-----|----------------|
| `consent` | `willing` \| `selective` \| `unwilling` \| `revoked` |
| `allowed_partners` | string[] character ids (when `selective`) |
| `hard_limits` | string[] tags — **always fail-closed** |
| `soft_limits` | string[] tags — stim ×0.5 |
| `kinks` | string[] tags — stim ×1.5 |
| `inhibition` | int (locked Int/Wis/Cha mod) |
| `climax_successes` / `climax_failures` | int |
| `edging` | bool |
| `overstimulation` | int 0–6; StatusEffect name `Overstimulation N`, `conditionName` `overstimulation` |
| `climax_streak` / `climax_incapacitated` | repeated climax while still down |
| `had_physical` / `flirt_beats` | verbal-cap tracking |
| `bindings` | array (see `lewd-bindings`) |
| `posture` | string |
| `arm_position` | `free` \| `front` \| `behind` \| `above` \| … |
| `leg_position` | `free` \| `front` \| `behind` \| `apart` \| … |
| `imprints` | trait mirror `id:points:level:origin:day`. See `lewd-imprints-conditioning` |
| `intrusive_thoughts` | comma tokens (`imprint:ordeal`, `vice:sex`, …) |
| `lustbrands` / `lustbrand_inhib` / `lustbrand_glow` | brand list + inhib sum + glow. See `lewd-brands` |
| `pregnant` / `pregnancy_progress` / `pregnancy_type` | see `lewd-pregnancy` |
| `bad_ended` | defeat flag; consequences via `lewd-bad-ending` |

Character Traits/Attributes also hold durable `vice.<id>.*` (see `lewd-vices`) and pregnancy/brand/imprint mirrors outside the mode.

Exit with `mode_transition` action `exit` (or when all participants mark `scene_end`).

## Engine verbs

| `$type` | Use when |
|---------|----------|
| `lewd_advance` | Apply resolved stimulation (martial / indirect / skilled) |
| `lewd_climax_check` | Start-of-turn / forced climax save (DC 15) |
| `lewd_bind` | Add structured binding entry before narrating restraint |
| `lewd_unbind` | Remove binding / clear implies before narrating freedom |
| `lewd_pregnancy` | Impregnate, term, rest poison, termination — see `lewd-pregnancy` |
| `lewd_apply_brand` | Apply, remove, vow, trigger, release, or stabilize a lustbrand — see `lewd-brands` |
| `lewd_bad_end` | Record defeat or explicit bad-end — see `lewd-bad-ending` |
| `lewd_imprint` / `lewd_decondition` | Fetish tracks — see `lewd-imprints-conditioning` |
| `lewd_vice` | Addiction consume/resist/rest — see `lewd-vices` (no mode required) |
| core `rest` / travel / elapsed | Rest + time passage drive imprint/brand/vice/pregnancy observers |
| core `resource` / `status` / `engagement_relation` | Pools, StatusEffects, pairwise holds |
| core `mode_transition` | Enter / exit |

### `lewd_advance`

Prefer supplying resolved `stimulationAmount` / `hit`. When host wires Sdk `IChangeContext.Rolls`, you may omit amount and set `stimulationDice` / `anatomyKey` / `implementId` (and for unwilling martial, `attackBonus` + `targetAc`) so the handler rolls. Unwilling martial still **requires** a hit path.

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "martial",
  "stimulationAmount": 7,
  "stimulationType": "piercing",
  "tags": ["phallic"],
  "hit": true,
  "isCritical": false,
  "maximizeStimulation": false,
  "notes": "cock thrust"
}
```

Rules the handler enforces:

- Must be inside active `lewd_encounter`.
- `revoked` or matching `hard_limits` → commit **fails**.
- Soft/kink tag adjust after amount.
- Numbing absorbs stim first; leftover raises `arousal`.
- At/over max → auto climax-failure path; stim ≥ arousal max → instant climax.
- Lewd verbs do **not** tick a filth counter. Dirt, mud, dust, fluids, and cleanup are yours to record with core changes when the fiction actually changes, including from the ground — not only from sex. Verbal / non-contact beats never dirty anyone by themselves.
- Character: `character_update.appearanceOverride` (e.g. "Covered in mud") and temporary `tagsToAdd` / `tagsToRemove` (`muddy`, `wet`, `disheveled`). Tag the person, not every item they carry.
- Worn or dropped gear: `item_update.tagsToAdd` / `tagsToRemove` and `newState` for a short override ("Covered in mud"). Tag the container, not the contents. Durable marks (stains, scorch) use `upsertItemDetail`; retire the detail when cleaned. Never auto-deleted.
- Verbal/non-contact skilled or indirect advances are capped by sexual history (see `lewd-sexual-histories`). Engine enforces the cap and blocks verbal climax until `had_physical`.
- Climax while `climax_incapacitated`: 2nd stunned, 3rd paralyzed, 4th+ +1 `overstimulation` and stamps `Overstimulation N`. Level ≥5 keeps edging after climax. Level 6 marks `bad_ended`. Triggers, the record verb, and consequences: `lewd-bad-ending`. Do not drain levels in the marking commit.

Stim source resolution: see `lewd-implements-anatomy`. Tone gating: see `lewd-intimacy-tone`.

### `lewd_climax_check`

```json
{
  "$type": "lewd_climax_check",
  "targetId": "chars/bob",
  "d20": 12,
  "forceClimax": false
}
```

- Save: d20 + Inhibition vs DC 15 (use raw Inhibition for climax saves).
- Track successes/failures separately to 3; Nat 1 = two failures; Nat 20 = drop to max−1 and clear edging.
- `forceClimax: true` for effects like `power_word_cum`.

Call at start of turn when edging / at max arousal (mode may flag edging on turn start).

### `lewd_bind` / `lewd_unbind`

Emit **before** narrating cuffs, hobbles, hoods, suits, etc. No prose keyword scanner — if State `bindings[]` does not say it, limbs are free. Full schema: `lewd-bindings`.

```json
{
  "$type": "lewd_bind",
  "targetId": "chars/bob",
  "kind": "cuffs",
  "sites": ["wrists"],
  "implies": ["cuffed"],
  "itemId": "items/iron_cuffs",
  "materials": ["iron"],
  "hardened": false
}
```

## Pools

| Pool | Role |
|------|------|
| `arousal` | Reverse HP; Never recovery; long rest → reduce by half of **maximum** (LLM/handler) |
| `numbing` | Absorbs stim first; does not stack (replace) |
| `recovery_dice` | Spend on climax / short rest to lower arousal; faces from sexual history |

Mirror keys on participant State: `arousal_current`, `arousal_max`.

## Consent — fail closed

1. Read participant `consent` / `hard_limits` / `allowed_partners` **before** narrating willingness.
2. Never narrate consent that contradicts State.
3. `hard_limits` and `revoked` always block advances in **all** intimacy tones.
4. `selective` without actor on `allowed_partners` → refuse.
5. Willing / wanted advances treat Inhibition as `min(0, raw)` for AC/saves vs advances; climax saves still use raw Inhibition unless overridden.
6. Plugin install, campaign enablement, and NPC flirt prose are **not** consent.

## Rests and time passage

Host `RestChange`, `TravelChange`, and any change with `minutesElapsed` are the clock. Observers (not a plugin background tick) react after a successful commit:

- Time-passing changes sync vice withdrawal from `last_hours` vs campaign time.
- Long rest: imprint time-drop when unexposed; brand rest hooks; vice withdrawal saves when addicted and overdue (sheet ability mod + disadvantage).
- Short or long rest while pregnant: Con DC 15 rest poison (sheet Con). `lewd_pregnancy action=rest` is the Rolls-null fallback.
- Rest/status also applies a pending `bad_end_imprint_*` jump (same eagerness as `bad_end_vice_id`).
- Overstimulation −1 on long rest is host-side when the stacking name is `Overstimulation N`.
- If `IChangeContext.Rolls` is null, observers ask you to emit the matching verb with `d20` / ability mod.

There is no plugin API to start a rest. Propose a small host/Sdk helper only if rest commits are missing from the table flow.

## Do not invent

- Consent, hard limits, bindings, posture, pool currents, climax counters.
- New handbook spells/feats when curated YAML already covers the beat — prefer `lewd-catalog`.
- Unstructured “they’re tied up” without `lewd_bind`.
