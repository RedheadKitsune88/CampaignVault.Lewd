# Implementation Plan: Pregnancy & Fertility, Lustbrands, Imprints/Conditioning, Bad-Ending, Kink Conversion, Overstimulation, Filth

**Status:** Phase 1–7 implemented (2026-09-23). Vices = §9 (`lewd_vice` + observer). Phase 6 polish in skills/README/RULES_NOTES.  
**Date:** 2026-09-23 (based on session)  
**Project:** CampaignVault.Lewd (Serena active)  
**Related:** lewd-handbook-plugin-plan.md, RULES_NOTES.md, lewd-handbook.md (operator-local source)

This plan incorporates explicit user decisions from the review session:

1. Hooks/listeners: As dedicated as reasonable. Use what CampaignVault.PluginSdk actually exposes (IWorldChangeHandler for custom `lewd_*`, IModeStateMachine, IChangeContext, prepared `Observers/` for `IWorldChangeObserver` with `IsInterestedIn`, participant.State + Character.SystemStats, GetSystemOptionsAsync, Rolls, Record*).
2. Imprints/conditioning: **Full** — gainable fetish tracks (wanton / training / breeding / ordeal / cruelty), willing vs unwilling payloads, decondition (therapy + time), INT/WIS resist saves, intrusive thoughts. Petplay/bitchsuit ticks **training**, not a separate category.
3. Filth: **LLM discretion via core CampaignVault verbs**, not a plugin counter. `character_update.appearanceOverride` + temporary tags; `item_update` tags/`newState` (tag the container); `upsertItemDetail` for durable stains. Verbal advances never apply filth.
4. Pregnancy: **Progress/term counter** (in addition to flag/cond).
5. Bad-ending: engine marks the fact; `lewd_bad_end` records it and the chosen consequence. Level drain stays a table/LLM follow-up, not an MCP mutation. The one mechanical alt this plan owns is an imprint jump (phase 5 applies a stored chunk). Vices/addiction are handbook rules and are **not** in this plan.
6. Overstimulation auto: **Climax count** driven (multiple climaxes while incapacitated).
7. Lustbrands: **Per-brand StatusEffects** (more accurate to handbook).
8. Rests: Not currently exposed in plugin code. Check required; workarounds or propose small core addition if needed.
9. Prioritization: Finish **all**. Prioritize quick wins + unrelated/isolated first (overstim, filth, kink extensions → pregnancy → bad-end → brands → imprints).
10. Compatibility: **Homebrew** (not strict 5e). Flexible with custom status, resources, state, etc.

## 1. Current State & SDK Hook Surface (from inspection)

**Implemented today (core only):**
- Participant `State` dict (consent, limits, kinks, inhibition, climax counters, edging, overstimulation=0, arousal mirrors, bindings, posture/positions).
- `LewdKeys`, `ConsentGate` (auth + tone + EffectiveInhibition + tag-based stim adjust).
- Math: `StimulationMath`, `ClimaxMath` (handbook-accurate).
- Handlers: `lewd_advance`, `lewd_climax_check`, `lewd_bind`/`unbind`.
- `LewdPoolHelper` (host ResourcePools + StatusEffects stamping + mirrors).
- `LewdEncounterMode` + state machine (enter seeds, AdvanceTurn for edging).
- Yaml: pools, many conditions (incl. overstimulation stacking), histories, curated items/spells (partial pregnancy_ward, power_word_pregnant).
- Skills: state/verb guidance, "engine is source of truth".
- No observers implemented yet (Observers/ only .gitkeep).
- No "rest"/long-rest/short-rest mentions anywhere in .cs.
- No pregnancy, lustbrand, imprint, filth, or bad-end logic beyond placeholders/yaml.

**SDK hooks available (verified via code + plan.md):**
- Dedicated: Custom WorldChange + `IWorldChangeHandler` (`ShouldHandle` + `ApplyAsync(IChangeContext)`).
- Lifecycle: `IModeStateMachine` (CreateEncounter, AdvanceTurn, action budget, IsComplete).
- Context: `IChangeContext` (ActiveMode, Characters.SystemStats.{ResourcePools, StatusEffects, Traits}, GetSystemOptionsAsync, Rolls, RecordMessage/RecordPhysicalStateNudge).
- Passive listeners: `IWorldChangeObserver` (prepared in plan.md: "Prefer observers...", `IsInterestedIn` filter for cheap reaction to any change).
- Mutation: participant.State + Character.SystemStats; LLM can emit core commits (`resource`, `status`, `take_turn` batches, potential character mutations).
- campaignOptions in plugin.json (SystemOptions).
- No rest exposure observed.

**Gaps vs lewd-handbook.md (metadata + sections):**
- Pregnancy/Fertility (full rules + breeding imprint refs).
- Lustbrands (per-brand with tiers, inhib reduction, glow, specific triggers).
- Imprints/Conditioning (vaginal vs breeding split, pet play, INT/WIS saves, behavior skew, intrusive thoughts, bitchsuit).
- Bad-Ending (triggers + outcomes including level drain option).
- Overstimulation (auto from climax count + extended edging).
- Filth (numeric delta + mandatory sensory).
- Kink conversion (full limits/resists + verbal gating).
- Cross-cutting: rest effects, per-brand StatusEffects, progress counters, observers for reactive side-effects.

## 2. Hook Strategy (user decision #1)

