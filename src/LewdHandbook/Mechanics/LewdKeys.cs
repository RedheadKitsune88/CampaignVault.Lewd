namespace LewdHandbook.Mechanics;

internal static class LewdKeys
{
    public const string Consent = "consent";
    public const string AllowedPartners = "allowed_partners";
    public const string HardLimits = "hard_limits";
    public const string SoftLimits = "soft_limits";
    public const string Kinks = "kinks";
    public const string Inhibition = "inhibition";
    public const string ClimaxSuccesses = "climax_successes";
    public const string ClimaxFailures = "climax_failures";
    public const string Edging = "edging";
    public const string Overstimulation = "overstimulation";
    public const string OverstimSource = "overstim_source";
    public const string ClimaxStreak = "climax_streak";
    public const string ClimaxIncapacitated = "climax_incapacitated";
    /// <summary>Turn starts left before climax incapacitation ends (0 = ends at this participant's next turn start).</summary>
    public const string ClimaxIncapTurns = "climax_incap_turns";
    public const string EdgingBeats = "edging_beats";
    public const string HadPhysical = "had_physical";
    public const string FlirtBeats = "flirt_beats";
    public const string BadEnded = "bad_ended";
    public const string BadEndReason = "bad_end_reason";
    public const string BadEndConsequence = "bad_end_consequence";
    public const string BadEndImprintTrack = "bad_end_imprint_track";
    public const string BadEndImprintJump = "bad_end_imprint_jump";
    public const string BadEndImprintOrigin = "bad_end_imprint_origin";
    public const string BadEndViceId = "bad_end_vice_id";
    public const string Lustbrands = "lustbrands";
    public const string LustbrandGlow = "lustbrand_glow";
    public const string LustbrandInhib = "lustbrand_inhib";
    public const string LustbrandConcubi = ModeTraitPrefix + "lustbrand.concubi";
    public const string LustbrandPendingRebrand = ModeTraitPrefix + "lustbrand.pending_rebrand";
    public const string LustbrandRebrandId = ModeTraitPrefix + "lustbrand.rebrand_id";
    /// <summary>Per-brand bookkeeping Traits (<c>lustbrand.&lt;id&gt;.payload</c> …) live under the mode prefix: hidden outside a scene.</summary>
    public const string LustbrandTraitPrefix = ModeTraitPrefix + "lustbrand.";
    public const string ImprintTraitPrefix = ModeTraitPrefix + "imprint.";
    public const string LustbrandJustClimaxed = "lustbrand_just_climaxed";
    public const string Pregnant = "pregnant";
    public const string PregnancyProgress = "pregnancy_progress";
    public const string PregnancyType = "pregnancy_type";
    public const string PregnancySource = "pregnancy_source";
    public const string PregnancyOffspring = "pregnancy_offspring";
    /// <summary>Retired: the rest-poison guard is the Attribute pregnancy.rest_check_hours now.</summary>
    public const string PregnancyRestPoisonDay = "pregnancy_rest_poison_day";
    public const string PregnancyDue = "pregnancy_due";
    public const string ArousalCurrentMirror = "arousal_current";
    public const string ArousalMaxMirror = "arousal_max";

    public const string Bindings = "bindings";
    public const string Occupied = "occupied";
    public const string InternalDeposits = "internal_deposits";
    public const string Posture = "posture";

    public const string SealOpen = "open";
    public const string SealPlugged = "plugged";
    public const string SealBeaded = "beaded";
    public const string OccupancyPhallic = "phallic";
    public const string OccupancyPlug = "plug";
    public const string OccupancyBeads = "beads";
    public const string OccupancyWand = "wand";
    public const string OccupancyPartner = "partner";
    /// <summary>Derived from bindings on wrists/arms: free | front | behind | above | together | crossed.</summary>
    public const string ArmPosition = "arm_position";
    /// <summary>Derived from bindings on ankles/legs: free | front | behind | apart | together | crossed | folded.</summary>
    public const string LegPosition = "leg_position";

    public const string PoolArousal = "arousal";
    public const string PoolNumbing = "numbing";
    public const string PoolRecoveryDice = "recovery_dice";

    public const string ConsentWilling = "willing";
    public const string ConsentSelective = "selective";
    public const string ConsentUnwilling = "unwilling";
    public const string ConsentRevoked = "revoked";

