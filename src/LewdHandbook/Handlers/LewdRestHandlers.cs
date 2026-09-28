using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>A completed rest (core.rested.v1; interrupted rests publish nothing) becomes one ordered lewd_rest.</summary>
public sealed class LewdRestEventHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [CoreEvents.Rested];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(CoreEvents.Fields.CharacterId, out var characterId) || string.IsNullOrWhiteSpace(characterId))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);
        e.TryGet<string>(CoreEvents.Fields.RestType, out var restType);
        var kind = (restType ?? "").Contains("short", StringComparison.OrdinalIgnoreCase) ? "short" : "long";
        return Task.FromResult<IReadOnlyList<WorldChange>>([new LewdRestChange { CharacterId = characterId, RestType = kind }]);
    }
}

public sealed class LewdRestHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdRestChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var rest = (LewdRestChange)change;
        if (!context.Characters.TryGetValue(rest.CharacterId, out var character))
            return ChangeHandlerResult.Ok; // Not a character the engine loaded: nothing of ours on it.

        var longRest = !string.Equals(rest.RestType, "short", StringComparison.OrdinalIgnoreCase);
        var participant = LewdModeAccess.TryGetParticipant(context, rest.CharacterId);
        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);
        var notes = new List<string>();

        ViceState.ApplyPendingBadEnd(character, context);
        await ViceState.OnRestAsync(character, participant, longRest, now, context, ct).ConfigureAwait(false);

        BrandState.OnRest(character, participant, longRest, context);

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        ImprintState.ApplyPendingJump(participant, character, day, context);
        if (longRest)
        {
            await ImprintState.OnLongRestAsync(character, participant, context, ct).ConfigureAwait(false);
            Humiliation.ClearRecent(character);
        }

        if (PregnancyState.Sync(character, now, context) is { } progress)
            notes.Add(progress);
        if (await PregnancyRest.SaveAsync(context, character, now, 0, null, ct).ConfigureAwait(false) is { } poison)
            notes.Add(poison);

        RestArousal(character, participant, longRest, now, notes);

        if (notes.Count > 0)
            context.RecordMessage(string.Join(" ", notes));
        return ChangeHandlerResult.Ok;
    }

    /// <summary>Long rest: arousal −½ of its maximum (never below 0) and numbing cleared. Short rest: the recovery-dice window.</summary>
    private static void RestArousal(Character character, ModeParticipantState? participant, bool longRest, float now, List<string> notes)
    {
        var pools = character.SystemStats.ResourcePools;
        if (!pools.ContainsKey(LewdKeys.PoolArousal) && ArousalMath.RecoveryDieSides(character) is null)
            return; // Never touched by lewd mechanics: don't grow pools on every rester.

        var arousal = LewdPoolHelper.Arousal(character);
        if (longRest)
        {
            var before = arousal.Current;
            if (arousal.Max > 0)
                arousal.Current = StimulationMath.LongRestArousalReduction(arousal.Current, arousal.Max);
            if (pools.TryGetValue(LewdKeys.PoolNumbing, out var numbing) && numbing is not null && numbing.Current > 0)
                pools[LewdKeys.PoolNumbing] = numbing with { Current = 0 };
            RecoveryWindow.Close(character);
            if (before != arousal.Current)
                notes.Add($"{character.Id} long rest: arousal {before}→{arousal.Current}/{arousal.Max}.");
        }
        else if (arousal.Current > 0 &&
                 pools.TryGetValue(LewdKeys.PoolRecoveryDice, out var dice) && dice is { Current: > 0 } && arousal.Max > 0)
        {
            RecoveryWindow.Open(character, RecoveryWindow.ShortRest, now);
            notes.Add($"{character.Id} short rest: may spend up to {ArousalMath.ProficiencyBonus(character)} recovery dice (lewd_recover) to lower arousal {arousal.Current}/{arousal.Max}.");
        }

        if (participant is not null)
            LewdPoolHelper.MirrorArousal(participant, arousal);
    }
}

