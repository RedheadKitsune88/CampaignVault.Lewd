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
        if (!AgeGate.TryPassAll(context, out var ageError, req.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        var character = context.Characters[req.TargetId];

        var participant = LewdModeAccess.TryGetParticipant(context, req.TargetId);
        var action = (req.Action ?? "apply").Trim().ToLowerInvariant();
        // Taking a brand off is never blocked by the bearer's stance or limits.
        if (action != "remove" && LewdProfile.IsRevoked(participant, character))
            return ChangeHandlerResult.Failure("consent revoked.");

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
                PregnancyState.Set(character, LewdKeys.LustbrandTraitPrefix + $"{id}.payload", req.Payload.Trim());
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
            case "heal":
                if (id != BrandCatalog.Altruism)
                    return ChangeHandlerResult.Failure("heal requires brandId=altruism (the bearer healed someone else).");
                if (req.Amount is not > 0)
                    return ChangeHandlerResult.Failure("heal requires amount > 0 (hit points restored).");
                BrandState.OnHeal(character, participant, req.Amount.Value, context);
                return ChangeHandlerResult.Ok;
            case "trigger":
                if (id != BrandCatalog.Transformation)
                    return ChangeHandlerResult.Failure("trigger requires brandId=transformation.");
                var conMod = req.ConModifier ?? AbilityScores.Mod(character, "con");
                var save = await SaveDice.RollAsync(context, "lewd_transformation_save", req.D20, conMod, disadvantage: false, ct)
                    .ConfigureAwait(false);
                if (save.Error is not null)
                    return ChangeHandlerResult.Failure(save.Error);
                var total = save.Total;
                var saved = total >= BrandState.TransformationDc;
                if (!saved)
                {
                    LewdPoolHelper.EnsureNamedCondition(character, LewdKeys.ConditionNymphomanic, LewdKeys.ConditionNymphomanic,
                        "Brand of Transformation: hybrid form, 1 hour.");
                }

                context.RecordMessage(
                    $"{req.TargetId} Brand of Transformation: {save.Summary} vs DC {BrandState.TransformationDc} → {(saved ? "resisted" : "hybrid form 1 hour, nymphomanic, advantage on sexual advances, disadvantage vs charm or domination")}.");
                return ChangeHandlerResult.Ok;
            default:
                return ChangeHandlerResult.Failure("action must be apply, remove, vow, trigger, release, stabilize, or heal.");
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

        if (action == "remove")
        {
            var method = req.Method?.Trim().ToLowerInvariant();
            if (method is not ("wish" or "feature"))
                return ChangeHandlerResult.Failure("Lustbrands are not removed by remove curse. method must be wish or feature.");
            if (!BrandState.Remove(participant, character, id!, req.Concubi, context))
                return ChangeHandlerResult.Failure($"Target does not bear lustbrand '{id}'.");
            context.Publish(
                Events.LewdEvents.BrandChanged,
                new { characterId = req.TargetId, brandId = id, action = "remove", tier = 0 });
            return ChangeHandlerResult.Ok;
        }

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var probe = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id!, "lustbrand", "brand" };
        if (settings.HitsHardLimit(probe, out var campaignHit))
            return ChangeHandlerResult.Failure($"Campaign hard limit '{campaignHit}' blocks lustbrand '{id}'.");
        if (LewdProfile.HardLimits(participant, character).FirstOrDefault(probe.Contains) is { } personal)
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' hard limit '{personal}' blocks lustbrand '{id}'.");
        if (!req.Willing && !settings.AllowsUnwanted(character, req.TargetId, out var policyError))
            return ChangeHandlerResult.Failure($"Unwilling lewd_apply_brand: {policyError} Or set willing=true if they accept it.");
        settings.Narrate(context);

        var tier = req.Tier ?? def.Tier;
        if (tier is < 1 or > BrandCatalog.MaxTier)
            return ChangeHandlerResult.Failure("tier must be 1–5.");
        BrandState.Apply(participant, character, id!, tier, req.SourceId, req.Payload, req.Concubi, context);
        context.Publish(
            Events.LewdEvents.BrandChanged,
            new { characterId = req.TargetId, brandId = id, action = "apply", tier });
        return ChangeHandlerResult.Ok;
    }
}
