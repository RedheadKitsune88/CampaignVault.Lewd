using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Maps a climax deposit onto core <see cref="SoilChange"/>s (and optional pregnancy follow-ups).
/// External mess only: sealed insides / plugs are a later layer.
/// </summary>
internal static class LewdDirt
{
    public static async Task<IReadOnlyList<WorldChange>> FromClimaxAsync(
        DomainEvent e,
        IChangeContext ctx,
        CancellationToken ct = default)
    {
        if (!e.TryGet<string>(Events.LewdEvents.Fields.Outcome, out var outcome) || outcome != "climax")
            return [];
        if (!e.TryGet<string>(Events.LewdEvents.Fields.CharacterId, out var climaxId) || string.IsNullOrWhiteSpace(climaxId))
            return [];

        var settings = await LewdSettings.ResolveAsync(ctx, ct).ConfigureAwait(false);
        if (!settings.Fluids)
            return [];

        if (e.TryGet<bool>(Events.LewdEvents.Fields.Physical, out var physical) && !physical)
            return [];

        e.TryGet<string>(Events.LewdEvents.Fields.Finish, out var finish);
        finish = string.IsNullOrWhiteSpace(finish) ? null : finish.Trim().ToLowerInvariant();
        if (finish is null || finish == LewdKeys.FinishNone)
            return [];

        e.TryGet<string>(Events.LewdEvents.Fields.TargetAnatomy, out var anatomy);
        anatomy = string.IsNullOrWhiteSpace(anatomy) ? null : anatomy.Trim().ToLowerInvariant();
        e.TryGet<string>(Events.LewdEvents.Fields.DepositOnId, out var depositOn);
        depositOn = string.IsNullOrWhiteSpace(depositOn) ? climaxId : depositOn.Trim();
        e.TryGet<string>(Events.LewdEvents.Fields.SourceId, out var sourceId);

        if (!ctx.Characters.TryGetValue(depositOn, out var depositChar))
            return [];

        var participant = LewdModeAccess.TryGetParticipant(ctx, depositOn)
                         ?? new ModeParticipantState { CharacterId = depositOn };
        var probe = LewdSettings.FluidHardLimitTags.Concat(anatomy is null ? [] : new[] { anatomy });
        if (settings.HitsHardLimit(probe, out _))
            return [];
        if (LewdProfile.HardLimits(participant, depositChar)
                .Any(limit => probe.Contains(limit, StringComparer.OrdinalIgnoreCase)))
            return [];

        var changes = new List<WorldChange>();
        var orifice = OccupancyGraph.NormalizeOrifice(anatomy);
        var occupied = OccupancyGraph.Get(participant, depositChar);
        var seal = OccupancyGraph.SealFor(occupied, orifice);
        var day = (int)((await ctx.GetCurrentTimeAsync().ConfigureAwait(false)).TotalDaysElapsed);

        if (finish == LewdKeys.FinishInside)
        {
            var deposits = OccupancyGraph.GetDeposits(depositChar);
            OccupancyGraph.AddDeposit(deposits, orifice, amount: 2, sourceId: climaxId, day: day);
            OccupancyGraph.SetDeposits(depositChar, deposits);
            Occupancy.Sync(depositChar);
            ctx.RecordMessage($"{depositOn}: internal deposit at {orifice} (seal={seal}).");
        }

        // External soil now, unless an inside finish is held by a plug/beads seal.
        var sealedInside = finish == LewdKeys.FinishInside && OccupancyGraph.HoldsFluids(seal);
        if (settings.ExternalMarks && !sealedInside)
        {
            var (spot, amount) = SpotFor(finish, anatomy);
            changes.Add(new SoilChange
            {
                TargetId = depositOn,
                Kind = LewdKeys.DirtKindCum,
                Amount = amount,
                Spot = spot,
                AppliedBy = $"lewd_climax:{climaxId}",
                Note = finish == LewdKeys.FinishInside ? "overflow" : "climax",
            });
        }
        else if (sealedInside)
        {
            ctx.RecordPhysicalStateNudge(
                $"{depositOn}: finish held by {seal} seal at {orifice}; will leak on removal/travel unless cleaned.");
        }

        if (finish == LewdKeys.FinishInside &&
            settings.CreampiePregnancy != LewdCreampiePregnancy.Off &&
            IsBreedableSite(anatomy))
        {
            if (settings.CreampiePregnancy == LewdCreampiePregnancy.Prompt)
            {
                ctx.RecordMessage(
                    $"{depositOn}: finished inside ({anatomy ?? "unspecified"}). Emit lewd_pregnancy action=impregnate if the fiction calls for it.");
                ctx.RecordPhysicalStateNudge(
                    $"lewdCreampiePregnancy=prompt: {climaxId} finished inside {depositOn}; consider lewd_pregnancy.");
            }
            else
            {
                changes.Add(new LewdPregnancyChange
                {
                    TargetId = depositOn,
                    ActorId = climaxId,
                    Action = "impregnate",
                    Kind = "traditional",
                    Notes = "auto from lewdCreampiePregnancy after inside finish",
                });
            }
        }

        return changes;
    }

