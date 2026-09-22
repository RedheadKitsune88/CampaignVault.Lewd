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
| `posture` | Coarse whole-body pose (`standing`, `kneeling`, `prone`, `suspended`, `all_fours_crawl`, …) |
| `arm_position` | Derived from wrist/arm binds: `free` \| `front` \| `behind` \| `above` \| `together` \| `crossed` \| `folded` |
| `leg_position` | Derived from ankle/leg binds: `free` \| `front` \| `behind` \| `apart` \| `together` \| `crossed` \| `folded` |
| `binding_implies` | Aggregated condition tokens |
| `binding_effects` | Aggregated effect tags |

Prefer engine `StatusEffects` for handbook bound conditions (`cuffed`, `hobbled`, `encased`, …) derived from `implies`. See `lewd-catalog`.


## Limb positions (arms & legs)

Cuffs and rope do **not** imply a pose by themselves. Always set `orientation` on `lewd_bind` (or Item `properties.orientation`).

| Orientation | Typical sites | State update | Narration / casting |
|-------------|---------------|--------------|---------------------|
| `behind` | `wrists`, `arms` | `arm_position=behind` | Hands at the back; no fine manipulation; stamps `no_somatic_spellcasting` |
| `front` | `wrists`, `arms` | `arm_position=front` | Hands before the body; can still see/use limited gestures; still `cuffed` |
| `above` | `wrists`, `arms` | `arm_position=above` | Arms raised / overhead; hands useless for tools |
| `together` | wrists or ankles | matching limb `together` | Limbs bound to each other |
| `apart` | `ankles` (+ spreader) | `leg_position=apart` | Forced open stance / hobble-spread |
| `crossed` | wrists or ankles | matching limb `crossed` | Crossed and locked |
| `folded` | suit / encasement | arms/legs `folded` | Crawl-suit / tar wrap limb tuck |
| `free` | (no bind) | default on enter / after unbind | Limb free |

**Examples**

Wrists behind the back:

```json
{
  "$type": "lewd_bind",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "cuffs",
  "sites": ["wrists"],
  "orientation": "behind",
  "implies": ["cuffed"],
  "itemId": "leather_cuffs"
}
```

→ `State.arm_position = "behind"`, effects gain `arms_rear_bound` / `no_hand_use` / `no_somatic_spellcasting`.

Wrists in front (marching cuffs):

```json
{
  "$type": "lewd_bind",
  "sites": ["wrists"],
  "orientation": "front",
  "implies": ["cuffed"],
  "itemId": "leather_cuffs"
}
```

→ `State.arm_position = "front"` (still cuffed; somatic may be awkward — narrate disadvantage; engine does **not** auto-block S unless you also imply `mitted` / add effect).

Ankles spread:

```json
{
  "$type": "lewd_bind",
  "sites": ["ankles"],
  "orientation": "apart",
  "implies": ["cuffed", "hobbled"],
  "itemId": "spreader_bar"
}
```

→ `State.leg_position = "apart"`.

**Tracking discipline:** before narrating “hands behind their back” or “ankles locked apart”, read `arm_position` / `leg_position`. If State says `front`, do not invent behind. Change pose with a new `lewd_bind` (or unbind + rebind) — never by prose alone.

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
| `orientation` | **Required for cuffs/ties.** Arms/legs: `front` \| `behind` \| `above` \| `together` \| `apart` \| `crossed` \| `folded` \| `hogtie`. Engine mirrors into `arm_position` / `leg_position`. |
| `links` | Anchors / partners: `anchor:…`, `to:chars/…`, `to:bind_02` |
| `implies` | Condition tokens handlers/LLM must honor: `cuffed`, `hobbled`, `encased`, `gagged`, `mitted`, `blinded`, `deafened`, `suspended`, `leashed`, `full_tied`, `limb_bound` |
| `materials` | `hemp`, `leather`, `iron`, `silk`, `linen`, `tar`, … |
| `hardened` | Magical / masterwork — harder escape; `word_of_safety` may not clear |
| `effects` | Freeform constraint tags from gear (`forced_crawl`, `bent_knees_elbows`, `forced_spread`, …) plus escape/break/HP when set on the commit |
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


## ItemDefinition → bind seeding

When `lewd_bind.itemId` points at a live Item (or template name like `bitchsuit`), Properties seed the graph:

| Property | Becomes |
|----------|---------|
| `implies` / `seedsConditions` | `bindings[].implies` |
| `sites` | `bindings[].sites` |
| `effects` | `bindings[].effects` (e.g. bitchsuit → `forced_crawl`, `bent_knees_elbows`, `all_fours`) |
| `posture` | participant `State.posture` if the commit omits `posture` |
| `materials` / DCs / hp | binding materials + escape/break/hp |

**Narrate from State.** Example: bitchsuit bound → posture `all_fours_crawl` + effects include `forced_crawl` → character crawls on bent knees and elbows; do not narrate upright walking until unbound / effects cleared.

**Spellcasting:** gags/hoods that imply `gagged` (or effects `no_verbal_spellcasting`) stamp StatusEffect `gagged` with `BlocksVerbalComponents` — host casting gate hard-fails V spells. Armbinders / mitts / crawl-suits that block hands stamp `BlocksSomaticComponents`. Prefer Faerûn materials in gear prose (leather, iron, hemp, silk, pitch) — avoid modern latex/nylon/zippers.

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
