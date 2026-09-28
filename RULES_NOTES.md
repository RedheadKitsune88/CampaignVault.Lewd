## Source texts

- Canonical mechanics: *The Lewd Handbook* **3.7.1** (`~/Downloads/The lewd handbook 3.7.1 [Low-Res].pdf`) and the fuller text dump `~/Downloads/lewd_handbook.md`. Prefer these over the older truncated `lewd-handbook.md` extract when they disagree.
- Creature adaptations: *Monsterfucker's Bestiary* (`~/Downloads/MM.pdf`) — unfinished community adaptation of official monsters for Lewd Handbook arousal/implements. PDF fonts extract poorly; curated `RulesetData/dnd5e/creatures/*_lewd.yaml` stubs are intentional, not a full port.
- Plugin encoding stays curated stubs (spells/classes/creatures/feats) for the LLM — not a page-faithful reprint.

# RULES_NOTES — Lewd Handbook encoding

Sources (operator machine only; do not commit PDF/extracts):

| Role | Path |
|------|------|
| Canonical | `~/Downloads/Lewd Handbook.pdf` |
| Working extract | `~/Downloads/lewd-handbook.md` (prefer) |
| Raw OCR | `~/Downloads/lewd_handbook.md` (last resort) |

Encoded from `lewd-handbook.md` sections below. Re-check the PDF when extracts disagree.

## Arousal & stimulation

- Arousal starts at 0 and rises with stimulation (reverse HP).
- Arousal max: recovery die max + Con at 1st level, then the die's average (rounded up) + Con per level (`ArousalMath`). Synced when a character is created, updated or levels up, and whenever a lewd verb loads the pool (Altruism's suppression is kept on top). The die comes from `lewd_encounter.recovery_die` or the sexual history.
- `arousal` and `numbing` are core `ownerManaged` pools with `startsAt: zero`: core creates them once and never refills or resizes them, so arousal starts at 0 and numbing survives sheet updates.
- Soothing lowers arousal (floor 0).
- Numbing points absorb stimulation first; leftover raises arousal. Numbing does not stack; choose old or new. Long rest clears depleted/remaining numbing unless a feature says otherwise.
- Long rest: arousal reduced by **half of maximum** (not “fill to max”), numbing cleared — applied by `lewd_rest` (below).

## Recovery dice