    public const string ConditionEdging = "edging";
    public const string ConditionOverstimulation = "overstimulation";
    public const string ConditionIntoxicated = "intoxicated";
    public const string ConditionHyperaroused = "hyperaroused";
    public const string ConditionInfatuated = "infatuated";
    public const string ConditionIncapacitated = "incapacitated";
    public const string ConditionStunned = "stunned";
    public const string ConditionParalyzed = "paralyzed";
    public const string ConditionPregnant = "pregnant";
    public const string ConditionBadEnded = "bad_ended";
    public const string ConditionHyperfertile = "hyperfertile";
    public const string ConditionInfertile = "infertile";
    public const string ConditionHypervirile = "hypervirile";
    public const string ConditionDenied = "denied";
    public const string ConditionNymphomanic = "nymphomanic";

    // Player-owned campaign options (plugin.json campaignOptions, playerOnly).
    public const string NarrationOption = "lewdNarration";
    public const string NarrationExplicit = "explicit";
    public const string NarrationSuggestive = "suggestive";
    public const string NarrationFade = "fade";
    public const string NonConsentOption = "lewdNonConsent";
    public const string NonConsentOff = "off";
    public const string NonConsentNotAgainstPc = "not_against_pc";
    public const string NonConsentOn = "on";
    public const string HardLimitsOption = "lewdHardLimits";
    public const string MoodBuffsOption = "lewdMoodBuffs";
    public const string HumiliationOption = "lewdHumiliation";
    public const string FluidsOption = "lewdFluids";
    public const string ExternalMarksOption = "lewdExternalMarks";
    public const string FluidViceHookOption = "lewdFluidViceHook";
    public const string CreampiePregnancyOption = "lewdCreampiePregnancy";
    public const string InsertedToysOption = "lewdInsertedToys";
    public const string LeaksOption = "lewdLeaks";

    public const string OptionOn = "on";
    public const string OptionOff = "off";
    public const string CreampiePregnancyPrompt = "prompt";
    public const string CreampiePregnancyAuto = "auto";

    /// <summary>Core <c>soil</c> kind for climax deposits (namespaced; Phrase shows the leaf).</summary>
    public const string DirtKindCum = "lewd.cum";
    public const string DirtKindFluids = "lewd.fluids";

    public const string FinishInside = "inside";
    public const string FinishOutside = "outside";
    public const string FinishNone = "none";

    /// <summary>Pending deposit from a <c>lewd_advance</c>: applied when this character next climaxes.</summary>
    public const string PendingFinish = "pending_finish";
    public const string PendingTargetAnatomy = "pending_target_anatomy";
    public const string PendingDepositOn = "pending_deposit_on";

    /// <summary>Mode-gated catalog/track Traits use <see cref="ModeTraitPrefix"/> so NpcCard.SystemTraits only shows them in an active lewd_encounter.</summary>
    public const string ModeTraitPrefix = "lewd_encounter.";

    public const string TraitSexualHistory = ModeTraitPrefix + "sexual_history";
    public const string TraitRecoveryDie = ModeTraitPrefix + "recovery_die";
    public const string TraitImplementProficiencies = ModeTraitPrefix + "implement_proficiencies";
    public const string TraitHyperfertile = ModeTraitPrefix + "hyperfertile";
    public const string TraitInfertile = ModeTraitPrefix + "infertile";
    public const string TraitHypervirile = ModeTraitPrefix + "hypervirile";

    // Participant State keys (mode mirrors). Their Trait twins below are hidden bookkeeping.
    public const string Imprints = "imprints";
    public const string IntrusiveThoughts = "intrusive_thoughts";
    public const string ImprintInhib = "imprint_inhib";
    public const string TraitImprints = ModeTraitPrefix + Imprints;
    public const string TraitIntrusiveThoughts = ModeTraitPrefix + IntrusiveThoughts;
    public const string TraitImprintInhib = ModeTraitPrefix + ImprintInhib;
    public const string TraitLustbrandInhib = ModeTraitPrefix + LustbrandInhib;
    public const string TraitBadEndImprintTrack = ModeTraitPrefix + BadEndImprintTrack;
    public const string TraitBadEndImprintJump = ModeTraitPrefix + BadEndImprintJump;
    public const string TraitBadEndImprintOrigin = ModeTraitPrefix + BadEndImprintOrigin;
    public const string TraitBadEndViceId = ModeTraitPrefix + BadEndViceId;

    public const string AnatomyPrefix = ModeTraitPrefix + "anatomy.";

