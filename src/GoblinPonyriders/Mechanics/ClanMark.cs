using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

/// <summary>
/// Visible Clan Mark stigma / conditioning overlay (levels 1–5). Slow-burn; Lewd imprints/brands
/// still own fetish tracks — this is the Unified Clans-specific stigma and command bias.
/// </summary>
internal static class ClanMark
{
    public const int Min = 0;
    public const int Max = 5;

    public static int Get(Character character) =>
        Math.Clamp(Traits.GetInt(character, GoblinKeys.ClanMarkLevel), Min, Max);

    public static int Set(Character character, int level, IChangeContext? context = null, string? reason = null)
    {
        var clamped = Math.Clamp(level, Min, Max);
        if (clamped <= 0)
        {
            Traits.Set(character, GoblinKeys.ClanMarkLevel, null);
            ClearStatus(character);
            context?.RecordMessage($"{character.Id} Clan Mark cleared{(reason is null ? "" : $" ({reason})")}.");
            return 0;
        }

        Traits.SetInt(character, GoblinKeys.ClanMarkLevel, clamped);
        StampStatus(character, clamped);
        context?.RecordMessage(
            $"{character.Id} Clan Mark → {clamped}/5 — {EffectSummary(clamped)}{(reason is null ? "" : $" ({reason})")}.");
        return clamped;
    }

    public static int Raise(Character character, int delta, IChangeContext? context = null, string? reason = null) =>
        Set(character, Get(character) + Math.Max(1, delta), context, reason);

    public static string EffectSummary(int level) => level switch
    {
        1 => "visible jagged tattoo; mild arousal near goblin scent; social stigma in towns",
        2 => "glowing mark; stronger stigma; +conditioning speed near Unified Clans territory",
        3 => "WIS checks vs goblin commands; intrusive belonging thoughts",
        4 => "failed saves force minor presents/kneels; any clan rider may leash without contest",
        _ => "deep overlay — treats clan riders as rightful keepers; sabotage risk (player rollback still applies)",
    };

    private static void StampStatus(Character character, int level)
    {
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e =>
            string.Equals(e.AppliedBy, GoblinKeys.AppliedBy, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.ConditionName, GoblinKeys.ConditionClanMark, StringComparison.OrdinalIgnoreCase));
        effects.Add(new StatusEffect
        {
            Name = $"Clan Mark {level}",
            ConditionName = GoblinKeys.ConditionClanMark,
            Category = "Condition",
            AppliedBy = GoblinKeys.AppliedBy,
            RecoveryHint = EffectSummary(level),
        });
    }

    private static void ClearStatus(Character character) =>
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.AppliedBy, GoblinKeys.AppliedBy, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.ConditionName, GoblinKeys.ConditionClanMark, StringComparison.OrdinalIgnoreCase));
}
