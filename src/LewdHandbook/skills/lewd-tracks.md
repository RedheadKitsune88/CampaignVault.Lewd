---
name: lewd-tracks
description: Durable tracks — lewd_vice, imprints, lustbrands, pregnancy, lewd_bad_end. Mostly global (no mode required).
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Tracks (durable)

Character Traits/Attributes/StatusEffects are source of truth. Most verbs work **outside** `lewd_encounter`.

NPC-card visibility: `bindings`, `posture`, `pregnant*`, `bad_ended*`, `lustbrands` and `lustbrand_glow` show on the card always (restraint, a visible belly, a mark, a character out of play). Everything else (`lewd_encounter.*`: stance, kinks, anatomy, imprint ledger, thoughts, fertility flags, per-brand and per-vice bookkeeping) shows only while the character is in an active scene. Read and write them by their prefixed names.

## Bad end — `lewd_bad_end` (mode-scoped)

Triggers: overstim 6; climax while edging with recovery≤0; arousal max≤0; pregnancy `noEscape`; explicit/defeat commit. HP≤0 does **not** auto-mark — emit only if no rescue (in-encounter observer nudges).

```json
{ "$type": "lewd_bad_end", "targetId": "chars/b", "reason": "defeat", "noEscape": true, "consequence": "slave" }
```

Consequences: `level_drain` | `slave` | `seedbed` | `curse` | `lustbrand` | `class_change` | `narrated` (stored; apply later) | `vice` (+`viceId`) | `imprint` (+`imprintTrack`/`imprintJump`). Do not drain overstim in the marking commit. Needs `noEscape` (adults only). Where `lewdNonConsent` allows unwanted acts against the target it stores the permanent bad end above. Where it does not (setting off, or `not_against_pc` and the target is the PC) it is a **fade-to-black rescue** instead: no mark, no `bad_end.v1`; the engine frees them (unless `keepBindings`), clears the overstim/edging/incapacitation that caused it, stamps *Knocked senseless* (all checks, saves and attacks −2 for 24h) and drains 15 willpower, once per day. You then choose the rest: skip time (`advance_world`), place them nearby, say who found them and what was taken (item and location changes), end the scene. `outcome` records your wording; a permanent `consequence` is refused (use none or `narrated`). Engine-owned triggers (overstim 6, empty recovery, arousal max ≤ 0, capture) follow the same split. Engine-owned bad ends (overstim 6, empty recovery, arousal max ≤ 0, capture) publish `bad_end.v1` too.

## Vices — `lewd_vice` (global)

Ids: `sex`, `sexual_fluids`, `alcohol`, `succubus_venom`. The clock is campaign time since the last partake. Stages: **sated** → **craving** (½ window) → **withdrawal** (24h; alcohol 4h) → **severe** (3 windows). The engine announces each stage once; party members get a context line.

```json
{ "$type": "lewd_vice", "characterId": "chars/b", "action": "note_presence", "viceId": "alcohol", "d20": 0 }
```

| action | When |
|--------|------|
| `consume` | They partake. First time: addiction save (`ability` for complex vices). DC +1 per partake. Clears withdrawal and compulsion |
| `note_presence` | The vice is at hand. In withdrawal this **is** the handbook's save (disadvantage): failure → `Compelled: <id>` (they try to partake by any means), success → +1 Resolve. Otherwise just the disadvantage reminder |
| `resist` | An explicit temptation save (withdrawal or `inPresence: true`) — same results as above |
| `rest` | Only when the host could not roll the long-rest save (no Rolls): pass `d20` |
| `treat` | `method`: `lesser_restoration` / `healers_kit` (advantage on addiction saves), `greater_restoration` (automatic success) — until the next long rest; `remove_curse` + `slotLevel` (magical vices: DC −1 per level above 2nd) |

Long rest in withdrawal (engine-rolled): a **normal** addiction save, plus Resolve (all spent, +1 each; max 3), advantage when aided. Success: DC −1 and the clean streak grows; at base DC, one more success ends the addiction. Failure: the vice's penalty (sex/fluids: overstimulation; alcohol: exhaustion; venom: denied), nat 1 doubled. Brand of Addiction locks `sexual_fluids` (no clean).

Durable: Traits `lewd_encounter.vice.<id>.*`, Attributes `vice.<id>.*` (dc, last_hours, resolve, clean_streak); StatusEffects `Vice: <id>`, `Withdrawal: <id>`, `Compelled: <id>`.

## Imprints — `lewd_imprint` / `lewd_decondition` (global)

Tracks: `wanton`, `training`, `breeding`, `ordeal`, `cruelty`. Levels 0–3. Hard limits (player's and the character's) block. Unwilling needs `lewdNonConsent` to allow it for the target, or `willing`.

**Ordeal ≈ masochism** (how much pain/humiliation converts to want). Distinct from **inhibition** (how hard it is to start an advance / climax save). Do not invent a sixth `masochism` track.

### Humiliation — `lewd_humiliate` (global)

Public theater, naming, piercing display, forced begging. Always drains willpower and stamps timed `Humiliated` / `Deeply humiliated` (no EffectTier; daily cap 3; `lewdHumiliation` default on). Hard limits `humiliation` / `shame`.

```json
{ "$type": "lewd_humiliate", "characterId": "chars/b", "severity": 2, "tags": ["public", "piercing"], "sourceId": "chars/a" }
```

