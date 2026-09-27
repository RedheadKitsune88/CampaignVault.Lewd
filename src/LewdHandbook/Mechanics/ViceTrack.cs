using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>How far a character is from their last partake.</summary>
internal enum ViceStage
{
    None = 0,

    /// <summary>Partook within half the withdrawal window.</summary>
    Sated,

    /// <summary>Past half the window: the pull starts (narration only).</summary>
    Craving,

    /// <summary>Past the window: handbook withdrawal — presence saves, long-rest saves.</summary>
    Withdrawal,

    /// <summary>Three windows or more without it (narration escalates; homebrew).</summary>
    Severe,
}

/// <summary>
/// The game loop around an addiction, on top of <see cref="ViceState"/>'s bookkeeping.
/// Handbook: presence saves while in withdrawal (failure: they try to partake by any means), a normal addiction save each
/// long rest in withdrawal (success lowers the DC; at base, one more success ends it), and aid from Lesser Restoration or
/// a healer's kit (advantage), Greater Restoration (automatic success) or Remove Curse (magical vices: DC −1 per slot
/// level above 2nd) until the next long rest.
/// Homebrew: stages re-announce the craving as it deepens, and every temptation resisted earns 1 Resolve (max 3), spent
/// as +1 each on the next long-rest save — so holding out in the fiction pays off at the table.
/// </summary>
internal static class ViceTrack
{
    public const int MaxResolve = 3;
    public const string Advantage = "advantage";
    public const string Auto = "auto";

    public static string ResolveKey(string id) => $"vice.{id}.resolve";
    public static string StreakKey(string id) => $"vice.{id}.clean_streak";
    public static string StageKey(string id) => $"vice.{id}.stage";
    public static string AidKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.aid";
    public static string CompelledKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.compelled";
    public static string WithdrawalEffect(string id) => "Withdrawal: " + ViceCatalog.Normalize(id);
    public static string CompelledEffect(string id) => "Compelled: " + ViceCatalog.Normalize(id);

    public static ViceStage Stage(Character character, ViceDef def, float nowHours)
    {
        if (!ViceState.IsAddicted(character, def.Id))
            return ViceStage.None;
        if (!character.SystemStats.Attributes.TryGetValue(ViceState.LastHoursKey(def.Id), out var last))
            return ViceStage.Sated;
        var gap = nowHours - last;
        return gap >= def.WithdrawalHours * 3 ? ViceStage.Severe
            : gap >= def.WithdrawalHours ? ViceStage.Withdrawal
            : gap >= def.WithdrawalHours / 2f ? ViceStage.Craving
            : ViceStage.Sated;
    }

    public static float HoursSince(Character character, ViceDef def, float nowHours) =>
        character.SystemStats.Attributes.TryGetValue(ViceState.LastHoursKey(def.Id), out var last) ? nowHours - last : 0f;

    /// <summary>Announces a deeper stage once (stored), so the DM hears about it without repeats.</summary>
    public static void AnnounceStage(Character character, ViceDef def, float nowHours, IChangeContext? context)
    {
        var stage = Stage(character, def, nowHours);
        var before = (ViceStage)(int)ViceState.Attr(character, StageKey(def.Id));
        ViceState.SetAttr(character, StageKey(def.Id), (int)stage);
        if (stage <= before || context is null)
            return;
        var hours = HoursSince(character, def, nowHours);
        switch (stage)
        {
            case ViceStage.Craving:
                context.RecordMessage($"{character.Id} craves {def.Id} ({hours:0}h since last); withdrawal at {def.WithdrawalHours}h.");
                break;
            case ViceStage.Severe:
                context.RecordMessage(
                    $"{character.Id} {def.Id} withdrawal is severe ({hours:0}h). Narrate it; presence of the vice still calls for lewd_vice action=note_presence.");
                break;
        }
    }

    public static int Resolve(Character character, string id) => (int)ViceState.Attr(character, ResolveKey(id));

    public static string? Aid(Character character, string id) => PregnancyState.Text(character, AidKey(id));

    public static bool IsCompelled(Character character, string id) => PregnancyState.Flag(character, CompelledKey(id));

