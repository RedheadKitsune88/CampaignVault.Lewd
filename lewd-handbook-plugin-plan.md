# Plan: Lewd Handbook External Plugin

## Goal

Ship the Lewd Handbook as an **out-of-tree** CampaignVault plugin: an opt-in `lewd_encounter` interaction mode layered on `ActiveSystem=dnd5e`, plus a YAML data pack and LLM-client skill sidecars. Adult/optional content stays in a **separate GitHub account/repo**. The main CampaignVault repo never receives handbook text, assets, or mode code.

**Prerequisite (assumed done before this work starts):** `CampaignVault.PluginSdk` is published to **nuget.org** at a stable version (currently designed as `0.1.0`). The plugin repo uses only:

```xml
<PackageReference Include="CampaignVault.PluginSdk" Version="0.1.0" />
```

No `ProjectReference` to the host, no private feed, no PAT, no `InternalsVisibleTo`, and **never** ship `CampaignVault.PluginSdk.dll` beside the plugin.

**Platform already available (do not rebuild):** Track B (`IInteractionMode` / `ModeEncounter` / `IWorldChangeObserver` / `EnabledModeIds` / `mode_transition`) and Track E PluginSdk (wire registry, nested `Plugins/<Name>/`, `plugin.json`, ALC Sdk unification, RulesetData merge). In-tree template: `plugins/CraftingMode`.

---

## Why this shape

The handbook is a 5e **combat mirror** for sexual encounters (arousal ≈ HP, stimulation ≈ damage, etc.). CampaignVault already has the right primitives:

| Handbook concept | CampaignVault surface |
|------------------|------------------------|
| Arousal / max arousal | `ResourcePools["arousal"]` (Current / Max) |
| Numbing | `ResourcePools["numbing"]` or temp buffer subtracted before arousal gain |
| Soothing | `resource` spend/grant that lowers arousal |
| Stimulation by type | Typed application → arousal delta; kink/limit via `DamageResistances` / attributes |
| Inhibition Bonus | `Attributes["inhibition"]` (or derived from Int/Wis/Cha choice at setup) |
| Sexual AC (willing / unwilling) | Mode-local AC formula using Inhibition when consent is absent |
| Direct / martial advance | Mode verb + attack-roll style check via active `IRulesetModule.Actions` |
| Indirect / skilled advance | Mode verb + save or contested skill check via `Actions` |
| Edging / climax saves | Status `edging` + death-save-style counters on participant `State` |
| Overstimulation | Status levels mirroring exhaustion (`overstimulation_1`…`_6`) |
| Afterglow / bad end | StatusEffects + mode completion outcomes |
| Partners / contact | `engagement_relation` (embrace / watch / etc.) |
| Kinks / limits / hard limits | Character attributes + resistance map; consent flags per participant |
| Subclasses / feats / gear / spells | YAML under plugin `RulesetData/dnd5e/` (last-wins merge) |

**Do this as a mode + data pack.** Do **not** fork `Dnd5eRulesetModule`, clone combat into a second `ICombatRuleset`, or extend `RulesetActionType`. Mode verbs are `[PluginWorldChange]` types; dice math delegates to the campaign’s already-active ruleset module.

---

## Repo & packaging

### Ownership

- New private (or public-but-clearly-marked) repo under a **separate GitHub account**.
- Suggested names: `cv-lewd-handbook`, `lewd-handbook-mode` — pick one and keep `plugin.json` `id` stable (`com.<org>.lewd-handbook`).
- LICENSE / README must state: adult content, operator opt-in only, full-trust DLL (same trust model as any CampaignVault code plugin).

### Layout

```
cv-lewd-handbook/
├── README.md
├── LICENSE
├── LewdHandbook.sln
├── src/
│   └── LewdHandbook/
│       ├── LewdHandbook.csproj          # PackageReference PluginSdk only
│       ├── plugin.json
│       ├── LewdEncounterMode.cs         # IInteractionMode + IModeStateMachine
│       ├── Changes/                     # [PluginWorldChange] types
│       ├── Handlers/                    # IWorldChangeHandler
│       ├── Observers/                   # optional IWorldChangeObserver
│       ├── Guidance/                    # optional soft-trigger IGuidanceContributor
│       ├── RulesetData/
│       │   └── dnd5e/
│       │       ├── pools/
│       │       ├── conditions/
│       │       ├── classes/             # phased content
│       │       ├── feats/
│       │       ├── spells/
│       │       └── items/
│       └── skills/                      # Claude/OpenCode markdown sidecars
├── tests/
│   └── LewdHandbook.Tests/              # unit tests against Sdk types + harness
└── pack/
    └── README.md                        # how to drop into host Plugins/
```