- **Dedicated verbs** where it makes sense (e.g. optional `lewd_impregnate`, `lewd_apply_brand`).
- **Observers** (new `IWorldChangeObserver` impls) for passive/cross-cutting:
  - Filter to active `lewd_encounter` + relevant changes (resource deltas, status, custom lewd changes).
  - Use for: climax counting → overstim, brand triggers (climax/heal), imprint ticks, pregnant rest checks, bad-end evaluation. Not filth.
- Keep heavy logic in handlers (like current `ApplyClimaxResult`).
- Workarounds for rests (if not exposed): 
  - Skills instruct explicit resource/status commits during rest narration.
  - Or lightweight `lewd_rest` helper.
  - Pattern-match on soothing/recovery patterns.
- If full rest support is desired, document as "propose small addition in main CampaignVault SDK/host".

## 3. Per-Feature Design (user decisions applied)

### Overstimulation (quick win, isolated — prioritize)
- Drive from **climax count** while incapacitated (handbook: after 3rd +1 level per additional; also extend incapac rounds).
- Edging duration for "hours == Inhibition" rule (state-tracked turns/beats or explicit marker; homebrew flexible).
- On increment: update state + stamp/update stacking `StatusEffect`.
- Cascade lower effects (1 Intoxicated, 2 Hyperaroused, 3 inhib=0, 4 Infatuated, 5 no edging clear on climax, 6 Bad-Ended).
- Long-rest reduction (via rest workaround + observer/handler).
- Update: Climax handler/ApplyClimaxResult, mode AdvanceTurn, skills, tests.
- Yaml already good (stacking, mechanicalSummary).

### Filth (LLM discretion — not a plugin counter)
- Do not tick filth from `lewd_advance`, climax, or bind. Verbal / non-contact never dirties.
- Record when the fiction changes (sex, dust, mud, cleanup) with core `character_update` (`appearanceOverride`, temporary tags) and `item_update` (tags / `newState` on the container; `upsertItemDetail` for durable stains, retire when cleaned).

### Kink / Limit / Conversion (extend existing — quick)
- Build on current `kinks`/`soft_limits`/`hard_limits` + `AdjustStimulationForTags`.
- Add fuller stimulation-type kinks (inverse resist per handbook), verbal caps by sexual_history trait, more tag probing.
- Homebrew-flexible (not strict 5e damage conversion).

### Pregnancy & Fertility (progress/term counter)
- State: `pregnant`, `pregnancy_progress` (or term counter, e.g. 0-100 or days), `pregnancy_type`, source.
- Impregnation logic: extend `lewd_advance` (repro tag + anatomy) **or** small `lewd_impregnate` + handler.
  - Traditional: Impregnation Con check (DC 10 + Con mod + prof; adv if hyper).
  - Non-trad: Con save (+Inhib if unwanted).
  - Modifiers: hyperfertile, infertile, contraceptives (new items: condom DC25, oil, potion, beads).
- On success: set pregnant + progress; apply `pregnant` StatusEffect (disadv Str/Dex, crit 18-20, rest poison, termination risk).
- Effects: rest check (DC15 Con or poisoned) via observer/workaround.
- Term: advance progress on time/rests (or LLM with validation); non-trad fast.
- Add yaml: pregnant.yaml, contraceptive items.
- Tie to bad-end (capture + impreg no escape).

### Bad-Ending
- Triggers (in climax/overstim paths): edging+climax+0 rec dice; max arousal <=0; 6 overstim; capture+impreg no escape; phys defeat.
- On trigger: set `bad_ended` + status. Record options.
- Record: `lewd_bad_end`. Automatic triggers still set the flag so the LLM cannot skip the fact. The verb attaches the consequence.
- Level drain: not applied here. No drain verb exists. Skills may narrate it or patch `systemStats.level` / `classLevels` in a later commit.
- Mechanical alt this plan owns: `consequence=imprint` plus a track and a jump (stored now, applied in phase 5). Slave, curse, class change, lustbrand stay narration or their own later phase. Addiction/Vices are not encoded.
- Permanent via state/traits (homebrew flexible).

### Lustbrands (per-brand StatusEffects)
- State: `lustbrands: [{id, tier, ...}]`.
- Apply: `lewd_apply_brand` (or via spell) + handler. Create per-brand `StatusEffect` (e.g. "lustbrand:denial", mechanicalSummary with tier).
- Common: inhib -= tier (extend ConsentGate); glow flag for snapshots.
- Triggers (exact handbook): on rest (abundance, bestial...), on climax (echoes, ruin, re-brand), on heal (altruism), on vow, etc. → observer + helpers.
- Removal: special only (not remove curse); concubi auto new on climax (often higher tier).
- Multiple supported.
- Update catalog with per-brand summaries.

### Imprints / Conditioning (gainable fetishes — revised)

**Intent:** lasting, gainable fetishes / detriments from (a) slutting around, (b) sexual training, (c) bad-ends, (d) cruelty — enjoying others’ pain / “evil” lean — not anatomy-site counters. Petplay/bitchsuit is a **tag on the training track**, not its own category. Handbook body has no imprint section; this is a documented homebrew overlay (`RULES_NOTES`). Do **not** merge the 8-category expansion in `~/Downloads/3`.

**Tracks (small closed set):**