    public static void StampWithdrawal(Character character, ViceDef def) =>
        Stamp(character, WithdrawalEffect(def.Id),
            $"In withdrawal from {def.Id}: in its presence, addiction save (disadvantage) or try to partake by any means; " +
            $"each long rest, addiction save or {def.WithdrawalFailHint}");

    public static void ClearWithdrawalEffects(Character character, string id)
    {
        Unstamp(character, WithdrawalEffect(id));
        ClearCompelled(character, id);
        RemoveThought(character, id);
    }

    public static void Compel(Character character, ViceDef def)
    {
        PregnancyState.Set(character, CompelledKey(def.Id), "true");
        Stamp(character, CompelledEffect(def.Id),
            $"Compelled to partake of {def.Id} by any means possible. Ends when they partake (lewd_vice consume) or resist later.");
    }

    public static void ClearCompelled(Character character, string id)
    {
        character.SystemStats.Traits.Remove(CompelledKey(id));
        Unstamp(character, CompelledEffect(id));
    }

    public static int GainResolve(Character character, string id)
    {
        var next = Math.Min(MaxResolve, Resolve(character, id) + 1);
        ViceState.SetAttr(character, ResolveKey(id), next);
        return next;
    }

    public static void Reset(Character character, string id)
    {
        character.SystemStats.Attributes.Remove(ResolveKey(id));
        character.SystemStats.Attributes.Remove(StreakKey(id));
        character.SystemStats.Attributes.Remove(StageKey(id));
        character.SystemStats.Traits.Remove(AidKey(id));
        ClearWithdrawalEffects(character, id);
    }

    /// <summary>
    /// A temptation: the vice is here. Rolled with disadvantage (handbook signs of addiction); aid can cancel it or make it
    /// automatic. Failure compels the character to partake; success earns Resolve and lifts a compulsion.
    /// Returns (resisted, message) or an error.
    /// </summary>
    public static async Task<(bool? Resisted, string Message)> PresenceSaveAsync(
        IChangeContext context, Character character, ViceDef def, string ability, int d20, int? abilityMod, CancellationToken ct)
    {
        var dc = ViceState.CurrentDc(character, def);
        var aid = Aid(character, def.Id);
        if (aid == Auto)
        {
            var gained = GainResolve(character, def.Id);
            ClearCompelled(character, def.Id);
            return (true, $"{character.Id} resists {def.Id} (Greater Restoration: automatic). Resolve {gained}/{MaxResolve}.");
        }

        var mod = AbilityScores.Resolve(character, ability, abilityMod);
        var disadvantage = aid != Advantage;
        var roll = await SaveDice.RollAsync(context, "lewd_vice_resist", d20, mod, disadvantage, ct).ConfigureAwait(false);
        if (roll.Error is not null)
            return (null, roll.Error);

        var how = disadvantage ? "disadvantage" : "aided: straight roll";
        if (roll.Total < dc)
        {
            Compel(character, def);
            return (false,
                $"{character.Id} {def.Id} presence save {roll.Summary} < DC {dc} ({how}): compelled to partake by any means. " +
                "Narrate it; emit lewd_vice action=consume when they do.");
        }

        var resolve = GainResolve(character, def.Id);
        ClearCompelled(character, def.Id);
        return (true, $"{character.Id} {def.Id} presence save {roll.Summary} ≥ DC {dc} ({how}): resisted. Resolve {resolve}/{MaxResolve}.");
    }

