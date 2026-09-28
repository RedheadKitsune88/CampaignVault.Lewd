using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using GoblinPonyriders.Events;
using GoblinPonyriders.Mechanics;

namespace GoblinPonyriders.Handlers;

/// <summary>
/// Role / naming / terror-release stamp clan piercing kits via core <c>piercing</c> follow-ups.
/// </summary>
public sealed class GoblinPiercingHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } =
    [
        GoblinEvents.Role,
        GoblinEvents.Release,
        GoblinEvents.Name,
    ];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(
        DomainEvent e,
        IChangeContext ctx,
        CancellationToken ct = default)
    {
        if (!e.TryGet<string>(GoblinEvents.Fields.CharacterId, out var characterId) ||
            string.IsNullOrWhiteSpace(characterId))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        switch (e.Topic)
        {
            case GoblinEvents.Name:
                ctx.RecordMessage($"{characterId}: naming ritual stamps locked iron ear notch.");
                return Task.FromResult<IReadOnlyList<WorldChange>>([GoblinPiercingKits.NamingEar(characterId)]);

            case GoblinEvents.Release:
                {
                    if (!e.TryGet<string>(GoblinEvents.Fields.Action, out var action) ||
                        !string.Equals(action, "terror_release", StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<IReadOnlyList<WorldChange>>([]);
                    ctx.RecordMessage(
                        $"{characterId}: terror-release leaves a locked heavy nose ring (core piercing) and a rumor seed.");
                    return Task.FromResult<IReadOnlyList<WorldChange>>([GoblinPiercingKits.TerrorMark(characterId)]);
                }

            case GoblinEvents.Role:
                {
                    if (!e.TryGet<string>(GoblinEvents.Fields.Role, out var role) || string.IsNullOrWhiteSpace(role))
                        return Task.FromResult<IReadOnlyList<WorldChange>>([]);
                    if (string.Equals(role, RoleCatalog.RidingGirl.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.RecordMessage($"{characterId}: Riding-Girl kit adds septum lead ring (leashable).");
                        return Task.FromResult<IReadOnlyList<WorldChange>>([GoblinPiercingKits.RidingSeptum(characterId)]);
                    }

                    if (string.Equals(role, RoleCatalog.Entertainer.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.RecordMessage($"{characterId}: Entertainer kit adds weighted iron bells on both nipples.");
                        return Task.FromResult<IReadOnlyList<WorldChange>>(GoblinPiercingKits.EntertainerBells(characterId).ToList());
                    }

                    return Task.FromResult<IReadOnlyList<WorldChange>>([]);
                }

            default:
                return Task.FromResult<IReadOnlyList<WorldChange>>([]);
        }
    }
}
