using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdInsertHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdInsertChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdInsertChange)change;
        if (string.IsNullOrWhiteSpace(req.ActorId) || string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("actorId and targetId are required.");
        if (string.IsNullOrWhiteSpace(req.Orifice))
            return ChangeHandlerResult.Failure("orifice is required (pussy, ass, or mouth).");
        if (!AgeGate.TryPassAll(context, out var ageError, req.ActorId, req.TargetId))
            return ChangeHandlerResult.Failure(ageError!);

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!settings.InsertedToys)
            return ChangeHandlerResult.Failure("lewdInsertedToys is off: occupancy verbs do nothing until the player enables them.");

        context.Characters.TryGetValue(req.TargetId, out var targetChar);
        if (targetChar is null)
            return ChangeHandlerResult.Failure($"Unknown target '{req.TargetId}'.");

        var participant = LewdModeAccess.TryGetParticipant(context, req.TargetId)
                         ?? new ModeParticipantState { CharacterId = req.TargetId };
        var tags = new List<string> { "insertion", "toys", OccupancyGraph.NormalizeOrifice(req.Orifice) };
        var kindGuess = OccupancyGraph.NormalizeKind(req.Kind, req.ItemId);
        tags.Add(kindGuess);
        if (kindGuess is LewdKeys.OccupancyPlug or LewdKeys.OccupancyBeads)
            tags.Add("plugs");
        if (kindGuess == LewdKeys.OccupancyBeads)
            tags.Add("beads");
        if (OccupancyGraph.NormalizeOrifice(req.Orifice) == "ass")
            tags.Add("anal");

        if (settings.HitsHardLimit(LewdSettings.InsertHardLimitTags.Concat(tags), out var hit))
            return ChangeHandlerResult.Failure($"Campaign hard limit '{hit}' blocks insertion.");
        if (!ConsentGate.AuthorizeEffect(participant, targetChar, req.TargetId, req.ActorId, tags, settings, out _, out var gateError))
            return ChangeHandlerResult.Failure(gateError!);

        string? itemName = null;
        if (!string.IsNullOrWhiteSpace(req.ItemId) && context.Items.TryGetValue(req.ItemId, out var item))
            itemName = item.Name ?? item.DefinitionName;

        var entry = new OccupancyEntry
        {
            Orifice = OccupancyGraph.NormalizeOrifice(req.Orifice),
            Kind = OccupancyGraph.NormalizeKind(req.Kind, itemName ?? req.ItemId),
            Seal = string.IsNullOrWhiteSpace(req.Seal)
                ? OccupancyGraph.SealForKind(OccupancyGraph.NormalizeKind(req.Kind, itemName ?? req.ItemId))
                : req.Seal.Trim().ToLowerInvariant(),
            ItemId = req.ItemId,
            SourceId = req.SourceId ?? (kindGuess == LewdKeys.OccupancyPartner ? req.ActorId : null),
            Label = req.Label ?? itemName,
            AppliedById = req.ActorId,
            BeadStages = Math.Max(0, req.BeadStages),
            BeadStage = req.BeadStage ?? Math.Max(0, req.BeadStages),
        };
        if (entry.Kind == LewdKeys.OccupancyBeads && entry.BeadStages <= 0)
        {
            entry.BeadStages = 5;
            entry.BeadStage = 5;
        }

        var list = OccupancyGraph.Get(participant, targetChar);
        OccupancyGraph.AddOrReplace(list, entry);
        OccupancyGraph.Set(participant, targetChar, list);
        Occupancy.Sync(targetChar);

        context.RecordMessage(
            $"{req.TargetId}: {entry.Orifice} now holds {entry.Kind} [{entry.Id}] seal={entry.Seal}" +
            (entry.ItemId is null ? "" : $" item={entry.ItemId}") + ".");
        settings.Narrate(context);
        return ChangeHandlerResult.Ok;
    }
}

