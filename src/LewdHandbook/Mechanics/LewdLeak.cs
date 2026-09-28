using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Events;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>Turns internal deposits into core <c>soil</c> when the seal cannot hold them.</summary>
internal static class LewdLeak
{
    /// <summary>
    /// Mutates deposits on the character and returns soil follow-ups (empty when nothing leaks).
    /// Call after occupancy changes or on time/turn ticks.
    /// </summary>
    public static IReadOnlyList<SoilChange> Tick(
        Character character,
        LewdSettings settings,
        string reason,
        int amount = 1,
        string? orificeFilter = null)
    {
        if (!settings.Fluids || !settings.Leaks || !settings.ExternalMarks)
            return [];

        var occupied = OccupancyGraph.Get(null, character);
        var deposits = OccupancyGraph.GetDeposits(character);
        if (deposits.Count == 0)
            return [];

        var soils = new List<SoilChange>();
        var remaining = new List<InternalDeposit>();
        foreach (var deposit in deposits)
        {
            if (orificeFilter is not null &&
                !string.Equals(deposit.Orifice, OccupancyGraph.NormalizeOrifice(orificeFilter), StringComparison.OrdinalIgnoreCase))
            {
                remaining.Add(deposit);
                continue;
            }

            var seal = OccupancyGraph.SealFor(occupied, deposit.Orifice);
            // Plugged holds; beaded only leaks on vigorous ticks (travel / unplug / bead pull).
            if (seal == LewdKeys.SealPlugged ||
                (seal == LewdKeys.SealBeaded && reason is "turn"))
            {
                remaining.Add(deposit);
                continue;
            }

            var lost = Math.Min(deposit.Severity, Math.Max(1, amount));
            soils.Add(new SoilChange
            {
                TargetId = character.Id,
                Kind = LewdKeys.DirtKindCum,
                Amount = lost,
                Spot = deposit.Orifice is "mouth" ? DirtSpots.Face : "thighs",
                AppliedBy = $"lewd_leak:{deposit.Orifice}",
                Note = reason,
            });

            var left = deposit.Severity - lost;
            if (left > 0)
            {
                deposit.Severity = left;
                remaining.Add(deposit);
            }
        }

        OccupancyGraph.SetDeposits(character, remaining);
        Occupancy.Sync(character);
        return soils;
    }

    public static void RecordLeakMessages(IChangeContext context, Character character, IReadOnlyList<SoilChange> soils, string reason)
    {
        if (soils.Count == 0)
            return;
        context.RecordMessage(
            $"{character.Id} leaks ({reason}): {string.Join(", ", soils.Select(s => $"{s.Spot} +{s.Amount}"))}.");
        context.RecordPhysicalStateNudge(
            $"{character.Id} is leaking; core soil follow-ups applied.");
    }

    public static void PublishSoils(IChangeContext context, string characterId, IReadOnlyList<SoilChange> soils)
    {
        if (soils.Count == 0)
            return;
        context.Publish(
            Events.LewdEvents.Leak,
            new
            {
                characterId,
                action = "leak",
                soils = soils.Select(s => new
                {
                    targetId = s.TargetId,
                    kind = s.Kind,
                    amount = s.Amount,
                    spot = s.Spot,
                    appliedBy = s.AppliedBy,
                    note = s.Note,
                }).ToArray(),
            });
    }
}
