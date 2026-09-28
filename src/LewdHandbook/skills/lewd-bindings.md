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
| `sites` / `orientation` | Body slots: `wrists`, `arms`, `elbows`, `thighs`, `ankles`, `torso`, `neck`, `mouth`, `eyes` (`hands`, `legs`, `feet`, `head`… map onto them). Orientation: `behind`/`front`/`above`/`belt`/`collar`/`together`/`apart`/`hogtie`. Wrists tied to the `belt` or `collar` pin the hands (in front of the body) unless the tie is loose |
| `implies` / `effects` | Extra condition tokens / tags |
| `anchorId` / `holderId` / `slackFeet` | What they're tied to: a character (`chars/…`, who holds their own end), an item or fixture (`items/wall_ring`, `fixture:post`). `holderId` names who holds the end of an item anchor (a rider with the rope on a saddle horn): if the holder is put down the tie falls away. **They cannot travel unless the anchor or holder goes too, in the same commit.** It is a core `tether`, so `tether strain` (a check against the binding's break DC) frees it, and the binding drops its anchor. At most four per character |
| `locked` / `lockDc` / `keyItemId` | A lock (DC 15 by default). With a `keyItemId`, `lewd_unbind` needs that key |
| `escapeDc` / `breakDc` / `hp` | Default 20 / 20 / 15. `hardened` or `hardenAtRound` → 25 |
| `slack` / `fit` | `slack` is `tight` (default) or `loose`, the play a tie leaves. Loose ankles walk at **15 ft** instead of 5; loose wrists (long slack) leave the hands **awkwardly usable** (disadvantage on attacks and Dex) instead of pinned, and are easier to slip (escape DC −4). One tight tie on a slot makes it tight. `fit` is a few words on how it is fitted (`"wrists to belt, 25 cm chain"`, `"75 cm hobble cord"`): not mechanics, it rides in the summary and you rule on what it allows (can she reach the buckle? hop the stairs?) with an ordinary check |
| `materials` / `quality` | With no explicit DCs, the toughest known material sets the base escape/break DC (cloth 12/10, rope 15/13, leather 16/15, chain 18/20, iron 20/22, steel 22/24, adamantine 26/28…). `quality` (`crude` −4, `poor` −2, `standard`, `fine` +2, `masterwork` +4, `enchanted` +6) shifts escape, break and lock DCs |
| `willing` | The target submits. Otherwise an unwilling target must be **subdued first**: grappled (e.g. by the actor), restrained, incapacitated, unconscious, paralyzed, stunned, already bound, or at 0 HP |
| `erotic` | Omitted: true inside a lewd_encounter or for erotic gear (suit, spreader…). Erotic binds follow the lewd consent rules (stance, limits, `lewdNonConsent`); plain restraint only the player's hard limits |
| `posture` | `kneeling`, `prone`, `all_fours_crawl`… kept until the last binding comes off |

Inside a scene, binding someone is the actor's action for the turn.

## What the slots mean

Effects come from which slots are taken, not from what the tie is called, so ties of different kinds add up:

| Occupied | Effect |
|----------|--------|
| wrists, arms or elbows | `cuffed` (disadvantage on Dex) |
| wrists behind, above, at the belt or collar (tight), or two of wrists/arms/elbows | hands unusable, no somatic components |
| the same, but loose | hands awkward: disadvantage on attacks and Dex, somatic still possible |
| wrists plus arms or elbows | `limb_bound` |
| ankles or thighs | `hobbled`: speed 5 ft (loose: 15 ft) |
| wrists + ankles with `posture: hogtie` (or orientation `hogtie`) | `full_tied`, prone |
| mouth | `gagged`, no verbal components |
| eyes | `blinded` |
| an anchor | `leashed` (tethered) |

Ties are presets that fill in sites, orientation and DCs. Every bind and unbind gives you one line, `Slots [wrists behind, mouth] → hands unusable, no somatic components; no verbal components; …`, so you always know what the character can do.

## Time in restraints

Limb-binding ties wear the character down as the clock runs, automatically, **arms and legs separately, and the two stack**:

| Bound for | Arms (wrists, arms, elbows) | Legs (thighs, ankles) | Willpower |
|-----------|-----------------------------|-----------------------|-----------|
| 4h | Cramped arms: −1 attack | Stiff legs: −5 ft speed | −5 per area |
| 12h | Numb arms: −2 attack, −2 Sleight of Hand | Numb legs: −10 ft, −2 Athletics | −10 |
| 24h | Dead arms: −3 attack, −3 Sleight of Hand | Failing legs: −20 ft, −3 Athletics | −15 |

These are consequences of the restraint, tagged `restraint_aftermath`, not events: they do not count toward the two-event-debuff cap. Each lasts 8 or 24 hours and is refreshed while the tie stays on, so it fades after they are freed. Consensual scene gear stops at the first stage unless `lewdNonConsent=on`; a captive climbs the whole ladder. Nothing to emit; the engine reports each step.

**Willpower** (0–100, default 75) matters everywhere now, not just here: charm, fear, compulsion and mental saves move with it (90+ +1, 30–59 −1, 10–29 −2, under 10 −3 and disadvantage; each roll says so). Captivity drains it as above; each 4 hour rest step gives back 5 of what was drained (never a value you set with `attribute`).

**Conditions are mechanics now.** Hands bound behind cost advantage on attacks, bound limbs or hobbles on Dex checks and saves, a blindfold on attacks and Perception; intoxicated or flustered characters roll mental saves at disadvantage. Hobbled or encased characters move at 5 ft, which slows a march. An escape attempt is exempt from the restraint it is escaping.

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
