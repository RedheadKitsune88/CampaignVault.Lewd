---
name: lewd-sexual-histories
description: Eight sexual-history backgrounds — recovery dice, implement proficiency, verbal stim caps, climax gating.
metadata:
  type: skill
  plugin: com.campaignvault.lewd-handbook
---

# Sexual Histories

Sexual histories are chosen **in addition to** normal 5e backgrounds. Encode on the character:

```text
Traits["sexual_history"] = "modest_lover"
Traits["recovery_die"]   = "d8"
```

Pool `recovery_dice` uses that die face. Arousal max ≈ Recovery Dice progression + Con mod (handbook).

## When to use

- Character create / level-up history change.
- Capping verbal / non-contact stimulation.
- Deciding implement proficiency and unfamiliar-stim options.

## The eight histories

| Id | Recovery die | Natural implements | Artificial proficiency | Notes |
|----|--------------|--------------------|------------------------|-------|
| `devoted_partner` | d8 | Proficient | Up to 4 | Up to 2 fetishes (excl. Anonymist / Wanton Whore / Swinger); seduction vs them at disadv while partner/token present |
| `erotic_professional` | d12 | Proficient | Up to 8 | Up to 3 fetishes; always add proficiency to seduction checks |
| `experienced_kinkster` | d10 | Proficient | Up to 8 | 4–6 fetishes, benefit from 3 at a time (swap on long rest); 10 min observe shared fetishes |
| `modest_lover` | d8 | Proficient | Up to 4 | 1 fetish; 1/long rest reroll advance or save vs advance |
| `promiscuous` | d8 | Proficient | Up to 6 | Up to 3 fetishes; adv on Insight for sexual prefs |
| `strictly_vanilla` | d6 | Reproductive natural only | Up to 2 | No fetishes; disadv Insight for prefs; may subtract Inhibition from unfamiliar stim |
| `virgin` | d6 | **Not** proficient (pleasuring others) | 1 artificial | Optional fetish Chaste Onlooker / Edge Puppet / Voyeur; unfamiliar stim − Inhibition; Insight instead of AC/save vs subtle seduction (fail → unrecognized, immune) |
| `willingly_celibate` | d6 | Not proficient | None | Optional Chaste Onlooker / Edge Puppet; adv vs hyperaroused; seduction vs them at disadv |

Curated feat YAML stubs exist for a subset (`virgin`, `modest_lover`, `experienced_kinkster`, `erotic_professional`, `willingly_celibate`); still set Traits even when using feats.

### Seed example

```json
{
  "$type": "character_update",
  "characterId": "chars/bob",
  "traits": {
    "sexual_history": "virgin",
    "recovery_die": "d6",
    "implement_proficiencies": "hand_mirror"
  }
}
```

## Verbal / non-contact stim caps

Apply to **purely verbal or non-contact** Skilled/Indirect advances (flirting, dirty talk, distant magic tease without touch). Check **target** history:

| Target history | Cap |
|----------------|-----|
| `virgin`, `strictly_vanilla`, `modest_lover` (and similarly inexperienced) | Max **+1–2** arousal per exchange. **No full stim dice** unless physical contact, or ≥3 consecutive high-flirt buildup beats, or actual touch. |
| `experienced_kinkster`, `promiscuous`, `erotic_professional`, and higher-experience bands | Full dice as normal. |
| `devoted_partner` | Treat as modest-tier for strangers; full dice with dedicated partners (table call). |
| `willingly_celibate` | Use modest-tier caps; seduction already at disadv. |

Commit the **capped** `stimulationAmount`, not the uncapped roll.

```json
{
  "$type": "lewd_advance",
  "actorId": "chars/alice",
  "targetId": "chars/bob",
  "kind": "skilled",
  "stimulationAmount": 2,
  "stimulationType": "psychic",
  "tags": ["verbal"],
  "notes": "target sexual_history=virgin; verbal cap +2"
}
```

## Verbal climax rule

For targets with **Modest Lover or lower** experience (`modest_lover`, `strictly_vanilla`, `virgin`, `willingly_celibate`):

- Purely verbal advances **cannot** trigger a full climax until the character has completed **at least one prior physical lewd scene**.
- Verbal may build toward **edging**, but climax needs a physical stim source **or** four consecutive high-intensity flirt/physical beats without semantic repetition.
- Do not set `forceClimax` from words alone for these histories.

Kinkster+ / promiscuous / erotic professional: normal climax rules apply (still prefer physical for first-time scenes if Traits say virgin — virgin overrides).

## Recovery dice spend

On climax or short rest: spend ≤ proficiency bonus Recovery Dice; each die + Con mod lowers arousal. Long rest: regain spent Recovery Dice; arousal reduced by **half of maximum** (not “fill to max”).

## History changes

May change on level-up using sexual XP bands (handbook). New Recovery Die faces apply to **future** arousal-max gains only. Update `Traits["sexual_history"]` and `Traits["recovery_die"]` together.
