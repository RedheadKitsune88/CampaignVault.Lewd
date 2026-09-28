# CampaignVault.Lewd

Plugin for the CampaignVault host (`~/RiderProjects/CampaignVault`), D&D 5e only (`plugin.json` `systems: ["dnd5e"]`).
Tests: `dotnet test tests/LewdHandbook.Tests`; host flow tests: `tests/LewdHandbook.HostTests` (need `../CampaignVault`).

## Verbs: scene-bound vs. works anywhere
Scoping a verb with `[PluginWorldChange("x", ModeId = LewdEncounterMode.ModeIdValue)]` hides it from the default
`take_turn` schema (resolvable only via `type=`). Do that ONLY for verbs that make no sense outside `lewd_encounter`.

- Scene-bound (`ModeId` set): `lewd_advance`, `lewd_bad_end`, and the `[EngineOnly]` verbs `lewd_turn_start`,
  `lewd_scene_end`, `lewd_rest`, `lewd_echo_check`.
- Work outside a scene (NO `ModeId`, must stay in the default schema): `lewd_stance` (default scope), `lewd_bind`,
  `lewd_unbind`, `lewd_escape`, `lewd_apply_brand`, `lewd_imprint`, `lewd_pregnancy`, `lewd_vice`,
  `lewd_decondition`, `lewd_recover`, `lewd_climax_check` (spells/items force climaxes with no scene), `lewd_insert`, `lewd_remove`, `lewd_cleanup`, `lewd_humiliate`.
- Their handlers use `LewdModeAccess.TryGetParticipant(...)` and must keep working when it returns null
  (fall back to the character sheet / a scratch participant). Do not add "requires an active scene" to them.
- These are not gated on `EnabledModeIds`; only `systems` limits where the plugin runs.
- Adding a verb: decide which group it is in, and update this list, README and `skills/lewd-encounter.md`.

## Invariants
- Age gate (`Mechanics/AgeGate.cs`) is unconditional: every character a lewd verb touches passes it. Not configurable.
- Consent goes through `ConsentGate.AuthorizeEffect`; `lewdNonConsent=on` is grimdark. Revoked stance, hard limits
  and the age gate apply in every setting.

## Blocking statuses need an exit
The host (SDK 0.9.0+; plugin builds on 0.12.0) refuses `[ActorAction]` verbs, and core attack/spell/item-use actions, from an actor with a hard-block
status (`incapacitated`, `stunned`, `paralyzed`, ... or `StatModifiers["BlocksAllActions"]`). Any status the plugin stamps that
way must end in AND out of a scene: give it `ExpiresAtDay` (the host stops honouring an expired block even if nothing removed it),
clear it in `EndClimaxIncapacitation` / scene end, and test "no scene, time passes, they can act again".
`[ActorAction]` verbs: `lewd_advance`, `lewd_bind`, `lewd_escape`. Exempt (unmarked) verbs: saves, `lewd_recover` (dice and the Con save), `lewd_climax_check`, and anything aimed at the blocked character.

## Bondage model
`BindingEntry.Sites` map onto body slots (`Mechanics/BondageSlots.cs`); conditions, spell-component blocks and the DM summary
line are derived from the occupied slots (`Restraint.Sync` recomputes everything from the binding list). Anchors are core
`Tether`s on `SystemExtension.Tethers` (`AttachedBy = "lewd_bind:<bindingId>"`), never `EngagementRelations`; a binding whose
tether has gone drops its anchor on the next `Sync`. Plugin handlers cannot dispatch `tether`/`apply_effect` changes, so
they write those SDK models directly.
Time in restraints: `RestraintTimeObserver` (core `IWorldTimeObserver`) adds hours to each binding and stamps one effect per
area (`restraint_aftermath.arms` / `.legs`, `AppliedBy = restraint_aftermath`, no `EffectTier`, so core's event-debuff cap
ignores them, and `Restraint.Sync` keeps them). Magnitudes stay inside host `EffectTiers` by hand; plugins cannot reference
host code. `Willpower` only records drain in `SystemExtension.WillpowerDrained` (core restores it on rest and applies weak willpower
to charm/fear/compulsion/mental saves). `LewdRollModifierProvider` (core `IRollModifierProvider`, SDK 0.12.0) turns conditions and
restraints into situational advantage/disadvantage and the hobbled speed cap; it adds no numbers, those live on status effects, so
nothing counts twice. Plugin rolls go through `SaveDice.RollAsync(..., who:, subject:, tags:)` so they see core's layers; give escape
checks the `escape` tag so the restraint being escaped does not hamper its own escape.
Slack and fit: `BindingEntry.Slack` (tight default | loose) and `Fit` (free text for the DM). The engine owns only three numbers
from slack (ankle speed cap 5 vs 15, hands pinned vs `awkward_hands`, slip DC -4); everything geometric stays in `Fit`, surfaced in
`BondageSlots.Summary`. The LLM may add effects, not silently remove slot-derived ones: a looser tie is a visible `slack` change.

## Imprint shock and mood buffs
Both are engine-owned, deterministic, tiered by hand inside host `EffectTiers`, and stamped without an `EffectTier` (host event caps ignore them).
`ImprintAftermath` (`Tick` on an unwilling track that gains a level): one effect `imprint_aftermath` (Shaken / Rattled / Broken down: Wisdom
-1/-2/-3, Intelligence 0/-1/-2, 8h/24h/24h) plus willpower drain 4/8/12; tracks share it, a lower climb never weakens it, willing imprints cost nothing.
`LewdMood` (`lewd_mood` effect): Warm glow (wanted, non-cruel `lewd_advance`, 1h), Afterglow (scene end with physical contact, not unwilling or bad-ended, 8h),
Relief (a `lewd_decondition` that lowered a level, 8h). Guards: `lewdMoodBuffs` setting (default on), one mood at a time (stronger replaces weaker),
one grant per trigger+scope per day, `DailyCap` 3 per character.

## Bad end vs rescue
`BadEndState.Apply` branches per target on `LewdSettings.AllowsUnwanted`: allowed -> the permanent mark; not allowed -> `BadEndRescue`
(`Begin` resets the trigger synchronously, `FinishAsync` from the observer or `lewd_bad_end` stamps the timed `bad_end_rescue` effect: no
`EffectTier`, no hard block, 24h, willpower -15, once per day). Sync trigger sites read settings via `LewdSettings.Peek(context)` (what a
handler already resolved for that commit); unknown means legacy permanent, never rescue. `lewd_bad_end` takes `outcome` / `keepBindings` for the rescue;
gear loss, location and time skip stay with the DM through core changes (plugin handlers cannot dispatch them).
