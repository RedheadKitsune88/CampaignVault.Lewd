using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdImprintHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdImprintChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdImprintChange)change;
        if (string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        var category = ImprintMath.Normalize(req.Category);
        if (!ImprintMath.IsTrack(category))
            return ChangeHandlerResult.Failure("category must be wanton, training, breeding, ordeal, or cruelty.");
        var source = ImprintMath.Normalize(req.Source);
        if (req.SetLevel is null && !ImprintMath.IsSource(source))
            return ChangeHandlerResult.Failure("source must be training, wanton, bad_end, cruelty, or exposure.");
        if (!AgeGate.TryPassAll(context, out var ageError, req.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        var character = context.Characters[req.TargetId];

        var participant = LewdModeAccess.TryGetParticipant(context, req.TargetId);
        if (LewdProfile.IsRevoked(participant, character))
            return ChangeHandlerResult.Failure("consent revoked.");
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        if (ImprintState.HardBlocked(participant, character, settings, category, req.Tags))
            return ChangeHandlerResult.Failure($"Hard limit blocks imprint '{category}'.");

        if (req.SetLevel is { } setLevel)
        {
            if (setLevel is < 1 or > 3)
                return ChangeHandlerResult.Failure("setLevel must be 1–3.");
            var seedDay = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
            ImprintState.Seed(participant, character, category, setLevel, req.Willing, seedDay, context, req.AnchorId);
            PublishChanged(context, req.TargetId, character, category, "set");
            return ChangeHandlerResult.Ok;
        }

        if (!req.Willing && !req.Accept && !settings.AllowsUnwanted(character, req.TargetId, out var policyError))
            return ChangeHandlerResult.Failure($"Unwilling lewd_imprint: {policyError} Or set willing=true.");

        if (req.Accept && LewdProfile.Stance(participant, character) == LewdKeys.ConsentUnwilling)
            return ChangeHandlerResult.Failure("accept requires stance willing or selective.");

        var ability = ImprintMath.Normalize(req.Ability);
        if (ability is not ("wis" or "int"))
            return ChangeHandlerResult.Failure("ability must be wis or int.");

        var delta = req.Delta ?? (req.Accept ? 0 : 1);
        if (req.Accept && req.Delta is null)
            delta = 0;
        if (delta is < 0 or > 3)
            return ChangeHandlerResult.Failure("delta must be 0–3.");

        var pending = PregnancyState.Text(character, LewdKeys.TraitBadEndImprintTrack);
        var existing = ImprintState.Find(character, category);
        if (req.Accept && existing is null && delta == 0 &&
            !string.Equals(pending, category, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure($"No imprint track '{category}' to accept.");

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);

        if (!req.Willing && !req.Accept && delta > 0)
        {
            var mod = AbilityScores.Resolve(character, ability, req.AbilityMod);
            var die = await SaveDice.RollAsync(
                context, "lewd_imprint_resist", req.D20, mod, disadvantage: false, ct).ConfigureAwait(false);
            if (die.Error is not null)
                return ChangeHandlerResult.Failure(die.Error);
            var dc = ImprintMath.ResistDc(ImprintState.Level(character, category), req.DcMod);
            if (die.Total >= dc)
            {
                context.RecordMessage(
                    $"{req.TargetId} imprint {category} {ability} save {die.Summary} ≥ DC {dc}. Tick negated.");
                return ChangeHandlerResult.Ok;
            }

            context.RecordMessage(
                $"{req.TargetId} imprint {category} {ability} save {die.Summary} < DC {dc}. Tick applies.");
        }

        if (req.Accept && !ImprintState.Accept(participant, character, category, day, context) && delta == 0)
            return ChangeHandlerResult.Failure($"No imprint track '{category}' to accept.");
        if (delta > 0)
            ImprintState.Tick(participant, character, category, req.Willing || req.Accept, delta, day, req.Accept, context, req.AnchorId);
        PublishChanged(context, req.TargetId, character, category, req.Accept ? "accept" : "tick");
        return ChangeHandlerResult.Ok;
    }

    private static void PublishChanged(IChangeContext context, string targetId, Character character, string category, string action) =>
        context.Publish(
            Events.LewdEvents.ImprintChanged,
            new { characterId = targetId, category, level = ImprintState.Level(character, category), action });
}
