using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdPregnancyHandler : IWorldChangeHandler
{
    private static readonly string[] HardLimitTags = ["pregnancy", "impregnation", "breeding", "repro"];

    public bool ShouldHandle(WorldChange change) => change is LewdPregnancyChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdPregnancyChange)change;
        if (string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");

        if (!context.Characters.TryGetValue(req.TargetId, out var targetChar))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the commit context.");

        var participant = FindParticipant(context, req.TargetId);
        var action = (req.Action ?? "impregnate").Trim().ToLowerInvariant();

        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);
        if (action is "advance" or "terminate" or "termination_save" or "birth")
            return await ApplyOngoingAsync(req, context, targetChar, participant, action, now, ct).ConfigureAwait(false);
        if (action == "rest")
            return await ApplyRestAsync(req, context, targetChar, now, ct).ConfigureAwait(false);
        if (action != "impregnate")
            return ChangeHandlerResult.Failure("action must be impregnate, advance, rest, birth, terminate, or termination_save.");

        return await ImpregnateAsync(req, context, targetChar, participant, now, ct).ConfigureAwait(false);
    }

    /// <summary>Fallback when the host could not roll the rest save itself (no Rolls): same save, same once-per-rest guard.</summary>
    private static async Task<ChangeHandlerResult> ApplyRestAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        float now,
        CancellationToken ct)
    {
        if (!PregnancyState.Flag(targetChar, LewdKeys.Pregnant))
            return ChangeHandlerResult.Failure($"{req.TargetId} is not pregnant.");
        PregnancyState.Sync(targetChar, now, context);
        var note = await PregnancyRest.SaveAsync(context, targetChar, now, req.D20, req.TargetConModifier, ct).ConfigureAwait(false);
        context.RecordMessage(note ?? $"Lewd pregnancy rest {req.TargetId}: not showing yet (progress < {PregnancyMath.VisibleProgress}); no save.");
        return ChangeHandlerResult.Ok;
    }

    /// <summary>The given d20 (1–20), or one the host rolls when it is 0; an error when neither is possible.</summary>
    private static async Task<(int Face, string? Error)> D20Async(IChangeContext context, string tag, int given, CancellationToken ct) =>
        await D20Async(context, tag, given, null, ct).ConfigureAwait(false) is var (face, _, error) ? (face, error) : (0, null);

    /// <summary>As above, through the roll pipeline for <paramref name="who"/>; <c>Bonus</c> is what the pipeline adds to their Con modifier.</summary>
    private static async Task<(int Face, int Bonus, string? Error)> D20Async(
        IChangeContext context, string tag, int given, Character? who, CancellationToken ct)
    {
        var die = await SaveDice.RollAsync(context, tag, given, 0, false, ct, who: who, subject: "con").ConfigureAwait(false);
        return die.Error is null ? (die.Face, die.Total - die.Face, null) : (0, 0, die.Error);
    }

    private static async Task<ChangeHandlerResult> ApplyOngoingAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant,
        string action,
        float now,
        CancellationToken ct)
    {
        if (!PregnancyState.Flag(targetChar, LewdKeys.Pregnant))
            return ChangeHandlerResult.Failure($"{req.TargetId} is not pregnant.");
        PregnancyState.Sync(targetChar, now, context);

        if (action is "terminate" or "birth")
        {
            var progress = PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress);
            var offspring = Math.Max(1, PregnancyState.Int(targetChar, LewdKeys.PregnancyOffspring));
            var source = PregnancyState.Text(targetChar, LewdKeys.PregnancySource);
            var premature = action == "birth" && progress < PregnancyMath.ProgressMax;
            PregnancyState.ClearPregnant(targetChar);
            PregnancyState.Mirror(participant, targetChar);
            Publish(context, req.TargetId, action == "birth" ? "birth" : "terminated", source, offspring, progress);
            if (action == "birth" && BrandState.Has(targetChar, BrandCatalog.Fertility))
            {
                // Handbook: each birth raises Brand of Fertility a tier, up to 5th.
                var tier = BrandState.Tier(targetChar, BrandCatalog.Fertility);
                if (tier < BrandCatalog.MaxTier)
                {
                    BrandState.SetTier(targetChar, BrandCatalog.Fertility, tier + 1);
                    context.RecordMessage($"{req.TargetId} Brand of Fertility rises to tier {tier + 1}.");
                }
            }
            context.RecordMessage(action == "birth"
                ? $"Lewd pregnancy {req.TargetId}: birth ({offspring} offspring{(premature ? $", premature at {progress}" : "")}). Create any offspring who matter with character_create."
                : $"Lewd pregnancy {req.TargetId}: terminated at progress {progress}.");
            return ChangeHandlerResult.Ok;
        }

        if (action == "advance")
        {
            var nontrad = PregnancyState.IsNontraditional(targetChar);
            var delta = req.ProgressDelta ?? PregnancyMath.DefaultRestDelta(nontrad);
            var before = PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress);
            var after = PregnancyMath.ClampProgress(before + delta);
            // Magic that speeds (or slows) a pregnancy moves the clock with it, so time keeps counting from here.
            PregnancyState.Set(targetChar, LewdKeys.PregnancyProgress, after.ToString());
            PregnancyState.StartClock(targetChar, now, after, PregnancyState.Attr(targetChar, PregnancyState.TermHoursKey, PregnancyMath.DefaultTermHours(nontrad)));
            PregnancyState.Sync(targetChar, now, context);
            PregnancyState.Mirror(participant, targetChar);
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: progress {before}→{after}.");
            return ChangeHandlerResult.Ok;
        }

        if (req.Dc is null)
            return ChangeHandlerResult.Failure("dc is required for termination_save (use the damage taken).");
        var (termDie, termBonus, termError) = await D20Async(context, "lewd_pregnancy_termination", req.D20, targetChar, ct).ConfigureAwait(false);
        if (termError is not null)
            return ChangeHandlerResult.Failure(termError);
        var termCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier) + termBonus;
        var termTotal = termDie + termCon;
        var held = PregnancyMath.CheckSucceeds(termDie, termCon, req.Dc.Value);
        if (!held)
        {
            var lostAt = PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress);
            var source = PregnancyState.Text(targetChar, LewdKeys.PregnancySource);
            PregnancyState.ClearPregnant(targetChar);
            PregnancyState.Mirror(participant, targetChar);
            Publish(context, req.TargetId, "terminated", source, 0, lostAt);
        }

        context.RecordMessage(
            $"Lewd pregnancy termination save {req.TargetId}: {termDie}+{termCon}={termTotal} vs DC {req.Dc} → {(held ? "held" : "terminated")}.");
        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult> ImpregnateAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant,
        float now,
        CancellationToken ct)
    {
        var gate = await GateAsync(req, context, targetChar, participant, ct).ConfigureAwait(false);
        if (gate is not null)
            return gate.Value;

        var nontrad = IsNontraditional(req.Kind);
        var already = PregnancyState.Flag(targetChar, LewdKeys.Pregnant);

        if (req.Force)
        {
            if (already)
            {
                var count = Math.Max(1, PregnancyState.Int(targetChar, LewdKeys.PregnancyOffspring));
                PregnancyState.Set(targetChar, LewdKeys.PregnancyOffspring, (count * 2).ToString());
                PregnancyState.Mirror(participant, targetChar);
                context.RecordMessage($"Lewd pregnancy {req.TargetId}: already pregnant; offspring {count}→{count * 2}.");
                return ChangeHandlerResult.Ok;
            }

            Begin(targetChar, participant, req, nontrad, req.ProgressOnSuccess ?? PregnancyMath.ForcedHalfway, now, context);
            NoteCapture(req, context, targetChar, participant);
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: forced ({KindLabel(nontrad)}), progress {PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress)}.");
            return ChangeHandlerResult.Ok;
        }

        if (Blocked(req, targetChar, context, nontrad, out var block))
        {
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: blocked ({block}).");
            return ChangeHandlerResult.Ok;
        }

        if (nontrad)
        {
            if (req.Dc is null)
                return ChangeHandlerResult.Failure("dc is required for nontraditional pregnancy.");
            var (first, firstBonus, firstError) = await D20Async(context, "lewd_pregnancy_save", req.D20, targetChar, ct).ConfigureAwait(false);
            if (firstError is not null)
                return ChangeHandlerResult.Failure(firstError);
            var disadv = req.Hyperfertile || req.Hypervirile ||
                         PregnancyState.Flag(targetChar, LewdKeys.TraitHyperfertile) ||
                         PregnancyState.HasCondition(targetChar, LewdKeys.ConditionHyperfertile) ||
                         ActorHypervirile(req, context);
            var die = first;
            if (disadv)
            {
                var (second, secondError) = await D20Async(context, "lewd_pregnancy_save_2", req.SecondD20 ?? 0, ct).ConfigureAwait(false);
                if (secondError is not null)
                    context.RecordMessage($"Lewd pregnancy: disadvantage declared but no second die ({secondError}); using the single die.");
                else
                    die = PregnancyMath.PickDie(first, second, advantage: false);
            }
            var inhib = !Unwanted(req, participant, targetChar)
                ? 0
                : req.InhibitionBonus != 0 ? req.InhibitionBonus : LewdProfile.Inhibition(participant, targetChar);
            var targetCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier) + firstBonus;
            var total = die + targetCon + inhib;
            var saved = PregnancyMath.CheckSucceeds(die, targetCon + inhib, req.Dc.Value);
            if (saved)
            {
                context.RecordMessage($"Lewd pregnancy {req.TargetId}: nontraditional save {die}+{targetCon}+{inhib}={total} vs DC {req.Dc} → resisted.");
                return ChangeHandlerResult.Ok;
            }
        }
        else
        {
            Character? actorChar = null;
            if (!string.IsNullOrWhiteSpace(req.ActorId))
                context.Characters.TryGetValue(req.ActorId, out actorChar);
            var (first, actorBonus, firstError) = await D20Async(context, "lewd_pregnancy_impregnation", req.D20, actorChar, ct).ConfigureAwait(false);
            if (firstError is not null)
                return ChangeHandlerResult.Failure(firstError);
            var advantage = req.Hyperfertile ||
                            PregnancyState.Flag(targetChar, LewdKeys.TraitHyperfertile) ||
                            PregnancyState.HasCondition(targetChar, LewdKeys.ConditionHyperfertile) ||
                            ActorHyperfertile(req, context);
            var die = first;
            if (advantage)
            {
                var (second, secondError) = await D20Async(context, "lewd_pregnancy_impregnation_2", req.SecondD20 ?? 0, ct).ConfigureAwait(false);
                if (secondError is not null)
                    context.RecordMessage($"Lewd pregnancy: advantage declared but no second die ({secondError}); using the single die.");
                else
                    die = PregnancyMath.PickDie(first, second, advantage: true);
            }
            var condom = IsContraceptive(req, "condom");
            var targetCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier);
            var actorCon = AbilityScores.Resolve(actorChar, "con", req.ActorConModifier) + actorBonus;
            var dc = PregnancyMath.TraditionalDc(targetCon, req.TargetProficiency, condom);
            var total = die + actorCon;
            if (!PregnancyMath.CheckSucceeds(die, actorCon, dc))
            {
                context.RecordMessage($"Lewd pregnancy {req.TargetId}: impregnation {die}+{actorCon}={total} vs DC {dc} → failed.");
                return ChangeHandlerResult.Ok;
            }
        }

        if (already)
        {
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: already pregnant; progress unchanged.");
            return ChangeHandlerResult.Ok;
        }

        Begin(targetChar, participant, req, nontrad, req.ProgressOnSuccess ?? 0, now, context);
        NoteCapture(req, context, targetChar, participant);
        context.RecordMessage(
            $"Lewd pregnancy {req.TargetId}: impregnated ({KindLabel(nontrad)}) by {req.ActorId ?? "unknown"}, progress {PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress)}.");
        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult?> GateAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant,
        CancellationToken ct)
    {
        if (!AgeGate.TryPassAll(context, out var ageError, req.TargetId, req.ActorId))
            return ChangeHandlerResult.Failure(ageError!);
        if (LewdProfile.IsRevoked(participant, targetChar))
            return ChangeHandlerResult.Failure("consent revoked.");

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var limits = LewdProfile.HardLimits(participant, targetChar).Concat(settings.HardLimits);
        if (limits.Any(t => HardLimitTags.Any(h => string.Equals(t, h, StringComparison.OrdinalIgnoreCase))))
            return ChangeHandlerResult.Failure("hard limit blocks pregnancy.");

        if (Unwanted(req, participant, targetChar) && !settings.AllowsUnwanted(targetChar, req.TargetId, out var policyError))
            return ChangeHandlerResult.Failure($"Unwilling impregnation: {policyError}");

        settings.Narrate(context);
        return null;
    }

    private static bool Blocked(
        LewdPregnancyChange req,
        Character targetChar,
        IChangeContext context,
        bool nontrad,
        out string reason)
    {
        if (IsContraceptive(req, "beads"))
        {
            reason = "beads of prevention";
            return true;
        }

        if (req.AutoSucceedSave && nontrad)
        {
            reason = "ward";
            return true;
        }

        if (!nontrad && (req.Infertile || IsContraceptive(req, "infertility") ||
                         PregnancyState.Flag(targetChar, LewdKeys.TraitInfertile) ||
                         PregnancyState.HasCondition(targetChar, LewdKeys.ConditionInfertile) ||
                         ActorInfertile(req, context) ||
                         IsContraceptive(req, "oil")))
        {
            reason = IsContraceptive(req, "oil") ? "oil of impotence" : "infertile";
            return true;
        }

        reason = "";
        return false;
    }

    private static void Begin(
        Character targetChar,
        ModeParticipantState? participant,
        LewdPregnancyChange req,
        bool nontrad,
        int progress,
        float now,
        IChangeContext context)
    {
        PregnancyState.Set(targetChar, LewdKeys.Pregnant, "true");
        PregnancyState.Set(targetChar, LewdKeys.PregnancyProgress, PregnancyMath.ClampProgress(progress).ToString());
        PregnancyState.Set(targetChar, LewdKeys.PregnancyType, nontrad ? "nontraditional" : "traditional");
        PregnancyState.Set(targetChar, LewdKeys.PregnancySource, req.ActorId ?? "");
        PregnancyState.Set(targetChar, LewdKeys.PregnancyOffspring, "1");
        var termHours = req.TermDays is > 0 ? req.TermDays.Value * 24f : PregnancyMath.DefaultTermHours(nontrad);
        PregnancyState.StartClock(targetChar, now, progress, termHours);
        PregnancyState.Sync(targetChar, now, context);
        PregnancyState.Mirror(participant, targetChar);
        Publish(context, targetChar.Id, "conceived", req.ActorId, 1, PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress));
    }

    private static void Publish(IChangeContext context, string characterId, string state, string? sourceId, int offspring, int progress) =>
        context.Publish(
            Events.LewdEvents.Pregnancy,
            new { characterId, state, sourceId = string.IsNullOrWhiteSpace(sourceId) ? null : sourceId, offspring, progress });

    private static void NoteCapture(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant)
    {
        if (!req.NoEscape)
            return;
        BadEndState.Apply(participant, targetChar, BadEndMath.CaptureImpreg, req.ActorId, context: context);
    }

    private static bool Unwanted(LewdPregnancyChange req, ModeParticipantState? participant, Character targetChar)
    {
        // Only the stance can make it wanted: a blank actorId no longer skips the check.
        return req.Unwilling || !LewdProfile.Wants(participant, targetChar, req.ActorId);
    }

    private static bool IsNontraditional(string? kind) =>
        string.Equals(kind, "nontraditional", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "non-traditional", StringComparison.OrdinalIgnoreCase);

    /// <summary>Accepts the short form (<c>oil</c>) or the catalog item name (<c>oil_of_impotence</c>).</summary>
    private static bool IsContraceptive(LewdPregnancyChange req, string name) =>
        ContraceptiveKind(req.Contraceptive) == name;

    private static string? ContraceptiveKind(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_') switch
        {
            "condom" or "condoms" => "condom",
            "oil" or "oil_of_impotence" => "oil",
            "beads" or "beads_of_prevention" => "beads",
            "potion_of_infertility" or "infertility" => "infertility",
            _ => null,
        };

    private static bool ActorHyperfertile(LewdPregnancyChange req, IChangeContext context) =>
        ActorFlag(req, context, LewdKeys.TraitHyperfertile, LewdKeys.ConditionHyperfertile);

    private static bool ActorHypervirile(LewdPregnancyChange req, IChangeContext context) =>
        ActorFlag(req, context, LewdKeys.TraitHypervirile, LewdKeys.ConditionHypervirile);

    private static bool ActorInfertile(LewdPregnancyChange req, IChangeContext context) =>
        ActorFlag(req, context, LewdKeys.TraitInfertile, LewdKeys.ConditionInfertile);

    private static bool ActorFlag(LewdPregnancyChange req, IChangeContext context, string trait, string condition)
    {
        if (string.IsNullOrWhiteSpace(req.ActorId) || !context.Characters.TryGetValue(req.ActorId, out var actor))
            return false;
        return PregnancyState.Flag(actor, trait) || PregnancyState.HasCondition(actor, condition);
    }

    private static string KindLabel(bool nontrad) => nontrad ? "nontraditional" : "traditional";

    private static ModeParticipantState? FindParticipant(IChangeContext context, string id)
    {
        var mode = LewdModeAccess.TryGetActive(context);
        if (mode is null)
            return null;
        return mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));
    }
}
