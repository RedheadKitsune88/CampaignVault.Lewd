---
name: lewd-brands
description: Lustbrand apply/remove verb, per-brand StatusEffects, inhibition, glow, and trigger commits.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Lustbrands

The engine is the source of truth for `lustbrands`. Do not narrate a brand the trait list does not contain. `remove curse` does not remove one.

## Apply

```json
{ "$type": "lewd_apply_brand", "targetId": "chars/bob", "brandId": "denial", "sourceId": "chars/succubus", "payload": "permission from chars/succubus" }
```

Does not require `lewd_encounter`. Fails if consent is `revoked`, a hard limit is `lustbrand` / `brand` / the brand id, the id is unknown, or the target is unwilling under `intimacyTone=consensual`. Pass `willing: true` when they seek the brand. `fade` applies and nudges a non-graphic curse. `grimdark` applies.

`tier` overrides the catalog tier (1–5). Omitted tier uses the handbook default.

State: trait `lustbrands` = `denial:5,fertility:2`. Participant mirrors that string, `lustbrand_inhib` (sum of tiers), and `lustbrand_glow` (`mark` at arousal 0, `visible` while aroused, `bright` at max — 5 ft dim light). Each brand is a StatusEffect `Lustbrand: <Title>`, `conditionName` `lustbrand:<id>`, category Curse, `statModifiers.Inhibition` = −tier.

Inhibition bonus is reduced by the **sum** of tiers. Omit `inhibitionBonus` on `lewd_climax_check` and the engine subtracts it. If you pass `inhibitionBonus`, do not also expect a second subtraction.

## Catalog

| id | tier | Engine | You narrate |
|----|------|--------|-------------|
| `abundance` | 1 | Rest without a climax since the last rest: endowment +1. Climax: −1 and a 1 liter note | −endowment AC and Dex saves |
| `addiction` | 1 | Sets `vice.sexual_fluids` locked+addicted DC 18 and stamps `Vice: sexual_fluids`. Craving/saves/withdrawal: `lewd_vice` (`lewd-vices`) | 8 oz of sexual fluids to gain a long rest |
| `altruism` | 1 | Heal (`hp` delta > 0) lowers arousal max by that amount until a short or long rest. `action: stabilize` raises tier, max 5 | Con save vs spell DC or `hyperaroused` |
| `bestial` | 4 | Short or long rest stamps `nymphomanic` until a climax tagged `unprotected`, `repro`, or `no_contraceptive` | The rut |
| `betrayal` | 5 | Stores `payload` as the foe type | Nymphomanic within 10 ft; their climax infatuates 1d6 hours |
| `denial` | 5 | Stamps `denied`. Climax is blocked. `action: release` drops Denied until the next rest, heal, advance, or climax | The release condition in `payload` |
| `echoes` | 2 | Stim on someone else echoes as psychic stim. Self-advance cannot climax | If within 5 ft of a climax, `lewd_climax_check` `forceClimax: true`. Do not assume distance |
| `emptiness` | 1 | Status only | Larger penetration has no penalty. Disadvantage while not so penetrated |
| `false_dominance` | 1 | Status only. The spell is not granted | Demitri's Demanding Desire 1/long rest; auto-fail charm from a submissive source |
| `fertility` | 2 | Stamps `hyperfertile`. `hyperaroused` while `pregnant` | Climax only from unprotected sex. Extra pregnancies −1 Str/Dex |
| `infatuation` | 3 | Stamps `infatuated`. `sourceId` or `payload` is the source | Cannot remove the infatuation while the brand remains |
| `oaths` | 2 | `action: vow` stores `payload` | Breach: public humiliation, or nymphomanic and −1 arousal max per day |
| `ruin` | 1 | A would-be climax does not climax. Spends 1 recovery die and reduces arousal by tier. No dice: +1 overstimulation | Add the die result and apply that total as psychic damage. HP 0 becomes 1; then re-apply at tier+1 |
| `obedience` | 5 | Stamps `infatuated` by `sourceId` | Cannot willingly move more than 1 mile. Overstimulated: DC 20 Wis or dominated |
| `forsaken` | 3 | Status only | True Love contact: poisoned + 1d6 radiant per turn |
| `hungry_gaze` | 3 | Status only | Stealth disadvantage. Viewers: DC 15 Wis or sexually aggressive until climax |
| `transformation` | 5 | `action: trigger` with `d20` + `conModifier` vs DC 18. Failure stamps `nymphomanic` | Hybrid form 1 hour. Store the trigger in `payload` |

## Remove

```json
{ "$type": "lewd_apply_brand", "targetId": "chars/bob", "brandId": "denial", "action": "remove", "method": "wish" }
```

`method` must be `wish` or `feature`. Any other method, including remove curse, fails. A `status` remove of the effect is restamped while the trait remains.

`concubi: true` (or an existing concubi flag) and removal of the **last** brand stores a pending rebrand. The next real climax applies the same id at tier+1 (max 5). That is the handbook's "usually higher tier" default. To choose a different brand, apply it before that climax.

Brand of Addiction removal clears `vice.sexual_fluids.locked` only. It leaves `addicted` and DC 18. It does not roll withdrawal.

## Do not

- Invent a vice save, withdrawal, or `lewd_vice` here.
- Remove a brand with `remove curse` or `status` remove.
- Apply a brand inside the `lewd_bad_end` commit. Store `consequence=lustbrand`, then emit this verb on a later turn.
