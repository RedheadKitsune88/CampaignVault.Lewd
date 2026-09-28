using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>Derives StatusEffects from occupancy + internal deposits (source of truth is Trait JSON).</summary>
internal static class Occupancy
{
    public const string AppliedBy = "lewd_occupancy";
    public const string DepositAppliedBy = "lewd_creampie";
    public const string SummaryName = "Occupied";
    public const string FilledName = "Filled";

    public static void Sync(Character character)
    {
        var occupied = OccupancyGraph.Get(null, character);
        var deposits = OccupancyGraph.GetDeposits(character);
        var effects = character.SystemStats.StatusEffects;

        effects.RemoveAll(e =>
            string.Equals(e.AppliedBy, AppliedBy, StringComparison.Ordinal) ||
            string.Equals(e.AppliedBy, DepositAppliedBy, StringComparison.Ordinal));

        foreach (var entry in occupied)
        {
            effects.Add(new StatusEffect
            {
                Name = LabelFor(entry),
                Category = "Condition",
                ConditionName = entry.Kind is LewdKeys.OccupancyPlug or LewdKeys.OccupancyBeads ? "plugged" : "filled",
                AppliedBy = AppliedBy,
                RecoveryHint =
                    $"{entry.Orifice} occupied by {entry.Kind}" +
                    (entry.ItemId is null ? "" : $" ({entry.ItemId})") +
                    $", seal={entry.Seal}. Free with lewd_remove.",
            });
        }

        if (occupied.Count > 0)
        {
            effects.Add(new StatusEffect
            {
                Name = SummaryName,
                Category = "Condition",
                AppliedBy = AppliedBy,
                RecoveryHint = string.Join("; ", occupied.Select(e =>
                    $"{e.Orifice}:{e.Kind}/{e.Seal}" + (e.ItemId is null ? "" : $"@{e.ItemId}"))),
            });
        }

        if (deposits.Count > 0)
        {
            effects.Add(new StatusEffect
            {
                Name = FilledName,
                Category = "Condition",
                ConditionName = "filled",
                AppliedBy = DepositAppliedBy,
                RecoveryHint =
                    "Internal deposit: " +
                    string.Join(", ", deposits.Select(d => $"{d.Orifice}×{d.Severity}")) +
                    ". Leaks when the seal opens, on travel/turn, or lewd_remove.",
            });
        }
    }

    private static string LabelFor(OccupancyEntry e) => e.Kind switch
    {
        LewdKeys.OccupancyPlug => "Plugged",
        LewdKeys.OccupancyBeads => "Beads seated",
        LewdKeys.OccupancyWand => "Wand seated",
        LewdKeys.OccupancyPartner => "Filled",
        _ => "Occupied",
    };
}