public sealed class LewdRecoverHandler : IWorldChangeHandler
{
    /// <summary>Handbook: while incapacitated by a climax, a free action each turn: Con save, DC 12 + climaxes in the last hour.</summary>
    private static async Task<ChangeHandlerResult> SaveAgainstIncapacitationAsync(
        LewdRecoverChange req, IChangeContext context, Character character, float now, CancellationToken ct)
    {
        var participant = LewdModeAccess.TryGetParticipant(context, character.Id);
        var incapacitated = (participant is not null && ConsentGate.GetBool(participant, LewdKeys.ClimaxIncapacitated)) ||
                            LewdPoolHelper.HasClimaxIncapacitation(character);
        if (!incapacitated)
            return ChangeHandlerResult.Failure($"{character.Id} is not incapacitated by a climax.");

        var dc = 12 + ClimaxLog.Count(character, now, ClimaxLog.Hour);
        var save = await SaveDice.RollAsync(
            context, "lewd_incap_recover", req.D20, AbilityScores.Mod(character, "con"), disadvantage: false, ct,
            who: character, subject: "con").ConfigureAwait(false);
        if (save.Error is not null)
            return ChangeHandlerResult.Failure(save.Error);

        var recovered = save.Total >= dc;
        if (recovered)
            LewdPoolHelper.EndClimaxIncapacitation(participant, character);
        context.RecordMessage(
            $"{character.Id} tries to shake off the climax: Con save {save.Summary} vs DC {dc} → {(recovered ? "no longer incapacitated" : "still incapacitated")}.");
        return ChangeHandlerResult.Ok;
    }

    public bool ShouldHandle(WorldChange change) => change is LewdRecoverChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdRecoverChange)change;
        if (!context.Characters.TryGetValue(req.CharacterId ?? "", out var character))
            return ChangeHandlerResult.Failure($"Character '{req.CharacterId}' is not in the commit context.");
        if (!AgeGate.TryPass(character, req.CharacterId!, out var ageError))
            return ChangeHandlerResult.Failure(ageError!);

        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);
        if (req.Save)
            return await SaveAgainstIncapacitationAsync(req, context, character, now, ct).ConfigureAwait(false);

        var window = RecoveryWindow.OpenKind(character, now);
        if (window is null)
            return ChangeHandlerResult.Failure(
                "Recovery dice are spent right after a climax (before that character's next turn) or within an hour of a short rest.");

        var arousal = LewdPoolHelper.Arousal(character);
        if (arousal.Max <= 0)
            return ChangeHandlerResult.Failure($"{character.Id}'s arousal maximum is 0 or lower: arousal can no longer be reduced (Bad-Ended).");
        if (!character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolRecoveryDice, out var dice) || dice is null)
            return ChangeHandlerResult.Failure($"{character.Id} has no recovery_dice pool.");
        if (ArousalMath.RecoveryDieSides(character) is not int sides)
            return ChangeHandlerResult.Failure(
                $"{character.Id} has no recovery die: set Trait {LewdKeys.TraitRecoveryDie} (e.g. d8) or {LewdKeys.TraitSexualHistory}.");

        var cap = ArousalMath.ProficiencyBonus(character);
        if (req.Dice < 1 || req.Dice > cap)
            return ChangeHandlerResult.Failure($"dice must be 1..{cap} (proficiency bonus).");
        if (req.Dice > dice.Current)
            return ChangeHandlerResult.Failure($"{character.Id} has only {dice.Current} recovery dice left.");

        var roll = await LewdDice.RollAsync(context, "lewd_recovery_dice", req.Dice, sides, req.Faces, ct).ConfigureAwait(false);
        var con = AbilityScores.Mod(character, "con");
        var reduction = Math.Max(0, roll.Total + con * req.Dice);
        var before = arousal.Current;
        arousal.Current = Math.Max(0, arousal.Current - reduction);
        character.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = dice with { Current = dice.Current - req.Dice };
        RecoveryWindow.Close(character);

        var participant = LewdModeAccess.TryGetParticipant(context, character.Id);
        var incap = "";
        if (participant is not null)
        {
            LewdPoolHelper.MirrorArousal(participant, arousal);
            if (window == RecoveryWindow.Climax && ConsentGate.GetBool(participant, LewdKeys.ClimaxIncapacitated))
            {
                // Handbook: incapacitated for as many rounds as dice spent.
                var turns = Math.Max(ConsentGate.GetInt(participant, LewdKeys.ClimaxIncapTurns), req.Dice);
                participant.State[LewdKeys.ClimaxIncapTurns] = turns;
                incap = $" Incapacitated for {turns} round(s).";
            }
        }

        context.RecordMessage(
            $"{character.Id} spends {req.Dice} recovery dice ({window}): {roll.Summary} + Con {con:+#;-#;+0}×{req.Dice} = −{reduction} arousal, " +
            $"{before}→{arousal.Current}/{arousal.Max}; {dice.Current - req.Dice} dice left.{incap}");
        return ChangeHandlerResult.Ok;
    }
}
