---
name: lewd-implements-anatomy
description: Artificial implements (Items) vs natural anatomy Traits; lewd_advance stim resolution order; sexual_history Traits.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Implements & Anatomy

How to source stimulation dice and tags for `lewd_advance`. Prefer structured Item / Trait data over inventing dice in prose.

## When to use

- Resolving martial (or skilled) stim amount before commit.
- Creating/updating characters with natural anatomy or artificial toys.
- Choosing finesse (Dex) vs Strength for stim ability bonus.

## Two sources

| Kind | Storage | Shape |
|------|---------|--------|
| **Artificial implement** | `Item` / ItemDefinition | `Properties`: `damageDice`, `damageType`, `implementTags` (or `tags`), `finesse` |
| **Natural anatomy** | `SystemStats.Traits` (SystemExtension.Traits) | Keys `anatomy.*` with `die=…;tags=…;size=…;finesse=…` |

Also store:

| Trait key | Example value |
|-----------|---------------|
| `sexual_history` | `modest_lover` |
| `recovery_die` | `d8` |
| `implement_proficiencies` | `dildo,wand,rope` (comma list) |

## Anatomy trait format

```text
Traits["anatomy.cock"]  = "die=1d8;tags=phallic,natural;size=medium;finesse=false"
Traits["anatomy.pussy"] = "die=1d6;tags=orifice,natural;size=medium"
Traits["anatomy.mouth"] = "die=1d4;tags=orifice,oral,natural;finesse=true"
Traits["anatomy.ass"]   = "die=1d6;tags=orifice,natural;size=medium"
```

Bare tokens without `=` become tags. Common keys: `anatomy.cock`, `anatomy.pussy`, `anatomy.mouth`, `anatomy.ass`, plus campaign-specific (`anatomy.tentacle`, etc.).

Seed on character create / update:

```json
{
  "$type": "character_update",
  "characterId": "chars/alice",
  "traits": {
    "sexual_history": "experienced_kinkster",
    "recovery_die": "d10",
    "anatomy.cock": "die=1d8;tags=phallic,natural;size=medium"
  }
}
```

## Artificial item Properties

```json
{
  "name": "finesse_implement",
  "properties": {
    "damageDice": "1d6",
    "damageType": "piercing",
    "implementTags": ["artificial", "finesse", "phallic"],
    "finesse": true
  }
}
```

Aliases the resolver accepts: `damage` / `stimulationDice` / `die` for dice; `stimulationType` for type; `tags` if `implementTags` absent. Finesse if `finesse=true` or tag `finesse`.

Curated stub: RulesetData `items/finesse_implement.yaml`.

## Resolution order (`lewd_advance`)

Resolve stim source in this order (do not skip to invent dice):

1. **Held / equipped Item** on the actor with implement-like Properties (`damageDice` etc.).
2. **`implementId`** → live Item by id/name, else ItemDefinition / template name.
3. **Traits `anatomy.*`** — explicit anatomy key if given, else first listed anatomy trait.
4. **Explicit commit fields** — `stimulationDice` / resolved `stimulationAmount` (and optional type) on the advance.

Then: roll (or take max if edging / `maximizeStimulation`), add Str (or Dex if finesse), apply verbal caps from `lewd-sexual-histories`, then commit `stimulationAmount`.

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "martial",
  "implementId": "items/toy_wand",
  "stimulationAmount": 8,
  "stimulationType": "thunder",
  "tags": ["artificial", "vibrating"],
  "hit": true
}
```

Natural anatomy example (no item):

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "martial",
  "anatomyKey": "anatomy.cock",
  "stimulationAmount": 11,
  "stimulationType": "piercing",
  "tags": ["phallic", "natural"],
  "hit": true
}
```

## Proficiency & unfamiliar stim

- Check `implement_proficiencies` / sexual history for natural vs artificial proficiency when narrating advantage/disadvantage on the **advance roll** (LLM-side until a dedicated roll field exists).
- Virgin / Strictly Vanilla: may subtract Inhibition from unfamiliar stim types (tentacles, elemental) — apply before commit amount if using that optional rule.
- Edging targets: successful advances deal **maximum** die result (`maximizeStimulation: true` or pre-max the amount).

## Do not

- Narrate a cock/toy die that is absent from Traits / Items.
- Bypass hard-limit tags carried on the implement (`implementTags` feed the consent probe).
- Confuse HP weapon damage with stimulation — same Property names, different pool (`arousal`).

## Host ItemDefinition workflow

1. Browse templates: MCP `get_rules_reference` with `kind: "items"` (optional `itemNameQuery` / `itemCategory` / `itemTag`, e.g. tag `lewd`).
2. Spawn a live instance with `world_build` `items[]` and `definitionName` (e.g. `"finesse_implement"`) — host copies category/tags/properties/equipZones/equipLayer once; explicit fields on the same entry override.
3. Equip/hold so `HolderId` points at the actor; `lewd_advance` resolves stim from live `Item.Properties` (`damage` / `damageDice`, `damageType`, `implementTags`, `finesse`).

Plugin YAML under `RulesetData/dnd5e/items/` uses the host ItemDefinition schema (`category` enum + nested `properties`). Bondage gear keeps `lewdCategory` / `implies` / `sites` inside `properties` for `lewd_bind` seeding.