| Id | What it captures | Typical sources |
|----|------------------|-----------------|
| `wanton` | Promiscuity, public/multi, “slutting around” | Repeated casual/public climaxes, Wanton Whore-adjacent tags |
| `training` | Being (or becoming) sexually trained — obedience, petplay, bitchsuit, “good girl/boy” | Dedicated training scenes, owner commands, bitchsuit bind, training fetish |
| `breeding` | Seedbed / pregnancy-as-identity | Repro-tagged climaxes, capture+impreg, Brand of Fertility overlap |
| `ordeal` | Trauma/defeat lean (masochism, helplessness) | Bad-end, forced climax, denial, incapacitated overstim |
| `cruelty` | Sadism / enjoying others’ pain or ruin | Inflicting pain-stim, watching ruin, evil/grimdark acts for pleasure |

Optional later (do not ship v1): `devotion` (positive partner-bond counterpart to `training`).

Each track is `{ points: int, level: 0–3, origin: willing | unwilling, last_tick_day?: int }`.
Levels: 0 latent; 1 mild (kink tag injected); 2 strong fetish + skew; 3 complete (feat-stub fetish + permanent drawback; bad-end alt may jump here).

**Willing vs unwilling (same track, different payload):**

| | Willing | Unwilling |
|--|---------|-----------|
| Tick | Explicit `lewd_imprint` or tagged climax **without** resist save | Auto-tick on tagged climax/training/bad-end; **WIS or INT save** (actor picks; DC 12 + level + mods) to **negate** the tick |
| Buffs | Aligned stim ×1.5 (existing kink path); optional 1/rest advantage on aligned seduction/training checks at level ≥2 | None until the player **accepts** (consent-gated convert `origin` → willing) |
| Debuffs | Mild: intrusive thought at 3 only | Intrusive thoughts at 1+; disadv on **non-aligned** actions when triggered; Inhibition −level vs the source/category; training/ordeal may force obey-save vs owner |
| Bad-end | `lewd_bad_end` `consequence=imprint` may set origin=willing and store a jump to 3 **instead of** level drain. Phase 5 applies it. | Same verb, origin=unwilling, jump ordeal and/or the named track. Not a Vices/addiction track. |

Player consent still gates grimdark ticks (`intimacyTone` + hard_limits). Hard-limit tags **never** imprint.

**Decondition (therapy / time / aftercare):**

- `lewd_decondition` (dedicated verb): therapy, aftercare, or a willing “corrective” scene. Target + category. WIS save DC 10 + level (adv if aftercare / devoted partner present). Success: −1 to −3 points; drop a level if below threshold. Unwilling-origin: harder (DC +2) unless `method=therapy`.
- Time: rest observer on **long rest** with **no aligned exposure** since last tick → WIS DC 14, success −1 point. Level 3 unwilling needs therapy or a month of avoidance (skills: require several successful rests; engine: skip auto-drop at 3 unless therapy).
- Exposure while deconditioning (aligned climax/training) **cancels** that rest’s drop.
- Cannot decondition a track that is also a live Lustbrand’s core (skills + handler refuse or warn).

**Hooks / verbs (keep small):**

| Verb | Owns | When |
|------|------|------|
| `lewd_imprint` | Explicit training / GM / LLM “they’re being trained / they’re leaning into it” | `{ targetId, category, source: training\|wanton\|bad_end\|cruelty\|exposure, willing: bool, delta?: int }` Fail-closed if hard limit. Rolls resist save when `willing=false`. |
| `lewd_decondition` | Therapy / aftercare / time montage | `{ targetId, category, method: therapy\|aftercare\|rest }` |
| (no extra) | Auto | `lewd_advance` / `ApplyClimaxResult` / bitchsuit bind / bad-end helper **suggest** a tick (RecordMessage) and apply only when tags match a track map; unwilling → save. Prefer this for slutting-around and cruelty so the LLM does not have to remember the verb every beat. |

Do **not** add per-category verbs. Observers: rest decondition only; they do not own imprint dice.

**Thresholds → engine effects:**

- Points: 3 → L1, 7 → L2, 12 → L3 (same shape as the unofficial expansion, different categories).
- L1: inject kink tag (`wanton`, `training`, `breeding`, `ordeal`, `cruelty`) into `kinks[]` if willing; if unwilling inject into `soft_limits` **and** still bias stim (conflict = net ×0.75 or leave as kink-with-shame — pick **kink + intrusive**, not soft-limit, so the body betrays them).
- L2: stim bias as now; disadv non-aligned when the trigger is in-scene; `intrusive_thoughts[]` entry.
- L3: stamp StatusEffect `imprint:<category>` (Manual); may add existing fetish feat-stub id (`wanton_whore`, `edge_puppet`, …) as Trait; training L3 = obey-save vs owner; cruelty L3 = aligned pain-stim crit floor or extra stim when target is suffering (keep tiny).
- Breeding track ties pregnancy tags; training track ticks on bitchsuit `lewd_bind`.

**Skills:** `lewd-imprints-conditioning.md` — engine is source of truth; when to emit `lewd_imprint` vs rely on auto; snapshots; decondition; bad-end alt = jump track instead of drain.

## 4. Prioritized Phasing (quick wins first)

**Phase 1: Quick wins / isolated (do these first)**
- Overstimulation (climax count + duration).
- Filth (numeric + ticks).
- Kink extensions.
- Supporting: LewdKeys, small helpers, tests, skills updates (catalog + encounter).
- No new observers yet.

