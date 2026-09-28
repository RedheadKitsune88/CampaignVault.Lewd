using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdDeconditionHandler : IWorldChangeHandler
{
    private static readonly string[] Methods = ["therapy", "aftercare", "rest"];

    public bool ShouldHandle(WorldChange change) => change is LewdDeconditionChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdDeconditionChange)change;
        if (string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        var category = ImprintMath.Normalize(req.Category);
        if (!ImprintMath.IsTrack(category))
            return ChangeHandlerResult.Failure("category must be wanton, training, breeding, ordeal, or cruelty.");
        var method = ImprintMath.Normalize(req.Method);
        if (!Methods.Contains(method))
            return ChangeHandlerResult.Failure("method must be therapy, aftercare, or rest.");
        if (!context.Characters.TryGetValue(req.TargetId, out var character))
            return ChangeHandlerResult.Failure($"Target '{req.TargetId}' is not in the commit context.");

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.TargetId, StringComparison.OrdinalIgnoreCase));
        var wisMod = AbilityScores.Resolve(character, "wis", req.WisMod);
        var die = await SaveDice.RollAsync(
            context, "lewd_decondition", req.D20, abilityMod: 0, disadvantage: false, ct,
            who: character, subject: "wis", tags: ["mental"]).ConfigureAwait(false);
        if (die.Error is not null)
            return ChangeHandlerResult.Failure(die.Error);
        // Only the roll pipeline contributes to the total here (the Wis modifier is added by Decondition); fold it in.
        wisMod += die.Total - die.Face;

        var advantage = method == "aftercare" || HasDevoted(character);
        var face = die.Face;
        if (advantage)
        {
            if (req.D20Other is >= 1 and <= 20)
                face = Math.Max(face, req.D20Other.Value);
            else if (context.Rolls is not null)
            {
                var second = await SaveDice.RollAsync(
                    context, "lewd_decondition_adv", faceOrZero: 0, abilityMod: 0, disadvantage: false, ct)
                    .ConfigureAwait(false);
                if (second.Error is null)
                    face = Math.Max(face, second.Face);
            }
            else
            {
                context.RecordMessage($"{req.TargetId} decondition advantage not rolled (no d20Other, no Rolls).");
            }
        }

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        var levelBefore = ImprintState.Level(character, category);
        var note = ImprintState.Decondition(participant, character, category, method, face, wisMod, day, out var error);
        if (note is not null)
        {
            context.Publish(
                Events.LewdEvents.ImprintChanged,
                new
                {
                    characterId = req.TargetId,
                    category,
                    level = ImprintState.Level(character, category),
                    action = "decondition",
                });
        }
        if (note is null)
            return ChangeHandlerResult.Failure(error);
        context.RecordMessage(note);
        if (ImprintState.Level(character, category) < levelBefore)
        {
            var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
            var now = await LewdClock.NowDaysAsync(context).ConfigureAwait(false) ?? day;
            if (LewdMood.TryGrant(character, LewdMood.Relief, category, day, now, settings) is { } mood)
                context.RecordMessage(mood);
        }

        return ChangeHandlerResult.Ok;
    }

    private static bool HasDevoted(Character character)
    {
        var history = AnatomyTraits.GetSexualHistory(character);
        if (history is not null && history.Contains("devoted", StringComparison.OrdinalIgnoreCase))
            return true;
        return character.SystemStats.Traits.Keys.Any(k =>
            k.Contains("devoted_partner", StringComparison.OrdinalIgnoreCase));
    }
}
