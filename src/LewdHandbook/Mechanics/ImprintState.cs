using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <param name="AnchorId">Who the conditioning is tied to (a captor, a trusted partner); null = anyone.</param>
internal readonly record struct ImprintTrack(string Id, int Points, int Level, string Origin, int LastDay, string? AnchorId = null);

internal static class ImprintState
{
    public const string EffectPrefix = "Imprint: ";

    public static IReadOnlyList<ImprintTrack> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        var list = new List<ImprintTrack>();
        foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':');
            if (bits.Length < 4 || !ImprintMath.IsTrack(bits[0]))
                continue;
            var points = int.TryParse(bits[1], out var p) ? p : 0;
            var origin = bits[3] == "willing" ? "willing" : "unwilling";
            var day = bits.Length > 4 && int.TryParse(bits[4], out var d) ? d : 0;
            var anchor = bits.Length > 5 ? string.Join(':', bits[5..]).Trim() : null;
            if (points <= 0)
                continue;
            list.Add(new ImprintTrack(
                ImprintMath.Normalize(bits[0]), points, ImprintMath.LevelFor(points), origin, day,
                string.IsNullOrWhiteSpace(anchor) ? null : anchor));
        }

        return list;
    }

    public static string Format(IEnumerable<ImprintTrack> tracks) =>
        string.Join("|", tracks
            .Where(t => t.Points > 0)
            .Select(t => $"{t.Id}:{t.Points}:{ImprintMath.LevelFor(t.Points)}:{t.Origin}:{t.LastDay}{(t.AnchorId is null ? "" : ":" + t.AnchorId)}"));

    /// <summary>
    /// Inhibition lost to unwilling imprints against <paramref name="actorId"/>: an imprint tied to someone only weighs
    /// against that someone (learned helplessness toward the captor, not toward everyone); an untied one against all.
    /// </summary>
    public static int InhibitionPenalty(Character? character, string? actorId) =>
        Parse(PregnancyState.Text(character, LewdKeys.TraitImprints))
            .Where(t => t.Origin != "willing" &&
                        (t.AnchorId is null || string.Equals(t.AnchorId, actorId, StringComparison.OrdinalIgnoreCase)))
            .Sum(t => t.Level);

    public static bool HasAny(Character? character) =>
        Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)).Count > 0;

    public static ImprintTrack? Find(Character? character, string id)
    {
        var key = ImprintMath.Normalize(id);
        foreach (var track in Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)))
        {
            if (track.Id == key)
                return track;
        }

        return null;
    }

    public static int Level(Character? character, string id) => Find(character, id)?.Level ?? 0;

    public static bool BrandLocked(Character character, string id) => ImprintMath.Normalize(id) switch
    {
        "breeding" => BrandState.Has(character, BrandCatalog.Fertility),
        "training" => BrandState.Has(character, BrandCatalog.Obedience),
        _ => false,
    };

    public static bool HardBlocked(
        ModeParticipantState? participant,
        Character? character,
        LewdSettings settings,
        string track,
        IEnumerable<string>? tags)
    {
        var hard = LewdProfile.HardLimits(participant, character).Concat(settings.HardLimits).ToList();
        if (hard.Count == 0)
            return false;
        var probe = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { track, "imprint" };
        if (tags is not null)
            probe.UnionWith(tags.Where(t => !string.IsNullOrWhiteSpace(t)));
        return hard.Any(probe.Contains);
    }

    public static int SufferingBonus(Character? actor, IEnumerable<string>? tags, int targetOverstim) =>
        Level(actor, "cruelty") >= 3 && ImprintMath.IsSuffering(tags, targetOverstim) ? 1 : 0;

    public static void ApplyPendingJump(
        ModeParticipantState? participant,
        Character character,
        int day,
        IChangeContext? context) =>
        ConsumeJump(participant, character, day, context);

    public static void ConsumeJump(ModeParticipantState? participant, Character character, int day, IChangeContext? context)
    {
        var track = PregnancyState.Text(character, LewdKeys.TraitBadEndImprintTrack);
        if (string.IsNullOrWhiteSpace(track) || !ImprintMath.IsTrack(track))
            return;
        var jump = PregnancyState.Int(character, LewdKeys.TraitBadEndImprintJump);
        if (jump < 1)
            jump = 3;
        jump = Math.Clamp(jump, 1, 3);
        var origin = PregnancyState.Text(character, LewdKeys.TraitBadEndImprintOrigin) == "willing" ? "willing" : "unwilling";
        var jumped = ImprintMath.Normalize(track);
        Write(participant, character, jumped, ImprintMath.PointsFor(jump), origin, day, forceOrigin: true, context);
        SetExposed(character, jumped);
        ClearJump(participant, character);
        context?.RecordMessage(
            $"{character.Id} bad-end imprint jump applied: {track} level {jump} ({origin}). Pending jump cleared. This is not a level drain and not a vice.");
    }

    public static void Tick(
        ModeParticipantState? participant,
        Character character,
        string id,
        bool willing,
        int delta,
        int day,
        bool convert,
        IChangeContext? context,
        string? anchorId = null)
    {
        ConsumeJump(participant, character, day, context);
        id = ImprintMath.Normalize(id);
        var existing = Find(character, id);
        var origin = convert || (existing is null && willing)
            ? "willing"
            : existing?.Origin ?? (willing ? "willing" : "unwilling");
        if (existing is not null && !convert)
            origin = existing.Value.Origin;
        var points = (existing?.Points ?? 0) + Math.Clamp(delta, 1, 3);
        Write(participant, character, id, points, origin, day, forceOrigin: true, context, anchorId ?? existing?.AnchorId);
        SetExposed(character, id);
        context?.RecordMessage(
            $"{character.Id} imprint {id} +{Math.Clamp(delta, 1, 3)} → {points} (level {ImprintMath.LevelFor(points)}, {origin}).");
    }

    /// <summary>Sets a track to exactly <paramref name="level"/> (backstory), keeping any higher existing points.</summary>
    public static void Seed(
        ModeParticipantState? participant,
        Character character,
        string id,
        int level,
        bool willing,
        int day,
        IChangeContext? context,
        string? anchorId = null)
    {
        id = ImprintMath.Normalize(id);
        var points = Math.Max(ImprintMath.PointsFor(Math.Clamp(level, 1, 3)), Find(character, id)?.Points ?? 0);
        var origin = willing ? "willing" : "unwilling";
        Write(participant, character, id, points, origin, day, forceOrigin: true, context, anchorId ?? Find(character, id)?.AnchorId);
        context?.RecordMessage($"{character.Id} imprint {id} seeded at level {ImprintMath.LevelFor(points)} ({origin}).");
    }

    public static bool Accept(ModeParticipantState? participant, Character character, string id, int day, IChangeContext? context)
    {
        ConsumeJump(participant, character, day, context);
        var existing = Find(character, id);
        if (existing is null)
            return false;
        Write(participant, character, existing.Value.Id, existing.Value.Points, "willing", existing.Value.LastDay, forceOrigin: true, context, existing.Value.AnchorId);
        context?.RecordMessage($"{character.Id} accepted imprint {existing.Value.Id}. Origin is willing. Buffs apply; unwilling debuffs drop.");
        return true;
    }

    public static string? Decondition(
        ModeParticipantState? participant,
        Character character,
        string id,
        string method,
        int die,
        int wisMod,
        int day,
        out string error)
    {
        error = "";
        id = ImprintMath.Normalize(id);
        var existing = Find(character, id);
        if (existing is null)
        {
            error = $"No imprint track '{id}'.";
            return null;
        }

        if (BrandLocked(character, id))
        {
            error = $"Cannot decondition {id} while its lustbrand core remains.";
            return null;
        }

        var unwilling = existing.Value.Origin != "willing";
        if (method == "rest" && existing.Value.Level >= 3 && unwilling)
        {
            error = "Level 3 unwilling imprints do not drop on rest. Use method=therapy.";
            return null;
        }

        if (method == "rest" && Exposed(character, id))
        {
            ClearExposed(character, id);
            return $"{character.Id} imprint {id}: aligned exposure cancels this rest's drop.";
        }

        var dc = ImprintMath.DeconditionDc(existing.Value.Level, unwilling, method);
        var total = die + wisMod;
        if (total < dc)
            return $"{character.Id} imprint {id} decondition {die}+{wisMod}={total} < DC {dc}. No drop.";

        var drop = method == "rest" ? 1 : ImprintMath.TherapyDrop(total - dc);
        var points = Math.Max(0, existing.Value.Points - drop);
        if (points == 0)
            Remove(participant, character, id);
        else
            Write(participant, character, id, points, existing.Value.Origin, day, forceOrigin: true, context: null);
        ClearExposed(character, id);
        if (method == "rest")
            PregnancyState.Set(character, RestDayKey(id), day.ToString());
        return $"{character.Id} imprint {id} decondition {die}+{wisMod}={total} ≥ DC {dc}. −{drop} → {points} (level {ImprintMath.LevelFor(points)}).";
    }

    /// <summary>
    /// Imprint pressure from an act. Inside a lewd scene it only records the tracks in the scene ledger — at most
    /// one tick per track per scene, resolved by <see cref="ResolveSceneAsync"/> when the encounter ends. Outside a
    /// scene it resolves at once.
    /// </summary>
    public static async Task AutoAsync(
        IChangeContext context,
        ModeParticipantState? participant,
        Character? character,
        IEnumerable<string>? tags,
        bool wanted,
        bool bitchsuit,
        CancellationToken ct,
        string? anchorId = null)
    {
        if (character is null)
            return;
        if (string.Equals(anchorId, character.Id, StringComparison.OrdinalIgnoreCase))
            anchorId = null;
        var tagList = tags?.ToList() ?? [];
        var tracks = ImprintMath.TracksFor(tagList, bitchsuit);
        if (tracks.Count == 0)
            return;

        var inScene = LewdModeAccess.TryGetParticipant(context, character.Id) is not null;
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var ledger = inScene ? ReadLedger(character) : null;
        var day = inScene ? 0 : await DayAsync(context, ct).ConfigureAwait(false);
        foreach (var id in tracks)
        {
            if (HardBlocked(participant, character, settings, id, tagList))
            {
                context.RecordMessage($"{character.Id} hard limit blocks imprint '{id}'. Hard limits never imprint.");
                continue;
            }

            if (ledger is not null)
            {
                // Unwilling pressure outweighs willing within one scene; the first to press it is its anchor.
                if (!ledger.TryGetValue(id, out var already) || (already.Wanted && !wanted))
                    ledger[id] = (wanted, already.Anchor ?? anchorId);
                continue;
            }

            await ResolveTickAsync(context, participant, character, id, wanted, settings, day, ct, anchorId).ConfigureAwait(false);
        }

        if (ledger is not null)
            WriteLedger(character, ledger);
    }

    /// <summary>Resolves and clears the scene ledger: one tick (or resist save) per recorded track.</summary>
    public static async Task ResolveSceneAsync(IChangeContext context, Character character, CancellationToken ct)
    {
        var ledger = ReadLedger(character);
        character.SystemStats.Traits.Remove(LewdKeys.TraitSceneImprints);
        if (ledger.Count == 0)
            return;

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var day = await DayAsync(context, ct).ConfigureAwait(false);
        foreach (var (id, entry) in ledger.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            await ResolveTickAsync(context, null, character, id, entry.Wanted, settings, day, ct, entry.Anchor).ConfigureAwait(false);
    }

    private static async Task ResolveTickAsync(
        IChangeContext context,
        ModeParticipantState? participant,
        Character character,
        string id,
        bool wanted,
        LewdSettings settings,
        int day,
        CancellationToken ct,
        string? anchorId = null)
    {
        if (wanted)
        {
            Tick(participant, character, id, willing: true, delta: 1, day, convert: false, context, anchorId);
            return;
        }

        if (!settings.AllowsUnwanted(character, character.Id, out _))
        {
            context.RecordMessage($"{character.Id} unwilling imprint '{id}' skipped: lewdNonConsent does not allow it for them.");
            return;
        }

        if (context.Rolls is null)
        {
            context.RecordMessage(
                $"{character.Id} imprint '{id}' suggested (unwilling). Emit lewd_imprint with a WIS or INT save (DC {ImprintMath.ResistDc(Level(character, id), 0)}) to negate. This did not tick.");
            return;
        }

        var abilityMod = AbilityScores.Mod(character, "wis");
        var roll = await SaveDice.RollAsync(
            context, "lewd_imprint_resist", faceOrZero: 0, abilityMod, disadvantage: false, ct).ConfigureAwait(false);
        if (roll.Error is not null)
        {
            context.RecordMessage($"{character.Id} imprint '{id}': {roll.Error}");
            return;
        }

        var dc = ImprintMath.ResistDc(Level(character, id), 0);
        context.RecordMessage(
            $"Lewd imprint resist {id}: {roll.Summary} vs DC {dc} (wis from sheet; pass abilityMod on lewd_imprint to override).");
        if (roll.Total >= dc)
        {
            context.RecordMessage($"{character.Id} resisted imprint '{id}'.");
            return;
        }

        Tick(participant, character, id, willing: false, delta: 1, day, convert: false, context, anchorId);
    }

    private static Dictionary<string, (bool Wanted, string? Anchor)> ReadLedger(Character character)
    {
        var ledger = new Dictionary<string, (bool Wanted, string? Anchor)>(StringComparer.OrdinalIgnoreCase);
        var raw = PregnancyState.Text(character, LewdKeys.TraitSceneImprints);
        if (string.IsNullOrWhiteSpace(raw))
            return ledger;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':', 3, StringSplitOptions.TrimEntries);
            if (ImprintMath.IsTrack(bits[0]))
                ledger[ImprintMath.Normalize(bits[0])] = (bits.Length > 1 && bits[1] == "willing", bits.Length > 2 && bits[2].Length > 0 ? bits[2] : null);
        }

        return ledger;
    }

    private static void WriteLedger(Character character, Dictionary<string, (bool Wanted, string? Anchor)> ledger) =>
        PregnancyState.Set(character, LewdKeys.TraitSceneImprints,
            string.Join(",", ledger.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}:{(kv.Value.Wanted ? "willing" : "unwilling")}{(kv.Value.Anchor is null ? "" : ":" + kv.Value.Anchor)}")));

    public static async Task OnLongRestAsync(
        Character character,
        ModeParticipantState? participant,
        IChangeContext context,
        CancellationToken ct)
    {
        var day = await DayAsync(context, ct).ConfigureAwait(false);
        foreach (var track in Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)))
        {
            if (Exposed(character, track.Id))
            {
                ClearExposed(character, track.Id);
                context.RecordMessage($"{character.Id} imprint {track.Id}: exposure cancels this rest's drop.");
                continue;
            }

            if (track.Level >= 3 && track.Origin != "willing")
            {
                context.RecordMessage($"{character.Id} imprint {track.Id} is level 3 unwilling. Rest does not drop it. Use lewd_decondition method=therapy.");
                continue;
            }

            if (BrandLocked(character, track.Id))
            {
                context.RecordMessage($"{character.Id} imprint {track.Id} is locked by a live lustbrand core. Rest does not drop it.");
                continue;
            }

            if (PregnancyState.Int(character, RestDayKey(track.Id)) == day && day != 0)
                continue;

            if (context.Rolls is null)
            {
                context.RecordMessage(
                    $"{character.Id} imprint {track.Id}: long rest, no exposure. Emit lewd_decondition method=rest with d20 and wisMod. DC {ImprintMath.RestDc}. This observer did not roll.");
                continue;
            }

            var wisMod = AbilityScores.Mod(character, "wis");
            var roll = await SaveDice.RollAsync(
                context, "lewd_imprint_rest", faceOrZero: 0, wisMod, disadvantage: false, ct).ConfigureAwait(false);
            if (roll.Error is not null)
            {
                context.RecordMessage($"{character.Id} imprint {track.Id}: {roll.Error}");
                continue;
            }

            var note = Decondition(participant, character, track.Id, "rest", roll.Face, wisMod, day, out var error);
            context.RecordMessage(note ?? error);
        }
    }

    public static void Restamp(Character character, string? removed)
    {
        if (string.IsNullOrWhiteSpace(removed))
            return;
        foreach (var track in Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)))
        {
            if (track.Level < 3)
                continue;
            if (!StatusMatches(removed, track.Id))
                continue;
            Stamp(character, track.Id, track.Level, track.Origin, track.AnchorId);
        }
    }

    public static bool Exposed(Character character, string id) =>
        PregnancyState.Flag(character, ExposedKey(id));

    public static void SetExposed(Character character, string id) =>
        PregnancyState.Set(character, ExposedKey(id), "true");

    public static void ClearExposed(Character character, string id) =>
        PregnancyState.Remove(character, ExposedKey(id));

    public static async Task<int> DayAsync(IChangeContext context, CancellationToken ct)
    {
        var time = await context.GetCurrentTimeAsync().ConfigureAwait(false);
        return time.TotalDaysElapsed;
    }

    public static string? FeatStub(string id) => id switch
    {
        "wanton" => "wanton_whore",
        "training" or "ordeal" => "edge_puppet",
        _ => null,
    };

    private static void Write(
        ModeParticipantState? participant,
        Character character,
        string id,
        int points,
        string origin,
        int day,
        bool forceOrigin,
        IChangeContext? context,
        string? anchorId = null)
    {
        var tracks = Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)).ToList();
        var index = tracks.FindIndex(t => t.Id == id);
        var keptOrigin = !forceOrigin && index >= 0 ? tracks[index].Origin : origin;
        var anchor = anchorId ?? (index >= 0 ? tracks[index].AnchorId : null);
        if (index >= 0)
            tracks[index] = new ImprintTrack(id, points, ImprintMath.LevelFor(points), keptOrigin, day, anchor);
        else
            tracks.Add(new ImprintTrack(id, points, ImprintMath.LevelFor(points), keptOrigin, day, anchor));
        PregnancyState.Set(character, LewdKeys.TraitImprints, Format(tracks));
        ApplyEffects(participant, character, tracks);
        _ = context;
    }

    private static void Remove(ModeParticipantState? participant, Character character, string id)
    {
        var tracks = Parse(PregnancyState.Text(character, LewdKeys.TraitImprints)).Where(t => t.Id != id).ToList();
        if (tracks.Count == 0)
            PregnancyState.Remove(character, LewdKeys.TraitImprints);
        else
            PregnancyState.Set(character, LewdKeys.TraitImprints, Format(tracks));
        PregnancyState.Remove(character, FeatKey(id));
        PregnancyState.Remove(character, ExposedKey(id));
        character.SystemStats.StatusEffects.RemoveAll(e => StatusMatches(e.Name, id) || StatusMatches(e.ConditionName, id));
        ApplyEffects(participant, character, tracks);
    }

    private static void ApplyEffects(ModeParticipantState? participant, Character character, List<ImprintTrack> tracks)
    {
        var thoughts = new List<string>();
        var inhib = 0;
        foreach (var track in tracks)
        {
            var unwilling = track.Origin != "willing";
            if (track.Level >= 1)
                EnsureKink(participant, track.Id);
            if ((unwilling && track.Level >= 1) || (!unwilling && track.Level >= 3))
                thoughts.Add("imprint:" + track.Id);
            if (unwilling && track.AnchorId is null)
                inhib += track.Level;
            if (track.Level >= 3)
            {
                Stamp(character, track.Id, track.Level, track.Origin, track.AnchorId);
                var feat = FeatStub(track.Id);
                if (feat is not null)
                    PregnancyState.Set(character, FeatKey(track.Id), feat);
            }
            else
            {
                character.SystemStats.StatusEffects.RemoveAll(e => StatusMatches(e.Name, track.Id) || StatusMatches(e.ConditionName, track.Id));
                PregnancyState.Remove(character, FeatKey(track.Id));
            }
        }

        var others = (PregnancyState.Text(character, LewdKeys.TraitIntrusiveThoughts) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.StartsWith("imprint:", StringComparison.OrdinalIgnoreCase));
        var thoughtText = string.Join(",", thoughts.Concat(others).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.Ordinal));
        if (thoughtText.Length == 0)
            PregnancyState.Remove(character, LewdKeys.TraitIntrusiveThoughts);
        else
            PregnancyState.Set(character, LewdKeys.TraitIntrusiveThoughts, thoughtText);
        PregnancyState.Set(character, LewdKeys.TraitImprintInhib, inhib.ToString());

        Mirror(participant, character, tracks, thoughtText, inhib);
    }

    private static void Stamp(Character character, string id, int level, string origin, string? anchorId)
    {
        var effects = character.SystemStats.StatusEffects;
        var condition = "imprint:" + id;
        var name = EffectPrefix + Title(id);
        var hint = Hint(id, level, origin, anchorId);
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.ConditionName, condition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name;
            existing.ConditionName = condition;
            existing.RecoveryHint = hint;
            return;
        }

        effects.Add(new StatusEffect
        {
            Name = name,
            Category = "Condition",
            ConditionName = condition,
            AppliedBy = "lewd_imprint",
            RecoveryHint = hint,
        });
    }

    private static string Hint(string id, int level, string origin, string? anchorId)
    {
        var tail = id switch
        {
            "training" when level >= 3 => $" Obey-save vs {anchorId ?? "whoever trained them"} DC 15. Engine does not roll it.",
            "cruelty" when level >= 3 => " +1 stimulation when this bearer inflicts pain or the target is overstimulated.",
            _ => "",
        };
        var tied = anchorId is null ? "" : $" Tied to {anchorId}: only they trigger it.";
        return $"Lasting {id} fetish, level {level}, {origin}.{tied} Decondition with lewd_decondition. Not a vice. Not removed by remove curse.{tail}";
    }

    private static void Mirror(
        ModeParticipantState? participant,
        Character character,
        List<ImprintTrack> tracks,
        string thoughts,
        int inhib)
    {
        if (participant is null)
            return;
        participant.State[LewdKeys.Imprints] = Format(tracks);
        participant.State[LewdKeys.IntrusiveThoughts] = thoughts;
        participant.State[LewdKeys.ImprintInhib] = inhib;
    }

    private static void EnsureKink(ModeParticipantState? participant, string tag)
    {
        if (participant is null)
            return;
        var kinks = ConsentGate.GetStringList(participant, LewdKeys.Kinks);
        if (kinks.Any(k => string.Equals(k, tag, StringComparison.OrdinalIgnoreCase)))
            return;
        kinks.Add(tag);
        participant.State[LewdKeys.Kinks] = kinks;
    }

    private static void ClearJump(ModeParticipantState? participant, Character character)
    {
        PregnancyState.Remove(character, LewdKeys.TraitBadEndImprintTrack);
        PregnancyState.Remove(character, LewdKeys.TraitBadEndImprintJump);
        PregnancyState.Remove(character, LewdKeys.TraitBadEndImprintOrigin);
        if (participant is null)
            return;
        participant.State.Remove(LewdKeys.BadEndImprintTrack);
        participant.State.Remove(LewdKeys.BadEndImprintJump);
        participant.State.Remove(LewdKeys.BadEndImprintOrigin);
    }

    private static bool StatusMatches(string? status, string id) =>
        string.Equals(status, "imprint:" + id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, EffectPrefix + Title(id), StringComparison.OrdinalIgnoreCase);

    private static string Title(string id) => id switch
    {
        "wanton" => "Wanton",
        "training" => "Training",
        "breeding" => "Breeding",
        "ordeal" => "Ordeal",
        "cruelty" => "Cruelty",
        _ => id,
    };

    private static string ExposedKey(string id) => LewdKeys.ImprintTraitPrefix + id + ".exposed";
    private static string FeatKey(string id) => LewdKeys.ImprintTraitPrefix + id + ".feat";
    private static string RestDayKey(string id) => LewdKeys.ImprintTraitPrefix + id + ".rest_day";
}