**Phase 2: Pregnancy**
- State + progress/term counter.
- Impregnation logic + contraceptives.
- Effects (incl. rest workaround).
- Yaml + light tie to bad-end.

**Phase 3: Bad-ending + hooks foundation** — groomed, not started. Exact edits: §8.
- Unify the partial `bad_ended` writes already in tree.
- Engine marks only the automatic handbook triggers. No level drain. No new rest work.
- One cheap `IWorldChangeObserver` (reconcile + arousal-max + defeat prompt). Explicit defeat is `lewd_bad_end`.

**Phase 4: Lustbrands**
- Per-brand StatusEffects + state.
- Inhib mod.
- Observer for triggers (climax, heal, rest patterns).
- Catalog.
- Brand of Addiction stamps the brand only. Craving, saves, and withdrawal are Phase 7. Do not invent a vice engine here.

**Phase 5: Imprints/Conditioning (full)**
- Five tracks (`wanton`, `training`, `breeding`, `ordeal`, `cruelty`); willing/unwilling origin; decondition.
- Verbs: `lewd_imprint`, `lewd_decondition`; auto-tick from climax/advance/bind/bad-end.
- Rest observer: time-based −1 if no exposure.
- Skills + tests. Do not encode 8-category fetish expansion. On first imprint write, if `bad_end_imprint_track` is set, jump that track to the stored level (cap 3), set origin from `bad_end_imprint_origin`, then clear the pending jump. That is the bad-end chunk. Do not add a Vices/addiction track.

**Phase 7: Vices** — see §9. Implemented: `LewdViceChange`/`Handler`/`Observer`, `ViceCatalog`/`ViceState`, yaml under `vices/` + `conditions/vice_*`, skill `lewd-vices.md`, tests in `ViceTests`.

**Phase 6: Polish + integration** — done with phase 7 land: rest observer docs, snapshot fields in `lewd-encounter`, README verb/skill tables, RULES_NOTES vices/rest/feather-goblins notes, brand/bad-end skill cross-links.

## 5. Architecture & Files to Touch (high level)

**New/updated keys (LewdKeys.cs):**
- `pregnant`, `pregnancy_progress`, `pregnancy_type`, ...
- filth is not a state key (core appearance/tags/item details)
- `lustbrands` (array)
- `imprints` (dict of tracks: points/level/origin), `intrusive_thoughts`
- `bad_ended`
- Per-brand condition names

**Helpers:**
- Extend LewdPoolHelper / new *Math or *Helper for impregnation checks, imprint ticks+saves, overstim from climax count, brand effects. No filth math.
- ConsentGate extensions (imprint bias, brand inhib).

**Handlers/Changes:**
- Minor extensions to LewdAdvanceHandler + ApplyClimaxResult.
- Optional new small Changes + handlers for dedicated actions.
- New observer class(es) in Observers/.

**Data (RulesetData/dnd5e/...):**
- conditions/pregnant.yaml (and hyper/infertile if separate)
- items/ for contraceptives
- Update overstimulation.yaml if needed
- Catalog entries (per-brand, pregnant, etc.)

**Skills:**
- Update lewd-encounter.md, lewd-catalog.md
- New/expanded: lewd-pregnancy.md, lewd-brands.md, lewd-imprints-conditioning.md
- Explicit mutation options for bad-end
- Rest narration guidance
- Snapshot examples with all new fields

**Other:**
- LewdEncounterMode.cs (seed new state defaults)
- Tests (new math/handler coverage)
- RULES_NOTES.md (append sections with handbook citations)
- Possibly plugin.json if new campaign options

**No changes to main CampaignVault.**

## 6. Risks, Mitigations, Success Criteria

**Risks:**
- Rest gap (mitigate with skills workarounds + observer patterns; escalate to core if blocking).
- Over-coupling (mitigate by isolating quick wins first).
- LLM ignoring state (mitigate with strong "engine is source of truth" in skills + fail-closed handlers).
- Numeric vs prose balance (homebrew-flexible; keep simple counters).

**Success:**
- All features implemented per handbook + user decisions.
- Quick wins (overstim, filth, kink) done early and isolated.
- Observers used appropriately for listeners.
- Per-brand StatusEffects, progress counters, full imprints. Filth stays on core character/item changes.
- Bad-end supports LLM mutation **or** imprint/fetish alt.
- Skills fully guide LLM.
- Tests pass; pack/install clean; no Sdk.dll leakage.
- Compatible with existing (dnd5e + feather-goblins style state).

## 7. Next Steps (after approval)
1. Switch to build mode (done).
2. Execute Phase 1 (overstim + filth + kink) using Serena tools + edits.
3. Update this plan.md with status as we go (or keep separate tracking).
4. Run tests/lint after each phase.
5. Verify against operator-local lewd-handbook.md + PDF where extracts differ.

## 8. Phase 3 groom (exact changes)

**Do not implement this section until asked.** Scope is bad-ending state plus the first observer. Do not start lustbrands, imprints, filth, or rest decondition.

### Already in tree (do not re-add)

