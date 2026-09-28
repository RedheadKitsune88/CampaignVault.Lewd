using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>Travel and time passage can force a leak through a beaded or open seal.</summary>
public sealed class LewdOccupancyTravelHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } =
    [
        CoreEvents.Traveled,
    ];

    public async Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(CoreEvents.Fields.CharacterId, out var id) || string.IsNullOrWhiteSpace(id))
            return [];
        if (!ctx.Characters.TryGetValue(id, out var character))
            return [];

        var settings = await LewdSettings.ResolveAsync(ctx, ct).ConfigureAwait(false);
        var soils = LewdLeak.Tick(character, settings, "travel", amount: 1);
        LewdLeak.RecordLeakMessages(ctx, character, soils, "travel");
        return soils.Cast<WorldChange>().ToList();
    }
}
