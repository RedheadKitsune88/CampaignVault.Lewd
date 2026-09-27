using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class ViceState
{
    // Mode-gated Traits (NpcCard.SystemTraits). Attributes stay on vice.<id>.* (numeric sheet state).
    public static string KindKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.kind";
    public static string AddictedKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.addicted";
    public static string WithdrawalKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.withdrawal";
    public static string LockedKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.locked";
    public static string AbilityKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.ability";
    public static string PriorKey(string id) => LewdKeys.ModeTraitPrefix + $"vice.{id}.prior_addiction";
    public static string LegacyKindKey(string id) => $"vice.{id}.kind";
    public static string LegacyAddictedKey(string id) => $"vice.{id}.addicted";
    public static string LegacyWithdrawalKey(string id) => $"vice.{id}.withdrawal";
    public static string LegacyLockedKey(string id) => $"vice.{id}.locked";
    public static string LegacyAbilityKey(string id) => $"vice.{id}.ability";
    public static string LegacyPriorKey(string id) => $"vice.{id}.prior_addiction";
    public static string DcKey(string id) => $"vice.{id}.dc";
    public static string BaseDcKey(string id) => $"vice.{id}.base_dc";
    public static string LastHoursKey(string id) => $"vice.{id}.last_hours";
    public static string WeekStartKey(string id) => $"vice.{id}.week_start_hours";
    public static string WeekCountKey(string id) => $"vice.{id}.week_count";

    public static readonly string[] TraitSuffixes =
        ["kind", "addicted", "withdrawal", "locked", "ability", "prior_addiction"];

    public static float HoursNow(CampaignTime time) => time.TotalDaysElapsed * 24f + time.Hour;

    public static async Task<float> HoursNowAsync(IChangeContext context, CancellationToken ct)
    {
        var time = await context.GetCurrentTimeAsync().ConfigureAwait(false);
        return HoursNow(time);
    }

    public static bool IsAddicted(Character character, string id) =>
        PregnancyState.Flag(character, AddictedKey(id)) ||
        PregnancyState.Flag(character, LegacyAddictedKey(id));

    public static bool IsLocked(Character character, string id) =>
        PregnancyState.Flag(character, LockedKey(id)) ||
        PregnancyState.Flag(character, LegacyLockedKey(id));

    public static bool IsWithdrawal(Character character, string id) =>
        PregnancyState.Flag(character, WithdrawalKey(id)) ||
        PregnancyState.Flag(character, LegacyWithdrawalKey(id));

    public static float Attr(Character character, string key, float fallback = 0f) =>
        character.SystemStats.Attributes.TryGetValue(key, out var v) ? v : fallback;

    public static void SetAttr(Character character, string key, float value) =>
        character.SystemStats.Attributes[key] = value;

    public static int CurrentDc(Character character, ViceDef def)
    {
        var dc = (int)Attr(character, DcKey(def.Id), def.BaseDc);
        return Math.Max(def.BaseDc, dc);
    }

    public static void LockSexualFluids(Character character, IChangeContext? context)
    {
        if (!ViceCatalog.TryGet(ViceCatalog.SexualFluids, out var def))
            return;
        EnsureSeed(character, def, locked: true, addicted: true, dc: BrandState.AddictionDc);
        Stamp(character, def);
        PublishState(context, character, def, "addicted");
        context?.RecordMessage(
            $"{character.Id} sexual_fluids vice locked and addicted at DC {BrandState.AddictionDc}. No withdrawal roll from the brand.");
    }

    public static void ClearLock(Character character, string id)
    {
        character.SystemStats.Traits.Remove(LockedKey(id));
        character.SystemStats.Traits.Remove(LegacyLockedKey(id));
    }

    public static void RemoveTraitPair(Character character, string currentKey, string legacyKey)
    {
        character.SystemStats.Traits.Remove(currentKey);
        character.SystemStats.Traits.Remove(legacyKey);
    }

    public static void ApplyPendingBadEnd(Character character, IChangeContext? context)
    {
        var pending = PregnancyState.Text(character, LewdKeys.TraitBadEndViceId);
        if (string.IsNullOrWhiteSpace(pending) || !ViceCatalog.TryGet(pending, out var def))
            return;
        EnsureSeed(character, def, locked: false, addicted: true, dc: def.BaseDc);
        Stamp(character, def);
        ApplyAddictedSideEffects(character, null, def, context);
        PregnancyState.Remove(character, LewdKeys.TraitBadEndViceId);
        PublishState(context, character, def, "addicted");
        context?.RecordMessage(
            $"{character.Id} bad-end vice jump applied: {def.Id} addicted at DC {def.BaseDc}. Pending id cleared.");
    }

    public static void EnsureSeed(Character character, ViceDef def, bool locked, bool addicted, int dc)
    {
        PregnancyState.Set(character, KindKey(def.Id), def.Kind);
        PregnancyState.Set(character, AbilityKey(def.Id), def.DefaultAbility);
        SetAttr(character, BaseDcKey(def.Id), def.BaseDc);
        SetAttr(character, DcKey(def.Id), Math.Max(def.BaseDc, dc));
        if (addicted)
            PregnancyState.Set(character, AddictedKey(def.Id), "true");

        if (locked)
        {
            PregnancyState.Set(character, LockedKey(def.Id), "true");
            character.SystemStats.Traits.Remove(LegacyLockedKey(def.Id));
        }
        else
            ClearLock(character, def.Id);

        // Drop legacy trait copies once current keys are written.
        character.SystemStats.Traits.Remove(LegacyKindKey(def.Id));
        character.SystemStats.Traits.Remove(LegacyAbilityKey(def.Id));
        if (addicted)
            character.SystemStats.Traits.Remove(LegacyAddictedKey(def.Id));
    }

    public static void SyncWithdrawal(Character character, ViceDef def, float nowHours, IChangeContext? context)
    {
        if (!IsAddicted(character, def.Id))
        {
            ClearWithdrawal(character, def.Id);
            return;
        }

        if (!character.SystemStats.Attributes.ContainsKey(LastHoursKey(def.Id)))
        {
            // Addicted without ever partaking here (brand, bad end): the clock starts now.
            SetAttr(character, LastHoursKey(def.Id), nowHours);
            return;
        }

        var last = Attr(character, LastHoursKey(def.Id), nowHours);
        var gap = nowHours - last;
        ViceTrack.AnnounceStage(character, def, nowHours, context);
        if (gap >= def.WithdrawalHours)
        {
            if (!IsWithdrawal(character, def.Id))
            {
                PregnancyState.Set(character, WithdrawalKey(def.Id), "true");
                AppendThought(character, def.Id);
                ViceTrack.StampWithdrawal(character, def);
                var voice = CurrentDc(character, def) >= def.BaseDc + 6 || IsLocked(character, def.Id)
                    ? "constant"
                    : "intrusive";
                context?.RecordMessage(
                    $"{character.Id} vice {def.Id} withdrawal ({voice} temptation). Narrate the intrusive thought; when the vice is at hand, " +
                    "emit lewd_vice action=note_presence (a save), or consume if they give in.");
            }

            if (def.Id == ViceCatalog.Alcohol && gap >= 4)
                StampOwned(character, LewdKeys.ConditionIntoxicated, LewdKeys.ConditionIntoxicated,
                    "Alcohol vice: more than 4 hours since last drink.", "vice.alcohol");
        }
        else
        {
            ClearWithdrawal(character, def.Id);
            if (def.Id == ViceCatalog.Alcohol)
                RemoveOwned(character, LewdKeys.ConditionIntoxicated, "vice.alcohol");
        }
    }

    public static void Consume(
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        float nowHours,
        string ability,
        bool becameAddicted,
        IChangeContext? context,
        string? itemId)
    {
        EnsureSeed(character, def, IsLocked(character, def.Id), IsAddicted(character, def.Id) || becameAddicted, CurrentDc(character, def));
        PregnancyState.Set(character, AbilityKey(def.Id), ability);
        SetAttr(character, LastHoursKey(def.Id), nowHours);
        ClearWithdrawal(character, def.Id);
        SetAttr(character, ViceTrack.StreakKey(def.Id), 0);
        SetAttr(character, ViceTrack.StageKey(def.Id), (int)ViceStage.Sated);
        if (def.Id == ViceCatalog.Alcohol)
            RemoveOwned(character, LewdKeys.ConditionIntoxicated, "vice.alcohol");
        BumpWeek(character, def, nowHours);
        if (becameAddicted || IsAddicted(character, def.Id))
        {
            PregnancyState.Set(character, AddictedKey(def.Id), "true");
            Stamp(character, def);
            ApplyAddictedSideEffects(character, participant, def, context);
        }

        var itemNote = string.IsNullOrWhiteSpace(itemId) ? "" : $" itemId={itemId.Trim()}";
        context?.RecordMessage(
            $"{character.Id} vice {def.Id} consume. last_hours={nowHours:0.##} DC={CurrentDc(character, def)}.{itemNote}");
    }

    public static void BumpWeek(Character character, ViceDef def, float nowHours)
    {
        var start = Attr(character, WeekStartKey(def.Id), -1f);
        var count = Attr(character, WeekCountKey(def.Id), 0f);
        if (start < 0 || nowHours - start >= ViceCatalog.WeekHours)
        {
            start = nowHours;
            count = 0;
        }

        count += 1;
        SetAttr(character, WeekStartKey(def.Id), start);
        SetAttr(character, WeekCountKey(def.Id), count);

        var step = def.Id == ViceCatalog.Alcohol && PregnancyState.Flag(character, PriorKey(def.Id)) ? 2 : 1;
        var dc = Math.Max(def.BaseDc, (int)Attr(character, DcKey(def.Id), def.BaseDc)) + step;
        SetAttr(character, DcKey(def.Id), dc);
    }

    public static bool TryCleanOnRestSuccess(Character character, ViceDef def, IChangeContext? context)
    {
        var dc = CurrentDc(character, def);
        var baseDc = (int)Attr(character, BaseDcKey(def.Id), def.BaseDc);
        if (dc > baseDc)
        {
            SetAttr(character, DcKey(def.Id), dc - 1);
            context?.RecordMessage($"{character.Id} vice {def.Id} DC lowered to {dc - 1}.");
            return false;
        }

        if (IsLocked(character, def.Id))
        {
            context?.RecordMessage($"{character.Id} vice {def.Id} clean save ignored while locked.");
            return false;
        }

        ClearAddicted(character, def);
        context?.RecordMessage($"{character.Id} vice {def.Id} addiction ended.");
        return true;
    }

    public static void ClearAddicted(Character character, ViceDef def)
    {
        PregnancyState.Set(character, PriorKey(def.Id), "true");
        character.SystemStats.Traits.Remove(LegacyPriorKey(def.Id));
        RemoveTraitPair(character, AddictedKey(def.Id), LegacyAddictedKey(def.Id));
        ClearWithdrawal(character, def.Id);
        ViceTrack.Reset(character, def.Id);
        SetAttr(character, DcKey(def.Id), def.BaseDc);
        RemoveEffect(character, def.Id);
        if (def.Id is ViceCatalog.Sex or ViceCatalog.SexualFluids)
        {
            // Leave hyperaroused/nymphomanic if other sources own them.
        }

        if (def.Id == ViceCatalog.SuccubusVenom)
            RemoveOwned(character, LewdKeys.ConditionDenied, "vice.succubus_venom");
        if (def.Id == ViceCatalog.Alcohol)
            RemoveOwned(character, LewdKeys.ConditionIntoxicated, "vice.alcohol");
    }

    public static void ClearWithdrawal(Character character, string id)
    {
        RemoveTraitPair(character, WithdrawalKey(id), LegacyWithdrawalKey(id));
        ViceTrack.ClearWithdrawalEffects(character, id);
    }

    public static void FailWithdrawal(
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        int d20,
        IChangeContext? context)
    {
        var nat1 = d20 == 1;
        switch (def.Id)
        {
            case ViceCatalog.Sex:
            case ViceCatalog.SexualFluids:
                BumpOverstim(participant, character, nat1 ? 2 : 1, "lewd_vice_withdrawal", context);
                break;
            case ViceCatalog.Alcohol:
                AddExhaustion(character, nat1 ? 2 : 1);
                break;
            case ViceCatalog.SuccubusVenom:
                StampOwned(character, LewdKeys.ConditionDenied, LewdKeys.ConditionDenied,
                    "Succubus venom withdrawal.", "vice.succubus_venom");
                break;
        }

        context?.RecordMessage(
            $"{character.Id} vice {def.Id} withdrawal save failed (d20={d20}). {def.WithdrawalFailHint}");
    }

    public static void ApplyAddictedSideEffects(
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        IChangeContext? context)
    {
        switch (def.Id)
        {
            case ViceCatalog.Sex:
            case ViceCatalog.SexualFluids:
                StampOwned(character, LewdKeys.ConditionHyperaroused, LewdKeys.ConditionHyperaroused,
                    def.AddictedHint, "vice." + def.Id);
                if (ArousalAboveHalf(character, participant))
                {
                    StampOwned(character, LewdKeys.ConditionNymphomanic, LewdKeys.ConditionNymphomanic,
                        "Arousal above half maximum while sex/sexual_fluids addicted.", "vice." + def.Id);
                }

                break;
            case ViceCatalog.Alcohol:
                // 4-hour threshold handled in SyncWithdrawal.
                break;
            case ViceCatalog.SuccubusVenom:
                // Disadvantage lives in recoveryHint.
                break;
        }

        _ = context;
    }

    public static void Stamp(Character character, ViceDef def)
    {
        var effects = character.SystemStats.StatusEffects;
        var condition = ViceCatalog.ConditionName(def.Id);
        var name = ViceCatalog.EffectName(def.Id);
        var hint = def.AddictedHint + " Narrate temptation when withdrawal or in presence.";
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name;
            existing.ConditionName = condition;
            existing.RecoveryHint = hint;
            existing.AppliedBy = ViceCatalog.AppliedBy;
            return;
        }

        effects.Add(new StatusEffect
        {
            Name = name,
            Category = "Condition",
            ConditionName = condition,
            AppliedBy = ViceCatalog.AppliedBy,
            RecoveryHint = hint,
        });
    }

    public static void Restamp(Character character, string? removedStatus)
    {
        foreach (var def in ViceCatalog.All)
        {
            if (!IsAddicted(character, def.Id))
                continue;
            if (!StatusMatches(removedStatus, def.Id))
                continue;
            Stamp(character, def);
        }
    }

    public static void AppendThought(Character character, string id)
    {
        var token = "vice:" + id;
        var existing = PregnancyState.Text(character, LewdKeys.TraitIntrusiveThoughts);
        if (string.IsNullOrWhiteSpace(existing))
        {
            PregnancyState.Set(character, LewdKeys.TraitIntrusiveThoughts, token);
            return;
        }

        var parts = existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (parts.Any(p => string.Equals(p, token, StringComparison.OrdinalIgnoreCase)))
            return;
        parts.Add(token);
        PregnancyState.Set(character, LewdKeys.TraitIntrusiveThoughts, string.Join(",", parts));
    }

    public static void BumpOverstim(
        ModeParticipantState? participant,
        Character character,
        int delta,
        string source,
        IChangeContext? context)
    {
        var current = participant is not null
            ? ConsentGate.GetInt(participant, LewdKeys.Overstimulation)
            : OverstimLevel(character);
        var next = Math.Clamp(current + delta, 0, OverstimMath.MaxLevel);
        if (participant is not null)
        {
            LewdPoolHelper.SetOverstimulation(participant, character, next, source, context);
            return;
        }

        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, LewdKeys.ConditionOverstimulation, StringComparison.OrdinalIgnoreCase) ||
            (e.Name?.StartsWith("Overstimulation", StringComparison.OrdinalIgnoreCase) ?? false));
        if (next <= 0)
        {
            if (existing is not null)
                effects.Remove(existing);
            return;
        }

        var name = $"Overstimulation {next}";
        if (existing is null)
        {
            effects.Add(new StatusEffect
            {
                Name = name,
                Category = "Condition",
                ConditionName = LewdKeys.ConditionOverstimulation,
                AppliedBy = source,
                RecoveryHint = "Long rest reduces 1 level if no sexual stimulation while resting. Level 6 is Bad-Ended.",
            });
        }
        else
        {
            existing.Name = name;
            existing.ConditionName = LewdKeys.ConditionOverstimulation;
            existing.AppliedBy = source;
        }
    }

    public static void AddExhaustion(Character character, int delta)
    {
        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, "exhaustion", StringComparison.OrdinalIgnoreCase) ||
            (e.Name?.StartsWith("Exhaustion", StringComparison.OrdinalIgnoreCase) ?? false));
        var level = 0;
        if (existing?.Name is { } name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var n))
                level = n;
        }

        level = Math.Clamp(level + delta, 1, 6);
        var label = $"Exhaustion {level}";
        if (existing is null)
        {
            effects.Add(new StatusEffect
            {
                Name = label,
                Category = "Condition",
                ConditionName = "exhaustion",
                AppliedBy = ViceCatalog.AppliedBy,
                RecoveryHint = "Long rest reduces exhaustion. Host parses Exhaustion N.",
            });
        }
        else
        {
            existing.Name = label;
            existing.ConditionName = "exhaustion";
        }
    }

    private static int OverstimLevel(Character character)
    {
        var existing = character.SystemStats.StatusEffects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, LewdKeys.ConditionOverstimulation, StringComparison.OrdinalIgnoreCase) ||
            (e.Name?.StartsWith("Overstimulation", StringComparison.OrdinalIgnoreCase) ?? false));
        if (existing?.Name is null)
            return 0;
        var parts = existing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[^1], out var n) ? n : 0;
    }

    private static bool ArousalAboveHalf(Character character, ModeParticipantState? participant)
    {
        if (character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolArousal, out var pool) && pool.Max > 0)
            return pool.Current > pool.Max / 2f;
        if (participant is not null)
        {
            var cur = ConsentGate.GetInt(participant, LewdKeys.ArousalCurrentMirror);
            var max = ConsentGate.GetInt(participant, LewdKeys.ArousalMaxMirror);
            return max > 0 && cur > max / 2;
        }

        return false;
    }

    private static void RemoveEffect(Character character, string id)
    {
        var condition = ViceCatalog.ConditionName(id);
        var name = ViceCatalog.EffectName(id);
        var effects = character.SystemStats.StatusEffects;
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var e = effects[i];
            if (string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                effects.RemoveAt(i);
        }
    }

    /// <summary>
    /// Stamps a condition this vice owns. If the character already has it from another source (a brand, overstimulation),
    /// that one is left alone and not claimed, so ending the vice never strips a condition something else still applies.
    /// </summary>
    private static void StampOwned(Character character, string name, string condition, string hint, string owner)
    {
        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (string.Equals(existing.AppliedBy, owner, StringComparison.OrdinalIgnoreCase))
                existing.RecoveryHint = hint;
            return;
        }

        effects.Add(new StatusEffect
        {
            Name = name,
            Category = "Condition",
            ConditionName = condition,
            AppliedBy = owner,
            RecoveryHint = hint,
        });
    }

    /// <summary>Completed rest (from lewd_rest): syncs every vice's clock; a long rest rolls the withdrawal saves.</summary>
    public static async Task OnRestAsync(
        Character character,
        ModeParticipantState? participant,
        bool longRest,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        foreach (var def in ViceCatalog.All)
        {
            if (!IsAddicted(character, def.Id) &&
                string.IsNullOrWhiteSpace(PregnancyState.Text(character, LewdKeys.TraitBadEndViceId)))
                continue;

            SyncWithdrawal(character, def, now, context);
            if (!longRest || !IsWithdrawal(character, def.Id))
                continue;

            if (context.Rolls is null && ViceTrack.Aid(character, def.Id) != ViceTrack.Auto)
            {
                context.RecordMessage(
                    $"{character.Id} vice {def.Id} long rest withdrawal save pending. Emit lewd_vice action=rest with d20 (Rolls unavailable).");
                continue;
            }

            var ability = PregnancyState.Text(character, AbilityKey(def.Id));
            if (string.IsNullOrWhiteSpace(ability))
                ability = def.DefaultAbility;
            context.RecordMessage(await ViceTrack.RestSaveAsync(context, character, participant, def, ability, 0, null, ct).ConfigureAwait(false));
        }

        // Aid lasts "until the next long rest", used or not.
        if (longRest)
        {
            foreach (var def in ViceCatalog.All)
                character.SystemStats.Traits.Remove(ViceTrack.AidKey(def.Id));
        }
    }

    public static void PublishState(IChangeContext? context, Character character, ViceDef def, string state) =>
        context?.Publish(
            Events.LewdEvents.ViceState,
            new { characterId = character.Id, viceId = def.Id, state, dc = state == "clean" ? (int?)null : CurrentDc(character, def) });

    private static void RemoveOwned(Character character, string condition, string owner)
    {
        var effects = character.SystemStats.StatusEffects;
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var e = effects[i];
            if (string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.AppliedBy, owner, StringComparison.OrdinalIgnoreCase))
                effects.RemoveAt(i);
        }
    }

    private static bool StatusMatches(string? removed, string id)
    {
        if (string.IsNullOrWhiteSpace(removed))
            return false;
        return removed.Contains(ViceCatalog.ConditionName(id), StringComparison.OrdinalIgnoreCase) ||
               removed.Contains(ViceCatalog.EffectName(id), StringComparison.OrdinalIgnoreCase) ||
               removed.Contains("vice:" + id, StringComparison.OrdinalIgnoreCase) ||
               removed.Contains("vice_" + id, StringComparison.OrdinalIgnoreCase);
    }
}