public sealed class LewdRemoveHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdRemoveChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdRemoveChange)change;
        if (string.IsNullOrWhiteSpace(req.ActorId) || string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("actorId and targetId are required.");
        if (!AgeGate.TryPassAll(context, out var ageError, req.ActorId, req.TargetId))
            return ChangeHandlerResult.Failure(ageError!);

        context.Characters.TryGetValue(req.TargetId, out var targetChar);
        if (targetChar is null)
            return ChangeHandlerResult.Failure($"Unknown target '{req.TargetId}'.");

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var participant = LewdModeAccess.TryGetParticipant(context, req.TargetId)
                         ?? new ModeParticipantState { CharacterId = req.TargetId };
        var list = OccupancyGraph.Get(participant, targetChar);
        if (list.Count == 0)
            return ChangeHandlerResult.Failure($"{req.TargetId} has nothing seated.");

        OccupancyEntry? entry = null;
        if (!string.IsNullOrWhiteSpace(req.OccupancyId))
            entry = list.FirstOrDefault(e => string.Equals(e.Id, req.OccupancyId, StringComparison.OrdinalIgnoreCase));
        else if (!string.IsNullOrWhiteSpace(req.Orifice))
            entry = OccupancyGraph.At(list, req.Orifice);
        else if (!string.IsNullOrWhiteSpace(req.ItemId))
            entry = list.FirstOrDefault(e => string.Equals(e.ItemId, req.ItemId, StringComparison.OrdinalIgnoreCase));
        else if (list.Count == 1)
            entry = list[0];

        if (entry is null)
            return ChangeHandlerResult.Failure("Name occupancyId, orifice, or itemId (or leave only one occupancy).");

        // Partial bead pull: lower stage, leak, keep entry if stages remain.
        if (entry.Kind == LewdKeys.OccupancyBeads && req.BeadPull is > 0 && entry.BeadStage > req.BeadPull)
        {
            entry.BeadStage -= req.BeadPull.Value;
            OccupancyGraph.Set(participant, targetChar, list);
            Occupancy.Sync(targetChar);
            var beadLeaks = LewdLeak.Tick(targetChar, settings, "bead_pull", amount: req.BeadPull.Value, orificeFilter: entry.Orifice);
            LewdLeak.RecordLeakMessages(context, targetChar, beadLeaks, "bead pull");
            LewdLeak.PublishSoils(context, targetChar.Id, beadLeaks);
            context.RecordMessage($"{req.TargetId}: pulled {req.BeadPull} bead(s); {entry.BeadStage}/{entry.BeadStages} remain.");
            return ChangeHandlerResult.Ok;
        }

        var orifice = entry.Orifice;
        list.RemoveAll(e => e.Id == entry.Id);
        OccupancyGraph.Set(participant, targetChar, list);
        Occupancy.Sync(targetChar);

        var soils = LewdLeak.Tick(targetChar, settings, "unplug", amount: 2, orificeFilter: orifice);
        LewdLeak.RecordLeakMessages(context, targetChar, soils, "removal");
        LewdLeak.PublishSoils(context, targetChar.Id, soils);

        context.RecordMessage($"{req.TargetId}: removed {entry.Kind} from {orifice} [{entry.Id}].");
        settings.Narrate(context);
        return ChangeHandlerResult.Ok;
    }

}

/// <summary>Turns published leak payloads into real core <c>soil</c> follow-ups in the same commit.</summary>
public sealed class LewdLeakHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [Events.LewdEvents.Leak];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.Data.TryGetValue("soils", out var soilsEl) || soilsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        var changes = new List<WorldChange>();
        foreach (var row in soilsEl.EnumerateArray())
        {
            var targetId = row.TryGetProperty("targetId", out var tid) ? tid.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(targetId))
                continue;
            changes.Add(new SoilChange
            {
                TargetId = targetId,
                Kind = row.TryGetProperty("kind", out var k) ? k.GetString() ?? LewdKeys.DirtKindCum : LewdKeys.DirtKindCum,
                Amount = row.TryGetProperty("amount", out var a) && a.TryGetInt32(out var n) ? n : 1,
                Spot = row.TryGetProperty("spot", out var s) ? s.GetString() : "thighs",
                AppliedBy = row.TryGetProperty("appliedBy", out var by) ? by.GetString() : "lewd_leak",
                Note = row.TryGetProperty("note", out var note) ? note.GetString() : null,
            });
        }

        return Task.FromResult<IReadOnlyList<WorldChange>>(changes);
    }
}
