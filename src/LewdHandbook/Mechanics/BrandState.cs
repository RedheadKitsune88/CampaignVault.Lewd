using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class BrandState
{
    public static string ViceLocked => ViceState.LockedKey(ViceCatalog.SexualFluids);
    public static string ViceAddicted => ViceState.AddictedKey(ViceCatalog.SexualFluids);
    public static string ViceKind => ViceState.KindKey(ViceCatalog.SexualFluids);
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
            var tier = Math.Clamp(bits.Length > 1 && int.TryParse(bits[1], out var n) ? n : def.Tier, 1, BrandCatalog.MaxTier);
            // A brand listed twice (hand-edited Trait, old writes) counts once, at its highest tier.
            var existing = list.FindIndex(b => b.Id == id);
            if (existing >= 0)
                list[existing] = (id, Math.Max(list[existing].Tier, tier));
            else
                list.Add((id, tier));
        }

        return list;
    }

    /// <summary>Changes the tier of a brand the character already bears, keeping its source.</summary>
    public static void SetTier(Character character, string id, int tier)
    {
        if (!BrandCatalog.TryGet(id, out var def))
            return;
        var brands = Parse(PregnancyState.Text(character, LewdKeys.Lustbrands)).ToList();
        var index = brands.FindIndex(b => b.Id == id);
        if (index < 0)
            return;
        tier = Math.Clamp(tier, 1, BrandCatalog.MaxTier);
        brands[index] = (id, tier);
        PregnancyState.Set(character, LewdKeys.Lustbrands, Format(brands));
        StampBrand(character, def, tier, PregnancyState.Text(character, PayloadKey(id, "source")));
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

    /// <summary>Dice Brand of Ruin needs if it intercepts a climax, rolled up front by the async callers.</summary>
    public readonly record struct RuinDice(int RecoveryFace, int PsychicFace);

    /// <summary>Null unless the character bears Ruin. Rolls one recovery die and the fallback 1d12.</summary>
    public static async Task<RuinDice?> PreRollRuinAsync(IChangeContext context, Character? character, CancellationToken ct)
    {
        if (character is null || !Has(character, BrandCatalog.Ruin))
            return null;
        var sides = ArousalMath.RecoveryDieSides(character) ?? 8;
        var recovery = await LewdDice.RollAsync(context, "lewd_ruin_recovery_die", 1, sides, ct: ct).ConfigureAwait(false);
        var psychic = await LewdDice.RollAsync(context, "lewd_ruin_psychic", 1, 12, ct: ct).ConfigureAwait(false);
        return new RuinDice(recovery.Total, psychic.Total);
    }

    public static bool InterceptClimax(
        ModeParticipantState target,
        Character? character,
        ResourcePool arousal,
        IChangeContext? context,
        out string note,
        RuinDice? ruin = null,
        IEnumerable<string>? tags = null,
        bool forced = false)
    {
        note = "";
        if (character is null)
            return false;
        if (!forced && Has(character, BrandCatalog.Fertility) && !ClimaxUnprotected(character, tags))
        {
            LewdPoolHelper.SetEdging(target, character, true);
            note = " Brand of Fertility: only unprotected sex brings climax; still edging.";
            context?.RecordMessage($"{character.Id} Brand of Fertility: climax blocked (not unprotected sex). Tag the advance \"unprotected\" when it is.");
            return true;
        }

        if (BlocksClimax(character))
        {
            LewdPoolHelper.SetEdging(target, character, true);
            note = " Denied; climax blocked.";
            context?.RecordMessage($"{character.Id} Brand of Denial blocks climax. Climax save auto-succeeds.");
            return true;
        }

        if (!Has(character, BrandCatalog.Ruin))
            return false;

        note = ApplyRuin(target, character, arousal, context, ruin);
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

        if (Has(character, BrandCatalog.Bestial) && ClimaxUnprotected(character, tags))
        {
            ClearOwned(character, LewdKeys.ConditionNymphomanic, OwnsKey(BrandCatalog.Bestial, LewdKeys.ConditionNymphomanic));
            notes.Add(" Bestial nymphomanic cleared.");
        }

        if (TraitInt(character, LewdKeys.LustbrandPendingRebrand) > 0 && TierSum(character) == 0)
        {
            var id = PregnancyState.Text(character, LewdKeys.LustbrandRebrandId);
            var tier = TraitInt(character, LewdKeys.LustbrandPendingRebrand);
            PregnancyState.Remove(character, LewdKeys.LustbrandPendingRebrand);
            PregnancyState.Remove(character, LewdKeys.LustbrandRebrandId);
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
        var arousal = LewdPoolHelper.Arousal(character);
        var numbing = LewdPoolHelper.Numbing(character);
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

        PregnancyState.Remove(character, FlagKey(BrandCatalog.Abundance, "climaxed"));

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

    public static string AltruismBaseMaxKey => FlagKey(BrandCatalog.Altruism, "base_max");
    public static string AltruismSuppressedKey => FlagKey(BrandCatalog.Altruism, "suppressed");

    public static void OnHeal(Character character, ModeParticipantState? participant, int amount, IChangeContext? context)
    {
        if (amount <= 0 || !Has(character, BrandCatalog.Altruism))
            return;
        var pool = LewdPoolHelper.Arousal(character);
        if (!character.SystemStats.Traits.ContainsKey(AltruismBaseMaxKey))
            PregnancyState.Set(character, AltruismBaseMaxKey, (ArousalMath.DerivedMax(character) ?? pool.Max).ToString());
        var suppressed = TraitInt(character, AltruismSuppressedKey) + amount;
        PregnancyState.Set(character, AltruismSuppressedKey, suppressed.ToString());
        LewdPoolHelper.SyncArousalMax(character, pool);
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
        PregnancyState.Remove(character, InactiveKey(BrandCatalog.Denial));
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

    private const string LastStimUnprotectedKey = LewdKeys.ModeTraitPrefix + "last_stim_unprotected";

    /// <summary>Remembers whether the latest physical stimulation was unprotected sex, for a later climax save.</summary>
    public static void RecordStimulation(Character? character, IEnumerable<string>? tags)
    {
        if (character is not null)
            PregnancyState.Set(character, LastStimUnprotectedKey, Unprotected(tags) ? "true" : "false");
    }

    /// <summary>Whether this climax comes from unprotected sex: the advance's own tags, else the latest recorded stimulation.</summary>
    public static bool ClimaxUnprotected(Character? character, IEnumerable<string>? tags) =>
        tags is not null && tags.Any() ? Unprotected(tags) : PregnancyState.Flag(character, LastStimUnprotectedKey);

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
                PregnancyState.Remove(character, InactiveKey(id));
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

    /// <summary>
    /// Handbook: instead of climaxing, spend one recovery die as if it had: arousal drops by the roll + tier and the bearer
    /// takes that much psychic damage. With no dice left: 1d12 psychic and +1 overstimulation. Damage that would drop the
    /// bearer to 0 leaves them at 1 and raises the tier. HP is applied here, since the roll is the engine's.
    /// </summary>
    private static string ApplyRuin(
        ModeParticipantState target,
        Character character,
        ResourcePool arousal,
        IChangeContext? context,
        RuinDice? ruin)
    {
        var tier = Math.Max(1, Tier(character, BrandCatalog.Ruin));
        var sides = ArousalMath.RecoveryDieSides(character) ?? 8;
        string outcome;
        int damage;
        if (character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolRecoveryDice, out var dice) &&
            dice is not null && dice.Current > 0)
        {
            var face = ruin?.RecoveryFace ?? LewdDice.Average(sides);
            damage = face + tier;
            dice.Current -= 1;
            arousal.Current = Math.Max(0, arousal.Current - damage);
            LewdPoolHelper.MirrorArousal(target, arousal);
            LewdPoolHelper.SetEdging(target, character, false);
            outcome = $" Brand of Ruin: no climax; spent 1 recovery die (d{sides}={face}) + tier {tier}: arousal −{damage}, {damage} psychic.";
        }
        else
        {
            damage = ruin?.PsychicFace ?? LewdDice.Average(12);
            var level = ConsentGate.GetInt(target, LewdKeys.Overstimulation) + 1;
            LewdPoolHelper.SetOverstimulation(target, character, level, BrandCatalog.ConditionName(BrandCatalog.Ruin), context);
            LewdPoolHelper.SetEdging(target, character, false);
            outcome = $" Brand of Ruin: no recovery dice; {damage} psychic (1d12), overstimulation {level}.";
        }

        if (character.MaxHp > 0 && character.CurrentHp > 0)
        {
            if (character.CurrentHp - damage <= 0)
            {
                character.CurrentHp = 1;
                SetTier(character, BrandCatalog.Ruin, tier + 1);
                outcome += $" Would have dropped to 0 HP: left at 1 HP, Ruin tier {tier + 1}.";
            }
            else
            {
                character.CurrentHp -= damage;
            }
        }

        context?.RecordMessage($"{character.Id}{outcome}");
        Mirror(target, character);
        return outcome;
    }

    private static void RestoreArousalMax(Character character)
    {
        var baseMax = TraitInt(character, AltruismBaseMaxKey);
        character.SystemStats.Traits.Remove(AltruismBaseMaxKey);
        character.SystemStats.Traits.Remove(AltruismSuppressedKey);
        if (!character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolArousal, out var pool) || pool is null)
            return;
        if (ArousalMath.DerivedMax(character) is null)
            pool.Max = Math.Max(pool.Max, baseMax);
        LewdPoolHelper.SyncArousalMax(character, pool);
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
        PregnancyState.Remove(character, ownsKey);
    }

    private static int TraitInt(Character character, string key) => PregnancyState.Int(character, key);

    private static string PayloadKey(string id, string field) => LewdKeys.LustbrandTraitPrefix + $"{id}.{field}";

    private static string FlagKey(string id, string field) => LewdKeys.LustbrandTraitPrefix + $"{id}.{field}";

    private static string InactiveKey(string id) => LewdKeys.LustbrandTraitPrefix + $"{id}.inactive";

    private static string OwnsKey(string id, string condition) => LewdKeys.LustbrandTraitPrefix + $"{id}.owns_{condition}";
}
