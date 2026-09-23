using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdApplyBrandHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdApplyBrandChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdApplyBrandChange)change;
        if (string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        if (!context.Characters.TryGetValue(req.TargetId, out var character))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the commit context.");

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.TargetId, StringComparison.OrdinalIgnoreCase));
        if (participant is not null &&
            string.Equals(ConsentGate.GetString(participant, LewdKeys.Consent), LewdKeys.ConsentRevoked, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure("consent revoked.");

        var action = (req.Action ?? "apply").Trim().ToLowerInvariant();
        var id = req.BrandId?.Trim().ToLowerInvariant();

        if (action is "apply" or "remove")
            return await ApplyOrRemove(req, context, character, participant, action, id, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(id) || !BrandState.Has(character, id))
            return ChangeHandlerResult.Failure($"Target does not bear lustbrand '{req.BrandId}'.");

        switch (action)
        {
            case "vow":
                if (id != BrandCatalog.Oaths)
                    return ChangeHandlerResult.Failure("vow requires brandId=oaths.");
                if (string.IsNullOrWhiteSpace(req.Payload))
                    return ChangeHandlerResult.Failure("vow requires payload.");
                character.SystemStats.Traits[$"lustbrand.{id}.payload"] = req.Payload.Trim();
                context.RecordMessage(
                    $"{req.TargetId} Brand of Oaths stores the vow. A breach compels public sexual humiliation, or nymphomanic and −1 arousal max per day. The engine does not judge the breach.");
                return ChangeHandlerResult.Ok;
            case "release":
                if (id != BrandCatalog.Denial)
                    return ChangeHandlerResult.Failure("release requires brandId=denial.");
                BrandState.ReleaseDenial(character, context);
                BrandState.Mirror(participant, character);
                return ChangeHandlerResult.Ok;
            case "stabilize":
                if (id != BrandCatalog.Altruism)
                    return ChangeHandlerResult.Failure("stabilize requires brandId=altruism.");
                var next = Math.Min(BrandCatalog.MaxTier, BrandState.Tier(character, id) + 1);
                BrandState.Apply(participant, character, id, next, req.SourceId, req.Payload, req.Concubi, context);
                return ChangeHandlerResult.Ok;
            case "trigger":
                if (id != BrandCatalog.Transformation)
                    return ChangeHandlerResult.Failure("trigger requires brandId=transformation.");
                if (req.D20 is < 1 or > 20)
                    return ChangeHandlerResult.Failure("d20 must be between 1 and 20 for a transformation save.");
                var total = req.D20 + req.ConModifier;
                var saved = total >= BrandState.TransformationDc;
                if (!saved)
                {
                    LewdPoolHelper.EnsureNamedCondition(character, LewdKeys.ConditionNymphomanic, LewdKeys.ConditionNymphomanic,
                        "Brand of Transformation: hybrid form, 1 hour.");
                }

                context.RecordMessage(
                    $"{req.TargetId} Brand of Transformation: {req.D20}+{req.ConModifier}={total} vs DC {BrandState.TransformationDc} → {(saved ? "resisted" : "hybrid form 1 hour, nymphomanic, advantage on sexual advances, disadvantage vs charm or domination")}.");
                return ChangeHandlerResult.Ok;
            default:
                return ChangeHandlerResult.Failure("action must be apply, remove, vow, trigger, release, or stabilize.");
        }
    }

    private static async Task<ChangeHandlerResult> ApplyOrRemove(
        LewdApplyBrandChange req,
        IChangeContext context,
        Character character,
        ModeParticipantState? participant,
        string action,
        string? id,
        CancellationToken ct)
    {
        if (!BrandCatalog.TryGet(id, out var def))
            return ChangeHandlerResult.Failure($"Unknown lustbrand '{req.BrandId}'.");

        if (participant is not null && HitsHardLimit(participant, id!))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' hard limit blocks lustbrand '{id}'.");

        if (action == "remove")
        {
            var method = req.Method?.Trim().ToLowerInvariant();
            if (method is not ("wish" or "feature"))
                return ChangeHandlerResult.Failure("Lustbrands are not removed by remove curse. method must be wish or feature.");
            if (!BrandState.Remove(participant, character, id!, req.Concubi, context))
                return ChangeHandlerResult.Failure($"Target does not bear lustbrand '{id}'.");
            return ChangeHandlerResult.Ok;
        }

        var tone = await IntimacyTone.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!req.Willing && tone == IntimacyToneKind.Consensual)
            return ChangeHandlerResult.Failure("Unwilling lewd_apply_brand requires intimacyTone fade or grimdark, or willing=true.");
        if (!req.Willing && tone == IntimacyToneKind.Fade)
        {
            context.RecordPhysicalStateNudge(
                $"{req.TargetId} lustbrand applied under intimacyTone=fade; narrate the curse without a graphic branding.");
        }

        var tier = req.Tier ?? def.Tier;
        if (tier is < 1 or > BrandCatalog.MaxTier)
            return ChangeHandlerResult.Failure("tier must be 1–5.");
        BrandState.Apply(participant, character, id!, tier, req.SourceId, req.Payload, req.Concubi, context);
        return ChangeHandlerResult.Ok;
    }

    private static bool HitsHardLimit(ModeParticipantState participant, string id)
    {
        var hard = ConsentGate.GetStringList(participant, LewdKeys.HardLimits);
        return hard.Any(h =>
            string.Equals(h, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(h, "lustbrand", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(h, "brand", StringComparison.OrdinalIgnoreCase));
    }
}
