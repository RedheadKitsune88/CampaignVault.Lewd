using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>
/// A scene ends when the world moves on: a participant travels away, is caught by an encounter, is dropped to 0 HP or dies,
/// combat starts around them, or they finish a rest. The plugin only listens to what core already publishes, then
/// asks core to exit the mode, which fires <c>core.mode_exited.v1</c> and the ordinary <c>lewd_scene_end</c> wrap-up.
/// </summary>
public sealed class LewdInterruptHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } =
    [
        CoreEvents.Traveled, CoreEvents.EncounterInterrupted, CoreEvents.CharacterDowned, CoreEvents.CharacterDied, CoreEvents.CombatStarted, CoreEvents.Rested,
    ];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (LewdModeAccess.TryGetActive(ctx) is not { } mode || Cause(e, mode) is not { } cause)
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        ctx.RecordMessage($"lewd_encounter ends: {cause}.");
        ctx.RecordPhysicalStateNudge($"The scene is interrupted: {cause}.");
        return Task.FromResult<IReadOnlyList<WorldChange>>(
            [new ModeTransitionChange { ModeId = LewdEncounterMode.ModeIdValue, Action = "exit" }]);
    }

    /// <summary>What interrupted the scene, or null when the event does not touch it.</summary>
    private static string? Cause(DomainEvent e, ModeEncounter mode)
    {
        bool InScene(string? id) =>
            !string.IsNullOrWhiteSpace(id) &&
            mode.Participants.Any(p => string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));

        e.TryGet<string>(CoreEvents.Fields.CharacterId, out var characterId);
        switch (e.Topic)
        {
            case CoreEvents.Traveled:
                e.TryGet<string>(CoreEvents.Fields.LocationId, out var arrivedAt);
                return InScene(characterId) && !string.Equals(arrivedAt, mode.LocationId, StringComparison.OrdinalIgnoreCase)
                    ? $"{characterId} left for {arrivedAt}"
                    : null;
            case CoreEvents.EncounterInterrupted:
                return InScene(characterId) ? $"{characterId} was interrupted by an encounter" : null;
            case CoreEvents.CharacterDowned:
                return InScene(characterId) ? $"{characterId} was knocked down" : null;
            case CoreEvents.CharacterDied:
                return InScene(characterId) ? $"{characterId} died" : null;
            case CoreEvents.Rested:
                return InScene(characterId) ? $"{characterId} went to rest" : null;
            case CoreEvents.CombatStarted:
                return e.TryGet<List<string>>(CoreEvents.Fields.CombatantIds, out var combatants) && combatants.Any(InScene)
                    ? "combat broke out"
                    : null;
            default:
                return null;
        }
    }
}
