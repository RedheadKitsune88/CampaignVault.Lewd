using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdCleanupHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdCleanupChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdCleanupChange)change;
        if (string.IsNullOrWhiteSpace(req.ActorId) || string.IsNullOrWhiteSpace(req.TargetId))
            return ChangeHandlerResult.Failure("actorId and targetId are required.");
        if (!AgeGate.TryPassAll(context, out var ageError, req.ActorId, req.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        if (!context.Characters.TryGetValue(req.TargetId, out var targetChar))
            return ChangeHandlerResult.Failure($"Unknown target '{req.TargetId}'.");

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var participant = LewdModeAccess.TryGetParticipant(context, req.TargetId)
                         ?? new ModeParticipantState { CharacterId = req.TargetId };

        OccupancyGraph.SetDeposits(targetChar, []);
        if (req.RemoveToys)
        {
            OccupancyGraph.Set(participant, targetChar, []);
        }

        Occupancy.Sync(targetChar);

        // Clear engine sexual dirt via follow-up soil clears (same-commit).
        context.Publish(
            Events.LewdEvents.Cleanup,
            new
            {
                characterId = req.TargetId,
                actorId = req.ActorId,
                removeToys = req.RemoveToys,
                kinds = new[] { LewdKeys.DirtKindCum, LewdKeys.DirtKindFluids },
            });

        context.RecordMessage(
            $"{req.TargetId}: cleaned up (internal deposits cleared" +
            (req.RemoveToys ? ", toys removed" : "") +
            "; lewd dirt clear follow-ups queued).");
        settings.Narrate(context);
        return ChangeHandlerResult.Ok;
    }
}

/// <summary>Turns <c>cleanup.v1</c> into core <c>soil</c> clear follow-ups for <c>lewd.*</c> kinds.</summary>
public sealed class LewdCleanupSoilHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [Events.LewdEvents.Cleanup];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(Events.LewdEvents.Fields.CharacterId, out var id) || string.IsNullOrWhiteSpace(id))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        var kinds = new List<string> { LewdKeys.DirtKindCum, LewdKeys.DirtKindFluids };
        if (e.Data.TryGetValue("kinds", out var kindsEl) && kindsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            kinds = kindsEl.EnumerateArray()
                .Select(x => x.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<WorldChange>>(
            kinds.Select(k => (WorldChange)new SoilChange
            {
                TargetId = id,
                Kind = k,
                Clear = true,
                AppliedBy = "lewd_cleanup",
                Note = "wash",
            }).ToList());
    }
}
