---
name: lewd-bindings
description: Structured binding graph v1 — participant bindings[] + posture; lewd_bind / lewd_unbind; no prose scanners.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Bindings (graph v1)

Restraint is **structured State**, not prose. There is **no** keyword scanner that infers “cuffed” from narration. If it is not in `bindings[]` / derived StatusEffects, limbs are free.

## When to use

- Applying or removing cuffs, rope, suits, gags, hoods, leashes, encasement.
- Before narrating inability to move, speak, see, cast, or use hands.
- When an ItemDefinition (bondage gear / suit) should seed default bind entries.

## Verbs

| `$type` | Purpose |
|---------|---------|
| `lewd_bind` | Append/merge a bind entry; refresh `implies` + StatusEffects |
| `lewd_unbind` | Remove by id/sites/kind; clear implies when no longer justified |

**Emit bind/unbind before narrating.** Never describe locked wrists then forget the commit.

## Participant State

| Key | Role |
|-----|------|
| `bindings` | Array of bind entries (source of truth) |
| `posture` | Coarse pose (`prone`, `kneeling`, `suspended`, `spread`, …) — does not replace bindings |

Prefer engine `StatusEffects` for handbook bound conditions (`cuffed`, `hobbled`, `encased`, …) derived from `implies`. See `lewd-catalog`.

## Bind entry schema

```json
{
  "id": "bind_01",
  "kind": "cuffs",
  "sites": ["wrists"],
  "orientation": "behind",
  "links": ["anchor:bedpost"],
  "implies": ["cuffed"],
  "materials": ["iron"],
  "hardened": false,
  "effects": { "escapeDc": 20, "breakDc": 20, "hp": 15 },
  "itemId": "items/iron_cuffs"
}
```

| Field | Meaning |
|-------|---------|
| `kind` | `cuffs`, `rope`, `gag`, `hood`, `blindfold`, `mitts`, `hobble`, `harness`, `suit`, `collar`, `leash`, `web`, … |
| `sites` | Body sites: `wrists`, `ankles`, `thighs`, `arms`, `legs`, `mouth`, `eyes`, `head`, `torso`, `whole`, … |
| `orientation` | `front`, `behind`, `above`, `hogtie`, … |
| `links` | Anchors / partners: `anchor:…`, `to:chars/…`, `to:bind_02` |
| `implies` | Condition tokens handlers/LLM must honor: `cuffed`, `hobbled`, `encased`, `gagged`, `mitted`, `blinded`, `deafened`, `suspended`, `leashed`, `full_tied`, `limb_bound` |
| `materials` | `rope`, `leather`, `iron`, `silk`, `latex`, … |
| `hardened` | Magical / masterwork — harder escape; `word_of_safety` may not clear |
| `effects` | Escape/break DCs, HP, leash length, sensory flags |
| `itemId` | Gear that seeded defaults |

### `lewd_bind` example

```json
{
  "$type": "lewd_bind",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "cuffs",
  "sites": ["wrists"],
  "orientation": "behind",
  "implies": ["cuffed"],
  "materials": ["iron"],
  "itemId": "items/iron_cuffs",
  "effects": { "escapeDc": 20, "breakDc": 20, "hp": 15 }
}
```

### `lewd_unbind` example

```json
{
  "$type": "lewd_unbind",
  "targetId": "chars/bob",
  "bindingId": "bind_01"
}
```

Or match `sites` / `kind` / `itemId` when id unknown. After unbind, drop StatusEffects that no remaining entry implies.

## Suit / gear seeding

When `itemId` references a suit or restraint ItemDefinition, seed missing fields from item Properties (e.g. `bindingDefaults`, `implies`, `sites`, `materials`). Do not invent encasement if the item only implies `hobbled`.

## Limb freedom (enforce in narration)

Handlers (and you) treat `implies` as constraints:

| Implies | Freedom loss |
|---------|----------------|
| `cuffed` | Bound limbs unusable / disadv on checks needing them; Dex disadv |
| `limb_bound` | Named limbs fully unusable |
| `mitted` | No fine manipulation; fail Sleight of Hand; no somatic if hands required |
| `hobbled` | Speed ≤ 5 ft |
| `gagged` | No clear speech; no verbal components |
| `encased` | Incapacitated; includes cuffed+hobbled; auto-fail Str/Dex saves |
| `full_tied` | Cuffed + restrained + prone; cannot stand |
| `suspended` | Restrained, no leverage; auto-fail Str/Dex saves |
| `leashed` | Grappled-to-anchor; max distance = leash length |
| `blinded` / hood | See hood rules below |

Respect handbook **Biological Posture Realism**: if posture + clothing + bindings make an action impossible, the action fails (optional Con save DC 13 for strain) — do not narrate impossible straddles / crawls.

## Apply rules (handbook)

| Target state | Bind attempt |
|--------------|--------------|
| Willing | Action; apply one binding |
| Restrained | Contested Athletics/Acrobatics vs grapple or Sleight of Hand |
| Unconscious | Sleight of Hand vs Perception / passive; fail → wakes, no bind |

Default nonmagical gear: ~15 HP, escape Dex DC 20, break Str DC 20 (DM/item override).

Consent: `hard_limits` / `revoked` still fail-closed for coercive binding the table forbids; check `intimacyTone` for unwilling scenes (`lewd-intimacy-tone`).

## Hood / blindfold — agency split

| Subject | Rule |
|---------|------|
| **NPC** | Hood/blindfold may reduce their scene agency (cannot target by sight, disadvantage, rely on sound/touch). You may compress their tactical options. |
| **PC** | Do **not** mute the player. Narrate sensory constraint (darkness, muffled audio) but keep asking the player for intent; translate intent into what remaining senses/limbs allow. Never auto-pilot a hooded PC into compliance. |

`implies` should include `blinded` (and `deafened` if the hood does that). Still require `lewd_bind` before claiming they cannot see.

## Do not

- Scan narration for the word “rope” and invent State.
- Leave stale `implies` after unbind.
- Narrate escape without `lewd_unbind` or a successful escape check + unbind.
- Treat `engagement_relation` grappling as a full binding graph — use both when needed (grapple now, `lewd_bind` for lasting cuffs).