    /// <summary>
    /// The long-rest addiction save while in withdrawal: a normal roll (advantage when aided, automatic after Greater
    /// Restoration) plus every point of Resolve, which is spent. Aid ends with the rest.
    /// </summary>
    public static async Task<string> RestSaveAsync(
        IChangeContext context,
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        string ability,
        int d20,
        int? abilityMod,
        CancellationToken ct)
    {
        var dc = ViceState.CurrentDc(character, def);
        var aid = Aid(character, def.Id);
        var resolve = Resolve(character, def.Id);
        character.SystemStats.Traits.Remove(AidKey(def.Id));
        character.SystemStats.Attributes.Remove(ResolveKey(def.Id));

        bool success;
        string summary;
        var face = 0;
        if (aid == Auto)
        {
            success = true;
            summary = "automatic (Greater Restoration)";
        }
        else
        {
            var mod = AbilityScores.Resolve(character, ability, abilityMod) + resolve;
            var roll = await SaveDice.RollAsync(
                context, "lewd_vice_rest", d20, mod, disadvantage: false, ct, advantage: aid == Advantage).ConfigureAwait(false);
            if (roll.Error is not null)
                return $"{character.Id} vice {def.Id}: {roll.Error}";
            face = roll.Face;
            success = roll.Total >= dc;
            summary = $"{roll.Summary}{(resolve > 0 ? $" (incl. Resolve +{resolve})" : "")}{(aid == Advantage ? " (advantage)" : "")}";
        }

        if (!success)
        {
            ViceState.SetAttr(character, StreakKey(def.Id), 0);
            ViceState.FailWithdrawal(character, participant, def, face, context);
            return $"{character.Id} vice {def.Id} long-rest save {summary} < DC {dc}.";
        }

        var streak = (int)ViceState.Attr(character, StreakKey(def.Id)) + 1;
        ViceState.SetAttr(character, StreakKey(def.Id), streak);
        var note = $"{character.Id} vice {def.Id} long-rest save {summary} ≥ DC {dc}; clean streak {streak}.";
        if (ViceState.TryCleanOnRestSuccess(character, def, context))
            ViceState.PublishState(context, character, def, "clean");
        return note;
    }

    /// <summary>Treatment. Returns the message, or an error when the method does not fit.</summary>
    public static string? Treat(Character character, ViceDef def, string method, int slotLevel, out string message)
    {
        message = "";
        switch ((method ?? "").Trim().ToLowerInvariant().Replace(' ', '_'))
        {
            case "lesser_restoration":
            case "healers_kit":
            case "healer_kit":
            case "medicine":
                if (Aid(character, def.Id) != Auto)
                    PregnancyState.Set(character, AidKey(def.Id), Advantage);
                message = $"{character.Id} {def.Id}: advantage on addiction saves until the next long rest.";
                return null;
            case "greater_restoration":
                PregnancyState.Set(character, AidKey(def.Id), Auto);
                message = $"{character.Id} {def.Id}: addiction saves succeed automatically until the next long rest.";
                return null;
            case "remove_curse":
                if (def.Kind != "magical")
                    return $"Remove Curse only eases magical vices; {def.Id} is {def.Kind}.";
                var steps = Math.Max(0, slotLevel - 2);
                if (steps == 0)
                    return "Remove Curse lowers the DC by 1 per slot level above 2nd: pass slotLevel 4 or higher.";
                var baseDc = (int)ViceState.Attr(character, ViceState.BaseDcKey(def.Id), def.BaseDc);
                var dc = Math.Max(baseDc, ViceState.CurrentDc(character, def) - steps);
                ViceState.SetAttr(character, ViceState.DcKey(def.Id), dc);
                message = $"{character.Id} {def.Id}: addiction DC lowered to {dc}.";
                return null;
            default:
                return "method must be lesser_restoration, healers_kit, greater_restoration, or remove_curse.";
        }
    }

    private static void Stamp(Character character, string name, string hint)
    {
        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.RecoveryHint = hint;
            return;
        }

        effects.Add(new StatusEffect { Name = name, Category = "Condition", AppliedBy = ViceCatalog.AppliedBy, RecoveryHint = hint });
    }

    private static void Unstamp(Character character, string name) =>
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.AppliedBy, ViceCatalog.AppliedBy, StringComparison.Ordinal));

    private static void RemoveThought(Character character, string id)
    {
        var existing = PregnancyState.Text(character, LewdKeys.TraitIntrusiveThoughts);
        if (string.IsNullOrWhiteSpace(existing))
            return;
        var token = "vice:" + id;
        var kept = existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.Equals(p, token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (kept.Count == 0)
            PregnancyState.Remove(character, LewdKeys.TraitIntrusiveThoughts);
        else
            PregnancyState.Set(character, LewdKeys.TraitIntrusiveThoughts, string.Join(",", kept));
    }
}
