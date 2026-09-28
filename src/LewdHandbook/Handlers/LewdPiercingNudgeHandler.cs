using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>
/// On <c>core.pierced.v1</c> with heavy load or bell/leash_ring tags: Message-only nudge toward
/// <c>lewd_humiliate</c> when jewelry is theater. Never auto-drains willpower.
/// </summary>
public sealed class LewdPiercingNudgeHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [CoreEvents.Pierced];

    public Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(CoreEvents.Fields.CharacterId, out var characterId) || string.IsNullOrWhiteSpace(characterId))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);
        if (!e.TryGet<string>(CoreEvents.Fields.Action, out var action) ||
            action is not ("added" or "updated" or "replaced"))
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        var load = e.TryGet<string>(CoreEvents.Fields.Load, out var loadRaw) ? (loadRaw ?? "") : "";
        var tags = ReadTags(e);
        var heavy = string.Equals(load, PiercingLoads.Heavy, StringComparison.OrdinalIgnoreCase);
        var theater = tags.Any(t =>
            string.Equals(t, PiercingTags.Bell, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, PiercingTags.LeashRing, StringComparison.OrdinalIgnoreCase));
        if (!heavy && !theater)
            return Task.FromResult<IReadOnlyList<WorldChange>>([]);

        var site = e.TryGet<string>(CoreEvents.Fields.Site, out var s) ? s : "?";
        var kind = e.TryGet<string>(CoreEvents.Fields.Kind, out var k) ? k : "?";
        ctx.RecordMessage(
            $"{characterId}: {PiercingHelpers.Phrase(new PiercingMark { Site = site ?? "?", Kind = kind ?? "?", Load = load, Tags = tags })} — " +
            "when noticed in public or used as theater, commit lewd_humiliate (do not auto-drain every travel step).");
        ctx.RecordPhysicalStateNudge(
            $"{characterId} wears noticeable piercing jewelry ({site}/{kind}); consider lewd_humiliate if displayed.");
        return Task.FromResult<IReadOnlyList<WorldChange>>([]);
    }

    private static List<string> ReadTags(DomainEvent e)
    {
        if (!e.TryGet<object>(CoreEvents.Fields.Tags, out var raw) || raw is null)
            return [];
        return raw switch
        {
            IEnumerable<string> ss => ss.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(),
            System.Collections.IEnumerable enumerable => enumerable.Cast<object?>()
                .Select(o => o?.ToString())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToList(),
            _ => [],
        };
    }
}

/// <summary>
/// On a real climax, if recent pain/shame flags are set, nudge once per day toward ordeal imprint (LLM commits).
/// </summary>
public sealed class LewdOrdealClimbHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } = [Events.LewdEvents.Climax];

    public async Task<IReadOnlyList<WorldChange>> HandleAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(Events.LewdEvents.Fields.Outcome, out var outcome) || outcome != "climax")
            return [];
        if (!e.TryGet<string>(Events.LewdEvents.Fields.CharacterId, out var characterId) ||
            string.IsNullOrWhiteSpace(characterId) ||
            !ctx.Characters.TryGetValue(characterId, out var character))
            return [];

        var day = await ImprintState.DayAsync(ctx, ct).ConfigureAwait(false);
        OrdealClimb.MaybeNudge(character, day, ctx);
        return [];
    }
}
