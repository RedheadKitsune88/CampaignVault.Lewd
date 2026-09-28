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
        if (action is not ("consume" or "resist" or "note_presence" or "rest" or "treat"))
            return ChangeHandlerResult.Failure("action must be consume, note_presence, resist, rest, or treat.");

        if (string.IsNullOrWhiteSpace(req.ViceId) || !ViceCatalog.TryGet(req.ViceId, out var def))
            return ChangeHandlerResult.Failure("viceId must be sex, sexual_fluids, alcohol, or succubus_venom.");

        if (def.Id != ViceCatalog.Alcohol && !AgeGate.TryPass(character, req.CharacterId, out var ageError))
            return ChangeHandlerResult.Failure(ageError!);

        if (def.Id != ViceCatalog.Alcohol && action == "consume")
        {
            var partic = LewdModeAccess.TryGetParticipant(context, req.CharacterId);
            var viceSettings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
            if (LewdProfile.IsRevoked(partic, character))
                return ChangeHandlerResult.Failure($"'{req.CharacterId}' has revoked consent.");
            var probe = new[] { def.Id, "vice", "addiction" };
            var limit = viceSettings.HitsHardLimit(probe, out var campaignHit)
                ? campaignHit
                : LewdProfile.HardLimits(partic, character).FirstOrDefault(l => probe.Contains(l, StringComparer.OrdinalIgnoreCase));
            if (limit is not null)
                return ChangeHandlerResult.Failure($"Hard limit '{limit}' blocks {def.Id}.");
        }

        var ability = ViceCatalog.AbilityFor(def, req.Ability ?? PregnancyState.Text(character, ViceState.AbilityKey(def.Id)));
        if (def.Kind == "complex" && action == "consume" && string.IsNullOrWhiteSpace(req.Ability) &&
            !ViceState.IsAddicted(character, def.Id))
            return ChangeHandlerResult.Failure("complex vices require ability (con, wis, or cha) for the first addiction save.");

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, req.CharacterId, StringComparison.OrdinalIgnoreCase));
        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);

        return action switch
        {
            "consume" => await ConsumeAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            "resist" => await ResistAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            "note_presence" => await NotePresenceAsync(req, character, def, ability, now, context, ct).ConfigureAwait(false),
            "rest" => await RestAsync(req, character, participant, def, ability, now, context, ct)
                .ConfigureAwait(false),
            "treat" => Treat(req, character, def),
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
                context, "lewd_vice_addiction", req.D20, mod, disadvantage: false, ct,
                who: character, subject: ability, tags: ["mental"]).ConfigureAwait(false);
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
        if (became)
            ViceState.PublishState(context, character, def, "addicted");
        return ChangeHandlerResult.Ok;
    }

    /// <summary>An explicit temptation save (in withdrawal, or with the vice right there).</summary>
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

        var (resisted, message) = await ViceTrack.PresenceSaveAsync(context, character, def, ability, req.D20, req.AbilityMod, ct)
            .ConfigureAwait(false);
        if (resisted is null)
            return ChangeHandlerResult.Failure(message);
        context.RecordMessage(message);
        _ = participant;
        return ChangeHandlerResult.Ok;
    }

    /// <summary>
    /// The vice is at hand. In withdrawal the handbook requires a save (rolled here when the host can, or with d20);
    /// merely addicted, it's the disadvantage reminder.
    /// </summary>
    private static async Task<ChangeHandlerResult> NotePresenceAsync(
        LewdViceChange req,
        Character character,
        ViceDef def,
        string ability,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        ViceState.SyncWithdrawal(character, def, now, context);
        if (!ViceState.IsAddicted(character, def.Id))
            return ChangeHandlerResult.Ok;

        if (!ViceState.IsWithdrawal(character, def.Id))
        {
            context.RecordMessage(
                $"{character.Id} is near {def.Id}: disadvantage on checks and saves involving it while it is present. " +
                "Not in withdrawal, so no compulsion save.");
            return ChangeHandlerResult.Ok;
        }

        ViceState.AppendThought(character, def.Id);
        if (req.D20 == 0 && context.Rolls is null && ViceTrack.Aid(character, def.Id) != ViceTrack.Auto)
        {
            context.RecordMessage(
                $"{character.Id} in withdrawal meets {def.Id}: addiction save required. Emit lewd_vice action=resist with d20.");
            return ChangeHandlerResult.Ok;
        }

        var (resisted, message) = await ViceTrack.PresenceSaveAsync(context, character, def, ability, req.D20, req.AbilityMod, ct)
            .ConfigureAwait(false);
        if (resisted is null)
            return ChangeHandlerResult.Failure(message);
        context.RecordMessage(message);
        return ChangeHandlerResult.Ok;
    }

    /// <summary>Fallback for the long-rest withdrawal save when the host could not roll it (no Rolls).</summary>
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

        var note = await ViceTrack.RestSaveAsync(context, character, participant, def, ability, req.D20, req.AbilityMod, ct)
            .ConfigureAwait(false);
        context.RecordMessage(note);
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Treat(LewdViceChange req, Character character, ViceDef def)
    {
        if (!ViceState.IsAddicted(character, def.Id))
            return ChangeHandlerResult.Failure($"Not addicted to '{def.Id}'.");
        var error = ViceTrack.Treat(character, def, req.Method ?? "", req.SlotLevel, out var message);
        return error is null ? new ChangeHandlerResult(true, message) : ChangeHandlerResult.Failure(error);
    }
}
