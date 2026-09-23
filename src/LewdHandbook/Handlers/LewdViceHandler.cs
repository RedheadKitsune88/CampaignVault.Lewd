using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdViceHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdViceChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdViceChange)change;
        if (string.IsNullOrWhiteSpace(req.CharacterId))
            return ChangeHandlerResult.Failure("characterId is required.");
        if (!context.Characters.TryGetValue(req.CharacterId, out var character))
            return ChangeHandlerResult.Failure($"Character '{req.CharacterId}' is not in the commit context.");

        ViceState.ApplyPendingBadEnd(character, context);

        var action = ViceCatalog.Normalize(req.Action);
        if (action is not ("consume" or "resist" or "note_presence" or "rest"))
            return ChangeHandlerResult.Failure("action must be consume, resist, note_presence, or rest.");

        if (string.IsNullOrWhiteSpace(req.ViceId) || !ViceCatalog.TryGet(req.ViceId, out var def))
            return ChangeHandlerResult.Failure("viceId must be sex, sexual_fluids, alcohol, or succubus_venom.");

        var ability = ViceCatalog.AbilityFor(def, req.Ability);
        if (def.Kind == "complex" && string.IsNullOrWhiteSpace(req.Ability))
            return ChangeHandlerResult.Failure("complex vices require ability (con, wis, or cha).");

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.CharacterId, StringComparison.OrdinalIgnoreCase));
        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);

        return action switch
        {
            "consume" => await ConsumeAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            "resist" => await ResistAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            "note_presence" => NotePresence(character, def, now, req.InPresence, context),
            "rest" => await RestAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            _ => ChangeHandlerResult.Failure("unknown action."),
        };
    }

    private static async Task<ChangeHandlerResult> ConsumeAsync(
        LewdViceChange req,
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        string ability,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        var became = false;
        if (!ViceState.IsAddicted(character, def.Id))
        {
            var mod = AbilityScores.Resolve(character, ability, req.AbilityMod);
            // First addiction save is standard. Disadv applies once already addicted (resist / rest / presence).
            var die = await SaveDice.RollAsync(
                context, "lewd_vice_addiction", req.D20, mod, disadvantage: false, ct).ConfigureAwait(false);
            if (die.Error is not null)
                return ChangeHandlerResult.Failure(die.Error);

            if (!character.SystemStats.Attributes.ContainsKey(ViceState.DcKey(def.Id)))
                ViceState.SetAttr(character, ViceState.DcKey(def.Id), def.BaseDc);
            if (!character.SystemStats.Attributes.ContainsKey(ViceState.BaseDcKey(def.Id)))
                ViceState.SetAttr(character, ViceState.BaseDcKey(def.Id), def.BaseDc);

            var dc = ViceState.CurrentDc(character, def);
            if (dc <= 0)
                dc = def.BaseDc;
            if (die.Total < dc)
            {
                became = true;
                context.RecordMessage(
                    $"{req.CharacterId} vice {def.Id} addiction save {die.Summary} < DC {dc}. Addicted.");
            }
            else
            {
                context.RecordMessage(
                    $"{req.CharacterId} vice {def.Id} addiction save {die.Summary} ≥ DC {dc}. Not addicted.");
            }
        }

        ViceState.Consume(character, participant, def, now, ability, became, context, req.ItemId);
        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult> ResistAsync(
        LewdViceChange req,
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        string ability,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        ViceState.SyncWithdrawal(character, def, now, context);
        if (!ViceState.IsAddicted(character, def.Id))
            return ChangeHandlerResult.Failure($"Not addicted to '{def.Id}'.");
        if (!ViceState.IsWithdrawal(character, def.Id) && !req.InPresence)
            return ChangeHandlerResult.Failure("resist requires withdrawal or inPresence=true.");

        var mod = AbilityScores.Resolve(character, ability, req.AbilityMod);
        var die = await SaveDice.RollAsync(
            context, "lewd_vice_resist", req.D20, mod, disadvantage: true, ct).ConfigureAwait(false);
        if (die.Error is not null)
            return ChangeHandlerResult.Failure(die.Error);
        var dc = ViceState.CurrentDc(character, def);
        if (die.Total < dc)
        {
            context.RecordMessage(
                $"{req.CharacterId} vice {def.Id} resist {ability} save {die.Summary} < DC {dc} (disadv). Narrate giving in and emit lewd_vice action=consume.");
            return ChangeHandlerResult.Ok;
        }

        context.RecordMessage(
            $"{req.CharacterId} vice {def.Id} resist {ability} save {die.Summary} ≥ DC {dc} (disadv). Withdrawal remains until a partake.");
        _ = participant;
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult NotePresence(
        Character character,
        ViceDef def,
        float now,
        bool inPresence,
        IChangeContext context)
    {
        ViceState.SyncWithdrawal(character, def, now, context);
        if (ViceState.IsAddicted(character, def.Id) && (ViceState.IsWithdrawal(character, def.Id) || inPresence))
        {
            ViceState.AppendThought(character, def.Id);
            context.RecordMessage(
                $"{character.Id} vice {def.Id} presence temptation. Narrate the intrusive thought, then emit lewd_vice action=resist or consume.");
        }

        return ChangeHandlerResult.Ok;
    }

    private static async Task<ChangeHandlerResult> RestAsync(
        LewdViceChange req,
        Character character,
        ModeParticipantState? participant,
        ViceDef def,
        string ability,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        ViceState.SyncWithdrawal(character, def, now, context);
        if (!ViceState.IsAddicted(character, def.Id) || !ViceState.IsWithdrawal(character, def.Id))
        {
            context.RecordMessage($"{req.CharacterId} vice {def.Id} rest: no withdrawal save.");
            return ChangeHandlerResult.Ok;
        }

        var mod = AbilityScores.Resolve(character, ability, req.AbilityMod);
        var die = await SaveDice.RollAsync(
            context, "lewd_vice_rest", req.D20, mod, disadvantage: true, ct).ConfigureAwait(false);
        if (die.Error is not null)
            return ChangeHandlerResult.Failure(die.Error);
        var dc = ViceState.CurrentDc(character, def);
        if (die.Total < dc)
        {
            ViceState.FailWithdrawal(character, participant, def, die.Face, context);
            return ChangeHandlerResult.Ok;
        }

        context.RecordMessage(
            $"{req.CharacterId} vice {def.Id} withdrawal save {die.Summary} ≥ DC {dc} (disadv).");
        ViceState.TryCleanOnRestSuccess(character, def, context);
        return ChangeHandlerResult.Ok;
    }
}