### Install into a host

```
Plugins/
  LewdHandbook/
    plugin.json
    LewdHandbook.dll
    RulesetData/
    skills/                # optional; host logs path only
```

`plugin.json` (target shape):

```json
{
  "id": "com.example.lewd-handbook",
  "displayName": "Lewd Handbook",
  "version": "0.1.0",
  "minEngineVersion": "0.2.0",
  "modeIds": ["lewd_encounter"],
  "rulesetDataRoots": ["./RulesetData"],
  "skillsPath": "./skills"
}
```

Bump `minEngineVersion` when the plugin needs a newer host/Sdk contract.

### Build / publish plugin artifact

- `dotnet pack` or a simple zip of the nested folder contents.
- CI: build + unit tests; optional smoke that loads the DLL into a disposable host with PluginSdk type-identity checks (mirror CraftingMode harness ideas).
- Distribution is **manual drop-in** for MVP (Track D marketplace is out of scope).

---

## Design decisions

### 1. Mode id and enablement

- `ModeId` = `lewd_encounter`.
- `CompatibleSystems` = `["dnd5e"]` for MVP (PF2e port later if ever).
- Campaign must opt in via `campaign_update` → `EnabledModeIds` including `lewd_encounter`.
- Enter/exit via core `mode_transition` (`action: enter|exit`, `locationId`, `participantIds`).

### 2. Consent & limits (first-class, not prose-only)

Per-participant flags live on `ModeParticipantState.State` (and optionally mirrored on character attributes for persistence between scenes):

| Key | Meaning |
|-----|---------|
| `consent` | `willing` \| `selective` \| `unwilling` \| `revoked` |
| `allowed_partners` | optional list of character ids when `selective` |
| `hard_limits` | string tags that block stimulation of that kind |
| `soft_limits` | tags that reduce stimulation / raise DC |
| `kinks` | tags that increase stimulation (handbook “inverse resistance”) |
| `inhibition` | numeric bonus used when consent ≠ willing |

**Hard rules for handlers:**

- Advances against `revoked` or hard-limit tags **fail the commit** (or apply zero stimulation and record a clear message — pick one policy and keep it consistent; prefer **fail** for hard limits so the LLM cannot “accidentally” apply them).
- Willing partners treat Inhibition as 0 unless already negative (handbook rule).
- Table-level / campaign-level: plugin presence + `EnabledModeIds` is the GM gate; character consent is the per-scene gate.

Document this in skill sidecars so the LLM does not invent consent theater that contradicts engine state (align with existing playtest “no false consent scripts” discipline: engine state is source of truth).

### 3. Prefer existing commits; add custom `$type`s only where needed

**Reuse (already on the wire):**

- `mode_transition` — enter/exit
- `resource` — arousal / numbing / soothing pool edits
- `status` — edging, afterglow, overstimulation levels, handbook conditions
- `engagement_relation` — partner contact
- `campaign_update` — enable mode

**Add plugin `$type`s (MVP):**

| Discriminator | Purpose |
|---------------|---------|
| `lewd_advance` | Direct/indirect/skilled advance: resolve hit/save, apply stimulation, respect consent/limits |
| `lewd_climax_check` | Explicit climax/edging save step when automation is deferred to the LLM |
| `lewd_soothe` | Optional convenience if `resource` alone is too easy to misuse; otherwise skip and use `resource` |

Keep the verb set small. Prefer observers that apply edging-at-max-arousal automatically after a successful `lewd_advance` / `resource` when mode is active.

### 4. Dice via active ruleset, not reimplemented 5e math

`lewd_advance` handler should:

1. Validate active `ModeEncounter.ModeId == lewd_encounter`.
2. Resolve consent/limits.
3. Call into the host-facing patterns already expected of modes: skill checks / attack rolls / saves through whatever Sdk-exposed action resolution the PluginSdk version provides (`IRulesetModule.Actions` / related DTOs). If a needed roll helper is missing from Sdk `0.1.0`, **file a small Sdk bump** rather than copying 5e formulas into the plugin.
4. Apply arousal/numbing via entity helpers on `IChangeContext` (no Raven `Session`; no `IMutationDispatcher` until a later Sdk).

### 5. Skills are sidecars only

Ship markdown under `skills/` for Claude Code / OpenCode / etc. Host may log `skillsPath` presence; it does **not** inject or serve skills. README tells operators how to install/point their client at those files.

### 6. Source fidelity

Authoritative sources live on the operator machine (not vendored into CampaignVault or the plugin git history unless licensing is settled):