- `LewdKeys.BadEnded` (`bad_ended`). Seeded `false` in `LewdEncounterMode.CreateEncounter`.
- `LewdPoolHelper.SetOverstimulation`: participant flag when level ≥ 6. No trait, no `bad_ended` StatusEffect.
- `LewdAdvanceHandler.ApplyClimaxResult`: if overstim did not increase and level is already 6, sets the participant flag only. The "Bad-Ended." suffix is a message, not durable state.
- `LewdEncounterStateMachine.AdvanceTurn`: extended edging that reaches 6 sets the participant flag only. No `Character` in that method — cannot stamp traits there.
- `LewdPregnancyHandler.NoteCapture`: `noEscape` writes `Traits["bad_ended"]="true"` and the participant flag. No StatusEffect. Existing test `PregnancyTests` asserts both. Keep that assertion green by routing through the helper below.
- Skills mention level 6 / `noEscape` and say not to drain levels. They do not list the other triggers or the follow-up commits.

Missing handbook triggers: edging climax with no recovery dice left; arousal maximum ≤ 0. Defeat/unconscious is "may be", not automatic.

### SDK facts (host `CampaignVault.PluginSdk`, do not change the host)

- `IWorldChangeObserver` (`IsInterestedIn`, `OnCommittedAsync`) is convention-scanned from the plugin assembly (`ConventionRegistration.RegisterCollection`). No `plugin.json` entry. Public non-abstract class, parameterless ctor.
- Runs only after that change's handler returns success. Exceptions are logged and swallowed. `RecordFailure()` must not be called. Not invoked for `DispatchMutationAsync` children.
- Plugins cannot enqueue. `DispatchMutationAsync` downcasts to host `ChangeContext`. Mutate the `Character` / `ModeParticipantState` instances already on `IChangeContext` (same objects handlers mutate) and `RecordMessage`. Do not downcast.
- `RestChange` (`$type` `rest`) exists. Phase 3 does not subscribe to it. Rest poison stays on `lewd_pregnancy` `action: rest`. Long-rest overstim decrement stays host-side.
- No WorldChange lowers a class level. `level_up.levelsGained` only increases. `xp_grant.amount` may be negative and does not drop a level. `character_update` has no `classLevel` field; `systemStats` may carry `level` / `classLevels` (`Dnd5eExtension`). The plugin must not patch those.

### Engine rules (`BadEndMath`, pure)

New `src/LewdHandbook/Mechanics/BadEndMath.cs`. Reasons: `overstim`, `empty_recovery`, `arousal_max`, `capture_impreg`, `defeat`, `explicit`.

| Input | Result |
|----|--------|
| Already `bad_ended` | No second mark. Consequence string may still be stored. |
| Overstim ≥ 6 | Mark `overstim`. |
| Climaxed, was edging before the climax, `recovery_dice` pool exists, `Current` ≤ 0 | Mark `empty_recovery`. |
| `recovery_dice` pool absent | Do not mark. Message only: pool missing, do not assume zero. |
| `recovery_dice.Current` > 0 | Do not mark, even if dice were not spent. |
| Arousal pool exists and `Max` ≤ 0 | Mark `arousal_max`. Missing pool does not mark (enter mirror default is 10; `EnsurePool` default max is 10). |
| `CurrentHp` ≤ 0 | Do not mark. Prompt only. |
| Pregnancy `noEscape` after a successful impregnation | Mark `capture_impreg` (already gated by grimdark + hard limits in the pregnancy handler). |
| Explicit verb, `noEscape: true` | Mark `defeat` or `explicit`. |

Bad-end is permanent. Dropping overstim must not clear `bad_ended`. Do not set `scene_end`. Do not exit the mode.

### Durable write (`BadEndState.Apply`)

New `src/LewdHandbook/Mechanics/BadEndState.cs`. One function used by overstim, climax, pregnancy, the verb, and the observer.

Writes, idempotent (no duplicate StatusEffect):

- Participant: `bad_ended=true`, `bad_end_reason`, optional `bad_end_consequence`.
- `Traits["bad_ended"]="true"`, `Traits["bad_end_reason"]`, `Traits["bad_end_consequence"]` when provided.
- StatusEffect: `Name="Bad-Ended"`, `ConditionName="bad_ended"`, `Category="Condition"`, `AppliedBy` = source id or `"lewd_bad_end"`. `RecoveryHint`: permanent; follow-up commit chooses the consequence; this effect is not a level drain.
- `RecordMessage`: reason, and the consequence menu. State that this commit did not drain a level.

`LewdKeys` add: `BadEndReason="bad_end_reason"`, `BadEndConsequence="bad_end_consequence"`, `ConditionBadEnded="bad_ended"`. Do not re-add `BadEnded`.

`PregnancyState.Mirror`: also copy reason + consequence onto the participant (today it only copies the bool).

### Call-site edits

- `LewdPoolHelper.SetOverstimulation`: when `level >= 6`, call `BadEndState.Apply(..., "overstim")` instead of the raw participant assign. Still do not clear the flag when level drops.
- `LewdAdvanceHandler.ApplyClimaxResult`: read `edging` and `recovery_dice` before `SetEdging` clears edging. After the overstim tick, ask `BadEndMath`. Delete the `else if (level >= MaxLevel) State[BadEnded]=true` branch. Keep the message suffix, but include the reason when marked.
- `LewdEncounterStateMachine.AdvanceTurn`: leave the participant flag (no character to stamp). Do not add a character lookup.
- `LewdPregnancyHandler.NoteCapture`: replace the raw trait/state writes with `BadEndState.Apply(..., "capture_impreg")`. Same message intent: do not drain levels here.

### Explicit verb (optional triggers only)

