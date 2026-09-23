using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class SaveDice
{
    public static async Task<(int Face, int Total, string Summary, string? Error)> RollAsync(
        IChangeContext context,
        string tag,
        int faceOrZero,
        int abilityMod,
        bool disadvantage,
        CancellationToken ct)
    {
        if (faceOrZero is >= 1 and <= 20)
        {
            var total = faceOrZero + abilityMod;
            return (faceOrZero, total, $"{faceOrZero}+{abilityMod}={total}", null);
        }

        if (context.Rolls is null || faceOrZero != 0)
            return (0, 0, "", "d20 must be between 1 and 20 (or 0 with Rolls to auto-roll).");

        var roll = await context.Rolls.RollAsync(
            new RollRequest
            {
                Tag = tag,
                Expression = "1d20",
                Bonus = abilityMod,
                Mechanic = disadvantage ? DiceMechanic.Disadvantage : DiceMechanic.Standard,
            },
            ct).ConfigureAwait(false);

        var face = roll.IndividualDice.FirstOrDefault();
        if (face is < 1 or > 20)
            face = Math.Clamp(roll.Result - abilityMod, 1, 20);
        var totalRolled = face + abilityMod;
        var summary = roll.Summary;
        if (string.IsNullOrWhiteSpace(summary))
            summary = $"{face}+{abilityMod}={totalRolled}";
        context.RecordMessage($"Lewd save d20 ({tag}): {summary}.");
        return (face, totalRolled, summary, null);
    }
}
