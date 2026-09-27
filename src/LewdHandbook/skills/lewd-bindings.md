---
name: lewd-bindings
description: Restraint on the character, in or out of scenes — lewd_bind, lewd_unbind, lewd_escape; anchors (chains, leashes, posts), locks and keys, escape/break/pick.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Bindings

Restraint is State, not prose. If it is not in the bindings (and the conditions they stamp), limbs are free.

Bindings live on the **character** and work anywhere: a sex scene, a captive after combat, a road gang of convicts. Adults only (same rule as every lewd verb). The sheet shows a **`Bound`** status listing each binding with its id, DCs, lock and anchor, plus the conditions it implies (`cuffed`, `hobbled`, `gagged`, `mitted`, `limb_bound`, `leashed`, `encased`, `full_tied`, `suspended`, `restrained`, `blinded`). Spell components are blocked while they apply.

## Putting it on — `lewd_bind`

Emit **before** narrating the restraint.

```json
{ "$type": "lewd_bind", "actorId": "chars/warden", "targetId": "chars/convict", "kind": "shackles", "sites": ["ankles"], "anchorId": "chars/lead_convict", "keyItemId": "items/warden_key" }
```

| Field | Notes |
|-------|-------|
| `kind` | `cuffs`, `manacles` (locked), `shackles` (ankles, locked, hobble), `rope`, `gag`, `hood`, `collar`, `leash`/`chain`, `armbinder`, `spreader`, `bitchsuit`… — the kind alone seeds sensible defaults |
| `sites` / `orientation` | `wrists`/`ankles`/… and `behind`/`front`/`above`/`together`/`apart`/`hogtie`. Arms behind the back block hands and somatic components |
| `implies` / `effects` | Extra condition tokens / tags |
| `anchorId` | What they're tied to: a character (`chars/…`), an item or fixture (`items/wall_ring`), or a named post. **They cannot travel unless the anchor goes too, in the same commit** |
| `locked` / `lockDc` / `keyItemId` | A lock (DC 15 by default). With a `keyItemId`, `lewd_unbind` needs that key |
| `escapeDc` / `breakDc` / `hp` | Default 20 / 20 / 15. `hardened` or `hardenAtRound` → 25 |
| `willing` | The target submits. Otherwise an unwilling target must be **subdued first**: grappled (e.g. by the actor), restrained, incapacitated, unconscious, paralyzed, stunned, already bound, or at 0 HP |
| `erotic` | Omitted: true inside a lewd_encounter or for erotic gear (suit, spreader…). Erotic binds follow the lewd consent rules (stance, limits, `lewdNonConsent`); plain restraint only the player's hard limits |
| `posture` | `kneeling`, `prone`, `all_fours_crawl`… kept until the last binding comes off |

Inside a scene, binding someone is the actor's action for the turn.

## Moving a chained group

A binding with an anchor pins the character: core refuses their `travel` ("cannot travel because they are bound to …"). To move a coffle, a leashed captive or a prisoner and escort, send one `travel` per character **in the same commit** to the same destination. Anyone anchored to a post or item stays until freed.

## Getting out — `lewd_escape`

```json
{ "$type": "lewd_escape", "characterId": "chars/convict", "method": "slip", "d20": 0 }
```

| method | Roll | Notes |
|--------|------|-------|
| `slip` | Dex (or `ability: str`) + `bonus` vs escape DC | The bound character only. Disadvantage when encased, hog-tied or suspended |
| `break` | Str + `bonus` vs break DC | Self or a helper (`actorId`) |
| `pick` | Dex + `bonus` (tools) vs lock DC | Locked only. Bound hands can't pick their own lock: a helper must |
| `unlock` | — | With `keyItemId` matching the lock's key |
| `cut` | — | `amount` damage off the binding's hp; 0 hp and it's gone |

`d20: 0` lets the host roll. In a scene, an attempt is the character's action. Success removes that binding (and its anchor); failure leaves it. `bindingId` picks one; otherwise the most recent.

## Taking it off — `lewd_unbind`

```json
{ "$type": "lewd_unbind", "actorId": "chars/warden", "targetId": "chars/convict", "bindingId": "3f9a12bc", "keyItemId": "items/warden_key" }
```

By `bindingId`, `itemId`, or `removeAll: true`; otherwise the most recent. A selector that matches nothing fails and lists the bindings. Conditions from other sources (a web spell, a grapple) stay.

## Do not

Prose-only ties or escapes. Tie up an unwilling character who is not subdued (grapple first). Move one member of a chained group alone. Skip `orientation` when the pose matters.