New `Changes/LewdBadEndChange.cs` + `Handlers/LewdBadEndHandler.cs`. Discriminator `lewd_bad_end`.

```json
{ "$type": "lewd_bad_end", "targetId": "chars/bob", "reason": "defeat", "noEscape": true, "consequence": "slave" }
```

- `reason`: `defeat` | `explicit` only. Other reasons are engine-owned; reject them.
- `noEscape` must be true, else fail the commit.
- Active `lewd_encounter`, target is a participant, consent not `revoked`.
- `intimacyTone` `consensual` → fail. `fade` → mark + `RecordPhysicalStateNudge`. `grimdark` → mark.
- This verb is the record. Automatic triggers still set `bad_ended` first (overstim, empty recovery, arousal max, capture). `defeat` / `explicit` exist only on this verb. Calling it when already marked attaches or replaces the consequence; it does not clear the flag.
- `consequence` stored, not applied, except the imprint request below. Allowed: `level_drain`, `slave`, `seedbed`, `curse`, `lustbrand`, `class_change`, `imprint`, `narrated`. Unknown → fail. Omitted → record the flag only.
- `imprint` requires `imprintTrack`: `wanton` | `training` | `breeding` | `ordeal` | `cruelty`, and `imprintJump` 1–3 (bad-end default 3). Store `bad_end_imprint_track`, `bad_end_imprint_jump`, `bad_end_imprint_origin` (`willing` only if the payload says so; else `unwilling`). Do not create imprint tracks in phase 3. Phase 5 reads this and jumps that track once, then clears the pending jump.
- `vice` is allowed and stored only: `viceId` required (`sex`, `sexual_fluids`, `alcohol`, `succubus_venom`, or a yaml id). Store `bad_end_vice_id`. Phase 7 applies it (addicted at that vice's base DC, `locked` if the id is brand-locked). Do not roll an addiction save in phase 3.
- Does not patch level, HP, class, or brands. Does not set `scene_end`.

### Observer

New `src/LewdHandbook/Observers/LewdBadEndObserver.cs` : `IWorldChangeObserver`. Delete nothing else in `Observers/` except leave `.gitkeep` if the folder would otherwise be empty — a `.cs` file is enough; `.gitkeep` may stay.

`IsInterestedIn`: false unless `ActiveMode` is active `lewd_encounter`. Then true only for `HpChange`, `CharacterUpdate`, `StatusRemove`, `LewdAdvanceChange`, `LewdClimaxCheckChange`, `LewdPregnancyChange`. Not `RestChange`. Not `ResourceChange` (delta does not change max). No dice, no allocation beyond the type test.

`OnCommittedAsync`, only the character ids on that change (lewd changes: `TargetId`; `HpChange`/`CharacterUpdate`/`StatusRemove`: `characterId` / `CharacterId`):

- Participant or trait already `bad_ended` and StatusEffect missing → stamp (covers `AdvanceTurn`). If `StatusRemove` removed `Bad-Ended` / `bad_ended` while the trait is set, re-stamp. Do not clear a trait the LLM removed unless the participant flag is still set — participant flag wins while the mode is active.
- Arousal pool exists and `Max` ≤ 0 → `BadEndState.Apply(..., "arousal_max")`.
- `HpChange` and `CurrentHp` ≤ 0 and not already bad-ended → `RecordMessage` only: defeat in a lewd encounter is not automatic; emit `lewd_bad_end` with `reason=defeat` and `noEscape=true` if there is no rescue. Do not set the flag.

### YAML + skills + notes

- New `RulesetData/dnd5e/conditions/bad_ended.yaml`: `durationType: Manual` (same shape as `infatuated.yaml`). Summary: permanent sexual defeat; engine does not drain levels; see `lewd-bad-ending`.
- New `skills/lewd-bad-ending.md`. Engine is source of truth for the flag. Triggers table matching `BadEndMath`. Consequence is a **later** `take_turn`, never inside the marking commit:
  - `level_drain` (handbook default, optional): narrate the lost features. There is no drain verb. Do not emit `level_up`. Negative `xp_grant` does not drop a level. A later `character_update` may patch `systemStats.level` / `classLevels`; the plugin will not check it.
  - `imprint`: pass `imprintTrack` and `imprintJump` (usually 3). Phase 3 only stores the request. Do not emit `lewd_imprint` until phase 5.
  - `slave`, `seedbed`, `curse`, `class_change`, `narrated`: `event` plus narration. Curse may also be `status`. Do not invent a class-swap verb. Do not invent a vice.
  - `lustbrand`: do not emit a brand verb (phase 4). Store the consequence and narrate.
  - `vice`: pass `viceId`. Phase 3 stores it. Do not emit `lewd_vice` until phase 7.
  - Backgrounds Defiled Hero / Mark of the Beast are not shipped. Narrate only.
- Point from `lewd-encounter.md` (replace the level-6 one-liner), `lewd-catalog.md` (row next to `overstimulation`), `lewd-pregnancy.md` (keep "do not drain on noEscape"; point at the skill).
- `RULES_NOTES.md`: short Bad-Ending section citing handbook lines (edging+no recovery dice, arousal max ≤ 0, overstim 6, defeat is optional, capture+impreg). State the encoding choices above.

### Tests

New `tests/LewdHandbook.Tests/BadEndTests.cs`:

- `BadEndMath`: each row of the table, including absent recovery pool and HP ≤ 0 prompt-only.
- Climax while edging, pool `recovery_dice.Current == 0` → trait + StatusEffect + participant flag. `Current > 0` → no flag.
- Overstim tick to 6 → trait + status (not only participant state).
- Second apply does not duplicate the StatusEffect.
- Pregnancy `noEscape` test still passes and now also sees the StatusEffect — update that assert.
- Observer: HP ≤ 0 does not set the flag and records a message. Arousal max 0 on `CharacterUpdate` does set it. `StatusRemove` of `Bad-Ended` re-stamps while the mode flag is set. Inactive mode → `IsInterestedIn` false.
- `lewd_bad_end`: consensual fails; `noEscape: false` fails; grimdark + `consequence=slave` stores the string and does not change `ClassLevel` / `systemStats.level`. `consequence=imprint` without a track fails. With `imprintTrack=ordeal` and `imprintJump=3`, traits store the jump and no imprint dict is created.

### Out of scope

Imprint jump, brand apply, vice engine, rest observer, long-rest overstim, filth, mode auto-complete, host SDK edits, level-drain implementation.

---

## 9. Phase 7: Vices (implemented)

Handbook section is **Vices and Addictions** (in the OCR extract; the clean `lewd-handbook.md` only cites it from Brand of Addiction). ### What the book says

- A Vice is any substance or activity. Kinds: `chemical` (Con), `magical` (Wis), `psychological` (Cha), `complex` (caller picks the ability).
- Partake: addiction save vs DC, or become addicted. Base DC is per vice. DC +1 for each partake in the same 7-day span, even if already addicted.
- Addicted: disadvantage on checks and saves that are the vice's addiction save while affected or in its presence; disadvantage on saves against spells, effects, and persuasion that involve the vice; must partake once per 24 hours or enter withdrawal.
- Withdrawal: each long rest, addiction save or +1 exhaustion (nat 1 = +2). While withdrawing, a save is required when knowingly in the vice's presence; failure means they try to partake.
- Getting clean: each successful long-rest addiction save lowers DC by 1. At base DC, one more success ends it. Lesser Restoration = advantage until next long rest. Greater Restoration = auto-success until next long rest. Remove Curse on a magical vice = −1 DC per spell level above 2nd. Medicine + healer's kit = advantage until next long rest.
- Sex is a psychological vice, base DC 8. Withdrawal failure grants overstimulation, not exhaustion. Addicted: always hyperaroused; nymphomanic while arousal is above half maximum.
- Alcohol is complex, base DC 10, and must be consumed every 4 hours or the creature is intoxicated. Prior addiction makes later partakes +2 DC instead of +1.
- Succubus venom is chemical, base DC 14. Addiction: disadvantage vs charmed and infatuated. Withdrawal: `denied`.
- Brand of Addiction is not this system. It is a lustbrand that forces a sexual-fluids vice at DC 18 which cannot end while the brand remains.

### Do not use `IGuidanceContributor`

That interface lives in the host assembly `CampaignVault.Data.Guidance`, not in PluginSdk. This plugin cannot reference the host, so it cannot implement the interface. Convention-scanning it from the plugin will not see a type the plugin cannot name. Hints are also the wrong shape: once-per-campaign ledger, fixed `GuidanceTrigger` enum, budget 2 hints / 600 chars. A repeating temptation voice would be swallowed or delivered once.

Temptation reaches the LLM through channels this plugin already has:

- `IChangeContext.RecordMessage` on the commit that noticed withdrawal or a flare. That text is in the `take_turn` summary. Phrase it as an instruction: narrate the intrusive thought, then emit `lewd_vice` `resist` or `consume`. The engine does not write the prose.
- StatusEffect `vice:<id>` `recoveryHint` on the character, so a later `get_entity` still shows it.
- `skills/lewd-vices.md`. The host does not inject skills. They are a sidecar.

`Guidance/` stays empty. No host SDK change in this phase.

### Clock, not a free-running ticker

Nothing in the plugin runs on `advance_world`. A counter that must be incremented by a background tick will stay at 0. Store an absolute stamp instead.

On consume, `GetCurrentTimeAsync`: `last_hours = TotalDaysElapsed * 24 + Hour`. Withdrawal when `now_hours - last_hours >= 24` (alcohol flare threshold is 4 hours, from the sample). No `GetCharacter`. No downcast to `ChangeContext`. Characters are `context.Characters`.

Numeric fields go in `SystemStats.Attributes` (`float`). Identity goes in `Traits` (`string`). Not mode scratch — vices exist outside `lewd_encounter`.

Per vice id:

| Key | Where | Meaning |
|-----|--------|---------|
| `vice.<id>.kind` | Traits | chemical / magical / psychological / complex |
| `vice.<id>.dc` | Attributes | current addiction DC |
| `vice.<id>.base_dc` | Attributes | clean-target DC |
| `vice.<id>.addicted` | Traits | `true` / `false` |
| `vice.<id>.last_hours` | Attributes | campaign hour of last partake |
| `vice.<id>.week_start_hours` | Attributes | start of the +1-DC week |
| `vice.<id>.week_count` | Attributes | partakes in that week |
| `vice.<id>.withdrawal` | Traits | derived, also stored so snapshots show it |
| `vice.<id>.locked` | Traits | `true` while Brand of Addiction (or similar) forbids removal |
| `vice.<id>.ability` | Traits | con / wis / cha, when complex |

One StatusEffect per addicted vice: name `Vice: <id>`, `conditionName` `vice_<id>`, `durationType` Manual. `recoveryHint` carries the side effect one-liner and "narrate temptation when withdrawal or in presence".

### Verb

`lewd_vice`, not a generic `vice_consumption`. Prefix stays `lewd_*`.

```json
{ "$type": "lewd_vice", "characterId": "chars/bob", "action": "consume", "viceId": "sex", "d20": 12, "ability": "cha", "inPresence": true, "itemId": "items/wine" }
```

`action`: `consume` | `resist` | `note_presence`.

- `consume`: set `last_hours` to now (this is the "clear the craving ticker"). If not addicted, roll addiction save vs current DC; failure sets addicted and stamps the side-effect conditions. Always bump week count and DC +1 (alcohol with a prior-addiction trait: +2). `itemId` is recorded, not consumed — the LLM emits `item` / `item_use` if a charge is spent.
- `resist`: only meaningful in withdrawal or `inPresence`. Save vs DC. Failure → `RecordMessage` to narrate giving in and to emit `consume`. Success does not clear withdrawal; only a partake does.
- `note_presence`: no roll. Sets nothing durable except a message if addicted and in withdrawal: narrate the temptation voice.

Handler implements `IWorldChangeHandler`. `ShouldHandle` is `change is LewdViceChange`. `ApplyAsync(WorldChange, IChangeContext, CancellationToken)`. Fail if `characterId` is missing. Does not require `lewd_encounter`.

### Side effects (closed v1 catalog)

Yaml under `RulesetData/dnd5e/vices/` (`sex`, `sexual_fluids`, `alcohol`, `succubus_venom`). Engine applies only the mechanical line. The LLM narrates the rest.

| Id | Kind | Base DC | On addicted | Withdrawal fail |
|----|------|---------|-------------|-----------------|
| `sex` | psychological | 8 | stamp `hyperaroused`; if arousal > half max, also `nymphomanic` | +1 overstimulation (nat 1 = +2), not exhaustion |
| `sexual_fluids` | psychological | 18 | same hunger note as Brand of Addiction | same as `sex` |
| `alcohol` | complex | 10 | 4-hour threshold instead of 24 | +1 exhaustion; if `last_hours` older than 4, stamp `intoxicated` |
| `succubus_venom` | chemical | 14 | disadvantage is a `recoveryHint`, not a fake roll modifier | stamp `denied` |

Other handbook samples (mirage fever, healing magic) stay YAML-extensible later. Do not hardcode them.

### Long rest

`LewdViceObserver` : `IWorldChangeObserver`. `IsInterestedIn` is `change is RestChange` and `RestType` is long or `IntendedHours >= 8`. Not gated on `lewd_encounter`.

After the host rest handler has already decremented stacking `Exhaustion N` / `Overstimulation N`, roll the withdrawal save if `withdrawal=true` (`context.Rolls`, or fail asking for `d20` on a follow-up `lewd_vice` `action=rest` if `Rolls` is null). Failure adds a level. Success: DC −1, floored at `base_dc`. If DC was already `base_dc` and this save succeeds, clear addicted unless `locked=true`. Do not also clear via prose.

Sex / sexual_fluids add overstimulation. Others add exhaustion by renaming the existing stacking effect to `Exhaustion N` (host already parses that name on the next long rest).

### Flares and the temptation voice

The 24h (or 4h) gap is the flare clock. There is no second timer. When the observer or `note_presence` sees withdrawal, `RecordMessage` one line the LLM must narrate as intrusive thought / temptation, then choose `resist` or `consume`. High DC (DC ≥ base+6, or `locked`) says the voice is constant, not a hint to drop. Do not write the fantasy prose in the plugin.

Imprint `intrusive_thoughts` stays a phase 5 list. A vice flare may append a short token `vice:<id>` there only after phase 5 exists. Until then the message + status hint is enough.

### Bad-end and brands

- Phase 3 `lewd_bad_end` `consequence=vice` only stores `bad_end_vice_id`. Phase 7, on first `lewd_vice` or on a one-shot apply when the observer sees that trait, sets addicted at base DC and clears the pending id.
- Phase 4 Brand of Addiction sets `vice.sexual_fluids.locked=true` and addicted at DC 18. It does not roll withdrawal. Removal of the brand clears `locked` and then the normal clean rules apply.

### Tests (when built)

- Consume sets `last_hours` from a fake clock and clears withdrawal.
- Save failure sets addicted; a second partake in-week raises DC.
- 24h gap derived from stamps, with no background incrementer.
- Sex withdrawal failure stamps overstimulation, not exhaustion.
- `locked` vice ignores a successful clean save.
- Observer does not run when the mode is inactive — rest is enough.
- No test should reference `IGuidanceContributor` or `ChangeContext`.

---

**References:**
- lewd-handbook.md (sections: Pregnancy & Fertility, Concubi & Lustbrands, Edging/Climax/Overstimulation, Conditions & Bondage, Sexual Interaction, etc.)
- Current source (Lewd*Handler.cs, Mechanics/*, LewdKeys.cs, skills/*.md, RulesetData yaml)
- lewd-handbook-plugin-plan.md (original shape + observer guidance)
- RULES_NOTES.md (encoding decisions)

---

*This file was written because the plan did not previously exist on disk.*