using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using LewdHandbook.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>Bridges host mode events to the plugin's turn-start and scene-end verbs.</summary>
public sealed class LewdModeEventHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [CoreEvents.ModeTurnStarted, CoreEvents.ModeExited];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(CoreEvents.Fields.ModeId, out var modeId) ||
            !string.Equals(modeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        if (e.Topic == CoreEvents.ModeTurnStarted &&
            e.TryGet<string>(CoreEvents.Fields.CharacterId, out var characterId) &&
            !string.IsNullOrWhiteSpace(characterId))
        {
            return Task.FromResult<IReadOnlyList<WorldChange>>([new LewdTurnStartChange { CharacterId = characterId }]);
        }

        if (e.Topic == CoreEvents.ModeExited &&
            e.TryGet<List<string>>(CoreEvents.Fields.ParticipantIds, out var ids) && ids.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<WorldChange>>([new LewdSceneEndChange { ParticipantIds = ids }]);
        }

        return Task.FromResult<IReadOnlyList<WorldChange>>([]);
    }
}

public sealed class LewdTurnStartHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdTurnStartChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdTurnStartChange)change;
        var mode = LewdModeAccess.TryGetActive(context);
        var participant = LewdModeAccess.TryGetParticipant(context, req.CharacterId);
        if (mode is null || participant is null)
            return ChangeHandlerResult.Failure($"'{req.CharacterId}' is not in an active lewd_encounter.");
        context.Characters.TryGetValue(req.CharacterId, out var character);
        if (character is not null)
            RecoveryWindow.Close(character, RecoveryWindow.Climax); // the chance to spend dice on that climax has passed

        await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var notes = new List<string>();
        TickIncapacitation(participant, character, notes);
        TickEdging(participant, character, context, notes);
        HardenDueBindings(participant, character, mode.Round, notes);
        TickOccupancy(participant, character, context, notes);

        if (notes.Count > 0)
            context.RecordMessage($"{req.CharacterId} turn start (round {mode.Round}):{string.Concat(notes)}");
        return ChangeHandlerResult.Ok;
    }

    private static void TickOccupancy(
        ModeParticipantState participant, Character? character, IChangeContext context, List<string> notes)
    {
        if (character is null)
            return;
        var settings = LewdSettings.Peek(context) ?? LewdSettings.Default;
        if (settings.InsertedToys)
        {
            var occupied = OccupancyGraph.Get(participant, character);
            var stim = 0;
            foreach (var entry in occupied)
            {
                stim += entry.Kind switch
                {
                    LewdKeys.OccupancyWand => 2,
                    LewdKeys.OccupancyPhallic or LewdKeys.OccupancyPartner => 1,
                    LewdKeys.OccupancyBeads => Math.Max(1, entry.BeadStage / 2),
                    _ => 0,
                };
            }

            if (stim > 0)
            {
                var arousal = LewdPoolHelper.Arousal(character);
                var numbing = LewdPoolHelper.Numbing(character);
                var result = StimulationMath.Apply(arousal.Current, arousal.Max, numbing.Current, stim, isCritical: false);
                numbing.Current = result.NumbingAfter;
                arousal.Current = result.ArousalAfter;
                LewdPoolHelper.MirrorArousal(participant, arousal);
                notes.Add($" seated toys +{stim} stim (arousal {result.ArousalBefore}→{result.ArousalAfter}).");
            }
        }

        var soils = LewdLeak.Tick(character, settings, "turn");
        if (soils.Count > 0)
        {
            LewdLeak.RecordLeakMessages(context, character, soils, "turn");
            LewdLeak.PublishSoils(context, character.Id, soils);
            notes.Add(" leak tick.");
        }
    }

    private static void TickIncapacitation(ModeParticipantState participant, Character? character, List<string> notes)
    {
        if (!ConsentGate.GetBool(participant, LewdKeys.ClimaxIncapacitated))
            return;

        var turns = ConsentGate.GetInt(participant, LewdKeys.ClimaxIncapTurns);
        if (turns > 0)
        {
            participant.State[LewdKeys.ClimaxIncapTurns] = turns - 1;
            notes.Add(" still incapacitated after climax this turn.");
            return;
        }

        LewdPoolHelper.EndClimaxIncapacitation(participant, character);
        notes.Add(" climax incapacitation ends.");
    }

    private static void TickEdging(ModeParticipantState participant, Character? character, IChangeContext context, List<string> notes)
    {
        var arousal = character?.SystemStats.ResourcePools.GetValueOrDefault(LewdKeys.PoolArousal);
        var current = arousal?.Current ?? ConsentGate.GetInt(participant, LewdKeys.ArousalCurrentMirror);
        var max = arousal?.Max ?? ConsentGate.GetInt(participant, LewdKeys.ArousalMaxMirror);
        if (max > 0 && current >= max)
            LewdPoolHelper.SetEdging(participant, character, true);

        if (!ConsentGate.GetBool(participant, LewdKeys.Edging))
        {
            participant.State[LewdKeys.EdgingBeats] = 0;
            return;
        }

        var beats = ConsentGate.GetInt(participant, LewdKeys.EdgingBeats) + 1;
        participant.State[LewdKeys.EdgingBeats] = beats;
        notes.Add(" edging — make a climax save (lewd_climax_check).");

        var inhibition = LewdProfile.Inhibition(participant, character);
        var overstim = ConsentGate.GetInt(participant, LewdKeys.Overstimulation);
        if (OverstimMath.ExtendedEdgingOverstim(beats, inhibition, overstim) is not int level)
            return;

        var source = ConsentGate.GetString(participant, LewdKeys.OverstimSource);
        LewdPoolHelper.SetOverstimulation(participant, character, level, source, context);
        notes.Add($" extended edging raises overstimulation to {level}.");
    }

    private static void HardenDueBindings(ModeParticipantState participant, Character? character, int round, List<string> notes)
    {
        var bindings = BindingGraph.GetBindings(participant, character);
        var due = bindings.Where(b => !b.Hardened && b.HardenAtRound is { } at && round >= at).ToList();
        if (due.Count == 0)
            return;
        foreach (var binding in due)
            LewdBindHandler.Harden(binding);
        BindingGraph.SetBindings(participant, character, bindings);
        if (character is not null)
            Restraint.Sync(character, bindings);
        notes.Add($" {string.Join(", ", due.Select(b => b.Kind))} hardened (escape/break DC 25).");
    }
}

