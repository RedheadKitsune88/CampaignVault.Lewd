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
        CancellationToken ct,
        bool advantage = false,
        Character? who = null,
        string? subject = null,
        string kind = RollKinds.Save,
        IReadOnlyCollection<string>? tags = null)
    {
        // Run the roll through the host's pipeline (status effects, willpower, this plugin's provider) when we know whose it is:
        // "Dead arms" and a broken will then touch the plugin's own rolls too. Without `who` the caller's numbers stand.
        var reasons = "";
        if (who is not null)
        {
            var options = context.GetSystemOptionsAsync is { } load ? await load().ConfigureAwait(false) : new Dictionary<string, string>();
            var query = new RollQuery(kind, subject, tags ?? [], who, null, context.Config?.ActiveSystem ?? "dnd5e", options);
            var explicitEffect = disadvantage == advantage ? AdvantageEffect.None
                : disadvantage ? AdvantageEffect.Disadvantage : AdvantageEffect.Advantage;
            var resolved = context.ResolveRollModifiers(query, abilityMod, explicitEffect);
            abilityMod = resolved.Bonus;
            disadvantage = resolved.Advantage == AdvantageEffect.Disadvantage;
            advantage = resolved.Advantage == AdvantageEffect.Advantage;
            if (resolved.Notes.Count > 0)
                reasons = $" [{string.Join("; ", resolved.Notes)}]";
        }

        if (faceOrZero is >= 1 and <= 20)
        {
            var total = faceOrZero + abilityMod;
            return (faceOrZero, total, $"{faceOrZero}+{abilityMod}={total}{reasons}", null);
        }

        if (context.Rolls is null || faceOrZero != 0)
            return (0, 0, "", "d20 must be between 1 and 20 (or 0 with Rolls to auto-roll).");

        var roll = await context.Rolls.RollAsync(
            new RollRequest
            {
                Tag = tag,
                Expression = "1d20",
                Bonus = abilityMod,
                Mechanic = disadvantage == advantage ? DiceMechanic.Standard
                    : disadvantage ? DiceMechanic.Disadvantage : DiceMechanic.Advantage,
            },
            ct).ConfigureAwait(false);

        var face = roll.IndividualDice.FirstOrDefault();
        if (face is < 1 or > 20)
            face = Math.Clamp(roll.Result - abilityMod, 1, 20);
        var totalRolled = face + abilityMod;
        var summary = roll.Summary;
        if (string.IsNullOrWhiteSpace(summary))
            summary = $"{face}+{abilityMod}={totalRolled}";
        summary += reasons;
        context.RecordMessage($"Lewd save d20 ({tag}): {summary}.");
        return (face, totalRolled, summary, null);
    }
}