| Path | Role |
|------|------|
| `~/Downloads/Lewd Handbook.pdf` | **Canonical original.** Use for tables, layout, and any rule the extracts disagree on or garble. |
| `~/Downloads/lewd-handbook.md` | **Primary working extract.** RAG-optimized / cleaned complete ruleset (arousal, advances, climax, conditions, spells, homebrew). Prefer this for day-to-day encoding into pools, `$type`s, and YAML. |
| `~/Downloads/lewd_handbook.md` | Raw OCR dump of the PDF. Noisy (doubled glyphs, broken columns). Use only as a last resort search aid; never paste it wholesale into code or skills. |

**Encode from `lewd-handbook.md`, verify against the PDF.** Keep a short internal `RULES_NOTES.md` in the plugin repo that cites page/section for arousal, advances, edging, overstimulation, and consent. Full spell/class catalog remains a content marathon — phase it. Do not commit the PDF or either markdown extract into the main CampaignVault tree.

### 7. Trust model (unchanged)

Code plugin = full-trust in-process. Separate account = content isolation + opt-in install, **not** sandboxing. Track C is out of scope.

---

## Mechanics MVP (vertical slice)

Ship a playable loop before the content catalog:

1. Enable mode on a dnd5e campaign.
2. Ensure participants have `arousal` (and optionally `numbing`) pools — via YAML templates merged into dnd5e and/or bootstrap on mode enter.
3. `mode_transition` enter with 2+ participants; set consent flags.
4. `engagement_relation` as needed.
5. `lewd_advance` (martial) → stimulation → arousal rises; numbing absorbs first.
6. At max arousal → `edging` status; climax saves via `lewd_climax_check` or observer-driven prompts.
7. Climax success/fail updates arousal / afterglow / overstimulation.
8. `mode_transition` exit with outcome narrative.

**Out of MVP:** automatic attack↔advance conversion tables, concurrent sex+combat, full subclass/spell/item catalogs, PF2e, marketplace packaging.

---

## Phased delivery

### Phase 0 — Repo bootstrap (½ day)

- Create separate-account repo + solution.
- `PackageReference` PluginSdk from nuget.org.
- Copy CraftingMode shape: `plugin.json`, empty `IInteractionMode`, stub handler, CI build.
- README: install path, enablement, consent model, “do not ship Sdk.dll”.
- Acceptance: `dotnet build` green; zip installs under `Plugins/LewdHandbook/` on a stock host; mode id appears for `mode_transition` once enabled.

### Phase 1 — Mode state machine + enter/exit (1–2 days)

- `LewdEncounterMode` + state machine: action budget, turn advance, completion when all consented exits / scene end flag.
- On enter: ensure participant scratch state (`consent`, climax success/fail counters, etc.).
- Unit tests for budget consume / turn wrap / IsComplete.
- Acceptance: Crafting-parity lifecycle with `modeId=lewd_encounter`.

### Phase 2 — Pools, conditions, consent gates (2–3 days)

- YAML: `arousal`, `numbing` pool templates; conditions `edging`, `afterglow`, `overstimulation_1`…`_6` (or one condition with a level field if that fits existing status patterns better — match host `StatusEffect` conventions).
- Handler helpers: apply stimulation (subtract numbing, clamp arousal, stamp edging).
- Consent/hard-limit enforcement on advance path.
- Tests: willing vs unwilling AC/inhibition path; hard limit fails commit; numbing absorbs; max arousal → edging.
- Acceptance: pure unit coverage of the math without needing a live MCP.

### Phase 3 — `lewd_advance` + climax loop (3–5 days)

- `[PluginWorldChange("lewd_advance")]` + handler (martial / indirect / skilled variants via a `kind` field).
- Wire rolls through Sdk action APIs; degrade gracefully with a clear failure message if roll inputs are incomplete (LLM must supply ability/skill as needed).
- `[PluginWorldChange("lewd_climax_check")]` or observer that nudges/applies death-save-style climax rules.
- Optional `IGuidanceContributor`: soft hint when scene tags / intimacy pressure suggest offering `mode_transition`.
- Harness test: deserialize `lewd_advance` through host registry (integration against a test host or documented manual script).
- Acceptance: end-to-end scripted encounter in a disposable campaign (enable → enter → advance → edge → climax → exit).

### Phase 4 — Content layer 1 (ongoing)

Prioritize YAML that unlocks play without conversion automation:

1. Conditions + pool schemas (if not finished in Phase 2).
2. A small set of implements / items (stimulation die, finesse tag).
3. A handful of feats / histories that only adjust attributes/pools.
4. One subclass or martial archetype as a proof of data-pack merge.

