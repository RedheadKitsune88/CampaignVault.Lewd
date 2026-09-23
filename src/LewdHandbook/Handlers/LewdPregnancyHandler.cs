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

        if (action is "advance" or "terminate" or "termination_save")
            return ApplyOngoing(req, context, targetChar, participant, action);
        if (action == "rest")
            return await ApplyRestAsync(req, context, targetChar, ct).ConfigureAwait(false);

        return await ImpregnateAsync(req, context, targetChar, participant, ct).ConfigureAwait(false);
    }

    private static async Task<ChangeHandlerResult> ApplyRestAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        CancellationToken ct)
    {
        if (!PregnancyState.Flag(targetChar, LewdKeys.Pregnant))
            return ChangeHandlerResult.Failure($"{req.TargetId} is not pregnant.");
        if (req.D20 is < 1 or > 20)
            return ChangeHandlerResult.Failure("d20 must be between 1 and 20 for a pregnancy rest save.");

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        if (PregnancyState.Int(targetChar, LewdKeys.PregnancyRestPoisonDay) == day && day != 0)
        {
            context.RecordMessage($"Lewd pregnancy rest {req.TargetId}: already resolved this day.");
            return ChangeHandlerResult.Ok;
        }

        var conMod = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier);
        var total = req.D20 + conMod;
        var saved = PregnancyMath.CheckSucceeds(req.D20, conMod, PregnancyMath.RestPoisonDc);
        if (!saved)
            PregnancyState.StampPoisoned(targetChar);
        PregnancyState.Set(targetChar, LewdKeys.PregnancyRestPoisonDay, day.ToString());
        context.RecordMessage(
            $"Lewd pregnancy rest {req.TargetId}: {req.D20}+{conMod}={total} vs DC {PregnancyMath.RestPoisonDc} → {(saved ? "saved" : "poisoned 1d4 hours")}.");
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult ApplyOngoing(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant,
        string action)
    {
        if (action == "terminate")
        {
            PregnancyState.ClearPregnant(targetChar);
            PregnancyState.Mirror(participant, targetChar);
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: terminated.");
            return ChangeHandlerResult.Ok;
        }

        if (!PregnancyState.Flag(targetChar, LewdKeys.Pregnant))
            return ChangeHandlerResult.Failure($"{req.TargetId} is not pregnant.");

        if (action == "advance")
        {
            var nontrad = IsNontraditional(PregnancyState.Text(targetChar, LewdKeys.PregnancyType));
            var delta = req.ProgressDelta ?? PregnancyMath.DefaultRestDelta(nontrad);
            var before = PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress);
            var after = PregnancyMath.ClampProgress(before + delta);
            PregnancyState.Set(targetChar, LewdKeys.PregnancyProgress, after.ToString());
            PregnancyState.Mirror(participant, targetChar);
            context.RecordMessage($"Lewd pregnancy {req.TargetId}: progress {before}→{after}.");
            if (after >= PregnancyMath.ProgressMax)
                context.RecordPhysicalStateNudge($"{req.TargetId} pregnancy term is complete. Narrate birth; do not leave them pregnant.");
            return ChangeHandlerResult.Ok;
        }

        if (req.Dc is null)
            return ChangeHandlerResult.Failure("dc is required for termination_save (use the damage taken).");
        if (req.D20 is < 1 or > 20)
            return ChangeHandlerResult.Failure("d20 must be between 1 and 20 for a termination save.");
        var termCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier);
        var termTotal = req.D20 + termCon;
        var held = PregnancyMath.CheckSucceeds(req.D20, termCon, req.Dc.Value);
        if (!held)
        {
            PregnancyState.ClearPregnant(targetChar);
            PregnancyState.Mirror(participant, targetChar);
        }

        context.RecordMessage(
            $"Lewd pregnancy termination save {req.TargetId}: {req.D20}+{termCon}={termTotal} vs DC {req.Dc} → {(held ? "held" : "terminated")}.");
        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult> ImpregnateAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        Character targetChar,
        ModeParticipantState? participant,
        CancellationToken ct)
    {
        var gate = await GateAsync(req, context, participant, ct).ConfigureAwait(false);
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

            Begin(targetChar, participant, req, nontrad, req.ProgressOnSuccess ?? PregnancyMath.ForcedHalfway);
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
            if (req.D20 is < 1 or > 20)
                return ChangeHandlerResult.Failure("d20 must be between 1 and 20.");
            var disadv = req.Hyperfertile || req.Hypervirile ||
                         PregnancyState.Flag(targetChar, LewdKeys.TraitHyperfertile) ||
                         PregnancyState.HasCondition(targetChar, LewdKeys.ConditionHyperfertile) ||
                         ActorHypervirile(req, context);
            var die = PregnancyMath.PickDie(req.D20, req.SecondD20, advantage: false);
            if (!disadv)
                die = req.D20;
            else if (req.SecondD20 is null)
                context.RecordMessage("Lewd pregnancy: disadvantage declared but secondD20 omitted; using the single die.");
            var inhib = Unwanted(req, participant) ? req.InhibitionBonus : 0;
            var targetCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier);
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
            if (req.D20 is < 1 or > 20)
                return ChangeHandlerResult.Failure("d20 must be between 1 and 20.");
            var advantage = req.Hyperfertile ||
                            PregnancyState.Flag(targetChar, LewdKeys.TraitHyperfertile) ||
                            PregnancyState.HasCondition(targetChar, LewdKeys.ConditionHyperfertile) ||
                            ActorHyperfertile(req, context);
            var die = advantage
                ? PregnancyMath.PickDie(req.D20, req.SecondD20, advantage: true)
                : req.D20;
            if (advantage && req.SecondD20 is null)
                context.RecordMessage("Lewd pregnancy: advantage declared but secondD20 omitted; using the single die.");
            var condom = IsContraceptive(req, "condom");
            var targetCon = AbilityScores.Resolve(targetChar, "con", req.TargetConModifier);
            Character? actorChar = null;
            if (!string.IsNullOrWhiteSpace(req.ActorId))
                context.Characters.TryGetValue(req.ActorId, out actorChar);
            var actorCon = AbilityScores.Resolve(actorChar, "con", req.ActorConModifier);
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

        Begin(targetChar, participant, req, nontrad, req.ProgressOnSuccess ?? 0);
        NoteCapture(req, context, targetChar, participant);
        context.RecordMessage(
            $"Lewd pregnancy {req.TargetId}: impregnated ({KindLabel(nontrad)}) by {req.ActorId ?? "unknown"}, progress {PregnancyState.Int(targetChar, LewdKeys.PregnancyProgress)}.");
        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult?> GateAsync(
        LewdPregnancyChange req,
        IChangeContext context,
        ModeParticipantState? participant,
        CancellationToken ct)
    {
        if (participant is not null)
        {
            var limits = ConsentGate.GetStringList(participant, LewdKeys.HardLimits);
            if (limits.Any(t => HardLimitTags.Any(h => string.Equals(t, h, StringComparison.OrdinalIgnoreCase))))
                return ChangeHandlerResult.Failure("hard limit blocks pregnancy.");
            if (string.Equals(ConsentGate.GetString(participant, LewdKeys.Consent), LewdKeys.ConsentRevoked, StringComparison.OrdinalIgnoreCase))
                return ChangeHandlerResult.Failure("consent revoked.");
        }

        if (!Unwanted(req, participant))
            return null;

        var tone = await IntimacyTone.ResolveAsync(context, ct).ConfigureAwait(false);
        if (tone != IntimacyToneKind.Grimdark)
            return ChangeHandlerResult.Failure("unwilling impregnation requires intimacyTone grimdark.");
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

        if (!nontrad && (req.Infertile ||
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
        int progress)
    {
        PregnancyState.Set(targetChar, LewdKeys.Pregnant, "true");
        PregnancyState.Set(targetChar, LewdKeys.PregnancyProgress, PregnancyMath.ClampProgress(progress).ToString());
        PregnancyState.Set(targetChar, LewdKeys.PregnancyType, nontrad ? "nontraditional" : "traditional");
        PregnancyState.Set(targetChar, LewdKeys.PregnancySource, req.ActorId ?? "");
        PregnancyState.Set(targetChar, LewdKeys.PregnancyOffspring, "1");
        PregnancyState.StampPregnant(targetChar, req.ActorId);
        PregnancyState.Mirror(participant, targetChar);
    }

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

    private static bool Unwanted(LewdPregnancyChange req, ModeParticipantState? participant)
    {
        if (participant is not null && !string.IsNullOrWhiteSpace(req.ActorId))
            return !ConsentGate.IsAdvanceWanted(participant, req.ActorId);
        return req.Unwilling;
    }

    private static bool IsNontraditional(string? kind) =>
        string.Equals(kind, "nontraditional", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "non-traditional", StringComparison.OrdinalIgnoreCase);

    private static bool IsContraceptive(LewdPregnancyChange req, string name) =>
        string.Equals(req.Contraceptive, name, StringComparison.OrdinalIgnoreCase);

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
        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
            return null;
        return mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));
    }
}
