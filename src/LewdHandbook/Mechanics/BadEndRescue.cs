using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// What a bad end becomes when the campaign does not allow unwanted acts against this character (<c>lewdNonConsent</c> off, or
/// <c>not_against_pc</c> and the character is the PC): no permanent mark, a fade to black. The character is knocked senseless and
/// left for dead; the engine resets the scene state that caused it, stamps a fixed head-blow debuff and a willpower loss, and tells
/// the DM to place them nearby, decide who found them and what was taken (core item and location changes), and skip the time.
/// Two steps because the trigger sites are synchronous and have no clock: <see cref="Begin"/> resets state and marks the character
/// pending, <see cref="FinishAsync"/> (observer or handler) stamps the timed debuff. The trigger is cleared first, so it cannot re-fire.
/// </summary>
internal static class BadEndRescue
{
    public const string AppliedBy = "bad_end_rescue";
    public const string Key = "bad_end_rescue";
    public const float Drain = 15f;
    private const string PendingKey = LewdKeys.ModeTraitPrefix + "rescue.pending";
    private const string DayKey = LewdKeys.ModeTraitPrefix + "rescue.day";
    private const string KeepBindingsKey = LewdKeys.ModeTraitPrefix + "rescue.keep_bindings";
    private const string OutcomeKey = LewdKeys.ModeTraitPrefix + "rescue.outcome";

    public static readonly Dictionary<string, float> Modifiers = new() { ["AllChecks"] = -2, ["AllSaves"] = -2, ["AttackRoll"] = -2 };
    public const double Hours = 24;

    /// <summary>True when this character's bad end is a rescue rather than a permanent mark.</summary>
    public static bool Applies(LewdSettings settings, Character character) =>
        !settings.AllowsUnwanted(character, character.Id, out _);

    public static bool IsPending(Character? character) => PregnancyState.Text(character, PendingKey) is not null;

    /// <summary>Clears what caused the bad end and frees the character (unless the DM keeps them bound). Synchronous, no clock.</summary>
    public static void Begin(ModeParticipantState? participant, Character character, string reason, bool keepBindings = false, string? outcome = null)
    {
        if (participant is not null)
        {
            LewdPoolHelper.SetOverstimulation(participant, character, 0, null);
            LewdPoolHelper.WriteClimaxCounters(participant, 0, 0, character);
        }

        LewdPoolHelper.SetEdging(participant, character, false);
        LewdPoolHelper.EndClimaxIncapacitation(participant, character);
        var pools = character.SystemStats.ResourcePools;
        if (pools.TryGetValue(LewdKeys.PoolArousal, out var arousal) && arousal is not null)
        {
            arousal.Max = Math.Max(1, arousal.Max);
            arousal.Current = 0;
        }

        if (pools.TryGetValue(LewdKeys.PoolRecoveryDice, out var recovery) && recovery is not null && recovery.Max > 0)
            recovery.Current = recovery.Max;

        PregnancyState.Set(character, PendingKey, reason);
        PregnancyState.Set(character, KeepBindingsKey, keepBindings ? "true" : "false");
        if (!string.IsNullOrWhiteSpace(outcome))
            PregnancyState.Set(character, OutcomeKey, outcome.Trim());
    }

    /// <summary>Stamps the debuff and tells the DM, once per campaign day per character. No-op unless <see cref="Begin"/> ran.</summary>
    public static async Task FinishAsync(IChangeContext context, Character character, ModeParticipantState? participant, CancellationToken ct = default)
    {
        var reason = PregnancyState.Text(character, PendingKey);
        if (reason is null)
            return;
        PregnancyState.Remove(character, PendingKey);
        var keep = PregnancyState.Flag(character, KeepBindingsKey);
        var outcome = PregnancyState.Text(character, OutcomeKey);
        PregnancyState.Remove(character, KeepBindingsKey);
        PregnancyState.Remove(character, OutcomeKey);

        if (!keep && BindingGraph.GetBindings(participant, character).Count > 0)
        {
            BindingGraph.SetBindings(participant, character, []);
            Restraint.Sync(character, []);
        }

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        var now = await LewdClock.NowDaysAsync(context).ConfigureAwait(false) ?? day;
        var who = character.Id;
        if (PregnancyState.Text(character, DayKey) == day.ToString())
        {
            context.RecordMessage($"{who}: already rescued today; no second debuff ({reason}).");
            return;
        }

        PregnancyState.Set(character, DayKey, day.ToString());
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e => string.Equals(e.EffectKey, Key, StringComparison.OrdinalIgnoreCase));
        effects.Add(new StatusEffect
        {
            Name = "Knocked senseless",
            Category = "Injury",
            AppliedBy = AppliedBy,
            EffectKey = Key,
            StatModifiers = new Dictionary<string, float>(Modifiers),
            RecoveryHint = "Rest, or treatment (Medicine); the daze fades within the day.",
            ExpiresAtDay = (float)(now + Hours / 24.0),
        });
        var lost = Willpower.Drain(character, Drain);
        var message =
            $"{who} is knocked senseless ({reason}) and left for dead: this is a fade to black, not a permanent Bad End " +
            $"(non-consent does not allow one here). Knocked senseless: all checks, saves and attacks −2 for 24h" +
            (lost > 0 ? $"; willpower −{lost:0.#} ({character.SystemStats.Willpower:0.#} left)" : "") + ". " +
            (keep ? "They are still bound. " : "Any bindings are gone. ") +
            (outcome is null ? "" : $"DM's outcome: {outcome}. ") +
            "Now decide, in follow-up commits: skip the time (advance_world), place them nearby, say who found them and what was taken " +
            "(item and location changes), and end the scene (mode_transition exit).";
        context.RecordMessage(message);
        context.RecordPhysicalStateNudge(message);
    }
}
