using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdBadEndHandler : IWorldChangeHandler
{
    private static readonly string[] Consequences =
    [
        "level_drain", "slave", "seedbed", "curse", "lustbrand", "class_change", "imprint", "narrated", "vice",
    ];

    private static readonly string[] Tracks = ["wanton", "training", "breeding", "ordeal", "cruelty"];

    public bool ShouldHandle(WorldChange change) => change is LewdBadEndChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdBadEndChange)change;
        if (string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        if (!req.NoEscape)
            return ChangeHandlerResult.Failure("lewd_bad_end requires noEscape=true.");
        if (!BadEndMath.IsVerbReason(req.Reason))
            return ChangeHandlerResult.Failure("reason must be defeat or explicit.");

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return ChangeHandlerResult.Failure("lewd_bad_end requires an active lewd_encounter.");
        }

        var participant = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.TargetId, StringComparison.OrdinalIgnoreCase));
        if (participant is null)
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the lewd encounter.");
        if (string.Equals(ConsentGate.GetString(participant, LewdKeys.Consent), LewdKeys.ConsentRevoked, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure("consent revoked.");
        if (!context.Characters.TryGetValue(req.TargetId, out var character))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the commit context.");

        var tone = await IntimacyTone.ResolveAsync(context, ct).ConfigureAwait(false);
        if (tone == IntimacyToneKind.Consensual)
            return ChangeHandlerResult.Failure("lewd_bad_end requires intimacyTone fade or grimdark.");

        var consequence = string.IsNullOrWhiteSpace(req.Consequence) ? null : req.Consequence.Trim().ToLowerInvariant();
        if (consequence is not null && !Consequences.Contains(consequence))
            return ChangeHandlerResult.Failure($"Unknown bad-end consequence '{req.Consequence}'.");

        string? track = null;
        var jump = 3;
        if (consequence == "imprint")
        {
            track = req.ImprintTrack?.Trim().ToLowerInvariant();
            if (track is null || !Tracks.Contains(track))
                return ChangeHandlerResult.Failure("imprint requires imprintTrack: wanton, training, breeding, ordeal, or cruelty.");
            jump = req.ImprintJump ?? 3;
            if (jump is < 1 or > 3)
                return ChangeHandlerResult.Failure("imprintJump must be 1–3.");
        }

        if (consequence == "vice" && string.IsNullOrWhiteSpace(req.ViceId))
            return ChangeHandlerResult.Failure("vice requires viceId.");

        if (tone == IntimacyToneKind.Fade)
        {
            context.RecordPhysicalStateNudge(
                $"{req.TargetId} bad-end recorded under intimacyTone=fade; narrate a fade, not a graphic defeat.");
        }

        var reason = req.Reason.Trim().ToLowerInvariant();
        BadEndState.Apply(participant, character, reason, sourceId: "lewd_bad_end", consequence, context);
        if (track is not null)
        {
            BadEndState.StoreImprint(participant, character, track, jump, req.ImprintWilling ? "willing" : "unwilling");
            context.RecordMessage(
                $"{req.TargetId} imprint jump stored ({track} → {jump}). The next imprint write applies it and clears the pending jump. This commit did not write tracks.");
        }
        if (consequence == "vice")
            BadEndState.StoreVice(participant, character, req.ViceId!.Trim());
        return ChangeHandlerResult.Ok;
    }
}