| Severity | Willpower | Status |
|----------|-----------|--------|
| 1 | −2 | Humiliated ~4h: Cha −1 |
| 2 | −4 | Humiliated ~8h: Cha −1, Wis −1 |
| 3 | −6 | Deeply humiliated ~24h: Cha −2, Wis −1 |

**Arousal from shame only if ordeal ≥1** (severity 1 never arouses; 2–3 roll 1d4 ± ordeal). Inhibition is never an input. Publishes `humiliated.v1`.

**Slow ordeal climb:** after a pain-tagged advance or `lewd_humiliate`, if a climax lands soon, you *may* commit `lewd_imprint` category=`ordeal` with a small points tick. Engine Message-nudges once/day; never auto-levels.

Backstory (an NPC who arrives conditioned): `setLevel: 1–3` sets the track directly, no save; `willing` picks the origin.

`anchorId` ties a track to a person (the captor, a trusted partner): an unwilling imprint then lowers Inhibition only against them, and a level-3 training imprint obeys them. Imprints from an advance or a bind are tied to whoever pressed them.

```json
{ "$type": "lewd_imprint", "targetId": "chars/b", "category": "training", "source": "training", "willing": false, "ability": "wis", "d20": 8 }
```

```json
{ "$type": "lewd_decondition", "targetId": "chars/b", "category": "ordeal", "method": "therapy", "d20": 14 }
```

Inside a scene, matching physical advances / bitchsuit binds only record pressure; each track ticks **at most once per scene**, when the encounter exits (unwilling ticks roll the resist save then). Outside a scene they tick at once. Rest drops unexposed tracks (not L3 unwilling / lustbrand-locked). Bad-end imprint jump applies on the next write **or** rest.

## Brands — `lewd_apply_brand` (global)

```json
{ "$type": "lewd_apply_brand", "targetId": "chars/b", "brandId": "denial", "sourceId": "chars/x", "payload": "…" }
```

Remove: `action: remove`, `method: wish|feature` (not remove curse); never blocked by limits or stance. Unwilling apply needs `lewdNonConsent` to allow it for the target, or `willing: true`. Altruism: when the bearer heals someone else, `action: heal`, `amount: N`. Transformation `trigger` rolls when `d20` is 0. Inhibition −sum of tiers (omit climax `inhibitionBonus` to auto-subtract).

| id | tier | Engine hook |
|----|------|-------------|
| `abundance` | 1 | Rest sans climax: endowment +1; climax −1 |
| `addiction` | 1 | Lock `sexual_fluids` vice DC 18 |
| `altruism` | 1 | Heal lowers arousal max until rest; `stabilize` raises tier |
| `bestial` | 4 | Rest → `nymphomanic` until unprotected climax |
| `betrayal` | 5 | `payload` = foe type |
| `denial` | 5 | Blocks climax; `release` until next rest/heal/advance/climax |
| `echoes` | 2 | Echo stim; a climax within 5 ft (Touch position, physical engagement, chained together) forces theirs; unknown distance → you decide |
| `emptiness` | 1 | Status |
| `false_dominance` | 1 | Status |
| `fertility` | 2 | `hyperfertile` (Trait `lewd_encounter.hyperfertile`, also `infertile`/`hypervirile`); climax only from unprotected sex (tag the advance `unprotected`); +1 tier per birth |
| `infatuation` | 3 | `infatuated` by source |
| `oaths` | 2 | `vow` stores payload |
| `ruin` | 1 | No climax: rolls a recovery die, arousal and HP −(roll + tier); no dice: 1d12 psychic + overstim; would-be 0 HP → 1 HP, tier +1 |
| `obedience` | 5 | Infatuated; range/overstim dominate |
| `forsaken` | 3 | Status |
| `hungry_gaze` | 3 | Status |
| `transformation` | 5 | `trigger` Con DC 18 |

Do not apply brand inside `lewd_bad_end` commit.

## Pregnancy — `lewd_pregnancy` (global)

Progress follows campaign time over the term (default 270 days traditional, 3 days nontraditional; `termDays` on impregnate). It **shows** at 25 (nontraditional at once): only then the Pregnant condition and the rest save (DC 15 Con after any short or long rest, engine-rolled; failure: poisoned for 1d4 hours, lifted automatically). At 100 the engine flags the term: emit `birth`.

```json
{ "$type": "lewd_pregnancy", "targetId": "chars/b", "action": "impregnate", "d20": 14, "contraceptive": "condom" }
```

| action | Role |
|--------|------|
| `impregnate` | Traditional/nontraditional |
| `advance` | Magic that speeds it (`progressDelta`); the clock moves with it |
| `birth` | Ends it (premature if under 100); Brand of Fertility +1 tier |
| `terminate` / `termination_save` | Spell/other end; crit save with `dc` = damage |
| `rest` | Only when Rolls is unavailable: the rest save with `d20` |

Visible Traits (shown on the NPC card outside a scene): `pregnant`, `pregnancy_progress`, `pregnancy_type`, `pregnancy_source`, `pregnancy_offspring`, `pregnancy_due`. Contraceptives: `condom`, `oil_of_impotence`, `potion_of_infertility`, `beads_of_prevention` (charges are not consumed).

## Do not

Invent addicted/pregnant/branded State. Clear `locked` vice with prose alone. Tick pregnancy from `lewd_advance`.
