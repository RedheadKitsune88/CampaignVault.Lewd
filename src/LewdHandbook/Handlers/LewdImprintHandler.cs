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
        if (!ImprintMath.IsSource(source))
            return ChangeHandlerResult.Failure("source must be training, wanton, bad_end, cruelty, or exposure.");
        if (!context.Characters.TryGetValue(req.TargetId, out var character))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the commit context.");

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.TargetId, StringComparison.OrdinalIgnoreCase));
        if (participant is not null &&
            string.Equals(ConsentGate.GetString(participant, LewdKeys.Consent), LewdKeys.ConsentRevoked, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure("consent revoked.");
        if (ImprintState.HardBlocked(participant, category, req.Tags))
            return ChangeHandlerResult.Failure($"Hard limit blocks imprint '{category}'.");

        var tone = await IntimacyTone.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!req.Willing && !req.Accept && tone == IntimacyToneKind.Consensual)
            return ChangeHandlerResult.Failure("Unwilling lewd_imprint requires intimacyTone fade or grimdark, or willing=true.");
        if (!req.Willing && tone == IntimacyToneKind.Fade)
        {
            context.RecordPhysicalStateNudge(
                $"{req.TargetId} imprint under intimacyTone=fade; narrate the lean, not a graphic conditioning scene.");
        }

        if (req.Accept)
        {
            var consent = participant is null
                ? LewdKeys.ConsentWilling
                : ConsentGate.GetString(participant, LewdKeys.Consent) ?? LewdKeys.ConsentWilling;
            if (string.Equals(consent, LewdKeys.ConsentUnwilling, StringComparison.OrdinalIgnoreCase))
                return ChangeHandlerResult.Failure("accept requires consent willing or selective.");
        }

        var ability = ImprintMath.Normalize(req.Ability);
        if (ability is not ("wis" or "int"))
            return ChangeHandlerResult.Failure("ability must be wis or int.");

        var delta = req.Delta ?? (req.Accept ? 0 : 1);
        if (req.Accept && req.Delta is null)
            delta = 0;
        if (delta is < 0 or > 3)
            return ChangeHandlerResult.Failure("delta must be 0–3.");

        var pending = PregnancyState.Text(character, LewdKeys.BadEndImprintTrack);
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
            ImprintState.Tick(participant, character, category, req.Willing || req.Accept, delta, day, req.Accept, context);
        return ChangeHandlerResult.Ok;
    }

    internal static async Task<(int Value, string? Error)> DieAsync(
        int d20,
        IChangeContext context,
        string tag,
        CancellationToken ct)
    {
        if (d20 is >= 1 and <= 20)
            return (d20, null);
        if (context.Rolls is not null && d20 == 0)
        {
            var roll = await context.Rolls.RollAsync(new RollRequest { Tag = tag, Expression = "1d20" }, ct).ConfigureAwait(false);
            var face = roll.IndividualDice.FirstOrDefault();
            var die = face is >= 1 and <= 20 ? face : Math.Clamp(roll.Result, 1, 20);
            context.RecordMessage($"Lewd imprint d20 ({tag}): {roll.Summary}.");
            return (die, null);
        }

        return (0, "d20 must be between 1 and 20 (or 0 with Rolls to auto-roll).");
    }
}