Defer bulk spell/item catalogs until `RULES_NOTES.md` is clean against the PDF; prefer curated slices from `lewd-handbook.md` over raw OCR.

### Phase 5 — Skill sidecars + operator docs (1–2 days)

- Skills: when to enter mode, how to set consent, which `$type`s to emit, how to read arousal/edging from scene tools, what not to invent.
- Operator README: enablement, uninstall (delete folder + remove from `EnabledModeIds`), upgrade/`minEngineVersion`.
- Acceptance: a fresh operator can install from README alone.

### Phase 6 — Hardening (as needed)

- Discriminator collision check against core + Crafting (`lewd_*` prefix).
- Version matrix: PluginSdk `0.1.x` ↔ host `0.2.x`.
- Fuzz consent revocation mid-scene.
- Performance: observers must stay cheap (`IsInterestedIn` filters to active lewd mode only).

---

## Testing strategy

| Layer | What |
|-------|------|
| Unit (plugin repo) | State machine, stimulation/numbing math, consent gates, climax counters |
| Package identity | Plugin types `is` Sdk `IInteractionMode` / `WorldChange` when loaded by host ALC |
| Host smoke | Drop into `Plugins/`, boot MCP, `get_commit_schema` lists `lewd_advance`, enable mode, run scripted `take_turn` batch |
| Regression | Removing the plugin folder leaves stock dnd5e campaigns unaffected |

Do **not** add Lewd Handbook fixtures or adult strings to the main CampaignVault test suite. Keep harnesses in the external repo; optional shared “load any plugin folder” host test already covered by CraftingMode.

---

## Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Sdk missing a roll helper | Small Sdk patch release on nuget.org; plugin bumps PackageReference |
| Extract/PDF disagreement or OCR noise | Prefer `lewd-handbook.md`; verify against `Lewd Handbook.pdf`; capture the ruling in `RULES_NOTES.md`; keep MVP surface tiny |
| LLM ignores consent state | Fail commits on hard limits; skills stress engine-as-source-of-truth |
| Accidental main-repo leakage | Separate account; CODEOWNERS/CI path filters; never vendor handbook into CampaignVault |
| Type-identity / Sdk.dll in zip | Pack script excludes Sdk.dll; README warning; host already skips+warns |
| Scope explosion (full book) | Phases 0–3 gate “playable”; content is explicitly ongoing |

---

## Out of scope

- Publishing or changing CampaignVault core / dnd5e embeds for adult content
- Forking `IRulesetModule` or expanding `RulesetActionType`
- Track C sandbox / Track D marketplace
- Host auto-injection of skill files
- Full automatic combat↔sexual conversion of arbitrary SRD features
- Simultaneous active combat + lewd mode
- PF2e / system-agnostic lewd mode
- Legal advice on distributing third-party handbook text — treat copyright/licensing as an author responsibility in the external repo README

---

## Success criteria

1. Fresh clone of the external repo builds with **only** a nuget.org `CampaignVault.PluginSdk` reference.
2. Nested folder drops into a stock CampaignVault `Plugins/` directory; host boots; Sdk.dll is absent from the drop.
3. Campaign with `ActiveSystem=dnd5e` can `campaign_update` enable `lewd_encounter`, enter mode, run advances with consent enforcement, resolve edging/climax, and exit.
4. `get_commit_schema` lists plugin discriminators; no collisions with core types.
5. Uninstalling the plugin restores a clean host with no residual required content in the main repo.
6. Main CampaignVault tree remains free of handbook mechanics, prose, and assets.

---

## Suggested first ticket

**Phase 0 + Phase 1 only:** scaffold the external repo from CraftingMode, PackageReference nuget.org Sdk, implement `lewd_encounter` enter/exit/turn machine with consent scratch fields, ship a dry-run zip, and prove enable → enter → exit on a local host before writing advance math or YAML catalogs.

---

## References

- `PLUGIN_SDK_PLAN.md` — platform Track E (assumed shipped + Sdk on nuget.org)
- `INTERACTION_MODES_PLAN.md` — Track B mode/observer design
- `PLUGINS.md` — external PluginSdk checklist, trust model, Type 3 modes
- `PLUGIN_SYSTEM_PLAN.md` — tracks overview; adult content stays out of core
- In-tree template: `plugins/CraftingMode/`
- Source material (operator machine, external to git):
  - `~/Downloads/Lewd Handbook.pdf` — canonical original
  - `~/Downloads/lewd-handbook.md` — primary cleaned/RAG working extract
  - `~/Downloads/lewd_handbook.md` — raw OCR (deprioritized)
)
