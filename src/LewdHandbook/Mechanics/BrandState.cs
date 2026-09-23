using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class BrandState
{
    public const string ViceLocked = "vice.sexual_fluids.locked";
    public const string ViceAddicted = "vice.sexual_fluids.addicted";
    public const string ViceKind = "vice.sexual_fluids.kind";
    public const string ViceDc = "vice.sexual_fluids.dc";
    public const string ViceBaseDc = "vice.sexual_fluids.base_dc";
    public const int AddictionDc = 18;
    public const int TransformationDc = 18;

    public static IReadOnlyList<(string Id, int Tier)> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var list = new List<(string Id, int Tier)>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (bits.Length == 0 || string.IsNullOrWhiteSpace(bits[0]))
                continue;
            var id = bits[0].ToLowerInvariant();
            if (!BrandCatalog.TryGet(id, out var def))
                continue;
            var tier = bits.Length > 1 && int.TryParse(bits[1], out var n) ? n : def.Tier;
            list.Add((id, Math.Clamp(tier, 1, BrandCatalog.MaxTier)));
        }

        return list;
    }

    public static string Format(IEnumerable<(string Id, int Tier)> brands) =>
        string.Join(",", brands
            .OrderBy(b => b.Id, StringComparer.Ordinal)
            .Select(b => $"{b.Id}:{b.Tier}"));

    public static bool Has(Character? character, string id) =>
        Parse(PregnancyState.Text(character, LewdKeys.Lustbrands)).Any(b => b.Id == id);

    public static int Tier(Character? character, string id) =>
        Parse(PregnancyState.Text(character, LewdKeys.Lustbrands))
            .Where(b => b.Id == id)
            .Select(b => b.Tier)
            .FirstOrDefault();

    public static int TierSum(Character? character) =>
        Parse(PregnancyState.Text(character, LewdKeys.Lustbrands)).Sum(b => b.Tier);

    public static void Apply(
        ModeParticipantState? participant,
        Character character,
        string id,
        int tier,
        string? sourceId,
        string? payload,
        bool concubi,
        IChangeContext? context)
    {
        if (!BrandCatalog.TryGet(id, out var def))
            return;

        tier = Math.Clamp(tier, 1, BrandCatalog.MaxTier);
        var brands = Parse(PregnancyState.Text(character, LewdKeys.Lustbrands)).ToList();
        var index = brands.FindIndex(b => b.Id == id);
        if (index >= 0)
            brands[index] = (id, tier);
        else
            brands.Add((id, tier));

        PregnancyState.Set(character, LewdKeys.Lustbrands, Format(brands));
        if (!string.IsNullOrWhiteSpace(sourceId))
            PregnancyState.Set(character, PayloadKey(id, "source"), sourceId.Trim());
        if (!string.IsNullOrWhiteSpace(payload))
            PregnancyState.Set(character, PayloadKey(id, "payload"), payload.Trim());
        if (concubi)
            PregnancyState.Set(character, LewdKeys.LustbrandConcubi, "true");

        StampBrand(character, def, tier, sourceId);
        StampLinked(character, id, sourceId, context);
        Mirror(participant, character);
        context?.RecordMessage(ApplyMessage(character, def, tier));
    }

    public static bool Remove(
        ModeParticipantState? participant,
        Character character,
        string id,
        bool concubi,
        IChangeContext? context)
    {
        var brands = Parse(PregnancyState.Text(character, LewdKeys.Lustbrands)).ToList();
        var index = brands.FindIndex(b => b.Id == id);
        if (index < 0)
            return false;
        var existing = brands[index];
        brands.RemoveAt(index);
        if (brands.Count == 0)
            character.SystemStats.Traits.Remove(LewdKeys.Lustbrands);
        else
            PregnancyState.Set(character, LewdKeys.Lustbrands, Format(brands));

        RemoveEffect(character, id);
        ClearLinked(character, id);
        if (id == BrandCatalog.Addiction)
            ViceState.ClearLock(character, ViceCatalog.SexualFluids);

        var marked = PregnancyState.Flag(character, LewdKeys.LustbrandConcubi) || concubi;
        if (concubi)
            PregnancyState.Set(character, LewdKeys.LustbrandConcubi, "true");
        if (marked && brands.Count == 0)
        {
            var next = Math.Min(BrandCatalog.MaxTier, existing.Tier + 1);
            PregnancyState.Set(character, LewdKeys.LustbrandPendingRebrand, next.ToString());
            PregnancyState.Set(character, LewdKeys.LustbrandRebrandId, id);
            context?.RecordMessage(
                $"{character.Id} concubi brand removed. Next climax applies {id} at tier {next}. remove curse did not do this.");
        }
        else
        {
            context?.RecordMessage($"{character.Id} lustbrand {id} removed by wish or feature. remove curse does not remove lustbrands.");
        }

        Mirror(participant, character);
        return true;
    }

    public static void Mirror(ModeParticipantState? participant, Character character)
    {
        if (participant is null)
            return;
        participant.State[LewdKeys.Lustbrands] = PregnancyState.Text(character, LewdKeys.Lustbrands) ?? "";
        participant.State[LewdKeys.LustbrandInhib] = TierSum(character);
        participant.State[LewdKeys.LustbrandGlow] = Glow(character);
        character.SystemStats.Traits[LewdKeys.LustbrandGlow] = Glow(character);
    }

    public static string Glow(Character character)
    {
        if (TierSum(character) <= 0)
            return "";
        if (!character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolArousal, out var pool) || pool is null || pool.Current <= 0)
            return "mark";
        if (pool.Max > 0 && pool.Current >= pool.Max)
            return "bright";
        return "visible";
    }

    public static bool BlocksClimax(Character character) =>
        Has(character, BrandCatalog.Denial) && !PregnancyState.Flag(character, InactiveKey(BrandCatalog.Denial));

    public static bool InterceptClimax(
        ModeParticipantState target,
        Character? character,
        ResourcePool arousal,
        IChangeContext? context,
        out string note)
    {
        note = "";
        if (character is null)
            return false;
        if (BlocksClimax(character))
        {
            LewdPoolHelper.SetEdging(target, character, true);
            note = " Denied; climax blocked.";
            context?.RecordMessage($"{character.Id} Brand of Denial blocks climax. Climax save auto-succeeds.");
            return true;
        }

        if (!Has(character, BrandCatalog.Ruin))
            return false;

        note = ApplyRuin(target, character, arousal, context);
        return true;
    }

    public static string OnClimax(
        ModeParticipantState target,
        Character character,
        IEnumerable<string>? tags,
        IChangeContext? context)
    {
        var notes = new List<string>();
        if (Has(character, BrandCatalog.Abundance))
        {
            PregnancyState.Set(character, FlagKey(BrandCatalog.Abundance, "climaxed"), "true");
            var endowment = TraitInt(character, FlagKey(BrandCatalog.Abundance, "endowment"));
            if (endowment > 0)
            {
                endowment--;
                PregnancyState.Set(character, FlagKey(BrandCatalog.Abundance, "endowment"), endowment.ToString());
            }

            notes.Add($" Abundance climax: 1 liter, endowment {endowment}.");
        }

        if (Has(character, BrandCatalog.Bestial) && Unprotected(tags))
        {
            ClearOwned(character, LewdKeys.ConditionNymphomanic, OwnsKey(BrandCatalog.Bestial, LewdKeys.ConditionNymphomanic));
            notes.Add(" Bestial nymphomanic cleared.");
        }

        if (TraitInt(character, LewdKeys.LustbrandPendingRebrand) > 0 && TierSum(character) == 0)
        {
            var id = PregnancyState.Text(character, LewdKeys.LustbrandRebrandId);
            var tier = TraitInt(character, LewdKeys.LustbrandPendingRebrand);
            character.SystemStats.Traits.Remove(LewdKeys.LustbrandPendingRebrand);
            character.SystemStats.Traits.Remove(LewdKeys.LustbrandRebrandId);
            if (id is not null && BrandCatalog.TryGet(id, out _))
            {
                Apply(target, character, id, tier, "lewd_rebrand", null, concubi: true, context);
                notes.Add($" Concubi rebrand {id}:{tier}.");
            }
        }

        target.State[LewdKeys.LustbrandJustClimaxed] = true;
        Mirror(target, character);
        return string.Concat(notes);
    }

    public static void EchoStim(ModeParticipantState participant, Character character, int amount, IChangeContext? context)
    {
        if (amount <= 0 || !Has(character, BrandCatalog.Echoes))
            return;
        var arousal = LewdPoolHelper.EnsurePool(character, LewdKeys.PoolArousal, 10, RecoveryType.Never);
        var numbing = LewdPoolHelper.EnsurePool(character, LewdKeys.PoolNumbing, 0, RecoveryType.Never);
        var result = StimulationMath.Apply(arousal.Current, Math.Max(1, arousal.Max), numbing.Current, amount, false);
        numbing.Current = result.NumbingAfter;
        arousal.Current = result.ArousalAfter;
        LewdPoolHelper.MirrorArousal(participant, arousal);
        if (arousal.Current >= arousal.Max)
            LewdPoolHelper.SetEdging(participant, character, true);
        Mirror(participant, character);
        context?.RecordMessage(
            $"{character.Id} Brand of Echoes: +{amount} psychic stim (arousal {result.ArousalAfter}/{arousal.Max}). Cannot self-climax.");
    }

    public static void OnRest(Character character, ModeParticipantState? participant, bool longRest, IChangeContext? context)
    {
        if (Has(character, BrandCatalog.Abundance) && !PregnancyState.Flag(character, FlagKey(BrandCatalog.Abundance, "climaxed")))
        {
            var endowment = TraitInt(character, FlagKey(BrandCatalog.Abundance, "endowment")) + 1;
            PregnancyState.Set(character, FlagKey(BrandCatalog.Abundance, "endowment"), endowment.ToString());
            context?.RecordMessage(
                $"{character.Id} Brand of Abundance: endowment {endowment} (−{endowment} AC and Dex saves). Narrate the swell.");
        }

        character.SystemStats.Traits.Remove(FlagKey(BrandCatalog.Abundance, "climaxed"));

        if (Has(character, BrandCatalog.Bestial))
        {
            StampOwned(character, LewdKeys.ConditionNymphomanic, LewdKeys.ConditionNymphomanic,
                "Brand of Bestial Instinct: until unprotected climax.", OwnsKey(BrandCatalog.Bestial, LewdKeys.ConditionNymphomanic));
            context?.RecordMessage($"{character.Id} Brand of Bestial Instinct: nymphomanic until unprotected climax.");
        }

        if (Has(character, BrandCatalog.Altruism))
            RestoreArousalMax(character);

        if (longRest && Has(character, BrandCatalog.Addiction))
        {
            context?.RecordMessage(
                $"{character.Id} Brand of Addiction: long-rest benefits require 8 oz of sexual fluids. Craving, saves, and withdrawal are not rolled.");
        }

        RelockDenial(character, context);
        Mirror(participant, character);
    }

    public static void OnHeal(Character character, ModeParticipantState? participant, int amount, IChangeContext? context)
    {
        if (amount <= 0 || !Has(character, BrandCatalog.Altruism))
            return;
        var pool = LewdPoolHelper.EnsurePool(character, LewdKeys.PoolArousal, 10, RecoveryType.Never);
        if (!character.SystemStats.Traits.ContainsKey(FlagKey(BrandCatalog.Altruism, "base_max")))
            PregnancyState.Set(character, FlagKey(BrandCatalog.Altruism, "base_max"), pool.Max.ToString());
        var suppressed = TraitInt(character, FlagKey(BrandCatalog.Altruism, "suppressed")) + amount;
        PregnancyState.Set(character, FlagKey(BrandCatalog.Altruism, "suppressed"), suppressed.ToString());
        var baseMax = TraitInt(character, FlagKey(BrandCatalog.Altruism, "base_max"));
        pool.Max = Math.Max(1, baseMax - suppressed);
        if (pool.Current > pool.Max)
            pool.Current = pool.Max;
        if (participant is not null)
            LewdPoolHelper.MirrorArousal(participant, pool);
        context?.RecordMessage(
            $"{character.Id} Brand of Altruism: arousal max reduced by {amount} to {pool.Max} until a short rest. Con save vs spell DC or stamp hyperaroused until the end of the next turn. The engine did not roll it.");
        Mirror(participant, character);
    }

    public static void Restamp(Character character, string? removedStatus, IChangeContext? context)
    {
        if (string.IsNullOrWhiteSpace(removedStatus))
            return;
        var brands = Parse(PregnancyState.Text(character, LewdKeys.Lustbrands));
        foreach (var brand in brands)
        {
            if (!BrandCatalog.TryGet(brand.Id, out var def))
                continue;
            if (!StatusMatches(removedStatus, def))
                continue;
            StampBrand(character, def, brand.Tier, PregnancyState.Text(character, PayloadKey(brand.Id, "source")));
            context?.RecordMessage(
                $"{character.Id} lustbrand {brand.Id} cannot be removed by status remove or remove curse. Restamped.");
        }

        if (Has(character, BrandCatalog.Denial) &&
            !PregnancyState.Flag(character, InactiveKey(BrandCatalog.Denial)) &&
            string.Equals(removedStatus, LewdKeys.ConditionDenied, StringComparison.OrdinalIgnoreCase))
        {
            StampOwned(character, LewdKeys.ConditionDenied, LewdKeys.ConditionDenied,
                "Brand of Denial. Cannot be removed while the brand is active.", OwnsKey(BrandCatalog.Denial, LewdKeys.ConditionDenied));
            context?.RecordMessage($"{character.Id} Denied restamped; Brand of Denial is still active.");
        }
    }

    public static void RelockDenial(Character character, IChangeContext? context)
    {
        if (!Has(character, BrandCatalog.Denial) || !PregnancyState.Flag(character, InactiveKey(BrandCatalog.Denial)))
            return;
        character.SystemStats.Traits.Remove(InactiveKey(BrandCatalog.Denial));
        StampOwned(character, LewdKeys.ConditionDenied, LewdKeys.ConditionDenied,
            "Brand of Denial. Cannot be removed while the brand is active.", OwnsKey(BrandCatalog.Denial, LewdKeys.ConditionDenied));
        context?.RecordMessage($"{character.Id} Brand of Denial relocked.");
    }

    public static void ReleaseDenial(Character character, IChangeContext? context)
    {
        PregnancyState.Set(character, InactiveKey(BrandCatalog.Denial), "true");
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.ConditionName, LewdKeys.ConditionDenied, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, LewdKeys.ConditionDenied, StringComparison.OrdinalIgnoreCase));
        context?.RecordMessage(
            $"{character.Id} Brand of Denial inactive until the next rest, heal, advance, or climax commit.");
    }

    public static bool Unprotected(IEnumerable<string>? tags)
    {
        if (tags is null)
            return false;
        foreach (var tag in tags)
        {
            if (string.Equals(tag, "unprotected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "repro", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "no_contraceptive", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool StatusMatches(string status, BrandDef def) =>
        string.Equals(status, BrandCatalog.ConditionName(def.Id), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, BrandCatalog.EffectName(def), StringComparison.OrdinalIgnoreCase) ||
        status.Contains(def.Id, StringComparison.OrdinalIgnoreCase) &&
        status.Contains("lustbrand", StringComparison.OrdinalIgnoreCase);

    private static string ApplyMessage(Character character, BrandDef def, int tier)
    {
        var glow = Glow(character);
        var extra = def.Id switch
        {
            BrandCatalog.Addiction => " vice.sexual_fluids locked and addicted at DC 18. Use lewd_vice for craving/saves/withdrawal.",
            BrandCatalog.Fertility => " hyperfertile stamped.",
            BrandCatalog.Denial => " denied stamped.",
            _ => "",
        };
        return $"{character.Id} lustbrand {def.Id} tier {tier}. Inhibition −{TierSum(character)}. Glow {glow}.{extra} remove curse does not remove this.";
    }

    private static void StampBrand(Character character, BrandDef def, int tier, string? sourceId)
    {
        var effects = character.SystemStats.StatusEffects;
        var condition = BrandCatalog.ConditionName(def.Id);
        var name = BrandCatalog.EffectName(def);
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new StatusEffect
            {
                Name = name,
                Category = "Curse",
                ConditionName = condition,
            };
            effects.Add(existing);
        }

        existing.Name = name;
        existing.ConditionName = condition;
        existing.Category = "Curse";
        existing.AppliedBy = string.IsNullOrWhiteSpace(sourceId) ? BrandCatalog.AppliedBy : sourceId;
        existing.StatModifiers["Inhibition"] = -tier;
        existing.RecoveryHint =
            $"Tier {tier}. {def.Hint} remove curse does not remove this. lewd_apply_brand action=remove method=wish|feature.";
    }

    private static void StampLinked(Character character, string id, string? sourceId, IChangeContext? context)
    {
        switch (id)
        {
            case BrandCatalog.Denial:
                StampOwned(character, LewdKeys.ConditionDenied, LewdKeys.ConditionDenied,
                    "Brand of Denial. Cannot be removed while the brand is active.", OwnsKey(id, LewdKeys.ConditionDenied));
                break;
            case BrandCatalog.Fertility:
                StampOwned(character, LewdKeys.ConditionHyperfertile, LewdKeys.ConditionHyperfertile,
                    "Brand of Fertility.", OwnsKey(id, LewdKeys.ConditionHyperfertile));
                PregnancyState.Set(character, LewdKeys.TraitHyperfertile, "true");
                if (PregnancyState.Flag(character, LewdKeys.Pregnant))
                    StampFertileHeat(character);
                break;
            case BrandCatalog.Infatuation:
            case BrandCatalog.Obedience:
                StampOwned(character, LewdKeys.ConditionInfatuated, LewdKeys.ConditionInfatuated,
                    "Lustbrand infatuation. Cannot be removed while the brand remains.", OwnsKey(id, LewdKeys.ConditionInfatuated));
                var inf = character.SystemStats.StatusEffects.FirstOrDefault(e =>
                    string.Equals(e.ConditionName, LewdKeys.ConditionInfatuated, StringComparison.OrdinalIgnoreCase));
                if (inf is not null && !string.IsNullOrWhiteSpace(sourceId))
                    inf.AppliedBy = sourceId;
                break;
            case BrandCatalog.Addiction:
                ViceState.LockSexualFluids(character, context);
                break;
        }
    }

    public static void StampFertileHeat(Character character)
    {
        if (!Has(character, BrandCatalog.Fertility) || !PregnancyState.Flag(character, LewdKeys.Pregnant))
            return;
        StampOwned(character, LewdKeys.ConditionHyperaroused, LewdKeys.ConditionHyperaroused,
            "Brand of Fertility while pregnant.", OwnsKey(BrandCatalog.Fertility, LewdKeys.ConditionHyperaroused));
    }

    public static void ClearFertileHeat(Character character)
    {
        if (PregnancyState.Flag(character, LewdKeys.Pregnant))
            return;
        ClearOwned(character, LewdKeys.ConditionHyperaroused, OwnsKey(BrandCatalog.Fertility, LewdKeys.ConditionHyperaroused));
    }

    private static void ClearLinked(Character character, string id)
    {
        switch (id)
        {
            case BrandCatalog.Denial:
                character.SystemStats.Traits.Remove(InactiveKey(id));
                ClearOwned(character, LewdKeys.ConditionDenied, OwnsKey(id, LewdKeys.ConditionDenied));
                break;
            case BrandCatalog.Fertility:
                ClearOwned(character, LewdKeys.ConditionHyperfertile, OwnsKey(id, LewdKeys.ConditionHyperfertile));
                ClearOwned(character, LewdKeys.ConditionHyperaroused, OwnsKey(id, LewdKeys.ConditionHyperaroused));
                break;
            case BrandCatalog.Infatuation:
            case BrandCatalog.Obedience:
                if (!Has(character, BrandCatalog.Infatuation) && !Has(character, BrandCatalog.Obedience))
                    ClearOwned(character, LewdKeys.ConditionInfatuated, OwnsKey(id, LewdKeys.ConditionInfatuated));
                break;
            case BrandCatalog.Bestial:
                ClearOwned(character, LewdKeys.ConditionNymphomanic, OwnsKey(id, LewdKeys.ConditionNymphomanic));
                break;
        }
    }

    private static string ApplyRuin(
        ModeParticipantState target,
        Character character,
        ResourcePool arousal,
        IChangeContext? context)
    {
        var tier = Math.Max(1, Tier(character, BrandCatalog.Ruin));
        if (character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolRecoveryDice, out var dice) &&
            dice is not null && dice.Current > 0)
        {
            dice.Current -= 1;
            arousal.Current = Math.Max(0, arousal.Current - tier);
            LewdPoolHelper.MirrorArousal(target, arousal);
            LewdPoolHelper.SetEdging(target, character, false);
            context?.RecordMessage(
                $"{character.Id} Brand of Ruin: no climax. Spent 1 recovery die. Arousal reduced by tier {tier}. Also subtract the recovery die and apply that total as psychic damage.");
            Mirror(target, character);
            return $" Brand of Ruin: spent 1 recovery die, arousal −{tier}, no climax.";
        }

        var level = ConsentGate.GetInt(target, LewdKeys.Overstimulation) + 1;
        LewdPoolHelper.SetOverstimulation(target, character, level, BrandCatalog.ConditionName(BrandCatalog.Ruin), context);
        LewdPoolHelper.SetEdging(target, character, false);
        context?.RecordMessage(
            $"{character.Id} Brand of Ruin: no recovery dice. Narrate 1d12 psychic. If that would drop HP to 0, set HP to 1 and emit lewd_apply_brand to raise ruin tier. Overstimulation {level}.");
        Mirror(target, character);
        return $" Brand of Ruin: no dice, overstimulation {level}, no climax.";
    }

    private static void RestoreArousalMax(Character character)
    {
        var key = FlagKey(BrandCatalog.Altruism, "base_max");
        if (!character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolArousal, out var pool) || pool is null)
        {
            character.SystemStats.Traits.Remove(key);
            character.SystemStats.Traits.Remove(FlagKey(BrandCatalog.Altruism, "suppressed"));
            return;
        }

        if (int.TryParse(PregnancyState.Text(character, key), out var baseMax))
            pool.Max = Math.Max(pool.Max, baseMax);
        character.SystemStats.Traits.Remove(key);
        character.SystemStats.Traits.Remove(FlagKey(BrandCatalog.Altruism, "suppressed"));
    }

    private static void RemoveEffect(Character character, string id)
    {
        if (!BrandCatalog.TryGet(id, out var def))
            return;
        var condition = BrandCatalog.ConditionName(id);
        var name = BrandCatalog.EffectName(def);
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void StampOwned(Character character, string name, string condition, string hint, string ownsKey)
    {
        if (PregnancyState.HasCondition(character, condition) ||
            character.SystemStats.StatusEffects.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            if (!PregnancyState.Flag(character, ownsKey) &&
                character.SystemStats.StatusEffects.Any(e =>
                    string.Equals(e.AppliedBy, BrandCatalog.AppliedBy, StringComparison.Ordinal) &&
                    (string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))))
                PregnancyState.Set(character, ownsKey, "true");
            return;
        }

        character.SystemStats.StatusEffects.Add(new StatusEffect
        {
            Name = name,
            Category = "Condition",
            ConditionName = condition,
            AppliedBy = BrandCatalog.AppliedBy,
            RecoveryHint = hint,
        });
        PregnancyState.Set(character, ownsKey, "true");
    }

    private static void ClearOwned(Character character, string condition, string ownsKey)
    {
        if (!PregnancyState.Flag(character, ownsKey))
            return;
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.AppliedBy, BrandCatalog.AppliedBy, StringComparison.Ordinal) &&
            (string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Name, condition, StringComparison.OrdinalIgnoreCase)));
        character.SystemStats.Traits.Remove(ownsKey);
    }

    private static int TraitInt(Character character, string key) => PregnancyState.Int(character, key);

    private static string PayloadKey(string id, string field) => $"lustbrand.{id}.{field}";

    private static string FlagKey(string id, string field) => $"lustbrand.{id}.{field}";

    private static string InactiveKey(string id) => $"lustbrand.{id}.inactive";

    private static string OwnsKey(string id, string condition) => $"lustbrand.{id}.owns_{condition}";
}
