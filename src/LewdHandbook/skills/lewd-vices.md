---
name: lewd-vices
description: Vice and addiction tracks — lewd_vice consume/resist/presence/rest, withdrawal clock, Brand of Addiction lock.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Vices & Addictions

Engine is source of truth for `vice.<id>.*` Traits/Attributes and StatusEffect `Vice: <id>` (`conditionName` `vice_<id>`). Do not invent addiction, withdrawal, or clean state that contradicts those fields. Does **not** require `lewd_encounter`.

Closed v1 catalog: `sex`, `sexual_fluids`, `alcohol`, `succubus_venom`. Other handbook samples stay YAML-extensible later.

## Verb

```json
{ "$type": "lewd_vice", "characterId": "chars/bob", "action": "consume", "viceId": "sex", "d20": 12, "ability": "cha", "inPresence": false, "itemId": "items/wine" }
```

| action | When |
|--------|------|
| `consume` | Partake. Sets `last_hours` from campaign time. Addiction save if not yet addicted. Always bumps week count and DC (+1, or +2 for alcohol after a prior addiction). `itemId` is recorded only — emit `item` / `item_use` yourself if a charge is spent. |
| `resist` | Withdrawal or `inPresence`. Save vs current DC **with disadvantage**. Failure → narrate giving in and emit `consume`. Success does not clear withdrawal. |
| `note_presence` | No roll. If addicted and withdrawing (or `inPresence`), records a temptation instruction. |
| `rest` | Long-rest withdrawal save when `Rolls` was unavailable to the observer, or when you want an explicit commit. Disadvantage + sheet ability mod. Failure applies side effects. Success lowers DC by 1 (floor `base_dc`); at base DC, clears addicted unless `locked`. |

`ability`: `con` / `wis` / `cha`. Chemical→Con, magical→Wis, psychological→Cha. Complex (`alcohol`) requires you to pass one. Omit `abilityMod` to read the sheet (`Dnd5eExtension`). Explicit `0` stays 0.

Withdrawal also syncs on `TravelChange` / any character change with `minutesElapsed` (flare only; no rest save).

## Durable fields

| Key | Where | Meaning |
|-----|--------|---------|
| `vice.<id>.kind` | Traits | chemical / magical / psychological / complex |
| `vice.<id>.dc` / `base_dc` | Attributes | current and clean-target DC |
| `vice.<id>.addicted` | Traits | `true` / `false` |
| `vice.<id>.last_hours` | Attributes | campaign hour of last partake (`TotalDaysElapsed*24 + Hour`) |
| `vice.<id>.week_start_hours` / `week_count` | Attributes | +DC week window |
| `vice.<id>.withdrawal` | Traits | derived from the hour gap; also stored for snapshots |
| `vice.<id>.locked` | Traits | Brand of Addiction (or similar) forbids clean |
| `vice.<id>.ability` | Traits | stored complex ability |
| `vice.<id>.prior_addiction` | Traits | after a clean; alcohol later partakes +2 DC |

Withdrawal when `now_hours - last_hours >= 24` (alcohol flare threshold is **4** hours). There is no background ticker — the clock is checked on rest, travel, elapsed minutes, and `lewd_vice`.

## Side effects

| Id | On addicted | Withdrawal fail |
|----|-------------|-----------------|
| `sex` | `hyperaroused`; `nymphomanic` if arousal > half max | +1 overstimulation (nat 1 = +2) |
| `sexual_fluids` | same hunger line as Brand of Addiction | same as `sex` |
| `alcohol` | 4h threshold; intoxicated if overdue | +1 exhaustion (`Exhaustion N`) |
| `succubus_venom` | disadvantage vs charmed/infatuated in `recoveryHint` | stamp `denied` |

Temptation reaches you via `RecordMessage` on the commit and the StatusEffect `recoveryHint`. Narrate the intrusive thought, then emit `resist` or `consume`. High DC (≥ base+6) or `locked` → voice is constant. Do not expect `IGuidanceContributor`.

A flare may append `vice:<id>` to trait/participant `intrusive_thoughts` (shared with imprint tokens).

## Brands and bad-end

- Brand of Addiction (`lewd_apply_brand` `addiction`) sets `sexual_fluids` locked + addicted at DC 18 and stamps `Vice: sexual_fluids`. It does not roll withdrawal. Removing the brand clears `locked` only; normal clean rules then apply.
- `lewd_bad_end` `consequence=vice` only stores `bad_end_vice_id`. First later `lewd_vice` (or the rest observer) applies addicted at base DC and clears the pending id.

## Snapshot example

```text
Traits: vice.sex.kind=psychological, vice.sex.addicted=true, vice.sex.withdrawal=true
Attributes: vice.sex.dc=10, vice.sex.base_dc=8, vice.sex.last_hours=54
StatusEffects: Vice: sex (vice_sex)
```

## Do not

- Invent a free-running craving counter.
- Clear a `locked` vice with prose or Greater Restoration fiction alone — emit `lewd_vice` / brand remove as appropriate.
- Drain levels from a vice.
- Reference host-only guidance interfaces.
