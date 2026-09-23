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
- Arousal max uses Recovery Dice (sexual history) + Con mod (see Sexual Histories).
- Soothing lowers arousal (floor 0).
- Numbing points absorb stimulation first; leftover raises arousal. Numbing does not stack; choose old or new. Long rest clears depleted/remaining numbing unless a feature says otherwise.
- Long rest: arousal reduced by **half of maximum** (not “fill to max”). Pool template uses `Never`; handlers/LLM apply half-max reduction.

## Consent & inhibition

- Inhibition Bonus = chosen Int/Wis/Cha modifier (locked at creation).
- Unwilling: add Inhibition to AC vs martial advances and to saves vs unwanted advances/effects.
- Willing / consenting: treat Inhibition as **0** unless already negative.
- Selective consent: only listed partners (and/or kinds) are willing.
- Hard limits: advances of that tag/type **fail the commit** (plugin policy).
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
- Climax: lose edging; may spend Recovery Dice (≤ proficiency) to lower arousal; incapacitated for rounds = dice spent (or until end of next turn if none).

## Overstimulation

- Stacking levels 1–6 (mirror exhaustion). Level 6 = Bad-Ended. YAML: single stacking condition `overstimulation` (like `exhaustion`). StatusEffect name must be `Overstimulation N` so host long-rest decrement parses the level.
- Multiple climaxes while still incapacitated: 2nd → Stunned, 3rd → Paralyzed, each climax after the 3rd → +1 overstimulation. Each such climax also extends incapacitation (narrate +1 round; engine records `climax_streak`).
- Extended edging: beats while edging (`edging_beats`, 10 beats ≈ 1 hour). After hours equal to Inhibition, +1 overstimulation per further hour (state on turn advance; stamp `Overstimulation N` on the next climax/advance that calls the helper, or emit `status` if the scene ends first).
- Cascade (current level and below): 1 Intoxicated, 2 Hyperaroused, 3 Inhibition 0, 4 Infatuated by source, 5 climax does not clear edging, 6 `bad_ended`.
- Long rest −1 only if no sexual stim while resting — host decrements the stacking status; do not also clear it in prose.

## Filth

- Not an engine counter. Lewd advance, climax, and bind do not increment filth. Verbal / non-contact beats never dirty a target.
- The model records dirt when the fiction changes (sex, dust, mud, cleanup) via core verbs: `character_update.appearanceOverride` + temporary visual tags; `item_update` temporary tags / `newState` on the container, not the contents; `upsertItemDetail` for durable stains, retired when no longer true.

## Kink / verbal gating

- Kink/soft/hard tags match `stimulationType` plus aliases (piercing↔penetration/phallic, bludgeoning↔impact, thunder↔vibration, psychic↔verbal, etc.).
- Purely verbal / non-contact skilled or indirect advances: virgin, strictly_vanilla, modest_lover, willingly_celibate capped at +2 unless physical/contact tags or `flirt_beats` ≥ 3. experienced_kinkster, promiscuous, erotic_professional uncapped. devoted_partner uncapped only if `allowed_partners` is set.
- Verbal climax blocked for capped histories until `had_physical` is true.

## Pregnancy

- Not ticked by `lewd_advance`. Verb `lewd_pregnancy` writes character Traits (`pregnant`, `pregnancy_progress` 0–100, `pregnancy_type`, `pregnancy_source`, `pregnancy_offspring`) and stamps StatusEffect `pregnant`. Mirrors onto the participant when the encounter is active.
- Traditional: impregnator Con check vs DC 10 + target Con mod + proficiency (condom DC 25). Oil, beads, or infertile auto-fail the check (commit still succeeds). Nontraditional: target Con save vs supplied DC; inhibition added only if unwanted. Beads and `autoSucceedSave` (ward) resist. Hyperfertile advantage / pregnancy-save disadvantage and hypervirile save disadvantage need `secondD20`.
- Unwilling impregnation fails unless `intimacyTone` is grimdark. Hard limits `pregnancy` / `impregnation` / `breeding` / `repro` always fail the commit.
- Rest: LLM emits `action: rest` with a d20. Failure stamps Poisoned; duration 1d4 hours is narrated, not clocked. Term: `action: advance` (+1 traditional, +25 nontraditional per call unless `progressDelta` is set). At 100, nudge birth. `noEscape` on a successful impregnation sets `bad_ended` only — no level drain.
- Crit vs pregnant: `termination_save` with `dc` = damage. `force` is Power Word Pregnant (progress 50, or double offspring).

## Bad-Ending

Handbook: edging climax with no recovery dice left, arousal maximum ≤ 0, overstimulation 6, and capture-plus-impregnation are automatic. Defeat or unconscious is optional ("may be"), not automatic.

Encoding: `BadEndState.Apply` writes the participant flag, traits, and StatusEffect `Bad-Ended` / `bad_ended`. `lewd_bad_end` records `defeat` or `explicit` and stores a consequence. It does not drain a level. `imprint` stores track + jump for phase 5. `vice` stores `bad_end_vice_id` for `lewd_vice` / the rest observer. HP ≤ 0 only prompts. A missing `recovery_dice` pool does not count as zero. Dropping overstim does not clear the flag.

## Plugin encoding choices

- Mode id `lewd_encounter`; verbs `lewd_advance`, `lewd_climax_check`, `lewd_bind`, `lewd_unbind`.
- Pools: `arousal`, `numbing`, `recovery_dice`.
- Participant scratch: `consent`, `hard_limits`, `soft_limits`, `kinks`, `inhibition`, climax counters, `edging`.
- Content pack is curated (not full catalog). Spells/feats YAMLs carry handbook mechanical summaries for the LLM; full prose stays in the extract/PDF.


