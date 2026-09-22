---
name: lewd-intimacy-tone
description: Campaign intimacyTone option — consensual, fade, grimdark — and fail-closed hard_limits/revoked.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Intimacy Tone (`intimacyTone`)

Campaign-level content dial for how `lewd_encounter` treats **unwilling** sexual advances. Read from campaign `SystemOptions["intimacyTone"]` (plugin campaign option). Default when unset: **`consensual`**.

## When to use

- Before the first unwilling or contested advance in a scene.
- When operator/table changes tone mid-campaign.
- Any time narration would imply coercion — check tone **and** participant State first.

## Set the option

1. **Read** MCP `get_config` → `systemOptions.intimacyTone`. Missing key ⇒ treat as `consensual`.
2. **Write** via `campaign_update` — `systemOptions` **merges** keys (does not wipe the rest of the bag). Host needs PluginSdk **0.1.2+**.

```json
{
  "$type": "campaign_update",
  "systemOptions": { "intimacyTone": "grimdark" }
}
```

Allowed values: `consensual` | `fade` | `grimdark` (case-insensitive). Re-check with `get_config` after the commit.

## Behavior matrix

| Tone | Unwilling / unwanted advances | Stimulation | Inhibition |
|------|-------------------------------|-------------|------------|
| **consensual** | **Fail the commit** (or refuse before commit). Do not run martial hit path for non-consenting targets. | N/A (blocked) | N/A |
| **fade** | Allow the *attempt* as narrative beat; **apply no stim**. | Force `stimulationAmount: 0` (or skip `lewd_advance` and use a non-stim status/physical nudge only). | May still narrate resistance; do not raise arousal via this beat. |
| **grimdark** | Allow unwilling advances through the engine. | Full stim math | Use full Inhibition on AC / unwanted saves (not the willing→0 rule). |

**Wanted** advances (`consent=willing`, or `selective` + actor on `allowed_partners`) behave the same in all tones: normal stim, Inhibition `min(0, raw)` for advance defense.

## Always fail-closed (every tone)

These **never** succeed, including grimdark and fade:

| State | Action |
|-------|--------|
| `consent: revoked` | Refuse / fail `lewd_advance` and coercive bind attempts that require consent override. |
| Tag in `hard_limits` matches `stimulationType` or `tags` | Fail commit. |
| Soft limits / kinks | Still apply ×0.5 / ×1.5 when an advance **is** allowed. |

Do **not** invent a tone that contradicts campaign options. Do **not** treat fade as “secret stim” or grimdark as license to ignore `hard_limits`.

## Fade — physical-state nudge

When tone is `fade` and the beat would be non-consensual sex:

1. Do **not** raise `arousal` / spend numbing via stim.
2. You **may** commit non-sexual physical outcomes that the table already allows (e.g. `status` grappled/restrained, clothing damage, relocation) — still respect hard limits and revoked.
3. Narrate cutaway / implication; keep mechanical arousal unchanged for that beat.

Example (fade, unwilling — no stim):

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/raider",
  "targetId": "chars/pc",
  "kind": "martial",
  "stimulationAmount": 0,
  "hit": true,
  "tags": ["fade_nudge"],
  "notes": "intimacyTone=fade; physical struggle only"
}
```

Prefer skipping `lewd_advance` entirely and using `status` / `engagement_relation` when the beat is pure restraint.

## Consensual — refuse pattern

If tone is `consensual` and target is `unwilling` (or selective without actor):

- Do not emit a stim advance.
- Narrate refusal, escape, interruption, or renegotiation.
- Offer the player a consent State update if the character chooses to become willing/selective.

## Grimdark — still structured

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/cultist",
  "targetId": "chars/pc",
  "kind": "martial",
  "stimulationAmount": 9,
  "stimulationType": "piercing",
  "hit": true,
  "notes": "intimacyTone=grimdark; Inhibition on AC already resolved"
}
```

Still require `hit` for unwilling martial. Still fail on `hard_limits` / `revoked`.

## Operator checklist

1. Confirm table opted into the chosen tone.
2. Confirm `intimacyTone` in SystemOptions.
3. Confirm each participant `consent` / `hard_limits`.
4. Then choose verb + stim amount.