public sealed class LewdSceneEndHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdSceneEndChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdSceneEndChange)change;
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        var now = await LewdClock.NowDaysAsync(context).ConfigureAwait(false) ?? day;
        var sceneKey = string.Join("+", req.ParticipantIds.Where(i => !string.IsNullOrWhiteSpace(i)).OrderBy(i => i, StringComparer.OrdinalIgnoreCase));
        foreach (var id in req.ParticipantIds.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            if (!context.Characters.TryGetValue(id, out var character))
                continue;

            LewdPoolHelper.EndClimaxIncapacitation(null, character);
            LewdPoolHelper.SetEdging(null, character, false);
            RecoveryWindow.Close(character, RecoveryWindow.Climax);
            LewdPoolHelper.SyncOverstimCascade(character, LewdPoolHelper.SheetOverstimLevel(character));
            await ImprintState.ResolveSceneAsync(context, character, ct).ConfigureAwait(false);
            Humiliation.ClearRecent(character);

            // Afterglow only for a scene that went somewhere and that this character wanted; never after a bad end.
            var participant = LewdModeAccess.TryGetParticipant(context, id);
            if (participant is not null && ConsentGate.GetBool(participant, LewdKeys.HadPhysical) &&
                !BadEndState.IsMarked(participant, character) && LewdProfile.Stance(participant, character) != LewdKeys.ConsentUnwilling &&
                AgeGate.TryPass(character, id, out _) &&
                LewdMood.TryGrant(character, LewdMood.Afterglow, sceneKey, day, now, settings) is { } mood)
                context.RecordMessage(mood);

            var bound = BindingGraph.GetBindings(null, character).Count;
            if (bound > 0)
                context.RecordMessage($"{id} is still bound ({bound} binding(s)); lewd_unbind frees them, in or out of a scene.");
        }

        return ChangeHandlerResult.Ok;
    }
}

/// <summary>
/// Brand of Echoes: the bearer climaxes the moment a creature within 5 ft of it climaxes. On this plugin's own
/// climax.v1 this names the characters who could be close — the scene's participants and whoever the climaxer is bound
/// to, engaged with or positioned by — in a <see cref="LewdEchoCheckChange"/>, so the host loads them before
/// <see cref="LewdEchoCheckHandler"/> looks for bearers.
/// </summary>
public sealed class LewdEchoesHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [LewdEvents.Climax];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(LewdEvents.Fields.Outcome, out var outcome) || outcome != "climax" ||
            !e.TryGet<string>(LewdEvents.Fields.CharacterId, out var climaxId) || string.IsNullOrWhiteSpace(climaxId))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (LewdModeAccess.TryGetActive(ctx) is { } mode)
            candidates.UnionWith(mode.Participants.Select(p => p.CharacterId));
        if (ctx.Characters.TryGetValue(climaxId, out var climaxer))
        {
            candidates.UnionWith(BindingGraph.GetBindings(null, climaxer).Select(b => b.AnchorId).OfType<string>());
            candidates.UnionWith(climaxer.SystemStats.EngagementRelations.Select(r => r.TargetId));
            candidates.UnionWith(climaxer.SystemStats.SpatialPositions.Select(p => p.TargetId));
        }

        candidates.UnionWith(ctx.Characters.Keys);
        candidates.Remove(climaxId);
        var ids = candidates.Where(id => id.StartsWith(CampaignVault.Data.CanonicalId.Characters, StringComparison.OrdinalIgnoreCase) ||
                                         ctx.Characters.ContainsKey(id) ||
                                         LewdModeAccess.TryGetParticipant(ctx, id) is not null).ToList();
        return Task.FromResult<IReadOnlyList<WorldChange>>(
            ids.Count == 0 ? [] : [new LewdEchoCheckChange { ClimaxedId = climaxId, CandidateIds = ids }]);
    }
}

public sealed class LewdEchoCheckHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdEchoCheckChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdEchoCheckChange)change;
        if (!context.Characters.TryGetValue(req.ClimaxedId, out var climaxer))
            return ChangeHandlerResult.Ok;

        foreach (var id in req.CandidateIds)
        {
            if (!context.Characters.TryGetValue(id, out var bearer) || !BrandState.Has(bearer, BrandCatalog.Echoes))
                continue;
            switch (Proximity.WithinFiveFeet(bearer, climaxer))
            {
                case true:
                    var forced = await new LewdClimaxCheckHandler().ApplyAsync(
                        new LewdClimaxCheckChange { TargetId = bearer.Id, ForceClimax = true }, context, enforceConsent: false, ct).ConfigureAwait(false);
                    if (!forced.Success)
                        context.RecordMessage($"{bearer.Id} Brand of Echoes: forced climax did not resolve ({forced.Message}).");
                    break;
                case null:
                    context.RecordMessage(
                        $"{bearer.Id} has Brand of Echoes. If within 5 ft of {req.ClimaxedId}'s climax, emit lewd_climax_check forceClimax=true.");
                    break;
            }
        }

        return ChangeHandlerResult.Ok;
    }
}