## Intimacy tone (campaign option)

Plugin-declared `intimacyTone` in `plugin.json` → `SystemOptions["intimacyTone"]`:

| Value | Unwilling advance |
|-------|-------------------|
| `consensual` (default) | Fail commit |
| `fade` | Success, **0 stim**, physical-state nudge |
| `grimdark` | Allow; use Inhibition on AC/saves |

`hard_limits` and `revoked` **always fail-closed** in every tone. Plugin install ≠ per-scene consent.

## Implements & anatomy

- Artificial: Item / ItemDefinition Properties (`damageDice`, `damageType`, `implementTags`, `finesse`).
- Natural: `SystemExtension.Traits["anatomy.*"]` e.g. `die=1d8;tags=phallic,natural;size=medium`.
- `Traits["sexual_history"]`, `Traits["recovery_die"]` mirror backgrounds.
- `lewd_advance` resolve order: held Item → `implementId` → anatomy Traits → explicit `stimulationDice` / `stimulationAmount`.
- With `IChangeContext.Rolls` (Sdk 0.1.1): martial attack + stim dice can be rolled in-handler; otherwise LLM supplies pre-resolved fields.

## Binding graph v1

- Participant State `bindings[]` + `posture` (structured; **no** prose keyword scanner).
- Verbs: `lewd_bind`, `lewd_unbind`.
- Entry fields: kind, sites, orientation, links, implies, materials, hardened, effects, itemId, escape/break DC, hp.
- Handlers stamp implied condition names and sensory StatusEffects (blinded/gagged/restrained) when applicable.

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

Brand of Addiction writes `vice.sexual_fluids.locked=true`, `addicted=true`, DC 18, and stamps StatusEffect `Vice: sexual_fluids`. Craving, saves, and withdrawal are `lewd_vice` (see Vices). Removal clears `locked` only.

Observer `LewdBrandObserver` handles rest (abundance, bestial, altruism max restore, addiction long-rest note), heal (altruism), and status restamp. Ruin and echoes stim are applied in the climax/advance handlers because the observer runs after those commits. Distance, True Love contact, and vow breaches are messages, not rolls.

## Imprints (phase 5)

Homebrew overlay. Handbook body has no imprint section. Five tracks only: `wanton`, `training`, `breeding`, `ordeal`, `cruelty`. Not the 8-category expansion. Not a vice.

Trait `imprints` is `id:points:level:origin:day` pairs. Thresholds 3 / 7 / 12. Willing ticks have no save. Unwilling ticks are a WIS or INT save, DC 12 + level; the ability modifier is on the roll. Hard limits never imprint. Consensual tone skips unwilling auto-ticks.

`lewd_bad_end` still only stores the jump. The first later imprint write applies it and clears `bad_end_imprint_*`.

`lewd_decondition` is therapy (DC 10 + level, −1 to −3), aftercare (same, advantage, unwilling DC +2), or rest (DC 14, −1). Level 3 unwilling does not auto-drop. Exposure cancels that rest's drop. Breeding is locked by Brand of Fertility; training by Brand of Obedience.

Level 3 stamps `imprint:<id>`. Unwilling levels sum into `imprint_inhib`. Cruelty level 3 is +1 stim on pain or an overstimulated target. Training level 3's obey-save is a message, not a roll.


## Vices (phase 7)

Handbook **Vices and Addictions**. Closed catalog: `sex`, `sexual_fluids`, `alcohol`, `succubus_venom`. Verb `lewd_vice` (`consume` | `resist` | `note_presence` | `rest`). Does not require `lewd_encounter`.

Clock is absolute campaign hours (`TotalDaysElapsed*24 + Hour`) on `last_hours`. No background ticker. Withdrawal when the gap reaches 24h (alcohol 4h). Numeric fields in `Attributes`; identity flags in `Traits`. StatusEffect `Vice: <id>` / `vice_<id>` while addicted.

Brand of Addiction locks `sexual_fluids` at DC 18. Bad-end `consequence=vice` stores `bad_end_vice_id`; first `lewd_vice` or `LewdViceObserver` on rest applies addicted at base DC.

Sex/sexual_fluids withdrawal failure bumps overstimulation. Alcohol bumps `Exhaustion N`. Succubus venom stamps `denied`. Temptation is `RecordMessage` + `recoveryHint`, not `IGuidanceContributor`.

## Rest surface

Host `RestChange` exists. Plugin observers subscribe after successful commits:

| Observer | Rest interest |
|----------|---------------|
| `LewdImprintObserver` | Long rest decondition when unexposed |
| `LewdBrandObserver` | Short/long rest brand hooks |
| `LewdViceObserver` | Any rest syncs withdrawal; long rest rolls withdrawal saves |

Pregnancy rest poison remains `lewd_pregnancy` `action: rest`. Long-rest overstim decrement is host-side on `Overstimulation N`. If rest commits are absent from a table's flow, that is a host gap — document and propose a small Sdk helper rather than inventing `advance_world` timers.

## Feather-goblins-style state

Marks, filth, and conditioning stay compatible with feather-goblins-style Traits/tags:

- Filth: core `character_update` / `item_update` / `upsertItemDetail` only (no plugin filth counter).
- Brands/imprints/vices: Trait strings + StatusEffects the LLM can read on `get_entity`.
- Participant mirrors inside `lewd_encounter` for encounter-scoped counters; durable vice/pregnancy/brand/imprint live on the Character.
