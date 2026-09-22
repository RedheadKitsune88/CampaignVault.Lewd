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

- Stacking levels 1–6 (mirror exhaustion). Level 6 = Bad-Ended.
- YAML: single stacking condition `overstimulation` (like `exhaustion`).

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