    // Character defaults for in-fiction disposition; participant State keys of the same short name override per scene.
    public const string TraitStance = ModeTraitPrefix + "stance";
    public const string TraitAllowedPartners = ModeTraitPrefix + "allowed_partners";
    public const string TraitHardLimits = ModeTraitPrefix + "hard_limits";
    public const string TraitSoftLimits = ModeTraitPrefix + "soft_limits";
    public const string TraitKinks = ModeTraitPrefix + "kinks";
    public const string TraitInhibition = ModeTraitPrefix + "inhibition";

    /// <summary>JSON list of bindings — lives on the Character so restraints outlast the scene. Unprefixed on purpose: a captive's cuffs are visible outside a scene.</summary>
    public const string TraitBindings = Bindings;
    public const string TraitOccupied = Occupied;
    /// <summary>Mode-gated bookkeeping for internal finishes; StatusEffect <c>Filled</c> is the always-on signal.</summary>
    public const string TraitInternalDeposits = ModeTraitPrefix + InternalDeposits;
    public const string TraitPendingFinish = ModeTraitPrefix + PendingFinish;
    public const string TraitPendingTargetAnatomy = ModeTraitPrefix + PendingTargetAnatomy;
    public const string TraitPendingDepositOn = ModeTraitPrefix + PendingDepositOn;

    /// <summary>Posture a binding put the character in (kneeling, prone, all_fours_crawl…); cleared when the last binding goes. Unprefixed: visible outside a scene.</summary>
    public const string TraitPosture = Posture;

    /// <summary>Per-scene imprint ledger, resolved once per track when the encounter ends.</summary>
    public const string TraitSceneImprints = ModeTraitPrefix + "scene.imprints";

    /// <summary>Campaign day of last pain-tagged advance (ordeal climb assist).</summary>
    public const string TraitRecentPain = ModeTraitPrefix + "recent_pain";
    /// <summary>Campaign day of last <c>lewd_humiliate</c>.</summary>
    public const string TraitRecentHumiliate = ModeTraitPrefix + "recent_humiliate";
    /// <summary>Set when climax lands close to pain/shame; cleared on imprint tick / rest / scene end.</summary>
    public const string TraitPendingOrdealFromPain = ModeTraitPrefix + "pending_ordeal_from_pain";
    /// <summary>Day the ordeal-climb Message nudge already fired (one per character per day).</summary>
    public const string TraitOrdealNudgeDay = ModeTraitPrefix + "ordeal_nudge_day";

    // Legacy unprefixed / bare keys — read by dual-read helpers and rewritten by LewdTraitsUpgrader.
    public const string LegacyTraitSexualHistory = "sexual_history";
    public const string LegacyTraitRecoveryDie = "recovery_die";
    public const string LegacyTraitImplementProficiencies = "implement_proficiencies";
    public const string LegacyAnatomyPrefix = "anatomy.";
    public const string LegacyVicePrefix = "vice.";
    public const string LegacyModeTraitBindings = ModeTraitPrefix + "bindings";
    public const string LegacyModeTraitPosture = ModeTraitPrefix + "posture";

    /// <summary>
    /// Bookkeeping Traits that used to be unprefixed (so cluttered every NPC card) and now live under
    /// <see cref="ModeTraitPrefix"/>. Keyed by their bare legacy name.
    /// </summary>
    public static readonly IReadOnlyList<string> HiddenBareTraits =
    [
        Imprints, IntrusiveThoughts, ImprintInhib, LustbrandInhib,
        "hyperfertile", "infertile", "hypervirile",
        BadEndImprintTrack, BadEndImprintJump, BadEndImprintOrigin, BadEndViceId,
    ];

    /// <summary>Bare per-id prefixes (<c>lustbrand.&lt;id&gt;.*</c>, <c>imprint.&lt;id&gt;.*</c>) that moved under the mode prefix.</summary>
    public static readonly IReadOnlyList<string> HiddenBarePrefixes = ["lustbrand.", "imprint."];

    /// <summary>The pre-migration spelling of a Trait key, or null when the key never moved. Lets readers survive an un-upgraded document.</summary>
    public static string? LegacyTraitKey(string key)
    {
        if (key is Bindings or Posture)
            return ModeTraitPrefix + key;
        if (!key.StartsWith(ModeTraitPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var bare = key[ModeTraitPrefix.Length..];
        if (HiddenBareTraits.Any(h => string.Equals(h, bare, StringComparison.OrdinalIgnoreCase)) ||
            HiddenBarePrefixes.Any(p => bare.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return bare;
        return null;
    }
}
