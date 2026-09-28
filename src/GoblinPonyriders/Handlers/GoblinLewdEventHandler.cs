using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using GoblinPonyriders.Events;
using GoblinPonyriders.Mechanics;

namespace GoblinPonyriders.Handlers;

/// <summary>
/// When a captive is under Unified Clans Capture State, LewdHandbook events nudge Defiance / Clan Mark.
/// Subscribes by string topic — no project reference to LewdHandbook.
/// </summary>
public sealed class GoblinLewdEventHandler : IDomainEventHandler
{
    public IReadOnlyCollection<string> Topics { get; } =
    [
        GoblinEvents.LewdTopics.BindingChanged,
        GoblinEvents.LewdTopics.ImprintChanged,
        GoblinEvents.LewdTopics.Climax,
        GoblinEvents.LewdTopics.BadEnd,
        GoblinEvents.LewdTopics.Humiliated,
    ];

    public async Task<IReadOnlyList<WorldChange>> HandleAsync(
        DomainEvent e,
        IChangeContext ctx,
        CancellationToken ct = default)
    {
        var settings = await GoblinSettings.ResolveAsync(ctx, ct).ConfigureAwait(false);
        if (!settings.Enabled)
            return [];

        if (!e.TryGet<string>(GoblinEvents.Fields.CharacterId, out var characterId) ||
            string.IsNullOrWhiteSpace(characterId))
        {
            // Some Lewd payloads use targetId for imprint.
            if (!e.TryGet("targetId", out characterId) || string.IsNullOrWhiteSpace(characterId))
                return [];
        }

        if (!ctx.Characters.TryGetValue(characterId, out var character))
            return [];
        if (!CaptureState.IsCaptured(character))
            return [];

        if (settings.HitsHardLimit(GoblinKeys.HardLimitProbe, out _))
            return [];

        switch (e.Topic)
        {
            case GoblinEvents.LewdTopics.BindingChanged:
                // Fresh restraints during capture erode defiance slightly.
                DefianceClock.Adjust(character, -1, ctx, "clan binding pressure");
                break;

            case GoblinEvents.LewdTopics.ImprintChanged:
                {
                    var level = ClanMark.Get(character);
                    if (level > 0 && level < ClanMark.Max)
                        ClanMark.Raise(character, 1, ctx, "imprint while marked");
                    DefianceClock.Adjust(character, -1, ctx, "imprint under capture");
                    break;
                }

            case GoblinEvents.LewdTopics.Climax:
                {
                    var forced = e.TryGet<bool>("forced", out var f) && f;
                    DefianceClock.Adjust(character, forced ? -1 : 0, ctx, forced ? "forced climax under capture" : null);
                    break;
                }

            case GoblinEvents.LewdTopics.BadEnd:
                DefianceClock.Set(character, 1, ctx, "bad end under clans");
                if (ClanMark.Get(character) < 3)
                    ClanMark.Set(character, 3, ctx, "bad end branding");
                break;

            case GoblinEvents.LewdTopics.Humiliated:
                {
                    // Public shame under capture hardens resistance until broken — severity ≥2 raises Defiance.
                    var severity = e.TryGet<int>("severity", out var sev) ? sev : 0;
                    if (severity >= 2)
                        DefianceClock.Adjust(character, 1, ctx, "public humiliation under capture");
                    break;
                }
        }

        return [];
    }
}
