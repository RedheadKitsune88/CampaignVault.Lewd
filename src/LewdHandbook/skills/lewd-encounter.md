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

Companion skills: `lewd-intimacy-tone`, `lewd-implements-anatomy`, `lewd-bindings`, `lewd-sexual-histories`, `lewd-catalog`.

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
| `bindings` | array (see `lewd-bindings`) |
| `posture` | string / object |

Exit with `mode_transition` action `exit` (or when all participants mark `scene_end`).

## Engine verbs

| `$type` | Use when |
|---------|----------|
| `lewd_advance` | Apply resolved stimulation (martial / indirect / skilled) |
| `lewd_climax_check` | Start-of-turn / forced climax save (DC 15) |
| `lewd_bind` | Add structured binding entry before narrating restraint |
| `lewd_unbind` | Remove binding / clear implies before narrating freedom |
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

## Do not invent

- Consent, hard limits, bindings, posture, pool currents, climax counters.
- New handbook spells/feats when curated YAML already covers the beat — prefer `lewd-catalog`.
- Unstructured “they’re tied up” without `lewd_bind`.