    public static async Task MaybeNudgeFluidsViceAsync(DomainEvent e, IChangeContext ctx, CancellationToken ct = default)
    {
        if (!e.TryGet<string>(CoreEvents.Fields.Kind, out var kind) ||
            kind is null ||
            !kind.StartsWith("lewd.", StringComparison.OrdinalIgnoreCase))
            return;
        if (!e.TryGet<string>(CoreEvents.Fields.Action, out var action) ||
            action is not ("applied" or "worsened"))
            return;
        if (!e.TryGet<string>(CoreEvents.Fields.TargetId, out var targetId) || string.IsNullOrWhiteSpace(targetId))
            return;

        var settings = await LewdSettings.ResolveAsync(ctx, ct).ConfigureAwait(false);
        if (!settings.Fluids || !settings.FluidViceHook)
            return;
        if (!ctx.Characters.TryGetValue(targetId, out _))
            return;

        e.TryGet<string>(CoreEvents.Fields.Spot, out var spot);
        var where = string.IsNullOrWhiteSpace(spot) ? "on them" : $"on their {spot}";
        var msg =
            $"{targetId} carries visible {SoilHelpers.DisplayKind(kind)} {where}. " +
            "If sexual_fluids vice is in play, emit lewd_vice action=note_presence (or consume).";
        ctx.RecordMessage(msg);
        ctx.RecordPhysicalStateNudge(msg);
    }

    private static (string Spot, int Amount) SpotFor(string finish, string? anatomy)
    {
        if (finish == LewdKeys.FinishInside)
        {
            // Internal finish: what shows is overflow on the thighs unless the named site is already external.
            return anatomy switch
            {
                "face" or "mouth" or "lips" or "cheek" => (DirtSpots.Face, 2),
                "hair" or "braid" or "scalp" => (DirtSpots.Hair, 2),
                "hands" or "hand" or "fingers" => (DirtSpots.Hands, 2),
                _ => ("thighs", 2),
            };
        }

        var spot = MapSpot(anatomy) ?? DirtSpots.Clothes;
        var amount = anatomy is "face" or "mouth" or "hair" ? 2 : 1;
        return (spot, amount);
    }

    private static string? MapSpot(string? anatomy) => anatomy switch
    {
        null or "" => null,
        "face" or "mouth" or "lips" or "cheek" => DirtSpots.Face,
        "hair" or "braid" or "scalp" => DirtSpots.Hair,
        "hands" or "hand" or "fingers" => DirtSpots.Hands,
        "chest" or "breasts" or "breast" or "tits" => DirtSpots.Chest,
        "back" => DirtSpots.Back,
        "clothes" or "dress" or "armor" => DirtSpots.Clothes,
        "thighs" or "thigh" or "legs" => "thighs",
        "belly" or "stomach" or "abdomen" => "belly",
        "pussy" or "vagina" or "cunt" or "ass" or "anus" or "cock" or "dick" => DirtSpots.Clothes,
        _ => anatomy,
    };

    private static bool IsBreedableSite(string? anatomy) => anatomy is
        "pussy" or "vagina" or "cunt" or "womb" or null or "";
}