- Pool `recovery_dice` (core refills it on a long rest). `lewd_recover` spends 1..proficiency dice, each die + Con off arousal, rolled by the host.
- Only in a window: right after a climax (until that character's next turn start or scene end), or within 1 hour after a short rest. Spending after a climax sets incapacitation to rounds = dice spent (never shorter than already owed).

## Rest pipeline

Core publishes `core.rested.v1` for completed rests only. `LewdRestEventHandler` turns it into one plugin-emitted `lewd_rest` per character, which runs in order: vice (pending bad-end vice, withdrawal save on a long rest), brands, imprints (pending jump, long-rest decondition), pregnancy (clock sync, rest save), arousal (long: −½ max, clear numbing; short: open the recovery window). Characters without lewd pools are skipped. The old per-observer rest hooks are gone.

## Consent & inhibition

- Inhibition Bonus = chosen Int/Wis/Cha modifier (locked at creation).
- Unwilling: add Inhibition to AC vs martial advances and to saves vs unwanted advances/effects.
- Willing / consenting: treat Inhibition as **0** unless already negative.
- Selective consent: only listed partners (and/or kinds) are willing.
- Hard limits: advances of that tag/type **fail the commit** (plugin policy). Player limits (`lewdHardLimits`) and the character's own (`hard_limits`) both apply.
- Handbook consent assumes several humans at a table. Here the player is the only human, so the plugin splits it: the player's content settings (`lewdNarration`, `lewdNonConsent`, `lewdHardLimits`, campaign options marked `playerOnly`) versus each character's in-fiction stance (`lewd_stance`: scene State or Traits `lewd_encounter.*`).
- Soft limits / kinks: reduce / increase applied stimulation (plugin: ×0.5 / ×1.5 after tag match).

## Advances

- **Martial**: attack-style roll vs AC + Inhibition; stim dice + Str (or Dex if finesse).
- **Indirect**: save vs DC; Inhibition added to the save when unwanted.
- **Skilled**: skill check vs DC or contested; stim by DM / supplied amount.
- Plugin MVP: LLM supplies resolved `hit` / `stimulationAmount`; engine enforces consent/limits and applies pools.

## Edging & climax

- Start of turn at max arousal → gain **edging** and make a climax save.
- Edging: Nymphomanic **without** forcing Inhibition to 0; half movement to brace or fall prone; successful advances against edged target deal **max** stim.
- Climax save: d20 + Inhibition, DC 15. Track successes and failures separately until three of a kind.
  - 3 successes → arousal = max − 1, lose edging, reset counters.
  - 3 failures → climax.
  - Nat 1 → two failures; Nat 20 → arousal = max − 1, lose edging.
- Stimulation while at/above max → immediate failed climax save (two if critical). Stim ≥ arousal max → **instant climax**.
- Climax: lose edging; may spend Recovery Dice (≤ proficiency) to lower arousal; incapacitated for rounds = dice spent (or until end of next turn if none). Engine: `climax_incap_turns` = 1 on a first climax, +1 per climax while still incapacitated; counted down by `lewd_turn_start` (driven by `mode_transition action=turn`), then streak and stunned/paralyzed clear. `lewd_recover` right after the climax raises it to rounds = dice spent. Scene exit ends incapacitation.

## Turn economy

- One action per turn: at entry only the first participant holds `action = 1`; `turn` gives it to whoever is up. `lewd_advance` spends it, and so does `lewd_bind` / `lewd_escape` inside a scene. Core charges the slot before the handler runs (`TryConsumeActionSlot`) and refunds it if the handler fails, so a second act or an out-of-turn act is refused with "move on with `mode_transition action=turn`".
- Enabling `lewd_encounter` is player-only (`playerOnlyModeIds`).

## Overstimulation

- Stacking levels 1–6 (mirror exhaustion). Level 6 = Bad-Ended. YAML: single stacking condition `overstimulation` (like `exhaustion`). StatusEffect name must be `Overstimulation N` so host long-rest decrement parses the level.
- Multiple climaxes while still incapacitated: 2nd → Stunned, 3rd → Paralyzed, each climax after the 3rd → +1 overstimulation. Each such climax also extends incapacitation (narrate +1 round; engine records `climax_streak`).
- Extended edging: beats while edging (`edging_beats`, one per turn start, 10 beats ≈ 1 hour). After hours equal to Inhibition, +1 overstimulation per further hour, stamped on the sheet by `lewd_turn_start`.
- Cascade (current level and below): 1 Intoxicated, 2 Hyperaroused, 3 Inhibition 0, 4 Infatuated by source, 5 climax does not clear edging, 6 `bad_ended`. Cascade conditions the plugin stamped are removed again when the level drops (resynced at scene end from the sheet's `Overstimulation N`).
- Long rest −1 only if no sexual stim while resting — host decrements the stacking status; do not also clear it in prose.

## Filth / fluids

- General dirt is core `soil` / `DirtMark` (blood, mud, dust…). Sexual mess uses the same verb with namespaced kinds `lewd.cum` / `lewd.fluids` when `lewdFluids=on` (default **off**).
- A climax with a known `finish` (`inside` / `outside`) and deposit site stamps soil via `LewdSoilHandler` follow-ups. `lewd_advance` can set `finish` + `targetAnatomy` (pending on the actor until they climax); `lewd_climax_check` can set `finish` / `targetAnatomy` / `depositOnId` on the check itself. No finish → no engine soil.
- Verbal / non-contact climaxes never soil. Hard limits `fluids` / `marking` / `creampie` / `cum` / `sexual_fluids` refuse. `lewdExternalMarks=off` skips DirtMarks; `lewdCreampiePregnancy` (`off`|`prompt`|`auto`) bridges inside finishes to `lewd_pregnancy`. `lewdFluidViceHook` nudges `sexual_fluids` presence when `lewd.*` soil lands.
- With `lewdFluids=off`, the model still records dirt via core `soil` or appearance/item tags as before.

## Occupancy / plugs / leaks

- `lewdInsertedToys` (default off): enables `lewd_insert` / `lewd_remove` and passive stim while occupied. Occupancy is Trait JSON `occupied` (unprefixed, visible like bindings) plus StatusEffects (`Plugged`, `Beads seated`, `Occupied`).
- Seal: `open` | `plugged` | `beaded`. Plugs hold internal deposits; beads leak on travel/unplug/bead-pull, not on quiet turn ticks.
- Inside finish writes `lewd_encounter.internal_deposits` and stamps `Filled`. If sealed, no immediate external soil; `lewdLeaks` (default on with fluids) emits `leak.v1` → core `soil` on turn/travel/remove.
- `lewd_advance leaveInserted` + `targetAnatomy` seats the implement/partner when toys are on.
- Pending finish is mirrored to sheet Traits (`lewd_encounter.pending_finish` / `pending_target_anatomy` / `pending_deposit_on`) so a later `lewd_climax_check` outside the scene still sees the deposit intent.
- `lewd_cleanup`: clear internal deposits (optional `removeToys`) and queue core `soil` clears for `lewd.cum` / `lewd.fluids`.
- Hard limits: `plugs`, `beads`, `insertion`, `toys`, `anal`.

## Kink / verbal gating

- Kink/soft/hard tags match `stimulationType` plus aliases (piercing↔penetration/phallic, bludgeoning↔impact, thunder↔vibration, psychic↔verbal, etc.).
- Purely verbal / non-contact skilled or indirect advances: virgin, strictly_vanilla, modest_lover, willingly_celibate capped at +2 unless physical/contact tags or `flirt_beats` ≥ 3. experienced_kinkster, promiscuous, erotic_professional uncapped. devoted_partner uncapped only if `allowed_partners` is set.
- Verbal climax blocked for capped histories until `had_physical` is true.

## Pregnancy

- Not ticked by `lewd_advance`. Verb `lewd_pregnancy` writes character Traits (`pregnant`, `pregnancy_progress` 0–100, `pregnancy_type`, `pregnancy_source`, `pregnancy_offspring`) and stamps StatusEffect `pregnant`. Mirrors onto the participant when the encounter is active.
- Traditional: impregnator Con check vs DC 10 + target Con mod + proficiency (condom DC 25). Oil, beads, or infertile auto-fail the check (commit still succeeds). Nontraditional: target Con save vs supplied DC; inhibition added only if unwanted. Beads and `autoSucceedSave` (ward) resist. Hyperfertile advantage / pregnancy-save disadvantage and hypervirile save disadvantage need `secondD20`.
- Unwilling impregnation fails unless `lewdNonConsent` allows it for the target. `contraceptive` accepts `condom` / `oil` / `beads` or the catalog item names (`oil_of_impotence`, `beads_of_prevention`, `potion_of_infertility`). Hard limits `pregnancy` / `impregnation` / `breeding` / `repro` always fail the commit.
- Clock: conception stamps `pregnancy.start_hours` and `term_hours` (traditional 270 days, nontraditional 3 days; `termDays` overrides). Progress follows campaign time, synced on travel, activity, needs, schedule and rest commits; at 25% the pregnancy becomes visible, at 100% it is flagged due with a birth nudge. `action: advance` restarts the clock at the new progress (manual jumps still work).
- Rest: each completed rest makes the DC 15 Con save once (guarded by the rest's end hour). Failure stamps Poisoned for 1d4 hours, clocked by `poisoned_until_hours` and cleared by the sync. `action: rest` still exists for a manual save.
- Birth / terminate publish `pregnancy.v1`. A birth with Brand of Fertility raises that brand one tier. `noEscape` on a successful impregnation sets `bad_ended` only — no level drain.
- Crit vs pregnant: `termination_save` with `dc` = damage. `force` is Power Word Pregnant (progress 50, or double offspring).

## Bad-Ending

Handbook: edging climax with no recovery dice left, arousal maximum ≤ 0, overstimulation 6, and capture-plus-impregnation are automatic. Defeat or unconscious is optional ("may be"), not automatic.

Encoding: every first mark publishes `bad_end.v1`. `BadEndState.Apply` writes the participant flag, traits, and StatusEffect `Bad-Ended` / `bad_ended`. `lewd_bad_end` records `defeat` or `explicit` and stores a consequence. It does not drain a level. `imprint` stores track + jump for phase 5. `vice` stores `lewd_encounter.bad_end_vice_id` for `lewd_vice` / the rest observer. HP ≤ 0 only prompts. A missing `recovery_dice` pool does not count as zero. Dropping overstim does not clear the flag.
- `IPluginTraitsUpgrader` (`LewdTraitsUpgrader`) migrates legacy unprefixed anatomy/history/vice Traits onto `lewd_encounter.` on character load. It also hides bookkeeping (`imprints`, `intrusive_thoughts`, `imprint_inhib`, `lustbrand.<id>.*`, `imprint.<id>.*`, fertility flags, `bad_end_imprint_*`, `bad_end_vice_id`), un-prefixes `bindings`/`posture`, renames anatomy aliases, and fills universal anatomy. Readers fall back to the old key (`LewdKeys.LegacyTraitKey`). Visible on the card: `bindings`, `posture`, `pregnant*`, `bad_ended*`, `lustbrands`, `lustbrand_glow`.

## Plugin encoding choices

- Mode id `lewd_encounter`; verbs `lewd_advance`, `lewd_climax_check`, `lewd_recover`, `lewd_bind`, `lewd_unbind`, `lewd_escape`, `lewd_stance`; plugin-emitted `lewd_turn_start` / `lewd_scene_end` / `lewd_rest` / `lewd_echo_check`.
- `intimacyTone` is migrated at host startup by `LewdCampaignOptionsUpgrader` (core `IPluginCampaignOptionsUpgrader`).
- Characters with unset `lifeStage` are never guessed; a context line names them in lewd-enabled campaigns.
- Adults only: every character a verb touches, and every participant at mode entry (`ValidateEntry`), needs `LifeStage` adult/elder and a description that doesn't read as a minor.
- Pools: `arousal`, `numbing`, `recovery_dice`.
- Participant scratch: climax counters, `edging`, overstimulation, mirrors. Stance keys (`consent`, `allowed_partners`, `hard_limits`, `soft_limits`, `kinks`, `inhibition`) are scene overrides only; defaults come from character Traits.
- Content pack is curated (not full catalog). Spells/feats YAMLs carry handbook mechanical summaries for the LLM; full prose stays in the extract/PDF.


## Player settings (campaign options, playerOnly)

| Option | Values | Effect |
|--------|--------|--------|
| `lewdNarration` | `explicit` \| `suggestive` (default) \| `fade` | Narration only; mechanics always resolve |
| `lewdNonConsent` | `off` (default) \| `not_against_pc` \| `on` | Whether unwanted acts resolve; `not_against_pc` never against `Character.IsPc` |
| `lewdHardLimits` | comma list | Always refused |

Unknown `lewdNonConsent` values fail closed to `off`. `revoked` stance and hard limits refuse in every setting. `intimacyTone` is retired.

## Implements & anatomy

- Artificial: Item / ItemDefinition Properties (`damageDice`, `damageType`, `implementTags`, `finesse`).
- Natural: `SystemExtension.Traits["lewd_encounter.anatomy.*"]` e.g. `die=1d8;tags=phallic,natural;size=medium` (mode-gated on NPC cards).
- `Traits["lewd_encounter.sexual_history"]`, `Traits["lewd_encounter.recovery_die"]` mirror backgrounds.
- `lewd_advance` resolve order: `implementId` → `anatomyKey` → `stimulationDice`; only when none is named and dice are needed: a held item marked as an implement (`implementTags` / `stimulationDice` / `lewdCategory`), then the character's declared implement anatomy (catalogue order, `source=default` last), then derived `hands`. The guess never picks a receptive part (pure implements rank above the mouth); an explicit `anatomyKey` may name any slot, receptive included, so its tags reach the consent/hard-limit check.
- **Slot schema** (`AnatomySlots`): `hands`, `mouth`, `ass` are universal (derived at read time by `AnatomyTraits.Effective`, and written as `source=default` by the upgrader onto characters that already have a lewd profile: anatomy, history, stance, limits, kinks; vice/fertility bookkeeping alone does not count); `cock`, `pussy`, `breasts`, `tail` are declared by the DM. Value grammar adds `role=implement|receptive|both`; `none` marks a part absent; `anatomy.plan=custom` turns derivation off. Aliases (dick/penis, vagina, anus/butt, tits) normalise to the canonical slot. The upgrader sees only the Traits bag, not life stage, so it never writes slots onto a character without a lewd profile; the runtime derivation is the fallback for those.
- `LewdAnatomyContributor` runs on `lewd_encounter` entry: for each adult participant it lists the asked slots (`cock`, `pussy`, `breasts`) with no declaration and no `none`, in one keyed line. Minors never get the prompt.
- With `IChangeContext.Rolls` (Sdk 0.1.1): martial attack + stim dice can be rolled in-handler; otherwise LLM supplies pre-resolved fields.

## Bindings (restraint)

- Character Trait `bindings` (JSON, source of truth; unprefixed so a captive's cuffs show on the NPC card outside a scene; `posture` likewise) mirrored to participant State `bindings` + `posture` (structured; **no** prose keyword scanner). Restraints outlast the scene and work without one (captives, a chain gang).
- Verbs, all global: `lewd_bind`, `lewd_unbind`, `lewd_escape` (`slip` Dex/Str vs escape DC, disadvantage when encased/hog-tied/suspended; `break` Str vs break DC; `pick` vs lock DC with free hands, so a cuffed character needs a helper; `unlock` with the key; `cut` against hp). In a scene, binding or escaping is the actor's action; a failed escape keeps it spent.
- Entry fields: kind, sites, orientation, links, implies, materials, hardened, effects, itemId, escape/break DC, hp, plus `anchorId` (character, item or fixture), `locked` / `lockDc` (15) / `keyItemId`, `appliedById`, `erotic`. The kind alone seeds defaults (manacles, shackles, collar, leash/chain…).
- Consent: an erotic binding (in a scene or erotic gear) goes through the advance consent rules; plain restraint only through the player's hard limits. An unwilling target must be subdued (grappled, restrained, incapacitated, unconscious, paralyzed, stunned, already bound, 0 HP).
- `Restraint.Sync` stamps the implied conditions (cuffed, hobbled, leashed, gagged, blinded…), one `Bound` summary status that blocks somatic/verbal components when hands or mouth are bound, and one Hard `bound to` engagement per anchor. Core blocks travel on that engagement unless the anchor travels in the same commit, so a coffle moves together. `binding_changed.v1` on bind / unbind / escape.

## Brand fixes (0.7.0)

- Echoes: a climax within 5 ft (physical engagement, a binding link, Touch position; Close or unknown distance forces with a note) forces the bearer's climax via `lewd_echo_check`, including bearers outside the scene.
- Fertility: climax only from unprotected stimulation (last advance without condom/oil/beads); Bestial uses the same flag. +1 tier per birth.
- Ruin: rolls a recovery die (+ tier) into arousal and psychic damage; at 0 HP the bearer stays at 1 HP and the brand gains a tier.
- Duplicate brand entries collapse to the highest tier.

## Imprint anchors

Ticks from `lewd_advance` carry the actor as `anchorId`; `lewd_imprint` accepts one. An anchored track's inhibition penalty applies only against its anchor; unanchored tracks feed `imprint_inhib`. Per-ruleset YAML imprint effects are deferred: the plugin has no YAML loader and core doesn't load custom categories.

## Sexual histories (backgrounds/)

Encoded from `lewd-handbook.md` § Sexual Histories & Experience. Histories are **in addition to** a normal 5e background in the book; plugin ships them as `BackgroundDefinition`-compatible YAML under `RulesetData/dnd5e/backgrounds/` so host merge can surface recovery die / implement / fetish limits to the LLM. Feat YAMLs under `feats/` remain mechanicalSummary mirrors.

| Id | Recovery die | Natural proficiency | Artificial cap | Starting fetish limit |
|----|--------------|---------------------|----------------|------------------------|
| virgin | d6 | no (pleasuring others) | 1 | Chaste Onlooker / Edge Puppet / Voyeur |
| willingly_celibate | d6 | none | 0 | Chaste Onlooker or Edge Puppet |
| strictly_vanilla | d6 | reproductive only | 2 | none |
| modest_lover | d8 | yes | 4 | 1 |
| devoted_partner | d8 | yes | 4 | ≤2 (not Anonymist/Wanton Whore/Swinger) |
| promiscuous | d8 | yes | 6 | ≤3 |
| experienced_kinkster | d10 | yes | 8 | 4–6 access, 3 active benefits |
| erotic_professional | d12 | yes | 8 | ≤3 |

Sexual XP thresholds for changing history on level-up: 0–300 Virgin/Celibate; 900–2700 Devoted/Promiscuous; 6500–14000 Kinkster; 23000+ Professional (table in extract; Modest/Vanilla sit between early bands in practice — confirm PDF if contested).

## Bound conditions (conditions/)

Encoded from § Bound Conditions: `cuffed`, `encased`, `engulfed`, `full_tied`, `gagged`, `hobbled`, `leashed`, `limb_bound`, `mitted`, `suspended`. Handbook `Limb-Bound` bullet text erroneously says “cuffed creature”; plugin YAML uses limb_bound semantics (limbs completely unusable). Default nonmagical binding stats (15 HP, escape/break DC 20) live on bondage item stubs.

## Implements, bondage gear, packs (items/)

Curated Item catalog stubs (not a full SRD table in the extract). Implements carry `damageDice` / `damageType` / `implementTags` / `finesse`. Bondage gear carries `seedsConditions` (+ optional escape/break/hp/materials). Packs are description-only loadout seeds for sexual histories. Exact GP/weight for toys are plugin estimates where the extract lacks a price table — prefer PDF if a table appears there.

## Curated spells (spells/) pack expansion

Prior set kept. Added handbook-named high-value spells (summaries only): `locate_hookup`, `lovenest`, `maddening_desire`, `vilgas_phallic_enhancement`, `ruin_orgasm`, `saint_resolve`, `sinful_caress`, `spectral_stockade`, `power_word_pregnant`, `pregnancy_ward`, `musk_cloud`, `vibe_check`.

**Lustbrands:** `remove curse` does **not** remove Lustbrands; only Wish or specialized features (handbook § Lustbrands). No `remove_lustbrand` spell stub invented. Working extract’s spell list begins at *Gangbang* (A–F spells may be truncated in `lewd-handbook.md`) — verify missing early alphabet against the PDF before encoding more.

## ItemDefinition schema (host)

Host loads `RulesetData/dnd5e/items/*.yaml` via `ItemDefinitionProvider` + `get_rules_reference` kind `items`.

Plugin item files use:
- `category`: Weapon | Armor | Clothing | Container | … (enum)
- `tags`: e.g. `lewd`, `implement`, `bondage`
- `properties`: open bag — `damage`/`damageDice`, `damageType`, `implementTags`, `finesse`, bondage `sites`/`implies`/`materials`, `lewdCategory` for plugin heuristics

Artificial implements → live `Item` instances (world_build). Natural anatomy stays on `SystemExtension.Traits`.

## Lustbrands (phase 4)

Handbook § Concubi Traits & Lustbrands. Closed catalog of 17 brands in `BrandCatalog`. Trait `lustbrands` is `id:tier` pairs. Each brand is a StatusEffect `lustbrand:<id>` (`Lustbrand: <Title>`), category Curse. Inhibition bonus drops by the **sum** of tiers. Glow is `mark` / `visible` / `bright` from the arousal pool.

`remove curse` does not remove a brand. `lewd_apply_brand` `action=remove` requires `method=wish|feature`. A `status` remove is restamped while the trait remains. A concubi whose last brand is removed (`concubi: true`) reapplies that id at tier+1 on the next real climax.

Brand of Addiction writes `lewd_encounter.vice.sexual_fluids.locked=true`, `addicted=true`, Attribute DC 18, and stamps StatusEffect `Vice: sexual_fluids`. Craving, saves, and withdrawal are `lewd_vice` (see Vices). Removal clears `locked` only.

Rest hooks (abundance, bestial, altruism max restore, addiction long-rest note) run in `lewd_rest`. Observer `LewdBrandObserver` handles heal (altruism) and status restamp. Ruin is applied in the climax handlers; Echoes through `lewd_echo_check`. Distance, True Love contact, and vow breaches are messages, not rolls.

## Imprints (phase 5)

Homebrew overlay. Handbook body has no imprint section. Five tracks only: `wanton`, `training`, `breeding`, `ordeal`, `cruelty`. Not the 8-category expansion. Not a vice.

Inside a scene, pressure goes to the ledger Trait `lewd_encounter.scene.imprints` and each track ticks at most once per scene at `lewd_scene_end`. `setLevel` seeds backstory. Trait `lewd_encounter.imprints` is `id:points:level:origin:day[:anchor]` pairs. Thresholds 3 / 7 / 12. Willing ticks have no save. Unwilling ticks are a WIS or INT save, DC 12 + level; the ability modifier is on the roll. Hard limits never imprint. Consensual tone skips unwilling auto-ticks.

`lewd_bad_end` still only stores the jump. The first later imprint write applies it and clears `lewd_encounter.bad_end_imprint_*`.

`lewd_decondition` is therapy (DC 10 + level, −1 to −3), aftercare (same, advantage, unwilling DC +2), or rest (DC 14, −1). Level 3 unwilling does not auto-drop. Exposure cancels that rest's drop. Breeding is locked by Brand of Fertility; training by Brand of Obedience.

Level 3 stamps `imprint:<id>`. Unwilling levels sum into `imprint_inhib`. Cruelty level 3 is +1 stim on pain or an overstimulated target. Training level 3's obey-save is a message, not a roll.


## Vices (phase 7)

Handbook **Vices and Addictions**. Closed catalog: `sex`, `sexual_fluids`, `alcohol`, `succubus_venom`. Verb `lewd_vice` (`consume` | `resist` | `note_presence` | `rest` | `treat`). Does not require `lewd_encounter`.

Game loop (`ViceTrack`):

| Stage | When | Effect |
|-------|------|--------|
| Sated | within half the withdrawal window | none |
| Craving | past half the window | announced once; narration |
| Withdrawal | past the window | StatusEffect `Withdrawal: <id>`; presence forces a save (disadvantage); long-rest save |
| Severe | 3+ windows | announced once; narration escalates (homebrew) |

- Presence save failure → `Compelled: <id>` until they partake by any means. Success (or `resist`) earns 1 Resolve (homebrew, max 3), spent as +1 each on the next long-rest save.
- Long-rest save (handbook: a normal roll) + Resolve; success lowers the DC, at base DC one more success ends the addiction; streak tracked.
- `treat`: Lesser Restoration or a healer's kit → advantage, Greater Restoration → automatic success, Remove Curse → magical vices only, DC −1 per slot level above 2nd. Aid lasts until the next long rest.
- Context: one line per party member whose stage changed (`LewdViceContributor`, keyed so it isn't repeated).

Clock is absolute campaign hours (`TotalDaysElapsed*24 + Hour`) on `last_hours`. No background ticker. Withdrawal when the gap reaches 24h (alcohol 4h). Numeric fields in Attributes (`vice.<id>.*`); identity flags in Traits (`lewd_encounter.vice.<id>.*`, mode-gated on cards). StatusEffect `Vice: <id>` / `vice_<id>` while addicted (outside-scene signal).

Brand of Addiction locks `sexual_fluids` at DC 18; an addiction that never partook starts its withdrawal clock at the next sync. Vices never claim a condition another source already applied. Bad-end `consequence=vice` stores `lewd_encounter.bad_end_vice_id`; the first `lewd_vice` or `lewd_rest` applies addicted at base DC.

Sex/sexual_fluids withdrawal failure bumps overstimulation. Alcohol bumps `Exhaustion N`. Succubus venom stamps `denied`. Temptation is `RecordMessage` + `recoveryHint`, not `IGuidanceContributor`.

## Rest surface

All rest effects run in `lewd_rest` (see **Rest pipeline**), emitted from `core.rested.v1`, so an interrupted rest does nothing. Long-rest overstim decrement stays host-side on `Overstimulation N`.

## High-stakes clan / ponyrider state

Marks, filth, and conditioning stay compatible with optional faction overlays (see **GoblinPonyriders** / CampaignVault.Goblins):

- Filth: core soil / `character_update` / `item_update` / `upsertItemDetail` (no plugin filth counter).
- Brands/imprints/vices: Trait strings + StatusEffects the LLM can read on `get_entity`.
- Participant mirrors inside `lewd_encounter` for encounter-scoped counters; durable vice/pregnancy/brand/imprint live on the Character.
- Unified Clans Defiance Clock / Clan Mark / Capture State live in the separate `GoblinPonyriders` plugin (`com.campaignvault.goblins`); bondage stays on Lewd `lewd_bind` / `lewd_insert`.
