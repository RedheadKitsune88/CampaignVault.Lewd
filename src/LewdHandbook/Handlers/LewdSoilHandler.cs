using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>
/// On a real climax with a known finish site and <c>lewdFluids=on</c>, returns core <c>soil</c> follow-ups
/// (and optional pregnancy when configured).
/// </summary>
public sealed class LewdSoilHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [Events.LewdEvents.Climax];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default) =>
        LewdDirt.FromClimaxAsync(e, ctx, ct);
}

/// <summary>
/// Visible <c>lewd.*</c> dirt nudges the sexual_fluids vice loop when <c>lewdFluidViceHook</c> is on.
/// </summary>
public sealed class LewdFluidViceSoilHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [CoreEvents.Soiled];

    public async Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        await LewdDirt.MaybeNudgeFluidsViceAsync(e, ctx, ct).ConfigureAwait(false);
        return [];
    }
}
